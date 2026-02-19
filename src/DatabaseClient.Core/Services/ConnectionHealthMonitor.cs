using System.Collections.Concurrent;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using Serilog;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Monitors active database connections and attempts automatic reconnection
/// with exponential backoff when a connection is lost.
/// </summary>
public sealed class ConnectionHealthMonitor : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<ConnectionHealthMonitor>();

    private readonly IConnectionManager _connectionManager;
    private readonly IConnectionRepository _connectionRepository;
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly ConcurrentDictionary<Guid, ConnectionHealthState> _states = new();
    private Timer? _pingTimer;
    private bool _disposed;

    /// <summary>Interval between health checks (default: 30 seconds).</summary>
    public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum reconnection attempts before giving up (default: 10).</summary>
    public int MaxReconnectAttempts { get; set; } = 10;

    /// <summary>Maximum backoff time between reconnection attempts (default: 60 seconds).</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Raised when a connection's health status changes.</summary>
    public event EventHandler<ConnectionHealthEventArgs>? HealthStatusChanged;

    public ConnectionHealthMonitor(
        IConnectionManager connectionManager,
        IConnectionRepository connectionRepository,
        IDatabaseProviderFactory providerFactory)
    {
        _connectionManager = connectionManager;
        _connectionRepository = connectionRepository;
        _providerFactory = providerFactory;
    }

    /// <summary>
    /// Starts the periodic health check loop.
    /// </summary>
    public void Start()
    {
        if (_pingTimer is not null) return;
        Logger.Information("Connection health monitor started (interval: {Interval})", PingInterval);
        _pingTimer = new Timer(OnPingTimerTick, null, PingInterval, PingInterval);
    }

    /// <summary>
    /// Stops the health check loop.
    /// </summary>
    public void Stop()
    {
        _pingTimer?.Dispose();
        _pingTimer = null;
        Logger.Information("Connection health monitor stopped");
    }

    /// <summary>
    /// Registers a connection for health monitoring.
    /// </summary>
    public void Track(Guid connectionId)
    {
        _states.TryAdd(connectionId, new ConnectionHealthState
        {
            ConnectionId = connectionId,
            Status = ConnectionHealthStatus.Healthy,
            LastCheckTime = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Removes a connection from health monitoring.
    /// </summary>
    public void Untrack(Guid connectionId)
    {
        _states.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Gets the current health status of a tracked connection.
    /// </summary>
    public ConnectionHealthStatus GetStatus(Guid connectionId)
    {
        return _states.TryGetValue(connectionId, out var state) ? state.Status : ConnectionHealthStatus.Unknown;
    }

    private async void OnPingTimerTick(object? state)
    {
        if (_disposed) return;

        var activeIds = _connectionManager.GetActiveConnectionIds();

        foreach (var connectionId in activeIds)
        {
            if (_disposed) return;

            // Auto-track new connections
            if (!_states.ContainsKey(connectionId))
                Track(connectionId);
        }

        // Check all tracked connections
        foreach (var kvp in _states)
        {
            if (_disposed) return;
            await CheckConnectionHealthAsync(kvp.Key, kvp.Value);
        }
    }

    private async Task CheckConnectionHealthAsync(Guid connectionId, ConnectionHealthState healthState)
    {
        try
        {
            var isConnected = _connectionManager.IsConnected(connectionId);
            healthState.LastCheckTime = DateTime.UtcNow;

            if (isConnected)
            {
                if (healthState.Status != ConnectionHealthStatus.Healthy)
                {
                    Logger.Information("Connection {ConnectionId} is healthy again", connectionId);
                    healthState.Status = ConnectionHealthStatus.Healthy;
                    healthState.ReconnectAttempts = 0;
                    RaiseStatusChanged(connectionId, ConnectionHealthStatus.Healthy, "Connection restored");
                }
                return;
            }

            // Connection lost
            if (healthState.Status == ConnectionHealthStatus.Healthy)
            {
                Logger.Warning("Connection {ConnectionId} lost", connectionId);
                healthState.Status = ConnectionHealthStatus.Reconnecting;
                healthState.ReconnectAttempts = 0;
                RaiseStatusChanged(connectionId, ConnectionHealthStatus.Reconnecting, "Connection lost. Reconnecting...");
            }

            // Attempt reconnection with exponential backoff
            if (healthState.ReconnectAttempts >= MaxReconnectAttempts)
            {
                if (healthState.Status != ConnectionHealthStatus.Failed)
                {
                    healthState.Status = ConnectionHealthStatus.Failed;
                    RaiseStatusChanged(connectionId, ConnectionHealthStatus.Failed,
                        $"Connection lost. Reconnection failed after {MaxReconnectAttempts} attempts.");
                    Logger.Error("Connection {ConnectionId} failed to reconnect after {Attempts} attempts",
                        connectionId, MaxReconnectAttempts);
                }
                return;
            }

            // Calculate backoff: 1s, 2s, 4s, 8s... up to MaxBackoff
            var backoff = TimeSpan.FromSeconds(Math.Min(
                Math.Pow(2, healthState.ReconnectAttempts),
                MaxBackoff.TotalSeconds));

            if (DateTime.UtcNow - healthState.LastReconnectAttempt < backoff)
                return; // Not time yet

            healthState.ReconnectAttempts++;
            healthState.LastReconnectAttempt = DateTime.UtcNow;

            Logger.Information("Attempting reconnection {Attempt}/{Max} for {ConnectionId} (backoff: {Backoff}s)",
                healthState.ReconnectAttempts, MaxReconnectAttempts, connectionId, backoff.TotalSeconds);

            // Try to get the connection info and reconnect
            var connections = await _connectionRepository.GetAllAsync();
            var connInfo = connections.FirstOrDefault(c => c.Id == connectionId);
            if (connInfo is not null)
            {
                try
                {
                    await _connectionManager.GetProviderAsync(connInfo);
                    healthState.Status = ConnectionHealthStatus.Healthy;
                    healthState.ReconnectAttempts = 0;
                    RaiseStatusChanged(connectionId, ConnectionHealthStatus.Healthy, "Reconnected successfully");
                    Logger.Information("Connection {ConnectionId} reconnected successfully", connectionId);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Reconnection attempt {Attempt} failed for {ConnectionId}",
                        healthState.ReconnectAttempts, connectionId);
                    RaiseStatusChanged(connectionId, ConnectionHealthStatus.Reconnecting,
                        $"Reconnecting... (attempt {healthState.ReconnectAttempts}/{MaxReconnectAttempts})");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error checking health for connection {ConnectionId}", connectionId);
        }
    }

    private void RaiseStatusChanged(Guid connectionId, ConnectionHealthStatus status, string message)
    {
        HealthStatusChanged?.Invoke(this, new ConnectionHealthEventArgs(connectionId, status, message));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _states.Clear();
    }
}

/// <summary>
/// Internal state tracking for a monitored connection.
/// </summary>
internal class ConnectionHealthState
{
    public Guid ConnectionId { get; set; }
    public ConnectionHealthStatus Status { get; set; }
    public DateTime LastCheckTime { get; set; }
    public DateTime LastReconnectAttempt { get; set; }
    public int ReconnectAttempts { get; set; }
}

/// <summary>
/// Health status of a monitored connection.
/// </summary>
public enum ConnectionHealthStatus
{
    Unknown,
    Healthy,
    Reconnecting,
    Failed
}

/// <summary>
/// Event args for connection health status changes.
/// </summary>
public class ConnectionHealthEventArgs : EventArgs
{
    public Guid ConnectionId { get; }
    public ConnectionHealthStatus Status { get; }
    public string Message { get; }

    public ConnectionHealthEventArgs(Guid connectionId, ConnectionHealthStatus status, string message)
    {
        ConnectionId = connectionId;
        Status = status;
        Message = message;
    }
}

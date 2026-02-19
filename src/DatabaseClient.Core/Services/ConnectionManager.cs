using System.Collections.Concurrent;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Manages active database connections across the application.
/// </summary>
public class ConnectionManager : IConnectionManager
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly ConcurrentDictionary<Guid, IDatabaseProvider> _activeProviders = new();

    public event EventHandler<ConnectionEventArgs>? ConnectionOpened;
    public event EventHandler<ConnectionEventArgs>? ConnectionClosed;
    public event EventHandler<ConnectionErrorEventArgs>? ConnectionError;

    public ConnectionManager(IDatabaseProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public async Task<IDatabaseProvider> GetProviderAsync(ConnectionInfo connectionInfo, CancellationToken ct = default)
    {
        if (_activeProviders.TryGetValue(connectionInfo.Id, out var existing) && existing.IsConnected)
            return existing;

        var provider = _providerFactory.Create(connectionInfo.DatabaseType);

        try
        {
            await provider.OpenAsync(connectionInfo, ct);
            _activeProviders[connectionInfo.Id] = provider;
            ConnectionOpened?.Invoke(this, new ConnectionEventArgs(connectionInfo));
            return provider;
        }
        catch (Exception ex)
        {
            ConnectionError?.Invoke(this, new ConnectionErrorEventArgs(connectionInfo, ex));
            throw;
        }
    }

    public async Task CloseConnectionAsync(Guid connectionId)
    {
        if (_activeProviders.TryRemove(connectionId, out var provider))
        {
            await provider.CloseAsync();
            await provider.DisposeAsync();
        }
    }

    public async Task CloseAllAsync()
    {
        foreach (var kvp in _activeProviders)
        {
            try
            {
                await kvp.Value.CloseAsync();
                await kvp.Value.DisposeAsync();
            }
            catch { /* Best effort */ }
        }
        _activeProviders.Clear();
    }

    public bool IsConnected(Guid connectionId)
    {
        return _activeProviders.TryGetValue(connectionId, out var provider) && provider.IsConnected;
    }

    public IReadOnlyList<Guid> GetActiveConnectionIds()
    {
        return _activeProviders.Keys.ToList().AsReadOnly();
    }
}

using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Manages active database connections across the application.
/// </summary>
public interface IConnectionManager
{
    event EventHandler<ConnectionEventArgs>? ConnectionOpened;
    event EventHandler<ConnectionEventArgs>? ConnectionClosed;
    event EventHandler<ConnectionErrorEventArgs>? ConnectionError;

    /// <summary>Gets an active provider for the specified connection, opening it if necessary.</summary>
    Task<IDatabaseProvider> GetProviderAsync(ConnectionInfo connectionInfo, CancellationToken ct = default);

    /// <summary>Closes a specific connection.</summary>
    Task CloseConnectionAsync(Guid connectionId);

    /// <summary>Closes all active connections.</summary>
    Task CloseAllAsync();

    /// <summary>Returns true if the specified connection is currently open.</summary>
    bool IsConnected(Guid connectionId);

    /// <summary>Returns all currently active connection IDs.</summary>
    IReadOnlyList<Guid> GetActiveConnectionIds();
}

public class ConnectionEventArgs(ConnectionInfo connectionInfo) : EventArgs
{
    public ConnectionInfo ConnectionInfo { get; } = connectionInfo;
}

public class ConnectionErrorEventArgs(ConnectionInfo connectionInfo, Exception exception) : EventArgs
{
    public ConnectionInfo ConnectionInfo { get; } = connectionInfo;
    public Exception Exception { get; } = exception;
}

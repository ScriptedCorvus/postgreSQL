using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Renci.SshNet;
using SshConnectionInfo = Renci.SshNet.ConnectionInfo;
using ConnectionInfo = DatabaseClient.Core.Models.ConnectionInfo;

namespace DatabaseClient.Data.Services;

/// <summary>
/// Manages SSH tunnels for database connections. Creates a local forwarded port
/// that routes traffic through the SSH server to the database host.
/// </summary>
public class SshTunnelManager : IDisposable
{
    private readonly ConcurrentDictionary<Guid, SshTunnelInfo> _activeTunnels = new();

    /// <summary>
    /// Opens an SSH tunnel for the given connection info. Returns the local port
    /// that should be used instead of the original host:port when connecting to the database.
    /// </summary>
    public async Task<(string LocalHost, int LocalPort)> OpenTunnelAsync(ConnectionInfo connectionInfo, CancellationToken ct = default)
    {
        if (!connectionInfo.UseSshTunnel)
            throw new InvalidOperationException("SSH tunnel is not enabled for this connection.");

        // If tunnel already exists and is active, return it
        if (_activeTunnels.TryGetValue(connectionInfo.Id, out var existing) && existing.Client.IsConnected)
        {
            return ("127.0.0.1", (int)existing.ForwardedPort.BoundPort);
        }

        // Build authentication methods
        var authMethods = new List<AuthenticationMethod>();

        if (!string.IsNullOrEmpty(connectionInfo.SshPrivateKeyPath) && System.IO.File.Exists(connectionInfo.SshPrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(connectionInfo.SshPassword)
                ? new PrivateKeyFile(connectionInfo.SshPrivateKeyPath)
                : new PrivateKeyFile(connectionInfo.SshPrivateKeyPath, connectionInfo.SshPassword);
            authMethods.Add(new PrivateKeyAuthenticationMethod(connectionInfo.SshUsername, keyFile));
        }

        if (!string.IsNullOrEmpty(connectionInfo.SshPassword))
        {
            authMethods.Add(new PasswordAuthenticationMethod(connectionInfo.SshUsername, connectionInfo.SshPassword));
        }

        if (authMethods.Count == 0)
            throw new InvalidOperationException("SSH authentication requires a password or private key.");

        var connectionInfoSsh = new SshConnectionInfo(
            connectionInfo.SshHost,
            connectionInfo.SshPort,
            connectionInfo.SshUsername,
            authMethods.ToArray());

        var client = new SshClient(connectionInfoSsh);

        await Task.Run(() => client.Connect(), ct);

        if (!client.IsConnected)
            throw new InvalidOperationException($"Failed to connect to SSH server {connectionInfo.SshHost}:{connectionInfo.SshPort}");

        // Find a free local port
        var localPort = GetFreePort();

        // Create forwarded port: local port → remote db host:port
        var forwardedPort = new ForwardedPortLocal(
            IPAddress.Loopback.ToString(),
            (uint)localPort,
            connectionInfo.Host,
            (uint)connectionInfo.Port);

        client.AddForwardedPort(forwardedPort);
        forwardedPort.Start();

        var tunnelInfo = new SshTunnelInfo(client, forwardedPort, connectionInfo.Id);
        _activeTunnels[connectionInfo.Id] = tunnelInfo;

        return ("127.0.0.1", localPort);
    }

    /// <summary>
    /// Closes the SSH tunnel for the given connection ID.
    /// </summary>
    public void CloseTunnel(Guid connectionId)
    {
        if (_activeTunnels.TryRemove(connectionId, out var tunnel))
        {
            try
            {
                if (tunnel.ForwardedPort.IsStarted)
                    tunnel.ForwardedPort.Stop();

                tunnel.Client.RemoveForwardedPort(tunnel.ForwardedPort);

                if (tunnel.Client.IsConnected)
                    tunnel.Client.Disconnect();

                tunnel.Client.Dispose();
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    /// <summary>
    /// Checks if an SSH tunnel is active for the given connection.
    /// </summary>
    public bool IsTunnelActive(Guid connectionId)
    {
        return _activeTunnels.TryGetValue(connectionId, out var tunnel)
            && tunnel.Client.IsConnected
            && tunnel.ForwardedPort.IsStarted;
    }

    /// <summary>
    /// Closes all active SSH tunnels.
    /// </summary>
    public void CloseAllTunnels()
    {
        foreach (var id in _activeTunnels.Keys.ToList())
        {
            CloseTunnel(id);
        }
    }

    public void Dispose()
    {
        CloseAllTunnels();
        GC.SuppressFinalize(this);
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed record SshTunnelInfo(SshClient Client, ForwardedPortLocal ForwardedPort, Guid ConnectionId);
}

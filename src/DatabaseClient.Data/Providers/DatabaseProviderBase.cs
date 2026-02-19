using System.Data;
using System.Data.Common;
using System.Diagnostics;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Data.Services;

namespace DatabaseClient.Data.Providers;

/// <summary>
/// Base class with shared logic for all database providers.
/// </summary>
public abstract class DatabaseProviderBase : IDatabaseProvider
{
    protected DbConnection? DbConnection;
    protected SshTunnelManager? SshTunnelManager;

    /// <summary>
    /// When SSH tunnel is active, this holds the local endpoint to connect to
    /// instead of the original host:port.
    /// </summary>
    protected (string Host, int Port)? SshLocalEndpoint;

    public abstract DatabaseType DatabaseType { get; }
    public DbConnection? Connection => DbConnection;
    public bool IsConnected => DbConnection?.State == ConnectionState.Open;

    /// <summary>Sets the SSH tunnel manager for this provider.</summary>
    public void SetSshTunnelManager(SshTunnelManager manager) => SshTunnelManager = manager;

    public abstract Task OpenAsync(ConnectionInfo connectionInfo, CancellationToken ct = default);

    /// <summary>
    /// If the connection uses an SSH tunnel, opens it and returns the local endpoint.
    /// Otherwise returns the original host and port.
    /// </summary>
    protected async Task<(string Host, int Port)> ResolveEndpointAsync(ConnectionInfo connectionInfo, CancellationToken ct)
    {
        if (connectionInfo.UseSshTunnel && SshTunnelManager is not null)
        {
            var (localHost, localPort) = await SshTunnelManager.OpenTunnelAsync(connectionInfo, ct);
            SshLocalEndpoint = (localHost, localPort);
            return (localHost, localPort);
        }

        return (connectionInfo.Host, connectionInfo.Port);
    }

    public async Task CloseAsync()
    {
        if (DbConnection is { State: not ConnectionState.Closed })
        {
            await DbConnection.CloseAsync();
        }
    }

    public async Task<bool> TestConnectionAsync(ConnectionInfo connectionInfo, CancellationToken ct = default)
    {
        try
        {
            await OpenAsync(connectionInfo, ct);
            await CloseAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public abstract Task<string> GetServerVersionAsync(CancellationToken ct = default);
    public abstract Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken ct = default);
    public abstract Task<IReadOnlyList<string>> GetSchemasAsync(string database, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<DatabaseObjectInfo>> GetTablesAsync(string database, string? schema = null, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<DatabaseObjectInfo>> GetViewsAsync(string database, string? schema = null, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string database, string table, string? schema = null, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<DatabaseObjectInfo>> GetIndexesAsync(string database, string table, string? schema = null, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<DatabaseObjectInfo>> GetFunctionsAsync(string database, string? schema = null, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<DatabaseObjectInfo>> GetTriggersAsync(string database, string? schema = null, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<DatabaseObjectInfo>> GetSequencesAsync(string database, string? schema = null, CancellationToken ct = default);

    public virtual async Task ChangeDatabaseAsync(string database, CancellationToken ct = default)
    {
        if (DbConnection is not null)
            await Task.Run(() => DbConnection.ChangeDatabase(database), ct);
    }

    // ── Query execution ────────────────────────────────────────────────────

    public async Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken ct = default)
    {
        return await ExecuteQueryAsync(sql, null, ct);
    }

    public async Task<QueryResult> ExecuteQueryAsync(string sql, IReadOnlyDictionary<string, object?>? parameters, CancellationToken ct = default)
    {
        EnsureConnected();
        var result = new QueryResult();
        var sw = Stopwatch.StartNew();

        try
        {
            await using var cmd = DbConnection!.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;

            // Add parameters if provided
            if (parameters is { Count: > 0 })
            {
                foreach (var (name, value) in parameters)
                {
                    var param = cmd.CreateParameter();
                    param.ParameterName = name;
                    param.Value = value ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                }
            }

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var dataTable = new DataTable();
            dataTable.Load(reader);

            result.ResultSet = dataTable;
            result.RowsAffected = reader.RecordsAffected > 0 ? reader.RecordsAffected : dataTable.Rows.Count;
        }
        catch (Exception ex)
        {
            result.Errors.Add(new QueryError
            {
                Message = ex.Message,
                Detail = ex.InnerException?.Message
            });
        }

        sw.Stop();
        result.ExecutionTime = sw.Elapsed;
        return result;
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default)
    {
        EnsureConnected();
        await using var cmd = DbConnection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<object?> ExecuteScalarAsync(string sql, CancellationToken ct = default)
    {
        EnsureConnected();
        await using var cmd = DbConnection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;
        return await cmd.ExecuteScalarAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (DbConnection is not null)
        {
            await CloseAsync();
            await DbConnection.DisposeAsync();
            DbConnection = null;
        }
        GC.SuppressFinalize(this);
    }

    protected void EnsureConnected()
    {
        if (DbConnection is null || DbConnection.State != ConnectionState.Open)
            throw new InvalidOperationException("Database connection is not open.");
    }
}

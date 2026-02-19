using System.Collections.ObjectModel;
using System.Data;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the database monitoring dashboard tab.
/// Shows real-time metrics, process list, and database stats.
/// </summary>
public partial class MonitorTabViewModel : TabViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private readonly ConnectionInfo _connectionInfo;
    private readonly System.Timers.Timer _refreshTimer;

    public override string TabIconKind => "MonitorDashboard";

    // --- General Info ---
    [ObservableProperty] private string _serverVersion = "-";
    [ObservableProperty] private string _serverUptime = "-";
    [ObservableProperty] private string _databaseSize = "-";
    [ObservableProperty] private int _activeConnections;
    [ObservableProperty] private string _cacheHitRatio = "-";

    // --- Process List ---
    public ObservableCollection<ProcessEntry> Processes { get; } = [];

    [ObservableProperty] private ProcessEntry? _selectedProcess;

    // --- Database Sizes ---
    public ObservableCollection<DbSizeEntry> DatabaseSizes { get; } = [];

    // --- Lock Info ---
    public ObservableCollection<LockEntry> Locks { get; } = [];

    // --- Status ---
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _lastRefreshTime = "-";
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private int _refreshIntervalSeconds = 10;
    [ObservableProperty] private bool _autoRefreshEnabled;
    [ObservableProperty] private string _databaseEngine = "Unknown";

    // --- Refresh interval options ---
    public int[] RefreshIntervalOptions { get; } = [5, 10, 30, 60];

    public MonitorTabViewModel(IConnectionManager connectionManager, ConnectionInfo connectionInfo)
    {
        _connectionManager = connectionManager;
        _connectionInfo = connectionInfo;
        Title = $"Monitor: {connectionInfo.Name}";
        ToolTip = $"Monitoring {connectionInfo.Host}:{connectionInfo.Port}/{connectionInfo.DefaultDatabase}";
        DatabaseEngine = connectionInfo.DatabaseType.ToString();

        _refreshTimer = new System.Timers.Timer(RefreshIntervalSeconds * 1000);
        _refreshTimer.Elapsed += OnRefreshTimerElapsed;
    }

    partial void OnRefreshIntervalSecondsChanged(int value)
    {
        _refreshTimer.Interval = value * 1000;
    }

    partial void OnAutoRefreshEnabledChanged(bool value)
    {
        if (value)
            _refreshTimer.Start();
        else
            _refreshTimer.Stop();
    }

    private async void OnRefreshTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await RefreshAllAsync();
        });
    }

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        StatusMessage = "Refreshing...";

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo, CancellationToken.None);

            switch (_connectionInfo.DatabaseType)
            {
                case DatabaseType.PostgreSQL:
                    await RefreshPostgreSqlAsync(provider);
                    break;
                case DatabaseType.MySQL:
                case DatabaseType.MariaDB:
                    await RefreshMySqlAsync(provider);
                    break;
                case DatabaseType.SQLite:
                    await RefreshSqliteAsync(provider);
                    break;
            }

            LastRefreshTime = DateTime.Now.ToString("HH:mm:ss");
            StatusMessage = "OK";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task KillProcess()
    {
        if (SelectedProcess is null) return;

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo, CancellationToken.None);
            string killSql = _connectionInfo.DatabaseType switch
            {
                DatabaseType.PostgreSQL => $"SELECT pg_terminate_backend({SelectedProcess.Pid})",
                DatabaseType.MySQL or DatabaseType.MariaDB => $"KILL {SelectedProcess.Pid}",
                _ => throw new NotSupportedException("Kill not supported for this engine")
            };

            await provider.ExecuteNonQueryAsync(killSql, CancellationToken.None);
            StatusMessage = $"Process {SelectedProcess.Pid} terminated.";
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Kill failed: {ex.Message}";
        }
    }

    // ========================
    // PostgreSQL Monitoring
    // ========================
    private async Task RefreshPostgreSqlAsync(IDatabaseProvider provider)
    {
        // Server version
        try
        {
            var result = await provider.ExecuteQueryAsync("SELECT version()", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0)
                ServerVersion = result.ResultSet.Rows[0][0]?.ToString()?.Split('(')[0]?.Trim() ?? "-";
        }
        catch { /* ignore */ }

        // Uptime
        try
        {
            var result = await provider.ExecuteQueryAsync(
                "SELECT date_trunc('second', current_timestamp - pg_postmaster_start_time())", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0)
                ServerUptime = result.ResultSet.Rows[0][0]?.ToString() ?? "-";
        }
        catch { /* ignore */ }

        // Database size
        try
        {
            var result = await provider.ExecuteQueryAsync(
                "SELECT pg_size_pretty(pg_database_size(current_database()))", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0)
                DatabaseSize = result.ResultSet.Rows[0][0]?.ToString() ?? "-";
        }
        catch { /* ignore */ }

        // Active connections
        try
        {
            var result = await provider.ExecuteQueryAsync(
                "SELECT count(*) FROM pg_stat_activity WHERE state = 'active'", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0)
                ActiveConnections = Convert.ToInt32(result.ResultSet.Rows[0][0]);
        }
        catch { /* ignore */ }

        // Cache hit ratio
        try
        {
            var result = await provider.ExecuteQueryAsync(@"
                SELECT ROUND(100.0 * sum(heap_blks_hit) / NULLIF(sum(heap_blks_hit) + sum(heap_blks_read), 0), 2) 
                FROM pg_statio_user_tables", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0 && result.ResultSet.Rows[0][0] != DBNull.Value)
                CacheHitRatio = $"{result.ResultSet.Rows[0][0]}%";
            else
                CacheHitRatio = "N/A";
        }
        catch { CacheHitRatio = "N/A"; }

        // Process list
        try
        {
            var result = await provider.ExecuteQueryAsync(@"
                SELECT pid, usename, datname, state, 
                       EXTRACT(EPOCH FROM (now() - query_start))::int AS duration_sec,
                       LEFT(query, 200) AS query,
                       wait_event_type, wait_event, client_addr::text
                FROM pg_stat_activity 
                WHERE pid <> pg_backend_pid()
                ORDER BY query_start NULLS LAST", CancellationToken.None);
            RefreshProcessList(result);
        }
        catch { /* ignore */ }

        // Database sizes
        try
        {
            var result = await provider.ExecuteQueryAsync(@"
                SELECT datname AS name, pg_size_pretty(pg_database_size(datname)) AS size,
                       pg_database_size(datname) AS size_bytes
                FROM pg_database WHERE datistemplate = false ORDER BY pg_database_size(datname) DESC", CancellationToken.None);
            RefreshDatabaseSizes(result);
        }
        catch { /* ignore */ }

        // Locks
        try
        {
            var result = await provider.ExecuteQueryAsync(@"
                SELECT l.pid, a.usename, l.locktype, l.mode, 
                       COALESCE(l.relation::regclass::text, '') AS relation,
                       l.granted, LEFT(a.query, 150) AS query
                FROM pg_locks l JOIN pg_stat_activity a ON l.pid = a.pid
                WHERE l.pid <> pg_backend_pid()
                ORDER BY l.granted, l.pid", CancellationToken.None);
            RefreshLocks(result);
        }
        catch { /* ignore */ }
    }

    // ========================
    // MySQL / MariaDB Monitoring
    // ========================
    private async Task RefreshMySqlAsync(IDatabaseProvider provider)
    {
        // Server version
        try
        {
            var result = await provider.ExecuteQueryAsync("SELECT VERSION()", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0)
                ServerVersion = result.ResultSet.Rows[0][0]?.ToString() ?? "-";
        }
        catch { /* ignore */ }

        // Uptime
        try
        {
            var result = await provider.ExecuteQueryAsync(
                "SHOW GLOBAL STATUS LIKE 'Uptime'", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0)
            {
                var seconds = Convert.ToInt64(result.ResultSet.Rows[0]["Value"]);
                ServerUptime = TimeSpan.FromSeconds(seconds).ToString(@"d\.hh\:mm\:ss");
            }
        }
        catch { /* ignore */ }

        // Database size (current DB)
        try
        {
            var result = await provider.ExecuteQueryAsync(@"
                SELECT ROUND(SUM(data_length + index_length) / 1024 / 1024, 2)
                FROM information_schema.TABLES 
                WHERE table_schema = DATABASE()", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0 && result.ResultSet.Rows[0][0] != DBNull.Value)
                DatabaseSize = $"{result.ResultSet.Rows[0][0]} MB";
        }
        catch { /* ignore */ }

        // Active connections
        try
        {
            var result = await provider.ExecuteQueryAsync(
                "SHOW GLOBAL STATUS LIKE 'Threads_connected'", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0)
                ActiveConnections = Convert.ToInt32(result.ResultSet.Rows[0]["Value"]);
        }
        catch { /* ignore */ }

        // Cache hit ratio (InnoDB buffer pool)
        try
        {
            var result = await provider.ExecuteQueryAsync(@"
                SELECT ROUND(
                    (1 - (SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME = 'Innodb_buffer_pool_reads') /
                         NULLIF((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME = 'Innodb_buffer_pool_read_requests'), 0)
                    ) * 100, 2)", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0 && result.ResultSet.Rows[0][0] != DBNull.Value)
                CacheHitRatio = $"{result.ResultSet.Rows[0][0]}%";
            else
                CacheHitRatio = "N/A";
        }
        catch { CacheHitRatio = "N/A"; }

        // Process list
        try
        {
            var result = await provider.ExecuteQueryAsync(@"
                SELECT ID AS pid, USER AS usename, DB AS datname, COMMAND AS state,
                       TIME AS duration_sec, LEFT(INFO, 200) AS query,
                       '' AS wait_event_type, STATE AS wait_event, HOST AS client_addr
                FROM information_schema.PROCESSLIST
                WHERE ID <> CONNECTION_ID()
                ORDER BY TIME DESC", CancellationToken.None);
            RefreshProcessList(result);
        }
        catch { /* ignore */ }

        // Database sizes
        try
        {
            var result = await provider.ExecuteQueryAsync(@"
                SELECT table_schema AS name,
                       CONCAT(ROUND(SUM(data_length + index_length) / 1024 / 1024, 2), ' MB') AS size,
                       SUM(data_length + index_length) AS size_bytes
                FROM information_schema.TABLES
                GROUP BY table_schema
                ORDER BY SUM(data_length + index_length) DESC", CancellationToken.None);
            RefreshDatabaseSizes(result);
        }
        catch { /* ignore */ }

        // Locks (InnoDB)
        try
        {
            var result = await provider.ExecuteQueryAsync(@"
                SELECT r.trx_id AS pid, r.trx_mysql_thread_id AS usename,
                       'row' AS locktype, r.trx_state AS mode,
                       '' AS relation, 
                       CASE WHEN r.trx_state = 'LOCK WAIT' THEN 'false' ELSE 'true' END AS granted,
                       LEFT(r.trx_query, 150) AS query
                FROM information_schema.INNODB_TRX r
                ORDER BY r.trx_started", CancellationToken.None);
            RefreshLocks(result);
        }
        catch { /* ignore */ }
    }

    // ========================
    // SQLite Monitoring
    // ========================
    private async Task RefreshSqliteAsync(IDatabaseProvider provider)
    {
        ServerVersion = "SQLite (embedded)";
        ServerUptime = "N/A";
        CacheHitRatio = "N/A";
        ActiveConnections = 1;

        // Database size
        try
        {
            var result = await provider.ExecuteQueryAsync(
                "SELECT page_count * page_size FROM pragma_page_count(), pragma_page_size()", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0)
            {
                var bytes = Convert.ToInt64(result.ResultSet.Rows[0][0]);
                DatabaseSize = bytes switch
                {
                    < 1024 => $"{bytes} B",
                    < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
                    < 1024 * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:F1} MB",
                    _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB"
                };
            }
        }
        catch { /* ignore */ }

        // WAL mode check
        try
        {
            var result = await provider.ExecuteQueryAsync("PRAGMA journal_mode", CancellationToken.None);
            if (result.IsSuccessful && result.ResultSet?.Rows.Count > 0)
                StatusMessage = $"Journal mode: {result.ResultSet.Rows[0][0]}";
        }
        catch { /* ignore */ }

        // Integrity check
        try
        {
            Processes.Clear();
            Processes.Add(new ProcessEntry { Query = "Use PRAGMA integrity_check for full check" });
        }
        catch { /* ignore */ }

        // Page info
        try
        {
            DatabaseSizes.Clear();
            var pcResult = await provider.ExecuteQueryAsync("PRAGMA page_count", CancellationToken.None);
            var psResult = await provider.ExecuteQueryAsync("PRAGMA page_size", CancellationToken.None);
            if (pcResult.IsSuccessful && psResult.IsSuccessful)
            {
                var pageCount = pcResult.ResultSet?.Rows[0][0]?.ToString() ?? "?";
                var pageSize = psResult.ResultSet?.Rows[0][0]?.ToString() ?? "?";
                DatabaseSizes.Add(new DbSizeEntry { Name = "Page Count", Size = pageCount });
                DatabaseSizes.Add(new DbSizeEntry { Name = "Page Size", Size = $"{pageSize} bytes" });
            }
        }
        catch { /* ignore */ }

        Locks.Clear();
    }

    // ========================
    // Helpers
    // ========================
    private void RefreshProcessList(QueryResult result)
    {
        Processes.Clear();
        if (!result.IsSuccessful || result.ResultSet is null) return;

        foreach (DataRow row in result.ResultSet.Rows)
        {
            Processes.Add(new ProcessEntry
            {
                Pid = Convert.ToInt64(row["pid"]),
                User = row["usename"]?.ToString() ?? "",
                Database = row["datname"]?.ToString() ?? "",
                State = row["state"]?.ToString() ?? "",
                DurationSec = row["duration_sec"] != DBNull.Value ? Convert.ToInt32(row["duration_sec"]) : 0,
                Query = row["query"]?.ToString() ?? "",
                WaitType = row["wait_event_type"]?.ToString() ?? "",
                WaitEvent = row["wait_event"]?.ToString() ?? "",
                ClientAddr = row["client_addr"]?.ToString() ?? ""
            });
        }
    }

    private void RefreshDatabaseSizes(QueryResult result)
    {
        DatabaseSizes.Clear();
        if (!result.IsSuccessful || result.ResultSet is null) return;

        foreach (DataRow row in result.ResultSet.Rows)
        {
            DatabaseSizes.Add(new DbSizeEntry
            {
                Name = row["name"]?.ToString() ?? "",
                Size = row["size"]?.ToString() ?? "",
                SizeBytes = row["size_bytes"] != DBNull.Value ? Convert.ToInt64(row["size_bytes"]) : 0
            });
        }
    }

    private void RefreshLocks(QueryResult result)
    {
        Locks.Clear();
        if (!result.IsSuccessful || result.ResultSet is null) return;

        foreach (DataRow row in result.ResultSet.Rows)
        {
            Locks.Add(new LockEntry
            {
                Pid = row["pid"]?.ToString() ?? "",
                User = row["usename"]?.ToString() ?? "",
                LockType = row["locktype"]?.ToString() ?? "",
                Mode = row["mode"]?.ToString() ?? "",
                Relation = row["relation"]?.ToString() ?? "",
                Granted = row["granted"]?.ToString() ?? "",
                Query = row["query"]?.ToString() ?? ""
            });
        }
    }

    public override Task<bool> CanCloseAsync()
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        return Task.FromResult(true);
    }
}

// ========================
// Data models for the dashboard
// ========================

public class ProcessEntry
{
    public long Pid { get; init; }
    public string User { get; init; } = "";
    public string Database { get; init; } = "";
    public string State { get; init; } = "";
    public int DurationSec { get; init; }
    public string Query { get; init; } = "";
    public string WaitType { get; init; } = "";
    public string WaitEvent { get; init; } = "";
    public string ClientAddr { get; init; } = "";
}

public class DbSizeEntry
{
    public string Name { get; init; } = "";
    public string Size { get; init; } = "";
    public long SizeBytes { get; init; }
}

public class LockEntry
{
    public string Pid { get; init; } = "";
    public string User { get; init; } = "";
    public string LockType { get; init; } = "";
    public string Mode { get; init; } = "";
    public string Relation { get; init; } = "";
    public string Granted { get; init; } = "";
    public string Query { get; init; } = "";
}

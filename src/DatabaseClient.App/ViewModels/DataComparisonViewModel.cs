using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the Data Comparison &amp; Synchronization wizard.
/// Compares data between two tables across same or different connections.
/// </summary>
public partial class DataComparisonViewModel : ViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private readonly IConnectionRepository _connectionRepository;

    // ── Step 1: Source/Target Selection ──────────────────────────────────

    [ObservableProperty]
    private int _currentStep = 1;

    public ObservableCollection<ConnectionInfo> Connections { get; } = [];

    [ObservableProperty]
    private ConnectionInfo? _sourceConnection;

    [ObservableProperty]
    private string _sourceDatabase = string.Empty;

    [ObservableProperty]
    private string _sourceTable = string.Empty;

    [ObservableProperty]
    private ConnectionInfo? _targetConnection;

    [ObservableProperty]
    private string _targetDatabase = string.Empty;

    [ObservableProperty]
    private string _targetTable = string.Empty;

    public ObservableCollection<string> SourceDatabases { get; } = [];
    public ObservableCollection<string> SourceTables { get; } = [];
    public ObservableCollection<string> TargetDatabases { get; } = [];
    public ObservableCollection<string> TargetTables { get; } = [];

    // ── Step 2: Key & Column Selection ──────────────────────────────────

    public ObservableCollection<CompareColumnItem> Columns { get; } = [];

    // ── Step 3: Results ─────────────────────────────────────────────────

    [ObservableProperty]
    private DataTable? _comparisonResults;

    [ObservableProperty]
    private int _onlyInSourceCount;

    [ObservableProperty]
    private int _onlyInTargetCount;

    [ObservableProperty]
    private int _differentCount;

    [ObservableProperty]
    private int _identicalCount;

    [ObservableProperty]
    private string _syncScript = string.Empty;

    [ObservableProperty]
    private bool _syncInserts = true;

    [ObservableProperty]
    private bool _syncUpdates = true;

    [ObservableProperty]
    private bool _syncDeletes;

    [ObservableProperty]
    private string _comparisonLog = string.Empty;

    public string NextButtonText => CurrentStep switch
    {
        1 => "Next →",
        2 => "Compare",
        _ => "Done"
    };

    public bool ShowPreviousButton => CurrentStep > 1;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(ShowPreviousButton));
    }

    public DataComparisonViewModel(IConnectionManager connectionManager, IConnectionRepository connectionRepository)
    {
        _connectionManager = connectionManager;
        _connectionRepository = connectionRepository;
        _ = LoadConnectionsAsync();
    }

    private async Task LoadConnectionsAsync()
    {
        var connections = await _connectionRepository.GetAllAsync();
        Connections.Clear();
        foreach (var conn in connections)
            Connections.Add(conn);
    }

    partial void OnSourceConnectionChanged(ConnectionInfo? value)
    {
        _ = LoadDatabases(value, SourceDatabases);
    }

    partial void OnTargetConnectionChanged(ConnectionInfo? value)
    {
        _ = LoadDatabases(value, TargetDatabases);
    }

    partial void OnSourceDatabaseChanged(string value)
    {
        if (SourceConnection != null && !string.IsNullOrEmpty(value))
            _ = LoadTables(SourceConnection, value, SourceTables);
    }

    partial void OnTargetDatabaseChanged(string value)
    {
        if (TargetConnection != null && !string.IsNullOrEmpty(value))
            _ = LoadTables(TargetConnection, value, TargetTables);
    }

    private async Task LoadDatabases(ConnectionInfo? conn, ObservableCollection<string> target)
    {
        target.Clear();
        if (conn == null) return;

        try
        {
            var provider = await _connectionManager.GetProviderAsync(conn);
            var result = await provider.ExecuteQueryAsync(conn.DatabaseType switch
            {
                DatabaseType.PostgreSQL => "SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname",
                DatabaseType.MySQL or DatabaseType.MariaDB => "SHOW DATABASES",
                DatabaseType.SQLite => "SELECT 'main' AS name",
                _ => "SELECT 'default'"
            });

            if (result.IsSuccessful && result.ResultSet != null)
            {
                foreach (DataRow row in result.ResultSet.Rows)
                    target.Add(row[0]?.ToString() ?? "");
            }
        }
        catch { /* ignore */ }
    }

    private async Task LoadTables(ConnectionInfo conn, string database, ObservableCollection<string> target)
    {
        target.Clear();
        try
        {
            var provider = await _connectionManager.GetProviderAsync(conn);
            await provider.ChangeDatabaseAsync(database);
            var tables = await provider.GetTablesAsync(database);
            foreach (var table in tables)
                target.Add(table.Name);
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private async Task Next()
    {
        if (CurrentStep == 1)
        {
            // Load columns from source table
            await LoadColumnsAsync();
            CurrentStep = 2;
        }
        else if (CurrentStep == 2)
        {
            // Run comparison
            await RunComparisonAsync();
            CurrentStep = 3;
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (CurrentStep > 1)
            CurrentStep--;
    }

    private async Task LoadColumnsAsync()
    {
        Columns.Clear();
        if (SourceConnection == null || string.IsNullOrEmpty(SourceTable)) return;

        try
        {
            var provider = await _connectionManager.GetProviderAsync(SourceConnection);
            await provider.ChangeDatabaseAsync(SourceDatabase);
            var result = await provider.ExecuteQueryAsync($"SELECT * FROM {QuoteIdentifier(SourceTable, SourceConnection.DatabaseType)} LIMIT 0");
            if (result.IsSuccessful && result.ResultSet != null)
            {
                foreach (DataColumn col in result.ResultSet.Columns)
                {
                    Columns.Add(new CompareColumnItem
                    {
                        ColumnName = col.ColumnName,
                        IsKey = col.Ordinal == 0, // Default: first column as key
                        IsCompared = true
                    });
                }
            }
        }
        catch { /* ignore */ }
    }

    private async Task RunComparisonAsync()
    {
        IsBusy = true;
        ComparisonLog = "Starting comparison...";
        OnlyInSourceCount = 0;
        OnlyInTargetCount = 0;
        DifferentCount = 0;
        IdenticalCount = 0;

        try
        {
            var keyColumns = Columns.Where(c => c.IsKey).Select(c => c.ColumnName).ToList();
            var compareColumns = Columns.Where(c => c.IsCompared && !c.IsKey).Select(c => c.ColumnName).ToList();

            if (keyColumns.Count == 0)
            {
                ComparisonLog = "Error: Please select at least one key column.";
                return;
            }

            var allColumns = keyColumns.Concat(compareColumns).ToList();
            var columnList = string.Join(", ", allColumns.Select(c => QuoteIdentifier(c, SourceConnection!.DatabaseType)));

            // Fetch source data
            ComparisonLog = "Fetching source data...";
            var sourceProvider = await _connectionManager.GetProviderAsync(SourceConnection!);
            await sourceProvider.ChangeDatabaseAsync(SourceDatabase);
            var sourceResult = await sourceProvider.ExecuteQueryAsync(
                $"SELECT {columnList} FROM {QuoteIdentifier(SourceTable, SourceConnection.DatabaseType)} ORDER BY {string.Join(", ", keyColumns.Select(c => QuoteIdentifier(c, SourceConnection.DatabaseType)))}");

            // Fetch target data
            ComparisonLog = "Fetching target data...";
            var targetProvider = await _connectionManager.GetProviderAsync(TargetConnection!);
            await targetProvider.ChangeDatabaseAsync(TargetDatabase);
            var targetResult = await targetProvider.ExecuteQueryAsync(
                $"SELECT {columnList} FROM {QuoteIdentifier(TargetTable, TargetConnection!.DatabaseType)} ORDER BY {string.Join(", ", keyColumns.Select(c => QuoteIdentifier(c, TargetConnection.DatabaseType)))}");

            if (!sourceResult.IsSuccessful || !targetResult.IsSuccessful)
            {
                ComparisonLog = $"Error fetching data: {sourceResult.Errors ?? targetResult.Errors}";
                return;
            }

            // Build comparison result table
            ComparisonLog = "Comparing rows...";
            var resultTable = new DataTable();
            resultTable.Columns.Add("Status", typeof(string));
            foreach (var col in allColumns)
                resultTable.Columns.Add(col, typeof(string));

            // Index target rows by key
            var targetIndex = new Dictionary<string, DataRow>();
            if (targetResult.ResultSet != null)
            {
                foreach (DataRow row in targetResult.ResultSet.Rows)
                {
                    var key = BuildRowKey(row, keyColumns);
                    targetIndex[key] = row;
                }
            }

            // Compare source rows against target
            var matchedKeys = new HashSet<string>();
            if (sourceResult.ResultSet != null)
            {
                foreach (DataRow srcRow in sourceResult.ResultSet.Rows)
                {
                    var key = BuildRowKey(srcRow, keyColumns);
                    var resultRow = resultTable.NewRow();

                    foreach (var col in allColumns)
                        resultRow[col] = srcRow[col]?.ToString() ?? "NULL";

                    if (targetIndex.TryGetValue(key, out var tgtRow))
                    {
                        matchedKeys.Add(key);
                        bool isDifferent = false;
                        foreach (var col in compareColumns)
                        {
                            var srcVal = srcRow[col]?.ToString() ?? "NULL";
                            var tgtVal = tgtRow[col]?.ToString() ?? "NULL";
                            if (srcVal != tgtVal)
                            {
                                isDifferent = true;
                                break;
                            }
                        }
                        resultRow["Status"] = isDifferent ? "Modified" : "Identical";
                        if (isDifferent) DifferentCount++;
                        else IdenticalCount++;
                    }
                    else
                    {
                        resultRow["Status"] = "Only in Source";
                        OnlyInSourceCount++;
                    }

                    resultTable.Rows.Add(resultRow);
                }
            }

            // Add rows only in target
            if (targetResult.ResultSet != null)
            {
                foreach (DataRow tgtRow in targetResult.ResultSet.Rows)
                {
                    var key = BuildRowKey(tgtRow, keyColumns);
                    if (!matchedKeys.Contains(key))
                    {
                        var resultRow = resultTable.NewRow();
                        resultRow["Status"] = "Only in Target";
                        foreach (var col in allColumns)
                            resultRow[col] = tgtRow[col]?.ToString() ?? "NULL";
                        resultTable.Rows.Add(resultRow);
                        OnlyInTargetCount++;
                    }
                }
            }

            ComparisonResults = resultTable;
            ComparisonLog = $"Comparison complete: {OnlyInSourceCount} only in source, {OnlyInTargetCount} only in target, {DifferentCount} different, {IdenticalCount} identical.";
        }
        catch (Exception ex)
        {
            ComparisonLog = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GenerateSyncScript()
    {
        if (ComparisonResults == null || TargetConnection == null) return;

        var keyColumns = Columns.Where(c => c.IsKey).Select(c => c.ColumnName).ToList();
        var compareColumns = Columns.Where(c => c.IsCompared && !c.IsKey).Select(c => c.ColumnName).ToList();
        var allColumns = keyColumns.Concat(compareColumns).ToList();
        var targetTableQuoted = QuoteIdentifier(TargetTable, TargetConnection.DatabaseType);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-- Data Synchronization Script");
        sb.AppendLine($"-- Target: {TargetTable} on {TargetConnection.Name}/{TargetDatabase}");
        sb.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        foreach (DataRow row in ComparisonResults.Rows)
        {
            var status = row["Status"]?.ToString();

            if (status == "Only in Source" && SyncInserts)
            {
                var cols = string.Join(", ", allColumns.Select(c => QuoteIdentifier(c, TargetConnection.DatabaseType)));
                var vals = string.Join(", ", allColumns.Select(c => EscapeSqlValue(row[c]?.ToString())));
                sb.AppendLine($"INSERT INTO {targetTableQuoted} ({cols}) VALUES ({vals});");
            }
            else if (status == "Modified" && SyncUpdates)
            {
                var sets = string.Join(", ", compareColumns.Select(c =>
                    $"{QuoteIdentifier(c, TargetConnection.DatabaseType)} = {EscapeSqlValue(row[c]?.ToString())}"));
                var where = string.Join(" AND ", keyColumns.Select(c =>
                    $"{QuoteIdentifier(c, TargetConnection.DatabaseType)} = {EscapeSqlValue(row[c]?.ToString())}"));
                sb.AppendLine($"UPDATE {targetTableQuoted} SET {sets} WHERE {where};");
            }
            else if (status == "Only in Target" && SyncDeletes)
            {
                var where = string.Join(" AND ", keyColumns.Select(c =>
                    $"{QuoteIdentifier(c, TargetConnection.DatabaseType)} = {EscapeSqlValue(row[c]?.ToString())}"));
                sb.AppendLine($"DELETE FROM {targetTableQuoted} WHERE {where};");
            }
        }

        SyncScript = sb.ToString();
    }

    private static string BuildRowKey(DataRow row, List<string> keyColumns)
    {
        return string.Join("|", keyColumns.Select(c => row[c]?.ToString() ?? "NULL"));
    }

    private static string QuoteIdentifier(string name, DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.PostgreSQL => $"\"{name}\"",
            DatabaseType.MySQL or DatabaseType.MariaDB => $"`{name}`",
            _ => $"\"{name}\""
        };
    }

    private static string EscapeSqlValue(string? value)
    {
        if (value == null || value == "NULL") return "NULL";
        return $"'{value.Replace("'", "''")}'";
    }
}

/// <summary>
/// Represents a column in the comparison configuration.
/// </summary>
public partial class CompareColumnItem : ObservableObject
{
    public string ColumnName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isKey;

    [ObservableProperty]
    private bool _isCompared = true;
}

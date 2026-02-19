using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

using ColInfo = DatabaseClient.Core.Models.ColumnInfo;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the Schema Comparison &amp; Synchronization wizard.
/// Compares database schema objects (tables, columns, indexes) between two databases.
/// </summary>
public partial class SchemaComparisonViewModel : ViewModelBase
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
    private ConnectionInfo? _targetConnection;

    [ObservableProperty]
    private string _targetDatabase = string.Empty;

    public ObservableCollection<string> SourceDatabases { get; } = [];
    public ObservableCollection<string> TargetDatabases { get; } = [];

    [ObservableProperty]
    private bool _compareTables = true;

    [ObservableProperty]
    private bool _compareViews = true;

    [ObservableProperty]
    private bool _compareFunctions;

    [ObservableProperty]
    private bool _compareIndexes = true;

    // ── Step 2: Results ──────────────────────────────────────────────────

    public ObservableCollection<SchemaObjectDiff> SchemaDiffs { get; } = [];

    [ObservableProperty]
    private SchemaObjectDiff? _selectedDiff;

    [ObservableProperty]
    private int _onlyInSourceCount;

    [ObservableProperty]
    private int _onlyInTargetCount;

    [ObservableProperty]
    private int _differentCount;

    [ObservableProperty]
    private int _identicalCount;

    [ObservableProperty]
    private string _comparisonLog = string.Empty;

    // ── Step 3: Migration Script ─────────────────────────────────────────

    [ObservableProperty]
    private string _migrationScript = string.Empty;

    [ObservableProperty]
    private bool _generateCreates = true;

    [ObservableProperty]
    private bool _generateAlters = true;

    [ObservableProperty]
    private bool _generateDrops;

    public string NextButtonText => CurrentStep switch
    {
        1 => "Compare Schemas",
        2 => "Generate Script",
        _ => "Done"
    };

    public bool ShowPreviousButton => CurrentStep > 1;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(ShowPreviousButton));
    }

    public SchemaComparisonViewModel(IConnectionManager connectionManager, IConnectionRepository connectionRepository)
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

    partial void OnSourceConnectionChanged(ConnectionInfo? value) => _ = LoadDatabases(value, SourceDatabases);
    partial void OnTargetConnectionChanged(ConnectionInfo? value) => _ = LoadDatabases(value, TargetDatabases);

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

    [RelayCommand]
    private async Task Next()
    {
        if (CurrentStep == 1)
        {
            await RunSchemaComparisonAsync();
            CurrentStep = 2;
        }
        else if (CurrentStep == 2)
        {
            GenerateMigrationScript();
            CurrentStep = 3;
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (CurrentStep > 1)
            CurrentStep--;
    }

    private async Task RunSchemaComparisonAsync()
    {
        IsBusy = true;
        ComparisonLog = "Reading source schema...";
        SchemaDiffs.Clear();
        OnlyInSourceCount = 0;
        OnlyInTargetCount = 0;
        DifferentCount = 0;
        IdenticalCount = 0;

        try
        {
            var sourceProvider = await _connectionManager.GetProviderAsync(SourceConnection!);
            await sourceProvider.ChangeDatabaseAsync(SourceDatabase);
            var targetProvider = await _connectionManager.GetProviderAsync(TargetConnection!);
            await targetProvider.ChangeDatabaseAsync(TargetDatabase);

            if (CompareTables)
            {
                ComparisonLog = "Comparing tables...";
                await CompareTablesAsync(sourceProvider, targetProvider);
            }

            if (CompareViews)
            {
                ComparisonLog = "Comparing views...";
                await CompareViewsAsync(sourceProvider, targetProvider);
            }

            ComparisonLog = $"Schema comparison complete: {OnlyInSourceCount} only in source, {OnlyInTargetCount} only in target, {DifferentCount} different, {IdenticalCount} identical.";
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

    private async Task CompareTablesAsync(IDatabaseProvider source, IDatabaseProvider target)
    {
        var srcTables = (await source.GetTablesAsync(SourceDatabase)).Select(t => t.Name).ToList();
        var tgtTables = (await target.GetTablesAsync(TargetDatabase)).Select(t => t.Name).ToList();

        var allTableNames = srcTables.Union(tgtTables).OrderBy(t => t).ToList();

        foreach (var table in allTableNames)
        {
            bool inSource = srcTables.Contains(table);
            bool inTarget = tgtTables.Contains(table);

            if (inSource && inTarget)
            {
                // Compare columns
                var srcCols = await GetColumnsAsync(source, table, SourceConnection!.DatabaseType);
                var tgtCols = await GetColumnsAsync(target, table, TargetConnection!.DatabaseType);
                var differences = CompareColumns(srcCols, tgtCols);

                if (differences.Count == 0)
                {
                    SchemaDiffs.Add(new SchemaObjectDiff
                    {
                        ObjectName = table,
                        ObjectType = "Table",
                        Status = "Identical",
                        Details = "Columns match"
                    });
                    IdenticalCount++;
                }
                else
                {
                    SchemaDiffs.Add(new SchemaObjectDiff
                    {
                        ObjectName = table,
                        ObjectType = "Table",
                        Status = "Modified",
                        Details = string.Join("\n", differences),
                        SourceColumns = srcCols,
                        TargetColumns = tgtCols
                    });
                    DifferentCount++;
                }
            }
            else if (inSource)
            {
                var srcCols = await GetColumnsAsync(source, table, SourceConnection!.DatabaseType);
                SchemaDiffs.Add(new SchemaObjectDiff
                {
                    ObjectName = table,
                    ObjectType = "Table",
                    Status = "Only in Source",
                    Details = $"{srcCols.Count} column(s)",
                    SourceColumns = srcCols
                });
                OnlyInSourceCount++;
            }
            else
            {
                var tgtCols = await GetColumnsAsync(target, table, TargetConnection!.DatabaseType);
                SchemaDiffs.Add(new SchemaObjectDiff
                {
                    ObjectName = table,
                    ObjectType = "Table",
                    Status = "Only in Target",
                    Details = $"{tgtCols.Count} column(s)",
                    TargetColumns = tgtCols
                });
                OnlyInTargetCount++;
            }
        }
    }

    private async Task CompareViewsAsync(IDatabaseProvider source, IDatabaseProvider target)
    {
        var srcViews = await GetViewNamesAsync(source, SourceConnection!.DatabaseType);
        var tgtViews = await GetViewNamesAsync(target, TargetConnection!.DatabaseType);

        var allViews = srcViews.Union(tgtViews).OrderBy(v => v).ToList();

        foreach (var view in allViews)
        {
            bool inSource = srcViews.Contains(view);
            bool inTarget = tgtViews.Contains(view);

            string status;
            string details;

            if (inSource && inTarget)
            {
                status = "Identical";
                details = "View exists in both";
                IdenticalCount++;
            }
            else if (inSource)
            {
                status = "Only in Source";
                details = "View only in source";
                OnlyInSourceCount++;
            }
            else
            {
                status = "Only in Target";
                details = "View only in target";
                OnlyInTargetCount++;
            }

            SchemaDiffs.Add(new SchemaObjectDiff
            {
                ObjectName = view,
                ObjectType = "View",
                Status = status,
                Details = details
            });
        }
    }

    private async Task<List<ColInfo>> GetColumnsAsync(IDatabaseProvider provider, string table, DatabaseType dbType)
    {
        var columns = new List<ColInfo>();
        var sql = dbType switch
        {
            DatabaseType.PostgreSQL =>
                $"SELECT column_name, data_type, is_nullable, character_maximum_length, column_default FROM information_schema.columns WHERE table_name = '{table}' ORDER BY ordinal_position",
            DatabaseType.MySQL or DatabaseType.MariaDB =>
                $"SELECT column_name, data_type, is_nullable, character_maximum_length, column_default FROM information_schema.columns WHERE table_name = '{table}' AND table_schema = DATABASE() ORDER BY ordinal_position",
            DatabaseType.SQLite =>
                $"PRAGMA table_info(\"{table}\")",
            _ => ""
        };

        if (string.IsNullOrEmpty(sql)) return columns;

        var result = await provider.ExecuteQueryAsync(sql);
        if (!result.IsSuccessful || result.ResultSet == null) return columns;

        foreach (DataRow row in result.ResultSet.Rows)
        {
            if (dbType == DatabaseType.SQLite)
            {
                columns.Add(new ColInfo
                {
                    Name = row["name"]?.ToString() ?? "",
                    DataType = row["type"]?.ToString() ?? "",
                    IsNullable = row["notnull"]?.ToString() == "0",
                    DefaultValue = row["dflt_value"]?.ToString()
                });
            }
            else
            {
                columns.Add(new ColInfo
                {
                    Name = row["column_name"]?.ToString() ?? "",
                    DataType = row["data_type"]?.ToString() ?? "",
                    IsNullable = row["is_nullable"]?.ToString() == "YES",
                    MaxLength = row["character_maximum_length"] as int?,
                    DefaultValue = row["column_default"]?.ToString()
                });
            }
        }

        return columns;
    }

    private async Task<List<string>> GetViewNamesAsync(IDatabaseProvider provider, DatabaseType dbType)
    {
        var sql = dbType switch
        {
            DatabaseType.PostgreSQL =>
                "SELECT table_name FROM information_schema.views WHERE table_schema = 'public' ORDER BY table_name",
            DatabaseType.MySQL or DatabaseType.MariaDB =>
                "SELECT table_name FROM information_schema.views WHERE table_schema = DATABASE() ORDER BY table_name",
            DatabaseType.SQLite =>
                "SELECT name FROM sqlite_master WHERE type = 'view' ORDER BY name",
            _ => ""
        };

        var views = new List<string>();
        if (string.IsNullOrEmpty(sql)) return views;

        var result = await provider.ExecuteQueryAsync(sql);
        if (result.IsSuccessful && result.ResultSet != null)
        {
            foreach (DataRow row in result.ResultSet.Rows)
                views.Add(row[0]?.ToString() ?? "");
        }

        return views;
    }

    private static List<string> CompareColumns(List<ColInfo> source, List<ColInfo> target)
    {
        var diffs = new List<string>();
        var srcDict = source.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var tgtDict = target.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var col in source)
        {
            if (!tgtDict.TryGetValue(col.Name, out var tgtCol))
            {
                diffs.Add($"Column '{col.Name}' only in source ({col.DataType})");
            }
            else
            {
                if (!string.Equals(col.DataType, tgtCol.DataType, StringComparison.OrdinalIgnoreCase))
                    diffs.Add($"Column '{col.Name}': type differs ({col.DataType} vs {tgtCol.DataType})");
                if (col.IsNullable != tgtCol.IsNullable)
                    diffs.Add($"Column '{col.Name}': nullable differs ({col.IsNullable} vs {tgtCol.IsNullable})");
            }
        }

        foreach (var col in target)
        {
            if (!srcDict.ContainsKey(col.Name))
                diffs.Add($"Column '{col.Name}' only in target ({col.DataType})");
        }

        return diffs;
    }

    private void GenerateMigrationScript()
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Schema Migration Script");
        sb.AppendLine($"-- Source: {SourceConnection?.Name}/{SourceDatabase}");
        sb.AppendLine($"-- Target: {TargetConnection?.Name}/{TargetDatabase}");
        sb.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        var targetDbType = TargetConnection?.DatabaseType ?? DatabaseType.PostgreSQL;

        foreach (var diff in SchemaDiffs)
        {
            if (diff.Status == "Only in Source" && GenerateCreates && diff.ObjectType == "Table" && diff.SourceColumns != null)
            {
                sb.AppendLine($"-- CREATE TABLE {diff.ObjectName} (only in source)");
                sb.Append($"CREATE TABLE {QuoteId(diff.ObjectName, targetDbType)} (");
                sb.AppendLine();
                var colDefs = diff.SourceColumns.Select(c =>
                    $"    {QuoteId(c.Name, targetDbType)} {c.DataType}{(c.MaxLength.HasValue ? $"({c.MaxLength})" : "")}{(c.IsNullable ? "" : " NOT NULL")}{(c.DefaultValue != null ? $" DEFAULT {c.DefaultValue}" : "")}");
                sb.AppendLine(string.Join(",\n", colDefs));
                sb.AppendLine(");");
                sb.AppendLine();
            }
            else if (diff.Status == "Modified" && GenerateAlters && diff.ObjectType == "Table" && diff.SourceColumns != null && diff.TargetColumns != null)
            {
                var srcDict = diff.SourceColumns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
                var tgtDict = diff.TargetColumns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

                sb.AppendLine($"-- ALTER TABLE {diff.ObjectName} (differences detected)");
                foreach (var col in diff.SourceColumns)
                {
                    if (!tgtDict.ContainsKey(col.Name))
                    {
                        sb.AppendLine($"ALTER TABLE {QuoteId(diff.ObjectName, targetDbType)} ADD COLUMN {QuoteId(col.Name, targetDbType)} {col.DataType}{(col.MaxLength.HasValue ? $"({col.MaxLength})" : "")}{(col.IsNullable ? "" : " NOT NULL")};");
                    }
                }
                foreach (var col in diff.TargetColumns)
                {
                    if (!srcDict.ContainsKey(col.Name) && GenerateDrops)
                    {
                        sb.AppendLine($"ALTER TABLE {QuoteId(diff.ObjectName, targetDbType)} DROP COLUMN {QuoteId(col.Name, targetDbType)};");
                    }
                }
                sb.AppendLine();
            }
            else if (diff.Status == "Only in Target" && GenerateDrops && diff.ObjectType == "Table")
            {
                sb.AppendLine($"DROP TABLE {QuoteId(diff.ObjectName, targetDbType)};");
                sb.AppendLine();
            }
        }

        MigrationScript = sb.ToString();
    }

    private static string QuoteId(string name, DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.PostgreSQL => $"\"{name}\"",
            DatabaseType.MySQL or DatabaseType.MariaDB => $"`{name}`",
            _ => $"\"{name}\""
        };
    }
}

/// <summary>
/// Represents the comparison result for a single schema object.
/// </summary>
public class SchemaObjectDiff
{
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public List<DatabaseClient.Core.Models.ColumnInfo>? SourceColumns { get; set; }
    public List<DatabaseClient.Core.Models.ColumnInfo>? TargetColumns { get; set; }
}

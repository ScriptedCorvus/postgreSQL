using System.Collections.ObjectModel;
using System.Data;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the Database Migration wizard.
/// Migrates schema and data between different database engines.
/// </summary>
public partial class MigrationWizardViewModel : ViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private readonly IConnectionRepository _connectionRepository;

    [ObservableProperty]
    private int _currentStep = 1;

    // ── Step 1: Source/Target ────────────────────────────────────────────

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

    // ── Step 2: Object Selection ─────────────────────────────────────────

    public ObservableCollection<MigrationTableItem> Tables { get; } = [];

    [ObservableProperty]
    private bool _migrateStructure = true;

    [ObservableProperty]
    private bool _migrateData = true;

    [ObservableProperty]
    private bool _truncateTarget;

    // ── Step 3: Type Mapping ─────────────────────────────────────────────

    public ObservableCollection<TypeMapping> TypeMappings { get; } = [];

    // ── Step 4: Execution ────────────────────────────────────────────────

    [ObservableProperty]
    private string _migrationLog = string.Empty;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMax = 100;

    [ObservableProperty]
    private bool _isMigrating;

    public string NextButtonText => CurrentStep switch
    {
        1 => "Next →",
        2 => "Next →",
        3 => "Start Migration",
        _ => "Done"
    };

    public bool ShowPreviousButton => CurrentStep > 1 && CurrentStep < 4;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(ShowPreviousButton));
    }

    public MigrationWizardViewModel(IConnectionManager connectionManager, IConnectionRepository connectionRepository)
    {
        _connectionManager = connectionManager;
        _connectionRepository = connectionRepository;
        _ = LoadConnectionsAsync();
    }

    private async Task LoadConnectionsAsync()
    {
        var conns = await _connectionRepository.GetAllAsync();
        Connections.Clear();
        foreach (var c in conns)
            Connections.Add(c);
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
                foreach (DataRow row in result.ResultSet.Rows)
                    target.Add(row[0]?.ToString() ?? "");
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private async Task Next()
    {
        switch (CurrentStep)
        {
            case 1:
                await LoadSourceTablesAsync();
                CurrentStep = 2;
                break;
            case 2:
                LoadTypeMappings();
                CurrentStep = 3;
                break;
            case 3:
                await ExecuteMigrationAsync();
                CurrentStep = 4;
                break;
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (CurrentStep > 1 && CurrentStep < 4)
            CurrentStep--;
    }

    private async Task LoadSourceTablesAsync()
    {
        Tables.Clear();
        if (SourceConnection == null || string.IsNullOrEmpty(SourceDatabase)) return;

        try
        {
            var provider = await _connectionManager.GetProviderAsync(SourceConnection);
            await provider.ChangeDatabaseAsync(SourceDatabase);
            var tables = await provider.GetTablesAsync(SourceDatabase);
            foreach (var t in tables)
                Tables.Add(new MigrationTableItem { TableName = t.Name, IsSelected = true });
        }
        catch { /* ignore */ }
    }

    private void LoadTypeMappings()
    {
        TypeMappings.Clear();
        if (SourceConnection == null || TargetConnection == null) return;

        var mappings = GetDefaultTypeMappings(SourceConnection.DatabaseType, TargetConnection.DatabaseType);
        foreach (var m in mappings)
            TypeMappings.Add(m);
    }

    private async Task ExecuteMigrationAsync()
    {
        IsMigrating = true;
        var log = new StringBuilder();
        var selectedTables = Tables.Where(t => t.IsSelected).ToList();
        ProgressMax = selectedTables.Count;
        ProgressValue = 0;

        try
        {
            var srcProvider = await _connectionManager.GetProviderAsync(SourceConnection!);
            await srcProvider.ChangeDatabaseAsync(SourceDatabase);
            var tgtProvider = await _connectionManager.GetProviderAsync(TargetConnection!);
            await tgtProvider.ChangeDatabaseAsync(TargetDatabase);

            var typeMap = TypeMappings.ToDictionary(m => m.SourceType.ToLowerInvariant(), m => m.TargetType, StringComparer.OrdinalIgnoreCase);

            foreach (var tableItem in selectedTables)
            {
                var table = tableItem.TableName;
                log.AppendLine($"── Table: {table} ──");
                MigrationLog = log.ToString();

                try
                {
                    if (MigrateStructure)
                    {
                        // Read source columns
                        var columns = await GetColumnsAsync(srcProvider, table, SourceConnection!.DatabaseType);

                        // Build CREATE TABLE SQL in target dialect
                        var createSql = BuildCreateTableSql(table, columns, TargetConnection!.DatabaseType, typeMap);
                        log.AppendLine($"  Creating table... ");

                        // Try to create (DROP first if exists for clean migration)
                        try
                        {
                            await tgtProvider.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {QuoteId(table, TargetConnection.DatabaseType)}");
                        }
                        catch { /* ignore if DROP fails */ }

                        var createResult = await tgtProvider.ExecuteNonQueryAsync(createSql);
                        log.AppendLine($"  Table created. Rows affected: {createResult}");
                    }

                    if (MigrateData)
                    {
                        if (TruncateTarget && !MigrateStructure)
                        {
                            await tgtProvider.ExecuteNonQueryAsync($"DELETE FROM {QuoteId(table, TargetConnection!.DatabaseType)}");
                            log.AppendLine("  Truncated target table.");
                        }

                        // Read all data from source
                        var dataResult = await srcProvider.ExecuteQueryAsync($"SELECT * FROM {QuoteId(table, SourceConnection!.DatabaseType)}");
                        if (dataResult.IsSuccessful && dataResult.ResultSet != null && dataResult.ResultSet.Rows.Count > 0)
                        {
                            int inserted = 0;
                            var dt = dataResult.ResultSet;
                            var colNames = new List<string>();
                            for (int i = 0; i < dt.Columns.Count; i++)
                                colNames.Add(dt.Columns[i].ColumnName);

                            var colsSql = string.Join(", ", colNames.Select(c => QuoteId(c, TargetConnection!.DatabaseType)));

                            foreach (DataRow row in dt.Rows)
                            {
                                var vals = colNames.Select(c => EscapeSqlValue(row[c])).ToList();
                                var insertSql = $"INSERT INTO {QuoteId(table, TargetConnection!.DatabaseType)} ({colsSql}) VALUES ({string.Join(", ", vals)})";
                                try
                                {
                                    await tgtProvider.ExecuteNonQueryAsync(insertSql);
                                    inserted++;
                                }
                                catch (Exception ex)
                                {
                                    log.AppendLine($"  Error inserting row: {ex.Message}");
                                }
                            }

                            log.AppendLine($"  Migrated {inserted}/{dt.Rows.Count} rows.");
                        }
                        else
                        {
                            log.AppendLine("  No data to migrate.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.AppendLine($"  ERROR: {ex.Message}");
                }

                ProgressValue++;
                MigrationLog = log.ToString();
            }

            log.AppendLine();
            log.AppendLine("═══ Migration complete ═══");
        }
        catch (Exception ex)
        {
            log.AppendLine($"FATAL ERROR: {ex.Message}");
        }
        finally
        {
            IsMigrating = false;
            MigrationLog = log.ToString();
        }
    }

    private async Task<List<Core.Models.ColumnInfo>> GetColumnsAsync(IDatabaseProvider provider, string table, DatabaseType dbType)
    {
        var columns = new List<Core.Models.ColumnInfo>();
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
                columns.Add(new Core.Models.ColumnInfo
                {
                    Name = row["name"]?.ToString() ?? "",
                    DataType = row["type"]?.ToString() ?? "",
                    IsNullable = row["notnull"]?.ToString() == "0",
                    DefaultValue = row["dflt_value"]?.ToString()
                });
            }
            else
            {
                columns.Add(new Core.Models.ColumnInfo
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

    private static string BuildCreateTableSql(string table, List<Core.Models.ColumnInfo> columns, DatabaseType targetDb, Dictionary<string, string> typeMap)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {QuoteId(table, targetDb)} (");

        var colDefs = new List<string>();
        foreach (var col in columns)
        {
            var mappedType = typeMap.TryGetValue(col.DataType.ToLowerInvariant(), out var mapped) ? mapped : col.DataType;
            var maxLen = col.MaxLength.HasValue ? $"({col.MaxLength})" : "";
            var nullable = col.IsNullable ? "" : " NOT NULL";
            colDefs.Add($"    {QuoteId(col.Name, targetDb)} {mappedType}{maxLen}{nullable}");
        }

        sb.AppendLine(string.Join(",\n", colDefs));
        sb.Append(")");
        return sb.ToString();
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

    private static string EscapeSqlValue(object? value)
    {
        if (value == null || value == DBNull.Value) return "NULL";
        var str = value.ToString() ?? "NULL";
        return $"'{str.Replace("'", "''")}'";
    }

    /// <summary>
    /// Returns default type mappings between two database engines.
    /// </summary>
    private static List<TypeMapping> GetDefaultTypeMappings(DatabaseType source, DatabaseType target)
    {
        var mappings = new List<TypeMapping>();

        // Common type mappings
        AddMapping(mappings, "integer", "integer");
        AddMapping(mappings, "int", "integer");
        AddMapping(mappings, "bigint", "bigint");
        AddMapping(mappings, "smallint", "smallint");
        AddMapping(mappings, "tinyint", "smallint");
        AddMapping(mappings, "boolean", "boolean");
        AddMapping(mappings, "bool", "boolean");
        AddMapping(mappings, "text", "text");
        AddMapping(mappings, "varchar", "varchar");
        AddMapping(mappings, "character varying", "varchar");
        AddMapping(mappings, "char", "char");
        AddMapping(mappings, "decimal", "decimal");
        AddMapping(mappings, "numeric", "numeric");
        AddMapping(mappings, "real", "real");
        AddMapping(mappings, "float", "real");
        AddMapping(mappings, "double", "double precision");
        AddMapping(mappings, "double precision", "double precision");
        AddMapping(mappings, "date", "date");
        AddMapping(mappings, "time", "time");
        AddMapping(mappings, "time without time zone", "time");
        AddMapping(mappings, "blob", "bytea");
        AddMapping(mappings, "bytea", "bytea");

        // MySQL-specific → PostgreSQL
        if (source is DatabaseType.MySQL or DatabaseType.MariaDB && target == DatabaseType.PostgreSQL)
        {
            AddMapping(mappings, "datetime", "timestamp");
            AddMapping(mappings, "timestamp", "timestamp");
            AddMapping(mappings, "mediumtext", "text");
            AddMapping(mappings, "longtext", "text");
            AddMapping(mappings, "mediumblob", "bytea");
            AddMapping(mappings, "longblob", "bytea");
            AddMapping(mappings, "enum", "varchar");
            AddMapping(mappings, "set", "varchar");
        }
        // PostgreSQL → MySQL
        else if (source == DatabaseType.PostgreSQL && target is DatabaseType.MySQL or DatabaseType.MariaDB)
        {
            AddMapping(mappings, "timestamp", "datetime");
            AddMapping(mappings, "timestamp without time zone", "datetime");
            AddMapping(mappings, "timestamp with time zone", "datetime");
            AddMapping(mappings, "serial", "int");
            AddMapping(mappings, "bigserial", "bigint");
            AddMapping(mappings, "uuid", "varchar(36)");
            AddMapping(mappings, "json", "json");
            AddMapping(mappings, "jsonb", "json");
            AddMapping(mappings, "inet", "varchar(45)");
        }
        // SQLite mappings
        else if (target == DatabaseType.SQLite)
        {
            AddMapping(mappings, "timestamp", "text");
            AddMapping(mappings, "datetime", "text");
            AddMapping(mappings, "boolean", "integer");
            AddMapping(mappings, "uuid", "text");
            AddMapping(mappings, "json", "text");
            AddMapping(mappings, "jsonb", "text");
        }
        else if (source == DatabaseType.SQLite)
        {
            AddMapping(mappings, "text", "text");
            AddMapping(mappings, "integer", "integer");
            AddMapping(mappings, "real", "real");
            AddMapping(mappings, "blob", "bytea");
        }

        return mappings;
    }

    private static void AddMapping(List<TypeMapping> list, string src, string tgt)
    {
        if (!list.Any(m => m.SourceType.Equals(src, StringComparison.OrdinalIgnoreCase)))
            list.Add(new TypeMapping { SourceType = src, TargetType = tgt });
    }
}

/// <summary>Represents a table available for migration.</summary>
public partial class MigrationTableItem : ObservableObject
{
    public string TableName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected = true;
}

/// <summary>Represents a data type mapping between source and target engines.</summary>
public partial class TypeMapping : ObservableObject
{
    public string SourceType { get; set; } = string.Empty;

    [ObservableProperty]
    private string _targetType = string.Empty;
}

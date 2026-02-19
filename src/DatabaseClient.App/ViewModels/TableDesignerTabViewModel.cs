using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the visual table designer tab.
/// Allows creating new tables or editing existing table structure.
/// </summary>
public partial class TableDesignerTabViewModel : TabViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private ConnectionInfo? _connectionInfo;
    private string? _database;
    private string? _schema;
    private readonly bool _isNewTable;
    private string _originalTableName = string.Empty;

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private string _generatedSql = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<ColumnDesignItem> Columns { get; } = [];
    public ObservableCollection<IndexDesignItem> Indexes { get; } = [];
    public ObservableCollection<ForeignKeyDesignItem> ForeignKeys { get; } = [];
    public ObservableCollection<CheckConstraintItem> CheckConstraints { get; } = [];

    /// <summary>Original columns snapshot for ALTER TABLE diff generation.</summary>
    private readonly List<ColumnDesignItem> _originalColumns = [];

    public override string TabIconKind => "TableEdit";

    /// <summary>Common SQL data types for the ComboBox.</summary>
    public static IReadOnlyList<string> CommonDataTypes { get; } =
    [
        // Numeric
        "INTEGER", "BIGINT", "SMALLINT", "SERIAL", "BIGSERIAL",
        "NUMERIC", "DECIMAL", "REAL", "DOUBLE PRECISION", "FLOAT",
        // String
        "VARCHAR(255)", "VARCHAR(50)", "CHAR(1)", "TEXT",
        // Date/Time
        "DATE", "TIME", "TIMESTAMP", "TIMESTAMPTZ", "INTERVAL",
        // Boolean
        "BOOLEAN",
        // Binary
        "BYTEA", "BLOB",
        // JSON
        "JSON", "JSONB",
        // UUID
        "UUID",
        // MySQL specific
        "INT", "TINYINT", "MEDIUMINT", "DATETIME", "ENUM", "SET",
        "LONGTEXT", "MEDIUMTEXT", "TINYTEXT",
        // SQLite
        "INTEGER PRIMARY KEY"
    ];

    /// <summary>ON DELETE/UPDATE actions for foreign keys.</summary>
    public static IReadOnlyList<string> FkActions { get; } =
    [
        "NO ACTION", "CASCADE", "SET NULL", "SET DEFAULT", "RESTRICT"
    ];

    public TableDesignerTabViewModel(IConnectionManager connectionManager, bool isNewTable = true)
    {
        _connectionManager = connectionManager;
        _isNewTable = isNewTable;
        Title = isNewTable ? "New Table" : "Edit Table";

        if (isNewTable)
        {
            // Start with one default column
            Columns.Add(new ColumnDesignItem { Name = "id", DataType = "INTEGER", IsPrimaryKey = true, IsNullable = false });
        }
    }

    public void SetConnection(ConnectionInfo connectionInfo, string database, string? schema = null)
    {
        _connectionInfo = connectionInfo;
        _database = database;
        _schema = schema;
    }

    /// <summary>
    /// Loads an existing table's structure for editing.
    /// </summary>
    public async Task LoadTableAsync(string tableName)
    {
        if (_connectionInfo is null || string.IsNullOrEmpty(_database))
            return;

        _originalTableName = tableName;
        TableName = tableName;
        Title = $"Design - {tableName}";

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            var columns = await provider.GetColumnsAsync(_database, tableName, _schema);

            Columns.Clear();
            _originalColumns.Clear();
            foreach (var col in columns)
            {
                var item = new ColumnDesignItem
                {
                    Name = col.Name,
                    DataType = col.DataType,
                    IsNullable = col.IsNullable,
                    IsPrimaryKey = col.IsPrimaryKey,
                    DefaultValue = col.DefaultValue ?? "",
                    IsOriginal = true,
                    OriginalName = col.Name
                };
                Columns.Add(item);
                _originalColumns.Add(new ColumnDesignItem
                {
                    Name = col.Name,
                    DataType = col.DataType,
                    IsNullable = col.IsNullable,
                    IsPrimaryKey = col.IsPrimaryKey,
                    DefaultValue = col.DefaultValue ?? "",
                    IsOriginal = true,
                    OriginalName = col.Name
                });
            }

            StatusMessage = $"Loaded {columns.Count} columns from {tableName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading table: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddColumn()
    {
        Columns.Add(new ColumnDesignItem
        {
            Name = $"column{Columns.Count + 1}",
            DataType = "VARCHAR(255)",
            IsNullable = true
        });
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void RemoveColumn(ColumnDesignItem? column)
    {
        if (column is not null)
        {
            Columns.Remove(column);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void MoveColumnUp(ColumnDesignItem? column)
    {
        if (column is null) return;
        var idx = Columns.IndexOf(column);
        if (idx > 0)
        {
            Columns.Move(idx, idx - 1);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void MoveColumnDown(ColumnDesignItem? column)
    {
        if (column is null) return;
        var idx = Columns.IndexOf(column);
        if (idx < Columns.Count - 1)
        {
            Columns.Move(idx, idx + 1);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void AddIndex()
    {
        Indexes.Add(new IndexDesignItem
        {
            Name = $"idx_{TableName}_{Indexes.Count + 1}",
            Columns = ""
        });
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void RemoveIndex(IndexDesignItem? index)
    {
        if (index is not null)
        {
            Indexes.Remove(index);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void AddForeignKey()
    {
        ForeignKeys.Add(new ForeignKeyDesignItem
        {
            Name = $"fk_{TableName}_{ForeignKeys.Count + 1}",
            OnDelete = "NO ACTION",
            OnUpdate = "NO ACTION"
        });
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void RemoveForeignKey(ForeignKeyDesignItem? fk)
    {
        if (fk is not null)
        {
            ForeignKeys.Remove(fk);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void AddCheckConstraint()
    {
        CheckConstraints.Add(new CheckConstraintItem
        {
            Name = $"chk_{TableName}_{CheckConstraints.Count + 1}",
            Expression = ""
        });
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void RemoveCheckConstraint(CheckConstraintItem? chk)
    {
        if (chk is not null)
        {
            CheckConstraints.Remove(chk);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void GenerateSql()
    {
        if (string.IsNullOrWhiteSpace(TableName))
        {
            StatusMessage = "Table name is required.";
            return;
        }

        if (Columns.Count == 0)
        {
            StatusMessage = "At least one column is required.";
            return;
        }

        // Validate column names are unique
        var dupes = Columns.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupes.Count > 0)
        {
            StatusMessage = $"Duplicate column names: {string.Join(", ", dupes)}";
            return;
        }

        var dbType = _connectionInfo?.DatabaseType ?? DatabaseType.PostgreSQL;
        GeneratedSql = _isNewTable
            ? GenerateCreateTableSql(dbType)
            : GenerateAlterTableSql(dbType);

        StatusMessage = "SQL generated. Review and execute.";
    }

    [RelayCommand]
    private async Task ExecuteSql()
    {
        if (_connectionInfo is null || string.IsNullOrWhiteSpace(GeneratedSql))
        {
            StatusMessage = "Generate SQL first.";
            return;
        }

        try
        {
            IsBusy = true;
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            await provider.ExecuteNonQueryAsync(GeneratedSql);
            StatusMessage = _isNewTable
                ? $"Table '{TableName}' created successfully."
                : $"Table '{TableName}' altered successfully.";
            HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string GenerateCreateTableSql(DatabaseType dbType)
    {
        var sb = new StringBuilder();
        var qualifiedName = GetQualifiedTableName(dbType);

        sb.AppendLine($"CREATE TABLE {qualifiedName} (");

        var columnDefs = new List<string>();
        var pkColumns = new List<string>();

        foreach (var col in Columns)
        {
            var def = $"    {QuoteIdentifier(col.Name, dbType)} {col.DataType}";
            if (!col.IsNullable)
                def += " NOT NULL";
            if (!string.IsNullOrWhiteSpace(col.DefaultValue))
                def += $" DEFAULT {col.DefaultValue}";
            columnDefs.Add(def);

            if (col.IsPrimaryKey)
                pkColumns.Add(QuoteIdentifier(col.Name, dbType));
        }

        sb.AppendLine(string.Join(",\n", columnDefs));

        // Primary key constraint
        if (pkColumns.Count > 0)
        {
            sb.AppendLine($"    ,CONSTRAINT pk_{TableName} PRIMARY KEY ({string.Join(", ", pkColumns)})");
        }

        // Foreign keys
        foreach (var fk in ForeignKeys)
        {
            if (!string.IsNullOrWhiteSpace(fk.LocalColumn) && !string.IsNullOrWhiteSpace(fk.ReferencedTable) &&
                !string.IsNullOrWhiteSpace(fk.ReferencedColumn))
            {
                sb.AppendLine($"    ,CONSTRAINT {QuoteIdentifier(fk.Name, dbType)} FOREIGN KEY ({QuoteIdentifier(fk.LocalColumn, dbType)})");
                sb.AppendLine($"        REFERENCES {fk.ReferencedTable} ({QuoteIdentifier(fk.ReferencedColumn, dbType)})");
                sb.AppendLine($"        ON DELETE {fk.OnDelete} ON UPDATE {fk.OnUpdate}");
            }
        }

        // Check constraints
        foreach (var chk in CheckConstraints)
        {
            if (!string.IsNullOrWhiteSpace(chk.Expression))
            {
                sb.AppendLine($"    ,CONSTRAINT {QuoteIdentifier(chk.Name, dbType)} CHECK ({chk.Expression})");
            }
        }

        sb.AppendLine(");");

        // Indexes
        foreach (var idx in Indexes)
        {
            if (!string.IsNullOrWhiteSpace(idx.Columns))
            {
                var unique = idx.IsUnique ? "UNIQUE " : "";
                sb.AppendLine($"\nCREATE {unique}INDEX {QuoteIdentifier(idx.Name, dbType)} ON {qualifiedName} ({idx.Columns});");
            }
        }

        return sb.ToString();
    }

    private string GenerateAlterTableSql(DatabaseType dbType)
    {
        var sb = new StringBuilder();
        var qualifiedName = GetQualifiedTableName(dbType);
        var currentNames = Columns.Select(c => c.OriginalName ?? c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Detect dropped columns (in original but not in current)
        foreach (var orig in _originalColumns)
        {
            if (!Columns.Any(c => string.Equals(c.OriginalName ?? c.Name, orig.Name, StringComparison.OrdinalIgnoreCase)))
            {
                if (dbType == DatabaseType.SQLite)
                    sb.AppendLine($"-- SQLite does not support DROP COLUMN directly (requires table recreation)");
                else
                    sb.AppendLine($"ALTER TABLE {qualifiedName} DROP COLUMN {QuoteIdentifier(orig.Name, dbType)};");
            }
        }

        // Detect modified columns (type or nullable changed)
        foreach (var col in Columns.Where(c => c.IsOriginal))
        {
            var orig = _originalColumns.FirstOrDefault(o => string.Equals(o.Name, col.OriginalName ?? col.Name, StringComparison.OrdinalIgnoreCase));
            if (orig is null) continue;

            var typeChanged = !string.Equals(orig.DataType, col.DataType, StringComparison.OrdinalIgnoreCase);
            var nullableChanged = orig.IsNullable != col.IsNullable;
            var defaultChanged = !string.Equals(orig.DefaultValue, col.DefaultValue, StringComparison.OrdinalIgnoreCase);
            var renamed = !string.Equals(orig.Name, col.Name, StringComparison.OrdinalIgnoreCase);

            if (renamed)
            {
                if (dbType == DatabaseType.PostgreSQL)
                    sb.AppendLine($"ALTER TABLE {qualifiedName} RENAME COLUMN {QuoteIdentifier(orig.Name, dbType)} TO {QuoteIdentifier(col.Name, dbType)};");
                else if (dbType is DatabaseType.MySQL or DatabaseType.MariaDB)
                    sb.AppendLine($"ALTER TABLE {qualifiedName} CHANGE COLUMN {QuoteIdentifier(orig.Name, dbType)} {QuoteIdentifier(col.Name, dbType)} {col.DataType}{(col.IsNullable ? "" : " NOT NULL")};");
            }

            if (typeChanged)
            {
                if (dbType == DatabaseType.PostgreSQL)
                    sb.AppendLine($"ALTER TABLE {qualifiedName} ALTER COLUMN {QuoteIdentifier(col.Name, dbType)} TYPE {col.DataType};");
                else if (dbType is DatabaseType.MySQL or DatabaseType.MariaDB)
                    sb.AppendLine($"ALTER TABLE {qualifiedName} MODIFY COLUMN {QuoteIdentifier(col.Name, dbType)} {col.DataType}{(col.IsNullable ? "" : " NOT NULL")};");
            }

            if (nullableChanged && !typeChanged)
            {
                if (dbType == DatabaseType.PostgreSQL)
                    sb.AppendLine($"ALTER TABLE {qualifiedName} ALTER COLUMN {QuoteIdentifier(col.Name, dbType)} {(col.IsNullable ? "DROP NOT NULL" : "SET NOT NULL")};");
                else if (dbType is DatabaseType.MySQL or DatabaseType.MariaDB)
                    sb.AppendLine($"ALTER TABLE {qualifiedName} MODIFY COLUMN {QuoteIdentifier(col.Name, dbType)} {col.DataType}{(col.IsNullable ? "" : " NOT NULL")};");
            }

            if (defaultChanged)
            {
                if (string.IsNullOrWhiteSpace(col.DefaultValue))
                    sb.AppendLine($"ALTER TABLE {qualifiedName} ALTER COLUMN {QuoteIdentifier(col.Name, dbType)} DROP DEFAULT;");
                else
                    sb.AppendLine($"ALTER TABLE {qualifiedName} ALTER COLUMN {QuoteIdentifier(col.Name, dbType)} SET DEFAULT {col.DefaultValue};");
            }
        }

        // Add new columns (those that are not marked as original)
        foreach (var col in Columns.Where(c => !c.IsOriginal))
        {
            var nullable = col.IsNullable ? "" : " NOT NULL";
            var defaultVal = !string.IsNullOrWhiteSpace(col.DefaultValue) ? $" DEFAULT {col.DefaultValue}" : "";
            sb.AppendLine($"ALTER TABLE {qualifiedName} ADD COLUMN {QuoteIdentifier(col.Name, dbType)} {col.DataType}{nullable}{defaultVal};");
        }

        // New indexes
        foreach (var idx in Indexes)
        {
            if (!string.IsNullOrWhiteSpace(idx.Columns))
            {
                var unique = idx.IsUnique ? "UNIQUE " : "";
                sb.AppendLine($"CREATE {unique}INDEX {QuoteIdentifier(idx.Name, dbType)} ON {qualifiedName} ({idx.Columns});");
            }
        }

        // New foreign keys
        foreach (var fk in ForeignKeys)
        {
            if (!string.IsNullOrWhiteSpace(fk.LocalColumn) && !string.IsNullOrWhiteSpace(fk.ReferencedTable))
            {
                sb.AppendLine($"ALTER TABLE {qualifiedName} ADD CONSTRAINT {QuoteIdentifier(fk.Name, dbType)}");
                sb.AppendLine($"    FOREIGN KEY ({QuoteIdentifier(fk.LocalColumn, dbType)})");
                sb.AppendLine($"    REFERENCES {fk.ReferencedTable} ({QuoteIdentifier(fk.ReferencedColumn, dbType)})");
                sb.AppendLine($"    ON DELETE {fk.OnDelete} ON UPDATE {fk.OnUpdate};");
            }
        }

        // New check constraints
        foreach (var chk in CheckConstraints)
        {
            if (!string.IsNullOrWhiteSpace(chk.Expression))
            {
                sb.AppendLine($"ALTER TABLE {qualifiedName} ADD CONSTRAINT {QuoteIdentifier(chk.Name, dbType)} CHECK ({chk.Expression});");
            }
        }

        if (sb.Length == 0)
            sb.AppendLine("-- No changes detected.");

        return sb.ToString();
    }

    private string GetQualifiedTableName(DatabaseType dbType)
    {
        if (!string.IsNullOrEmpty(_schema) && dbType is DatabaseType.PostgreSQL)
            return $"{QuoteIdentifier(_schema, dbType)}.{QuoteIdentifier(TableName, dbType)}";
        return QuoteIdentifier(TableName, dbType);
    }

    private static string QuoteIdentifier(string name, DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.MySQL or DatabaseType.MariaDB => $"`{name}`",
            _ => $"\"{name}\""
        };
    }
}

/// <summary>
/// Represents a column in the table designer.
/// </summary>
public partial class ColumnDesignItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _dataType = "VARCHAR(255)";
    [ObservableProperty] private bool _isNullable = true;
    [ObservableProperty] private bool _isPrimaryKey;
    [ObservableProperty] private string _defaultValue = string.Empty;
    [ObservableProperty] private string _comment = string.Empty;

    /// <summary>True if this column existed before editing (for ALTER TABLE generation).</summary>
    public bool IsOriginal { get; set; }

    /// <summary>Original column name for rename detection.</summary>
    public string? OriginalName { get; set; }
}

/// <summary>
/// Represents an index in the table designer.
/// </summary>
public partial class IndexDesignItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _columns = string.Empty;
    [ObservableProperty] private bool _isUnique;
}

/// <summary>
/// Represents a foreign key in the table designer.
/// </summary>
public partial class ForeignKeyDesignItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _localColumn = string.Empty;
    [ObservableProperty] private string _referencedTable = string.Empty;
    [ObservableProperty] private string _referencedColumn = string.Empty;
    [ObservableProperty] private string _onDelete = "NO ACTION";
    [ObservableProperty] private string _onUpdate = "NO ACTION";
}

/// <summary>
/// Represents a check constraint in the table designer.
/// </summary>
public partial class CheckConstraintItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _expression = string.Empty;
}

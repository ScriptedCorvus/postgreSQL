namespace DatabaseClient.Core.Models;

/// <summary>
/// Represents a database object in the schema tree.
/// </summary>
public enum DatabaseObjectType
{
    Server,
    Database,
    Schema,
    TableFolder,
    ViewFolder,
    FunctionFolder,
    TriggerFolder,
    SequenceFolder,
    IndexFolder,
    Table,
    View,
    Function,
    Column,
    Index,
    ForeignKey,
    Trigger,
    Sequence
}

/// <summary>
/// Describes a database object for the connection tree.
/// </summary>
public class DatabaseObjectInfo
{
    public string Name { get; set; } = string.Empty;
    public DatabaseObjectType ObjectType { get; set; }
    public string? Schema { get; set; }
    public string? ParentName { get; set; }
    public Dictionary<string, object?> Properties { get; set; } = [];
}

/// <summary>
/// Describes a column in a table.
/// </summary>
public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsAutoIncrement { get; set; }
    public int? MaxLength { get; set; }
    public int OrdinalPosition { get; set; }
    public string? Comment { get; set; }
}

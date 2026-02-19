using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DatabaseClient.Core.Models;

/// <summary>
/// Represents a table entity in an ER diagram.
/// </summary>
public partial class DiagramTable : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 180;

    [ObservableProperty]
    private double _height = 200;

    [ObservableProperty]
    private bool _isSelected;

    public ObservableCollection<DiagramColumn> Columns { get; } = [];
}

/// <summary>
/// Represents a column within a diagram table.
/// </summary>
public class DiagramColumn
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsNullable { get; set; }
}

/// <summary>
/// Represents a relationship (foreign key) connector between two tables in the ER diagram.
/// </summary>
public partial class DiagramRelationship : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Source (referencing) table name.</summary>
    public string SourceTable { get; set; } = string.Empty;

    /// <summary>Source column name (FK column).</summary>
    public string SourceColumn { get; set; } = string.Empty;

    /// <summary>Target (referenced) table name.</summary>
    public string TargetTable { get; set; } = string.Empty;

    /// <summary>Target column name (PK column).</summary>
    public string TargetColumn { get; set; } = string.Empty;

    /// <summary>Relationship cardinality label (e.g. "1:N", "1:1", "N:M").</summary>
    public string Cardinality { get; set; } = "1:N";

    [ObservableProperty]
    private bool _isSelected;
}

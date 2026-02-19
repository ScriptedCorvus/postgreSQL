using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the ER (Entity-Relationship) diagram editor.
/// Supports reverse engineering from schema, manual editing, zoom/pan, and export.
/// </summary>
public partial class DiagramTabViewModel : TabViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private ConnectionInfo? _connectionInfo;
    private string _database = string.Empty;

    public ObservableCollection<DiagramTable> Tables { get; } = [];
    public ObservableCollection<DiagramRelationship> Relationships { get; } = [];

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private double _panX;

    [ObservableProperty]
    private double _panY;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private DiagramTable? _selectedTable;

    [ObservableProperty]
    private string _generatedSql = string.Empty;

    public override string TabIconKind => "GraphOutline";

    /// <summary>Delegate set by View to export diagram as image.</summary>
    public Func<string, Task>? ExportDiagramDelegate { get; set; }

    public DiagramTabViewModel(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        Title = "ER Diagram";
    }

    public void SetConnection(ConnectionInfo connectionInfo, string database)
    {
        _connectionInfo = connectionInfo;
        _database = database;
        Title = $"ER Diagram - {database}";
    }

    /// <summary>
    /// Reverse engineer: loads all tables and foreign keys from the database schema.
    /// </summary>
    [RelayCommand]
    private async Task ReverseEngineer()
    {
        if (_connectionInfo is null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Reverse engineering schema...";

            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            await provider.ChangeDatabaseAsync(_database);

            // Load tables
            var tableInfos = await provider.GetTablesAsync(_database);
            Tables.Clear();
            Relationships.Clear();

            double startX = 50, startY = 50;
            int col = 0;

            foreach (var tableInfo in tableInfos)
            {
                var columns = await provider.GetColumnsAsync(_database, tableInfo.Name);
                var diagramTable = new DiagramTable
                {
                    Name = tableInfo.Name,
                    X = startX + (col % 4) * 250,
                    Y = startY + (col / 4) * 300,
                };

                foreach (var colInfo in columns)
                {
                    diagramTable.Columns.Add(new DiagramColumn
                    {
                        Name = colInfo.Name,
                        DataType = colInfo.DataType,
                        IsPrimaryKey = colInfo.IsPrimaryKey,
                        IsNullable = colInfo.IsNullable,
                    });
                }

                // Auto-size height based on column count
                diagramTable.Height = Math.Max(120, 40 + diagramTable.Columns.Count * 22);
                Tables.Add(diagramTable);
                col++;
            }

            // Load foreign keys to build relationships
            foreach (var table in Tables)
            {
                try
                {
                    var fkSql = GetForeignKeySql(table.Name);
                    if (string.IsNullOrEmpty(fkSql)) continue;

                    var result = await provider.ExecuteQueryAsync(fkSql);
                    if (result.IsSuccessful && result.ResultSet is not null)
                    {
                        foreach (System.Data.DataRow row in result.ResultSet.Rows)
                        {
                            var fkName = row["constraint_name"]?.ToString() ?? "";
                            var sourcCol = row["column_name"]?.ToString() ?? "";
                            var targetTable = row["referenced_table"]?.ToString() ?? "";
                            var targetCol = row["referenced_column"]?.ToString() ?? "";

                            if (!string.IsNullOrEmpty(targetTable))
                            {
                                Relationships.Add(new DiagramRelationship
                                {
                                    Name = fkName,
                                    SourceTable = table.Name,
                                    SourceColumn = sourcCol,
                                    TargetTable = targetTable,
                                    TargetColumn = targetCol,
                                    Cardinality = "1:N"
                                });

                                // Mark column as FK
                                var col2 = table.Columns.FirstOrDefault(c => c.Name == sourcCol);
                                if (col2 is not null) col2.IsForeignKey = true;
                            }
                        }
                    }
                }
                catch
                {
                    // Best effort for FK loading
                }
            }

            StatusMessage = $"Loaded {Tables.Count} table(s) and {Relationships.Count} relationship(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string GetForeignKeySql(string tableName)
    {
        if (_connectionInfo is null) return "";

        return _connectionInfo.DatabaseType switch
        {
            DatabaseType.PostgreSQL => $"""
                SELECT tc.constraint_name,
                       kcu.column_name,
                       ccu.table_name AS referenced_table,
                       ccu.column_name AS referenced_column
                FROM information_schema.table_constraints AS tc
                JOIN information_schema.key_column_usage AS kcu ON tc.constraint_name = kcu.constraint_name
                JOIN information_schema.constraint_column_usage AS ccu ON ccu.constraint_name = tc.constraint_name
                WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_name = '{tableName}'
                """,
            DatabaseType.MySQL or DatabaseType.MariaDB => $"""
                SELECT CONSTRAINT_NAME AS constraint_name,
                       COLUMN_NAME AS column_name,
                       REFERENCED_TABLE_NAME AS referenced_table,
                       REFERENCED_COLUMN_NAME AS referenced_column
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                WHERE TABLE_NAME = '{tableName}'
                  AND REFERENCED_TABLE_NAME IS NOT NULL
                  AND TABLE_SCHEMA = DATABASE()
                """,
            DatabaseType.SQLite => $"PRAGMA foreign_key_list(\"{tableName}\")",
            _ => ""
        };
    }

    /// <summary>Forward engineer: generates CREATE TABLE SQL from diagram.</summary>
    [RelayCommand]
    private void GenerateSql()
    {
        if (Tables.Count == 0)
        {
            GeneratedSql = "-- No tables in diagram";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("-- Generated by DatabaseClient ER Diagram Editor");
        sb.AppendLine();

        foreach (var table in Tables)
        {
            sb.AppendLine($"CREATE TABLE \"{table.Name}\" (");
            var lines = new List<string>();
            var pkCols = new List<string>();

            foreach (var col in table.Columns)
            {
                var line = $"    \"{col.Name}\" {col.DataType}";
                if (!col.IsNullable) line += " NOT NULL";
                lines.Add(line);
                if (col.IsPrimaryKey) pkCols.Add($"\"{col.Name}\"");
            }

            if (pkCols.Count > 0)
                lines.Add($"    PRIMARY KEY ({string.Join(", ", pkCols)})");

            sb.AppendLine(string.Join(",\n", lines));
            sb.AppendLine(");");
            sb.AppendLine();
        }

        // Foreign key constraints
        foreach (var rel in Relationships)
        {
            sb.AppendLine($"ALTER TABLE \"{rel.SourceTable}\" ADD CONSTRAINT \"{rel.Name}\"");
            sb.AppendLine($"    FOREIGN KEY (\"{rel.SourceColumn}\") REFERENCES \"{rel.TargetTable}\" (\"{rel.TargetColumn}\");");
            sb.AppendLine();
        }

        GeneratedSql = sb.ToString();
        StatusMessage = "SQL generated successfully.";
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(3.0, ZoomLevel + 0.1);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(0.3, ZoomLevel - 0.1);
    }

    [RelayCommand]
    private void ZoomReset()
    {
        ZoomLevel = 1.0;
        PanX = 0;
        PanY = 0;
    }

    [RelayCommand]
    private void AutoLayout()
    {
        // Simple grid auto-layout
        double x = 50, y = 50;
        int col = 0;
        foreach (var table in Tables)
        {
            table.X = x + (col % 4) * 250;
            table.Y = y + (col / 4) * 300;
            col++;
        }
        StatusMessage = "Auto-layout applied.";
    }

    [RelayCommand]
    private async Task ExportDiagram()
    {
        if (ExportDiagramDelegate is not null)
            await ExportDiagramDelegate("er_diagram.png");
    }

    [RelayCommand]
    private void AddTable()
    {
        var newTable = new DiagramTable
        {
            Name = $"new_table_{Tables.Count + 1}",
            X = 100 + Tables.Count * 50,
            Y = 100 + Tables.Count * 50,
        };
        newTable.Columns.Add(new DiagramColumn { Name = "id", DataType = "INTEGER", IsPrimaryKey = true });
        Tables.Add(newTable);
        SelectedTable = newTable;
        StatusMessage = $"Added table '{newTable.Name}'.";
    }

    [RelayCommand]
    private void RemoveTable()
    {
        if (SelectedTable is null) return;
        var name = SelectedTable.Name;

        // Remove related relationships
        var relsToRemove = Relationships
            .Where(r => r.SourceTable == name || r.TargetTable == name)
            .ToList();
        foreach (var rel in relsToRemove)
            Relationships.Remove(rel);

        Tables.Remove(SelectedTable);
        SelectedTable = null;
        StatusMessage = $"Removed table '{name}'.";
    }
}

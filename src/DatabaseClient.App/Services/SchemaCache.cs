using System.Collections.Generic;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.App.Services;

/// <summary>
/// Maintains a cached copy of the database schema for autocomplete purposes.
/// Provides quick access to table names, column names, and functions.
/// </summary>
public class SchemaCache
{
    private readonly Dictionary<string, List<string>> _tableColumns = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _tableNames = [];
    private readonly List<string> _viewNames = [];
    private readonly List<string> _functionNames = [];
    private readonly List<string> _schemaNames = [];

    public IReadOnlyList<string> TableNames => _tableNames;
    public IReadOnlyList<string> ViewNames => _viewNames;
    public IReadOnlyList<string> FunctionNames => _functionNames;
    public IReadOnlyList<string> SchemaNames => _schemaNames;

    /// <summary>
    /// Gets the column names for a given table.
    /// </summary>
    public IReadOnlyList<string> GetColumns(string tableName)
    {
        return _tableColumns.TryGetValue(tableName, out var cols) ? cols : [];
    }

    /// <summary>
    /// All known object names (tables, views, functions) combined.
    /// </summary>
    public IEnumerable<string> AllObjectNames
    {
        get
        {
            foreach (var t in _tableNames) yield return t;
            foreach (var v in _viewNames) yield return v;
            foreach (var f in _functionNames) yield return f;
        }
    }

    /// <summary>
    /// Refreshes the schema cache from the given database provider.
    /// </summary>
    public async Task RefreshAsync(IDatabaseProvider provider, string database, string? schema = null, CancellationToken ct = default)
    {
        _tableNames.Clear();
        _viewNames.Clear();
        _functionNames.Clear();
        _schemaNames.Clear();
        _tableColumns.Clear();

        try
        {
            // Load schemas
            var schemas = await provider.GetSchemasAsync(database, ct);
            _schemaNames.AddRange(schemas);

            var targetSchema = schema ?? (schemas.Count > 0 ? schemas[0] : null);

            // Load tables
            var tables = await provider.GetTablesAsync(database, targetSchema, ct);
            foreach (var t in tables)
                _tableNames.Add(t.Name);

            // Load views
            var views = await provider.GetViewsAsync(database, targetSchema, ct);
            foreach (var v in views)
                _viewNames.Add(v.Name);

            // Load functions
            var functions = await provider.GetFunctionsAsync(database, targetSchema, ct);
            foreach (var f in functions)
                _functionNames.Add(f.Name);

            // Load columns for each table
            foreach (var t in tables)
            {
                try
                {
                    var columns = await provider.GetColumnsAsync(database, t.Name, targetSchema, ct);
                    _tableColumns[t.Name] = columns.Select(c => c.Name).ToList();
                }
                catch
                {
                    // Skip tables that can't be inspected
                }
            }
        }
        catch
        {
            // Best effort — cache may be partial
        }
    }

    /// <summary>
    /// SQL keywords grouped by category for autocompletion.
    /// </summary>
    public static readonly string[] SqlKeywords =
    [
        // DML
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "EXISTS", "BETWEEN",
        "LIKE", "ILIKE", "IS", "NULL", "AS", "ON", "JOIN", "INNER", "LEFT", "RIGHT",
        "FULL", "OUTER", "CROSS", "NATURAL", "USING", "ORDER", "BY", "ASC", "DESC",
        "GROUP", "HAVING", "LIMIT", "OFFSET", "FETCH", "FIRST", "NEXT", "ROWS", "ONLY",
        "DISTINCT", "ALL", "UNION", "INTERSECT", "EXCEPT", "WITH", "RECURSIVE",
        "INSERT", "INTO", "VALUES", "DEFAULT", "UPDATE", "SET", "DELETE",
        "RETURNING", "CONFLICT", "DO", "NOTHING",
        // DDL
        "CREATE", "ALTER", "DROP", "TABLE", "VIEW", "INDEX", "FUNCTION", "PROCEDURE",
        "TRIGGER", "SEQUENCE", "SCHEMA", "DATABASE", "IF", "CASCADE", "RESTRICT",
        "ADD", "COLUMN", "RENAME", "TO", "CONSTRAINT", "PRIMARY", "KEY",
        "FOREIGN", "REFERENCES", "UNIQUE", "CHECK", "DEFAULT",
        // Data Types
        "INTEGER", "INT", "BIGINT", "SMALLINT", "SERIAL", "BIGSERIAL",
        "VARCHAR", "CHAR", "TEXT", "BOOLEAN", "BOOL", "DATE", "TIME",
        "TIMESTAMP", "TIMESTAMPTZ", "INTERVAL", "NUMERIC", "DECIMAL",
        "REAL", "FLOAT", "DOUBLE", "PRECISION", "JSON", "JSONB", "UUID",
        "BYTEA", "BLOB", "ARRAY",
        // TCL
        "BEGIN", "COMMIT", "ROLLBACK", "SAVEPOINT", "TRANSACTION",
        // Functions
        "COUNT", "SUM", "AVG", "MIN", "MAX", "COALESCE", "NULLIF",
        "CAST", "CASE", "WHEN", "THEN", "ELSE", "END",
        "LOWER", "UPPER", "TRIM", "SUBSTRING", "LENGTH", "CONCAT",
        "NOW", "CURRENT_TIMESTAMP", "CURRENT_DATE", "CURRENT_TIME",
        "EXTRACT", "DATE_TRUNC", "TO_CHAR", "TO_DATE", "TO_NUMBER",
        "ROW_NUMBER", "RANK", "DENSE_RANK", "OVER", "PARTITION",
        "LAG", "LEAD", "FIRST_VALUE", "LAST_VALUE",
        // Other
        "EXPLAIN", "ANALYZE", "VACUUM", "TRUNCATE", "GRANT", "REVOKE",
        "COPY", "SHOW", "DESCRIBE", "USE"
    ];
}

using System.Data.Common;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Abstraction for database provider operations. Each supported engine
/// (PostgreSQL, MySQL/MariaDB, SQLite) implements this interface.
/// </summary>
public interface IDatabaseProvider : IAsyncDisposable
{
    /// <summary>The type of database this provider connects to.</summary>
    DatabaseType DatabaseType { get; }

    /// <summary>Gets the underlying DbConnection (for advanced scenarios).</summary>
    DbConnection? Connection { get; }

    /// <summary>Whether the connection is currently open.</summary>
    bool IsConnected { get; }

    // ── Connection ─────────────────────────────────────────────────────────

    Task OpenAsync(ConnectionInfo connectionInfo, CancellationToken ct = default);
    Task CloseAsync();
    Task<bool> TestConnectionAsync(ConnectionInfo connectionInfo, CancellationToken ct = default);
    Task<string> GetServerVersionAsync(CancellationToken ct = default);

    // ── Schema discovery ───────────────────────────────────────────────────

    Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetSchemasAsync(string database, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseObjectInfo>> GetTablesAsync(string database, string? schema = null, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseObjectInfo>> GetViewsAsync(string database, string? schema = null, CancellationToken ct = default);
    Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string database, string table, string? schema = null, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseObjectInfo>> GetIndexesAsync(string database, string table, string? schema = null, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseObjectInfo>> GetFunctionsAsync(string database, string? schema = null, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseObjectInfo>> GetTriggersAsync(string database, string? schema = null, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseObjectInfo>> GetSequencesAsync(string database, string? schema = null, CancellationToken ct = default);

    // ── Query execution ────────────────────────────────────────────────────

    Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken ct = default);
    Task<QueryResult> ExecuteQueryAsync(string sql, IReadOnlyDictionary<string, object?> parameters, CancellationToken ct = default);
    Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default);
    Task<object?> ExecuteScalarAsync(string sql, CancellationToken ct = default);

    // ── Database switching ─────────────────────────────────────────────────

    Task ChangeDatabaseAsync(string database, CancellationToken ct = default);
}

using System.Data;
using DatabaseClient.Core.Models;
using Microsoft.Data.Sqlite;

namespace DatabaseClient.Data.Providers;

/// <summary>
/// Database provider implementation for SQLite using Microsoft.Data.Sqlite.
/// </summary>
public class SqliteProvider : DatabaseProviderBase
{
    public override DatabaseType DatabaseType => DatabaseType.SQLite;

    public override async Task OpenAsync(ConnectionInfo connectionInfo, CancellationToken ct = default)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = connectionInfo.DatabaseFilePath,
            Mode = File.Exists(connectionInfo.DatabaseFilePath)
                ? SqliteOpenMode.ReadWriteCreate
                : SqliteOpenMode.ReadWriteCreate,
        };

        if (!string.IsNullOrEmpty(connectionInfo.Password))
            csb.Password = connectionInfo.Password;

        DbConnection = new SqliteConnection(csb.ConnectionString);
        await DbConnection.OpenAsync(ct);

        // Enable WAL mode for better concurrency
        await ExecuteNonQueryAsync("PRAGMA journal_mode=WAL", ct);
    }

    public override Task<string> GetServerVersionAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        return Task.FromResult($"SQLite {DbConnection!.ServerVersion}");
    }

    public override Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken ct = default)
    {
        // SQLite has a single database (the file)
        EnsureConnected();
        var conn = (SqliteConnection)DbConnection!;
        var dbName = Path.GetFileNameWithoutExtension(conn.DataSource);
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { dbName });
    }

    public override Task<IReadOnlyList<string>> GetSchemasAsync(string database, CancellationToken ct = default)
    {
        // SQLite doesn't have schemas
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { "main" });
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetTablesAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        const string sql = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["name"].ToString()!,
                Schema = "main",
                ObjectType = DatabaseObjectType.Table
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetViewsAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        const string sql = "SELECT name FROM sqlite_master WHERE type = 'view' ORDER BY name";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["name"].ToString()!,
                Schema = "main",
                ObjectType = DatabaseObjectType.View
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string database, string table, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var sql = $"PRAGMA table_info('{table}')";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new ColumnInfo
            {
                Name = r["name"].ToString()!,
                DataType = r["type"].ToString()!,
                IsNullable = Convert.ToInt32(r["notnull"]) == 0,
                DefaultValue = r["dflt_value"] is DBNull ? null : r["dflt_value"]?.ToString(),
                IsPrimaryKey = Convert.ToInt32(r["pk"]) > 0,
                OrdinalPosition = Convert.ToInt32(r["cid"])
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetIndexesAsync(string database, string table, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var sql = $"PRAGMA index_list('{table}')";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["name"].ToString()!,
                Schema = "main",
                ObjectType = DatabaseObjectType.Index,
                Properties = new Dictionary<string, object?>
                {
                    ["unique"] = Convert.ToInt32(r["unique"]) == 1
                }
            }).ToList() ?? [];
    }

    public override Task<IReadOnlyList<DatabaseObjectInfo>> GetFunctionsAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        // SQLite doesn't support stored functions
        return Task.FromResult<IReadOnlyList<DatabaseObjectInfo>>([]);
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetTriggersAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var sql = "SELECT name, tbl_name, sql FROM sqlite_master WHERE type = 'trigger' ORDER BY name";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["name"].ToString()!,
                ObjectType = DatabaseObjectType.Trigger,
                ParentName = r["tbl_name"].ToString(),
                Properties = new Dictionary<string, object?>
                {
                    ["table"] = r["tbl_name"].ToString(),
                    ["sql"] = r["sql"].ToString()
                }
            }).ToList() ?? [];
    }

    public override Task<IReadOnlyList<DatabaseObjectInfo>> GetSequencesAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        // SQLite doesn't have sequences (uses AUTOINCREMENT)
        return Task.FromResult<IReadOnlyList<DatabaseObjectInfo>>([]);
    }

    public override Task ChangeDatabaseAsync(string database, CancellationToken ct = default)
    {
        // SQLite doesn't support changing database — it's a single file
        return Task.CompletedTask;
    }
}

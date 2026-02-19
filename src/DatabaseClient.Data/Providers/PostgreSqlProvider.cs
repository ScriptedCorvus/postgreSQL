using System.Data;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using Npgsql;

namespace DatabaseClient.Data.Providers;

/// <summary>
/// Database provider implementation for PostgreSQL using Npgsql.
/// </summary>
public class PostgreSqlProvider : DatabaseProviderBase
{
    public override DatabaseType DatabaseType => DatabaseType.PostgreSQL;

    public override async Task OpenAsync(ConnectionInfo connectionInfo, CancellationToken ct = default)
    {
        var (host, port) = await ResolveEndpointAsync(connectionInfo, ct);

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Username = connectionInfo.Username,
            Password = connectionInfo.Password,
            Database = string.IsNullOrEmpty(connectionInfo.DefaultDatabase) ? "postgres" : connectionInfo.DefaultDatabase,
            Timeout = 15,
            CommandTimeout = 30,
        };

        if (connectionInfo.UseSsl)
        {
            csb.SslMode = SslMode.Require;
            if (!string.IsNullOrEmpty(connectionInfo.SslCaCertificatePath))
                csb.RootCertificate = connectionInfo.SslCaCertificatePath;
        }

        DbConnection = new NpgsqlConnection(csb.ConnectionString);
        await DbConnection.OpenAsync(ct);
    }

    public override async Task<string> GetServerVersionAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        var version = await ExecuteScalarAsync("SELECT version()", ct);
        return version?.ToString() ?? DbConnection!.ServerVersion;
    }

    public override async Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        const string sql = "SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => r["datname"].ToString()!)
            .ToList() ?? [];
    }

    public override async Task<IReadOnlyList<string>> GetSchemasAsync(string database, CancellationToken ct = default)
    {
        EnsureConnected();
        const string sql = @"
            SELECT schema_name FROM information_schema.schemata 
            WHERE schema_name NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
            ORDER BY schema_name";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => r["schema_name"].ToString()!)
            .ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetTablesAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        schema ??= "public";
        var sql = $@"
            SELECT table_name, table_schema
            FROM information_schema.tables 
            WHERE table_schema = '{schema}' AND table_type = 'BASE TABLE'
            ORDER BY table_name";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["table_name"].ToString()!,
                Schema = r["table_schema"].ToString(),
                ObjectType = DatabaseObjectType.Table
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetViewsAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        schema ??= "public";
        var sql = $@"
            SELECT table_name, table_schema
            FROM information_schema.views 
            WHERE table_schema = '{schema}'
            ORDER BY table_name";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["table_name"].ToString()!,
                Schema = r["table_schema"].ToString(),
                ObjectType = DatabaseObjectType.View
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string database, string table, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        schema ??= "public";
        var sql = $@"
            SELECT c.column_name, c.data_type, c.is_nullable, c.column_default,
                   c.character_maximum_length, c.ordinal_position,
                   CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS is_pk
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu 
                    ON tc.constraint_name = kcu.constraint_name
                WHERE tc.table_name = '{table}' AND tc.table_schema = '{schema}' 
                    AND tc.constraint_type = 'PRIMARY KEY'
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_name = '{table}' AND c.table_schema = '{schema}'
            ORDER BY c.ordinal_position";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new ColumnInfo
            {
                Name = r["column_name"].ToString()!,
                DataType = r["data_type"].ToString()!,
                IsNullable = r["is_nullable"].ToString() == "YES",
                DefaultValue = r["column_default"] is DBNull ? null : r["column_default"]?.ToString(),
                IsPrimaryKey = Convert.ToBoolean(r["is_pk"]),
                MaxLength = r["character_maximum_length"] is DBNull ? null : Convert.ToInt32(r["character_maximum_length"]),
                OrdinalPosition = Convert.ToInt32(r["ordinal_position"])
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetIndexesAsync(string database, string table, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        schema ??= "public";
        var sql = $@"
            SELECT indexname, indexdef 
            FROM pg_indexes 
            WHERE schemaname = '{schema}' AND tablename = '{table}'
            ORDER BY indexname";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["indexname"].ToString()!,
                Schema = schema,
                ObjectType = DatabaseObjectType.Index,
                Properties = new Dictionary<string, object?> { ["definition"] = r["indexdef"].ToString() }
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetFunctionsAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        schema ??= "public";
        var sql = $@"
            SELECT routine_name, routine_type
            FROM information_schema.routines 
            WHERE routine_schema = '{schema}'
            ORDER BY routine_name";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["routine_name"].ToString()!,
                Schema = schema,
                ObjectType = DatabaseObjectType.Function,
                Properties = new Dictionary<string, object?> { ["type"] = r["routine_type"].ToString() }
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetTriggersAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        schema ??= "public";
        var sql = $@"
            SELECT trigger_name, event_manipulation, event_object_table, action_timing
            FROM information_schema.triggers 
            WHERE trigger_schema = '{schema}'
            ORDER BY event_object_table, trigger_name";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["trigger_name"].ToString()!,
                Schema = schema,
                ObjectType = DatabaseObjectType.Trigger,
                ParentName = r["event_object_table"].ToString(),
                Properties = new Dictionary<string, object?>
                {
                    ["event"] = r["event_manipulation"].ToString(),
                    ["timing"] = r["action_timing"].ToString(),
                    ["table"] = r["event_object_table"].ToString()
                }
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetSequencesAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        schema ??= "public";
        var sql = $@"
            SELECT sequence_name, data_type, start_value, increment, minimum_value, maximum_value
            FROM information_schema.sequences 
            WHERE sequence_schema = '{schema}'
            ORDER BY sequence_name";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["sequence_name"].ToString()!,
                Schema = schema,
                ObjectType = DatabaseObjectType.Sequence,
                Properties = new Dictionary<string, object?>
                {
                    ["data_type"] = r["data_type"].ToString(),
                    ["start_value"] = r["start_value"].ToString(),
                    ["increment"] = r["increment"].ToString()
                }
            }).ToList() ?? [];
    }
}

using System.Data;
using DatabaseClient.Core.Models;
using MySqlConnector;

namespace DatabaseClient.Data.Providers;

/// <summary>
/// Database provider implementation for MySQL and MariaDB using MySqlConnector.
/// </summary>
public class MySqlProvider : DatabaseProviderBase
{
    private readonly DatabaseType _databaseType;

    public MySqlProvider(DatabaseType databaseType = DatabaseType.MySQL)
    {
        _databaseType = databaseType;
    }

    public override DatabaseType DatabaseType => _databaseType;

    public override async Task OpenAsync(ConnectionInfo connectionInfo, CancellationToken ct = default)
    {
        var (host, port) = await ResolveEndpointAsync(connectionInfo, ct);

        var csb = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = (uint)port,
            UserID = connectionInfo.Username,
            Password = connectionInfo.Password,
            Database = string.IsNullOrEmpty(connectionInfo.DefaultDatabase) ? null : connectionInfo.DefaultDatabase,
            ConnectionTimeout = 15,
            DefaultCommandTimeout = 30,
        };

        if (connectionInfo.UseSsl)
        {
            csb.SslMode = MySqlSslMode.Required;
            if (!string.IsNullOrEmpty(connectionInfo.SslCaCertificatePath))
                csb.SslCa = connectionInfo.SslCaCertificatePath;
            if (!string.IsNullOrEmpty(connectionInfo.SslClientCertificatePath))
                csb.SslCert = connectionInfo.SslClientCertificatePath;
            if (!string.IsNullOrEmpty(connectionInfo.SslClientKeyPath))
                csb.SslKey = connectionInfo.SslClientKeyPath;
        }

        DbConnection = new MySqlConnection(csb.ConnectionString);
        await DbConnection.OpenAsync(ct);
    }

    public override async Task<string> GetServerVersionAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        var version = await ExecuteScalarAsync("SELECT VERSION()", ct);
        return version?.ToString() ?? DbConnection!.ServerVersion;
    }

    public override async Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        const string sql = "SHOW DATABASES";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => r[0].ToString()!)
            .ToList() ?? [];
    }

    public override Task<IReadOnlyList<string>> GetSchemasAsync(string database, CancellationToken ct = default)
    {
        // MySQL doesn't have schemas in the same way; database = schema
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { database });
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetTablesAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var sql = $@"
            SELECT TABLE_NAME 
            FROM information_schema.TABLES 
            WHERE TABLE_SCHEMA = '{database}' AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["TABLE_NAME"].ToString()!,
                Schema = database,
                ObjectType = DatabaseObjectType.Table
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetViewsAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var sql = $@"
            SELECT TABLE_NAME 
            FROM information_schema.VIEWS 
            WHERE TABLE_SCHEMA = '{database}'
            ORDER BY TABLE_NAME";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["TABLE_NAME"].ToString()!,
                Schema = database,
                ObjectType = DatabaseObjectType.View
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string database, string table, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var sql = $@"
            SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE, c.COLUMN_DEFAULT,
                   c.CHARACTER_MAXIMUM_LENGTH, c.ORDINAL_POSITION, c.COLUMN_KEY, c.EXTRA
            FROM information_schema.COLUMNS c
            WHERE c.TABLE_SCHEMA = '{database}' AND c.TABLE_NAME = '{table}'
            ORDER BY c.ORDINAL_POSITION";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new ColumnInfo
            {
                Name = r["COLUMN_NAME"].ToString()!,
                DataType = r["DATA_TYPE"].ToString()!,
                IsNullable = r["IS_NULLABLE"].ToString() == "YES",
                DefaultValue = r["COLUMN_DEFAULT"] is DBNull ? null : r["COLUMN_DEFAULT"]?.ToString(),
                IsPrimaryKey = r["COLUMN_KEY"].ToString() == "PRI",
                IsAutoIncrement = r["EXTRA"].ToString()?.Contains("auto_increment") == true,
                MaxLength = r["CHARACTER_MAXIMUM_LENGTH"] is DBNull ? null : Convert.ToInt32(r["CHARACTER_MAXIMUM_LENGTH"]),
                OrdinalPosition = Convert.ToInt32(r["ORDINAL_POSITION"])
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetIndexesAsync(string database, string table, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var sql = $"SHOW INDEX FROM `{table}` FROM `{database}`";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .GroupBy(r => r["Key_name"].ToString()!)
            .Select(g => new DatabaseObjectInfo
            {
                Name = g.Key,
                Schema = database,
                ObjectType = DatabaseObjectType.Index,
                Properties = new Dictionary<string, object?>
                {
                    ["columns"] = string.Join(", ", g.Select(r => r["Column_name"].ToString())),
                    ["unique"] = g.First()["Non_unique"].ToString() == "0"
                }
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetFunctionsAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var sql = $@"
            SELECT ROUTINE_NAME, ROUTINE_TYPE
            FROM information_schema.ROUTINES 
            WHERE ROUTINE_SCHEMA = '{database}'
            ORDER BY ROUTINE_NAME";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["ROUTINE_NAME"].ToString()!,
                Schema = database,
                ObjectType = DatabaseObjectType.Function,
                Properties = new Dictionary<string, object?> { ["type"] = r["ROUTINE_TYPE"].ToString() }
            }).ToList() ?? [];
    }

    public override async Task<IReadOnlyList<DatabaseObjectInfo>> GetTriggersAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var sql = $@"
            SELECT TRIGGER_NAME, EVENT_MANIPULATION, EVENT_OBJECT_TABLE, ACTION_TIMING
            FROM information_schema.TRIGGERS 
            WHERE TRIGGER_SCHEMA = '{database}'
            ORDER BY EVENT_OBJECT_TABLE, TRIGGER_NAME";
        var result = await ExecuteQueryAsync(sql, ct);
        return result.ResultSet?.AsEnumerable()
            .Select(r => new DatabaseObjectInfo
            {
                Name = r["TRIGGER_NAME"].ToString()!,
                Schema = database,
                ObjectType = DatabaseObjectType.Trigger,
                ParentName = r["EVENT_OBJECT_TABLE"].ToString(),
                Properties = new Dictionary<string, object?>
                {
                    ["event"] = r["EVENT_MANIPULATION"].ToString(),
                    ["timing"] = r["ACTION_TIMING"].ToString(),
                    ["table"] = r["EVENT_OBJECT_TABLE"].ToString()
                }
            }).ToList() ?? [];
    }

    public override Task<IReadOnlyList<DatabaseObjectInfo>> GetSequencesAsync(string database, string? schema = null, CancellationToken ct = default)
    {
        // MySQL/MariaDB does not have sequences (MariaDB 10.3+ has them but via different syntax)
        return Task.FromResult<IReadOnlyList<DatabaseObjectInfo>>([]);
    }
}

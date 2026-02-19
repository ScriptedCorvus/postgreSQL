using System.IO;
using System.Text.Json;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Persists user query snippets to a JSON file and provides built-in snippets per database engine.
/// </summary>
public class JsonQuerySnippetService : IQuerySnippetService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<QuerySnippet>? _snippets;

    public JsonQuerySnippetService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseClient", "snippets.json");
    }

    public async Task<IReadOnlyList<QuerySnippet>> GetAllAsync()
    {
        await LoadIfNeededAsync();
        return _snippets!.AsReadOnly();
    }

    public async Task<IReadOnlyList<QuerySnippet>> SearchAsync(string searchText)
    {
        await LoadIfNeededAsync();
        return _snippets!
            .Where(s => s.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                     || s.Sql.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                     || s.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                     || s.Tags.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    public async Task SaveAsync(QuerySnippet snippet)
    {
        await LoadIfNeededAsync();
        var existing = _snippets!.FindIndex(s => s.Id == snippet.Id);
        if (existing >= 0)
        {
            snippet.ModifiedAt = DateTime.UtcNow;
            _snippets[existing] = snippet;
        }
        else
        {
            snippet.CreatedAt = DateTime.UtcNow;
            snippet.ModifiedAt = DateTime.UtcNow;
            _snippets.Insert(0, snippet);
        }
        await PersistAsync();
    }

    public async Task DeleteAsync(string id)
    {
        await LoadIfNeededAsync();
        _snippets!.RemoveAll(s => s.Id == id);
        await PersistAsync();
    }

    public Task<IReadOnlyList<QuerySnippet>> GetBuiltInSnippetsAsync(DatabaseType dbType)
    {
        var snippets = dbType switch
        {
            DatabaseType.PostgreSQL => GetPostgreSqlSnippets(),
            DatabaseType.MySQL or DatabaseType.MariaDB => GetMySqlSnippets(),
            DatabaseType.SQLite => GetSqliteSnippets(),
            _ => []
        };
        return Task.FromResult<IReadOnlyList<QuerySnippet>>(snippets);
    }

    private async Task LoadIfNeededAsync()
    {
        if (_snippets is not null) return;

        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _snippets = JsonSerializer.Deserialize<List<QuerySnippet>>(json, _jsonOptions) ?? [];
            }
            catch
            {
                _snippets = [];
            }
        }
        else
        {
            _snippets = [];
        }
    }

    private async Task PersistAsync()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_snippets, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    #region Built-in Snippets

    private static List<QuerySnippet> GetPostgreSqlSnippets() =>
    [
        new() { Name = "List all tables", Sql = "SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname = 'public' ORDER BY tablename;", Description = "List all tables in the public schema", Tags = "tables,schema", DatabaseType = DatabaseType.PostgreSQL },
        new() { Name = "Database size", Sql = "SELECT pg_database.datname, pg_size_pretty(pg_database_size(pg_database.datname)) AS size FROM pg_database ORDER BY pg_database_size(pg_database.datname) DESC;", Description = "Show size of all databases", Tags = "size,admin", DatabaseType = DatabaseType.PostgreSQL },
        new() { Name = "Active connections", Sql = "SELECT pid, usename, application_name, client_addr, state, query_start, query FROM pg_stat_activity WHERE state = 'active';", Description = "Show active connections and their queries", Tags = "connections,monitor", DatabaseType = DatabaseType.PostgreSQL },
        new() { Name = "Table sizes", Sql = "SELECT relname AS table_name, pg_size_pretty(pg_total_relation_size(relid)) AS total_size FROM pg_catalog.pg_statio_user_tables ORDER BY pg_total_relation_size(relid) DESC;", Description = "Show sizes of all user tables", Tags = "size,tables", DatabaseType = DatabaseType.PostgreSQL },
        new() { Name = "Running queries", Sql = "SELECT pid, now() - pg_stat_activity.query_start AS duration, query, state FROM pg_stat_activity WHERE (now() - pg_stat_activity.query_start) > interval '5 seconds' AND state != 'idle';", Description = "Show long-running queries (>5s)", Tags = "monitor,performance", DatabaseType = DatabaseType.PostgreSQL },
        new() { Name = "Index usage", Sql = "SELECT schemaname, relname, indexrelname, idx_scan, idx_tup_read, idx_tup_fetch FROM pg_stat_user_indexes ORDER BY idx_scan DESC;", Description = "Show index usage statistics", Tags = "index,performance", DatabaseType = DatabaseType.PostgreSQL },
        new() { Name = "Lock info", Sql = "SELECT l.pid, l.locktype, l.mode, l.granted, a.usename, a.query FROM pg_locks l JOIN pg_stat_activity a ON l.pid = a.pid WHERE NOT l.granted;", Description = "Show waiting locks", Tags = "locks,monitor", DatabaseType = DatabaseType.PostgreSQL },
    ];

    private static List<QuerySnippet> GetMySqlSnippets() =>
    [
        new() { Name = "Show tables", Sql = "SHOW TABLES;", Description = "List all tables in the current database", Tags = "tables,schema", DatabaseType = DatabaseType.MySQL },
        new() { Name = "Show processlist", Sql = "SHOW FULL PROCESSLIST;", Description = "Show active connections and queries", Tags = "connections,monitor", DatabaseType = DatabaseType.MySQL },
        new() { Name = "Table sizes", Sql = "SELECT table_name, ROUND(((data_length + index_length) / 1024 / 1024), 2) AS size_mb FROM information_schema.tables WHERE table_schema = DATABASE() ORDER BY (data_length + index_length) DESC;", Description = "Show table sizes in MB", Tags = "size,tables", DatabaseType = DatabaseType.MySQL },
        new() { Name = "Show variables", Sql = "SHOW VARIABLES LIKE '%max_connections%';", Description = "Show server variables", Tags = "config,admin", DatabaseType = DatabaseType.MySQL },
        new() { Name = "Show status", Sql = "SHOW GLOBAL STATUS LIKE 'Threads_%';", Description = "Show thread status", Tags = "monitor,status", DatabaseType = DatabaseType.MySQL },
        new() { Name = "User list", Sql = "SELECT user, host, account_locked FROM mysql.user;", Description = "List all MySQL users", Tags = "users,admin", DatabaseType = DatabaseType.MySQL },
    ];

    private static List<QuerySnippet> GetSqliteSnippets() =>
    [
        new() { Name = "List tables", Sql = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;", Description = "List all tables", Tags = "tables,schema", DatabaseType = DatabaseType.SQLite },
        new() { Name = "Table info", Sql = "PRAGMA table_info('table_name');", Description = "Show column info for a table (change table_name)", Tags = "columns,schema", DatabaseType = DatabaseType.SQLite },
        new() { Name = "Index list", Sql = "SELECT name, tbl_name FROM sqlite_master WHERE type='index' ORDER BY tbl_name, name;", Description = "List all indexes", Tags = "index,schema", DatabaseType = DatabaseType.SQLite },
        new() { Name = "Database size", Sql = "SELECT page_count * page_size as size FROM pragma_page_count(), pragma_page_size();", Description = "Show database file size in bytes", Tags = "size,admin", DatabaseType = DatabaseType.SQLite },
        new() { Name = "Integrity check", Sql = "PRAGMA integrity_check;", Description = "Check database integrity", Tags = "admin,maintenance", DatabaseType = DatabaseType.SQLite },
    ];

    #endregion
}

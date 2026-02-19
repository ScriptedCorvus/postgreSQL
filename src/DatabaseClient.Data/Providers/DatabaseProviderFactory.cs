using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Data.Services;

namespace DatabaseClient.Data.Providers;

/// <summary>
/// Factory that creates the appropriate database provider for a given engine type.
/// Injects the SSH tunnel manager into providers that support it.
/// </summary>
public class DatabaseProviderFactory : IDatabaseProviderFactory
{
    private readonly SshTunnelManager _sshTunnelManager;

    public DatabaseProviderFactory(SshTunnelManager sshTunnelManager)
    {
        _sshTunnelManager = sshTunnelManager;
    }

    public IDatabaseProvider Create(DatabaseType databaseType)
    {
        DatabaseProviderBase provider = databaseType switch
        {
            DatabaseType.PostgreSQL => new PostgreSqlProvider(),
            DatabaseType.MySQL => new MySqlProvider(DatabaseType.MySQL),
            DatabaseType.MariaDB => new MySqlProvider(DatabaseType.MariaDB),
            DatabaseType.SQLite => new SqliteProvider(),
            _ => throw new NotSupportedException($"Database type '{databaseType}' is not supported.")
        };

        provider.SetSshTunnelManager(_sshTunnelManager);
        return provider;
    }
}

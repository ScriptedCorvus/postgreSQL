using System.Text.Json.Serialization;

namespace DatabaseClient.Core.Models;

/// <summary>
/// Represents a saved database connection configuration.
/// </summary>
public class ConnectionInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; }

    // Connection
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Username { get; set; } = string.Empty;

    [JsonIgnore]
    public string Password { get; set; } = string.Empty;

    /// <summary>Encrypted password for persistence.</summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    public string DefaultDatabase { get; set; } = string.Empty;

    // SQLite specific
    public string DatabaseFilePath { get; set; } = string.Empty;

    // SSL
    public bool UseSsl { get; set; }
    public string SslMode { get; set; } = "Require";
    public string SslCaCertificatePath { get; set; } = string.Empty;
    public string SslClientCertificatePath { get; set; } = string.Empty;
    public string SslClientKeyPath { get; set; } = string.Empty;

    // Authentication
    public string AuthenticationMethod { get; set; } = "Default";

    // SSH Tunnel
    public bool UseSshTunnel { get; set; }
    public string SshHost { get; set; } = string.Empty;
    public int SshPort { get; set; } = 22;
    public string SshUsername { get; set; } = string.Empty;
    public string SshPassword { get; set; } = string.Empty;
    public string SshPrivateKeyPath { get; set; } = string.Empty;

    // Visual
    public string ColorTag { get; set; } = "#007ACC";
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Returns the default port for a given database type.
    /// </summary>
    public static int GetDefaultPort(DatabaseType dbType) => dbType switch
    {
        DatabaseType.PostgreSQL => 5432,
        DatabaseType.MySQL => 3306,
        DatabaseType.MariaDB => 3306,
        DatabaseType.SQLite => 0,
        _ => 0
    };
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Persists connection configurations to a JSON file with DPAPI-encrypted passwords.
/// </summary>
public class JsonConnectionRepository : IConnectionRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<ConnectionInfo> _connections = [];

    public JsonConnectionRepository(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseClient", "connections.json");
    }

    public async Task<IReadOnlyList<ConnectionInfo>> GetAllAsync()
    {
        await LoadAsync();
        return _connections.AsReadOnly();
    }

    public async Task<ConnectionInfo?> GetByIdAsync(Guid id)
    {
        await LoadAsync();
        return _connections.FirstOrDefault(c => c.Id == id);
    }

    public async Task SaveAsync(ConnectionInfo connection)
    {
        await LoadAsync();

        var existing = _connections.FindIndex(c => c.Id == connection.Id);
        EncryptPassword(connection);

        if (existing >= 0)
            _connections[existing] = connection;
        else
            _connections.Add(connection);

        await PersistAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await LoadAsync();
        _connections.RemoveAll(c => c.Id == id);
        await PersistAsync();
    }

    public async Task SaveAllAsync(IEnumerable<ConnectionInfo> connections)
    {
        _connections = connections.ToList();
        foreach (var conn in _connections)
            EncryptPassword(conn);
        await PersistAsync();
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            _connections = [];
            return;
        }

        var json = await File.ReadAllTextAsync(_filePath);
        _connections = JsonSerializer.Deserialize<List<ConnectionInfo>>(json, _jsonOptions) ?? [];

        foreach (var conn in _connections)
            DecryptPassword(conn);
    }

    private async Task PersistAsync()
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_connections, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private static void EncryptPassword(ConnectionInfo conn)
    {
        if (string.IsNullOrEmpty(conn.Password))
            return;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(conn.Password);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            conn.EncryptedPassword = Convert.ToBase64String(encrypted);
        }
        catch
        {
            // Fallback: store as-is (development / non-Windows)
            conn.EncryptedPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(conn.Password));
        }
    }

    private static void DecryptPassword(ConnectionInfo conn)
    {
        if (string.IsNullOrEmpty(conn.EncryptedPassword))
            return;

        try
        {
            var encrypted = Convert.FromBase64String(conn.EncryptedPassword);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            conn.Password = Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            // Fallback
            try
            {
                conn.Password = Encoding.UTF8.GetString(Convert.FromBase64String(conn.EncryptedPassword));
            }
            catch
            {
                conn.Password = string.Empty;
            }
        }
    }

    public async Task ExportConnectionsAsync(string filePath, IEnumerable<ConnectionInfo> connections)
    {
        // Export without passwords (strip encrypted data for safety)
        var exportList = connections.Select(c => new ConnectionInfo
        {
            Id = c.Id,
            Name = c.Name,
            DatabaseType = c.DatabaseType,
            Host = c.Host,
            Port = c.Port,
            Username = c.Username,
            Password = string.Empty,
            EncryptedPassword = string.Empty,
            DefaultDatabase = c.DefaultDatabase,
            DatabaseFilePath = c.DatabaseFilePath,
            UseSsl = c.UseSsl,
            SslMode = c.SslMode,
            SslCaCertificatePath = c.SslCaCertificatePath,
            SslClientCertificatePath = c.SslClientCertificatePath,
            SslClientKeyPath = c.SslClientKeyPath,
            AuthenticationMethod = c.AuthenticationMethod,
            UseSshTunnel = c.UseSshTunnel,
            SshHost = c.SshHost,
            SshPort = c.SshPort,
            SshUsername = c.SshUsername,
            SshPassword = string.Empty,
            SshPrivateKeyPath = c.SshPrivateKeyPath,
            ColorTag = c.ColorTag,
            Group = c.Group,
        }).ToList();

        var json = JsonSerializer.Serialize(exportList, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<IReadOnlyList<ConnectionInfo>> ImportConnectionsAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var imported = JsonSerializer.Deserialize<List<ConnectionInfo>>(json, _jsonOptions) ?? [];

        // Assign new Ids to avoid conflicts
        foreach (var conn in imported)
            conn.Id = Guid.NewGuid();

        // Merge with existing connections
        await LoadAsync();
        _connections.AddRange(imported);
        await PersistAsync();

        return imported.AsReadOnly();
    }
}

using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Handles export/import of profiles, connections, and snippets for collaboration.
/// Profile files (.dbcprofile) are ZIP archives containing JSON files.
/// Passwords in connections are encrypted with a user-provided master key using AES-256.
/// </summary>
public class ProfileSharingService : IProfileSharingService
{
    private readonly string _appDataDir;
    private readonly IConnectionRepository _connectionRepository;
    private readonly IQuerySnippetService _snippetService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ProfileSharingService(
        IConnectionRepository connectionRepository,
        IQuerySnippetService snippetService,
        string? appDataDir = null)
    {
        _connectionRepository = connectionRepository;
        _snippetService = snippetService;
        _appDataDir = appDataDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseClient");
    }

    public async Task ExportProfileAsync(string filePath, string masterKey)
    {
        using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);

        // Settings
        var settingsFile = Path.Combine(_appDataDir, "settings.json");
        if (File.Exists(settingsFile))
            zip.CreateEntryFromFile(settingsFile, "settings.json", CompressionLevel.Optimal);

        // Connections (encrypt passwords)
        var connections = await _connectionRepository.GetAllAsync();
        var exportConnections = connections.Select(c =>
        {
            var clone = CloneConnection(c);
            if (!string.IsNullOrEmpty(clone.Password))
            {
                clone.EncryptedPassword = EncryptAes(clone.Password, masterKey);
                clone.Password = string.Empty;
            }
            return clone;
        }).ToList();

        var connJson = JsonSerializer.Serialize(exportConnections, _jsonOptions);
        var connEntry = zip.CreateEntry("connections.json");
        using (var writer = new StreamWriter(connEntry.Open()))
            await writer.WriteAsync(connJson);

        // Snippets
        var snippets = await _snippetService.GetAllAsync();
        var snippetsJson = JsonSerializer.Serialize(snippets, _jsonOptions);
        var snippetsEntry = zip.CreateEntry("snippets.json");
        using (var writer = new StreamWriter(snippetsEntry.Open()))
            await writer.WriteAsync(snippetsJson);

        // Templates
        var templatesFile = Path.Combine(_appDataDir, "templates.json");
        if (File.Exists(templatesFile))
            zip.CreateEntryFromFile(templatesFile, "templates.json", CompressionLevel.Optimal);

        // Metadata
        var metadata = new { ExportedAt = DateTime.UtcNow, Version = "1.0" };
        var metaEntry = zip.CreateEntry("profile-info.json");
        using (var writer = new StreamWriter(metaEntry.Open()))
            await writer.WriteAsync(JsonSerializer.Serialize(metadata, _jsonOptions));
    }

    public async Task ImportProfileAsync(string filePath, string masterKey, bool replace)
    {
        using var zip = ZipFile.OpenRead(filePath);

        // Settings
        var settingsEntry = zip.GetEntry("settings.json");
        if (settingsEntry != null)
        {
            var targetPath = Path.Combine(_appDataDir, "settings.json");
            await ExtractEntry(settingsEntry, targetPath);
        }

        // Connections
        var connEntry = zip.GetEntry("connections.json");
        if (connEntry != null)
        {
            using var stream = connEntry.Open();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var connections = JsonSerializer.Deserialize<List<ConnectionInfo>>(json, _jsonOptions) ?? new();

            foreach (var conn in connections)
            {
                if (!string.IsNullOrEmpty(conn.EncryptedPassword))
                {
                    try
                    {
                        conn.Password = DecryptAes(conn.EncryptedPassword, masterKey);
                    }
                    catch
                    {
                        conn.Password = string.Empty; // Wrong key, clear password
                    }
                }
                await _connectionRepository.SaveAsync(conn);
            }
        }

        // Snippets
        var snippetsEntry = zip.GetEntry("snippets.json");
        if (snippetsEntry != null)
        {
            using var stream = snippetsEntry.Open();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var snippets = JsonSerializer.Deserialize<List<QuerySnippet>>(json, _jsonOptions) ?? new();

            foreach (var snippet in snippets)
                await _snippetService.SaveAsync(snippet);
        }

        // Templates
        var templatesEntry = zip.GetEntry("templates.json");
        if (templatesEntry != null)
        {
            var targetPath = Path.Combine(_appDataDir, "templates.json");
            await ExtractEntry(templatesEntry, targetPath);
        }
    }

    public async Task ExportConnectionsAsync(string filePath, IEnumerable<ConnectionInfo> connections, string masterKey)
    {
        var exportList = connections.Select(c =>
        {
            var clone = CloneConnection(c);
            if (!string.IsNullOrEmpty(clone.Password))
            {
                clone.EncryptedPassword = EncryptAes(clone.Password, masterKey);
                clone.Password = string.Empty;
            }
            return clone;
        }).ToList();

        var json = JsonSerializer.Serialize(exportList, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<IReadOnlyList<ConnectionInfo>> ImportConnectionsAsync(string filePath, string masterKey)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var connections = JsonSerializer.Deserialize<List<ConnectionInfo>>(json, _jsonOptions) ?? new();

        foreach (var conn in connections)
        {
            if (!string.IsNullOrEmpty(conn.EncryptedPassword))
            {
                try
                {
                    conn.Password = DecryptAes(conn.EncryptedPassword, masterKey);
                }
                catch
                {
                    conn.Password = string.Empty;
                }
            }
        }

        return connections.AsReadOnly();
    }

    public async Task ExportSnippetsAsync(string filePath)
    {
        var snippets = await _snippetService.GetAllAsync();
        var json = JsonSerializer.Serialize(snippets, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task ImportSnippetsAsync(string filePath, bool merge)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var snippets = JsonSerializer.Deserialize<List<QuerySnippet>>(json, _jsonOptions) ?? new();

        if (!merge)
        {
            // Delete existing snippets first
            var existing = await _snippetService.GetAllAsync();
            foreach (var s in existing)
                await _snippetService.DeleteAsync(s.Id);
        }

        foreach (var snippet in snippets)
            await _snippetService.SaveAsync(snippet);
    }

    // ── AES-256 Encryption Helpers ──────────────────────────────────────

    private static string EncryptAes(string plainText, string password)
    {
        var key = DeriveKey(password);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to cipher text
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    private static string DecryptAes(string cipherBase64, string password)
    {
        var key = DeriveKey(password);
        var fullCipher = Convert.FromBase64String(cipherBase64);

        using var aes = Aes.Create();
        aes.Key = key;

        // Extract IV from beginning
        var iv = new byte[aes.BlockSize / 8];
        var cipherBytes = new byte[fullCipher.Length - iv.Length];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, iv.Length, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] DeriveKey(string password)
    {
        // Use PBKDF2 with a fixed salt for key derivation
        var salt = "DatabaseClient_ProfileSharing_Salt"u8.ToArray();
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key
    }

    private static ConnectionInfo CloneConnection(ConnectionInfo source)
    {
        return new ConnectionInfo
        {
            Id = source.Id,
            Name = source.Name,
            DatabaseType = source.DatabaseType,
            Host = source.Host,
            Port = source.Port,
            Username = source.Username,
            Password = source.Password,
            DefaultDatabase = source.DefaultDatabase,
            DatabaseFilePath = source.DatabaseFilePath,
            UseSsl = source.UseSsl,
            SslCaCertificatePath = source.SslCaCertificatePath,
            SslClientCertificatePath = source.SslClientCertificatePath,
            SslClientKeyPath = source.SslClientKeyPath,
            UseSshTunnel = source.UseSshTunnel,
            SshHost = source.SshHost,
            SshPort = source.SshPort,
            SshUsername = source.SshUsername,
            SshPassword = source.SshPassword,
            SshPrivateKeyPath = source.SshPrivateKeyPath,
            ColorTag = source.ColorTag,
            Group = source.Group,
        };
    }

    private static async Task ExtractEntry(ZipArchiveEntry entry, string targetPath)
    {
        var dir = Path.GetDirectoryName(targetPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        await File.WriteAllTextAsync(targetPath, content);
    }
}

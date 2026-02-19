using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Manages backup and restore operations for application configuration.
/// Backups are stored as ZIP archives containing JSON config files.
/// </summary>
public class BackupService : IBackupService
{
    private readonly string _appDataDir;
    private readonly string _backupsDir;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public int MaxAutoBackups { get; set; } = 5;

    public BackupService(string? appDataDir = null)
    {
        _appDataDir = appDataDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseClient");
        _backupsDir = Path.Combine(_appDataDir, "backups");

        if (!Directory.Exists(_backupsDir))
            Directory.CreateDirectory(_backupsDir);
    }

    public async Task<BackupInfo> CreateBackupAsync(string name, BackupContents contents)
    {
        var backupInfo = new BackupInfo
        {
            Name = name,
            IsAutomatic = false,
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            Contents = contents,
            CreatedAt = DateTime.UtcNow,
        };

        await CreateBackupArchive(backupInfo, contents);
        return backupInfo;
    }

    public async Task<BackupInfo> CreateAutoBackupAsync()
    {
        var contents = new BackupContents
        {
            Settings = true,
            Connections = true,
            QueryHistory = true,
            Snippets = true,
            Templates = true,
        };

        var backupInfo = new BackupInfo
        {
            Name = $"Auto backup {DateTime.Now:yyyy-MM-dd HH:mm}",
            IsAutomatic = true,
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            Contents = contents,
            CreatedAt = DateTime.UtcNow,
        };

        await CreateBackupArchive(backupInfo, contents);

        // Prune old automatic backups
        await PruneAutoBackupsAsync();

        return backupInfo;
    }

    public async Task RestoreFromBackupAsync(string backupId, BackupContents contentsToRestore)
    {
        var backups = await GetBackupsAsync();
        var backup = backups.FirstOrDefault(b => b.Id == backupId);
        if (backup == null || !File.Exists(backup.FilePath))
            throw new FileNotFoundException($"Backup not found: {backupId}");

        using var zip = ZipFile.OpenRead(backup.FilePath);

        if (contentsToRestore.Settings)
            await RestoreEntry(zip, "settings.json", Path.Combine(_appDataDir, "settings.json"));

        if (contentsToRestore.Connections)
            await RestoreEntry(zip, "connections.json", Path.Combine(_appDataDir, "connections.json"));

        if (contentsToRestore.QueryHistory)
            await RestoreEntry(zip, "query-history.json", Path.Combine(_appDataDir, "query-history.json"));

        if (contentsToRestore.Snippets)
            await RestoreEntry(zip, "snippets.json", Path.Combine(_appDataDir, "snippets.json"));

        if (contentsToRestore.Templates)
            await RestoreEntry(zip, "templates.json", Path.Combine(_appDataDir, "templates.json"));
    }

    public async Task<IReadOnlyList<BackupInfo>> GetBackupsAsync()
    {
        var manifestPath = Path.Combine(_backupsDir, "backups-manifest.json");
        if (!File.Exists(manifestPath))
            return Array.Empty<BackupInfo>();

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            var list = JsonSerializer.Deserialize<List<BackupInfo>>(json, _jsonOptions) ?? new();

            // Remove entries for deleted files
            list.RemoveAll(b => !File.Exists(b.FilePath));

            return list.OrderByDescending(b => b.CreatedAt).ToList().AsReadOnly();
        }
        catch
        {
            return Array.Empty<BackupInfo>();
        }
    }

    public async Task<bool> DeleteBackupAsync(string backupId)
    {
        var backups = (await GetBackupsAsync()).ToList();
        var backup = backups.FirstOrDefault(b => b.Id == backupId);
        if (backup == null) return false;

        if (File.Exists(backup.FilePath))
            File.Delete(backup.FilePath);

        backups.Remove(backup);
        await SaveManifestAsync(backups);
        return true;
    }

    public async Task ResetToFactoryDefaultsAsync()
    {
        // Create auto backup before reset
        await CreateAutoBackupAsync();

        // Delete config files (not backups)
        var filesToDelete = new[]
        {
            Path.Combine(_appDataDir, "settings.json"),
            Path.Combine(_appDataDir, "connections.json"),
            Path.Combine(_appDataDir, "query-history.json"),
            Path.Combine(_appDataDir, "snippets.json"),
            Path.Combine(_appDataDir, "templates.json"),
        };

        foreach (var file in filesToDelete)
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    private async Task CreateBackupArchive(BackupInfo backupInfo, BackupContents contents)
    {
        var timestamp = backupInfo.CreatedAt.ToString("yyyyMMdd_HHmmss");
        var safeName = string.Join("_", backupInfo.Name.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"backup_{timestamp}_{safeName}.zip";
        var filePath = Path.Combine(_backupsDir, fileName);

        using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
        {
            if (contents.Settings)
                AddFileToZip(zip, Path.Combine(_appDataDir, "settings.json"), "settings.json");

            if (contents.Connections)
                AddFileToZip(zip, Path.Combine(_appDataDir, "connections.json"), "connections.json");

            if (contents.QueryHistory)
                AddFileToZip(zip, Path.Combine(_appDataDir, "query-history.json"), "query-history.json");

            if (contents.Snippets)
                AddFileToZip(zip, Path.Combine(_appDataDir, "snippets.json"), "snippets.json");

            if (contents.Templates)
                AddFileToZip(zip, Path.Combine(_appDataDir, "templates.json"), "templates.json");

            // Add manifest entry inside the zip
            var manifestJson = JsonSerializer.Serialize(backupInfo, _jsonOptions);
            var manifestEntry = zip.CreateEntry("backup-info.json");
            using var writer = new StreamWriter(manifestEntry.Open());
            await writer.WriteAsync(manifestJson);
        }

        backupInfo.FilePath = filePath;
        backupInfo.FileSizeBytes = new FileInfo(filePath).Length;

        // Update global manifest
        var backups = (await GetBackupsAsync()).ToList();
        backups.Add(backupInfo);
        await SaveManifestAsync(backups);
    }

    private static void AddFileToZip(ZipArchive zip, string sourceFile, string entryName)
    {
        if (File.Exists(sourceFile))
            zip.CreateEntryFromFile(sourceFile, entryName, CompressionLevel.Optimal);
    }

    private static async Task RestoreEntry(ZipArchive zip, string entryName, string targetPath)
    {
        var entry = zip.GetEntry(entryName);
        if (entry == null) return;

        var dir = Path.GetDirectoryName(targetPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        await File.WriteAllTextAsync(targetPath, content);
    }

    private async Task PruneAutoBackupsAsync()
    {
        var backups = (await GetBackupsAsync()).ToList();
        var autoBackups = backups.Where(b => b.IsAutomatic).OrderByDescending(b => b.CreatedAt).ToList();

        if (autoBackups.Count <= MaxAutoBackups) return;

        var toDelete = autoBackups.Skip(MaxAutoBackups).ToList();
        foreach (var backup in toDelete)
        {
            if (File.Exists(backup.FilePath))
                File.Delete(backup.FilePath);
            backups.Remove(backup);
        }

        await SaveManifestAsync(backups);
    }

    private async Task SaveManifestAsync(IEnumerable<BackupInfo> backups)
    {
        var manifestPath = Path.Combine(_backupsDir, "backups-manifest.json");
        var json = JsonSerializer.Serialize(backups.ToList(), _jsonOptions);
        await File.WriteAllTextAsync(manifestPath, json);
    }
}

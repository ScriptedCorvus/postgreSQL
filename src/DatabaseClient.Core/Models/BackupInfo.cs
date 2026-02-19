namespace DatabaseClient.Core.Models;

/// <summary>
/// Represents a backup of application configuration and data.
/// </summary>
public class BackupInfo
{
    /// <summary>Unique backup identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display name of the backup.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Date and time the backup was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether this was an automatic backup.</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>Application version at time of backup.</summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>What was included in the backup.</summary>
    public BackupContents Contents { get; set; } = new();

    /// <summary>File path of the backup archive.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Size of the backup file in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Human-readable file size.</summary>
    public string FileSize => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB",
    };
}

/// <summary>
/// Specifies what content is included in a backup.
/// </summary>
public class BackupContents
{
    public bool Settings { get; set; } = true;
    public bool Connections { get; set; } = true;
    public bool QueryHistory { get; set; } = true;
    public bool Snippets { get; set; } = true;
    public bool Templates { get; set; } = true;
}

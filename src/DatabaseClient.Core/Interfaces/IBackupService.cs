using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Manages backup and restore operations for application configuration.
/// </summary>
public interface IBackupService
{
    /// <summary>Creates a manual backup with the specified contents.</summary>
    Task<BackupInfo> CreateBackupAsync(string name, BackupContents contents);

    /// <summary>Creates an automatic backup (e.g., before app update).</summary>
    Task<BackupInfo> CreateAutoBackupAsync();

    /// <summary>Restores from a backup file.</summary>
    Task RestoreFromBackupAsync(string backupId, BackupContents contentsToRestore);

    /// <summary>Gets all available backups sorted by date descending.</summary>
    Task<IReadOnlyList<BackupInfo>> GetBackupsAsync();

    /// <summary>Deletes a specific backup.</summary>
    Task<bool> DeleteBackupAsync(string backupId);

    /// <summary>Resets all configuration to factory defaults.</summary>
    Task ResetToFactoryDefaultsAsync();

    /// <summary>Gets the maximum number of automatic backups to retain.</summary>
    int MaxAutoBackups { get; set; }
}

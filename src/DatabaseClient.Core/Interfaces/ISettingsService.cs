using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Manages application settings persistence.
/// </summary>
public interface ISettingsService
{
    /// <summary>Gets the current settings.</summary>
    AppSettings Settings { get; }

    /// <summary>Loads settings from storage.</summary>
    Task LoadAsync();

    /// <summary>Saves current settings to storage.</summary>
    Task SaveAsync();

    /// <summary>Resets all settings to defaults.</summary>
    void ResetToDefaults();

    /// <summary>Raised when settings change.</summary>
    event EventHandler? SettingsChanged;
}

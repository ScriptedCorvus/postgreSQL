using System.Text.Json;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Persists application settings to a JSON file.
/// </summary>
public class JsonSettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AppSettings Settings { get; private set; } = new();

    public event EventHandler? SettingsChanged;

    public JsonSettingsService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseClient", "settings.json");
    }

    /// <summary>Current settings file format version.</summary>
    private const int CurrentVersion = 2;

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            Settings = new AppSettings { Version = CurrentVersion };
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            MigrateIfNeeded();
        }
        catch
        {
            // If settings file is corrupt, reset to defaults
            Settings = new AppSettings { Version = CurrentVersion };
        }
    }

    /// <summary>
    /// Migrates settings from older versions to the current version.
    /// </summary>
    private void MigrateIfNeeded()
    {
        if (Settings.Version >= CurrentVersion) return;

        // v1 → v2: Added syntax colors, notification settings
        if (Settings.Version < 2)
        {
            // Ensure new defaults are applied for properties that didn't exist
            Settings.SyntaxKeywordColor ??= "#0000FF";
            Settings.SyntaxStringColor ??= "#A31515";
            Settings.SyntaxCommentColor ??= "#008000";
            Settings.SyntaxNumberColor ??= "#098658";
            Settings.SyntaxFunctionColor ??= "#795E26";
        }

        Settings.Version = CurrentVersion;
        // Auto-save migrated settings
        _ = SaveAsync();
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(Settings, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetToDefaults()
    {
        Settings = new AppSettings();
    }
}

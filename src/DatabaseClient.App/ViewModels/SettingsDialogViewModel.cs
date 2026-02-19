using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the Settings dialog with category navigation.
/// Edits a copy of settings and applies on confirm.
/// </summary>
public partial class SettingsDialogViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    // ── Categories ─────────────────────────────────────────────────────────

    public string[] Categories { get; } =
    [
        "General",
        "Appearance",
        "Editor",
        "Data Grid",
        "Query Execution",
        "Query History",
        "Keyboard Shortcuts",
        "Notifications",
        "Backup & Restore"
    ];

    [ObservableProperty]
    private string _selectedCategory = "General";

    [ObservableProperty]
    private string _settingsSearchText = string.Empty;

    /// <summary>Filtered categories based on search text.</summary>
    public IEnumerable<string> FilteredCategories =>
        string.IsNullOrWhiteSpace(SettingsSearchText)
            ? Categories
            : Categories.Where(c => c.Contains(SettingsSearchText, StringComparison.OrdinalIgnoreCase)
                || GetCategoryKeywords(c).Any(k => k.Contains(SettingsSearchText, StringComparison.OrdinalIgnoreCase)));

    partial void OnSettingsSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredCategories));
        // Auto-select the first matching category
        var filtered = FilteredCategories.ToList();
        if (filtered.Count > 0 && !filtered.Contains(SelectedCategory))
            SelectedCategory = filtered[0];
    }

    private static IEnumerable<string> GetCategoryKeywords(string category) => category switch
    {
        "General" => ["language", "close", "session", "updates"],
        "Appearance" => ["theme", "dark", "light", "accent", "color"],
        "Editor" => ["font", "line numbers", "word wrap", "syntax", "highlight"],
        "Data Grid" => ["null", "rows", "alternating", "grid"],
        "Query Execution" => ["timeout", "max rows", "auto commit", "transaction"],
        "Query History" => ["history", "entries", "save"],
        "Keyboard Shortcuts" => ["shortcut", "keys", "binding", "hotkey"],
        "Notifications" => ["toast", "alert", "sound", "status bar", "popup"],
        "Backup & Restore" => ["backup", "restore", "export", "import"],
        _ => []
    };

    // ── General ────────────────────────────────────────────────────────────

    public string[] Languages { get; } = ["en", "es"];

    [ObservableProperty]
    private string _language = "en";

    [ObservableProperty]
    private bool _confirmCloseUnsaved = true;

    [ObservableProperty]
    private bool _restoreLastSession;

    [ObservableProperty]
    private bool _checkForUpdates = true;

    [ObservableProperty]
    private string _sharedProjectDirectory = string.Empty;

    // ── Appearance ─────────────────────────────────────────────────────────

    public ThemeMode[] ThemeModes { get; } = [ThemeMode.Light, ThemeMode.Dark, ThemeMode.System];

    [ObservableProperty]
    private ThemeMode _selectedTheme = ThemeMode.Light;

    [ObservableProperty]
    private string _accentColor = "#1976D2";

    public string[] AccentColorPresets { get; } =
    [
        "#1976D2", // Blue
        "#388E3C", // Green
        "#D32F2F", // Red
        "#7B1FA2", // Purple
        "#F57C00", // Orange
        "#00838F", // Teal
        "#C2185B", // Pink
        "#455A64"  // Blue Grey
    ];

    // ── Editor ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _editorFontFamily = "Cascadia Code, Consolas, Courier New";

    [ObservableProperty]
    private int _editorFontSize = 14;

    [ObservableProperty]
    private bool _showLineNumbers = true;

    [ObservableProperty]
    private bool _wordWrap;

    // Editor syntax colors
    [ObservableProperty]
    private string _syntaxKeywordColor = "#0000FF";

    [ObservableProperty]
    private string _syntaxStringColor = "#A31515";

    [ObservableProperty]
    private string _syntaxCommentColor = "#008000";

    [ObservableProperty]
    private string _syntaxNumberColor = "#098658";

    [ObservableProperty]
    private string _syntaxFunctionColor = "#795E26";

    // ── Data Grid ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _nullDisplayText = "NULL";

    [ObservableProperty]
    private bool _alternatingRowColors = true;

    [ObservableProperty]
    private int _gridFontSize = 12;

    // ── Query Execution ────────────────────────────────────────────────────

    [ObservableProperty]
    private int _queryTimeoutSeconds = 60;

    [ObservableProperty]
    private int _maxRowsReturned = 10000;

    [ObservableProperty]
    private bool _autoCommit = true;

    // ── Query History ──────────────────────────────────────────────────────

    [ObservableProperty]
    private int _maxHistoryEntries = 500;

    [ObservableProperty]
    private bool _saveQueryHistory = true;

    // ── Keyboard Shortcuts ─────────────────────────────────────────────────

    private IKeyboardShortcutService? _shortcutService;

    public ObservableCollection<ShortcutEditItem> ShortcutItems { get; } = [];

    [ObservableProperty]
    private string _shortcutConflictMessage = string.Empty;

    // ── Notifications ───────────────────────────────────────────────────

    [ObservableProperty]
    private bool _showStatusBarNotifications = true;

    [ObservableProperty]
    private bool _showToastNotifications = true;

    [ObservableProperty]
    private bool _notifyQueryCompletion = true;

    [ObservableProperty]
    private bool _playSoundOnCompletion;

    [ObservableProperty]
    private int _toastDurationSeconds = 5;

    // ── Backup & Restore ──────────────────────────────────────────────────

    private IBackupService? _backupService;

    public ObservableCollection<BackupInfo> Backups { get; } = [];

    [ObservableProperty]
    private string _backupName = string.Empty;

    [ObservableProperty]
    private bool _backupSettings = true;

    [ObservableProperty]
    private bool _backupConnections = true;

    [ObservableProperty]
    private bool _backupHistory = true;

    [ObservableProperty]
    private bool _backupSnippets = true;

    [ObservableProperty]
    private bool _backupTemplates = true;

    [ObservableProperty]
    private string _backupStatusMessage = string.Empty;

    [ObservableProperty]
    private BackupInfo? _selectedBackup;

    // ── Dialog result ──────────────────────────────────────────────────────

    public bool IsConfirmed { get; private set; }

    public SettingsDialogViewModel(ISettingsService settingsService, 
        IKeyboardShortcutService? shortcutService = null,
        IBackupService? backupService = null)
    {
        _settingsService = settingsService;
        _shortcutService = shortcutService;
        _backupService = backupService;
        LoadFromSettings(settingsService.Settings);
        LoadShortcuts();
        _ = LoadBackupsAsync();
    }

    private void LoadFromSettings(AppSettings s)
    {
        // General
        Language = s.Language;
        ConfirmCloseUnsaved = s.ConfirmCloseUnsaved;
        RestoreLastSession = s.RestoreLastSession;
        CheckForUpdates = s.CheckForUpdates;
        SharedProjectDirectory = s.SharedProjectDirectory;

        // Appearance
        SelectedTheme = s.Theme;
        AccentColor = s.AccentColor;

        // Editor
        EditorFontFamily = s.EditorFontFamily;
        EditorFontSize = s.EditorFontSize;
        ShowLineNumbers = s.ShowLineNumbers;
        WordWrap = s.WordWrap;

        // Syntax Colors
        SyntaxKeywordColor = s.SyntaxKeywordColor;
        SyntaxStringColor = s.SyntaxStringColor;
        SyntaxCommentColor = s.SyntaxCommentColor;
        SyntaxNumberColor = s.SyntaxNumberColor;
        SyntaxFunctionColor = s.SyntaxFunctionColor;

        // Data Grid
        NullDisplayText = s.NullDisplayText;
        AlternatingRowColors = s.AlternatingRowColors;
        GridFontSize = s.GridFontSize;

        // Query Execution
        QueryTimeoutSeconds = s.QueryTimeoutSeconds;
        MaxRowsReturned = s.MaxRowsReturned;
        AutoCommit = s.AutoCommit;

        // Query History
        MaxHistoryEntries = s.MaxHistoryEntries;
        SaveQueryHistory = s.SaveQueryHistory;

        // Notifications
        ShowStatusBarNotifications = s.ShowStatusBarNotifications;
        ShowToastNotifications = s.ShowToastNotifications;
        NotifyQueryCompletion = s.NotifyQueryCompletion;
        PlaySoundOnCompletion = s.PlaySoundOnCompletion;
        ToastDurationSeconds = s.ToastDurationSeconds;
    }

    private void ApplyToSettings(AppSettings s)
    {
        // General
        s.Language = Language;
        s.ConfirmCloseUnsaved = ConfirmCloseUnsaved;
        s.RestoreLastSession = RestoreLastSession;
        s.CheckForUpdates = CheckForUpdates;
        s.SharedProjectDirectory = SharedProjectDirectory;

        // Appearance
        s.Theme = SelectedTheme;
        s.AccentColor = AccentColor;

        // Editor
        s.EditorFontFamily = EditorFontFamily;
        s.EditorFontSize = EditorFontSize;
        s.ShowLineNumbers = ShowLineNumbers;
        s.WordWrap = WordWrap;

        // Syntax Colors
        s.SyntaxKeywordColor = SyntaxKeywordColor;
        s.SyntaxStringColor = SyntaxStringColor;
        s.SyntaxCommentColor = SyntaxCommentColor;
        s.SyntaxNumberColor = SyntaxNumberColor;
        s.SyntaxFunctionColor = SyntaxFunctionColor;

        // Data Grid
        s.NullDisplayText = NullDisplayText;
        s.AlternatingRowColors = AlternatingRowColors;
        s.GridFontSize = GridFontSize;

        // Query Execution
        s.QueryTimeoutSeconds = QueryTimeoutSeconds;
        s.MaxRowsReturned = MaxRowsReturned;
        s.AutoCommit = AutoCommit;

        // Query History
        s.MaxHistoryEntries = MaxHistoryEntries;
        s.SaveQueryHistory = SaveQueryHistory;

        // Notifications
        s.ShowStatusBarNotifications = ShowStatusBarNotifications;
        s.ShowToastNotifications = ShowToastNotifications;
        s.NotifyQueryCompletion = NotifyQueryCompletion;
        s.PlaySoundOnCompletion = PlaySoundOnCompletion;
        s.ToastDurationSeconds = ToastDurationSeconds;
    }

    [RelayCommand]
    private async Task Confirm()
    {
        ApplyToSettings(_settingsService.Settings);
        await _settingsService.SaveAsync();

        // Apply language change at runtime
        Services.LocalizationManager.Instance.SetLanguage(Language);

        // Save shortcut customizations
        if (_shortcutService is not null)
        {
            foreach (var item in ShortcutItems)
                _shortcutService.SetShortcut(item.ActionId, item.CurrentShortcut);
            await _shortcutService.SaveAsync();
        }

        IsConfirmed = true;
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        _settingsService.ResetToDefaults();
        LoadFromSettings(_settingsService.Settings);
    }

    [RelayCommand]
    private void SelectAccentColor(string color)
    {
        AccentColor = color;
    }

    /// <summary>Delegate to open a folder browser dialog. Set from the View code-behind.</summary>
    public Func<string?>? BrowseFolderDialog { get; set; }

    [RelayCommand]
    private void BrowseSharedDirectory()
    {
        var path = BrowseFolderDialog?.Invoke();
        if (!string.IsNullOrEmpty(path))
            SharedProjectDirectory = path;
    }

    [RelayCommand]
    private void ClearSharedDirectory()
    {
        SharedProjectDirectory = string.Empty;
    }

    // ── Shortcut Helpers ────────────────────────────────────────────────────

    private void LoadShortcuts()
    {
        if (_shortcutService is null) return;

        ShortcutItems.Clear();
        foreach (var action in _shortcutService.GetAllActions())
        {
            ShortcutItems.Add(new ShortcutEditItem
            {
                ActionId = action.ActionId,
                DisplayName = action.DisplayName,
                Category = action.Category,
                DefaultShortcut = action.DefaultShortcut,
                CurrentShortcut = action.CurrentShortcut
            });
        }
    }

    [RelayCommand]
    private void ResetShortcut(ShortcutEditItem? item)
    {
        if (item is null) return;
        item.CurrentShortcut = item.DefaultShortcut;
    }

    [RelayCommand]
    private void ResetAllShortcuts()
    {
        foreach (var item in ShortcutItems)
            item.CurrentShortcut = item.DefaultShortcut;
    }

    public void CheckShortcutConflict(ShortcutEditItem item)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentShortcut))
        {
            ShortcutConflictMessage = string.Empty;
            return;
        }

        var conflicts = ShortcutItems
            .Where(s => s.ActionId != item.ActionId
                && string.Equals(s.CurrentShortcut, item.CurrentShortcut, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ShortcutConflictMessage = conflicts.Count > 0
            ? $"Conflict: '{item.CurrentShortcut}' is also used by '{conflicts[0].DisplayName}'"
            : string.Empty;
    }

    // ── Backup & Restore Helpers ──────────────────────────────────────────

    private async Task LoadBackupsAsync()
    {
        if (_backupService is null) return;

        Backups.Clear();
        var backups = await _backupService.GetBackupsAsync();
        foreach (var b in backups)
            Backups.Add(b);
    }

    [RelayCommand]
    private async Task CreateBackup()
    {
        if (_backupService is null) return;

        var name = string.IsNullOrWhiteSpace(BackupName)
            ? $"Manual backup {DateTime.Now:yyyy-MM-dd HH:mm}"
            : BackupName;

        var contents = new BackupContents
        {
            Settings = BackupSettings,
            Connections = BackupConnections,
            QueryHistory = BackupHistory,
            Snippets = BackupSnippets,
            Templates = BackupTemplates,
        };

        try
        {
            var backup = await _backupService.CreateBackupAsync(name, contents);
            BackupStatusMessage = $"Backup created: {backup.FileSize}";
            BackupName = string.Empty;
            await LoadBackupsAsync();
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Backup failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RestoreBackup()
    {
        if (_backupService is null || SelectedBackup is null) return;

        var contents = new BackupContents
        {
            Settings = BackupSettings,
            Connections = BackupConnections,
            QueryHistory = BackupHistory,
            Snippets = BackupSnippets,
            Templates = BackupTemplates,
        };

        try
        {
            await _backupService.RestoreFromBackupAsync(SelectedBackup.Id, contents);
            BackupStatusMessage = "Restore completed. Restart the application to apply changes.";
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Restore failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteBackup()
    {
        if (_backupService is null || SelectedBackup is null) return;

        await _backupService.DeleteBackupAsync(SelectedBackup.Id);
        SelectedBackup = null;
        BackupStatusMessage = "Backup deleted.";
        await LoadBackupsAsync();
    }

    [RelayCommand]
    private async Task ResetToFactory()
    {
        if (_backupService is null) return;

        try
        {
            await _backupService.ResetToFactoryDefaultsAsync();
            BackupStatusMessage = "Reset to factory defaults. Restart the application to apply.";
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Reset failed: {ex.Message}";
        }
    }
}

/// <summary>
/// Editable item for keyboard shortcut customization in the Settings dialog.
/// </summary>
public partial class ShortcutEditItem : ObservableObject
{
    public string ActionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DefaultShortcut { get; set; } = string.Empty;

    [ObservableProperty]
    private string _currentShortcut = string.Empty;

    public bool IsCustomized => CurrentShortcut != DefaultShortcut;
}

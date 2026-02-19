namespace DatabaseClient.Core.Models;

/// <summary>
/// Application-wide settings persisted as JSON.
/// </summary>
public class AppSettings
{
    /// <summary>Settings file format version for migration support.</summary>
    public int Version { get; set; } = 1;

    // ── Appearance ─────────────────────────────────────────────────────────

    /// <summary>Theme mode: Light, Dark, or System.</summary>
    public ThemeMode Theme { get; set; } = ThemeMode.Light;

    /// <summary>Primary accent color hex (e.g. "#1976D2").</summary>
    public string AccentColor { get; set; } = "#1976D2";

    /// <summary>Editor font family.</summary>
    public string EditorFontFamily { get; set; } = "Cascadia Code, Consolas, Courier New";

    /// <summary>Editor font size.</summary>
    public int EditorFontSize { get; set; } = 14;

    /// <summary>Show line numbers in the SQL editor.</summary>
    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>Enable word wrap in the SQL editor.</summary>
    public bool WordWrap { get; set; }

    // ── Syntax Colors ──────────────────────────────────────────────────────

    /// <summary>Color for SQL keywords.</summary>
    public string SyntaxKeywordColor { get; set; } = "#0000FF";

    /// <summary>Color for string literals.</summary>
    public string SyntaxStringColor { get; set; } = "#A31515";

    /// <summary>Color for comments.</summary>
    public string SyntaxCommentColor { get; set; } = "#008000";

    /// <summary>Color for numeric literals.</summary>
    public string SyntaxNumberColor { get; set; } = "#098658";

    /// <summary>Color for function names.</summary>
    public string SyntaxFunctionColor { get; set; } = "#795E26";

    // ── Query Execution ────────────────────────────────────────────────────

    /// <summary>Default query timeout in seconds (0 = no limit).</summary>
    public int QueryTimeoutSeconds { get; set; } = 60;

    /// <summary>Maximum number of rows returned by default.</summary>
    public int MaxRowsReturned { get; set; } = 10000;

    /// <summary>Auto-commit mode (execute each statement in its own transaction).</summary>
    public bool AutoCommit { get; set; } = true;

    // ── Data Grid ──────────────────────────────────────────────────────────

    /// <summary>Display text for NULL values.</summary>
    public string NullDisplayText { get; set; } = "NULL";

    /// <summary>Show alternating row colors in results grid.</summary>
    public bool AlternatingRowColors { get; set; } = true;

    /// <summary>Results grid font size.</summary>
    public int GridFontSize { get; set; } = 12;

    // ── Query History ──────────────────────────────────────────────────────

    /// <summary>Maximum number of history entries to keep.</summary>
    public int MaxHistoryEntries { get; set; } = 500;

    /// <summary>Whether to save query history.</summary>
    public bool SaveQueryHistory { get; set; } = true;

    // ── General ────────────────────────────────────────────────────────────

    /// <summary>Application language (es, en).</summary>
    public string Language { get; set; } = "en";

    /// <summary>Confirm before closing tabs with unsaved changes.</summary>
    public bool ConfirmCloseUnsaved { get; set; } = true;

    /// <summary>Restore tabs from last session on startup.</summary>
    public bool RestoreLastSession { get; set; }

    /// <summary>Check for updates on startup.</summary>
    public bool CheckForUpdates { get; set; } = true;

    // ── Keyboard Shortcuts ─────────────────────────────────────────────────

    /// <summary>Custom keyboard shortcut overrides. Key = ActionId, Value = shortcut string (e.g. "Ctrl+N").</summary>
    public Dictionary<string, string> KeyboardShortcuts { get; set; } = [];

    // ── Collaboration ──────────────────────────────────────────────────────

    /// <summary>Path to a shared project directory for team collaboration (connections, snippets, templates).</summary>
    public string SharedProjectDirectory { get; set; } = string.Empty;

    // ── Notifications ──────────────────────────────────────────────────────

    /// <summary>Show status bar notifications.</summary>
    public bool ShowStatusBarNotifications { get; set; } = true;

    /// <summary>Show toast/popup notifications.</summary>
    public bool ShowToastNotifications { get; set; } = true;

    /// <summary>Show query completion notifications when app is not focused.</summary>
    public bool NotifyQueryCompletion { get; set; } = true;

    /// <summary>Play sound on query completion.</summary>
    public bool PlaySoundOnCompletion { get; set; }

    /// <summary>Duration in seconds for toast notifications (0 = manual dismiss).</summary>
    public int ToastDurationSeconds { get; set; } = 5;
}

/// <summary>
/// Theme mode options.
/// </summary>
public enum ThemeMode
{
    Light,
    Dark,
    System
}

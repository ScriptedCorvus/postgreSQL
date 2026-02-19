using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Manages keyboard shortcut definitions, customization, and persistence via ISettingsService.
/// </summary>
public class KeyboardShortcutService : IKeyboardShortcutService
{
    private readonly ISettingsService _settingsService;
    private readonly List<KeyboardShortcutAction> _actions;

    public event EventHandler? ShortcutsChanged;

    public KeyboardShortcutService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _actions = BuildDefaultActions();
        LoadCustomizations();
    }

    public IReadOnlyList<KeyboardShortcutAction> GetAllActions() => _actions.AsReadOnly();

    public string GetShortcut(string actionId)
    {
        var action = _actions.Find(a => a.ActionId == actionId);
        return action?.CurrentShortcut ?? string.Empty;
    }

    public void SetShortcut(string actionId, string shortcut)
    {
        var action = _actions.Find(a => a.ActionId == actionId);
        if (action is null) return;

        action.CurrentShortcut = shortcut;
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetToDefault(string actionId)
    {
        var action = _actions.Find(a => a.ActionId == actionId);
        if (action is null) return;

        action.CurrentShortcut = action.DefaultShortcut;
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetAllToDefaults()
    {
        foreach (var action in _actions)
            action.CurrentShortcut = action.DefaultShortcut;
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<KeyboardShortcutAction> FindConflicts(string actionId, string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return [];

        return _actions
            .Where(a => a.ActionId != actionId
                && string.Equals(a.CurrentShortcut, shortcut, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    public async Task SaveAsync()
    {
        var customized = new Dictionary<string, string>();
        foreach (var action in _actions)
        {
            if (action.IsCustomized)
                customized[action.ActionId] = action.CurrentShortcut;
        }
        _settingsService.Settings.KeyboardShortcuts = customized;
        await _settingsService.SaveAsync();
    }

    private void LoadCustomizations()
    {
        var overrides = _settingsService.Settings.KeyboardShortcuts;
        if (overrides is null) return;

        foreach (var kvp in overrides)
        {
            var action = _actions.Find(a => a.ActionId == kvp.Key);
            if (action is not null)
                action.CurrentShortcut = kvp.Value;
        }
    }

    private static List<KeyboardShortcutAction> BuildDefaultActions()
    {
        return
        [
            // General
            new() { ActionId = "General.NewConnection", DisplayName = "New Connection", Category = "General", DefaultShortcut = "Ctrl+Shift+N", CurrentShortcut = "Ctrl+Shift+N" },
            new() { ActionId = "General.NewQuery", DisplayName = "New Query Tab", Category = "General", DefaultShortcut = "Ctrl+N", CurrentShortcut = "Ctrl+N" },
            new() { ActionId = "General.CloseTab", DisplayName = "Close Tab", Category = "General", DefaultShortcut = "Ctrl+W", CurrentShortcut = "Ctrl+W" },
            new() { ActionId = "General.Settings", DisplayName = "Open Settings", Category = "General", DefaultShortcut = "Ctrl+,", CurrentShortcut = "Ctrl+," },
            new() { ActionId = "General.Snippets", DisplayName = "Open Snippets", Category = "General", DefaultShortcut = "Ctrl+Shift+S", CurrentShortcut = "Ctrl+Shift+S" },
            new() { ActionId = "General.QueryHistory", DisplayName = "Open Query History", Category = "General", DefaultShortcut = "Ctrl+H", CurrentShortcut = "Ctrl+H" },

            // Editor
            new() { ActionId = "Editor.Execute", DisplayName = "Execute Query", Category = "Editor", DefaultShortcut = "F5", CurrentShortcut = "F5" },
            new() { ActionId = "Editor.ExecuteSelected", DisplayName = "Execute Selected", Category = "Editor", DefaultShortcut = "Ctrl+Shift+E", CurrentShortcut = "Ctrl+Shift+E" },
            new() { ActionId = "Editor.Explain", DisplayName = "Explain Query Plan", Category = "Editor", DefaultShortcut = "Ctrl+Shift+X", CurrentShortcut = "Ctrl+Shift+X" },
            new() { ActionId = "Editor.Autocomplete", DisplayName = "Trigger Autocomplete", Category = "Editor", DefaultShortcut = "Ctrl+Space", CurrentShortcut = "Ctrl+Space" },
            new() { ActionId = "Editor.Comment", DisplayName = "Toggle Comment", Category = "Editor", DefaultShortcut = "Ctrl+/", CurrentShortcut = "Ctrl+/" },
            new() { ActionId = "Editor.Cancel", DisplayName = "Cancel Execution", Category = "Editor", DefaultShortcut = "Ctrl+Break", CurrentShortcut = "Ctrl+Break" },

            // Navigation
            new() { ActionId = "Nav.NextTab", DisplayName = "Next Tab", Category = "Navigation", DefaultShortcut = "Ctrl+Tab", CurrentShortcut = "Ctrl+Tab" },
            new() { ActionId = "Nav.PrevTab", DisplayName = "Previous Tab", Category = "Navigation", DefaultShortcut = "Ctrl+Shift+Tab", CurrentShortcut = "Ctrl+Shift+Tab" },
            new() { ActionId = "Nav.FocusEditor", DisplayName = "Focus Editor", Category = "Navigation", DefaultShortcut = "Ctrl+E", CurrentShortcut = "Ctrl+E" },
            new() { ActionId = "Nav.FocusConnections", DisplayName = "Focus Connections", Category = "Navigation", DefaultShortcut = "Ctrl+Shift+C", CurrentShortcut = "Ctrl+Shift+C" },

            // Data
            new() { ActionId = "Data.RefreshData", DisplayName = "Refresh Data", Category = "Data", DefaultShortcut = "F5", CurrentShortcut = "F5" },
            new() { ActionId = "Data.ApplyChanges", DisplayName = "Apply Changes", Category = "Data", DefaultShortcut = "Ctrl+S", CurrentShortcut = "Ctrl+S" },
            new() { ActionId = "Data.InsertRow", DisplayName = "Insert Row", Category = "Data", DefaultShortcut = "Ctrl+Insert", CurrentShortcut = "Ctrl+Insert" },
            new() { ActionId = "Data.DeleteRow", DisplayName = "Delete Row", Category = "Data", DefaultShortcut = "Ctrl+Delete", CurrentShortcut = "Ctrl+Delete" },

            // Export
            new() { ActionId = "Export.Csv", DisplayName = "Export to CSV", Category = "Export", DefaultShortcut = "", CurrentShortcut = "" },
            new() { ActionId = "Export.Excel", DisplayName = "Export to Excel", Category = "Export", DefaultShortcut = "", CurrentShortcut = "" },
            new() { ActionId = "Export.Json", DisplayName = "Export to JSON", Category = "Export", DefaultShortcut = "", CurrentShortcut = "" },
        ];
    }
}

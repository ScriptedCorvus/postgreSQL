namespace DatabaseClient.Core.Models;

/// <summary>
/// Defines a keyboard shortcut action mapping.
/// </summary>
public class KeyboardShortcutAction
{
    /// <summary>Unique identifier for the action (e.g. "General.NewQuery").</summary>
    public string ActionId { get; set; } = string.Empty;

    /// <summary>Display name of the action.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Category grouping (General, Editor, Navigation, Data).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Default keyboard shortcut string (e.g. "Ctrl+N").</summary>
    public string DefaultShortcut { get; set; } = string.Empty;

    /// <summary>Current (possibly customized) shortcut.</summary>
    public string CurrentShortcut { get; set; } = string.Empty;

    /// <summary>Whether the current shortcut differs from the default.</summary>
    public bool IsCustomized => CurrentShortcut != DefaultShortcut;
}

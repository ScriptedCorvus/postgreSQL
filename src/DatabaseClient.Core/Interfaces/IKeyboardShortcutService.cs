using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Manages keyboard shortcut definitions and customization.
/// </summary>
public interface IKeyboardShortcutService
{
    /// <summary>Gets all registered shortcut actions.</summary>
    IReadOnlyList<KeyboardShortcutAction> GetAllActions();

    /// <summary>Gets the current shortcut for an action.</summary>
    string GetShortcut(string actionId);

    /// <summary>Sets a custom shortcut for an action.</summary>
    void SetShortcut(string actionId, string shortcut);

    /// <summary>Resets a single action's shortcut to its default.</summary>
    void ResetToDefault(string actionId);

    /// <summary>Resets all shortcuts to defaults.</summary>
    void ResetAllToDefaults();

    /// <summary>Finds conflicts: other actions using the same shortcut.</summary>
    IReadOnlyList<KeyboardShortcutAction> FindConflicts(string actionId, string shortcut);

    /// <summary>Save all custom shortcuts to settings.</summary>
    Task SaveAsync();

    /// <summary>Raised when any shortcut changes.</summary>
    event EventHandler? ShortcutsChanged;
}

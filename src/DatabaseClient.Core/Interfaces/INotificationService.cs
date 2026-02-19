namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Application notification service for toast/snackbar messages.
/// </summary>
public interface INotificationService
{
    /// <summary>Shows an informational notification.</summary>
    void ShowInfo(string message);

    /// <summary>Shows a success notification.</summary>
    void ShowSuccess(string message);

    /// <summary>Shows a warning notification.</summary>
    void ShowWarning(string message);

    /// <summary>Shows an error notification.</summary>
    void ShowError(string message);
}

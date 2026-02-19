using System.Windows;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using MaterialDesignThemes.Wpf;

namespace DatabaseClient.App.Services;

/// <summary>
/// Notification service backed by MaterialDesign Snackbar.
/// Requires a SnackbarMessageQueue to be set after MainWindow loads.
/// Respects notification settings from AppSettings.
/// </summary>
public class SnackbarNotificationService : INotificationService
{
    private SnackbarMessageQueue? _messageQueue;
    private readonly ISettingsService _settingsService;

    public SnackbarNotificationService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Sets the Snackbar MessageQueue from the MainWindow.
    /// </summary>
    public void SetMessageQueue(SnackbarMessageQueue messageQueue)
    {
        _messageQueue = messageQueue;
    }

    public void ShowInfo(string message)
    {
        Enqueue(message);
    }

    public void ShowSuccess(string message)
    {
        Enqueue($"✓ {message}");
    }

    public void ShowWarning(string message)
    {
        Enqueue($"⚠ {message}");
    }

    public void ShowError(string message)
    {
        Enqueue($"✗ {message}");
    }

    private AppSettings Settings => _settingsService.Settings;

    private void Enqueue(string message)
    {
        if (_messageQueue is null) return;
        if (!Settings.ShowToastNotifications) return;

        var duration = Settings.ToastDurationSeconds > 0
            ? TimeSpan.FromSeconds(Settings.ToastDurationSeconds)
            : TimeSpan.FromSeconds(3);

        Application.Current.Dispatcher.Invoke(() =>
        {
            _messageQueue.Enqueue(message, null, null, null, false, true, duration);
        });
    }
}

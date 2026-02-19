using System.Windows;
using System.Windows.Threading;
using DatabaseClient.Core.Services;
using Serilog;

namespace DatabaseClient.App.Services;

/// <summary>
/// Handles all unhandled exceptions globally:
/// - DispatcherUnhandledException (WPF UI thread)
/// - TaskScheduler.UnobservedTaskException (async tasks)
/// - AppDomain.UnhandledException (all remaining)
/// Logs the error and shows a user-friendly dialog.
/// </summary>
public sealed class GlobalExceptionHandler
{
    private static readonly ILogger Logger = Log.ForContext<GlobalExceptionHandler>();

    /// <summary>
    /// Hooks into all global exception sources.
    /// Call once during App.OnStartup.
    /// </summary>
    public void Register(Application application)
    {
        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
    }

    /// <summary>
    /// Unhooks all handlers. Call during App.OnExit.
    /// </summary>
    public void Unregister(Application application)
    {
        application.DispatcherUnhandledException -= OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Unhandled UI exception");
        ShowErrorDialog(e.Exception);
        e.Handled = true; // Prevent app crash
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Unobserved task exception");
        e.SetObserved(); // Prevent process termination

        // Marshal to UI thread for dialog
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            ShowErrorDialog(e.Exception.InnerException ?? e.Exception);
        });
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Fatal(ex, "Fatal unhandled exception (IsTerminating={IsTerminating})", e.IsTerminating);

            try
            {
                ShowErrorDialog(ex, isFatal: true);
            }
            catch
            {
                // Last resort — can't show dialog
            }
        }
    }

    /// <summary>
    /// Shows an error dialog with a user-friendly translation and expandable technical details.
    /// Can also be called directly from catch blocks for expected errors.
    /// </summary>
    public static void ShowErrorDialog(Exception ex, bool isFatal = false)
    {
        var friendlyMessage = DatabaseErrorTranslator.Translate(ex);
        var technicalDetails = FormatTechnicalDetails(ex);

        var title = isFatal ? "Fatal Error" : "Error";

        Application.Current?.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.ErrorDialog(title, friendlyMessage, technicalDetails, isFatal);
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        });
    }

    private static string FormatTechnicalDetails(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        var current = ex;
        int depth = 0;

        while (current != null)
        {
            if (depth > 0)
                sb.AppendLine($"\n--- Inner Exception ({depth}) ---");

            sb.AppendLine($"Type: {current.GetType().FullName}");
            sb.AppendLine($"Message: {current.Message}");

            // Provider-specific fields
            var props = current.GetType().GetProperties()
                .Where(p => p.Name is "SqlState" or "ErrorCode" or "Number" or "Code");
            foreach (var prop in props)
            {
                try
                {
                    var val = prop.GetValue(current);
                    if (val != null)
                        sb.AppendLine($"{prop.Name}: {val}");
                }
                catch { /* ignore reflection errors */ }
            }

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(current.StackTrace);
            }

            current = current.InnerException;
            depth++;
        }

        return sb.ToString();
    }
}

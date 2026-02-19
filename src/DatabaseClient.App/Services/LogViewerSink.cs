using Serilog.Core;
using Serilog.Events;

namespace DatabaseClient.App.Services;

/// <summary>
/// Custom Serilog sink that routes log events to the LogViewerTabViewModel.
/// Can be attached after the ViewModel is created.
/// </summary>
public class LogViewerSink : ILogEventSink
{
    private Action<LogEvent>? _handler;

    /// <summary>
    /// Registers a handler for incoming log events. 
    /// This is called when the LogViewer tab is opened.
    /// </summary>
    public void SetHandler(Action<LogEvent> handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Clears the handler (e.g., when LogViewer tab is closed).
    /// </summary>
    public void ClearHandler()
    {
        _handler = null;
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            _handler?.Invoke(logEvent);
        }
        catch
        {
            // Don't let sink errors propagate
        }
    }

    /// <summary>
    /// Singleton instance for use across the application.
    /// </summary>
    public static LogViewerSink Instance { get; } = new();
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.ViewModels;
using Serilog.Events;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the application log viewer tab.
/// Displays Serilog log events in real-time with filtering by level.
/// </summary>
public partial class LogViewerTabViewModel : TabViewModelBase
{
    public override string TabIconKind => "TextBoxSearch";

    private readonly ObservableCollection<LogEntryViewModel> _allEntries = [];

    public ObservableCollection<LogEntryViewModel> FilteredEntries { get; } = [];

    [ObservableProperty]
    private LogEventLevel _minimumLevel = LogEventLevel.Information;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _filteredCount;

    [ObservableProperty]
    private bool _showVerbose;

    [ObservableProperty]
    private bool _showDebug;

    [ObservableProperty]
    private bool _showInformation = true;

    [ObservableProperty]
    private bool _showWarning = true;

    [ObservableProperty]
    private bool _showError = true;

    [ObservableProperty]
    private bool _showFatal = true;

    public LogViewerTabViewModel()
    {
        Title = "Log Viewer";
    }

    /// <summary>
    /// Adds a log event to the viewer. Called from the custom Serilog sink.
    /// </summary>
    public void AddLogEvent(LogEvent logEvent)
    {
        var entry = new LogEntryViewModel
        {
            Timestamp = logEvent.Timestamp.LocalDateTime,
            Level = logEvent.Level,
            Message = logEvent.RenderMessage(),
            Exception = logEvent.Exception?.ToString(),
            Source = logEvent.Properties.TryGetValue("SourceContext", out var ctx)
                ? ctx.ToString().Trim('"')
                : string.Empty
        };

        _allEntries.Add(entry);
        TotalCount = _allEntries.Count;

        if (MatchesFilter(entry))
        {
            FilteredEntries.Add(entry);
            FilteredCount = FilteredEntries.Count;
        }

        // Trim to prevent unbounded memory growth
        if (_allEntries.Count > 10000)
        {
            _allEntries.RemoveAt(0);
            ApplyFilter();
        }
    }

    partial void OnShowVerboseChanged(bool value) => ApplyFilter();
    partial void OnShowDebugChanged(bool value) => ApplyFilter();
    partial void OnShowInformationChanged(bool value) => ApplyFilter();
    partial void OnShowWarningChanged(bool value) => ApplyFilter();
    partial void OnShowErrorChanged(bool value) => ApplyFilter();
    partial void OnShowFatalChanged(bool value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void ClearLogs()
    {
        _allEntries.Clear();
        FilteredEntries.Clear();
        TotalCount = 0;
        FilteredCount = 0;
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        var text = string.Join(Environment.NewLine,
            FilteredEntries.Select(e =>
                $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{e.Level}] {e.Source} {e.Message}" +
                (e.Exception != null ? $"\n{e.Exception}" : "")));

        if (!string.IsNullOrEmpty(text))
            System.Windows.Clipboard.SetText(text);
    }

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        foreach (var entry in _allEntries)
        {
            if (MatchesFilter(entry))
                FilteredEntries.Add(entry);
        }
        FilteredCount = FilteredEntries.Count;
    }

    private bool MatchesFilter(LogEntryViewModel entry)
    {
        // Level filter
        var levelMatch = entry.Level switch
        {
            LogEventLevel.Verbose => ShowVerbose,
            LogEventLevel.Debug => ShowDebug,
            LogEventLevel.Information => ShowInformation,
            LogEventLevel.Warning => ShowWarning,
            LogEventLevel.Error => ShowError,
            LogEventLevel.Fatal => ShowFatal,
            _ => true
        };
        if (!levelMatch) return false;

        // Text search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            return entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || entry.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || (entry.Exception?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return true;
    }
}

/// <summary>
/// Represents a single log entry for display.
/// </summary>
public class LogEntryViewModel
{
    public DateTime Timestamp { get; set; }
    public LogEventLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string Source { get; set; } = string.Empty;

    public string LevelShort => Level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        LogEventLevel.Fatal => "FTL",
        _ => "???"
    };
}

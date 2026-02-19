using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// Main ViewModel for the application shell. Manages the tab collection,
/// the connection tree, and global commands.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IConnectionManager _connectionManager;
    private readonly IQueryHistoryService _queryHistoryService;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IQuerySnippetService _querySnippetService;

    private readonly IKeyboardShortcutService? _keyboardShortcutService;
    private readonly IConnectionTemplateService? _connectionTemplateService;
    private readonly IBackupService? _backupService;

    /// <summary>Delegate for showing the connection dialog.</summary>
    public Func<ConnectionDialogViewModel, bool>? ShowConnectionDialog { get; set; }

    /// <summary>Delegate for showing the settings dialog.</summary>
    public Func<SettingsDialogViewModel, bool>? ShowSettingsDialog { get; set; }

    /// <summary>Delegate for showing the import data dialog for a specific database.</summary>
    public Action<ConnectionInfo, string>? RequestImportData { get; set; }

    /// <summary>Delegate for showing the export data dialog for a specific database/table.</summary>
    public Action<ConnectionInfo, string, string?>? RequestExportData { get; set; }

    /// <summary>Delegate for showing the profile sharing dialog.</summary>
    public Action? RequestOpenProfileSharing { get; set; }

    /// <summary>Delegate for showing the data comparison dialog.</summary>
    public Action? RequestOpenDataComparison { get; set; }

    /// <summary>Delegate for showing the schema comparison dialog.</summary>
    public Action? RequestOpenSchemaComparison { get; set; }

    /// <summary>Delegate for showing the migration wizard dialog.</summary>
    public Action? RequestOpenMigrationWizard { get; set; }

    /// <summary>Delegate for showing the user management dialog.</summary>
    public Action<ConnectionInfo, string>? RequestOpenUserManagement { get; set; }

    /// <summary>Delegate for showing the task scheduler dialog.</summary>
    public Action? RequestOpenScheduler { get; set; }

    /// <summary>Delegate to browse a file for import connections.</summary>
    public Func<string?>? BrowseImportConnectionsFile { get; set; }

    /// <summary>Delegate to browse a file for export connections.</summary>
    public Func<string?>? BrowseExportConnectionsFile { get; set; }

    public ObservableCollection<TabViewModelBase> Tabs { get; } = [];
    public ObservableCollection<ConnectionNodeViewModel> Connections { get; } = [];

    /// <summary>Filtered view of connections for the tree. Returns all when filter is empty.</summary>
    public IEnumerable<ConnectionNodeViewModel> FilteredConnections
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ConnectionFilter))
                return Connections;

            return Connections.Where(c =>
                c.Name.Contains(ConnectionFilter, StringComparison.OrdinalIgnoreCase) ||
                c.TypeLabel.Contains(ConnectionFilter, StringComparison.OrdinalIgnoreCase));
        }
    }

    [ObservableProperty]
    private TabViewModelBase? _selectedTab;

    [ObservableProperty]
    private string _applicationTitle = "Database Client";

    [ObservableProperty]
    private string _connectionFilter = string.Empty;

    [ObservableProperty]
    private int _activeConnectionCount;

    [ObservableProperty]
    private bool _hasNoTabs = true;

    [ObservableProperty]
    private string _memoryUsage = "-";

    private System.Timers.Timer? _memoryTimer;

    public MainViewModel(IConnectionRepository connectionRepository, IConnectionManager connectionManager,
        IQueryHistoryService queryHistoryService, ISettingsService settingsService,
        INotificationService notificationService, IQuerySnippetService querySnippetService,
        IKeyboardShortcutService? keyboardShortcutService = null,
        IConnectionTemplateService? connectionTemplateService = null,
        IBackupService? backupService = null)
    {
        _connectionRepository = connectionRepository;
        _connectionManager = connectionManager;
        _queryHistoryService = queryHistoryService;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _querySnippetService = querySnippetService;
        _keyboardShortcutService = keyboardShortcutService;
        _connectionTemplateService = connectionTemplateService;
        _backupService = backupService;

        Tabs.CollectionChanged += (_, _) => HasNoTabs = Tabs.Count == 0;

        // Start memory monitor timer (every 5 seconds)
        _memoryTimer = new System.Timers.Timer(5000);
        _memoryTimer.Elapsed += (_, _) => UpdateMemoryUsage();
        _memoryTimer.Start();
        UpdateMemoryUsage();
    }

    private void UpdateMemoryUsage()
    {
        var bytes = GC.GetTotalMemory(false);
        var mb = bytes / 1024.0 / 1024.0;
        MemoryUsage = $"{mb:F1} MB";
    }

    partial void OnConnectionFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredConnections));
    }

    /// <summary>Loads saved connections on startup.</summary>
    public async Task InitializeAsync()
    {
        StatusMessage = "Loading connections...";
        var connections = await _connectionRepository.GetAllAsync();
        Connections.Clear();
        foreach (var conn in connections)
        {
            Connections.Add(new ConnectionNodeViewModel(conn, _connectionManager, this, _connectionRepository, _queryHistoryService));
        }

        // Load connection templates
        if (_connectionTemplateService != null)
            await _connectionTemplateService.LoadAsync();

        ActiveConnectionCount = 0;
        StatusMessage = $"Loaded {connections.Count} connection(s). Ready.";
    }

    /// <summary>Updates the active connection counter.</summary>
    public void RefreshActiveConnectionCount()
    {
        ActiveConnectionCount = _connectionManager.GetActiveConnectionIds().Count;
    }

    [RelayCommand]
    private void NewQueryTab()
    {
        var tab = new QueryTabViewModel(_connectionManager, _queryHistoryService);
        WireQueryTabDelegates(tab);
        AddTab(tab);
    }

    [RelayCommand]
    private async Task NewConnection()
    {
        var dialogVm = new ConnectionDialogViewModel(_connectionManager, _connectionTemplateService);

        if (ShowConnectionDialog?.Invoke(dialogVm) == true && dialogVm.ResultConnection is { } conn)
        {
            await _connectionRepository.SaveAsync(conn);
            Connections.Add(new ConnectionNodeViewModel(conn, _connectionManager, this, _connectionRepository, _queryHistoryService));
            StatusMessage = $"Connection '{conn.Name}' created.";
            _notificationService.ShowSuccess($"Connection '{conn.Name}' created.");
            OnPropertyChanged(nameof(FilteredConnections));
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var dialogVm = new SettingsDialogViewModel(_settingsService, _keyboardShortcutService, _backupService);
        if (ShowSettingsDialog?.Invoke(dialogVm) == true)
        {
            _notificationService.ShowSuccess("Settings saved.");
        }
    }

    [RelayCommand]
    private void OpenProfileSharing()
    {
        RequestOpenProfileSharing?.Invoke();
    }

    [RelayCommand]
    private void OpenDataComparison()
    {
        RequestOpenDataComparison?.Invoke();
    }

    [RelayCommand]
    private void OpenSchemaComparison()
    {
        RequestOpenSchemaComparison?.Invoke();
    }

    [RelayCommand]
    private void OpenMigrationWizard()
    {
        RequestOpenMigrationWizard?.Invoke();
    }

    [RelayCommand]
    private void OpenScheduler()
    {
        RequestOpenScheduler?.Invoke();
    }

    [RelayCommand]
    private void OpenLogViewer()
    {
        // Check if log viewer tab already open
        var existing = Tabs.OfType<LogViewerTabViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var logTab = new LogViewerTabViewModel();
        // Wire the Serilog sink to dispatch events to the UI thread
        Services.LogViewerSink.Instance.SetHandler(evt =>
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                logTab.AddLogEvent(evt);
            });
        });
        AddTab(logTab);
    }

    [RelayCommand]
    private void ForceGarbageCollection()
    {
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);
        UpdateMemoryUsage();
        StatusMessage = "Garbage collection completed.";
    }

    [RelayCommand]
    private void OpenDiagnostics()
    {
        // Check if diagnostics tab already open
        var existing = Tabs.OfType<DiagnosticsTabViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var tab = new DiagnosticsTabViewModel(_connectionManager);
        AddTab(tab);
        _ = tab.RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ExportConnections()
    {
        var filePath = BrowseExportConnectionsFile?.Invoke();
        if (string.IsNullOrEmpty(filePath)) return;

        try
        {
            var allConnections = await _connectionRepository.GetAllAsync();
            await _connectionRepository.ExportConnectionsAsync(filePath, allConnections);
            _notificationService.ShowSuccess($"Exported {allConnections.Count} connections.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"Export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportConnections()
    {
        var filePath = BrowseImportConnectionsFile?.Invoke();
        if (string.IsNullOrEmpty(filePath)) return;

        try
        {
            var imported = await _connectionRepository.ImportConnectionsAsync(filePath);
            // Reload all connections
            var allConnections = await _connectionRepository.GetAllAsync();
            Connections.Clear();
            foreach (var conn in allConnections)
                Connections.Add(new ConnectionNodeViewModel(conn, _connectionManager, this, _connectionRepository, _queryHistoryService));
            _notificationService.ShowSuccess($"Imported {imported.Count} connections.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"Import failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenQueryHistory()
    {
        // Check if history tab already open
        var existing = Tabs.OfType<QueryHistoryTabViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var historyTab = new QueryHistoryTabViewModel(_queryHistoryService);
        historyTab.OpenInNewTab = sql =>
        {
            var tab = new QueryTabViewModel(_connectionManager, _queryHistoryService) { SqlText = sql };
            WireQueryTabDelegates(tab);
            AddTab(tab);
        };
        AddTab(historyTab);
    }

    [RelayCommand]
    private void OpenSnippets()
    {
        var existing = Tabs.OfType<SnippetsTabViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var snippetsTab = new SnippetsTabViewModel(_querySnippetService);
        snippetsTab.InsertIntoEditor = sql =>
        {
            // Insert into active query tab if available
            var activeQuery = Tabs.OfType<QueryTabViewModel>().FirstOrDefault(t => t == SelectedTab)
                ?? Tabs.OfType<QueryTabViewModel>().FirstOrDefault();
            if (activeQuery is not null)
            {
                activeQuery.SqlText = sql;
                SelectedTab = activeQuery;
            }
            else
            {
                var tab = new QueryTabViewModel(_connectionManager, _queryHistoryService) { SqlText = sql };
                WireQueryTabDelegates(tab);
                AddTab(tab);
            }
        };
        AddTab(snippetsTab);
        _ = snippetsTab.LoadAsync();
    }

    /// <summary>Opens a chart tab from query result data.</summary>
    public void OpenChartFromData(System.Data.DataTable data, string title)
    {
        var chartTab = new ChartTabViewModel();
        chartTab.LoadData(data, title);
        AddTab(chartTab);
    }

    /// <summary>Wires the OpenChartDelegate on a query tab so it can open charts.</summary>
    public void WireQueryTabDelegates(QueryTabViewModel tab)
    {
        tab.OpenChartDelegate = OpenChartFromData;
    }

    public void AddTab(TabViewModelBase tab)
    {
        tab.CloseRequested += OnTabCloseRequested;
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    private async void OnTabCloseRequested(object? sender, EventArgs e)
    {
        if (sender is TabViewModelBase tab)
        {
            if (await tab.CanCloseAsync())
            {
                tab.CloseRequested -= OnTabCloseRequested;
                Tabs.Remove(tab);
                if (SelectedTab == tab)
                    SelectedTab = Tabs.LastOrDefault();
            }
        }
    }

    [RelayCommand]
    private void CloseTab(TabViewModelBase? tab)
    {
        tab?.RequestCloseCommand.Execute(null);
    }

    [RelayCommand]
    private void CloseOtherTabs(TabViewModelBase? keepTab)
    {
        foreach (var tab in Tabs.Where(t => t != keepTab).ToList())
            tab.RequestCloseCommand.Execute(null);
    }

    [RelayCommand]
    private void CloseAllTabs()
    {
        foreach (var tab in Tabs.ToList())
            tab.RequestCloseCommand.Execute(null);
    }

    /// <summary>Removes a connection from the tree and repository.</summary>
    public async Task RemoveConnectionAsync(ConnectionNodeViewModel connectionNode)
    {
        await _connectionRepository.DeleteAsync(connectionNode.ConnectionInfo.Id);
        Connections.Remove(connectionNode);
        _notificationService.ShowInfo($"Connection '{connectionNode.Name}' removed.");
        OnPropertyChanged(nameof(FilteredConnections));
    }

    /// <summary>Duplicates a connection.</summary>
    public async Task DuplicateConnectionAsync(ConnectionNodeViewModel connectionNode)
    {
        var original = connectionNode.ConnectionInfo;
        var duplicate = new ConnectionInfo
        {
            Name = $"{original.Name} (Copy)",
            DatabaseType = original.DatabaseType,
            Host = original.Host,
            Port = original.Port,
            Username = original.Username,
            Password = original.Password,
            DefaultDatabase = original.DefaultDatabase,
            DatabaseFilePath = original.DatabaseFilePath,
            UseSsl = original.UseSsl,
            ColorTag = original.ColorTag,
            Group = original.Group,
        };
        await _connectionRepository.SaveAsync(duplicate);
        Connections.Add(new ConnectionNodeViewModel(duplicate, _connectionManager, this, _connectionRepository, _queryHistoryService));
        _notificationService.ShowSuccess($"Connection '{duplicate.Name}' created.");
        OnPropertyChanged(nameof(FilteredConnections));
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for a node in the connection tree. Supports lazy loading of child nodes.
/// </summary>
public partial class ConnectionNodeViewModel : ViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private readonly IConnectionRepository _connectionRepository;
    private readonly MainViewModel _mainViewModel;
    private readonly IQueryHistoryService? _queryHistoryService;
    private IDatabaseProvider? _provider;
    private bool _childrenLoaded;

    public ConnectionInfo ConnectionInfo { get; }
    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isConnected;

    public string Name => ConnectionInfo.Name;
    public string TypeLabel => ConnectionInfo.DatabaseType.ToString();

    /// <summary>Icon for the database type (MaterialDesign PackIcon kind).</summary>
    public string DatabaseTypeIcon => ConnectionInfo.DatabaseType switch
    {
        DatabaseType.PostgreSQL => "Elephant",
        DatabaseType.MySQL => "Dolphin",
        DatabaseType.MariaDB => "Dolphin",
        DatabaseType.SQLite => "FileDocument",
        _ => "Database"
    };

    public ConnectionNodeViewModel(ConnectionInfo connectionInfo, IConnectionManager connectionManager,
        MainViewModel mainViewModel, IConnectionRepository connectionRepository,
        IQueryHistoryService? queryHistoryService = null)
    {
        ConnectionInfo = connectionInfo;
        _connectionManager = connectionManager;
        _mainViewModel = mainViewModel;
        _connectionRepository = connectionRepository;
        _queryHistoryService = queryHistoryService;

        // Placeholder for lazy loading
        Children.Add(new TreeNodeViewModel("Loading...", DatabaseObjectType.Database));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_childrenLoaded)
        {
            _ = LoadChildrenAsync();
        }
    }

    [RelayCommand]
    private async Task Connect()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Connecting...";
            _provider = await _connectionManager.GetProviderAsync(ConnectionInfo);
            IsConnected = true;
            StatusMessage = "Connected";
            _mainViewModel.RefreshActiveConnectionCount();
            await LoadChildrenAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsConnected = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        await _connectionManager.CloseConnectionAsync(ConnectionInfo.Id);
        IsConnected = false;
        _provider = null;
        _childrenLoaded = false;
        Children.Clear();
        Children.Add(new TreeNodeViewModel("Loading...", DatabaseObjectType.Database));
        _mainViewModel.RefreshActiveConnectionCount();
    }

    [RelayCommand]
    private void OpenNewQuery()
    {
        var tab = new QueryTabViewModel(_connectionManager, _queryHistoryService)
        {
            Title = $"Query - {ConnectionInfo.Name}",
        };
        tab.SetConnection(ConnectionInfo);
        _mainViewModel.WireQueryTabDelegates(tab);
        _mainViewModel.AddTab(tab);
    }

    [RelayCommand]
    private void EditConnection()
    {
        // TODO: Open connection dialog for editing
        _mainViewModel.StatusMessage = $"Edit connection: {ConnectionInfo.Name}";
    }

    [RelayCommand]
    private async Task DuplicateConnection()
    {
        await _mainViewModel.DuplicateConnectionAsync(this);
    }

    [RelayCommand]
    private async Task DeleteConnection()
    {
        var result = MessageBox.Show(
            $"Are you sure you want to delete connection '{ConnectionInfo.Name}'?",
            "Delete Connection",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            if (IsConnected)
                await Disconnect();
            await _mainViewModel.RemoveConnectionAsync(this);
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        _childrenLoaded = false;
        await LoadChildrenAsync();
    }

    private async Task LoadChildrenAsync()
    {
        if (_provider is null || !_provider.IsConnected)
        {
            await Connect();
            if (_provider is null) return;
        }

        try
        {
            IsBusy = true;
            Children.Clear();

            var databases = await _provider.GetDatabasesAsync();
            foreach (var db in databases)
            {
                var dbNode = new DatabaseNodeViewModel(db, ConnectionInfo, _connectionManager, _mainViewModel, _queryHistoryService);
                Children.Add(dbNode);
            }

            _childrenLoaded = true;
        }
        catch (Exception ex)
        {
            Children.Clear();
            Children.Add(new TreeNodeViewModel($"Error: {ex.Message}", DatabaseObjectType.Database));
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>
/// ViewModel for a database node in the tree.
/// </summary>
public partial class DatabaseNodeViewModel : TreeNodeViewModel
{
    private readonly ConnectionInfo _connectionInfo;
    private readonly IConnectionManager _connectionManager;
    private readonly MainViewModel _mainViewModel;
    private readonly IQueryHistoryService? _queryHistoryService;
    private bool _childrenLoaded;

    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    public DatabaseNodeViewModel(string name, ConnectionInfo connectionInfo,
        IConnectionManager connectionManager, MainViewModel mainViewModel,
        IQueryHistoryService? queryHistoryService = null)
        : base(name, DatabaseObjectType.Database)
    {
        _connectionInfo = connectionInfo;
        _connectionManager = connectionManager;
        _mainViewModel = mainViewModel;
        _queryHistoryService = queryHistoryService;

        // Placeholder
        Children.Add(new TreeNodeViewModel("Loading...", DatabaseObjectType.TableFolder));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_childrenLoaded)
        {
            _ = LoadChildrenAsync();
        }
    }

    [RelayCommand]
    private void OpenNewQuery()
    {
        var tab = new QueryTabViewModel(_connectionManager, _queryHistoryService)
        {
            Title = $"Query - {Name}",
        };
        tab.SetConnection(_connectionInfo);
        tab.SelectedDatabase = Name;
        _mainViewModel.WireQueryTabDelegates(tab);
        _mainViewModel.AddTab(tab);
    }

    [RelayCommand]
    private void NewTable()
    {
        var tab = new TableDesignerTabViewModel(_connectionManager);
        tab.SetConnection(_connectionInfo, Name);
        _mainViewModel.AddTab(tab);
    }

    [RelayCommand]
    private void OpenErDiagram()
    {
        var tab = new DiagramTabViewModel(_connectionManager);
        tab.SetConnection(_connectionInfo, Name);
        _mainViewModel.AddTab(tab);
        _ = tab.ReverseEngineerCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void OpenMonitor()
    {
        // Check if monitor for this connection already exists
        var existing = _mainViewModel.Tabs.OfType<MonitorTabViewModel>()
            .FirstOrDefault(m => m.Title == $"Monitor: {_connectionInfo.Name}");
        if (existing is not null)
        {
            _mainViewModel.SelectedTab = existing;
            return;
        }

        var tab = new MonitorTabViewModel(_connectionManager, _connectionInfo);
        _mainViewModel.AddTab(tab);
        _ = tab.RefreshAllCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ImportData()
    {
        _mainViewModel.RequestImportData?.Invoke(_connectionInfo, Name);
    }

    [RelayCommand]
    private void ExportData()
    {
        _mainViewModel.RequestExportData?.Invoke(_connectionInfo, Name, null);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        _childrenLoaded = false;
        await LoadChildrenAsync();
    }

    private async Task LoadChildrenAsync()
    {
        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            await provider.ChangeDatabaseAsync(Name);

            Children.Clear();

            // Tables folder
            var tablesFolder = new FolderNodeViewModel("Tables", DatabaseObjectType.TableFolder);
            var tables = await provider.GetTablesAsync(Name);
            foreach (var t in tables)
                tablesFolder.Children.Add(new TableNodeViewModel(t.Name, _connectionInfo, Name, _connectionManager, _mainViewModel, _queryHistoryService));
            Children.Add(tablesFolder);

            // Views folder
            var viewsFolder = new FolderNodeViewModel("Views", DatabaseObjectType.ViewFolder);
            var views = await provider.GetViewsAsync(Name);
            foreach (var v in views)
                viewsFolder.Children.Add(new TreeNodeViewModel(v.Name, DatabaseObjectType.View));
            Children.Add(viewsFolder);

            // Functions folder
            var functionsFolder = new FolderNodeViewModel("Functions", DatabaseObjectType.FunctionFolder);
            var functions = await provider.GetFunctionsAsync(Name);
            foreach (var f in functions)
                functionsFolder.Children.Add(new TreeNodeViewModel(f.Name, DatabaseObjectType.Function));
            Children.Add(functionsFolder);

            // Triggers folder
            var triggersFolder = new FolderNodeViewModel("Triggers", DatabaseObjectType.TriggerFolder);
            var triggers = await provider.GetTriggersAsync(Name);
            foreach (var tr in triggers)
                triggersFolder.Children.Add(new ObjectNodeViewModel(tr));
            Children.Add(triggersFolder);

            // Sequences folder (PostgreSQL)
            if (_connectionInfo.DatabaseType == DatabaseType.PostgreSQL)
            {
                var sequencesFolder = new FolderNodeViewModel("Sequences", DatabaseObjectType.SequenceFolder);
                var sequences = await provider.GetSequencesAsync(Name);
                foreach (var s in sequences)
                    sequencesFolder.Children.Add(new ObjectNodeViewModel(s));
                Children.Add(sequencesFolder);
            }

            _childrenLoaded = true;
        }
        catch (Exception ex)
        {
            Children.Clear();
            Children.Add(new TreeNodeViewModel($"Error: {ex.Message}", DatabaseObjectType.TableFolder));
        }
    }
}

/// <summary>
/// ViewModel for a table node with context menu commands (SELECT TOP, copy name, etc.).
/// </summary>
public partial class TableNodeViewModel : TreeNodeViewModel
{
    private readonly ConnectionInfo _connectionInfo;
    private readonly string _database;
    private readonly IConnectionManager _connectionManager;
    private readonly MainViewModel _mainViewModel;
    private readonly IQueryHistoryService? _queryHistoryService;
    private bool _childrenLoaded;

    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    public TableNodeViewModel(string name, ConnectionInfo connectionInfo, string database,
        IConnectionManager connectionManager, MainViewModel mainViewModel,
        IQueryHistoryService? queryHistoryService = null)
        : base(name, DatabaseObjectType.Table)
    {
        _connectionInfo = connectionInfo;
        _database = database;
        _connectionManager = connectionManager;
        _mainViewModel = mainViewModel;
        _queryHistoryService = queryHistoryService;

        // Placeholder for lazy loading
        Children.Add(new TreeNodeViewModel("Loading...", DatabaseObjectType.Column));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_childrenLoaded)
        {
            _ = LoadChildrenAsync();
        }
    }

    private async Task LoadChildrenAsync()
    {
        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            await provider.ChangeDatabaseAsync(_database);

            Children.Clear();

            // Columns
            var columns = await provider.GetColumnsAsync(_database, Name);
            var columnsFolder = new FolderNodeViewModel("Columns", DatabaseObjectType.Column);
            foreach (var col in columns)
            {
                var colDisplay = $"{col.Name} ({col.DataType}{(col.IsNullable ? ", null" : "")}{(col.IsPrimaryKey ? ", PK" : "")})";
                columnsFolder.Children.Add(new TreeNodeViewModel(colDisplay, DatabaseObjectType.Column));
            }
            Children.Add(columnsFolder);

            // Indexes
            var indexes = await provider.GetIndexesAsync(_database, Name);
            if (indexes.Count > 0)
            {
                var indexesFolder = new FolderNodeViewModel("Indexes", DatabaseObjectType.IndexFolder);
                foreach (var idx in indexes)
                    indexesFolder.Children.Add(new ObjectNodeViewModel(idx));
                Children.Add(indexesFolder);
            }

            _childrenLoaded = true;
        }
        catch (Exception ex)
        {
            Children.Clear();
            Children.Add(new TreeNodeViewModel($"Error: {ex.Message}", DatabaseObjectType.Column));
        }
    }

    [RelayCommand]
    private void OpenData()
    {
        var tab = new TableDataTabViewModel(_connectionManager);
        tab.SetConnection(_connectionInfo, _database, Name);
        _mainViewModel.AddTab(tab);
        _ = tab.LoadDataAsync();
    }

    [RelayCommand]
    private void SelectTop()
    {
        var sql = _connectionInfo.DatabaseType switch
        {
            DatabaseType.PostgreSQL => $"SELECT * FROM \"{Name}\" LIMIT 100;",
            DatabaseType.MySQL or DatabaseType.MariaDB => $"SELECT * FROM `{Name}` LIMIT 100;",
            DatabaseType.SQLite => $"SELECT * FROM \"{Name}\" LIMIT 100;",
            _ => $"SELECT * FROM {Name} LIMIT 100;"
        };

        var tab = new QueryTabViewModel(_connectionManager, _queryHistoryService)
        {
            Title = $"{Name} - {_database}",
            SqlText = sql,
        };
        tab.SetConnection(_connectionInfo);
        tab.SelectedDatabase = _database;
        _mainViewModel.WireQueryTabDelegates(tab);
        _mainViewModel.AddTab(tab);
    }

    [RelayCommand]
    private void DesignTable()
    {
        var tab = new TableDesignerTabViewModel(_connectionManager);
        tab.SetConnection(_connectionInfo, _database);
        _ = tab.LoadTableAsync(Name);
        _mainViewModel.AddTab(tab);
    }

    [RelayCommand]
    private void ExportData()
    {
        _mainViewModel.RequestExportData?.Invoke(_connectionInfo, _database, Name);
    }
}

/// <summary>
/// Simple folder node in the tree (Tables, Views, Functions).
/// </summary>
public partial class FolderNodeViewModel : TreeNodeViewModel
{
    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Icon kind based on folder type.</summary>
    public string FolderIcon => ObjectType switch
    {
        DatabaseObjectType.TableFolder => "TableLarge",
        DatabaseObjectType.ViewFolder => "Eye",
        DatabaseObjectType.FunctionFolder => "Function",
        DatabaseObjectType.TriggerFolder => "Lightning",
        DatabaseObjectType.SequenceFolder => "Numeric",
        DatabaseObjectType.IndexFolder => "KeyVariant",
        _ => "Folder"
    };

    public FolderNodeViewModel(string name, DatabaseObjectType objectType) : base(name, objectType) { }
}

/// <summary>
/// Generic tree node ViewModel.
/// </summary>
public partial class TreeNodeViewModel : ViewModelBase
{
    public string Name { get; }
    public DatabaseObjectType ObjectType { get; }

    /// <summary>Icon kind for the object type.</summary>
    public string ObjectTypeIcon => ObjectType switch
    {
        DatabaseObjectType.Table => "Table",
        DatabaseObjectType.View => "Eye",
        DatabaseObjectType.Function => "Function",
        DatabaseObjectType.Column => "TableColumn",
        DatabaseObjectType.Index => "KeyVariant",
        DatabaseObjectType.ForeignKey => "KeyLink",
        DatabaseObjectType.Trigger => "Lightning",
        DatabaseObjectType.Sequence => "Numeric",
        DatabaseObjectType.Database => "Database",
        _ => "FileDocument"
    };

    public TreeNodeViewModel(string name, DatabaseObjectType objectType)
    {
        Name = name;
        ObjectType = objectType;
    }

    [RelayCommand]
    private void CopyName()
    {
        Clipboard.SetText(Name);
    }
}

/// <summary>
/// A tree node backed by a DatabaseObjectInfo, exposing its properties for display.
/// </summary>
public partial class ObjectNodeViewModel : TreeNodeViewModel
{
    public DatabaseObjectInfo ObjectInfo { get; }

    /// <summary>Summary text with key properties.</summary>
    public string PropertiesSummary
    {
        get
        {
            if (ObjectInfo.Properties.Count == 0) return string.Empty;
            return string.Join(", ", ObjectInfo.Properties
                .Where(p => p.Value is not null)
                .Select(p => $"{p.Key}: {p.Value}"));
        }
    }

    /// <summary>Properties dictionary for binding in a detail panel.</summary>
    public IReadOnlyDictionary<string, object?> Properties => ObjectInfo.Properties;

    public ObjectNodeViewModel(DatabaseObjectInfo objectInfo)
        : base(objectInfo.Name, objectInfo.ObjectType)
    {
        ObjectInfo = objectInfo;
    }
}

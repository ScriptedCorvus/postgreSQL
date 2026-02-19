using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DatabaseClient.App.Services;
using DatabaseClient.App.ViewModels;
using DatabaseClient.App.Views;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;
using MahApps.Metro.Controls;

namespace DatabaseClient.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    private readonly IConnectionManager _connectionManager;
    private readonly IProfileSharingService _profileSharingService;
    private readonly IConnectionRepository _connectionRepository;
    private readonly ISchedulerService _schedulerService;

    public MainWindow(MainViewModel viewModel, SnackbarNotificationService notificationService,
        IConnectionManager connectionManager, IProfileSharingService profileSharingService,
        IConnectionRepository connectionRepository, ISchedulerService schedulerService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _connectionManager = connectionManager;
        _profileSharingService = profileSharingService;
        _connectionRepository = connectionRepository;
        _schedulerService = schedulerService;

        // Wire up dialog services
        viewModel.ShowConnectionDialog = ShowConnectionDialog;
        viewModel.ShowSettingsDialog = ShowSettingsDialog;
        viewModel.RequestImportData = ShowImportDataDialog;
        viewModel.RequestExportData = ShowExportDataDialog;
        viewModel.RequestOpenProfileSharing = ShowProfileSharingDialog;
        viewModel.RequestOpenDataComparison = ShowDataComparisonDialog;
        viewModel.RequestOpenSchemaComparison = ShowSchemaComparisonDialog;
        viewModel.RequestOpenMigrationWizard = ShowMigrationWizardDialog;
        viewModel.RequestOpenUserManagement = ShowUserManagementDialog;
        viewModel.RequestOpenScheduler = ShowSchedulerDialog;

        // Wire import/export connection file dialogs
        viewModel.BrowseImportConnectionsFile = () =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Connections",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        };
        viewModel.BrowseExportConnectionsFile = () =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Connections",
                Filter = "JSON Files (*.json)|*.json",
                DefaultExt = ".json",
                FileName = "connections-export.json"
            };
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        };

        // Wire Snackbar
        notificationService.SetMessageQueue(MainSnackbar.MessageQueue!);

        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    private bool ShowConnectionDialog(ConnectionDialogViewModel dialogVm)
    {
        var dialog = new ConnectionDialog(dialogVm)
        {
            Owner = this
        };
        return dialog.ShowDialog() == true;
    }

    private bool ShowSettingsDialog(SettingsDialogViewModel dialogVm)
    {
        var dialog = new SettingsDialog(dialogVm)
        {
            Owner = this
        };
        return dialog.ShowDialog() == true;
    }

    private void ShowImportDataDialog(ConnectionInfo connectionInfo, string database)
    {
        var vm = new DataImportWizardViewModel(_connectionManager);
        vm.SetConnection(connectionInfo, database);
        var dialog = new DataImportDialog
        {
            DataContext = vm,
            Owner = this
        };
        dialog.ShowDialog();
    }

    private async void ShowExportDataDialog(ConnectionInfo connectionInfo, string database, string? tableName)
    {
        var vm = new DataExportWizardViewModel(_connectionManager);
        await vm.SetConnectionAsync(connectionInfo, database, tableName);
        var dialog = new DataExportDialog
        {
            DataContext = vm,
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void ShowProfileSharingDialog()
    {
        var vm = new ProfileSharingViewModel(_profileSharingService, _connectionRepository);
        var dialog = new ProfileSharingDialog(vm)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void ShowDataComparisonDialog()
    {
        var vm = new DataComparisonViewModel(_connectionManager, _connectionRepository);
        var dialog = new DataComparisonDialog
        {
            DataContext = vm,
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void ShowSchemaComparisonDialog()
    {
        var vm = new SchemaComparisonViewModel(_connectionManager, _connectionRepository);
        var dialog = new SchemaComparisonDialog
        {
            DataContext = vm,
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void ShowMigrationWizardDialog()
    {
        var vm = new MigrationWizardViewModel(_connectionManager, _connectionRepository);
        var dialog = new MigrationWizardDialog
        {
            DataContext = vm,
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void ShowUserManagementDialog(ConnectionInfo connectionInfo, string database)
    {
        var vm = new UserManagementViewModel(_connectionManager, connectionInfo, database);
        var dialog = new UserManagementDialog
        {
            DataContext = vm,
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void ShowSchedulerDialog()
    {
        var vm = new SchedulerViewModel(_schedulerService, _connectionRepository);
        var dialog = new SchedulerDialog
        {
            DataContext = vm,
            Owner = this
        };
        dialog.ShowDialog();
    }

    // ── Tab Drag & Drop Reordering ─────────────────────────────────────────

    private Point _tabDragStartPoint;
    private bool _tabDragInProgress;

    private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _tabDragStartPoint = e.GetPosition(null);
        _tabDragInProgress = false;
    }

    private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _tabDragInProgress) return;

        var diff = _tabDragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // Find the TabItem being dragged
        var tabItem = FindParent<TabItem>((DependencyObject)e.OriginalSource);
        if (tabItem?.DataContext is not TabViewModelBase draggedTab) return;

        _tabDragInProgress = true;
        DragDrop.DoDragDrop(tabItem, draggedTab, DragDropEffects.Move);
        _tabDragInProgress = false;
    }

    private void TabControl_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(QueryTabViewModel)) ||
                    e.Data.GetDataPresent(typeof(TableDesignerTabViewModel)) ||
                    e.Data.GetDataPresent(typeof(TableDataTabViewModel)) ||
                    e.Data.GetDataPresent(typeof(QueryHistoryTabViewModel)) ||
                    e.Data.GetDataPresent(typeof(SnippetsTabViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void TabControl_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Find the dragged tab from any known type
        TabViewModelBase? draggedTab = null;
        foreach (var type in new[] { typeof(QueryTabViewModel), typeof(TableDesignerTabViewModel),
                     typeof(TableDataTabViewModel), typeof(QueryHistoryTabViewModel), typeof(SnippetsTabViewModel) })
        {
            if (e.Data.GetDataPresent(type))
            {
                draggedTab = e.Data.GetData(type) as TabViewModelBase;
                break;
            }
        }
        if (draggedTab == null) return;

        // Find the target TabItem
        var targetTabItem = FindParent<TabItem>((DependencyObject)e.OriginalSource);
        if (targetTabItem?.DataContext is not TabViewModelBase targetTab || targetTab == draggedTab) return;

        var oldIndex = vm.Tabs.IndexOf(draggedTab);
        var newIndex = vm.Tabs.IndexOf(targetTab);
        if (oldIndex < 0 || newIndex < 0) return;

        vm.Tabs.Move(oldIndex, newIndex);
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current != null)
        {
            if (current is T found) return found;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
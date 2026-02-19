using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the Task Scheduler management dialog.
/// </summary>
public partial class SchedulerViewModel : ViewModelBase
{
    private readonly ISchedulerService _schedulerService;
    private readonly IConnectionRepository _connectionRepository;

    public ObservableCollection<ScheduledTask> Tasks { get; } = [];
    public ObservableCollection<ConnectionInfo> Connections { get; } = [];

    [ObservableProperty]
    private ScheduledTask? _selectedTask;

    // ── New Task Fields ──────────────────────────────────────────────────

    [ObservableProperty]
    private string _taskName = string.Empty;

    [ObservableProperty]
    private ScheduledTaskType _taskType = ScheduledTaskType.SqlScript;

    [ObservableProperty]
    private ConnectionInfo? _taskConnection;

    [ObservableProperty]
    private string _taskDatabase = string.Empty;

    [ObservableProperty]
    private int _intervalMinutes = 60;

    [ObservableProperty]
    private string _scriptContent = string.Empty;

    [ObservableProperty]
    private string _exportPath = string.Empty;

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private string _backupPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _executionLog = string.Empty;

    public ScheduledTaskType[] TaskTypes { get; } = Enum.GetValues<ScheduledTaskType>();

    public SchedulerViewModel(ISchedulerService schedulerService, IConnectionRepository connectionRepository)
    {
        _schedulerService = schedulerService;
        _connectionRepository = connectionRepository;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var conns = await _connectionRepository.GetAllAsync();
        Connections.Clear();
        foreach (var c in conns)
            Connections.Add(c);

        await RefreshTasksAsync();
    }

    [RelayCommand]
    private async Task RefreshTasksAsync()
    {
        var tasks = await _schedulerService.GetTasksAsync();
        Tasks.Clear();
        foreach (var t in tasks)
            Tasks.Add(t);
        StatusMessage = $"{Tasks.Count} task(s) loaded.";
    }

    [RelayCommand]
    private async Task SaveTask()
    {
        if (string.IsNullOrWhiteSpace(TaskName))
        {
            StatusMessage = "Task name is required.";
            return;
        }

        if (TaskConnection == null)
        {
            StatusMessage = "Please select a connection.";
            return;
        }

        var task = new ScheduledTask
        {
            Name = TaskName,
            TaskType = TaskType,
            ConnectionId = TaskConnection.Id,
            Database = TaskDatabase,
            IntervalMinutes = IntervalMinutes,
            ScriptContent = TaskType == ScheduledTaskType.SqlScript ? ScriptContent : null,
            ExportPath = TaskType == ScheduledTaskType.Export ? ExportPath : null,
            TableName = TaskType == ScheduledTaskType.Export ? TableName : null,
            BackupPath = TaskType == ScheduledTaskType.Backup ? BackupPath : null,
            IsEnabled = true
        };

        await _schedulerService.SaveTaskAsync(task);
        StatusMessage = $"Task '{task.Name}' saved.";
        TaskName = string.Empty;
        ScriptContent = string.Empty;
        await RefreshTasksAsync();
    }

    [RelayCommand]
    private async Task DeleteTask()
    {
        if (SelectedTask == null) return;
        await _schedulerService.DeleteTaskAsync(SelectedTask.Id);
        StatusMessage = $"Task '{SelectedTask.Name}' deleted.";
        await RefreshTasksAsync();
    }

    [RelayCommand]
    private async Task RunNow()
    {
        if (SelectedTask == null) return;
        IsBusy = true;
        StatusMessage = $"Executing '{SelectedTask.Name}'...";

        try
        {
            var result = await _schedulerService.ExecuteTaskAsync(SelectedTask);
            ExecutionLog += $"[{DateTime.Now:HH:mm:ss}] {SelectedTask.Name}: {result}\n";
            StatusMessage = $"Task completed: {result}";
            await RefreshTasksAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleEnabled()
    {
        if (SelectedTask == null) return;
        SelectedTask.IsEnabled = !SelectedTask.IsEnabled;
        SelectedTask.NextRun = SelectedTask.IsEnabled
            ? DateTime.Now.AddMinutes(SelectedTask.IntervalMinutes)
            : null;
        await _schedulerService.SaveTaskAsync(SelectedTask);
        StatusMessage = $"Task '{SelectedTask.Name}' {(SelectedTask.IsEnabled ? "enabled" : "disabled")}.";
        await RefreshTasksAsync();
    }
}

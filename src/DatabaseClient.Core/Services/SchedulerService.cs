using System.IO;
using System.Text.Json;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Scheduler service that manages and executes scheduled tasks using a timer.
/// Tasks are persisted in a JSON file under AppData.
/// </summary>
public class SchedulerService : ISchedulerService, IDisposable
{
    private readonly IConnectionManager _connectionManager;
    private readonly IConnectionRepository _connectionRepository;
    private readonly string _tasksFilePath;
    private List<ScheduledTask> _tasks = [];
    private Timer? _timer;
    private bool _isRunning;

    public event Action<ScheduledTask, string>? TaskExecuted;

    public SchedulerService(IConnectionManager connectionManager, IConnectionRepository connectionRepository)
    {
        _connectionManager = connectionManager;
        _connectionRepository = connectionRepository;

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DatabaseClient");
        Directory.CreateDirectory(appData);
        _tasksFilePath = Path.Combine(appData, "scheduled-tasks.json");
    }

    public async Task<IReadOnlyList<ScheduledTask>> GetTasksAsync()
    {
        await LoadTasksAsync();
        return _tasks.AsReadOnly();
    }

    public async Task SaveTaskAsync(ScheduledTask task)
    {
        await LoadTasksAsync();

        var existing = _tasks.FindIndex(t => t.Id == task.Id);
        if (existing >= 0)
            _tasks[existing] = task;
        else
            _tasks.Add(task);

        // Set next run
        if (task.NextRun == null && task.IsEnabled)
            task.NextRun = DateTime.Now.AddMinutes(task.IntervalMinutes);

        await PersistTasksAsync();
    }

    public async Task DeleteTaskAsync(string taskId)
    {
        await LoadTasksAsync();
        _tasks.RemoveAll(t => t.Id == taskId);
        await PersistTasksAsync();
    }

    public async Task<string> ExecuteTaskAsync(ScheduledTask task)
    {
        try
        {
            var connections = await _connectionRepository.GetAllAsync();
            var conn = connections.FirstOrDefault(c => c.Id == task.ConnectionId);
            if (conn == null) return "Error: Connection not found.";

            var provider = await _connectionManager.GetProviderAsync(conn);

            if (!string.IsNullOrEmpty(task.Database))
                await provider.ChangeDatabaseAsync(task.Database);

            string result;

            switch (task.TaskType)
            {
                case ScheduledTaskType.SqlScript:
                    if (string.IsNullOrEmpty(task.ScriptContent))
                        return "Error: No script content.";
                    var queryResult = await provider.ExecuteQueryAsync(task.ScriptContent);
                    result = queryResult.IsSuccessful
                        ? $"Script executed. Rows: {queryResult.RowsAffected}"
                        : $"Script error: {queryResult.Errors}";
                    break;

                case ScheduledTaskType.Export:
                    if (string.IsNullOrEmpty(task.TableName) || string.IsNullOrEmpty(task.ExportPath))
                        return "Error: Table name and export path required.";
                    var exportResult = await provider.ExecuteQueryAsync($"SELECT * FROM {task.TableName}");
                    if (exportResult.IsSuccessful && exportResult.ResultSet != null)
                    {
                        var csv = DataTableToCsv(exportResult.ResultSet);
                        var dir = Path.GetDirectoryName(task.ExportPath);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);
                        await File.WriteAllTextAsync(task.ExportPath, csv);
                        result = $"Exported {exportResult.ResultSet.Rows.Count} rows to {task.ExportPath}";
                    }
                    else
                    {
                        result = $"Export error: {exportResult.Errors}";
                    }
                    break;

                case ScheduledTaskType.Backup:
                    // Execute a simple backup via SQL dump of table list
                    var tablesResult = await provider.GetTablesAsync(task.Database);
                    var backupPath = task.BackupPath ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DatabaseClient", "backups",
                        $"backup_{task.Database}_{DateTime.Now:yyyyMMdd_HHmmss}.sql");

                    var backupDir = Path.GetDirectoryName(backupPath);
                    if (!string.IsNullOrEmpty(backupDir))
                        Directory.CreateDirectory(backupDir);

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"-- Backup of {task.Database}");
                    sb.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine();

                    foreach (var table in tablesResult)
                    {
                        var data = await provider.ExecuteQueryAsync($"SELECT * FROM \"{table.Name}\"");
                        if (data.IsSuccessful && data.ResultSet != null)
                        {
                            foreach (System.Data.DataRow row in data.ResultSet.Rows)
                            {
                                var cols = new List<string>();
                                var vals = new List<string>();
                                for (int i = 0; i < data.ResultSet.Columns.Count; i++)
                                {
                                    cols.Add($"\"{data.ResultSet.Columns[i].ColumnName}\"");
                                    var val = row[i];
                                    vals.Add(val == null || val == DBNull.Value
                                        ? "NULL"
                                        : $"'{val.ToString()?.Replace("'", "''")}'");
                                }
                                sb.AppendLine($"INSERT INTO \"{table.Name}\" ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});");
                            }
                            sb.AppendLine();
                        }
                    }

                    await File.WriteAllTextAsync(backupPath, sb.ToString());
                    result = $"Backup created: {backupPath} ({tablesResult.Count} tables)";
                    break;

                default:
                    result = "Unknown task type.";
                    break;
            }

            task.LastRun = DateTime.Now;
            task.LastStatus = result;
            task.NextRun = DateTime.Now.AddMinutes(task.IntervalMinutes);
            await PersistTasksAsync();

            TaskExecuted?.Invoke(task, result);
            return result;
        }
        catch (Exception ex)
        {
            task.LastRun = DateTime.Now;
            task.LastStatus = $"Error: {ex.Message}";
            await PersistTasksAsync();
            TaskExecuted?.Invoke(task, task.LastStatus);
            return task.LastStatus;
        }
    }

    public Task StartAsync()
    {
        if (_isRunning) return Task.CompletedTask;
        _isRunning = true;
        _timer = new Timer(CheckAndExecuteTasks, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _isRunning = false;
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }

    private async void CheckAndExecuteTasks(object? state)
    {
        try
        {
            await LoadTasksAsync();
            var now = DateTime.Now;

            foreach (var task in _tasks.Where(t => t.IsEnabled && t.NextRun.HasValue && t.NextRun <= now))
            {
                await ExecuteTaskAsync(task);
            }
        }
        catch { /* ignore timer errors */ }
    }

    private async Task LoadTasksAsync()
    {
        if (File.Exists(_tasksFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_tasksFilePath);
                _tasks = JsonSerializer.Deserialize<List<ScheduledTask>>(json) ?? [];
            }
            catch
            {
                _tasks = [];
            }
        }
    }

    private async Task PersistTasksAsync()
    {
        var json = JsonSerializer.Serialize(_tasks, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_tasksFilePath, json);
    }

    private static string DataTableToCsv(System.Data.DataTable table)
    {
        var sb = new System.Text.StringBuilder();
        var colNames = new List<string>();
        for (int i = 0; i < table.Columns.Count; i++)
            colNames.Add(table.Columns[i].ColumnName);
        sb.AppendLine(string.Join(",", colNames));

        foreach (System.Data.DataRow row in table.Rows)
        {
            var vals = colNames.Select(c =>
            {
                var val = row[c]?.ToString() ?? "";
                return val.Contains(',') || val.Contains('"') || val.Contains('\n')
                    ? $"\"{val.Replace("\"", "\"\"")}\""
                    : val;
            });
            sb.AppendLine(string.Join(",", vals));
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

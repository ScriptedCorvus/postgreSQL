using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Service for managing and executing scheduled tasks.
/// </summary>
public interface ISchedulerService
{
    Task<IReadOnlyList<ScheduledTask>> GetTasksAsync();
    Task SaveTaskAsync(ScheduledTask task);
    Task DeleteTaskAsync(string taskId);
    Task<string> ExecuteTaskAsync(ScheduledTask task);
    Task StartAsync();
    Task StopAsync();
    event Action<ScheduledTask, string>? TaskExecuted;
}

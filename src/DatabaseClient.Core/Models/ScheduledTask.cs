namespace DatabaseClient.Core.Models;

/// <summary>
/// Represents a scheduled task that can be executed periodically.
/// </summary>
public class ScheduledTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public ScheduledTaskType TaskType { get; set; }
    public Guid ConnectionId { get; set; }
    public string Database { get; set; } = string.Empty;

    /// <summary>Interval in minutes between executions.</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Whether the task is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>SQL script for Script tasks.</summary>
    public string? ScriptContent { get; set; }

    /// <summary>Export file path for Export tasks.</summary>
    public string? ExportPath { get; set; }

    /// <summary>Table name for Export tasks.</summary>
    public string? TableName { get; set; }

    /// <summary>Backup file path for Backup tasks.</summary>
    public string? BackupPath { get; set; }

    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
    public string LastStatus { get; set; } = "Never run";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public enum ScheduledTaskType
{
    Backup,
    SqlScript,
    Export
}

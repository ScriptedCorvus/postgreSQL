namespace DatabaseClient.Core.Models;

/// <summary>
/// Represents a saved query history entry.
/// </summary>
public class QueryHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sql { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ExecutionTime { get; set; }
    public int RowsAffected { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
}

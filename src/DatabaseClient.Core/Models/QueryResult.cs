using System.Data;

namespace DatabaseClient.Core.Models;

/// <summary>
/// Encapsulates the result of a SQL query execution.
/// </summary>
public class QueryResult
{
    public DataTable? ResultSet { get; set; }
    public int RowsAffected { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public List<string> Messages { get; set; } = [];
    public List<QueryError> Errors { get; set; } = [];
    public bool IsSuccessful => Errors.Count == 0;
}

/// <summary>
/// Represents a SQL execution error.
/// </summary>
public class QueryError
{
    public string Message { get; set; } = string.Empty;
    public string? SqlState { get; set; }
    public int? Line { get; set; }
    public string? Detail { get; set; }
}

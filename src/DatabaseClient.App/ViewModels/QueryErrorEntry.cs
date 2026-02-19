namespace DatabaseClient.App.ViewModels;

/// <summary>
/// Represents a structured error entry displayed in the query tab error panel.
/// </summary>
public class QueryErrorEntry
{
    /// <summary>Severity icon kind: "Error", "Warning", or "Information".</summary>
    public string Severity { get; init; } = "Error";

    /// <summary>Source line number in the SQL editor (1-based), or null if unknown.</summary>
    public int? Line { get; init; }

    /// <summary>SQL state code (e.g., "42P01" for PostgreSQL), or null.</summary>
    public string? SqlState { get; init; }

    /// <summary>Human-readable error message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Additional detail text, if available.</summary>
    public string? Detail { get; init; }

    /// <summary>Display text for the line column.</summary>
    public string LineDisplay => Line.HasValue ? Line.Value.ToString() : "-";
}

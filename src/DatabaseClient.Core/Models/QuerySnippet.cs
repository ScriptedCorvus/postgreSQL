using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Models;

/// <summary>
/// Represents a saved SQL snippet / favorite query.
/// </summary>
public class QuerySnippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public DatabaseType? DatabaseType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

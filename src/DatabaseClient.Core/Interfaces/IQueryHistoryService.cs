using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Manages query history persistence and retrieval.
/// </summary>
public interface IQueryHistoryService
{
    /// <summary>Adds a new history entry.</summary>
    Task AddEntryAsync(QueryHistoryEntry entry);

    /// <summary>Gets all history entries, newest first.</summary>
    Task<IReadOnlyList<QueryHistoryEntry>> GetAllAsync();

    /// <summary>Searches history by SQL text.</summary>
    Task<IReadOnlyList<QueryHistoryEntry>> SearchAsync(string searchText);

    /// <summary>Clears all history.</summary>
    Task ClearAsync();

    /// <summary>Deletes a specific entry.</summary>
    Task DeleteAsync(Guid id);

    /// <summary>Gets the count of history entries.</summary>
    Task<int> GetCountAsync();
}

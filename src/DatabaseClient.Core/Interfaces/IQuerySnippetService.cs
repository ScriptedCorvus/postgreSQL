using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Manages saved SQL snippets / favorite queries.
/// </summary>
public interface IQuerySnippetService
{
    Task<IReadOnlyList<QuerySnippet>> GetAllAsync();
    Task<IReadOnlyList<QuerySnippet>> SearchAsync(string searchText);
    Task SaveAsync(QuerySnippet snippet);
    Task DeleteAsync(string id);
    Task<IReadOnlyList<QuerySnippet>> GetBuiltInSnippetsAsync(DatabaseType dbType);
}

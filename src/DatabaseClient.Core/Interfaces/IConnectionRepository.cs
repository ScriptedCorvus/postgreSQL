using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Manages persistence of connection configurations.
/// </summary>
public interface IConnectionRepository
{
    Task<IReadOnlyList<ConnectionInfo>> GetAllAsync();
    Task<ConnectionInfo?> GetByIdAsync(Guid id);
    Task SaveAsync(ConnectionInfo connection);
    Task DeleteAsync(Guid id);
    Task SaveAllAsync(IEnumerable<ConnectionInfo> connections);
    
    /// <summary>Exports connections to a JSON file.</summary>
    Task ExportConnectionsAsync(string filePath, IEnumerable<ConnectionInfo> connections);
    
    /// <summary>Imports connections from a JSON file, returning the imported connections.</summary>
    Task<IReadOnlyList<ConnectionInfo>> ImportConnectionsAsync(string filePath);
}

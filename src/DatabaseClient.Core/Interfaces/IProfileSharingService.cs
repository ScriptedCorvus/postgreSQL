using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Manages collaboration features: export/import profiles, connections, and snippets.
/// </summary>
public interface IProfileSharingService
{
    /// <summary>
    /// Exports a complete profile (settings + connections + snippets + templates) to a .dbcprofile file.
    /// Passwords are encrypted with the provided master key.
    /// </summary>
    Task ExportProfileAsync(string filePath, string masterKey);

    /// <summary>
    /// Imports a profile from a .dbcprofile file.
    /// </summary>
    Task ImportProfileAsync(string filePath, string masterKey, bool replace);

    /// <summary>
    /// Exports selected connections to a .dbcconnection file.
    /// </summary>
    Task ExportConnectionsAsync(string filePath, IEnumerable<ConnectionInfo> connections, string masterKey);

    /// <summary>
    /// Imports connections from a .dbcconnection file.
    /// </summary>
    Task<IReadOnlyList<ConnectionInfo>> ImportConnectionsAsync(string filePath, string masterKey);

    /// <summary>
    /// Exports snippets to a .dbcsnippets file.
    /// </summary>
    Task ExportSnippetsAsync(string filePath);

    /// <summary>
    /// Imports snippets from a .dbcsnippets file, optionally merging with existing.
    /// </summary>
    Task ImportSnippetsAsync(string filePath, bool merge);
}

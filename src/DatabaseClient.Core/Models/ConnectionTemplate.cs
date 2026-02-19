namespace DatabaseClient.Core.Models;

/// <summary>
/// Represents a connection template with pre-filled defaults for quick connection creation.
/// </summary>
public class ConnectionTemplate
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display name of the template.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of the template.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Category for grouping (e.g., "Local", "AWS", "Google Cloud", "Azure").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Database type.</summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>Default host.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Default port.</summary>
    public int Port { get; set; } = 5432;

    /// <summary>Default username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Default database name.</summary>
    public string DefaultDatabase { get; set; } = string.Empty;

    /// <summary>Whether SSL is enabled by default.</summary>
    public bool UseSsl { get; set; }

    /// <summary>Whether this is a built-in template (cannot be deleted).</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Whether the template is hidden by the user.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Default color tag.</summary>
    public string ColorTag { get; set; } = "#007ACC";

    /// <summary>Help text displayed in the connection dialog when this template is selected.</summary>
    public string HelpText { get; set; } = string.Empty;

    /// <summary>Documentation URL for the cloud provider.</summary>
    public string DocumentationUrl { get; set; } = string.Empty;

    /// <summary>
    /// Applies this template's values to a ConnectionInfo object.
    /// </summary>
    public ConnectionInfo ToConnectionInfo()
    {
        return new ConnectionInfo
        {
            Name = $"New {Name}",
            DatabaseType = DatabaseType,
            Host = Host,
            Port = Port,
            Username = Username,
            DefaultDatabase = DefaultDatabase,
            UseSsl = UseSsl,
            ColorTag = ColorTag,
        };
    }
}

using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Manages connection templates (built-in and custom).
/// </summary>
public interface IConnectionTemplateService
{
    /// <summary>Gets all visible templates.</summary>
    IReadOnlyList<ConnectionTemplate> GetTemplates();

    /// <summary>Gets all templates including hidden ones.</summary>
    IReadOnlyList<ConnectionTemplate> GetAllTemplates();

    /// <summary>Gets templates filtered by category.</summary>
    IReadOnlyList<ConnectionTemplate> GetTemplatesByCategory(string category);

    /// <summary>Gets a template by ID.</summary>
    ConnectionTemplate? GetTemplate(string id);

    /// <summary>Creates a new custom template from a connection.</summary>
    ConnectionTemplate CreateFromConnection(ConnectionInfo connection, string templateName);

    /// <summary>Adds a custom template.</summary>
    void AddTemplate(ConnectionTemplate template);

    /// <summary>Updates an existing custom template.</summary>
    void UpdateTemplate(ConnectionTemplate template);

    /// <summary>Deletes a custom template. Built-in templates cannot be deleted.</summary>
    bool DeleteTemplate(string id);

    /// <summary>Hides/unhides a built-in template.</summary>
    void SetHidden(string id, bool hidden);

    /// <summary>Gets all available categories.</summary>
    IReadOnlyList<string> GetCategories();

    /// <summary>Loads templates from storage.</summary>
    Task LoadAsync();

    /// <summary>Saves custom templates to storage.</summary>
    Task SaveAsync();
}

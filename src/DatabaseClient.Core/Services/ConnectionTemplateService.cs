using System.IO;
using System.Text.Json;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Manages connection templates with built-in presets and user-defined templates.
/// Built-in templates are hardcoded; custom templates are persisted to JSON.
/// </summary>
public class ConnectionTemplateService : IConnectionTemplateService
{
    private readonly string _filePath;
    private readonly List<ConnectionTemplate> _builtInTemplates;
    private List<ConnectionTemplate> _customTemplates = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ConnectionTemplateService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseClient", "templates.json");

        _builtInTemplates = CreateBuiltInTemplates();
    }

    public IReadOnlyList<ConnectionTemplate> GetTemplates()
    {
        return GetAllTemplates().Where(t => !t.IsHidden).ToList().AsReadOnly();
    }

    public IReadOnlyList<ConnectionTemplate> GetAllTemplates()
    {
        var all = new List<ConnectionTemplate>(_builtInTemplates);
        all.AddRange(_customTemplates);
        return all.AsReadOnly();
    }

    public IReadOnlyList<ConnectionTemplate> GetTemplatesByCategory(string category)
    {
        return GetTemplates().Where(t => t.Category == category).ToList().AsReadOnly();
    }

    public ConnectionTemplate? GetTemplate(string id)
    {
        return _builtInTemplates.FirstOrDefault(t => t.Id == id)
            ?? _customTemplates.FirstOrDefault(t => t.Id == id);
    }

    public ConnectionTemplate CreateFromConnection(ConnectionInfo connection, string templateName)
    {
        var template = new ConnectionTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = templateName,
            Description = $"Custom template from '{connection.Name}'",
            Category = "Custom",
            DatabaseType = connection.DatabaseType,
            Host = connection.Host,
            Port = connection.Port,
            Username = connection.Username,
            DefaultDatabase = connection.DefaultDatabase,
            UseSsl = connection.UseSsl,
            ColorTag = connection.ColorTag,
            IsBuiltIn = false,
        };
        _customTemplates.Add(template);
        return template;
    }

    public void AddTemplate(ConnectionTemplate template)
    {
        template.IsBuiltIn = false;
        _customTemplates.Add(template);
    }

    public void UpdateTemplate(ConnectionTemplate template)
    {
        // Can only update custom templates
        var index = _customTemplates.FindIndex(t => t.Id == template.Id);
        if (index >= 0)
        {
            _customTemplates[index] = template;
        }
    }

    public bool DeleteTemplate(string id)
    {
        var template = _customTemplates.FirstOrDefault(t => t.Id == id);
        if (template == null) return false; // Cannot delete built-in

        _customTemplates.Remove(template);
        return true;
    }

    public void SetHidden(string id, bool hidden)
    {
        var template = _builtInTemplates.FirstOrDefault(t => t.Id == id);
        if (template != null)
            template.IsHidden = hidden;
    }

    public IReadOnlyList<string> GetCategories()
    {
        return GetTemplates()
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c == "Local" ? 0 : c == "Custom" ? 99 : 1)
            .ThenBy(c => c)
            .ToList()
            .AsReadOnly();
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            var data = JsonSerializer.Deserialize<TemplateStorageData>(json, _jsonOptions);
            if (data != null)
            {
                _customTemplates = data.CustomTemplates ?? new();

                // Restore hidden state of built-in templates
                if (data.HiddenBuiltInIds != null)
                {
                    foreach (var id in data.HiddenBuiltInIds)
                    {
                        var builtIn = _builtInTemplates.FirstOrDefault(t => t.Id == id);
                        if (builtIn != null)
                            builtIn.IsHidden = true;
                    }
                }
            }
        }
        catch
        {
            _customTemplates = new();
        }
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var data = new TemplateStorageData
        {
            CustomTemplates = _customTemplates,
            HiddenBuiltInIds = _builtInTemplates
                .Where(t => t.IsHidden)
                .Select(t => t.Id)
                .ToList(),
        };

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private static List<ConnectionTemplate> CreateBuiltInTemplates()
    {
        return new List<ConnectionTemplate>
        {
            // Local templates
            new()
            {
                Id = "builtin-pg-local",
                Name = "PostgreSQL Local",
                Description = "Local PostgreSQL server (localhost:5432)",
                Category = "Local",
                DatabaseType = DatabaseType.PostgreSQL,
                Host = "localhost",
                Port = 5432,
                Username = "postgres",
                DefaultDatabase = "postgres",
                ColorTag = "#336791",
                IsBuiltIn = true,
            },
            new()
            {
                Id = "builtin-mysql-local",
                Name = "MySQL Local",
                Description = "Local MySQL server (localhost:3306)",
                Category = "Local",
                DatabaseType = DatabaseType.MySQL,
                Host = "localhost",
                Port = 3306,
                Username = "root",
                ColorTag = "#4479A1",
                IsBuiltIn = true,
            },
            new()
            {
                Id = "builtin-mariadb-local",
                Name = "MariaDB Local",
                Description = "Local MariaDB server (localhost:3306)",
                Category = "Local",
                DatabaseType = DatabaseType.MariaDB,
                Host = "localhost",
                Port = 3306,
                Username = "root",
                ColorTag = "#003545",
                IsBuiltIn = true,
            },
            new()
            {
                Id = "builtin-sqlite-new",
                Name = "SQLite New File",
                Description = "Create a new SQLite database file",
                Category = "Local",
                DatabaseType = DatabaseType.SQLite,
                Host = "",
                Port = 0,
                ColorTag = "#003B57",
                IsBuiltIn = true,
            },

            // Amazon RDS
            new()
            {
                Id = "builtin-aws-rds-pg",
                Name = "Amazon RDS PostgreSQL",
                Description = "Amazon RDS for PostgreSQL (requires endpoint)",
                Category = "Amazon Web Services",
                DatabaseType = DatabaseType.PostgreSQL,
                Host = "your-instance.region.rds.amazonaws.com",
                Port = 5432,
                Username = "postgres",
                UseSsl = true,
                ColorTag = "#FF9900",
                IsBuiltIn = true,
                HelpText = "Replace the host with your RDS endpoint from the AWS Console. SSL is required by default. Username is typically 'postgres' for PostgreSQL.",
                DocumentationUrl = "https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/CHAP_PostgreSQL.html",
            },
            new()
            {
                Id = "builtin-aws-rds-mysql",
                Name = "Amazon RDS MySQL",
                Description = "Amazon RDS for MySQL (requires endpoint)",
                Category = "Amazon Web Services",
                DatabaseType = DatabaseType.MySQL,
                Host = "your-instance.region.rds.amazonaws.com",
                Port = 3306,
                Username = "admin",
                UseSsl = true,
                ColorTag = "#FF9900",
                IsBuiltIn = true,
                HelpText = "Replace the host with your RDS endpoint. Default username for MySQL RDS is 'admin'.",
                DocumentationUrl = "https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/CHAP_MySQL.html",
            },

            // Google Cloud SQL
            new()
            {
                Id = "builtin-gcp-cloudsql-pg",
                Name = "Google Cloud SQL PostgreSQL",
                Description = "Google Cloud SQL for PostgreSQL (requires instance IP)",
                Category = "Google Cloud",
                DatabaseType = DatabaseType.PostgreSQL,
                Host = "your-instance-ip",
                Port = 5432,
                Username = "postgres",
                UseSsl = true,
                ColorTag = "#4285F4",
                IsBuiltIn = true,
                HelpText = "Use the public IP of your Cloud SQL instance. For private IP, ensure VPC peering is configured. Consider using Cloud SQL Auth Proxy for secure connections.",
                DocumentationUrl = "https://cloud.google.com/sql/docs/postgres/connect-overview",
            },
            new()
            {
                Id = "builtin-gcp-cloudsql-mysql",
                Name = "Google Cloud SQL MySQL",
                Description = "Google Cloud SQL for MySQL (requires instance IP)",
                Category = "Google Cloud",
                DatabaseType = DatabaseType.MySQL,
                Host = "your-instance-ip",
                Port = 3306,
                Username = "root",
                UseSsl = true,
                ColorTag = "#4285F4",
                IsBuiltIn = true,
                HelpText = "Use the public IP of your Cloud SQL instance. Consider Cloud SQL Auth Proxy for production environments.",
                DocumentationUrl = "https://cloud.google.com/sql/docs/mysql/connect-overview",
            },

            // Azure
            new()
            {
                Id = "builtin-azure-pg",
                Name = "Azure Database for PostgreSQL",
                Description = "Azure Database for PostgreSQL Flexible Server",
                Category = "Microsoft Azure",
                DatabaseType = DatabaseType.PostgreSQL,
                Host = "your-server.postgres.database.azure.com",
                Port = 5432,
                Username = "your-admin",
                UseSsl = true,
                ColorTag = "#0078D4",
                IsBuiltIn = true,
                HelpText = "Replace host with your Azure server name. Username format: 'admin@servername' for Single Server, or just 'admin' for Flexible Server. SSL is enforced by default.",
                DocumentationUrl = "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/",
            },
            new()
            {
                Id = "builtin-azure-mysql",
                Name = "Azure Database for MySQL",
                Description = "Azure Database for MySQL Flexible Server",
                Category = "Microsoft Azure",
                DatabaseType = DatabaseType.MySQL,
                Host = "your-server.mysql.database.azure.com",
                Port = 3306,
                Username = "your-admin",
                UseSsl = true,
                ColorTag = "#0078D4",
                IsBuiltIn = true,
                HelpText = "Replace host with your Azure MySQL server name. SSL is enforced by default in Azure.",
                DocumentationUrl = "https://learn.microsoft.com/en-us/azure/mysql/flexible-server/",
            },
        };
    }

    /// <summary>
    /// Internal storage format for templates.json.
    /// </summary>
    private class TemplateStorageData
    {
        public List<ConnectionTemplate> CustomTemplates { get; set; } = new();
        public List<string> HiddenBuiltInIds { get; set; } = new();
    }
}

using DatabaseClient.Core.Models;
using DatabaseClient.Core.Services;
using FluentAssertions;

namespace DatabaseClient.Core.Tests.Services;

[TestFixture]
public class ConnectionTemplateServiceTests
{
    private ConnectionTemplateService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        // Use a temp file so we don't interfere with real templates
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "templates.json");
        _sut = new ConnectionTemplateService(tempFile);
    }

    [Test]
    public void GetTemplates_ReturnsBuiltInTemplates()
    {
        var templates = _sut.GetTemplates();
        templates.Should().NotBeEmpty();
    }

    [Test]
    public void GetTemplates_ContainsLocalPresets()
    {
        var templates = _sut.GetTemplates();
        templates.Should().Contain(t => t.Name.Contains("PostgreSQL") && t.Category == "Local");
        templates.Should().Contain(t => t.Name.Contains("MySQL") && t.Category == "Local");
        templates.Should().Contain(t => t.Name.Contains("SQLite") && t.Category == "Local");
    }

    [Test]
    public void GetTemplates_DoNotIncludeHidden()
    {
        var templates = _sut.GetTemplates();
        var first = templates[0];
        _sut.SetHidden(first.Id, true);

        var afterHide = _sut.GetTemplates();
        afterHide.Should().NotContain(t => t.Id == first.Id);
    }

    [Test]
    public void GetAllTemplates_IncludesHidden()
    {
        var allBefore = _sut.GetAllTemplates();
        var first = allBefore[0];
        _sut.SetHidden(first.Id, true);

        var allAfter = _sut.GetAllTemplates();
        allAfter.Should().Contain(t => t.Id == first.Id);
    }

    [Test]
    public void CreateFromConnection_CreatesCustomTemplate()
    {
        var conn = new ConnectionInfo
        {
            Name = "My PG",
            DatabaseType = DatabaseType.PostgreSQL,
            Host = "db.example.com",
            Port = 5432,
            Username = "admin",
            DefaultDatabase = "production"
        };

        var template = _sut.CreateFromConnection(conn, "My PG Template");

        template.Name.Should().Be("My PG Template");
        template.Category.Should().Be("Custom");
        template.DatabaseType.Should().Be(DatabaseType.PostgreSQL);
        template.Host.Should().Be("db.example.com");
        template.IsBuiltIn.Should().BeFalse();
    }

    [Test]
    public void AddTemplate_AppearsInGetTemplates()
    {
        var countBefore = _sut.GetTemplates().Count;

        _sut.AddTemplate(new ConnectionTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Custom Template",
            Category = "Custom",
            DatabaseType = DatabaseType.MySQL
        });

        _sut.GetTemplates().Should().HaveCount(countBefore + 1);
        _sut.GetTemplates().Should().Contain(t => t.Name == "Custom Template");
    }

    [Test]
    public void DeleteTemplate_RemovesCustomTemplate()
    {
        var template = new ConnectionTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = "ToDelete",
            Category = "Custom",
            DatabaseType = DatabaseType.SQLite
        };
        _sut.AddTemplate(template);

        var result = _sut.DeleteTemplate(template.Id);
        
        result.Should().BeTrue();
        _sut.GetTemplates().Should().NotContain(t => t.Id == template.Id);
    }

    [Test]
    public void DeleteTemplate_CannotDeleteBuiltIn()
    {
        var builtIn = _sut.GetTemplates().First(t => t.IsBuiltIn);
        var result = _sut.DeleteTemplate(builtIn.Id);

        result.Should().BeFalse();
    }

    [Test]
    public void GetTemplate_ById_ReturnsCorrectTemplate()
    {
        var templates = _sut.GetTemplates();
        var first = templates[0];

        var result = _sut.GetTemplate(first.Id);
        result.Should().NotBeNull();
        result!.Name.Should().Be(first.Name);
    }

    [Test]
    public void GetTemplate_UnknownId_ReturnsNull()
    {
        _sut.GetTemplate("nonexistent").Should().BeNull();
    }

    [Test]
    public void GetCategories_ReturnsDistinctCategories()
    {
        var categories = _sut.GetCategories();
        categories.Should().NotBeEmpty();
        categories.Should().Contain("Local");
    }

    [Test]
    public void GetTemplatesByCategory_FiltersCorrectly()
    {
        var local = _sut.GetTemplatesByCategory("Local");
        local.Should().NotBeEmpty();
        local.Should().OnlyContain(t => t.Category == "Local");
    }
}

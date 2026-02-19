using System.IO;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.Services;
using FluentAssertions;
using Moq;

namespace DatabaseClient.Core.Tests.Services;

[TestFixture]
public class JsonQueryHistoryServiceTests
{
    private string _tempDir = null!;
    private string _tempFile = null!;
    private Mock<ISettingsService> _settingsMock = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "history.json");

        _settingsMock = new Mock<ISettingsService>();
        _settingsMock.Setup(s => s.Settings).Returns(new AppSettings
        {
            SaveQueryHistory = true,
            MaxHistoryEntries = 500
        });
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private JsonQueryHistoryService CreateSut() => new(_settingsMock.Object, _tempFile);

    [Test]
    public async Task AddEntryAsync_StoresEntry()
    {
        var sut = CreateSut();
        var entry = CreateEntry("SELECT 1");

        await sut.AddEntryAsync(entry);

        var all = await sut.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Sql.Should().Be("SELECT 1");
    }

    [Test]
    public async Task AddEntryAsync_WhenHistoryDisabled_DoesNotStore()
    {
        _settingsMock.Setup(s => s.Settings).Returns(new AppSettings { SaveQueryHistory = false });
        var sut = CreateSut();

        await sut.AddEntryAsync(CreateEntry("SELECT 1"));

        var all = await sut.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Test]
    public async Task AddEntryAsync_TrimsToMaxEntries()
    {
        _settingsMock.Setup(s => s.Settings).Returns(new AppSettings
        {
            SaveQueryHistory = true,
            MaxHistoryEntries = 3
        });
        var sut = CreateSut();

        for (int i = 0; i < 5; i++)
            await sut.AddEntryAsync(CreateEntry($"SELECT {i}"));

        var all = await sut.GetAllAsync();
        all.Should().HaveCount(3);
        // Most recent first
        all[0].Sql.Should().Be("SELECT 4");
    }

    [Test]
    public async Task SearchAsync_FindsBySQL()
    {
        var sut = CreateSut();
        await sut.AddEntryAsync(CreateEntry("SELECT * FROM users"));
        await sut.AddEntryAsync(CreateEntry("SELECT * FROM orders"));
        await sut.AddEntryAsync(CreateEntry("INSERT INTO users VALUES (1)"));

        var results = await sut.SearchAsync("users");
        results.Should().HaveCount(2);
    }

    [Test]
    public async Task SearchAsync_FindsByConnectionName()
    {
        var sut = CreateSut();
        var entry = CreateEntry("SELECT 1");
        entry.ConnectionName = "Production";
        await sut.AddEntryAsync(entry);

        var results = await sut.SearchAsync("Production");
        results.Should().HaveCount(1);
    }

    [Test]
    public async Task ClearAsync_RemovesAllEntries()
    {
        var sut = CreateSut();
        await sut.AddEntryAsync(CreateEntry("SELECT 1"));
        await sut.AddEntryAsync(CreateEntry("SELECT 2"));

        await sut.ClearAsync();

        var all = await sut.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteAsync_RemovesSpecificEntry()
    {
        var sut = CreateSut();
        var entry1 = CreateEntry("SELECT 1");
        var entry2 = CreateEntry("SELECT 2");
        await sut.AddEntryAsync(entry1);
        await sut.AddEntryAsync(entry2);

        await sut.DeleteAsync(entry1.Id);

        var all = await sut.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Sql.Should().Be("SELECT 2");
    }

    [Test]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        var sut = CreateSut();
        await sut.AddEntryAsync(CreateEntry("SELECT 1"));
        await sut.AddEntryAsync(CreateEntry("SELECT 2"));

        var count = await sut.GetCountAsync();
        count.Should().Be(2);
    }

    [Test]
    public async Task Persistance_SurvivesRestarts()
    {
        var sut1 = CreateSut();
        await sut1.AddEntryAsync(CreateEntry("SELECT 1"));

        // Create a new instance pointing to the same file
        var sut2 = CreateSut();
        var all = await sut2.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Sql.Should().Be("SELECT 1");
    }

    private static QueryHistoryEntry CreateEntry(string sql) => new()
    {
        Sql = sql,
        ConnectionName = "TestConn",
        Database = "testdb",
        DatabaseType = DatabaseType.PostgreSQL,
        IsSuccessful = true,
        RowsAffected = 1,
        ExecutionTime = TimeSpan.FromMilliseconds(42)
    };
}

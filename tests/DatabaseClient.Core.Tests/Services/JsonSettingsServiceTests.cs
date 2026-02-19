using System.IO;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.Services;
using FluentAssertions;

namespace DatabaseClient.Core.Tests.Services;

[TestFixture]
public class JsonSettingsServiceTests
{
    private string _tempDir = null!;
    private string _tempFile = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task LoadAsync_WhenFileNotExists_CreatesDefaults()
    {
        var sut = new JsonSettingsService(_tempFile);

        await sut.LoadAsync();

        sut.Settings.Should().NotBeNull();
        sut.Settings.Theme.Should().Be(ThemeMode.Light);
        sut.Settings.MaxHistoryEntries.Should().Be(500);
    }

    [Test]
    public async Task SaveAsync_ThenLoadAsync_RoundTrips()
    {
        var sut = new JsonSettingsService(_tempFile);
        await sut.LoadAsync();
        sut.Settings.Theme = ThemeMode.Dark;
        sut.Settings.EditorFontSize = 16;
        sut.Settings.Language = "es";

        await sut.SaveAsync();

        var sut2 = new JsonSettingsService(_tempFile);
        await sut2.LoadAsync();
        sut2.Settings.Theme.Should().Be(ThemeMode.Dark);
        sut2.Settings.EditorFontSize.Should().Be(16);
        sut2.Settings.Language.Should().Be("es");
    }

    [Test]
    public async Task SaveAsync_CreatesDirectoryIfNotExists()
    {
        var nestedFile = Path.Combine(_tempDir, "nested", "dir", "settings.json");
        var sut = new JsonSettingsService(nestedFile);
        await sut.LoadAsync();

        await sut.SaveAsync();

        File.Exists(nestedFile).Should().BeTrue();
    }

    [Test]
    public async Task ResetToDefaults_RestoresDefaultSettings()
    {
        var sut = new JsonSettingsService(_tempFile);
        await sut.LoadAsync();
        sut.Settings.Theme = ThemeMode.Dark;
        sut.Settings.EditorFontSize = 20;

        sut.ResetToDefaults();

        sut.Settings.Theme.Should().Be(ThemeMode.Light);
        sut.Settings.EditorFontSize.Should().Be(14);
    }

    [Test]
    public async Task LoadAsync_CorruptFile_FallsBackToDefaults()
    {
        await File.WriteAllTextAsync(_tempFile, "{ not valid json !!!");

        var sut = new JsonSettingsService(_tempFile);
        await sut.LoadAsync();

        sut.Settings.Should().NotBeNull();
        sut.Settings.Theme.Should().Be(ThemeMode.Light);
    }

    [Test]
    public async Task LoadAsync_V1Settings_MigratesToV2()
    {
        // Write a v1 settings file (no syntax color settings)
        var v1Json = """
        {
            "version": 1,
            "theme": "Light",
            "fontSize": 12
        }
        """;
        await File.WriteAllTextAsync(_tempFile, v1Json);

        var sut = new JsonSettingsService(_tempFile);
        await sut.LoadAsync();

        sut.Settings.Version.Should().Be(2);
        sut.Settings.Theme.Should().Be(ThemeMode.Light);
        sut.Settings.SyntaxKeywordColor.Should().NotBeNullOrEmpty();
        sut.Settings.SyntaxStringColor.Should().NotBeNullOrEmpty();
        sut.Settings.SyntaxCommentColor.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task SaveAsync_RaisesSettingsChangedEvent()
    {
        var sut = new JsonSettingsService(_tempFile);
        await sut.LoadAsync();

        bool eventFired = false;
        sut.SettingsChanged += (_, _) => eventFired = true;

        await sut.SaveAsync();

        eventFired.Should().BeTrue();
    }
}

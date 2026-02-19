using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.Services;
using FluentAssertions;
using Moq;

namespace DatabaseClient.Core.Tests.Services;

[TestFixture]
public class KeyboardShortcutServiceTests
{
    private Mock<ISettingsService> _settingsMock = null!;

    [SetUp]
    public void SetUp()
    {
        _settingsMock = new Mock<ISettingsService>();
        _settingsMock.Setup(s => s.Settings).Returns(new AppSettings());
    }

    private KeyboardShortcutService CreateSut() => new(_settingsMock.Object);

    [Test]
    public void GetAllActions_ReturnsDefaultActions()
    {
        var sut = CreateSut();
        var actions = sut.GetAllActions();

        actions.Should().NotBeEmpty();
        actions.Should().Contain(a => a.ActionId == "General.NewQuery");
        actions.Should().Contain(a => a.ActionId == "Editor.Execute");
    }

    [Test]
    public void GetAllActions_HasExpectedCategories()
    {
        var sut = CreateSut();
        var categories = sut.GetAllActions().Select(a => a.Category).Distinct().ToList();

        categories.Should().Contain("General");
        categories.Should().Contain("Editor");
        categories.Should().Contain("Navigation");
        categories.Should().Contain("Data");
        categories.Should().Contain("Export");
    }

    [Test]
    public void GetShortcut_ReturnsDefaultShortcut()
    {
        var sut = CreateSut();
        sut.GetShortcut("Editor.Execute").Should().Be("F5");
    }

    [Test]
    public void GetShortcut_UnknownAction_ReturnsEmpty()
    {
        var sut = CreateSut();
        sut.GetShortcut("Nonexistent.Action").Should().BeEmpty();
    }

    [Test]
    public void SetShortcut_UpdatesShortcut()
    {
        var sut = CreateSut();
        sut.SetShortcut("Editor.Execute", "Ctrl+F5");

        sut.GetShortcut("Editor.Execute").Should().Be("Ctrl+F5");
    }

    [Test]
    public void SetShortcut_RaisesEvent()
    {
        var sut = CreateSut();
        bool eventFired = false;
        sut.ShortcutsChanged += (_, _) => eventFired = true;

        sut.SetShortcut("Editor.Execute", "Ctrl+F5");

        eventFired.Should().BeTrue();
    }

    [Test]
    public void SetShortcut_UnknownAction_NoOp()
    {
        var sut = CreateSut();
        // Should not throw
        sut.SetShortcut("Nonexistent.Action", "Ctrl+X");
        sut.GetShortcut("Nonexistent.Action").Should().BeEmpty();
    }

    [Test]
    public void ResetToDefault_RestoresDefaultShortcut()
    {
        var sut = CreateSut();
        sut.SetShortcut("Editor.Execute", "Ctrl+F5");
        sut.GetShortcut("Editor.Execute").Should().Be("Ctrl+F5");

        sut.ResetToDefault("Editor.Execute");
        sut.GetShortcut("Editor.Execute").Should().Be("F5");
    }

    [Test]
    public void ResetAllToDefaults_RestoresAll()
    {
        var sut = CreateSut();
        sut.SetShortcut("Editor.Execute", "Ctrl+F5");
        sut.SetShortcut("General.NewQuery", "Ctrl+T");

        sut.ResetAllToDefaults();

        sut.GetShortcut("Editor.Execute").Should().Be("F5");
        sut.GetShortcut("General.NewQuery").Should().Be("Ctrl+N");
    }

    [Test]
    public void FindConflicts_DetectsConflict()
    {
        var sut = CreateSut();
        // Execute and RefreshData both use F5
        var conflicts = sut.FindConflicts("Editor.Execute", "F5");
        // Should find Data.RefreshData as a conflict (it also defaults to F5)
        conflicts.Should().Contain(a => a.ActionId == "Data.RefreshData");
    }

    [Test]
    public void FindConflicts_NoConflict_ReturnsEmpty()
    {
        var sut = CreateSut();
        var conflicts = sut.FindConflicts("Editor.Execute", "Ctrl+Alt+Shift+Z");
        conflicts.Should().BeEmpty();
    }

    [Test]
    public void FindConflicts_EmptyShortcut_ReturnsEmpty()
    {
        var sut = CreateSut();
        var conflicts = sut.FindConflicts("Editor.Execute", "");
        conflicts.Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_PersistsOnlyCustomizations()
    {
        _settingsMock.Setup(s => s.SaveAsync()).Returns(Task.CompletedTask);
        var sut = CreateSut();
        sut.SetShortcut("Editor.Execute", "Ctrl+F5");

        await sut.SaveAsync();

        _settingsMock.Verify(s => s.SaveAsync(), Times.Once);
        var shortcuts = _settingsMock.Object.Settings.KeyboardShortcuts;
        shortcuts.Should().ContainKey("Editor.Execute");
        shortcuts["Editor.Execute"].Should().Be("Ctrl+F5");
    }

    [Test]
    public void Constructor_LoadsCustomizationsFromSettings()
    {
        _settingsMock.Setup(s => s.Settings).Returns(new AppSettings
        {
            KeyboardShortcuts = new Dictionary<string, string>
            {
                ["Editor.Execute"] = "Ctrl+F5"
            }
        });

        var sut = CreateSut();
        sut.GetShortcut("Editor.Execute").Should().Be("Ctrl+F5");
    }
}

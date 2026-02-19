using DatabaseClient.Core.Models;
using FluentAssertions;

namespace DatabaseClient.UI.Tests.Models;

[TestFixture]
public class ConnectionInfoTests
{
    [Test]
    public void NewConnectionInfo_HasUniqueId()
    {
        var conn1 = new ConnectionInfo();
        var conn2 = new ConnectionInfo();
        conn1.Id.Should().NotBe(conn2.Id);
    }

    [Test]
    public void NewConnectionInfo_HasDefaultValues()
    {
        var conn = new ConnectionInfo();
        conn.Host.Should().Be("localhost");
        conn.Port.Should().Be(5432);
        conn.Username.Should().BeEmpty();
        conn.Password.Should().BeEmpty();
        conn.UseSsl.Should().BeFalse();
        conn.UseSshTunnel.Should().BeFalse();
    }
}

[TestFixture]
public class AppSettingsTests
{
    [Test]
    public void NewAppSettings_HasSensibleDefaults()
    {
        var settings = new AppSettings();
        settings.Theme.Should().Be(ThemeMode.Light);
        settings.EditorFontSize.Should().Be(14);
        settings.MaxHistoryEntries.Should().Be(500);
        settings.SaveQueryHistory.Should().BeTrue();
        settings.Language.Should().Be("en");
    }

    [Test]
    public void AppSettings_KeyboardShortcuts_DefaultsToEmptyDictionary()
    {
        var settings = new AppSettings();
        settings.KeyboardShortcuts.Should().NotBeNull().And.BeEmpty();
    }
}

[TestFixture]
public class QueryHistoryEntryTests
{
    [Test]
    public void NewQueryHistoryEntry_HasUniqueId()
    {
        var entry1 = new QueryHistoryEntry();
        var entry2 = new QueryHistoryEntry();
        entry1.Id.Should().NotBe(entry2.Id);
    }

    [Test]
    public void NewQueryHistoryEntry_HasRecentTimestamp()
    {
        var entry = new QueryHistoryEntry();
        entry.ExecutedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

[TestFixture]
public class QueryResultTests
{
    [Test]
    public void QueryResult_DefaultValues()
    {
        var result = new QueryResult();
        result.IsSuccessful.Should().BeTrue(); // No errors = successful
        result.RowsAffected.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public void QueryResult_WithErrors_IsNotSuccessful()
    {
        var result = new QueryResult();
        result.Errors.Add(new QueryError { Message = "syntax error" });
        result.IsSuccessful.Should().BeFalse();
    }
}

[TestFixture]  
public class DatabaseTypeTests
{
    [Test]
    public void DatabaseType_HasExpectedValues()
    {
        Enum.GetValues<DatabaseType>().Should().Contain(DatabaseType.PostgreSQL);
        Enum.GetValues<DatabaseType>().Should().Contain(DatabaseType.MySQL);
        Enum.GetValues<DatabaseType>().Should().Contain(DatabaseType.MariaDB);
        Enum.GetValues<DatabaseType>().Should().Contain(DatabaseType.SQLite);
    }
}

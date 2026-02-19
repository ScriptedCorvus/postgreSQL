using DatabaseClient.Core.Models;
using DatabaseClient.Data.Providers;
using DatabaseClient.Data.Services;
using FluentAssertions;

namespace DatabaseClient.Data.Tests.Providers;

[TestFixture]
public class DatabaseProviderFactoryTests
{
    private DatabaseProviderFactory _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new DatabaseProviderFactory(new SshTunnelManager());
    }

    [Test]
    public void Create_PostgreSQL_ReturnsCorrectProvider()
    {
        var provider = _sut.Create(DatabaseType.PostgreSQL);
        provider.Should().NotBeNull();
        provider.Should().BeOfType<PostgreSqlProvider>();
    }

    [Test]
    public void Create_MySQL_ReturnsCorrectProvider()
    {
        var provider = _sut.Create(DatabaseType.MySQL);
        provider.Should().NotBeNull();
        provider.Should().BeOfType<MySqlProvider>();
    }

    [Test]
    public void Create_MariaDB_ReturnsCorrectProvider()
    {
        var provider = _sut.Create(DatabaseType.MariaDB);
        provider.Should().NotBeNull();
        provider.Should().BeOfType<MySqlProvider>();
    }

    [Test]
    public void Create_SQLite_ReturnsCorrectProvider()
    {
        var provider = _sut.Create(DatabaseType.SQLite);
        provider.Should().NotBeNull();
        provider.Should().BeOfType<SqliteProvider>();
    }

    [Test]
    public void Create_UnsupportedType_ThrowsNotSupportedException()
    {
        var act = () => _sut.Create((DatabaseType)99);
        act.Should().Throw<NotSupportedException>();
    }
}

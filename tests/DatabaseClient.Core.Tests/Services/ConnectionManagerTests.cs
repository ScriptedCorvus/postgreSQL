using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.Services;
using FluentAssertions;
using Moq;

namespace DatabaseClient.Core.Tests.Services;

[TestFixture]
public class ConnectionManagerTests
{
    private Mock<IDatabaseProviderFactory> _factoryMock = null!;
    private ConnectionManager _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _factoryMock = new Mock<IDatabaseProviderFactory>();
        _sut = new ConnectionManager(_factoryMock.Object);
    }

    [Test]
    public async Task GetProviderAsync_CreatesAndOpensProvider()
    {
        var connInfo = CreateConnectionInfo();
        var providerMock = new Mock<IDatabaseProvider>();
        providerMock.Setup(p => p.OpenAsync(connInfo, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _factoryMock.Setup(f => f.Create(DatabaseType.PostgreSQL)).Returns(providerMock.Object);

        var provider = await _sut.GetProviderAsync(connInfo);

        provider.Should().NotBeNull();
        providerMock.Verify(p => p.OpenAsync(connInfo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetProviderAsync_ReturnsCachedProvider_WhenAlreadyConnected()
    {
        var connInfo = CreateConnectionInfo();
        var providerMock = new Mock<IDatabaseProvider>();
        providerMock.Setup(p => p.IsConnected).Returns(true);
        providerMock.Setup(p => p.OpenAsync(connInfo, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _factoryMock.Setup(f => f.Create(DatabaseType.PostgreSQL)).Returns(providerMock.Object);

        var first = await _sut.GetProviderAsync(connInfo);
        var second = await _sut.GetProviderAsync(connInfo);

        first.Should().BeSameAs(second);
        _factoryMock.Verify(f => f.Create(DatabaseType.PostgreSQL), Times.Once);
    }

    [Test]
    public async Task CloseConnectionAsync_RemovesProvider()
    {
        var connInfo = CreateConnectionInfo();
        var providerMock = new Mock<IDatabaseProvider>();
        providerMock.Setup(p => p.OpenAsync(connInfo, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        providerMock.Setup(p => p.IsConnected).Returns(true);
        providerMock.Setup(p => p.CloseAsync()).Returns(Task.CompletedTask);
        providerMock.Setup(p => p.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _factoryMock.Setup(f => f.Create(DatabaseType.PostgreSQL)).Returns(providerMock.Object);

        await _sut.GetProviderAsync(connInfo);
        _sut.IsConnected(connInfo.Id).Should().BeTrue();

        await _sut.CloseConnectionAsync(connInfo.Id);
        _sut.IsConnected(connInfo.Id).Should().BeFalse();
    }

    [Test]
    public async Task GetActiveConnectionIds_ReturnsCorrectIds()
    {
        var connInfo = CreateConnectionInfo();
        var providerMock = new Mock<IDatabaseProvider>();
        providerMock.Setup(p => p.OpenAsync(connInfo, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _factoryMock.Setup(f => f.Create(DatabaseType.PostgreSQL)).Returns(providerMock.Object);

        _sut.GetActiveConnectionIds().Should().BeEmpty();

        await _sut.GetProviderAsync(connInfo);
        _sut.GetActiveConnectionIds().Should().Contain(connInfo.Id);
    }

    [Test]
    public async Task CloseConnectionAsync_NoOp_WhenUnknownId()
    {
        await _sut.CloseConnectionAsync(Guid.NewGuid());
        // Should not throw
    }

    [Test]
    public void IsConnected_ReturnsFalse_WhenNotConnected()
    {
        _sut.IsConnected(Guid.NewGuid()).Should().BeFalse();
    }

    private static ConnectionInfo CreateConnectionInfo() => new()
    {
        Name = "Test",
        DatabaseType = DatabaseType.PostgreSQL,
        Host = "localhost",
        Port = 5432,
        Username = "test",
        Password = "test",
        DefaultDatabase = "testdb"
    };
}

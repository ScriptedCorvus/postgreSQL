using DatabaseClient.Core.Services;
using FluentAssertions;

namespace DatabaseClient.Core.Tests.Services;

[TestFixture]
public class DatabaseErrorTranslatorTests
{
    // -- PostgreSQL --

    [Test]
    public void Translate_PostgreSqlAuthFailure_ReturnsFriendlyMessage()
    {
        var ex = CreateException("NpgsqlException", "FATAL: password authentication failed SqlState: 28P01");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("Authentication failed");
    }

    [Test]
    public void Translate_PostgreSqlDbNotExists_ReturnsFriendlyMessage()
    {
        var ex = CreateException("PostgresException", "database \"foobar\" does not exist SqlState: 3D000");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("does not exist");
    }

    [Test]
    public void Translate_PostgreSqlSyntaxError_ReturnsFriendlyMessage()
    {
        var ex = CreateException("NpgsqlException", "syntax error at or near SqlState: 42601");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("syntax error");
    }

    [Test]
    public void Translate_PostgreSqlDuplicateKey_ReturnsFriendlyMessage()
    {
        var ex = CreateException("PostgresException", "duplicate key value violates unique constraint SqlState: 23505");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("Duplicate key");
    }

    [Test]
    public void Translate_PostgreSqlTableNotFound_ReturnsFriendlyMessage()
    {
        var ex = CreateException("PostgresException", "relation \"xyz\" does not exist SqlState: 42P01");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("not found");
    }

    // -- MySQL --

    [Test]
    public void Translate_MySqlAccessDenied_ReturnsFriendlyMessage()
    {
        var ex = CreateException("MySqlException", "Access denied for user 'root'@'localhost'");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("Authentication failed");
    }

    [Test]
    public void Translate_MySqlUnknownDatabase_ReturnsFriendlyMessage()
    {
        var ex = CreateException("MySqlException", "Unknown database 'nonexistent'");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("does not exist");
    }

    [Test]
    public void Translate_MySqlDuplicateEntry_ReturnsFriendlyMessage()
    {
        var ex = CreateException("MySqlException", "Duplicate entry '1' for key 'PRIMARY'");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("Duplicate key");
    }

    [Test]
    public void Translate_MySqlSyntaxError_ReturnsFriendlyMessage()
    {
        var ex = CreateException("MySqlException", "You have an error in your SQL syntax");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("syntax error");
    }

    [Test]
    public void Translate_MySqlTooManyConnections_ReturnsFriendlyMessage()
    {
        var ex = CreateException("MySqlException", "Too many connections");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("Too many connections");
    }

    [Test]
    public void Translate_MySqlLostConnection_ReturnsFriendlyMessage()
    {
        var ex = CreateException("MySqlException", "Lost connection to MySQL server");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("Connection lost");
    }

    // -- SQLite --

    [Test]
    public void Translate_SqliteNoSuchTable_ReturnsFriendlyMessage()
    {
        var ex = CreateException("SqliteException", "SQLite Error 1: 'no such table: users'");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("Table not found");
    }

    [Test]
    public void Translate_SqliteUniqueConstraint_ReturnsFriendlyMessage()
    {
        var ex = CreateException("SqliteException", "UNIQUE constraint failed: users.email");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("Duplicate key");
    }

    [Test]
    public void Translate_SqliteDatabaseLocked_ReturnsFriendlyMessage()
    {
        var ex = CreateException("SqliteException", "database is locked");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("locked");
    }

    [Test]
    public void Translate_SqliteSyntaxError_ReturnsFriendlyMessage()
    {
        var ex = CreateException("SqliteException", "near \"SELECCT\": syntax error");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("syntax error");
    }

    // -- Generic --

    [Test]
    public void Translate_SocketExceptionConnectionRefused_ReturnsFriendlyMessage()
    {
        var ex = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused);
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("Connection refused");
    }

    [Test]
    public void Translate_TimeoutException_ReturnsFriendlyMessage()
    {
        var ex = new TimeoutException("Operation timed out");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("timed out");
    }

    [Test]
    public void Translate_OperationCancelled_ReturnsFriendlyMessage()
    {
        var ex = new OperationCanceledException();
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Contain("cancelled");
    }

    [Test]
    public void Translate_UnknownException_ReturnsFallbackMessage()
    {
        var ex = new InvalidOperationException("Something went wrong");
        var result = DatabaseErrorTranslator.Translate(ex);
        result.Should().Be("Something went wrong");
    }

    // -- Helper --

    /// <summary>
    /// Creates a mock exception with a specific type name and message.
    /// Uses a custom exception class whose name includes the database provider keyword.
    /// </summary>
    private static Exception CreateException(string typeName, string message)
    {
        // We use naming convention to match — the translator checks GetType().Name
        return typeName switch
        {
            "NpgsqlException" => new FakeNpgsqlException(message),
            "PostgresException" => new FakePostgresException(message),
            "MySqlException" => new FakeMySqlException(message),
            "SqliteException" => new FakeSqliteException(message),
            _ => new Exception(message)
        };
    }

    // Fake exceptions whose type names contain the keywords the translator looks for.
    private class FakeNpgsqlException(string message) : Exception(message);
    private class FakePostgresException(string message) : Exception(message);
    private class FakeMySqlException(string message) : Exception(message);
    private class FakeSqliteException(string message) : Exception(message);
}

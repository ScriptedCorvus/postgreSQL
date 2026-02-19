using System.Text.RegularExpressions;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Translates database provider exceptions into user-friendly messages.
/// Maps error codes and exception types to localized, actionable texts.
/// </summary>
public static partial class DatabaseErrorTranslator
{
    /// <summary>
    /// Translates a database exception into a user-friendly message.
    /// Returns the friendly message or null if no translation is available.
    /// </summary>
    public static string Translate(Exception ex)
    {
        var message = ex.Message;
        var typeName = ex.GetType().Name;

        // PostgreSQL (Npgsql) errors
        if (typeName.Contains("Npgsql") || typeName.Contains("PostgresException"))
        {
            return TranslatePostgreSql(ex);
        }

        // MySQL/MariaDB errors
        if (typeName.Contains("MySql"))
        {
            return TranslateMySql(ex);
        }

        // SQLite errors
        if (typeName.Contains("Sqlite") || typeName.Contains("SQLite"))
        {
            return TranslateSqlite(ex);
        }

        // Generic connection errors
        if (ex is System.Net.Sockets.SocketException socketEx)
        {
            return socketEx.SocketErrorCode switch
            {
                System.Net.Sockets.SocketError.ConnectionRefused =>
                    "Connection refused. Verify the server is running and the host/port are correct.",
                System.Net.Sockets.SocketError.HostNotFound =>
                    "Host not found. Check the hostname or IP address.",
                System.Net.Sockets.SocketError.TimedOut =>
                    "Connection timed out. The server may be unreachable or a firewall is blocking the connection.",
                _ => $"Network error: {socketEx.Message}"
            };
        }

        if (ex is TimeoutException)
        {
            return "The operation timed out. Try increasing the timeout value in Settings.";
        }

        if (ex is OperationCanceledException)
        {
            return "The operation was cancelled.";
        }

        if (ex is UnauthorizedAccessException)
        {
            return "Access denied. Check file permissions.";
        }

        if (ex is System.IO.IOException ioEx)
        {
            return $"I/O error: {ioEx.Message}";
        }

        // Fallback
        return message;
    }

    private static string TranslatePostgreSql(Exception ex)
    {
        var msg = ex.Message;

        // Extract SQL state code if present (format: XX000)
        var codeMatch = SqlStateRegex().Match(msg);
        var sqlState = codeMatch.Success ? codeMatch.Groups[1].Value : "";

        return sqlState switch
        {
            "28P01" or "28000" => "Authentication failed. Check your username and password.",
            "3D000" => "Database does not exist. Verify the database name.",
            "42P01" => "Table or view not found. Check the table name and schema.",
            "42601" => "SQL syntax error. Review your query for typos.",
            "42501" => "Insufficient privileges. Contact your database administrator.",
            "42703" => "Column not found. Verify the column name exists in the table.",
            "23505" => "Duplicate key violation. A record with this key already exists.",
            "23503" => "Foreign key violation. The referenced record does not exist.",
            "23502" => "NOT NULL constraint violation. A required field is missing.",
            "23514" => "Check constraint violation. The value does not meet the constraint.",
            "57014" => "Query cancelled by user.",
            "57P01" => "The server is shutting down.",
            "53300" => "Too many connections. Try closing unused connections.",
            "08001" or "08003" or "08006" => "Connection lost. The server may be down or unreachable.",
            _ => msg
        };
    }

    private static string TranslateMySql(Exception ex)
    {
        var msg = ex.Message;

        // MySQL error numbers
        if (msg.Contains("Access denied")) return "Authentication failed. Check your username and password.";
        if (msg.Contains("Unknown database")) return "Database does not exist. Verify the database name.";
        if (msg.Contains("doesn't exist")) return "Table or object not found. Check the name.";
        if (msg.Contains("Duplicate entry")) return "Duplicate key violation. A record with this key already exists.";
        if (msg.Contains("Cannot add or update a child row")) return "Foreign key violation. The referenced record does not exist.";
        if (msg.Contains("Column cannot be null")) return "NOT NULL constraint violation. A required field is missing.";
        if (msg.Contains("syntax error") || msg.Contains("SQL syntax")) return "SQL syntax error. Review your query for typos.";
        if (msg.Contains("Too many connections")) return "Too many connections. Try closing unused connections.";
        if (msg.Contains("Lost connection") || msg.Contains("gone away")) return "Connection lost. The server may be down or the connection timed out.";
        if (msg.Contains("Lock wait timeout")) return "Lock wait timeout. Another transaction is blocking this operation.";

        return msg;
    }

    private static string TranslateSqlite(Exception ex)
    {
        var msg = ex.Message;

        if (msg.Contains("no such table")) return "Table not found. Check the table name.";
        if (msg.Contains("no such column")) return "Column not found. Verify the column name.";
        if (msg.Contains("UNIQUE constraint failed")) return "Duplicate key violation. A record with this key already exists.";
        if (msg.Contains("FOREIGN KEY constraint failed")) return "Foreign key violation. The referenced record does not exist.";
        if (msg.Contains("NOT NULL constraint failed")) return "NOT NULL constraint violation. A required field is missing.";
        if (msg.Contains("near") && msg.Contains("syntax error")) return "SQL syntax error. Review your query for typos.";
        if (msg.Contains("database is locked")) return "Database is locked. Another process may be using the file.";
        if (msg.Contains("unable to open database")) return "Cannot open database file. Check the file path and permissions.";
        if (msg.Contains("disk I/O error")) return "Disk I/O error. Check disk space and file system permissions.";

        return msg;
    }

    [GeneratedRegex(@"(?:SqlState|SQLSTATE)\s*[:=]?\s*([A-Z0-9]{5})")]
    private static partial Regex SqlStateRegex();
}

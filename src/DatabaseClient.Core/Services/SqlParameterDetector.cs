using System.Text.RegularExpressions;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Detects SQL parameters in a query string. Supports @param (PostgreSQL/SQLite)
/// and :param (Oracle-style) syntaxes, ignoring parameters inside strings and comments.
/// </summary>
public static partial class SqlParameterDetector
{
    /// <summary>
    /// Finds all parameter names in the SQL text.
    /// Returns distinct parameter names without the prefix (@ or :).
    /// </summary>
    public static IReadOnlyList<SqlParameterInfo> DetectParameters(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return [];

        var parameters = new Dictionary<string, SqlParameterInfo>(StringComparer.OrdinalIgnoreCase);
        var cleanedSql = RemoveStringsAndComments(sql);

        // Match @param or :param patterns (not :: which is PostgreSQL cast operator)
        foreach (Match match in ParameterRegex().Matches(cleanedSql))
        {
            var prefix = match.Groups[1].Value;
            var name = match.Groups[2].Value;

            // Skip :: cast operator (e.g., value::int)
            if (prefix == ":" && match.Index > 0 && cleanedSql[match.Index - 1] == ':')
                continue;

            if (!parameters.ContainsKey(name))
            {
                parameters[name] = new SqlParameterInfo
                {
                    Name = name,
                    Prefix = prefix,
                    InferredType = InferType(name)
                };
            }
        }

        return parameters.Values.ToList();
    }

    /// <summary>
    /// Returns true if the SQL contains any parameters.
    /// </summary>
    public static bool HasParameters(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;
        var cleanedSql = RemoveStringsAndComments(sql);
        return ParameterRegex().IsMatch(cleanedSql);
    }

    /// <summary>
    /// Removes string literals and comments from SQL to avoid false positives.
    /// </summary>
    private static string RemoveStringsAndComments(string sql)
    {
        return CleanupRegex().Replace(sql, match =>
        {
            // Replace with spaces of equal length to preserve positions
            return new string(' ', match.Length);
        });
    }

    /// <summary>
    /// Infers a likely data type based on the parameter name.
    /// </summary>
    private static SqlParameterType InferType(string name)
    {
        var lower = name.ToLowerInvariant();

        if (lower.Contains("id") || lower.Contains("count") || lower.Contains("num") ||
            lower.Contains("qty") || lower.Contains("quantity") || lower.Contains("year") ||
            lower.Contains("month") || lower.Contains("day") || lower.Contains("age"))
            return SqlParameterType.Integer;

        if (lower.Contains("price") || lower.Contains("amount") || lower.Contains("total") ||
            lower.Contains("rate") || lower.Contains("cost") || lower.Contains("salary") ||
            lower.Contains("balance"))
            return SqlParameterType.Decimal;

        if (lower.Contains("date") || lower.Contains("created") || lower.Contains("updated") ||
            lower.Contains("time") || lower.Contains("timestamp") || lower.Contains("born"))
            return SqlParameterType.DateTime;

        if (lower.Contains("active") || lower.Contains("enabled") || lower.Contains("deleted") ||
            lower.Contains("is_") || lower.Contains("has_") || lower.Contains("flag"))
            return SqlParameterType.Boolean;

        return SqlParameterType.String;
    }

    // Matches @param or :param (word characters after @ or :)
    [GeneratedRegex(@"([@:])(\w+)", RegexOptions.Compiled)]
    private static partial Regex ParameterRegex();

    // Matches strings ('...'), double-quoted identifiers ("..."), line comments (--...), block comments (/*...*/)
    [GeneratedRegex(@"'(?:[^']|'')*'|""(?:[^""]|"""")*""|--[^\r\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled)]
    private static partial Regex CleanupRegex();
}

/// <summary>
/// Information about a detected SQL parameter.
/// </summary>
public class SqlParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = "@";
    public SqlParameterType InferredType { get; set; } = SqlParameterType.String;
    public string? Value { get; set; }
}

/// <summary>
/// Supported parameter data types for the parameter dialog.
/// </summary>
public enum SqlParameterType
{
    String,
    Integer,
    Decimal,
    Boolean,
    DateTime,
    Null
}

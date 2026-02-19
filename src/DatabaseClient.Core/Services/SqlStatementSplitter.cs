using System.Text;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Parses SQL scripts into individual statements, respecting string literals,
/// comments, and block structures (BEGIN...END, $$ delimiters for PostgreSQL).
/// </summary>
public static class SqlStatementSplitter
{
    /// <summary>
    /// Splits a SQL script into individual statements separated by semicolons.
    /// Respects single-quoted strings, double-quoted identifiers, line comments (--),
    /// block comments (/* */), PostgreSQL dollar-quoted strings ($$..$$), and
    /// MySQL DELIMITER directives.
    /// </summary>
    public static IReadOnlyList<string> Split(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            return [];

        var statements = new List<string>();
        var current = new StringBuilder();
        var delimiter = ";";
        int i = 0;
        int len = script.Length;

        while (i < len)
        {
            // -- Line comment
            if (i + 1 < len && script[i] == '-' && script[i + 1] == '-')
            {
                var end = script.IndexOf('\n', i);
                if (end < 0) end = len;
                current.Append(script, i, end - i);
                i = end;
                continue;
            }

            // /* Block comment */
            if (i + 1 < len && script[i] == '/' && script[i + 1] == '*')
            {
                var end = script.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) end = len - 2;
                current.Append(script, i, end + 2 - i);
                i = end + 2;
                continue;
            }

            // Single-quoted string literal
            if (script[i] == '\'')
            {
                current.Append(script[i]);
                i++;
                while (i < len)
                {
                    if (script[i] == '\'' && i + 1 < len && script[i + 1] == '\'')
                    {
                        // Escaped quote ''
                        current.Append("''");
                        i += 2;
                    }
                    else if (script[i] == '\'')
                    {
                        current.Append(script[i]);
                        i++;
                        break;
                    }
                    else
                    {
                        current.Append(script[i]);
                        i++;
                    }
                }
                continue;
            }

            // Double-quoted identifier
            if (script[i] == '"')
            {
                current.Append(script[i]);
                i++;
                while (i < len)
                {
                    if (script[i] == '"' && i + 1 < len && script[i + 1] == '"')
                    {
                        current.Append("\"\"");
                        i += 2;
                    }
                    else if (script[i] == '"')
                    {
                        current.Append(script[i]);
                        i++;
                        break;
                    }
                    else
                    {
                        current.Append(script[i]);
                        i++;
                    }
                }
                continue;
            }

            // PostgreSQL dollar-quoted string: $tag$...$tag$
            if (script[i] == '$')
            {
                var dollarTag = ReadDollarTag(script, i);
                if (dollarTag is not null)
                {
                    current.Append(dollarTag);
                    i += dollarTag.Length;

                    // Find closing dollar tag
                    var closeIdx = script.IndexOf(dollarTag, i, StringComparison.Ordinal);
                    if (closeIdx < 0)
                    {
                        // No closing tag, take rest of script
                        current.Append(script, i, len - i);
                        i = len;
                    }
                    else
                    {
                        current.Append(script, i, closeIdx + dollarTag.Length - i);
                        i = closeIdx + dollarTag.Length;
                    }
                    continue;
                }
            }

            // MySQL DELIMITER directive
            if (MatchesWord(script, i, "DELIMITER"))
            {
                int afterKeyword = i + "DELIMITER".Length;
                // Skip whitespace
                while (afterKeyword < len && script[afterKeyword] is ' ' or '\t')
                    afterKeyword++;

                // Read new delimiter until end of line
                var endOfLine = script.IndexOf('\n', afterKeyword);
                if (endOfLine < 0) endOfLine = len;

                delimiter = script[afterKeyword..endOfLine].Trim();
                if (string.IsNullOrEmpty(delimiter)) delimiter = ";";

                i = endOfLine;
                continue;
            }

            // Check for delimiter match
            if (StartsWithAt(script, i, delimiter))
            {
                var stmt = current.ToString().Trim();
                if (!string.IsNullOrEmpty(stmt))
                    statements.Add(stmt);
                current.Clear();
                i += delimiter.Length;
                continue;
            }

            current.Append(script[i]);
            i++;
        }

        // Add last statement if any
        var last = current.ToString().Trim();
        if (!string.IsNullOrEmpty(last))
            statements.Add(last);

        return statements;
    }

    /// <summary>
    /// Gets the statement at the cursor position (0-based offset in the script).
    /// </summary>
    public static string? GetStatementAtCursor(string script, int cursorOffset)
    {
        var statements = SplitWithPositions(script);
        foreach (var (stmt, start, end) in statements)
        {
            if (cursorOffset >= start && cursorOffset <= end)
                return stmt;
        }
        return null;
    }

    private static List<(string Statement, int Start, int End)> SplitWithPositions(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            return [];

        var results = new List<(string, int, int)>();
        var current = new StringBuilder();
        int stmtStart = 0;
        int i = 0;
        int len = script.Length;

        while (i < len)
        {
            // Skip whitespace to find statement start
            if (current.Length == 0)
            {
                while (i < len && char.IsWhiteSpace(script[i])) i++;
                stmtStart = i;
                if (i >= len) break;
            }

            // -- Line comment
            if (i + 1 < len && script[i] == '-' && script[i + 1] == '-')
            {
                var end = script.IndexOf('\n', i);
                if (end < 0) end = len;
                current.Append(script, i, end - i);
                i = end;
                continue;
            }

            // /* Block comment */
            if (i + 1 < len && script[i] == '/' && script[i + 1] == '*')
            {
                var end = script.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) end = len - 2;
                current.Append(script, i, end + 2 - i);
                i = end + 2;
                continue;
            }

            // String literals
            if (script[i] == '\'' || script[i] == '"')
            {
                char quote = script[i];
                current.Append(script[i]);
                i++;
                while (i < len)
                {
                    if (script[i] == quote && i + 1 < len && script[i + 1] == quote)
                    {
                        current.Append(script[i]);
                        current.Append(script[i + 1]);
                        i += 2;
                    }
                    else if (script[i] == quote)
                    {
                        current.Append(script[i]);
                        i++;
                        break;
                    }
                    else
                    {
                        current.Append(script[i]);
                        i++;
                    }
                }
                continue;
            }

            // Semicolon delimiter
            if (script[i] == ';')
            {
                var stmt = current.ToString().Trim();
                if (!string.IsNullOrEmpty(stmt))
                    results.Add((stmt, stmtStart, i));
                current.Clear();
                i++;
                continue;
            }

            current.Append(script[i]);
            i++;
        }

        var last = current.ToString().Trim();
        if (!string.IsNullOrEmpty(last))
            results.Add((last, stmtStart, len - 1));

        return results;
    }

    /// <summary>
    /// Reads a dollar-quoting tag like $$ or $tag$ starting at position i.
    /// Returns the tag string (e.g. "$$" or "$body$") or null if not a valid dollar tag.
    /// </summary>
    private static string? ReadDollarTag(string script, int i)
    {
        if (i >= script.Length || script[i] != '$') return null;

        int start = i;
        i++; // skip first $

        // Tag can be empty ($$) or contain [a-zA-Z0-9_]
        while (i < script.Length && (char.IsLetterOrDigit(script[i]) || script[i] == '_'))
            i++;

        if (i < script.Length && script[i] == '$')
        {
            return script[start..(i + 1)];
        }

        return null;
    }

    /// <summary>Checks if a word appears at position i (case-insensitive, word boundary).</summary>
    private static bool MatchesWord(string script, int i, string word)
    {
        if (i + word.Length > script.Length) return false;

        // Must be at start of line or preceded by whitespace
        if (i > 0 && !char.IsWhiteSpace(script[i - 1])) return false;

        for (int j = 0; j < word.Length; j++)
        {
            if (char.ToUpperInvariant(script[i + j]) != char.ToUpperInvariant(word[j]))
                return false;
        }

        // Must be followed by whitespace or end
        int after = i + word.Length;
        return after >= script.Length || char.IsWhiteSpace(script[after]);
    }

    /// <summary>Checks if the script starts with the given string at position i.</summary>
    private static bool StartsWithAt(string script, int i, string s)
    {
        if (i + s.Length > script.Length) return false;
        return script.AsSpan(i, s.Length).SequenceEqual(s.AsSpan());
    }
}

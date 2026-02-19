using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace DatabaseClient.App.Services;

/// <summary>
/// Provides SQL autocompletion data for AvalonEdit.
/// Supports keywords, table names, column names (context-aware), views, and functions.
/// </summary>
public class SqlCompletionProvider
{
    private SchemaCache? _schemaCache;

    /// <summary>Sets the schema cache to use for autocomplete.</summary>
    public void SetSchemaCache(SchemaCache cache) => _schemaCache = cache;

    /// <summary>
    /// Builds a list of completion items based on the current text and caret position.
    /// </summary>
    public IList<ICompletionData> GetCompletions(TextDocument document, int offset)
    {
        var results = new List<ICompletionData>();
        var wordStart = FindWordStart(document, offset);
        var prefix = document.GetText(wordStart, offset - wordStart).ToUpperInvariant();
        var textBefore = GetTextBefore(document, wordStart, 500);

        // Check if we're after a dot (table.column completion)
        if (wordStart > 0 && document.GetCharAt(wordStart - 1) == '.')
        {
            var aliasStart = FindWordStart(document, wordStart - 1);
            var tableName = document.GetText(aliasStart, wordStart - 1 - aliasStart);

            // Try to find columns for this table
            if (_schemaCache is not null)
            {
                var columns = _schemaCache.GetColumns(tableName);
                foreach (var col in columns)
                {
                    results.Add(new SqlCompletionData(col, "Column", CompletionKind.Column));
                }
            }

            return results;
        }

        // Context-aware suggestions based on last SQL keyword
        var context = DetectContext(textBefore);

        switch (context)
        {
            case SqlContext.From:
            case SqlContext.Join:
                // Suggest tables and views
                if (_schemaCache is not null)
                {
                    foreach (var t in _schemaCache.TableNames)
                        results.Add(new SqlCompletionData(t, "Table", CompletionKind.Table));
                    foreach (var v in _schemaCache.ViewNames)
                        results.Add(new SqlCompletionData(v, "View", CompletionKind.View));
                }
                break;

            case SqlContext.Select:
            case SqlContext.Where:
            case SqlContext.OrderBy:
            case SqlContext.GroupBy:
                // Suggest columns from all known tables + tables/views + keywords
                if (_schemaCache is not null)
                {
                    foreach (var t in _schemaCache.TableNames)
                    {
                        results.Add(new SqlCompletionData(t, "Table", CompletionKind.Table));
                        foreach (var col in _schemaCache.GetColumns(t))
                            results.Add(new SqlCompletionData(col, $"Column ({t})", CompletionKind.Column));
                    }
                }
                break;
        }

        // Always add SQL keywords
        foreach (var kw in SchemaCache.SqlKeywords)
        {
            if (string.IsNullOrEmpty(prefix) || kw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                results.Add(new SqlCompletionData(kw, "Keyword", CompletionKind.Keyword));
        }

        // Add schema objects if not already added by context
        if (context == SqlContext.General && _schemaCache is not null)
        {
            foreach (var t in _schemaCache.TableNames)
                results.Add(new SqlCompletionData(t, "Table", CompletionKind.Table));
            foreach (var v in _schemaCache.ViewNames)
                results.Add(new SqlCompletionData(v, "View", CompletionKind.View));
            foreach (var f in _schemaCache.FunctionNames)
                results.Add(new SqlCompletionData(f, "Function", CompletionKind.Function));
        }

        // Deduplicate by text
        return results
            .GroupBy(r => r.Text, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Text)
            .ToList();
    }

    private static int FindWordStart(TextDocument document, int offset)
    {
        while (offset > 0)
        {
            var ch = document.GetCharAt(offset - 1);
            if (char.IsLetterOrDigit(ch) || ch == '_')
                offset--;
            else
                break;
        }
        return offset;
    }

    private static string GetTextBefore(TextDocument document, int offset, int maxChars)
    {
        var start = Math.Max(0, offset - maxChars);
        return document.GetText(start, offset - start);
    }

    private static SqlContext DetectContext(string textBefore)
    {
        // Find the last significant SQL keyword
        var upper = textBefore.ToUpperInvariant().TrimEnd();

        // Check from end backwards for context keywords
        if (EndsWithWord(upper, "FROM") || EndsWithWord(upper, "UPDATE") || EndsWithWord(upper, "INTO") || EndsWithWord(upper, "TABLE"))
            return SqlContext.From;
        if (EndsWithWord(upper, "JOIN"))
            return SqlContext.Join;
        if (EndsWithWord(upper, "SELECT") || EndsWithWord(upper, "DISTINCT"))
            return SqlContext.Select;
        if (EndsWithWord(upper, "WHERE") || EndsWithWord(upper, "AND") || EndsWithWord(upper, "OR") || EndsWithWord(upper, "ON"))
            return SqlContext.Where;
        if (EndsWithWord(upper, "BY") && (upper.Contains("ORDER") || upper.Contains("GROUP")))
            return upper.Contains("ORDER") ? SqlContext.OrderBy : SqlContext.GroupBy;

        return SqlContext.General;
    }

    private static bool EndsWithWord(string text, string word)
    {
        if (!text.EndsWith(word, StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Length == word.Length) return true;
        var before = text[text.Length - word.Length - 1];
        return !char.IsLetterOrDigit(before) && before != '_';
    }
}

public enum SqlContext
{
    General,
    Select,
    From,
    Join,
    Where,
    OrderBy,
    GroupBy
}

public enum CompletionKind
{
    Keyword,
    Table,
    View,
    Column,
    Function,
    Schema
}

/// <summary>
/// Represents a single autocompletion item in the AvalonEdit completion window.
/// </summary>
public class SqlCompletionData : ICompletionData
{
    private readonly string _descriptionText;

    public SqlCompletionData(string text, string descriptionText, CompletionKind kind)
    {
        Text = text;
        _descriptionText = descriptionText;
        Kind = kind;
        Priority = kind switch
        {
            CompletionKind.Column => 1.0,
            CompletionKind.Table => 2.0,
            CompletionKind.View => 3.0,
            CompletionKind.Function => 4.0,
            CompletionKind.Keyword => 5.0,
            _ => 10.0
        };
    }

    public string Text { get; }
    public CompletionKind Kind { get; }
    public double Priority { get; }

    public System.Windows.Media.ImageSource? Image => null;

    object ICompletionData.Content => Text;
    object ICompletionData.Description => $"{Kind}: {_descriptionText}";

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}

using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Reads data from various file formats (CSV, JSON, XML) for import.
/// </summary>
public static class DataFileReader
{
    /// <summary>Reads a CSV file into a DataTable.</summary>
    public static DataTable ReadCsv(string filePath, char delimiter = ',', bool hasHeader = true, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var dt = new DataTable();
        var lines = File.ReadAllLines(filePath, encoding);

        if (lines.Length == 0) return dt;

        int startRow = 0;
        var firstLine = ParseCsvLine(lines[0], delimiter);

        if (hasHeader)
        {
            foreach (var col in firstLine)
                dt.Columns.Add(col.Trim());
            startRow = 1;
        }
        else
        {
            for (int i = 0; i < firstLine.Length; i++)
                dt.Columns.Add($"Column{i + 1}");
        }

        for (int i = startRow; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var values = ParseCsvLine(lines[i], delimiter);
            var row = dt.NewRow();
            for (int j = 0; j < Math.Min(values.Length, dt.Columns.Count); j++)
                row[j] = string.IsNullOrEmpty(values[j]) ? DBNull.Value : values[j].Trim();
            dt.Rows.Add(row);
        }

        return dt;
    }

    /// <summary>Reads a JSON array file into a DataTable.</summary>
    public static DataTable ReadJson(string filePath)
    {
        var dt = new DataTable();
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array) return dt;

        var items = doc.RootElement.EnumerateArray().ToList();
        if (items.Count == 0) return dt;

        // Create columns from first object
        foreach (var prop in items[0].EnumerateObject())
            dt.Columns.Add(prop.Name);

        foreach (var item in items)
        {
            var row = dt.NewRow();
            foreach (var prop in item.EnumerateObject())
            {
                if (dt.Columns.Contains(prop.Name))
                {
                    row[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Null => DBNull.Value,
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => prop.Value.ToString()
                    };
                }
            }
            dt.Rows.Add(row);
        }

        return dt;
    }

    /// <summary>Reads an XML file into a DataTable.</summary>
    public static DataTable ReadXml(string filePath)
    {
        var ds = new DataSet();
        ds.ReadXml(filePath);
        return ds.Tables.Count > 0 ? ds.Tables[0]! : new DataTable();
    }

    /// <summary>Reads an Excel (.xlsx) file into a DataTable using ClosedXML.</summary>
    public static DataTable ReadExcel(string filePath, bool hasHeader = true, string? sheetName = null)
    {
        var dt = new DataTable();
        using var workbook = new XLWorkbook(filePath);
        var worksheet = sheetName is not null
            ? workbook.Worksheet(sheetName)
            : workbook.Worksheets.First();

        var range = worksheet.RangeUsed();
        if (range is null) return dt;

        int startRow = range.FirstRow().RowNumber();
        int endRow = range.LastRow().RowNumber();
        int startCol = range.FirstColumn().ColumnNumber();
        int endCol = range.LastColumn().ColumnNumber();

        if (hasHeader)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                var headerValue = worksheet.Cell(startRow, c).GetString().Trim();
                dt.Columns.Add(string.IsNullOrEmpty(headerValue) ? $"Column{c}" : headerValue);
            }
            startRow++;
        }
        else
        {
            for (int c = startCol; c <= endCol; c++)
                dt.Columns.Add($"Column{c}");
        }

        for (int r = startRow; r <= endRow; r++)
        {
            var row = dt.NewRow();
            for (int c = startCol; c <= endCol; c++)
            {
                var cell = worksheet.Cell(r, c);
                var colIdx = c - range.FirstColumn().ColumnNumber();
                if (colIdx < dt.Columns.Count)
                {
                    row[colIdx] = cell.IsEmpty() ? DBNull.Value : cell.GetString();
                }
            }
            dt.Rows.Add(row);
        }

        return dt;
    }

    /// <summary>Gets preview rows (first N rows) from a file.</summary>
    public static DataTable GetPreview(string filePath, string format, int maxRows = 20, char csvDelimiter = ',', bool hasHeader = true)
    {
        var dt = format.ToLowerInvariant() switch
        {
            "csv" => ReadCsv(filePath, csvDelimiter, hasHeader),
            "json" => ReadJson(filePath),
            "xml" => ReadXml(filePath),
            "excel" => ReadExcel(filePath, hasHeader),
            _ => new DataTable()
        };

        // Trim to preview count
        while (dt.Rows.Count > maxRows)
            dt.Rows.RemoveAt(dt.Rows.Count - 1);

        return dt;
    }

    private static string[] ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return [.. result];
    }
}

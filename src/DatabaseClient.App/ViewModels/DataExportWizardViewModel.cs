using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClosedXML.Excel;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the data export wizard. Exports table/query data to CSV, Excel, JSON, XML, SQL, Markdown, HTML.
/// </summary>
public partial class DataExportWizardViewModel : ObservableObject
{
    private readonly IConnectionManager _connectionManager;
    private ConnectionInfo? _connectionInfo;
    private string? _database;
    private IDatabaseProvider? _provider;

    #region Step tracking

    [ObservableProperty]
    private int _currentStep = 1;

    public const int TotalSteps = 3;

    public bool CanGoBack => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < TotalSteps;
    public bool IsLastStep => CurrentStep == TotalSteps;

    #endregion

    #region Step 1 - Source Selection

    [ObservableProperty]
    private string _sourceType = "Table";

    [ObservableProperty]
    private string _selectedTable = string.Empty;

    [ObservableProperty]
    private string _customQuery = string.Empty;

    [ObservableProperty]
    private string _whereClause = string.Empty;

    [ObservableProperty]
    private DataTable? _previewData;

    [ObservableProperty]
    private string _previewStatus = string.Empty;

    [ObservableProperty]
    private long _totalRows;

    public ObservableCollection<string> AvailableTables { get; } = [];

    public static IReadOnlyList<string> SourceTypes { get; } = ["Table", "Custom Query"];

    #endregion

    #region Step 2 - Format & Column Selection

    [ObservableProperty]
    private string _selectedFormat = "CSV";

    [ObservableProperty]
    private char _csvDelimiter = ',';

    [ObservableProperty]
    private bool _includeHeader = true;

    [ObservableProperty]
    private string _encoding = "UTF-8";

    [ObservableProperty]
    private string _nullRepresentation = "";

    public ObservableCollection<ExportColumnItem> Columns { get; } = [];

    public static IReadOnlyList<string> ExportFormats { get; } =
        ["CSV", "Excel (.xlsx)", "JSON", "XML", "SQL (INSERT)", "Markdown", "HTML"];

    public static IReadOnlyList<char> CommonDelimiters { get; } = [',', ';', '\t', '|'];
    public static IReadOnlyList<string> Encodings { get; } = ["UTF-8", "ASCII", "ISO-8859-1", "UTF-16"];

    #endregion

    #region Step 3 - Export Execution

    [ObservableProperty]
    private string _outputFilePath = string.Empty;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private long _exportedRows;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _exportStatus = string.Empty;

    [ObservableProperty]
    private bool _exportComplete;

    public ObservableCollection<string> ExportLog { get; } = [];

    #endregion

    public DataExportWizardViewModel(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    /// <summary>
    /// Initialize the wizard with connection context and optionally a pre-selected table.
    /// </summary>
    public async Task SetConnectionAsync(ConnectionInfo connectionInfo, string database, string? tableName = null)
    {
        _connectionInfo = connectionInfo;
        _database = database;

        try
        {
            _provider = await _connectionManager.GetProviderAsync(connectionInfo);
            await _provider.ChangeDatabaseAsync(database);

            var tables = await _provider.GetTablesAsync(database);
            AvailableTables.Clear();
            foreach (var t in tables)
                AvailableTables.Add(t.Name);
        }
        catch { /* Continue without table list */ }

        if (!string.IsNullOrEmpty(tableName))
        {
            SelectedTable = tableName;
            SourceType = "Table";
        }
    }

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < TotalSteps)
        {
            CurrentStep++;
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(IsLastStep));
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(IsLastStep));
        }
    }

    [RelayCommand]
    private async Task LoadPreviewAsync()
    {
        if (_provider is null || _connectionInfo is null) return;

        try
        {
            PreviewStatus = "Loading preview...";
            var query = BuildQuery(limit: 50);

            var result = await _provider.ExecuteQueryAsync(query);
            PreviewData = result.ResultSet;

            // Load columns for step 2
            if (result.ResultSet is not null)
            {
                Columns.Clear();
                foreach (DataColumn col in result.ResultSet.Columns)
                    Columns.Add(new ExportColumnItem { ColumnName = col.ColumnName, Include = true });
            }

            // Count total rows
            var countQuery = BuildCountQuery();
            var countResult = await _provider.ExecuteScalarAsync(countQuery);
            TotalRows = Convert.ToInt64(countResult ?? 0);

            PreviewStatus = $"Preview: {result.ResultSet?.Rows.Count ?? 0} rows shown. Total: {TotalRows:N0} rows.";
        }
        catch (Exception ex)
        {
            PreviewStatus = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var col in Columns) col.Include = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var col in Columns) col.Include = false;
    }

    [RelayCommand]
    private async Task StartExportAsync()
    {
        if (_provider is null || string.IsNullOrEmpty(OutputFilePath)) return;

        IsExporting = true;
        ExportComplete = false;
        ExportedRows = 0;
        ProgressPercent = 0;
        ExportLog.Clear();
        ExportStatus = "Starting export...";

        try
        {
            var selectedColumns = Columns.Where(c => c.Include).Select(c => c.ColumnName).ToList();
            if (selectedColumns.Count == 0)
            {
                ExportStatus = "Error: No columns selected for export.";
                return;
            }

            var query = BuildQuery(selectedColumns: selectedColumns);
            ExportLog.Add($"Query: {query}");

            ExportStatus = "Fetching data...";
            var result = await _provider.ExecuteQueryAsync(query);
            var dt = result.ResultSet;

            if (dt is null || dt.Rows.Count == 0)
            {
                ExportStatus = "No data to export.";
                return;
            }

            ExportLog.Add($"Loaded {dt.Rows.Count:N0} rows.");

            var enc = GetEncoding();

            await Task.Run(() =>
            {
                if (SelectedFormat == "CSV")
                    ExportToCsv(dt, OutputFilePath, enc);
                else if (SelectedFormat == "Excel (.xlsx)")
                    ExportToExcel(dt, OutputFilePath);
                else if (SelectedFormat == "JSON")
                    ExportToJson(dt, OutputFilePath, enc);
                else if (SelectedFormat == "XML")
                    ExportToXml(dt, OutputFilePath, enc);
                else if (SelectedFormat == "SQL (INSERT)")
                    ExportToSql(dt, OutputFilePath, enc);
                else if (SelectedFormat == "Markdown")
                    ExportToMarkdown(dt, OutputFilePath, enc);
                else if (SelectedFormat == "HTML")
                    ExportToHtml(dt, OutputFilePath, enc);
            });

            ExportedRows = dt.Rows.Count;
            ProgressPercent = 100;
            ExportComplete = true;
            ExportStatus = $"Export complete! {ExportedRows:N0} rows exported to {Path.GetFileName(OutputFilePath)}";
            ExportLog.Add(ExportStatus);
        }
        catch (Exception ex)
        {
            ExportStatus = $"Export failed: {ex.Message}";
            ExportLog.Add($"ERROR: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
        }
    }

    #region Query builders

    private string BuildQuery(int? limit = null, List<string>? selectedColumns = null)
    {
        var cols = selectedColumns is not null && selectedColumns.Count > 0
            ? string.Join(", ", selectedColumns.Select(QuoteId))
            : "*";

        string source;
        if (SourceType == "Custom Query")
            source = $"({CustomQuery.TrimEnd(';')}) AS export_subquery";
        else
            source = QuoteId(SelectedTable);

        var sb = new StringBuilder($"SELECT {cols} FROM {source}");

        if (SourceType == "Table" && !string.IsNullOrWhiteSpace(WhereClause))
            sb.Append($" WHERE {WhereClause}");

        if (limit.HasValue)
            sb.Append($" LIMIT {limit.Value}");

        return sb.ToString();
    }

    private string BuildCountQuery()
    {
        string source;
        if (SourceType == "Custom Query")
            source = $"({CustomQuery.TrimEnd(';')}) AS count_subquery";
        else
            source = QuoteId(SelectedTable);

        var sb = new StringBuilder($"SELECT COUNT(*) FROM {source}");

        if (SourceType == "Table" && !string.IsNullOrWhiteSpace(WhereClause))
            sb.Append($" WHERE {WhereClause}");

        return sb.ToString();
    }

    private string QuoteId(string identifier)
    {
        if (_connectionInfo?.DatabaseType is DatabaseType.MySQL or DatabaseType.MariaDB)
            return $"`{identifier}`";
        return $"\"{identifier}\"";
    }

    #endregion

    #region Export methods

    private void ExportToCsv(DataTable dt, string path, Encoding enc)
    {
        using var writer = new StreamWriter(path, false, enc);

        if (IncludeHeader)
        {
            var headers = dt.Columns.Cast<DataColumn>().Select(c => EscapeCsv(c.ColumnName));
            writer.WriteLine(string.Join(CsvDelimiter, headers));
        }

        int count = 0;
        foreach (DataRow row in dt.Rows)
        {
            var values = dt.Columns.Cast<DataColumn>().Select(c =>
            {
                var val = row[c];
                return val is null || val == DBNull.Value
                    ? NullRepresentation
                    : EscapeCsv(val.ToString() ?? "");
            });
            writer.WriteLine(string.Join(CsvDelimiter, values));
            count++;
            if (count % 1000 == 0)
                UpdateProgress(count, dt.Rows.Count);
        }
        UpdateProgress(dt.Rows.Count, dt.Rows.Count);
    }

    private string EscapeCsv(string field)
    {
        var delim = CsvDelimiter.ToString();
        if (field.Contains(delim) || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    private void ExportToExcel(DataTable dt, string path)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Export");

        for (int c = 0; c < dt.Columns.Count; c++)
            ws.Cell(1, c + 1).Value = dt.Columns[c].ColumnName;

        for (int r = 0; r < dt.Rows.Count; r++)
        {
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                var val = dt.Rows[r][c];
                if (val is not null && val != DBNull.Value)
                {
                    var cell = ws.Cell(r + 2, c + 1);
                    if (val is int i) cell.Value = i;
                    else if (val is long l) cell.Value = l;
                    else if (val is decimal d) cell.Value = d;
                    else if (val is double dbl) cell.Value = dbl;
                    else if (val is float f) cell.Value = f;
                    else if (val is DateTime dtVal) cell.Value = dtVal;
                    else if (val is bool b) cell.Value = b;
                    else cell.Value = val.ToString();
                }
            }
            if ((r + 1) % 1000 == 0)
                UpdateProgress(r + 1, dt.Rows.Count);
        }

        ws.Columns().AdjustToContents(1, 50);
        ws.Row(1).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);

        workbook.SaveAs(path);
        UpdateProgress(dt.Rows.Count, dt.Rows.Count);
    }

    private void ExportToJson(DataTable dt, string path, Encoding enc)
    {
        using var writer = new StreamWriter(path, false, enc);
        writer.WriteLine("[");

        for (int r = 0; r < dt.Rows.Count; r++)
        {
            var row = dt.Rows[r];
            var entries = new List<string>();
            foreach (DataColumn col in dt.Columns)
            {
                var val = row[col];
                string jsonVal;
                if (val is null || val == DBNull.Value)
                    jsonVal = "null";
                else if (val is int or long or short or byte)
                    jsonVal = val.ToString()!;
                else if (val is decimal or double or float)
                    jsonVal = Convert.ToDouble(val).ToString(System.Globalization.CultureInfo.InvariantCulture);
                else if (val is bool b)
                    jsonVal = b ? "true" : "false";
                else
                    jsonVal = $"\"{EscapeJson(val.ToString() ?? "")}\"";

                entries.Add($"    \"{EscapeJson(col.ColumnName)}\": {jsonVal}");
            }

            var comma = r < dt.Rows.Count - 1 ? "," : "";
            writer.WriteLine($"  {{{System.Environment.NewLine}{string.Join($",{System.Environment.NewLine}", entries)}{System.Environment.NewLine}  }}{comma}");

            if ((r + 1) % 1000 == 0)
                UpdateProgress(r + 1, dt.Rows.Count);
        }

        writer.WriteLine("]");
        UpdateProgress(dt.Rows.Count, dt.Rows.Count);
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private void ExportToXml(DataTable dt, string path, Encoding enc)
    {
        using var writer = new StreamWriter(path, false, enc);
        writer.WriteLine("<?xml version=\"1.0\" encoding=\"" + enc.WebName + "\"?>");
        writer.WriteLine("<export>");

        for (int r = 0; r < dt.Rows.Count; r++)
        {
            var row = dt.Rows[r];
            writer.WriteLine("  <row>");
            foreach (DataColumn col in dt.Columns)
            {
                var val = row[col];
                var text = val is null || val == DBNull.Value
                    ? ""
                    : System.Security.SecurityElement.Escape(val.ToString());
                writer.WriteLine($"    <{col.ColumnName}>{text}</{col.ColumnName}>");
            }
            writer.WriteLine("  </row>");

            if ((r + 1) % 1000 == 0)
                UpdateProgress(r + 1, dt.Rows.Count);
        }

        writer.WriteLine("</export>");
        UpdateProgress(dt.Rows.Count, dt.Rows.Count);
    }

    private void ExportToSql(DataTable dt, string path, Encoding enc)
    {
        var tableName = SourceType == "Table" ? QuoteId(SelectedTable) : "exported_data";

        using var writer = new StreamWriter(path, false, enc);
        var columnNames = string.Join(", ",
            dt.Columns.Cast<DataColumn>().Select(c => QuoteId(c.ColumnName)));

        for (int r = 0; r < dt.Rows.Count; r++)
        {
            var row = dt.Rows[r];
            var values = dt.Columns.Cast<DataColumn>().Select(c =>
            {
                var val = row[c];
                if (val is null || val == DBNull.Value) return "NULL";
                if (val is int or long or short or decimal or double or float) return val.ToString()!;
                if (val is bool b) return b ? "TRUE" : "FALSE";
                if (val is DateTime dtVal) return $"'{dtVal:yyyy-MM-dd HH:mm:ss}'";
                return $"'{val.ToString()?.Replace("'", "''")}'";
            });

            writer.WriteLine($"INSERT INTO {tableName} ({columnNames}) VALUES ({string.Join(", ", values)});");

            if ((r + 1) % 1000 == 0)
                UpdateProgress(r + 1, dt.Rows.Count);
        }

        UpdateProgress(dt.Rows.Count, dt.Rows.Count);
    }

    private void ExportToMarkdown(DataTable dt, string path, Encoding enc)
    {
        using var writer = new StreamWriter(path, false, enc);
        var cols = dt.Columns.Cast<DataColumn>().ToList();

        writer.WriteLine("| " + string.Join(" | ", cols.Select(c => c.ColumnName)) + " |");
        writer.WriteLine("| " + string.Join(" | ", cols.Select(_ => "---")) + " |");

        for (int r = 0; r < dt.Rows.Count; r++)
        {
            var row = dt.Rows[r];
            var values = cols.Select(c =>
            {
                var val = row[c];
                return val is null || val == DBNull.Value
                    ? NullRepresentation
                    : (val.ToString()?.Replace("|", "\\|") ?? "");
            });
            writer.WriteLine("| " + string.Join(" | ", values) + " |");

            if ((r + 1) % 1000 == 0)
                UpdateProgress(r + 1, dt.Rows.Count);
        }

        UpdateProgress(dt.Rows.Count, dt.Rows.Count);
    }

    private void ExportToHtml(DataTable dt, string path, Encoding enc)
    {
        using var writer = new StreamWriter(path, false, enc);
        writer.WriteLine("<!DOCTYPE html>");
        writer.WriteLine("<html><head><meta charset=\"" + enc.WebName + "\"/>");
        writer.WriteLine("<style>");
        writer.WriteLine("body { font-family: 'Segoe UI', sans-serif; margin: 20px; }");
        writer.WriteLine("table { border-collapse: collapse; width: 100%; }");
        writer.WriteLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        writer.WriteLine("th { background-color: #4a86c8; color: white; position: sticky; top: 0; }");
        writer.WriteLine("tr:nth-child(even) { background-color: #f2f2f2; }");
        writer.WriteLine("tr:hover { background-color: #ddd; }");
        writer.WriteLine(".null { color: #999; font-style: italic; }");
        writer.WriteLine("</style></head><body>");
        writer.WriteLine($"<h2>Export - {System.Net.WebUtility.HtmlEncode(SourceType == "Table" ? SelectedTable : "Query Results")}</h2>");
        writer.WriteLine($"<p>Total rows: {dt.Rows.Count:N0}</p>");
        writer.WriteLine("<table>");

        writer.Write("<tr>");
        foreach (DataColumn col in dt.Columns)
            writer.Write($"<th>{System.Net.WebUtility.HtmlEncode(col.ColumnName)}</th>");
        writer.WriteLine("</tr>");

        for (int r = 0; r < dt.Rows.Count; r++)
        {
            var row = dt.Rows[r];
            writer.Write("<tr>");
            foreach (DataColumn col in dt.Columns)
            {
                var val = row[col];
                if (val is null || val == DBNull.Value)
                    writer.Write("<td class=\"null\">NULL</td>");
                else
                    writer.Write($"<td>{System.Net.WebUtility.HtmlEncode(val.ToString())}</td>");
            }
            writer.WriteLine("</tr>");

            if ((r + 1) % 1000 == 0)
                UpdateProgress(r + 1, dt.Rows.Count);
        }

        writer.WriteLine("</table></body></html>");
        UpdateProgress(dt.Rows.Count, dt.Rows.Count);
    }

    #endregion

    #region Helpers

    private Encoding GetEncoding() => Encoding switch
    {
        "ASCII" => System.Text.Encoding.ASCII,
        "ISO-8859-1" => System.Text.Encoding.Latin1,
        "UTF-16" => System.Text.Encoding.Unicode,
        _ => System.Text.Encoding.UTF8
    };

    private void UpdateProgress(int current, int total)
    {
        if (total > 0)
            ProgressPercent = (int)((double)current / total * 100);
    }

    #endregion
}

/// <summary>
/// Represents a column that can be included or excluded from export.
/// </summary>
public partial class ExportColumnItem : ObservableObject
{
    [ObservableProperty]
    private string _columnName = string.Empty;

    [ObservableProperty]
    private bool _include = true;
}

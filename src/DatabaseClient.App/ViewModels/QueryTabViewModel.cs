using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.App.Services;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.Services;
using DatabaseClient.Core.ViewModels;
using Microsoft.Win32;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the SQL query editor tab. Handles query execution,
/// cancellation, result display, and export.
/// </summary>
public partial class QueryTabViewModel : TabViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private readonly IQueryHistoryService? _queryHistoryService;
    private ConnectionInfo? _connectionInfo;
    private CancellationTokenSource? _executionCts;

    [ObservableProperty]
    private string _sqlText = string.Empty;

    [ObservableProperty]
    private DataTable? _resultData;

    [ObservableProperty]
    private string _messagesText = string.Empty;

    /// <summary>Structured error entries from the last execution for the error panel.</summary>
    public ObservableCollection<QueryErrorEntry> ErrorEntries { get; } = [];

    [ObservableProperty]
    private bool _hasErrors;

    [ObservableProperty]
    private int _rowCount;

    [ObservableProperty]
    private string _executionTimeText = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

    /// <summary>All result sets from the last execution (for multi-result set queries).</summary>
    public ObservableCollection<DataTable> AllResultSets { get; } = [];

    [ObservableProperty]
    private int _selectedResultSetIndex;

    /// <summary>Query timeout in seconds (0 = no timeout).</summary>
    [ObservableProperty]
    private int _queryTimeoutSeconds;

    /// <summary>Whether to wrap multi-statement scripts in a transaction.</summary>
    [ObservableProperty]
    private bool _useTransaction;

    [ObservableProperty]
    private string _selectedDatabase = string.Empty;

    [ObservableProperty]
    private string _queryPlanText = string.Empty;

    [ObservableProperty]
    private QueryPlan? _queryPlanData;

    /// <summary>
    /// The currently selected text in the SQL editor (set by the view code-behind).
    /// </summary>
    public string SelectedText { get; set; } = string.Empty;

    /// <summary>
    /// Schema cache used for SQL autocompletion. Populated when connection is set.
    /// </summary>
    public SchemaCache SchemaCache { get; } = new();

    /// <summary>Raised when the schema cache has been refreshed.</summary>
    public event EventHandler? SchemaCacheRefreshed;

    /// <summary>
    /// Delegate for showing the parameter input dialog. Set by the View code-behind.
    /// Returns the parameter values dictionary, or null if the user cancelled.
    /// </summary>
    public Func<IReadOnlyList<SqlParameterInfo>, Dictionary<string, object?>?>? ShowParameterDialog { get; set; }

    /// <summary>
    /// Delegate to open a chart tab from query results. Set by MainViewModel.
    /// Parameters: DataTable data, string title.
    /// </summary>
    public Action<DataTable, string>? OpenChartDelegate { get; set; }

    /// <summary>
    /// Saved parameter presets keyed by parameter names hash. Auto-populated on next run.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> _parameterPresets = [];

    public QueryTabViewModel(IConnectionManager connectionManager, IQueryHistoryService? queryHistoryService = null)
    {
        _connectionManager = connectionManager;
        _queryHistoryService = queryHistoryService;
        Title = "New Query";
    }

    public void SetConnection(ConnectionInfo connectionInfo)
    {
        _connectionInfo = connectionInfo;
        Title = $"Query - {connectionInfo.Name}";
        SelectedDatabase = connectionInfo.DefaultDatabase;
        _ = RefreshSchemaCacheAsync();
    }

    /// <summary>Refreshes the schema cache for autocompletion.</summary>
    public async Task RefreshSchemaCacheAsync()
    {
        if (_connectionInfo is null) return;
        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            var db = string.IsNullOrEmpty(SelectedDatabase) ? _connectionInfo.DefaultDatabase : SelectedDatabase;
            await SchemaCache.RefreshAsync(provider, db);
            SchemaCacheRefreshed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Best effort — autocomplete may not work if schema can't be loaded
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteQuery()
    {
        if (_connectionInfo is null || string.IsNullOrWhiteSpace(SqlText))
            return;

        // Use selected text if available, otherwise the full SQL
        var sqlToExecute = !string.IsNullOrWhiteSpace(SelectedText) ? SelectedText : SqlText;

        // Detect parameters and prompt user if any
        Dictionary<string, object?>? parameterValues = null;
        var detectedParams = SqlParameterDetector.DetectParameters(sqlToExecute);
        if (detectedParams.Count > 0)
        {
            if (ShowParameterDialog is null)
                return; // No dialog handler registered

            // Auto-populate from last used values (parameter presets)
            var presetKey = string.Join(",", detectedParams.Select(p => p.Name).OrderBy(n => n));
            if (_parameterPresets.TryGetValue(presetKey, out var lastValues))
            {
                foreach (var p in detectedParams)
                {
                    if (lastValues.TryGetValue(p.Name, out var lastVal))
                        p.Value = lastVal;
                }
            }

            parameterValues = ShowParameterDialog(detectedParams);
            if (parameterValues is null)
                return; // User cancelled

            // Save as preset for next run
            _parameterPresets[presetKey] = parameterValues
                .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
        }

        // Split into individual statements
        var statements = SqlStatementSplitter.Split(sqlToExecute);
        if (statements.Count == 0) return;

        IsExecuting = true;
        IsBusy = true;
        MessagesText = string.Empty;
        ErrorEntries.Clear();
        HasErrors = false;
        AllResultSets.Clear();
        _executionCts = new CancellationTokenSource();

        // Apply configurable timeout
        if (QueryTimeoutSeconds > 0)
            _executionCts.CancelAfter(TimeSpan.FromSeconds(QueryTimeoutSeconds));

        var totalSw = Stopwatch.StartNew();
        var messages = new StringBuilder();
        int totalRows = 0;
        int successCount = 0;
        DataTable? lastResultSet = null;

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo, _executionCts.Token);

            if (!string.IsNullOrEmpty(SelectedDatabase))
                await provider.ChangeDatabaseAsync(SelectedDatabase, _executionCts.Token);

            // Optional transactional mode for multi-statement scripts
            bool inTransaction = UseTransaction && statements.Count > 1;
            if (inTransaction && provider.Connection is not null)
            {
                await provider.ExecuteNonQueryAsync("BEGIN", _executionCts.Token);
                messages.AppendLine("-- Transaction started --");
            }

            try
            {
                for (int idx = 0; idx < statements.Count; idx++)
                {
                    _executionCts.Token.ThrowIfCancellationRequested();

                    var stmt = statements[idx];
                    if (statements.Count > 1)
                        messages.AppendLine($"-- Executing statement {idx + 1} of {statements.Count} --");

                    var result = parameterValues is { Count: > 0 }
                        ? await provider.ExecuteQueryAsync(stmt, parameterValues, _executionCts.Token)
                        : await provider.ExecuteQueryAsync(stmt, _executionCts.Token);

                    // Collect all result sets for multi-result support
                    if (result.ResultSet is { Rows.Count: > 0 })
                    {
                        lastResultSet = result.ResultSet;
                        AllResultSets.Add(result.ResultSet);
                    }

                    var rows = result.ResultSet?.Rows.Count ?? 0;
                    totalRows += rows;

                    if (result.Errors.Count > 0)
                    {
                        foreach (var err in result.Errors)
                        {
                            messages.AppendLine($"ERROR: {err.Message}");
                            if (err.Detail is not null)
                                messages.AppendLine($"  {err.Detail}");

                            ErrorEntries.Add(new QueryErrorEntry
                            {
                                Severity = "Error",
                                Line = err.Line,
                                SqlState = err.SqlState,
                                Message = err.Message,
                                Detail = err.Detail
                            });
                        }
                        HasErrors = true;

                        // Rollback on error in transactional mode
                        if (inTransaction)
                        {
                            await provider.ExecuteNonQueryAsync("ROLLBACK", default);
                            messages.AppendLine("-- Transaction rolled back due to error --");
                            inTransaction = false;
                            break;
                        }
                    }
                    else
                    {
                        successCount++;
                        messages.AppendLine($"OK: {rows} rows, {result.ExecutionTime.TotalMilliseconds:F0} ms");
                    }

                    if (result.Messages.Count > 0)
                        messages.AppendLine(string.Join(Environment.NewLine, result.Messages));
                }

                // Commit if all succeeded
                if (inTransaction)
                {
                    await provider.ExecuteNonQueryAsync("COMMIT", _executionCts.Token);
                    messages.AppendLine("-- Transaction committed --");
                }
            }
            catch when (inTransaction)
            {
                try { await provider.ExecuteNonQueryAsync("ROLLBACK", default); }
                catch { /* best effort rollback */ }
                messages.AppendLine("-- Transaction rolled back --");
                throw;
            }

            totalSw.Stop();

            ResultData = lastResultSet;
            SelectedResultSetIndex = AllResultSets.Count > 0 ? AllResultSets.Count - 1 : 0;
            RowCount = lastResultSet?.Rows.Count ?? 0;
            ExecutionTimeText = $"{totalSw.Elapsed.TotalMilliseconds:F0} ms";

            if (statements.Count == 1)
            {
                // Single statement: simpler message
                if (successCount > 0)
                    MessagesText = $"Query executed successfully. {totalRows} rows returned in {ExecutionTimeText}.";
                else
                    MessagesText = messages.ToString().TrimEnd();
            }
            else
            {
                messages.AppendLine($"\n-- Summary: {successCount}/{statements.Count} statements succeeded, {totalRows} total rows, {ExecutionTimeText} --");
                MessagesText = messages.ToString().TrimEnd();
            }

            // Record in history
            if (_queryHistoryService is not null)
            {
                await _queryHistoryService.AddEntryAsync(new QueryHistoryEntry
                {
                    Sql = sqlToExecute,
                    ConnectionName = _connectionInfo.Name,
                    Database = SelectedDatabase,
                    DatabaseType = _connectionInfo.DatabaseType,
                    ExecutionTime = totalSw.Elapsed,
                    RowsAffected = totalRows,
                    IsSuccessful = successCount == statements.Count
                });
            }
        }
        catch (OperationCanceledException)
        {
            MessagesText = messages.Length > 0
                ? messages + "\nQuery execution was cancelled."
                : "Query execution was cancelled.";
        }
        catch (Exception ex)
        {
            MessagesText = messages.Length > 0
                ? messages + $"\nERROR: {ex.Message}"
                : $"ERROR: {ex.Message}";

            // Record failed query in history
            if (_queryHistoryService is not null && _connectionInfo is not null)
            {
                await _queryHistoryService.AddEntryAsync(new QueryHistoryEntry
                {
                    Sql = sqlToExecute,
                    ConnectionName = _connectionInfo.Name,
                    Database = SelectedDatabase,
                    DatabaseType = _connectionInfo.DatabaseType,
                    IsSuccessful = false,
                    ErrorMessage = ex.Message
                });
            }
        }
        finally
        {
            IsExecuting = false;
            IsBusy = false;
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    private bool CanExecute() => !IsExecuting && _connectionInfo is not null;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExplainQuery()
    {
        if (_connectionInfo is null || string.IsNullOrWhiteSpace(SqlText))
            return;

        var sqlToExplain = !string.IsNullOrWhiteSpace(SelectedText) ? SelectedText : SqlText;

        // Use the first statement only for EXPLAIN
        var statements = SqlStatementSplitter.Split(sqlToExplain);
        if (statements.Count == 0) return;

        var firstStmt = statements[0];
        var explainSql = QueryPlanParser.GenerateExplainSql(firstStmt, _connectionInfo.DatabaseType);

        IsExecuting = true;
        IsBusy = true;
        _executionCts = new CancellationTokenSource();

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo, _executionCts.Token);

            if (!string.IsNullOrEmpty(SelectedDatabase))
                await provider.ChangeDatabaseAsync(SelectedDatabase, _executionCts.Token);

            var result = await provider.ExecuteQueryAsync(explainSql, _executionCts.Token);

            if (result.Errors.Count > 0)
            {
                MessagesText = string.Join("\n", result.Errors.Select(e => e.Message));
                foreach (var err in result.Errors)
                {
                    ErrorEntries.Add(new QueryErrorEntry
                    {
                        Severity = "Error",
                        Line = err.Line,
                        SqlState = err.SqlState,
                        Message = err.Message,
                        Detail = err.Detail
                    });
                }
                HasErrors = true;
                QueryPlanText = string.Empty;
                QueryPlanData = null;
            }
            else
            {
                // Collect EXPLAIN output text
                var sb = new StringBuilder();
                if (result.ResultSet is not null)
                {
                    foreach (System.Data.DataRow row in result.ResultSet.Rows)
                    {
                        sb.AppendLine(string.Join("\t", row.ItemArray));
                    }
                }

                var rawText = sb.ToString();
                QueryPlanText = rawText;

                // Parse into structured plan
                QueryPlanData = QueryPlanParser.ParseTextPlan(rawText, _connectionInfo.DatabaseType);

                MessagesText = $"EXPLAIN completed in {result.ExecutionTime.TotalMilliseconds:F0} ms";
            }
        }
        catch (OperationCanceledException)
        {
            MessagesText = "EXPLAIN cancelled.";
        }
        catch (Exception ex)
        {
            MessagesText = $"EXPLAIN ERROR: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
            IsBusy = false;
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _executionCts?.Cancel();
    }

    partial void OnSelectedResultSetIndexChanged(int value)
    {
        if (value >= 0 && value < AllResultSets.Count)
        {
            ResultData = AllResultSets[value];
            RowCount = ResultData?.Rows.Count ?? 0;
        }
    }

    // ── Export Commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private void ExportCsv()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = ".csv",
            FileName = "export.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            var csv = DataTableToCsv(ResultData);
            File.WriteAllText(dialog.FileName, csv, Encoding.UTF8);
            MessagesText = $"Exported {ResultData.Rows.Count} rows to {dialog.FileName}";
        }
    }

    [RelayCommand]
    private void ExportJson()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "export.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var json = DataTableToJson(ResultData);
            File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
            MessagesText = $"Exported {ResultData.Rows.Count} rows to {dialog.FileName}";
        }
    }

    [RelayCommand]
    private void ExportSql()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "SQL Files (*.sql)|*.sql|All Files (*.*)|*.*",
            DefaultExt = ".sql",
            FileName = "export.sql"
        };

        if (dialog.ShowDialog() == true)
        {
            var sql = DataTableToInsertStatements(ResultData, "exported_table");
            File.WriteAllText(dialog.FileName, sql, Encoding.UTF8);
            MessagesText = $"Exported {ResultData.Rows.Count} rows to {dialog.FileName}";
        }
    }

    [RelayCommand]
    private void ExportExcel()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
            DefaultExt = ".xlsx",
            FileName = "export.xlsx"
        };

        if (dialog.ShowDialog() == true)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Results");

            // Headers
            for (int c = 0; c < ResultData.Columns.Count; c++)
                ws.Cell(1, c + 1).Value = ResultData.Columns[c].ColumnName;

            // Data
            for (int r = 0; r < ResultData.Rows.Count; r++)
            {
                for (int c = 0; c < ResultData.Columns.Count; c++)
                {
                    var val = ResultData.Rows[r][c];
                    if (val is not null && val != DBNull.Value)
                        ws.Cell(r + 2, c + 1).Value = val.ToString();
                }
            }

            ws.Columns().AdjustToContents(1, 50);
            ws.Row(1).Style.Font.Bold = true;

            workbook.SaveAs(dialog.FileName);
            MessagesText = $"Exported {ResultData.Rows.Count} rows to {dialog.FileName}";
        }
    }

    [RelayCommand]
    private void ExportXml()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
            DefaultExt = ".xml",
            FileName = "export.xml"
        };

        if (dialog.ShowDialog() == true)
        {
            var xml = DataTableToXml(ResultData);
            File.WriteAllText(dialog.FileName, xml, Encoding.UTF8);
            MessagesText = $"Exported {ResultData.Rows.Count} rows to {dialog.FileName}";
        }
    }

    [RelayCommand]
    private void ExportMarkdown()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            DefaultExt = ".md",
            FileName = "export.md"
        };

        if (dialog.ShowDialog() == true)
        {
            var md = DataTableToMarkdown(ResultData);
            File.WriteAllText(dialog.FileName, md, Encoding.UTF8);
            MessagesText = $"Exported {ResultData.Rows.Count} rows to {dialog.FileName}";
        }
    }

    [RelayCommand]
    private void ExportHtml()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "HTML Files (*.html)|*.html|All Files (*.*)|*.*",
            DefaultExt = ".html",
            FileName = "export.html"
        };

        if (dialog.ShowDialog() == true)
        {
            var html = DataTableToHtml(ResultData);
            File.WriteAllText(dialog.FileName, html, Encoding.UTF8);
            MessagesText = $"Exported {ResultData.Rows.Count} rows to {dialog.FileName}";
        }
    }

    [RelayCommand]
    private void CopyAsCsv()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;
        var csv = DataTableToCsv(ResultData);
        System.Windows.Clipboard.SetText(csv);
        MessagesText = $"Copied {ResultData.Rows.Count} rows as CSV to clipboard.";
    }

    [RelayCommand]
    private void CopyAsInsert()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;
        var sql = DataTableToInsertStatements(ResultData, "table_name");
        System.Windows.Clipboard.SetText(sql);
        MessagesText = $"Copied {ResultData.Rows.Count} rows as INSERT to clipboard.";
    }

    // ── Export Helpers ──────────────────────────────────────────────────────

    private static string DataTableToCsv(DataTable table)
    {
        var sb = new StringBuilder();

        // Header
        var columns = table.Columns.Cast<DataColumn>().Select(c => EscapeCsvField(c.ColumnName));
        sb.AppendLine(string.Join(",", columns));

        // Rows
        foreach (DataRow row in table.Rows)
        {
            var values = row.ItemArray.Select(v =>
                v is null || v == DBNull.Value ? "" : EscapeCsvField(v.ToString() ?? ""));
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    private static string DataTableToJson(DataTable table)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (DataRow row in table.Rows)
        {
            var dict = new Dictionary<string, object?>();
            foreach (DataColumn col in table.Columns)
            {
                var val = row[col];
                dict[col.ColumnName] = val == DBNull.Value ? null : val;
            }
            rows.Add(dict);
        }

        return JsonSerializer.Serialize(rows, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static string DataTableToInsertStatements(DataTable table, string tableName)
    {
        var sb = new StringBuilder();
        var columnNames = string.Join(", ", table.Columns.Cast<DataColumn>().Select(c => $"\"{c.ColumnName}\""));

        foreach (DataRow row in table.Rows)
        {
            var values = row.ItemArray.Select(v =>
            {
                if (v is null || v == DBNull.Value) return "NULL";
                if (v is int or long or short or decimal or double or float) return v.ToString()!;
                if (v is bool b) return b ? "TRUE" : "FALSE";
                if (v is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss}'";
                return $"'{v.ToString()?.Replace("'", "''")}'";
            });

            sb.AppendLine($"INSERT INTO {tableName} ({columnNames}) VALUES ({string.Join(", ", values)});");
        }

        return sb.ToString();
    }

    // ── Property Change Handlers ────────────────────────────────────────────

    partial void OnSqlTextChanged(string value)
    {
        HasUnsavedChanges = !string.IsNullOrEmpty(value);
        ExecuteQueryCommand.NotifyCanExecuteChanged();
        ExplainQueryCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsExecutingChanged(bool value)
    {
        ExecuteQueryCommand.NotifyCanExecuteChanged();
        ExplainQueryCommand.NotifyCanExecuteChanged();
    }

    // ── Additional Export Helpers ────────────────────────────────────────────

    private static string DataTableToXml(DataTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<results>");

        foreach (DataRow row in table.Rows)
        {
            sb.AppendLine("  <row>");
            foreach (DataColumn col in table.Columns)
            {
                var val = row[col];
                var text = val is null || val == DBNull.Value ? "" : System.Security.SecurityElement.Escape(val.ToString());
                sb.AppendLine($"    <{col.ColumnName}>{text}</{col.ColumnName}>");
            }
            sb.AppendLine("  </row>");
        }

        sb.AppendLine("</results>");
        return sb.ToString();
    }

    private static string DataTableToMarkdown(DataTable table)
    {
        var sb = new StringBuilder();
        var cols = table.Columns.Cast<DataColumn>().ToList();

        // Header
        sb.AppendLine("| " + string.Join(" | ", cols.Select(c => c.ColumnName)) + " |");
        sb.AppendLine("| " + string.Join(" | ", cols.Select(_ => "---")) + " |");

        // Rows
        foreach (DataRow row in table.Rows)
        {
            var values = cols.Select(c =>
            {
                var val = row[c];
                return val is null || val == DBNull.Value ? "NULL" : val.ToString()?.Replace("|", "\\|") ?? "";
            });
            sb.AppendLine("| " + string.Join(" | ", values) + " |");
        }

        return sb.ToString();
    }

    private static string DataTableToHtml(DataTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', sans-serif; margin: 20px; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
        sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        sb.AppendLine("th { background-color: #4a86c8; color: white; }");
        sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
        sb.AppendLine("tr:hover { background-color: #ddd; }");
        sb.AppendLine(".null { color: #999; font-style: italic; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<table>");

        // Header
        sb.Append("<tr>");
        foreach (DataColumn col in table.Columns)
            sb.Append($"<th>{System.Net.WebUtility.HtmlEncode(col.ColumnName)}</th>");
        sb.AppendLine("</tr>");

        // Rows
        foreach (DataRow row in table.Rows)
        {
            sb.Append("<tr>");
            foreach (DataColumn col in table.Columns)
            {
                var val = row[col];
                if (val is null || val == DBNull.Value)
                    sb.Append("<td class=\"null\">NULL</td>");
                else
                    sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(val.ToString())}</td>");
            }
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    [RelayCommand]
    private void VisualizeAsChart()
    {
        if (ResultData is null || ResultData.Rows.Count == 0) return;
        OpenChartDelegate?.Invoke(ResultData, Title);
    }
}

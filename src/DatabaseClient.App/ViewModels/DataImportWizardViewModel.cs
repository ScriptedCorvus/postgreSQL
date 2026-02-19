using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.Services;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the data import wizard. Supports CSV, JSON, XML, SQL, and Excel files.
/// </summary>
public partial class DataImportWizardViewModel : ObservableObject
{
    private readonly IConnectionManager _connectionManager;
    private ConnectionInfo? _connectionInfo;
    private string? _database;
    private IDatabaseProvider? _provider;

    #region Step tracking

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private int _totalSteps = 4;

    public bool CanGoBack => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < TotalSteps;
    public bool IsLastStep => CurrentStep == TotalSteps;

    #endregion

    #region Step 1 - File Selection

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _selectedFormat = "CSV";

    [ObservableProperty]
    private DataTable? _previewData;

    [ObservableProperty]
    private string _previewStatus = string.Empty;

    public static IReadOnlyList<string> SupportedFormats { get; } = ["CSV", "JSON", "XML", "SQL", "Excel"];

    #endregion

    #region Step 2 - Format Configuration

    [ObservableProperty]
    private char _csvDelimiter = ',';

    [ObservableProperty]
    private bool _hasHeader = true;

    [ObservableProperty]
    private string _encoding = "UTF-8";

    public static IReadOnlyList<string> Encodings { get; } = ["UTF-8", "ASCII", "ISO-8859-1", "UTF-16"];
    public static IReadOnlyList<char> CommonDelimiters { get; } = [',', ';', '\t', '|'];

    #endregion

    #region Step 3 - Column Mapping

    [ObservableProperty]
    private string _targetTable = string.Empty;

    [ObservableProperty]
    private bool _createTableIfNotExists = true;

    public ObservableCollection<ColumnMapping> ColumnMappings { get; } = [];
    public ObservableCollection<string> AvailableTargetTables { get; } = [];
    public ObservableCollection<string> AvailableTargetColumns { get; } = [];

    #endregion

    #region Step 4 - Import Options

    [ObservableProperty]
    private string _importMode = "INSERT";

    [ObservableProperty]
    private int _batchSize = 1000;

    [ObservableProperty]
    private bool _useTransaction = true;

    [ObservableProperty]
    private bool _truncateBeforeImport;

    public static IReadOnlyList<string> ImportModes { get; } = ["INSERT", "INSERT IGNORE", "UPSERT"];

    #endregion

    #region Import Progress

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private int _importedRows;

    [ObservableProperty]
    private int _totalSourceRows;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _importStatus = string.Empty;

    public ObservableCollection<string> ImportLog { get; } = [];

    [ObservableProperty]
    private bool _importCompleted;

    #endregion

    public DataImportWizardViewModel(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public void SetConnection(ConnectionInfo connectionInfo, string database)
    {
        _connectionInfo = connectionInfo;
        _database = database;
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

            if (CurrentStep == 3)
                _ = LoadTargetTablesAsync();
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
    private void LoadPreview()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            PreviewStatus = "File not found.";
            return;
        }

        try
        {
            var enc = GetEncoding();

            if (SelectedFormat == "SQL")
            {
                var sql = File.ReadAllText(FilePath, enc);
                var preview = new DataTable();
                preview.Columns.Add("SQL Content");
                var lines = sql.Split('\n').Take(20);
                foreach (var line in lines)
                    preview.Rows.Add(line.TrimEnd());
                PreviewData = preview;
                PreviewStatus = $"SQL file loaded ({new FileInfo(FilePath).Length / 1024} KB)";
            }
            else
            {
                PreviewData = DataFileReader.GetPreview(FilePath, SelectedFormat.ToLowerInvariant(), 20, CsvDelimiter, HasHeader);
                PreviewStatus = $"Preview: {PreviewData.Rows.Count} rows, {PreviewData.Columns.Count} columns";
            }

            // Build column mappings
            BuildColumnMappings();
        }
        catch (Exception ex)
        {
            PreviewStatus = $"Error: {ex.Message}";
        }
    }

    private void BuildColumnMappings()
    {
        ColumnMappings.Clear();
        if (PreviewData is null) return;

        foreach (DataColumn col in PreviewData.Columns)
        {
            ColumnMappings.Add(new ColumnMapping
            {
                SourceColumn = col.ColumnName,
                TargetColumn = col.ColumnName,
                Include = true,
                InferredType = InferColumnType(PreviewData, col)
            });
        }
    }

    private async Task LoadTargetTablesAsync()
    {
        if (_connectionInfo is null || _database is null) return;

        try
        {
            _provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            await _provider.ChangeDatabaseAsync(_database);

            var tables = await _provider.GetTablesAsync(_database);
            AvailableTargetTables.Clear();
            foreach (var t in tables) AvailableTargetTables.Add(t.Name);

            if (!string.IsNullOrEmpty(TargetTable))
            {
                var columns = await _provider.GetColumnsAsync(_database, TargetTable);
                AvailableTargetColumns.Clear();
                foreach (var c in columns) AvailableTargetColumns.Add(c.Name);
            }
        }
        catch (Exception ex)
        {
            ImportLog.Add($"Error loading tables: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StartImport()
    {
        if (_provider is null || PreviewData is null || string.IsNullOrWhiteSpace(TargetTable))
        {
            ImportStatus = "Missing provider, data, or target table.";
            return;
        }

        IsImporting = true;
        ImportCompleted = false;
        ImportedRows = 0;
        ErrorCount = 0;
        ImportLog.Clear();

        try
        {
            // Read full data
            DataTable fullData;
            if (SelectedFormat == "SQL")
            {
                await ExecuteSqlFile();
                return;
            }
            else
            {
                var enc = GetEncoding();
                fullData = SelectedFormat.ToLowerInvariant() switch
                {
                    "csv" => DataFileReader.ReadCsv(FilePath, CsvDelimiter, HasHeader, enc),
                    "json" => DataFileReader.ReadJson(FilePath),
                    "xml" => DataFileReader.ReadXml(FilePath),
                    "excel" => DataFileReader.ReadExcel(FilePath, HasHeader),
                    _ => new DataTable()
                };
            }

            TotalSourceRows = fullData.Rows.Count;

            if (TruncateBeforeImport)
            {
                ImportLog.Add("Truncating target table...");
                var quoteId = QuoteIdentifier(TargetTable);
                await _provider.ExecuteNonQueryAsync($"DELETE FROM {quoteId}");
            }

            if (CreateTableIfNotExists && !AvailableTargetTables.Contains(TargetTable))
            {
                ImportLog.Add("Creating target table...");
                await CreateTargetTableAsync(fullData);
            }

            // Import in batches
            var includedMappings = ColumnMappings.Where(m => m.Include).ToList();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int batch = 0; batch < TotalSourceRows; batch += BatchSize)
            {
                var batchRows = fullData.AsEnumerable().Skip(batch).Take(BatchSize);

                if (UseTransaction)
                {
                    foreach (var row in batchRows)
                    {
                        try
                        {
                            var sql = GenerateInsertSql(row, includedMappings);
                            await _provider.ExecuteNonQueryAsync(sql);
                            ImportedRows++;
                        }
                        catch (Exception ex)
                        {
                            ErrorCount++;
                            ImportLog.Add($"Row {batch + ImportedRows + ErrorCount}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    foreach (var row in batchRows)
                    {
                        try
                        {
                            var sql = GenerateInsertSql(row, includedMappings);
                            await _provider.ExecuteNonQueryAsync(sql);
                            ImportedRows++;
                        }
                        catch (Exception ex)
                        {
                            ErrorCount++;
                            ImportLog.Add($"Row {batch + ImportedRows + ErrorCount}: {ex.Message}");
                        }
                    }
                }

                ProgressPercent = TotalSourceRows > 0 ? (double)(ImportedRows + ErrorCount) / TotalSourceRows * 100 : 0;
                ImportStatus = $"Imported {ImportedRows}/{TotalSourceRows} rows ({sw.Elapsed.TotalSeconds:F1}s)";
            }

            sw.Stop();
            ImportLog.Add($"Import completed: {ImportedRows} rows imported, {ErrorCount} errors, {sw.Elapsed.TotalSeconds:F1}s elapsed.");
            ImportStatus = $"Done: {ImportedRows} imported, {ErrorCount} errors.";
        }
        catch (Exception ex)
        {
            ImportStatus = $"Import failed: {ex.Message}";
            ImportLog.Add($"Fatal error: {ex.Message}");
        }
        finally
        {
            IsImporting = false;
            ImportCompleted = true;
        }
    }

    private async Task ExecuteSqlFile()
    {
        var sql = await File.ReadAllTextAsync(FilePath, GetEncoding());
        ImportLog.Add("Executing SQL file...");
        ImportStatus = "Executing SQL file...";
        try
        {
            await _provider!.ExecuteNonQueryAsync(sql);
            ImportedRows = 1;
            ImportLog.Add("SQL file executed successfully.");
            ImportStatus = "SQL file executed successfully.";
        }
        catch (Exception ex)
        {
            ErrorCount++;
            ImportLog.Add($"SQL execution error: {ex.Message}");
            ImportStatus = $"SQL error: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
            ImportCompleted = true;
        }
    }

    private async Task CreateTargetTableAsync(DataTable data)
    {
        var sb = new StringBuilder();
        sb.Append($"CREATE TABLE {QuoteIdentifier(TargetTable)} (");

        var cols = new List<string>();
        foreach (var mapping in ColumnMappings.Where(m => m.Include))
        {
            cols.Add($"{QuoteIdentifier(mapping.TargetColumn)} {mapping.InferredType}");
        }
        sb.Append(string.Join(", ", cols));
        sb.Append(')');

        await _provider!.ExecuteNonQueryAsync(sb.ToString());
        ImportLog.Add($"Table {TargetTable} created.");
    }

    private string GenerateInsertSql(DataRow row, List<ColumnMapping> mappings)
    {
        var sb = new StringBuilder();
        sb.Append($"INSERT INTO {QuoteIdentifier(TargetTable)} (");
        sb.Append(string.Join(", ", mappings.Select(m => QuoteIdentifier(m.TargetColumn))));
        sb.Append(") VALUES (");

        var values = new List<string>();
        foreach (var mapping in mappings)
        {
            var val = row.Table.Columns.Contains(mapping.SourceColumn) ? row[mapping.SourceColumn] : DBNull.Value;
            values.Add(FormatValue(val));
        }
        sb.Append(string.Join(", ", values));
        sb.Append(')');
        return sb.ToString();
    }

    private static string FormatValue(object value)
    {
        if (value == DBNull.Value || value is null) return "NULL";
        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            bool b => b ? "TRUE" : "FALSE",
            _ => value.ToString()?.Replace("'", "''") ?? "NULL"
        };
    }

    private static string InferColumnType(DataTable dt, DataColumn col)
    {
        // Sample first non-null values
        foreach (DataRow row in dt.Rows)
        {
            var val = row[col]?.ToString();
            if (string.IsNullOrEmpty(val)) continue;

            if (int.TryParse(val, out _)) return "INTEGER";
            if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)) return "NUMERIC";
            if (DateTime.TryParse(val, out _)) return "TIMESTAMP";
            if (bool.TryParse(val, out _)) return "BOOLEAN";
            return "TEXT";
        }
        return "TEXT";
    }

    private string QuoteIdentifier(string name)
    {
        if (_connectionInfo is null) return $"\"{name}\"";
        return _connectionInfo.DatabaseType switch
        {
            DatabaseType.MySQL or DatabaseType.MariaDB => $"`{name}`",
            _ => $"\"{name}\""
        };
    }

    private Encoding GetEncoding() => Encoding switch
    {
        "ASCII" => System.Text.Encoding.ASCII,
        "ISO-8859-1" => System.Text.Encoding.Latin1,
        "UTF-16" => System.Text.Encoding.Unicode,
        _ => System.Text.Encoding.UTF8
    };
}

/// <summary>Represents a column mapping from source file to target table.</summary>
public partial class ColumnMapping : ObservableObject
{
    [ObservableProperty]
    private string _sourceColumn = string.Empty;

    [ObservableProperty]
    private string _targetColumn = string.Empty;

    [ObservableProperty]
    private bool _include = true;

    [ObservableProperty]
    private string _inferredType = "TEXT";
}

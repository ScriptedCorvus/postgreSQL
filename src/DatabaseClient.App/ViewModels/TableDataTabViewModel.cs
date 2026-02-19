using System.Collections.ObjectModel;
using System.Data;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the inline data editor tab.
/// Displays table data in a paginated DataGrid with editing support.
/// </summary>
public partial class TableDataTabViewModel : TabViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private ConnectionInfo? _connectionInfo;
    private string? _database;
    private string _tableName = string.Empty;
    private IDatabaseProvider? _provider;
    private IReadOnlyList<ColumnInfo> _columns = [];
    private readonly HashSet<int> _modifiedRows = [];
    private readonly HashSet<int> _deletedRows = [];
    private readonly List<DataRow> _newRows = [];

    [ObservableProperty]
    private DataTable? _data;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalRows;

    [ObservableProperty]
    private int _pageSize = 100;

    [ObservableProperty]
    private string _sortColumn = string.Empty;

    [ObservableProperty]
    private bool _sortDescending;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasPrimaryKey;

    [ObservableProperty]
    private int _pendingChangesCount;

    public override string TabIconKind => "TableEdit";

    public static IReadOnlyList<int> PageSizeOptions { get; } = [25, 50, 100, 500, 1000];

    public TableDataTabViewModel(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        Title = "Data";
    }

    public void SetConnection(ConnectionInfo connectionInfo, string database, string tableName)
    {
        _connectionInfo = connectionInfo;
        _database = database;
        _tableName = tableName;
        Title = $"{tableName} - Data";
    }

    public async Task LoadDataAsync()
    {
        if (_connectionInfo is null || _database is null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Loading...";

            _provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            await _provider.ChangeDatabaseAsync(_database);

            // Get column metadata
            _columns = await _provider.GetColumnsAsync(_database, _tableName);
            HasPrimaryKey = _columns.Any(c => c.IsPrimaryKey);

            // Get total count
            var countSql = $"SELECT COUNT(*) FROM {QuoteIdentifier(_tableName)}";
            if (!string.IsNullOrWhiteSpace(FilterText))
                countSql += $" WHERE {FilterText}";

            var countResult = await _provider.ExecuteQueryAsync(countSql);
            if (countResult.IsSuccessful && countResult.ResultSet?.Rows.Count > 0)
            {
                TotalRows = Convert.ToInt32(countResult.ResultSet.Rows[0][0]);
            }

            TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalRows / PageSize));
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            // Build SELECT query with pagination
            var sql = BuildSelectQuery();
            var result = await _provider.ExecuteQueryAsync(sql);

            if (result.IsSuccessful && result.ResultSet is not null)
            {
                Data = result.ResultSet;
                ClearPendingChanges();
                StatusMessage = $"Loaded {Data.Rows.Count} rows (page {CurrentPage}/{TotalPages}, total: {TotalRows})";
            }
            else
            {
                StatusMessage = result.Errors.Count > 0 ? result.Errors[0].Message : "Query failed.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string BuildSelectQuery()
    {
        var sb = new StringBuilder();
        sb.Append($"SELECT * FROM {QuoteIdentifier(_tableName)}");

        if (!string.IsNullOrWhiteSpace(FilterText))
            sb.Append($" WHERE {FilterText}");

        if (!string.IsNullOrWhiteSpace(SortColumn))
        {
            sb.Append($" ORDER BY {QuoteIdentifier(SortColumn)}");
            if (SortDescending) sb.Append(" DESC");
        }

        int offset = (CurrentPage - 1) * PageSize;
        if (_connectionInfo is not null)
        {
            sb.Append($" LIMIT {PageSize} OFFSET {offset}");
        }

        return sb.ToString();
    }

    #region Pagination Commands

    [RelayCommand]
    private async Task FirstPage()
    {
        CurrentPage = 1;
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task LastPage()
    {
        CurrentPage = TotalPages;
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task ChangePageSize(int newSize)
    {
        PageSize = newSize;
        CurrentPage = 1;
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task RefreshData()
    {
        await LoadDataAsync();
    }

    #endregion

    #region Sorting

    public async Task SortByColumnAsync(string columnName)
    {
        if (SortColumn == columnName)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = columnName;
            SortDescending = false;
        }
        CurrentPage = 1;
        await LoadDataAsync();
    }

    #endregion

    #region Filtering

    [RelayCommand]
    private async Task ApplyFilter()
    {
        CurrentPage = 1;
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task ClearFilter()
    {
        FilterText = string.Empty;
        CurrentPage = 1;
        await LoadDataAsync();
    }

    #endregion

    #region Row Editing

    /// <summary>Called when a cell value is changed in the DataGrid.</summary>
    public void MarkRowModified(int rowIndex)
    {
        if (rowIndex >= 0 && !_deletedRows.Contains(rowIndex))
        {
            _modifiedRows.Add(rowIndex);
            UpdatePendingChangesCount();
        }
    }

    [RelayCommand]
    private void AddNewRow()
    {
        if (Data is null) return;
        var newRow = Data.NewRow();
        Data.Rows.Add(newRow);
        _newRows.Add(newRow);
        UpdatePendingChangesCount();
    }

    [RelayCommand]
    private void DeleteRow(DataRowView? rowView)
    {
        if (Data is null || rowView is null) return;
        int idx = Data.Rows.IndexOf(rowView.Row);
        if (idx >= 0)
        {
            if (_newRows.Contains(rowView.Row))
            {
                _newRows.Remove(rowView.Row);
                Data.Rows.Remove(rowView.Row);
            }
            else
            {
                _deletedRows.Add(idx);
                _modifiedRows.Remove(idx);
            }
            UpdatePendingChangesCount();
        }
    }

    [RelayCommand]
    private void SetCellNull(DataRowView? rowView)
    {
        // This would be called with specific column info from the View code-behind
    }

    [RelayCommand(CanExecute = nameof(CanApplyChanges))]
    private async Task ApplyChanges()
    {
        if (_provider is null || Data is null || !HasPrimaryKey) return;

        try
        {
            IsLoading = true;
            var pkColumns = _columns.Where(c => c.IsPrimaryKey).ToList();
            var errors = new List<string>();
            int successCount = 0;

            // Process DELETEs
            foreach (var rowIdx in _deletedRows.OrderByDescending(i => i))
            {
                if (rowIdx >= Data.Rows.Count) continue;
                var row = Data.Rows[rowIdx];
                var sql = GenerateDeleteSql(row, pkColumns);
                try
                {
                    await _provider.ExecuteNonQueryAsync(sql);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"DELETE row {rowIdx}: {ex.Message}");
                }
            }

            // Process UPDATEs
            foreach (var rowIdx in _modifiedRows)
            {
                if (rowIdx >= Data.Rows.Count) continue;
                var row = Data.Rows[rowIdx];
                if (_newRows.Contains(row)) continue; // handled as INSERT
                var sql = GenerateUpdateSql(row, pkColumns);
                try
                {
                    await _provider.ExecuteNonQueryAsync(sql);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"UPDATE row {rowIdx}: {ex.Message}");
                }
            }

            // Process INSERTs
            foreach (var row in _newRows)
            {
                var sql = GenerateInsertSql(row);
                try
                {
                    await _provider.ExecuteNonQueryAsync(sql);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"INSERT: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                StatusMessage = $"Applied {successCount} changes with {errors.Count} error(s): {errors[0]}";
            }
            else
            {
                StatusMessage = $"Successfully applied {successCount} change(s).";
            }

            // Reload data
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying changes: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanApplyChanges() => PendingChangesCount > 0 && HasPrimaryKey;

    [RelayCommand]
    private async Task DiscardChanges()
    {
        ClearPendingChanges();
        await LoadDataAsync();
    }

    private void ClearPendingChanges()
    {
        _modifiedRows.Clear();
        _deletedRows.Clear();
        _newRows.Clear();
        UpdatePendingChangesCount();
    }

    private void UpdatePendingChangesCount()
    {
        PendingChangesCount = _modifiedRows.Count + _deletedRows.Count + _newRows.Count;
        ApplyChangesCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region SQL Generation

    private string GenerateDeleteSql(DataRow row, List<ColumnInfo> pkColumns)
    {
        var sb = new StringBuilder();
        sb.Append($"DELETE FROM {QuoteIdentifier(_tableName)} WHERE ");
        sb.Append(BuildWhereClause(row, pkColumns));
        return sb.ToString();
    }

    private string GenerateUpdateSql(DataRow row, List<ColumnInfo> pkColumns)
    {
        var sb = new StringBuilder();
        sb.Append($"UPDATE {QuoteIdentifier(_tableName)} SET ");

        var setClauses = new List<string>();
        foreach (DataColumn col in Data!.Columns)
        {
            if (pkColumns.Any(pk => pk.Name.Equals(col.ColumnName, StringComparison.OrdinalIgnoreCase)))
                continue;
            setClauses.Add($"{QuoteIdentifier(col.ColumnName)} = {FormatValue(row[col])}");
        }
        sb.Append(string.Join(", ", setClauses));
        sb.Append(" WHERE ");
        sb.Append(BuildWhereClause(row, pkColumns));
        return sb.ToString();
    }

    private string GenerateInsertSql(DataRow row)
    {
        var sb = new StringBuilder();
        sb.Append($"INSERT INTO {QuoteIdentifier(_tableName)} (");

        var columns = new List<string>();
        var values = new List<string>();
        foreach (DataColumn col in Data!.Columns)
        {
            if (row[col] == DBNull.Value || row[col] is null) continue;
            columns.Add(QuoteIdentifier(col.ColumnName));
            values.Add(FormatValue(row[col]));
        }

        sb.Append(string.Join(", ", columns));
        sb.Append(") VALUES (");
        sb.Append(string.Join(", ", values));
        sb.Append(')');
        return sb.ToString();
    }

    private string BuildWhereClause(DataRow row, List<ColumnInfo> pkColumns)
    {
        var conditions = new List<string>();
        foreach (var pk in pkColumns)
        {
            var value = row[pk.Name];
            if (value == DBNull.Value || value is null)
                conditions.Add($"{QuoteIdentifier(pk.Name)} IS NULL");
            else
                conditions.Add($"{QuoteIdentifier(pk.Name)} = {FormatValue(value)}");
        }
        return string.Join(" AND ", conditions);
    }

    private static string FormatValue(object value)
    {
        if (value == DBNull.Value || value is null) return "NULL";
        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss.fffffffzzz}'",
            bool b => b ? "TRUE" : "FALSE",
            byte[] bytes => $"'\\x{Convert.ToHexString(bytes)}'",
            _ => value.ToString()?.Replace("'", "''") ?? "NULL"
        };
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

    #endregion

    /// <summary>Gets the set of deleted row indices for visual marking in the View.</summary>
    public IReadOnlySet<int> DeletedRows => _deletedRows;
    /// <summary>Gets the set of modified row indices for visual marking in the View.</summary>
    public IReadOnlySet<int> ModifiedRows => _modifiedRows;
    /// <summary>Checks if a DataRow is a new (unsaved) row.</summary>
    public bool IsNewRow(DataRow row) => _newRows.Contains(row);
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the Query History tab. Shows past queries with search,
/// allows copying SQL or opening in a new query tab.
/// </summary>
public partial class QueryHistoryTabViewModel : TabViewModelBase
{
    private readonly IQueryHistoryService _queryHistoryService;

    /// <summary>Action to open SQL in a new query tab.</summary>
    public Action<string>? OpenInNewTab { get; set; }

    public ObservableCollection<QueryHistoryEntry> Entries { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private QueryHistoryEntry? _selectedEntry;

    [ObservableProperty]
    private int _totalCount;

    public QueryHistoryTabViewModel(IQueryHistoryService queryHistoryService)
    {
        _queryHistoryService = queryHistoryService;
        Title = "Query History";
    }

    public override string TabIconKind => "History";

    [RelayCommand]
    private async Task LoadHistory()
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<QueryHistoryEntry> entries;
            if (string.IsNullOrWhiteSpace(SearchText))
                entries = await _queryHistoryService.GetAllAsync();
            else
                entries = await _queryHistoryService.SearchAsync(SearchText);

            Entries.Clear();
            foreach (var e in entries)
                Entries.Add(e);

            TotalCount = Entries.Count;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Search()
    {
        await LoadHistory();
    }

    [RelayCommand]
    private void CopySql()
    {
        if (SelectedEntry is not null)
        {
            System.Windows.Clipboard.SetText(SelectedEntry.Sql);
        }
    }

    [RelayCommand]
    private void OpenInEditor()
    {
        if (SelectedEntry is not null)
        {
            OpenInNewTab?.Invoke(SelectedEntry.Sql);
        }
    }

    [RelayCommand]
    private async Task DeleteEntry()
    {
        if (SelectedEntry is not null)
        {
            await _queryHistoryService.DeleteAsync(SelectedEntry.Id);
            Entries.Remove(SelectedEntry);
            TotalCount = Entries.Count;
        }
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        await _queryHistoryService.ClearAsync();
        Entries.Clear();
        TotalCount = 0;
    }

    partial void OnSearchTextChanged(string value)
    {
        // Debounced search would be ideal but simple approach for now
        SearchCommand.Execute(null);
    }
}

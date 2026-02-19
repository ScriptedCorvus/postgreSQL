using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the snippets management tab.
/// </summary>
public partial class SnippetsTabViewModel : TabViewModelBase
{
    private readonly IQuerySnippetService _snippetService;

    public ObservableCollection<QuerySnippet> Snippets { get; } = [];
    public ObservableCollection<QuerySnippet> BuiltInSnippets { get; } = [];

    [ObservableProperty]
    private QuerySnippet? _selectedSnippet;

    [ObservableProperty]
    private string _searchText = string.Empty;

    // Editing fields
    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editSql = string.Empty;

    [ObservableProperty]
    private string _editDescription = string.Empty;

    [ObservableProperty]
    private string _editTags = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public override string TabIconKind => "CodeBracesBox";

    /// <summary>Delegate to insert SQL into the active query editor.</summary>
    public Action<string>? InsertIntoEditor { get; set; }

    public SnippetsTabViewModel(IQuerySnippetService snippetService)
    {
        _snippetService = snippetService;
        Title = "Snippets";
    }

    public async Task LoadAsync()
    {
        var all = await _snippetService.GetAllAsync();
        Snippets.Clear();
        foreach (var s in all) Snippets.Add(s);
        StatusMessage = $"{Snippets.Count} snippet(s) loaded.";
    }

    public async Task LoadBuiltInAsync(DatabaseType dbType)
    {
        var builtIn = await _snippetService.GetBuiltInSnippetsAsync(dbType);
        BuiltInSnippets.Clear();
        foreach (var s in builtIn) BuiltInSnippets.Add(s);
    }

    partial void OnSelectedSnippetChanged(QuerySnippet? value)
    {
        if (value is not null)
        {
            EditName = value.Name;
            EditSql = value.Sql;
            EditDescription = value.Description;
            EditTags = value.Tags;
        }
    }

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadAsync();
            return;
        }
        var results = await _snippetService.SearchAsync(SearchText);
        Snippets.Clear();
        foreach (var s in results) Snippets.Add(s);
        StatusMessage = $"Found {Snippets.Count} snippet(s).";
    }

    [RelayCommand]
    private void NewSnippet()
    {
        SelectedSnippet = null;
        EditName = string.Empty;
        EditSql = string.Empty;
        EditDescription = string.Empty;
        EditTags = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private void EditSnippet()
    {
        if (SelectedSnippet is not null)
            IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveSnippet()
    {
        if (string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(EditSql))
        {
            StatusMessage = "Name and SQL are required.";
            return;
        }

        var snippet = SelectedSnippet ?? new QuerySnippet();
        snippet.Name = EditName;
        snippet.Sql = EditSql;
        snippet.Description = EditDescription;
        snippet.Tags = EditTags;

        await _snippetService.SaveAsync(snippet);
        IsEditing = false;
        await LoadAsync();
        StatusMessage = $"Snippet '{snippet.Name}' saved.";
    }

    [RelayCommand]
    private async Task DeleteSnippet()
    {
        if (SelectedSnippet is null) return;
        await _snippetService.DeleteAsync(SelectedSnippet.Id);
        await LoadAsync();
        StatusMessage = "Snippet deleted.";
    }

    [RelayCommand]
    private void InsertSnippet(QuerySnippet? snippet)
    {
        var sql = snippet?.Sql ?? SelectedSnippet?.Sql;
        if (sql is not null)
        {
            InsertIntoEditor?.Invoke(sql);
            StatusMessage = "Snippet inserted into editor.";
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        if (SelectedSnippet is not null)
            OnSelectedSnippetChanged(SelectedSnippet);
    }

    /// <summary>Saves the given SQL as a new snippet (called from query editor "Save as Snippet").</summary>
    public async Task SaveCurrentAsSnippetAsync(string sql, string name = "")
    {
        EditName = name;
        EditSql = sql;
        EditDescription = string.Empty;
        EditTags = string.Empty;
        IsEditing = true;

        if (!string.IsNullOrWhiteSpace(name))
        {
            await SaveSnippet();
        }
    }
}

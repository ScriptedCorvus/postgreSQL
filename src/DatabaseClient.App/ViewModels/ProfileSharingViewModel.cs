using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the Profile Sharing (Collaboration) dialog.
/// Supports export/import of full profiles, connections, and snippets.
/// </summary>
public partial class ProfileSharingViewModel : ViewModelBase
{
    private readonly IProfileSharingService _sharingService;
    private readonly IConnectionRepository _connectionRepository;

    // ── Mode Selection ─────────────────────────────────────────────────────

    [ObservableProperty]
    private string _selectedMode = "Export Profile";

    public string[] Modes { get; } = [
        "Export Profile",
        "Import Profile",
        "Export Connections",
        "Import Connections",
        "Export Snippets",
        "Import Snippets"
    ];

    // ── Common ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _masterKey = string.Empty;

    [ObservableProperty]
    private bool _replaceExisting;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    // ── Connection Selection ───────────────────────────────────────────────

    public ObservableCollection<SelectableConnection> SelectableConnections { get; } = [];

    public ProfileSharingViewModel(IProfileSharingService sharingService, IConnectionRepository connectionRepository)
    {
        _sharingService = sharingService;
        _connectionRepository = connectionRepository;
        _ = LoadConnectionsAsync();
    }

    private async Task LoadConnectionsAsync()
    {
        var connections = await _connectionRepository.GetAllAsync();
        SelectableConnections.Clear();
        foreach (var conn in connections)
        {
            SelectableConnections.Add(new SelectableConnection
            {
                Connection = conn,
                IsSelected = true
            });
        }
    }

    /// <summary>Delegate for file browsing (set by the View).</summary>
    public Func<string, string, string?>? BrowseFile { get; set; }

    [RelayCommand]
    private void BrowseFilePath()
    {
        var filter = SelectedMode switch
        {
            "Export Profile" or "Import Profile" => "Profile|*.dbcprofile",
            "Export Connections" or "Import Connections" => "Connections|*.dbcconnection",
            "Export Snippets" or "Import Snippets" => "Snippets|*.dbcsnippets",
            _ => "All Files|*.*"
        };

        var mode = SelectedMode.StartsWith("Export") ? "save" : "open";
        var result = BrowseFile?.Invoke(filter, mode);
        if (result != null)
            FilePath = result;
    }

    [RelayCommand]
    private async Task Execute()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            StatusMessage = "Please select a file path.";
            IsSuccess = false;
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            switch (SelectedMode)
            {
                case "Export Profile":
                    await _sharingService.ExportProfileAsync(FilePath, MasterKey);
                    StatusMessage = "Profile exported successfully.";
                    break;

                case "Import Profile":
                    await _sharingService.ImportProfileAsync(FilePath, MasterKey, ReplaceExisting);
                    StatusMessage = "Profile imported successfully. Restart to apply.";
                    break;

                case "Export Connections":
                    var selectedConns = SelectableConnections
                        .Where(s => s.IsSelected)
                        .Select(s => s.Connection)
                        .ToList();

                    if (selectedConns.Count == 0)
                    {
                        StatusMessage = "Please select at least one connection.";
                        IsSuccess = false;
                        return;
                    }

                    await _sharingService.ExportConnectionsAsync(FilePath, selectedConns, MasterKey);
                    StatusMessage = $"Exported {selectedConns.Count} connection(s).";
                    break;

                case "Import Connections":
                    var imported = await _sharingService.ImportConnectionsAsync(FilePath, MasterKey);
                    foreach (var conn in imported)
                        await _connectionRepository.SaveAsync(conn);
                    StatusMessage = $"Imported {imported.Count} connection(s).";
                    break;

                case "Export Snippets":
                    await _sharingService.ExportSnippetsAsync(FilePath);
                    StatusMessage = "Snippets exported successfully.";
                    break;

                case "Import Snippets":
                    await _sharingService.ImportSnippetsAsync(FilePath, !ReplaceExisting);
                    StatusMessage = "Snippets imported successfully.";
                    break;
            }

            IsSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>
/// Wrapper for a connection with selection state.
/// </summary>
public partial class SelectableConnection : ObservableObject
{
    public ConnectionInfo Connection { get; set; } = null!;

    [ObservableProperty]
    private bool _isSelected = true;
}

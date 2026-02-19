using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DatabaseClient.Core.ViewModels;

/// <summary>
/// Base class for all tab-hosted ViewModels (query, table viewer, designer, etc.).
/// </summary>
public abstract partial class TabViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private string _title = "New Tab";

    [ObservableProperty]
    private string _toolTip = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Unique identifier for this tab instance.</summary>
    public Guid TabId { get; } = Guid.NewGuid();

    /// <summary>Icon kind name for the tab header (MaterialDesign PackIconKind).</summary>
    public virtual string TabIconKind => "CodeBraces";

    /// <summary>Raised when the tab requests to be closed.</summary>
    public event EventHandler? CloseRequested;

    [RelayCommand]
    private void RequestClose()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called when the tab is about to be closed. Return false to cancel.</summary>
    public virtual Task<bool> CanCloseAsync()
    {
        return Task.FromResult(true);
    }
}

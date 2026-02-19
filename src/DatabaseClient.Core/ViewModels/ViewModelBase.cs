using CommunityToolkit.Mvvm.ComponentModel;

namespace DatabaseClient.Core.ViewModels;

/// <summary>
/// Base class for all ViewModels in the application.
/// Uses CommunityToolkit.Mvvm source generators.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;
}

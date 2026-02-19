using System.Windows;
using System.Windows.Controls;
using DatabaseClient.App.ViewModels;

namespace DatabaseClient.App.Views;

/// <summary>
/// Code-behind for the QueryHistoryTabView.
/// </summary>
public partial class QueryHistoryTabView : UserControl
{
    public QueryHistoryTabView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is QueryHistoryTabViewModel vm)
        {
            await vm.LoadHistoryCommand.ExecuteAsync(null);
        }
    }
}

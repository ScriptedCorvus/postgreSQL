using System.Windows.Controls;

namespace DatabaseClient.App.Views;

public partial class LogViewerTabView : UserControl
{
    public LogViewerTabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ViewModels.LogViewerTabViewModel vm)
        {
            // Wire auto-scroll behavior
            vm.FilteredEntries.CollectionChanged += (_, _) =>
            {
                if (vm.AutoScroll && LogListView.Items.Count > 0)
                {
                    LogListView.ScrollIntoView(LogListView.Items[^1]);
                }
            };
        }
    }
}

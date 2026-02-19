using System.Windows;
using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace DatabaseClient.App.Views;

public partial class DataExportDialog : MetroWindow
{
    public DataExportDialog()
    {
        InitializeComponent();
    }

    private void BrowseExportFile_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.DataExportWizardViewModel vm) return;

        var filter = vm.SelectedFormat switch
        {
            "CSV" => "CSV Files (*.csv)|*.csv",
            "Excel (.xlsx)" => "Excel Files (*.xlsx)|*.xlsx",
            "JSON" => "JSON Files (*.json)|*.json",
            "XML" => "XML Files (*.xml)|*.xml",
            "SQL (INSERT)" => "SQL Files (*.sql)|*.sql",
            "Markdown" => "Markdown Files (*.md)|*.md",
            "HTML" => "HTML Files (*.html)|*.html",
            _ => "All Files (*.*)|*.*"
        };

        var defaultExt = vm.SelectedFormat switch
        {
            "CSV" => ".csv",
            "Excel (.xlsx)" => ".xlsx",
            "JSON" => ".json",
            "XML" => ".xml",
            "SQL (INSERT)" => ".sql",
            "Markdown" => ".md",
            "HTML" => ".html",
            _ => ".txt"
        };

        var dialog = new SaveFileDialog
        {
            Title = "Export Data",
            Filter = filter + "|All Files (*.*)|*.*",
            DefaultExt = defaultExt,
            FileName = "export" + defaultExt
        };

        if (dialog.ShowDialog() == true)
            vm.OutputFilePath = dialog.FileName;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

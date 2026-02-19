using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace DatabaseClient.App.Views;

public partial class DataImportDialog : MetroWindow
{
    public DataImportDialog()
    {
        InitializeComponent();
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select file to import",
            Filter = "All Supported|*.csv;*.json;*.xml;*.sql|CSV Files|*.csv|JSON Files|*.json|XML Files|*.xml|SQL Files|*.sql|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            if (DataContext is ViewModels.DataImportWizardViewModel vm)
            {
                vm.FilePath = dialog.FileName;

                // Auto-detect format from extension
                var ext = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
                vm.SelectedFormat = ext switch
                {
                    ".csv" => "CSV",
                    ".json" => "JSON",
                    ".xml" => "XML",
                    ".sql" => "SQL",
                    _ => vm.SelectedFormat
                };

                vm.LoadPreviewCommand.Execute(null);
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// Converts step number match to Bold/Normal font weight for step indicator.
/// </summary>
public class StepFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepStr && int.TryParse(stepStr, out int step))
        {
            return currentStep == step ? FontWeights.Bold : FontWeights.Normal;
        }
        return FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

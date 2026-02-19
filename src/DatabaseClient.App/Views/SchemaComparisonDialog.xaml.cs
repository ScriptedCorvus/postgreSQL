using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DatabaseClient.App.ViewModels;
using MahApps.Metro.Controls;

namespace DatabaseClient.App.Views;

public partial class SchemaComparisonDialog : MetroWindow
{
    public SchemaComparisonDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SchemaGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is SchemaObjectDiff diff)
        {
            e.Row.Background = diff.Status switch
            {
                "Only in Source" => new SolidColorBrush(Color.FromArgb(60, 76, 175, 80)),
                "Only in Target" => new SolidColorBrush(Color.FromArgb(60, 244, 67, 54)),
                "Modified" => new SolidColorBrush(Color.FromArgb(60, 255, 152, 0)),
                "Identical" => Brushes.Transparent,
                _ => Brushes.Transparent
            };
        }
    }
}

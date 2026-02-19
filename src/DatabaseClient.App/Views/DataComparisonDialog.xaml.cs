using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.Controls;

namespace DatabaseClient.App.Views;

public partial class DataComparisonDialog : MetroWindow
{
    public DataComparisonDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Color-codes rows based on their Status column value.
    /// </summary>
    private void ResultsGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is DataRowView rowView)
        {
            var status = rowView["Status"]?.ToString();
            e.Row.Background = status switch
            {
                "Only in Source" => new SolidColorBrush(Color.FromArgb(60, 76, 175, 80)),   // Green
                "Only in Target" => new SolidColorBrush(Color.FromArgb(60, 244, 67, 54)),    // Red
                "Modified" => new SolidColorBrush(Color.FromArgb(60, 255, 152, 0)),           // Orange
                "Identical" => Brushes.Transparent,
                _ => Brushes.Transparent
            };
        }
    }
}

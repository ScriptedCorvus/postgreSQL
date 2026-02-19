using System.Windows;
using MahApps.Metro.Controls;

namespace DatabaseClient.App.Views;

/// <summary>
/// User-friendly error dialog with expandable technical details.
/// </summary>
public partial class ErrorDialog : MetroWindow
{
    private readonly string _technicalDetails;

    public ErrorDialog(string title, string friendlyMessage, string technicalDetails, bool isFatal = false)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = friendlyMessage;
        _technicalDetails = technicalDetails;
        DetailsTextBox.Text = technicalDetails;

        if (isFatal)
        {
            ErrorIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CloseOctagon;
            CloseButton.Content = "Exit Application";
        }
    }

    private void DetailsToggle_Changed(object sender, RoutedEventArgs e)
    {
        var isExpanded = DetailsToggle.IsChecked == true;
        DetailsTextBox.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        DetailsToggle.Content = isExpanded ? "Hide Details" : "Show Details";

        // Expand window height when details shown
        if (isExpanded && Height < 480)
            Height = 480;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText($"{MessageText.Text}\n\n{_technicalDetails}");
        }
        catch
        {
            // Clipboard access can fail in rare cases
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}

using System.Windows;
using MahApps.Metro.Controls;

namespace DatabaseClient.App.Views;

public partial class MigrationWizardDialog : MetroWindow
{
    public MigrationWizardDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

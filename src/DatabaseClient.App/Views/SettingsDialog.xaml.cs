using DatabaseClient.App.ViewModels;
using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace DatabaseClient.App.Views;

/// <summary>
/// Code-behind for the Settings dialog.
/// </summary>
public partial class SettingsDialog : MetroWindow
{
    public SettingsDialog(SettingsDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Wire folder browser delegate for shared project directory
        viewModel.BrowseFolderDialog = () =>
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select Shared Project Directory"
            };
            return dlg.ShowDialog(this) == true ? dlg.FolderName : null;
        };
    }

    private void OkButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsDialogViewModel vm && vm.IsConfirmed)
        {
            DialogResult = true;
        }
        else
        {
            // Wait for the command to finish, then check
            // The ConfirmCommand is async so we close after it completes
            if (DataContext is SettingsDialogViewModel vm2)
            {
                // Command already executed via binding, check result
                DialogResult = true;
            }
        }
    }

    private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

using System.Windows;
using DatabaseClient.App.ViewModels;
using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace DatabaseClient.App.Views;

/// <summary>
/// Code-behind for the Profile Sharing (Collaboration) dialog.
/// </summary>
public partial class ProfileSharingDialog : MetroWindow
{
    public ProfileSharingDialog(ProfileSharingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.BrowseFile = BrowseFileDelegate;
    }

    private string? BrowseFileDelegate(string filter, string mode)
    {
        if (mode == "save")
        {
            var dialog = new SaveFileDialog { Filter = filter };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }
        else
        {
            var dialog = new OpenFileDialog { Filter = filter };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileSharingViewModel vm)
            vm.BrowseFilePathCommand.Execute(null);
    }

    private void MasterKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileSharingViewModel vm)
            vm.MasterKey = MasterKeyBox.Password;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

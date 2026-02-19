using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;

namespace DatabaseClient.App.Views;

public partial class UserManagementDialog : MetroWindow
{
    public UserManagementDialog()
    {
        InitializeComponent();
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.UserManagementViewModel vm)
        {
            vm.NewPassword = ((PasswordBox)sender).Password;
        }
    }
}

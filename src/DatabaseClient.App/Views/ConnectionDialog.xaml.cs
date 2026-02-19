using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using DatabaseClient.App.ViewModels;
using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace DatabaseClient.App.Views;

/// <summary>
/// Code-behind for ConnectionDialog.
/// </summary>
public partial class ConnectionDialog : MetroWindow
{
    public ConnectionDialog(ConnectionDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (_, _) =>
        {
            DialogResult = viewModel.DialogResult;
            Close();
        };

        // Sync PasswordBox (cannot be bound directly for security)
        PasswordBox.PasswordChanged += (_, _) =>
        {
            if (DataContext is ConnectionDialogViewModel vm)
                vm.Password = PasswordBox.Password;
        };

        // Sync SSH PasswordBox
        SshPasswordBox.PasswordChanged += (_, _) =>
        {
            if (DataContext is ConnectionDialogViewModel vm)
                vm.SshPassword = SshPasswordBox.Password;
        };
    }

    private void BrowseSqliteFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select SQLite Database File",
            Filter = "SQLite Files (*.db;*.sqlite;*.sqlite3)|*.db;*.sqlite;*.sqlite3|All Files (*.*)|*.*",
            CheckFileExists = false
        };

        if (dialog.ShowDialog() == true && DataContext is ConnectionDialogViewModel vm)
        {
            vm.DatabaseFilePath = dialog.FileName;
        }
    }

    private void BrowseSshKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select SSH Private Key File",
            Filter = "Key Files (*.pem;*.ppk;*.key)|*.pem;*.ppk;*.key|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true && DataContext is ConnectionDialogViewModel vm)
        {
            vm.SshPrivateKeyPath = dialog.FileName;
        }
    }

    private void BrowseSslCa_Click(object sender, RoutedEventArgs e)
    {
        BrowseCertificate("Select CA Certificate", path =>
        {
            if (DataContext is ConnectionDialogViewModel vm) vm.SslCaCertificatePath = path;
        });
    }

    private void BrowseSslClientCert_Click(object sender, RoutedEventArgs e)
    {
        BrowseCertificate("Select Client Certificate", path =>
        {
            if (DataContext is ConnectionDialogViewModel vm) vm.SslClientCertificatePath = path;
        });
    }

    private void BrowseSslClientKey_Click(object sender, RoutedEventArgs e)
    {
        BrowseCertificate("Select Client Key", path =>
        {
            if (DataContext is ConnectionDialogViewModel vm) vm.SslClientKeyPath = path;
        });
    }

    private void BrowseCertificate(string title, Action<string> setter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Certificate Files (*.pem;*.crt;*.key;*.p12;*.pfx)|*.pem;*.crt;*.key;*.p12;*.pfx|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
            setter(dialog.FileName);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}

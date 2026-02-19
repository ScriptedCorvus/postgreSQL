using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the connection creation/edit dialog.
/// </summary>
public partial class ConnectionDialogViewModel : ViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private readonly IConnectionTemplateService? _templateService;

    [ObservableProperty]
    private string _connectionName = "New Connection";

    [ObservableProperty]
    private DatabaseType _selectedDatabaseType = DatabaseType.PostgreSQL;

    [ObservableProperty]
    private string _host = "localhost";

    [ObservableProperty]
    private int _port = 5432;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _defaultDatabase = string.Empty;

    [ObservableProperty]
    private string _databaseFilePath = string.Empty;

    [ObservableProperty]
    private bool _useSsl;

    [ObservableProperty]
    private string _sslMode = "Require";

    public string[] SslModes { get; } = ["Disable", "Prefer", "Require", "VerifyCA", "VerifyFull"];

    [ObservableProperty]
    private string _sslCaCertificatePath = string.Empty;

    [ObservableProperty]
    private string _sslClientCertificatePath = string.Empty;

    [ObservableProperty]
    private string _sslClientKeyPath = string.Empty;

    // Authentication
    [ObservableProperty]
    private string _authenticationMethod = "Default";

    public string[] PostgreSqlAuthMethods { get; } = ["Default", "SCRAM-SHA-256", "MD5", "Password", "Certificate"];
    public string[] MySqlAuthMethods { get; } = ["Default", "mysql_native_password", "caching_sha2_password", "sha256_password"];
    public string[] AuthMethods => SelectedDatabaseType switch
    {
        DatabaseType.PostgreSQL => PostgreSqlAuthMethods,
        DatabaseType.MySQL or DatabaseType.MariaDB => MySqlAuthMethods,
        _ => ["Default"]
    };

    // SSH Tunnel
    [ObservableProperty]
    private bool _useSshTunnel;

    [ObservableProperty]
    private string _sshHost = string.Empty;

    [ObservableProperty]
    private int _sshPort = 22;

    [ObservableProperty]
    private string _sshUsername = string.Empty;

    [ObservableProperty]
    private string _sshPassword = string.Empty;

    [ObservableProperty]
    private string _sshPrivateKeyPath = string.Empty;

    [ObservableProperty]
    private string _colorTag = "#007ACC";

    [ObservableProperty]
    private string _testResultMessage = string.Empty;

    [ObservableProperty]
    private bool _testSucceeded;

    [ObservableProperty]
    private string _cloudHelpText = string.Empty;

    [ObservableProperty]
    private string _cloudDocumentationUrl = string.Empty;

    [ObservableProperty]
    private bool _isSqliteSelected;

    // Template support
    [ObservableProperty]
    private bool _showTemplateSelector = true;

    [ObservableProperty]
    private ConnectionTemplate? _selectedTemplate;

    public ObservableCollection<ConnectionTemplate> Templates { get; } = new();

    public ObservableCollection<string> TemplateCategories { get; } = new();

    [ObservableProperty]
    private string? _selectedTemplateCategory;

    public ObservableCollection<ConnectionTemplate> FilteredTemplates { get; } = new();

    public ObservableCollection<DatabaseType> DatabaseTypes { get; } =
    [
        DatabaseType.PostgreSQL,
        DatabaseType.MySQL,
        DatabaseType.MariaDB,
        DatabaseType.SQLite
    ];

    /// <summary>True if the user confirmed (OK), false if cancelled.</summary>
    public bool DialogResult { get; private set; }

    /// <summary>The resulting ConnectionInfo after dialog is confirmed.</summary>
    public ConnectionInfo? ResultConnection { get; private set; }

    /// <summary>Event raised when the dialog should close.</summary>
    public event EventHandler? RequestClose;

    public ConnectionDialogViewModel(IConnectionManager connectionManager, IConnectionTemplateService? templateService = null)
    {
        _connectionManager = connectionManager;
        _templateService = templateService;
        LoadTemplates();
    }

    /// <summary>Load from an existing connection for editing.</summary>
    public void LoadFrom(ConnectionInfo conn)
    {
        ShowTemplateSelector = false; // Hide templates when editing
        ConnectionName = conn.Name;
        SelectedDatabaseType = conn.DatabaseType;
        Host = conn.Host;
        Port = conn.Port;
        Username = conn.Username;
        Password = conn.Password;
        DefaultDatabase = conn.DefaultDatabase;
        DatabaseFilePath = conn.DatabaseFilePath;
        UseSsl = conn.UseSsl;
        SslMode = conn.SslMode;
        SslCaCertificatePath = conn.SslCaCertificatePath;
        SslClientCertificatePath = conn.SslClientCertificatePath;
        SslClientKeyPath = conn.SslClientKeyPath;
        AuthenticationMethod = conn.AuthenticationMethod;
        UseSshTunnel = conn.UseSshTunnel;
        SshHost = conn.SshHost;
        SshPort = conn.SshPort;
        SshUsername = conn.SshUsername;
        SshPassword = conn.SshPassword;
        SshPrivateKeyPath = conn.SshPrivateKeyPath;
        ColorTag = conn.ColorTag;
    }

    partial void OnSelectedDatabaseTypeChanged(DatabaseType value)
    {
        Port = ConnectionInfo.GetDefaultPort(value);
        IsSqliteSelected = value == DatabaseType.SQLite;
        AuthenticationMethod = "Default";
        OnPropertyChanged(nameof(AuthMethods));
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        IsBusy = true;
        TestResultMessage = "Testing connection...";
        TestSucceeded = false;

        try
        {
            var connInfo = BuildConnectionInfo();
            var success = await _connectionManager.GetProviderAsync(connInfo)
                .ContinueWith(async t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        await _connectionManager.CloseConnectionAsync(connInfo.Id);
                        return true;
                    }
                    return false;
                }).Unwrap();

            TestSucceeded = success;
            TestResultMessage = success ? "Connection successful!" : "Connection failed.";
        }
        catch (Exception ex)
        {
            TestSucceeded = false;
            TestResultMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        ResultConnection = BuildConnectionInfo();
        DialogResult = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private ConnectionInfo BuildConnectionInfo()
    {
        return new ConnectionInfo
        {
            Name = ConnectionName,
            DatabaseType = SelectedDatabaseType,
            Host = Host,
            Port = Port,
            Username = Username,
            Password = Password,
            DefaultDatabase = DefaultDatabase,
            DatabaseFilePath = DatabaseFilePath,
            UseSsl = UseSsl,
            SslMode = SslMode,
            SslCaCertificatePath = SslCaCertificatePath,
            SslClientCertificatePath = SslClientCertificatePath,
            SslClientKeyPath = SslClientKeyPath,
            AuthenticationMethod = AuthenticationMethod,
            UseSshTunnel = UseSshTunnel,
            SshHost = SshHost,
            SshPort = SshPort,
            SshUsername = SshUsername,
            SshPassword = SshPassword,
            SshPrivateKeyPath = SshPrivateKeyPath,
            ColorTag = ColorTag,
        };
    }

    private void LoadTemplates()
    {
        if (_templateService == null) return;

        Templates.Clear();
        TemplateCategories.Clear();
        FilteredTemplates.Clear();

        foreach (var t in _templateService.GetTemplates())
            Templates.Add(t);

        TemplateCategories.Add("All");
        foreach (var cat in _templateService.GetCategories())
            TemplateCategories.Add(cat);

        SelectedTemplateCategory = "All";
    }

    partial void OnSelectedTemplateCategoryChanged(string? value)
    {
        FilteredTemplates.Clear();
        var source = (value == null || value == "All")
            ? Templates
            : Templates.Where(t => t.Category == value);

        foreach (var t in source)
            FilteredTemplates.Add(t);
    }

    [RelayCommand]
    private void ApplyTemplate(ConnectionTemplate? template)
    {
        if (template == null) return;

        SelectedTemplate = template;
        ConnectionName = $"New {template.Name}";
        SelectedDatabaseType = template.DatabaseType;
        Host = template.Host;
        Port = template.Port;
        Username = template.Username;
        DefaultDatabase = template.DefaultDatabase;
        UseSsl = template.UseSsl;
        ColorTag = template.ColorTag;
        CloudHelpText = template.HelpText;
        CloudDocumentationUrl = template.DocumentationUrl;
        ShowTemplateSelector = false;
    }

    [RelayCommand]
    private void ShowTemplates()
    {
        ShowTemplateSelector = true;
    }

    [RelayCommand]
    private void SkipTemplate()
    {
        ShowTemplateSelector = false;
    }

    [RelayCommand]
    private async Task SaveAsTemplate()
    {
        if (_templateService == null) return;

        var conn = BuildConnectionInfo();
        _templateService.CreateFromConnection(conn, ConnectionName);
        await _templateService.SaveAsync();
        LoadTemplates();
    }
}

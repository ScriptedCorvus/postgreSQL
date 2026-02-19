using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the User &amp; Permission Management dialog.
/// Supports PostgreSQL and MySQL/MariaDB user management.
/// </summary>
public partial class UserManagementViewModel : ViewModelBase
{
    private readonly IConnectionManager _connectionManager;
    private readonly ConnectionInfo _connectionInfo;
    private readonly string _database;

    public ObservableCollection<DatabaseUser> Users { get; } = [];

    [ObservableProperty]
    private DatabaseUser? _selectedUser;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private bool _canLogin = true;

    [ObservableProperty]
    private bool _canCreateDb;

    [ObservableProperty]
    private bool _isSuperuser;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<PermissionItem> Permissions { get; } = [];

    public bool IsPostgreSQL => _connectionInfo.DatabaseType == DatabaseType.PostgreSQL;
    public bool IsMySql => _connectionInfo.DatabaseType is DatabaseType.MySQL or DatabaseType.MariaDB;
    public bool IsSQLite => _connectionInfo.DatabaseType == DatabaseType.SQLite;

    public UserManagementViewModel(IConnectionManager connectionManager, ConnectionInfo connectionInfo, string database)
    {
        _connectionManager = connectionManager;
        _connectionInfo = connectionInfo;
        _database = database;
        _ = LoadUsersAsync();
    }

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        Users.Clear();
        IsBusy = true;

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);

            if (IsPostgreSQL)
            {
                var result = await provider.ExecuteQueryAsync(
                    "SELECT rolname, rolsuper, rolinherit, rolcreaterole, rolcreatedb, rolcanlogin, rolreplication " +
                    "FROM pg_roles ORDER BY rolname");

                if (result.IsSuccessful && result.ResultSet != null)
                {
                    foreach (DataRow row in result.ResultSet.Rows)
                    {
                        Users.Add(new DatabaseUser
                        {
                            Username = row["rolname"]?.ToString() ?? "",
                            IsSuperuser = row["rolsuper"]?.ToString() == "True",
                            CanLogin = row["rolcanlogin"]?.ToString() == "True",
                            CanCreateDb = row["rolcreatedb"]?.ToString() == "True",
                            CanCreateRole = row["rolcreaterole"]?.ToString() == "True"
                        });
                    }
                }
            }
            else if (IsMySql)
            {
                var result = await provider.ExecuteQueryAsync("SELECT User, Host FROM mysql.user ORDER BY User");
                if (result.IsSuccessful && result.ResultSet != null)
                {
                    foreach (DataRow row in result.ResultSet.Rows)
                    {
                        Users.Add(new DatabaseUser
                        {
                            Username = row["User"]?.ToString() ?? "",
                            Host = row["Host"]?.ToString() ?? "%"
                        });
                    }
                }
            }

            StatusMessage = $"Loaded {Users.Count} user(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedUserChanged(DatabaseUser? value)
    {
        if (value != null)
            _ = LoadPermissionsAsync(value);
    }

    private async Task LoadPermissionsAsync(DatabaseUser user)
    {
        Permissions.Clear();

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);

            if (IsPostgreSQL)
            {
                var result = await provider.ExecuteQueryAsync(
                    $"SELECT table_name, privilege_type FROM information_schema.table_privileges " +
                    $"WHERE grantee = '{user.Username}' AND table_catalog = '{_database}' ORDER BY table_name, privilege_type");

                if (result.IsSuccessful && result.ResultSet != null)
                {
                    foreach (DataRow row in result.ResultSet.Rows)
                    {
                        Permissions.Add(new PermissionItem
                        {
                            ObjectName = row["table_name"]?.ToString() ?? "",
                            Privilege = row["privilege_type"]?.ToString() ?? ""
                        });
                    }
                }
            }
            else if (IsMySql)
            {
                var result = await provider.ExecuteQueryAsync($"SHOW GRANTS FOR '{user.Username}'@'{user.Host}'");
                if (result.IsSuccessful && result.ResultSet != null)
                {
                    foreach (DataRow row in result.ResultSet.Rows)
                    {
                        Permissions.Add(new PermissionItem
                        {
                            ObjectName = "Grant",
                            Privilege = row[0]?.ToString() ?? ""
                        });
                    }
                }
            }
        }
        catch { /* ignore permission errors for system users */ }
    }

    [RelayCommand]
    private async Task CreateUser()
    {
        if (string.IsNullOrWhiteSpace(NewUsername))
        {
            StatusMessage = "Username is required.";
            return;
        }

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            string sql;

            if (IsPostgreSQL)
            {
                var opts = new System.Collections.Generic.List<string>();
                if (CanLogin) opts.Add("LOGIN");
                if (CanCreateDb) opts.Add("CREATEDB");
                if (IsSuperuser) opts.Add("SUPERUSER");
                if (!string.IsNullOrEmpty(NewPassword))
                    opts.Add($"PASSWORD '{NewPassword.Replace("'", "''")}'");

                sql = $"CREATE ROLE \"{NewUsername}\" {string.Join(" ", opts)}";
            }
            else
            {
                sql = $"CREATE USER '{NewUsername}'@'%' IDENTIFIED BY '{NewPassword.Replace("'", "''")}'";
            }

            await provider.ExecuteNonQueryAsync(sql);
            StatusMessage = $"User '{NewUsername}' created successfully.";
            NewUsername = string.Empty;
            NewPassword = string.Empty;
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating user: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DropUser()
    {
        if (SelectedUser == null) return;

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            string sql;

            if (IsPostgreSQL)
                sql = $"DROP ROLE IF EXISTS \"{SelectedUser.Username}\"";
            else
                sql = $"DROP USER IF EXISTS '{SelectedUser.Username}'@'{SelectedUser.Host}'";

            await provider.ExecuteNonQueryAsync(sql);
            StatusMessage = $"User '{SelectedUser.Username}' dropped.";
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GrantPrivilege()
    {
        if (SelectedUser == null) return;

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            string sql;

            if (IsPostgreSQL)
                sql = $"GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO \"{SelectedUser.Username}\"";
            else
                sql = $"GRANT ALL PRIVILEGES ON {_database}.* TO '{SelectedUser.Username}'@'{SelectedUser.Host}'";

            await provider.ExecuteNonQueryAsync(sql);
            StatusMessage = $"Granted all privileges to '{SelectedUser.Username}'.";
            await LoadPermissionsAsync(SelectedUser);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RevokePrivilege()
    {
        if (SelectedUser == null) return;

        try
        {
            var provider = await _connectionManager.GetProviderAsync(_connectionInfo);
            string sql;

            if (IsPostgreSQL)
                sql = $"REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM \"{SelectedUser.Username}\"";
            else
                sql = $"REVOKE ALL PRIVILEGES ON {_database}.* FROM '{SelectedUser.Username}'@'{SelectedUser.Host}'";

            await provider.ExecuteNonQueryAsync(sql);
            StatusMessage = $"Revoked all privileges from '{SelectedUser.Username}'.";
            await LoadPermissionsAsync(SelectedUser);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}

/// <summary>Represents a database user/role.</summary>
public class DatabaseUser
{
    public string Username { get; set; } = string.Empty;
    public string Host { get; set; } = "%";
    public bool IsSuperuser { get; set; }
    public bool CanLogin { get; set; }
    public bool CanCreateDb { get; set; }
    public bool CanCreateRole { get; set; }
}

/// <summary>Represents a permission granted to a user.</summary>
public class PermissionItem
{
    public string ObjectName { get; set; } = string.Empty;
    public string Privilege { get; set; } = string.Empty;
}

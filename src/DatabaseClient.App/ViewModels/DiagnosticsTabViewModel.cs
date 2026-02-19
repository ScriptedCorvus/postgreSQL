using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.ViewModels;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for the application diagnostics panel.
/// Shows .NET version, OS info, provider versions, memory, open connections, and recent errors.
/// </summary>
public partial class DiagnosticsTabViewModel : TabViewModelBase
{
    private readonly IConnectionManager _connectionManager;

    public override string TabIconKind => "InformationOutline";

    // --- System Info ---
    [ObservableProperty] private string _applicationVersion = "1.0.0";
    [ObservableProperty] private string _dotNetVersion = RuntimeInformation.FrameworkDescription;
    [ObservableProperty] private string _osVersion = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
    [ObservableProperty] private string _processArchitecture = RuntimeInformation.ProcessArchitecture.ToString();

    // --- Memory ---
    [ObservableProperty] private string _totalMemory = "-";
    [ObservableProperty] private string _gen0Collections = "-";
    [ObservableProperty] private string _gen1Collections = "-";
    [ObservableProperty] private string _gen2Collections = "-";
    [ObservableProperty] private string _workingSet = "-";

    // --- Connections ---
    [ObservableProperty] private int _activeConnectionCount;
    public ObservableCollection<string> ActiveConnectionIds { get; } = [];

    // --- Provider Versions ---
    public ObservableCollection<ProviderInfo> ProviderVersions { get; } = [];

    // --- Recent Errors ---
    public ObservableCollection<string> RecentErrors { get; } = [];

    public DiagnosticsTabViewModel(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        Title = "Diagnostics";
        ToolTip = "Application diagnostics and system information";
    }

    [RelayCommand]
    private Task Refresh()
    {
        // Application
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        ApplicationVersion = assembly?.GetName().Version?.ToString() ?? "1.0.0";

        // Memory
        var memInfo = GC.GetGCMemoryInfo();
        TotalMemory = FormatBytes(GC.GetTotalMemory(false));
        Gen0Collections = GC.CollectionCount(0).ToString();
        Gen1Collections = GC.CollectionCount(1).ToString();
        Gen2Collections = GC.CollectionCount(2).ToString();

        using var process = Process.GetCurrentProcess();
        WorkingSet = FormatBytes(process.WorkingSet64);

        // Active connections
        var ids = _connectionManager.GetActiveConnectionIds();
        ActiveConnectionCount = ids.Count;
        ActiveConnectionIds.Clear();
        foreach (var id in ids)
            ActiveConnectionIds.Add(id.ToString());

        // Provider versions
        ProviderVersions.Clear();
        ProviderVersions.Add(GetAssemblyVersion("Npgsql", "PostgreSQL"));
        ProviderVersions.Add(GetAssemblyVersion("MySqlConnector", "MySQL/MariaDB"));
        ProviderVersions.Add(GetAssemblyVersion("Microsoft.Data.Sqlite", "SQLite"));
        ProviderVersions.Add(GetAssemblyVersion("Renci.SshNet", "SSH.NET"));
        ProviderVersions.Add(GetAssemblyVersion("ClosedXML", "ClosedXML (Excel)"));
        ProviderVersions.Add(GetAssemblyVersion("ICSharpCode.AvalonEdit", "AvalonEdit"));
        ProviderVersions.Add(GetAssemblyVersion("Serilog", "Serilog"));

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Database Client Diagnostics ===");
        sb.AppendLine($"Version: {ApplicationVersion}");
        sb.AppendLine($".NET: {DotNetVersion}");
        sb.AppendLine($"OS: {OsVersion}");
        sb.AppendLine($"Architecture: {ProcessArchitecture}");
        sb.AppendLine();
        sb.AppendLine("--- Memory ---");
        sb.AppendLine($"Managed: {TotalMemory}");
        sb.AppendLine($"Working Set: {WorkingSet}");
        sb.AppendLine($"GC Gen0: {Gen0Collections}, Gen1: {Gen1Collections}, Gen2: {Gen2Collections}");
        sb.AppendLine();
        sb.AppendLine("--- Connections ---");
        sb.AppendLine($"Active: {ActiveConnectionCount}");
        foreach (var id in ActiveConnectionIds)
            sb.AppendLine($"  {id}");
        sb.AppendLine();
        sb.AppendLine("--- Providers ---");
        foreach (var prov in ProviderVersions)
            sb.AppendLine($"  {prov.Name}: {prov.Version}");

        System.Windows.Clipboard.SetText(sb.ToString());
    }

    private static ProviderInfo GetAssemblyVersion(string assemblyName, string displayName)
    {
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            return new ProviderInfo
            {
                Name = displayName,
                Version = asm?.GetName().Version?.ToString() ?? "Not loaded"
            };
        }
        catch
        {
            return new ProviderInfo { Name = displayName, Version = "Unknown" };
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:F1} MB",
        _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB"
    };
}

public class ProviderInfo
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
}

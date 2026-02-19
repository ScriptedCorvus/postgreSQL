using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DatabaseClient.App.ViewModels;

public partial class VersionLogViewModel : ObservableObject
{
    public ObservableCollection<VersionEntry> VersionEntries { get; } = new();

    public VersionLogViewModel()
    {
        LoadVersionLog();
    }

    private void LoadVersionLog()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "versions.json");
        if (!File.Exists(path)) return;
        var json = File.ReadAllText(path);
        var entries = JsonSerializer.Deserialize<List<VersionEntry>>(json);
        if (entries != null)
            foreach (var entry in entries)
                VersionEntries.Add(entry);
    }
}

public class VersionEntry
{
    public string Version { get; set; } = "";
    public int Build { get; set; }
    public string Date { get; set; } = "";
    public List<string> Changes { get; set; } = new();
}

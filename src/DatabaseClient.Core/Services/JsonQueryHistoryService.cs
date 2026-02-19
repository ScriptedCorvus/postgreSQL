using System.Text.Json;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Persists query history to a JSON file.
/// </summary>
public class JsonQueryHistoryService : IQueryHistoryService
{
    private readonly string _filePath;
    private readonly ISettingsService _settings;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<QueryHistoryEntry> _entries = [];

    public JsonQueryHistoryService(ISettingsService settings, string? filePath = null)
    {
        _settings = settings;
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseClient", "query_history.json");
    }

    public async Task AddEntryAsync(QueryHistoryEntry entry)
    {
        if (!_settings.Settings.SaveQueryHistory)
            return;

        await LoadIfNeededAsync();

        _entries.Insert(0, entry);

        // Trim to max entries
        var max = _settings.Settings.MaxHistoryEntries;
        if (_entries.Count > max)
            _entries = _entries.Take(max).ToList();

        await PersistAsync();
    }

    public async Task<IReadOnlyList<QueryHistoryEntry>> GetAllAsync()
    {
        await LoadIfNeededAsync();
        return _entries.AsReadOnly();
    }

    public async Task<IReadOnlyList<QueryHistoryEntry>> SearchAsync(string searchText)
    {
        await LoadIfNeededAsync();
        var lower = searchText.ToLowerInvariant();
        return _entries
            .Where(e => e.Sql.Contains(lower, StringComparison.OrdinalIgnoreCase)
                     || e.ConnectionName.Contains(lower, StringComparison.OrdinalIgnoreCase)
                     || e.Database.Contains(lower, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    public async Task ClearAsync()
    {
        _entries.Clear();
        await PersistAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await LoadIfNeededAsync();
        _entries.RemoveAll(e => e.Id == id);
        await PersistAsync();
    }

    public async Task<int> GetCountAsync()
    {
        await LoadIfNeededAsync();
        return _entries.Count;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private bool _loaded;

    private async Task LoadIfNeededAsync()
    {
        if (_loaded) return;

        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _entries = JsonSerializer.Deserialize<List<QueryHistoryEntry>>(json, _jsonOptions) ?? [];
            }
            catch
            {
                _entries = [];
            }
        }

        _loaded = true;
    }

    private async Task PersistAsync()
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_entries, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}

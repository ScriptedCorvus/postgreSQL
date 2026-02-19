using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace DatabaseClient.App.Services;

/// <summary>
/// Manages application localization with runtime language switching.
/// Implements INotifyPropertyChanged so WPF bindings auto-refresh on culture change.
/// Usage in XAML: {Binding Source={x:Static services:LocalizationManager.Instance}, Path=[Menu_NewConnection]}
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _lazy = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _lazy.Value;

    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationManager()
    {
        _resourceManager = new ResourceManager(
            "DatabaseClient.App.Resources.Strings",
            typeof(LocalizationManager).Assembly);
        _currentCulture = CultureInfo.CurrentUICulture;
    }

    /// <summary>
    /// Gets a localized string by key. Used as indexer for XAML binding.
    /// </summary>
    public string this[string key]
    {
        get
        {
            var value = _resourceManager.GetString(key, _currentCulture);
            return value ?? $"[{key}]";
        }
    }

    /// <summary>
    /// Gets the current culture code (e.g. "en", "es").
    /// </summary>
    public string CurrentLanguage => _currentCulture.TwoLetterISOLanguageName;

    /// <summary>
    /// Switches the application language at runtime.
    /// </summary>
    /// <param name="languageCode">ISO language code, e.g. "en" or "es".</param>
    public void SetLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            languageCode = "en";

        var culture = new CultureInfo(languageCode);
        if (_currentCulture.Name == culture.Name)
            return;

        _currentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // Notify all bindings that localized strings have changed
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }

    /// <summary>
    /// Gets a localized string by key (code-behind helper).
    /// </summary>
    public string GetString(string key)
    {
        return this[key];
    }
}

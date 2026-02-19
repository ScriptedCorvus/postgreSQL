using System.Windows;
using System.Windows.Media;
using ControlzEx.Theming;
using DatabaseClient.Core.Interfaces;
using DatabaseClient.Core.Models;
using ThemeMode = DatabaseClient.Core.Models.ThemeMode;

namespace DatabaseClient.App.Services;

/// <summary>
/// Manages runtime theme switching using MahApps ThemeManager.
/// Listens to ISettingsService.SettingsChanged to apply theme/accent changes.
/// </summary>
public class ThemeService
{
    private readonly ISettingsService _settingsService;

    public ThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += (_, _) => ApplyTheme();
    }

    /// <summary>
    /// Applies the current theme and accent color from settings.
    /// </summary>
    public void ApplyTheme()
    {
        var settings = _settingsService.Settings;
        var baseTheme = settings.Theme switch
        {
            ThemeMode.Dark => "Dark",
            ThemeMode.System => IsSystemDarkMode() ? "Dark" : "Light",
            _ => "Light"
        };

        // Try to parse accent color and apply
        try
        {
            var accentColor = (Color)ColorConverter.ConvertFromString(settings.AccentColor);
            
            // Create a runtime theme with the custom accent color
            var theme = RuntimeThemeGenerator.Current.GenerateRuntimeTheme(baseTheme, accentColor);
            if (theme != null)
            {
                ThemeManager.Current.ChangeTheme(Application.Current, theme);
            }
            else
            {
                // Fallback: use built-in Blue theme
                ThemeManager.Current.ChangeTheme(Application.Current, $"{baseTheme}.Blue");
            }
        }
        catch
        {
            // Fallback: if color parsing fails, use Blue
            ThemeManager.Current.ChangeTheme(Application.Current, $"{baseTheme}.Blue");
        }

        // Also update MaterialDesign theme
        ApplyMaterialDesignTheme(baseTheme);
    }

    private static void ApplyMaterialDesignTheme(string baseTheme)
    {
        // Find the BundledTheme in application resources and change base theme
        foreach (var dict in Application.Current.Resources.MergedDictionaries)
        {
            if (dict is MaterialDesignThemes.Wpf.BundledTheme bundledTheme)
            {
                bundledTheme.BaseTheme = baseTheme == "Dark"
                    ? MaterialDesignThemes.Wpf.BaseTheme.Dark
                    : MaterialDesignThemes.Wpf.BaseTheme.Light;
                return;
            }
        }
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }
}

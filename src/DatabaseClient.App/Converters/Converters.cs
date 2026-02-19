using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DatabaseClient.Core.Models;
using ThemeMode = DatabaseClient.Core.Models.ThemeMode;

namespace DatabaseClient.App.Converters;

/// <summary>
/// Converts a boolean to a Visibility value. True = Visible, False = Collapsed.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Inverse: True = Collapsed, False = Visible.
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Collapsed;
    }
}

/// <summary>
/// Converts a non-empty string to Visible, empty/null to Collapsed.
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns italic FontStyle for DBNull/null values, normal otherwise.
/// </summary>
public class NullToItalicConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || value == DBNull.Value)
            return FontStyles.Italic;
        return FontStyles.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DatabaseObjectType to a MaterialDesign icon kind string.
/// </summary>
public class DatabaseObjectTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            DatabaseObjectType.Table => "Table",
            DatabaseObjectType.View => "Eye",
            DatabaseObjectType.Function => "Function",
            DatabaseObjectType.Column => "TableColumn",
            DatabaseObjectType.Index => "KeyVariant",
            DatabaseObjectType.Database => "Database",
            _ => "FileDocument"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns Visible when the string value equals the ConverterParameter, Collapsed otherwise.
/// Used for category-based panel switching in Settings dialog.
/// </summary>
public class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && parameter is string target)
            return string.Equals(str, target, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a ThemeMode enum value to a friendly display name.
/// </summary>
public class ThemeModeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            Core.Models.ThemeMode.Light => "Light",
            Core.Models.ThemeMode.Dark => "Dark",
            Core.Models.ThemeMode.System => "Follow System",
            _ => value?.ToString() ?? ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a string PackIconKind name to an actual PackIconKind enum value.
/// </summary>
public class StringToPackIconKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string kindName && Enum.TryParse<MaterialDesignThemes.Wpf.PackIconKind>(kindName, true, out var kind))
            return kind;
        return MaterialDesignThemes.Wpf.PackIconKind.FileDocument;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value. True → False, False → True.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : value;
    }
}

/// <summary>
/// Converts a null or empty string to Collapsed, otherwise Visible.
/// </summary>
public class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an integer to Visibility by comparing with the ConverterParameter.
/// E.g. value=2, parameter="2" → Visible; value=2, parameter="3" → Collapsed.
/// </summary>
public class IntEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal && parameter is string target && int.TryParse(target, out int targetInt))
            return intVal == targetInt ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

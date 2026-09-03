using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Orbit.Core.Models;

namespace Orbit.App.Converters;

/// <summary>bool -> Visibility. Parameter "invert" flips the mapping.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>Inverts a boolean.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Non-empty string -> Visible, empty/null -> Collapsed. "invert" flips it
/// (used to show a placeholder only when a bound text box is empty).</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasText = !string.IsNullOrWhiteSpace(value as string);
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            hasText = !hasText;
        return hasText ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>
/// Icon cache path -> a decoded, cached <see cref="ImageSource"/>. Falls back to
/// the bundled default icon when the path is null or unreadable.
/// </summary>
public sealed class IconPathToImageConverter : IValueConverter
{
    private static readonly BitmapImage Default = Load(
        "pack://application:,,,/Orbit;component/Assets/default-app-icon.png")!;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var image = Load(path);
            if (image is not null)
                return image;
        }

        return Default;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static BitmapImage? Load(string uri)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(uri, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;          // read the file now, don't lock it
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>Maps <see cref="AppAvailability"/> to a short French status label.</summary>
public sealed class AvailabilityToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        AppAvailability.Available => "Prêt",
        AppAvailability.Missing => "Fichier introuvable",
        AppAvailability.Invalid => "Chemin invalide",
        _ => string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Maps <see cref="AppAvailability"/> to a status brush.</summary>
public sealed class AvailabilityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            AppAvailability.Available => "Brush.Success",
            AppAvailability.Missing => "Brush.Danger",
            AppAvailability.Invalid => "Brush.Warning",
            _ => "Brush.Text.Secondary"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Equality check against a ConverterParameter, for enum-bound radio buttons.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Binding.DoNothing;
}

/// <summary>Formats a nullable <see cref="DateTimeOffset"/> as a relative "il y a …" string.</summary>
public sealed class RelativeDateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset when)
            return "jamais";

        var delta = DateTimeOffset.Now - when;
        return delta switch
        {
            { TotalSeconds: < 60 } => "à l'instant",
            { TotalMinutes: < 60 } => $"il y a {(int)delta.TotalMinutes} min",
            { TotalHours: < 24 } => $"il y a {(int)delta.TotalHours} h",
            { TotalDays: < 30 } => $"il y a {(int)delta.TotalDays} j",
            _ => when.LocalDateTime.ToString("d", culture)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Count &gt; 0 -> Collapsed (used to toggle empty-state panels).</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        var visibleWhenZero = string.Equals(parameter as string, "emptystate", StringComparison.OrdinalIgnoreCase);
        var show = visibleWhenZero ? count == 0 : count > 0;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

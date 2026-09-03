using System.IO;
using Microsoft.Win32;
using System.Windows;
using Orbit.Core.Services;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Applies one of four palettes (light/dark × cool/warm) as merged-dictionary
/// slot 0 in <c>App.xaml</c>. Control styles reference its brushes with
/// <c>DynamicResource</c>, so a swap updates the live UI.
/// </summary>
public sealed class ThemeManager
{
    private static readonly IReadOnlyDictionary<(bool Dark, bool Warm), string> Palettes =
        new Dictionary<(bool, bool), string>
        {
            [(false, false)] = "/Orbit;component/Resources/Themes/Theme.LightCool.xaml",
            [(false, true)]  = "/Orbit;component/Resources/Themes/Theme.LightWarm.xaml",
            [(true, false)]  = "/Orbit;component/Resources/Themes/Theme.DarkCool.xaml",
            [(true, true)]   = "/Orbit;component/Resources/Themes/Theme.DarkWarm.xaml",
        };

    public void Apply(ThemePreference theme, AccentTemperature temperature)
    {
        var dark = theme switch
        {
            ThemePreference.Light => false,
            ThemePreference.Dark => true,
            _ => IsSystemUsingDarkMode()
        };
        var warm = temperature == AccentTemperature.Warm;

        var dict = new ResourceDictionary
        {
            Source = new Uri(Palettes[(dark, warm)], UriKind.Relative)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count == 0)
            merged.Add(dict);
        else
            merged[0] = dict;
    }

    private static bool IsSystemUsingDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

using System.IO;
using Microsoft.Win32;
using System.Windows;
using Orbit.Core.Services;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Applies the light/dark colour dictionary at runtime. The theme dictionary is
/// always merged dictionary slot 0 in <c>App.xaml</c>; control styles reference
/// its brushes with <c>DynamicResource</c> so a swap updates the live UI.
/// </summary>
public sealed class ThemeManager
{
    private const string LightUri = "/Orbit;component/Resources/Themes/Light.xaml";
    private const string DarkUri = "/Orbit;component/Resources/Themes/Dark.xaml";

    public void Apply(ThemePreference preference)
    {
        var useDark = preference switch
        {
            ThemePreference.Light => false,
            ThemePreference.Dark => true,
            _ => IsSystemUsingDarkMode()
        };

        var uri = new Uri(useDark ? DarkUri : LightUri, UriKind.Relative);
        var themeDict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count == 0)
            merged.Add(themeDict);
        else
            merged[0] = themeDict;
    }

    private static bool IsSystemUsingDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            // AppsUseLightTheme == 0 -> dark
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

using System.IO;
using Microsoft.Win32;
using System.Windows;
using Orbit.Core.Services;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Applies one palette dictionary as merged-dictionary slot 0 in <c>App.xaml</c>.
/// Control styles reference its brushes with <c>DynamicResource</c>, so a swap
/// updates the live UI. "System" resolves to Light or Dark from the OS.
/// </summary>
public sealed class ThemeManager
{
    private const string Prefix = "/Orbit;component/Resources/Themes/Theme.";

    public void Apply(ThemePreference theme)
    {
        var resolved = theme == ThemePreference.System
            ? (IsSystemUsingDarkMode() ? ThemePreference.Dark : ThemePreference.Light)
            : theme;

        var dict = new ResourceDictionary
        {
            Source = new Uri($"{Prefix}{resolved}.xaml", UriKind.Relative)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count == 0)
            merged.Add(dict);
        else
            merged[0] = dict;

        WindowThemeHelper.RaisePaletteChanged();
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
            return true;
        }
    }
}

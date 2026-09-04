namespace Orbit.Core.Services;

public enum ThemePreference
{
    System = 0,
    Light = 1,
    Dark = 2
}

/// <summary>Accent colour family: cool blues/violets or warm ambers/terracotta.</summary>
public enum AccentTemperature
{
    Cool = 0,
    Warm = 1
}

public enum LibrarySort
{
    Name = 0,
    RecentlyAdded = 1,
    MostLaunched = 2,
    LastLaunched = 3
}

/// <summary>User-facing settings, serialised to <c>settings.json</c>.</summary>
public sealed class AppSettings
{
    public ThemePreference Theme { get; set; } = ThemePreference.Dark;

    public AccentTemperature Temperature { get; set; } = AccentTemperature.Cool;

    public LibrarySort Sort { get; set; } = LibrarySort.Name;

    /// <summary>When true, removing a library entry asks for confirmation first.</summary>
    public bool ConfirmBeforeRemove { get; set; } = true;

    /// <summary>Remembers the last navigation section so the app reopens where it was.</summary>
    public string LastSection { get; set; } = "Home";

    // ---- Window ----
    // 0 = "not chosen yet": Orbit fits itself to the current screen on launch.
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    /// <summary>Closing the window hides it to the notification area instead of quitting.</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>Global interface scale (0.7–1.3). 0.85 = compact, 1.0 = normal, 1.15 = large.</summary>
    public double UiScale { get; set; } = 0.85;

    // ---- Optional online identification credentials (see Identification/) ----
    public string? IgdbClientId { get; set; }
    public string? IgdbClientSecret { get; set; }
    public string? SteamGridDbApiKey { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

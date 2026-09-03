namespace Orbit.Core.Services;

public enum ThemePreference
{
    System = 0,
    Light = 1,
    Dark = 2
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
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public LibrarySort Sort { get; set; } = LibrarySort.Name;

    /// <summary>When true, removing a library entry asks for confirmation first.</summary>
    public bool ConfirmBeforeRemove { get; set; } = true;

    /// <summary>Remembers the last navigation section so the app reopens where it was.</summary>
    public string LastSection { get; set; } = "Home";

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

namespace Orbit.Core.Models;

/// <summary>
/// High level type of a library entry. Drives the "Jeux" / "Applications"
/// navigation sections. Free-text categorisation is handled separately by
/// <see cref="AppEntry.Category"/>.
/// </summary>
public enum AppKind
{
    Application = 0,
    Game = 1
}

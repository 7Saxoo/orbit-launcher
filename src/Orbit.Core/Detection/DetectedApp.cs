using Orbit.Core.Models;

namespace Orbit.Core.Detection;

/// <summary>A candidate application or game found on the machine by an
/// <see cref="IInstalledAppSource"/>, before the user chooses to import it.</summary>
public sealed record DetectedApp
{
    public required string Name { get; init; }
    public required string ExecutablePath { get; init; }
    public AppKind Kind { get; init; } = AppKind.Application;

    /// <summary>Category proposed for the library entry (e.g. "Steam", "Epic Games").</summary>
    public string Category { get; init; } = string.Empty;

    public string? Publisher { get; init; }

    /// <summary>Human-readable origin, used to group the results ("Steam", "Programmes installés"…).</summary>
    public string Source { get; init; } = string.Empty;

    public string? InstallLocation { get; init; }
}

namespace Orbit.Core.Detection;

/// <summary>A place Orbit knows how to look for already-installed software
/// (the Windows uninstall registry, a Steam library, the Epic manifests…).
/// Implementations must be resilient: a missing store is an empty result, not
/// an exception.</summary>
public interface IInstalledAppSource
{
    /// <summary>Short label shown as the group header in the results list.</summary>
    string DisplayName { get; }

    /// <summary>Enumerates candidates. Runs on a background thread.</summary>
    IEnumerable<DetectedApp> Scan(CancellationToken ct);
}

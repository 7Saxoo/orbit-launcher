namespace Orbit.Core.Detection;

public sealed record DetectionResult(
    IReadOnlyList<DetectedApp> NewItems,
    int AlreadyInLibrary,
    int TotalFound);

/// <summary>Runs every <see cref="IInstalledAppSource"/>, filters the results to
/// launchable executables that are not already in the library, and de-duplicates
/// them.</summary>
public interface IAppDetectionService
{
    Task<DetectionResult> ScanAsync(CancellationToken ct = default);
}

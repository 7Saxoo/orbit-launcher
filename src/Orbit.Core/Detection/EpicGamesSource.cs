using Orbit.Core.Infrastructure;
using Serilog;

namespace Orbit.Core.Detection;

/// <summary>Finds installed Epic Games Store titles from the launcher's
/// per-game <c>.item</c> manifests under ProgramData.</summary>
public sealed class EpicGamesSource : IInstalledAppSource
{
    private readonly ILogger _log;

    public EpicGamesSource(ILogger log) => _log = log.ForContext<EpicGamesSource>();

    public string DisplayName => "Epic Games";

    public IEnumerable<DetectedApp> Scan(CancellationToken ct)
    {
        var manifestsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (!Directory.Exists(manifestsDir))
        {
            _log.Debug("Epic Games Launcher not detected");
            yield break;
        }

        string[] items;
        try { items = Directory.GetFiles(manifestsDir, "*.item"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            DetectedApp? detected;
            try { detected = EpicManifestReader.Parse(File.ReadAllText(item)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            if (detected is null)
                continue;

            yield return detected with { ExecutablePath = PathHelper.Normalize(detected.ExecutablePath) };
        }
    }
}

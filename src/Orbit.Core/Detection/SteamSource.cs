using System.Runtime.Versioning;
using Microsoft.Win32;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Serilog;

namespace Orbit.Core.Detection;

/// <summary>Finds installed Steam games by reading Steam's library folders and
/// app manifests, then locating each game's main executable on disk.</summary>
[SupportedOSPlatform("windows")]
public sealed class SteamSource : IInstalledAppSource
{
    private readonly ILogger _log;

    public SteamSource(ILogger log) => _log = log.ForContext<SteamSource>();

    public string DisplayName => "Steam";

    public IEnumerable<DetectedApp> Scan(CancellationToken ct)
    {
        var steamPath = ResolveSteamPath();
        if (steamPath is null || !Directory.Exists(steamPath))
        {
            _log.Debug("Steam not detected");
            yield break;
        }

        foreach (var library in ResolveLibraries(steamPath))
        {
            ct.ThrowIfCancellationRequested();

            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps))
                continue;

            string[] manifests;
            try { manifests = Directory.GetFiles(steamApps, "appmanifest_*.acf"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }

            foreach (var manifest in manifests)
            {
                ct.ThrowIfCancellationRequested();

                SteamCatalog.SteamGame? game;
                try { game = SteamCatalog.ParseAppManifest(File.ReadAllText(manifest)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                if (game is null)
                    continue;

                var installFolder = Path.Combine(steamApps, "common", game.InstallDir);
                var exe = MainExecutableFinder.Find(installFolder, game.Name);
                if (exe is null)
                {
                    _log.Debug("No executable found for Steam game {Name}", game.Name);
                    continue;
                }

                yield return new DetectedApp
                {
                    Name = game.Name,
                    ExecutablePath = PathHelper.Normalize(exe),
                    Kind = AppKind.Game,
                    Category = "Steam",
                    Source = "Steam",
                    InstallLocation = installFolder
                };
            }
        }
    }

    private static string? ResolveSteamPath()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
        {
            if (key?.GetValue("SteamPath") is string p && p.Length > 0)
                return p.Replace('/', '\\');
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = hklm.OpenSubKey(@"SOFTWARE\Valve\Steam");
            if (key?.GetValue("InstallPath") is string p && p.Length > 0)
                return p;
        }

        return null;
    }

    private IReadOnlyList<string> ResolveLibraries(string steamPath)
    {
        var libraries = new List<string> { steamPath };

        foreach (var candidate in new[]
                 {
                     Path.Combine(steamPath, "steamapps", "libraryfolders.vdf"),
                     Path.Combine(steamPath, "config", "libraryfolders.vdf")
                 })
        {
            if (!File.Exists(candidate))
                continue;

            try
            {
                foreach (var path in SteamCatalog.ParseLibraryFolders(File.ReadAllText(candidate)))
                {
                    if (Directory.Exists(path) && !libraries.Contains(path, StringComparer.OrdinalIgnoreCase))
                        libraries.Add(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warning(ex, "Could not read {File}", candidate);
            }

            break;
        }

        return libraries;
    }
}

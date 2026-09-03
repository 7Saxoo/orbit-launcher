using System.Runtime.Versioning;
using Microsoft.Win32;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Serilog;

namespace Orbit.Core.Detection;

/// <summary>
/// Enumerates the Windows "Add/Remove Programs" registry keys (HKLM 64/32-bit and
/// HKCU) and turns each real application into a <see cref="DetectedApp"/>. System
/// components, updates and redistributables are filtered out.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryUninstallSource : IInstalledAppSource
{
    private const string UninstallKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly string[] NameNoise =
    {
        "update for", "hotfix", "security update", "redistributable", "runtime",
        "microsoft visual c++", ".net framework", "directx", "driver", "sdk",
        "language pack", "windows software development kit"
    };

    private readonly ILogger _log;

    public RegistryUninstallSource(ILogger log) => _log = log.ForContext<RegistryUninstallSource>();

    public string DisplayName => "Programmes installés";

    public IEnumerable<DetectedApp> Scan(CancellationToken ct)
    {
        var roots = new (RegistryHive Hive, RegistryView View)[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Default),
        };

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, view) in roots)
        {
            ct.ThrowIfCancellationRequested();

            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(UninstallKey);
            if (uninstall is null)
                continue;

            foreach (var subName in uninstall.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();

                using var app = uninstall.OpenSubKey(subName);
                if (app is null)
                    continue;

                var detected = TryReadEntry(app);
                if (detected is not null && seenNames.Add(detected.Name))
                    yield return detected;
            }
        }
    }

    private DetectedApp? TryReadEntry(RegistryKey app)
    {
        var name = app.GetValue("DisplayName") as string;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (Convert.ToInt32(app.GetValue("SystemComponent") ?? 0) == 1)
            return null;
        if (!string.IsNullOrEmpty(app.GetValue("ParentKeyName") as string))
            return null; // an update/patch of another product
        if (!string.IsNullOrEmpty(app.GetValue("ReleaseType") as string))
            return null; // "Security Update", "Hotfix", "Update Rollup"

        var lowerName = name.ToLowerInvariant();
        if (NameNoise.Any(n => lowerName.Contains(n, StringComparison.Ordinal)))
            return null;

        var installLocation = (app.GetValue("InstallLocation") as string)?.Trim().Trim('"');
        var displayIcon = (app.GetValue("DisplayIcon") as string)?.Trim().Trim('"');
        var publisher = (app.GetValue("Publisher") as string)?.Trim();

        var exe = ResolveExecutable(displayIcon, installLocation, name);
        if (exe is null)
            return null;

        return new DetectedApp
        {
            Name = name.Trim(),
            ExecutablePath = PathHelper.Normalize(exe),
            Kind = AppKind.Application,
            Category = "Programmes installés",
            Source = "Programmes installés",
            Publisher = string.IsNullOrWhiteSpace(publisher) ? null : publisher,
            InstallLocation = installLocation
        };
    }

    private static string? ResolveExecutable(string? displayIcon, string? installLocation, string name)
    {
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var candidate = displayIcon;
            var comma = candidate.LastIndexOf(',');
            if (comma > 1 && !candidate.AsSpan(comma).Contains('\\'))
                candidate = candidate[..comma];

            candidate = candidate.Trim().Trim('"');
            if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !candidate.Contains("uninstall", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(candidate))
            {
                return candidate;
            }
        }

        return MainExecutableFinder.Find(installLocation, name, maxDepth: 2, maxFiles: 250);
    }
}

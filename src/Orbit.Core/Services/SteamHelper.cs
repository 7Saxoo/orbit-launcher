using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Orbit.Core.Detection;
using Orbit.Core.Infrastructure;
using Serilog;

namespace Orbit.Core.Services;

/// <inheritdoc />
[SupportedOSPlatform("windows")]
public sealed class SteamHelper : ISteamHelper
{
    private const string CommonMarker = @"\steamapps\common\";

    private readonly ILogger _log;

    public SteamHelper(ILogger log) => _log = log.ForContext<SteamHelper>();

    public bool IsSteamGamePath(string executablePath)
    {
        var path = PathHelper.Normalize(executablePath);
        return path.Contains(CommonMarker, StringComparison.OrdinalIgnoreCase);
    }

    public string? ResolveAppId(string executablePath)
    {
        try
        {
            var path = PathHelper.Normalize(executablePath);
            var idx = path.IndexOf(CommonMarker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;

            var steamAppsDir = path[..(idx + "\\steamapps".Length)];
            var afterCommon = path[(idx + CommonMarker.Length)..];
            var sep = afterCommon.IndexOf(Path.DirectorySeparatorChar);
            var installDir = sep >= 0 ? afterCommon[..sep] : afterCommon;
            if (installDir.Length == 0 || !Directory.Exists(steamAppsDir))
                return null;

            foreach (var manifest in Directory.EnumerateFiles(steamAppsDir, "appmanifest_*.acf"))
            {
                SteamCatalog.SteamGame? game;
                try { game = SteamCatalog.ParseAppManifest(File.ReadAllText(manifest)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }

                if (game is not null &&
                    string.Equals(game.InstallDir, installDir, StringComparison.OrdinalIgnoreCase))
                {
                    return game.AppId;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _log.Debug(ex, "Could not resolve a Steam appid for {Path}", executablePath);
        }

        return null;
    }

    public void EnsureRunningMinimised()
    {
        try
        {
            if (IsSteamRunning())
                return;

            var steamExe = FindSteamExe();
            if (steamExe is null)
            {
                _log.Debug("Steam client executable not found; leaving it to the steam:// handler");
                return;
            }

            _log.Information("Starting Steam minimised ({Exe} -silent)", steamExe);
            Process.Start(new ProcessStartInfo(steamExe, "-silent") { UseShellExecute = true });

            // Give the client a few seconds to come up so rungameid doesn't race it.
            for (var i = 0; i < 24 && !IsSteamRunning(); i++)
                Thread.Sleep(250);
            if (IsSteamRunning())
                Thread.Sleep(1500);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not pre-start the Steam client");
        }
    }

    private static bool IsSteamRunning()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("steam"))
                using (p) return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        return false;
    }

    private string? FindSteamExe()
    {
        foreach (var (hive, view) in new[]
                 {
                     (RegistryHive.CurrentUser, RegistryView.Default),
                     (RegistryHive.LocalMachine, RegistryView.Registry32),
                     (RegistryHive.LocalMachine, RegistryView.Registry64),
                 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(@"Software\Valve\Steam");
                if (key is null)
                    continue;

                if (key.GetValue("SteamExe") is string exe && File.Exists(exe))
                    return exe;

                var pathValue = key.GetValue("SteamPath") as string ?? key.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(pathValue))
                {
                    var candidate = Path.Combine(pathValue.Replace('/', '\\'), "steam.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
            {
            }
        }

        return null;
    }
}

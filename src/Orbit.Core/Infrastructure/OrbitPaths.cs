namespace Orbit.Core.Infrastructure;

/// <summary>
/// Resolves every on-disk location the launcher uses. Everything lives under
/// <c>%LOCALAPPDATA%\Orbit</c> by default so nothing is ever written inside the
/// application's install folder (which may be read-only under Program Files).
/// The base directory can be overridden for tests.
/// </summary>
public sealed class OrbitPaths
{
    public OrbitPaths(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Orbit");

        DatabaseFile = Path.Combine(BaseDirectory, "orbit.db");
        IconCacheDirectory = Path.Combine(BaseDirectory, "icons");
        LogDirectory = Path.Combine(BaseDirectory, "logs");
        SettingsFile = Path.Combine(BaseDirectory, "settings.json");
    }

    public string BaseDirectory { get; }
    public string DatabaseFile { get; }
    public string IconCacheDirectory { get; }
    public string LogDirectory { get; }
    public string SettingsFile { get; }

    /// <summary>Creates every directory the launcher needs. Safe to call repeatedly.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(IconCacheDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}

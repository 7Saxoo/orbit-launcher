namespace Orbit.Core.Detection;

/// <summary>Pure parsing helpers for Steam's on-disk catalogue files. IO lives
/// in <see cref="SteamSource"/>; everything here is unit-tested.</summary>
public static class SteamCatalog
{
    /// <summary>Steam's own bundled redistributables "app" – never a real game.</summary>
    public const string RedistributablesAppId = "228980";

    public sealed record SteamGame(string AppId, string Name, string InstallDir);

    /// <summary>
    /// Extracts every library path from a <c>libraryfolders.vdf</c>. Handles both
    /// the modern shape (<c>"0" { "path" "D:\\SteamLibrary" }</c>) and the legacy
    /// one (<c>"1" "D:\\SteamLibrary"</c>).
    /// </summary>
    public static IReadOnlyList<string> ParseLibraryFolders(string vdf)
    {
        var result = new List<string>();
        var root = VdfParser.Parse(vdf);

        var container = root["libraryfolders"] ?? root["LibraryFolders"] ?? root;
        foreach (var (key, node) in container.Children)
        {
            if (!int.TryParse(key, out _))
                continue;

            var path = node.Value ?? node.ValueOf("path");
            if (!string.IsNullOrWhiteSpace(path))
                result.Add(NormalizeSlashes(path));
        }

        return result;
    }

    /// <summary>Parses one <c>appmanifest_*.acf</c>. Returns null when the file is
    /// not a usable game entry.</summary>
    public static SteamGame? ParseAppManifest(string acf)
    {
        var root = VdfParser.Parse(acf);
        var state = root["AppState"];
        if (state is null)
            return null;

        var appId = state.ValueOf("appid");
        var name = state.ValueOf("name");
        var installDir = state.ValueOf("installdir");

        if (string.IsNullOrWhiteSpace(appId) ||
            string.IsNullOrWhiteSpace(installDir) ||
            appId == RedistributablesAppId)
        {
            return null;
        }

        return new SteamGame(
            appId.Trim(),
            string.IsNullOrWhiteSpace(name) ? installDir.Trim() : name.Trim(),
            installDir.Trim());
    }

    private static string NormalizeSlashes(string path) =>
        path.Replace("\\\\", "\\").Replace('/', '\\');
}

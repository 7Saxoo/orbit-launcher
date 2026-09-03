namespace Orbit.Core.Detection;

/// <summary>
/// Picks the most plausible "main" executable inside an install folder, skipping
/// obvious installers, redistributables and crash handlers. Bounded in depth and
/// file count so it stays cheap even on large game folders.
/// </summary>
public static class MainExecutableFinder
{
    private static readonly string[] ExcludedNameParts =
    {
        "unins", "uninstall", "setup", "installer", "install-", "vcredist", "vc_redist",
        "dxsetup", "dxwebsetup", "directx", "redist", "crashhandler", "crashpad", "crashreport",
        "notification_helper", "dotnet", "dotnetfx", "oalinst", "python", "cleanup", "reporter",
        "helper", "webview2", "prerequisites", "activation", "diagnostics"
    };

    private static readonly string[] ExcludedFolderNames =
    {
        "redist", "_commonredist", "commonredist", "redistributable", "redistributables",
        "directx", "dotnet", "vcredist", "prerequisites", "support", "installers"
    };

    public static string? Find(string? directory, string? hint = null, int maxDepth = 3, int maxFiles = 600)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        var hintKey = Simplify(hint);
        string? best = null;
        var bestScore = int.MinValue;
        var seen = 0;

        foreach (var (file, depth) in EnumerateExecutables(directory, maxDepth))
        {
            if (++seen > maxFiles)
                break;

            var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            if (ExcludedNameParts.Any(part => name.Contains(part, StringComparison.Ordinal)))
                continue;

            long size = 0;
            try { size = new FileInfo(file).Length; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

            var score = 0;
            if (hintKey.Length > 0)
            {
                var nameKey = Simplify(name);
                if (nameKey == hintKey) score += 1000;
                else if (nameKey.Length >= 3 && hintKey.Contains(nameKey, StringComparison.Ordinal)) score += 500;
                else if (hintKey.Length >= 3 && nameKey.Contains(hintKey, StringComparison.Ordinal)) score += 400;
                else if (SharedPrefixLength(nameKey, hintKey) >= 4) score += 200;
            }
            score -= depth * 15;                              // mild preference for shallower
            score += (int)Math.Min(size / (1024 * 1024), 60); // mild preference for bigger

            if (score > bestScore)
            {
                bestScore = score;
                best = file;
            }
        }

        return best;
    }

    private static string Simplify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        Span<char> buffer = stackalloc char[value.Length];
        var i = 0;
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
                buffer[i++] = char.ToLowerInvariant(c);
        }

        return new string(buffer[..i]);
    }

    private static int SharedPrefixLength(string a, string b)
    {
        var max = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < max && a[i] == b[i])
            i++;
        return i;
    }

    private static IEnumerable<(string file, int depth)> EnumerateExecutables(string root, int maxDepth)
    {
        var queue = new Queue<(string dir, int depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (dir, depth) = queue.Dequeue();

            string[] files;
            try { files = Directory.GetFiles(dir, "*.exe"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }

            foreach (var file in files)
                yield return (file, depth);

            if (depth >= maxDepth)
                continue;

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }

            foreach (var sub in subdirs)
            {
                var leaf = Path.GetFileName(sub).ToLowerInvariant();
                if (!ExcludedFolderNames.Contains(leaf))
                    queue.Enqueue((sub, depth + 1));
            }
        }
    }
}

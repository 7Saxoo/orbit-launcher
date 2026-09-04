namespace Orbit.Core.Identification;

public sealed record WinGetPackage(string Name, string Id, string Version, string Source);

/// <summary>Parses the fixed-width table printed by <c>winget search</c>
/// (locale-independent: it keys off the "Id"/"Version" column headers).</summary>
public static class WinGetOutputParser
{
    public static IReadOnlyList<WinGetPackage> Parse(string output)
    {
        var packages = new List<WinGetPackage>();
        if (string.IsNullOrWhiteSpace(output))
            return packages;

        var lines = output.Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.None);

        var headerIndex = Array.FindIndex(lines, l =>
            HasColumn(l, "Id") && HasColumn(l, "Version"));
        if (headerIndex < 0)
            return packages;

        var header = lines[headerIndex];
        var idCol = header.IndexOf("Id", StringComparison.Ordinal);
        var versionCol = header.IndexOf("Version", StringComparison.Ordinal);
        var sourceCol = header.IndexOf("Source", StringComparison.Ordinal);
        if (idCol <= 0 || versionCol <= idCol)
            return packages;

        var start = headerIndex + 1;
        if (start < lines.Length && IsRule(lines[start]))
            start++;

        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || IsRule(line))
                continue;
            if (line.Length <= idCol)
                continue;

            var name = Slice(line, 0, idCol);
            var id = Slice(line, idCol, versionCol);
            var version = sourceCol > versionCol
                ? Slice(line, versionCol, sourceCol)
                : Slice(line, versionCol, line.Length);
            var source = sourceCol > 0 && line.Length > sourceCol ? line[sourceCol..].Trim() : string.Empty;

            if (name.Length > 0 && id.Length > 0 && !id.Contains(' '))
                packages.Add(new WinGetPackage(name, id, version, source));
        }

        return packages;
    }

    private static bool HasColumn(string line, string name)
    {
        var idx = line.IndexOf(name, StringComparison.Ordinal);
        if (idx < 0)
            return false;
        var before = idx == 0 || line[idx - 1] == ' ';
        var afterPos = idx + name.Length;
        var after = afterPos >= line.Length || line[afterPos] == ' ';
        return before && after;
    }

    private static bool IsRule(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 0 && trimmed.All(c => c is '-' or '─' or '=' or ' ');
    }

    private static string Slice(string line, int start, int end)
    {
        if (start >= line.Length)
            return string.Empty;
        end = Math.Min(end, line.Length);
        return end <= start ? string.Empty : line[start..end].Trim();
    }
}

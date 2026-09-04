using System.Diagnostics;
using Orbit.Core.Infrastructure;

namespace Orbit.Core.Identification;

/// <summary>
/// Everything we can learn about a candidate executable *without* the network:
/// its name, where it lives and the version block Windows attached to it. Feeds
/// the identification providers.
/// </summary>
public sealed record ExeSignals
{
    public required string NormalizedPath { get; init; }
    public required string FileName { get; init; }
    public required string FileNameNoExt { get; init; }
    public required string ParentFolderName { get; init; }

    /// <summary>Lower-cased path segments, e.g. ["c:", "program files (x86)", "steam", ...].</summary>
    public required IReadOnlyList<string> Segments { get; init; }

    public string? ProductName { get; init; }
    public string? CompanyName { get; init; }
    public string? FileDescription { get; init; }
    public string? FileVersion { get; init; }

    public bool PathContains(string fragment) =>
        NormalizedPath.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>A human-ish display name derived from local data only.</summary>
    public string BestLocalName =>
        Clean(FileDescription)
        ?? Clean(ProductName)
        ?? Prettify(ParentLooksLikeGameName ? ParentFolderName : FileNameNoExt);

    private bool ParentLooksLikeGameName =>
        PathContains(@"steamapps\common")
        || PathContains(@"\gog galaxy\games\")
        || PathContains(@"\epic games\");

    public static ExeSignals Extract(string path)
    {
        var normalized = PathHelper.Normalize(path);
        var fileName = Path.GetFileName(normalized);
        var parent = Path.GetFileName(Path.GetDirectoryName(normalized) ?? string.Empty);

        string? product = null, company = null, description = null, version = null;
        try
        {
            if (File.Exists(normalized))
            {
                var vi = FileVersionInfo.GetVersionInfo(normalized);
                product = Clean(vi.ProductName);
                company = Clean(vi.CompanyName);
                description = Clean(vi.FileDescription);
                version = Clean(vi.FileVersion);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            // metadata is optional
        }

        return new ExeSignals
        {
            NormalizedPath = normalized,
            FileName = fileName,
            FileNameNoExt = Path.GetFileNameWithoutExtension(normalized),
            ParentFolderName = parent,
            Segments = normalized.ToLowerInvariant()
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries),
            ProductName = product,
            CompanyName = company,
            FileDescription = description,
            FileVersion = version
        };
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Prettify(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var spaced = raw.Replace('_', ' ').Replace('-', ' ').Replace('.', ' ').Trim();
        while (spaced.Contains("  "))
            spaced = spaced.Replace("  ", " ");
        return spaced.Length == 0 ? raw : char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}

using System.Text;

namespace Orbit.Core.Infrastructure;

/// <summary>
/// Small helpers for dealing with Windows executable paths. Kept dependency-free
/// and side-effect-free so it is trivially unit-testable.
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Normalises a user-supplied path: trims surrounding whitespace and quotes,
    /// expands environment variables, and returns the absolute, canonical form.
    /// Never throws for ordinary bad input – returns the best effort string.
    /// </summary>
    public static string Normalize(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return string.Empty;

        var trimmed = rawPath.Trim().Trim('"').Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        trimmed = Environment.ExpandEnvironmentVariables(trimmed);

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return trimmed;
        }
    }

    /// <summary>True when the path ends with the <c>.exe</c> extension (case-insensitive).</summary>
    public static bool HasExecutableExtension(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the directory that should be used as the working directory for a
    /// given executable path, or <c>null</c> when it cannot be determined.
    /// </summary>
    public static string? GetContainingDirectory(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return null;

        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(executablePath));
            return string.IsNullOrEmpty(dir) ? null : dir;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Wraps a path in double quotes when it contains whitespace, so it can be
    /// safely embedded in a command line. Existing surrounding quotes are kept
    /// as-is.
    /// </summary>
    public static string QuoteIfNeeded(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
            return path;

        return path.AsSpan().IndexOfAny(" \t") >= 0 ? $"\"{path}\"" : path;
    }

    /// <summary>
    /// Compares two paths for equality the way Windows would: case-insensitive,
    /// separator-insensitive, ignoring a trailing separator. Both inputs are
    /// normalised first.
    /// </summary>
    public static bool AreSamePath(string? a, string? b)
    {
        var na = Normalize(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var nb = Normalize(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return na.Length > 0 && string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Produces a stable, file-system-safe token for a path, used as part of an
    /// icon cache file name. Not a security hash – just a de-duplication key.
    /// </summary>
    public static string StableToken(string value)
    {
        // FNV-1a 64-bit over the lower-cased UTF-8 bytes.
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;

        var bytes = Encoding.UTF8.GetBytes(value.ToLowerInvariant());
        var hash = offset;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= prime;
        }

        return hash.ToString("x16");
    }
}

namespace Orbit.Core.Services;

/// <summary>
/// Extracts the icon of an executable once, caches it as a PNG on disk, and
/// returns the cached path on every subsequent call. Icons are never
/// re-extracted unless the source file changes.
/// </summary>
public interface IIconService
{
    /// <summary>
    /// Returns the absolute path to a cached PNG for <paramref name="executablePath"/>,
    /// extracting it on first use. Returns <c>null</c> when no icon can be
    /// obtained – callers should fall back to a default image.
    /// </summary>
    Task<string?> EnsureIconAsync(string executablePath, CancellationToken ct = default);
}

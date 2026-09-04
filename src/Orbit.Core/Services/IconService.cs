using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Orbit.Core.Infrastructure;
using Serilog;

namespace Orbit.Core.Services;

/// <summary>
/// Disk-cached icon extractor. Tries several sources (an explicit hint, the
/// executable, a matching <c>.ico</c> or sibling <c>.exe</c> in the folder) and
/// rejects Windows' generic "blank executable" icon so entries like <c>git.exe</c>
/// or <c>mysqld.exe</c> still get a real logo. The cache key folds in the chosen
/// source's last write time and size.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class IconService : IIconService
{
    private readonly OrbitPaths _paths;
    private readonly ILogger _log;
    private readonly Lazy<string?> _genericExeSignature;

    public IconService(OrbitPaths paths, ILogger log)
    {
        _paths = paths;
        _log = log.ForContext<IconService>();
        _genericExeSignature = new Lazy<string?>(ComputeGenericExeSignature);
    }

    public Task<string?> EnsureIconAsync(string executablePath, CancellationToken ct = default) =>
        EnsureIconAsync(executablePath, null, ct);

    public async Task<string?> EnsureIconAsync(
        string executablePath, string? iconHint, CancellationToken ct = default)
    {
        var path = PathHelper.Normalize(executablePath);
        if (path.Length == 0 || !File.Exists(path))
            return null;

        var sources = ResolveSources(path, iconHint).ToList();

        // Cache key: the first source plus the exe's own stamp (so an update busts it).
        string cacheFile;
        try
        {
            var info = new FileInfo(path);
            var key = PathHelper.StableToken(
                $"{string.Join('|', sources)}|{info.LastWriteTimeUtc.Ticks}|{info.Length}");
            cacheFile = Path.Combine(_paths.IconCacheDirectory, key + ".png");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Could not stat {Path} for icon caching", path);
            return null;
        }

        if (File.Exists(cacheFile))
            return cacheFile;

        return await Task.Run(() => Extract(sources, cacheFile), ct).ConfigureAwait(false);
    }

    private IEnumerable<string> ResolveSources(string exePath, string? iconHint)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateCandidates(exePath, iconHint))
        {
            var normalized = PathHelper.Normalize(candidate);
            if (normalized.Length > 0 && File.Exists(normalized) && seen.Add(normalized))
                yield return normalized;
        }
    }

    private static IEnumerable<string> EnumerateCandidates(string exePath, string? iconHint)
    {
        if (!string.IsNullOrWhiteSpace(iconHint))
        {
            var hint = iconHint.Trim().Trim('"');
            var comma = hint.LastIndexOf(',');
            if (comma > 1 && !hint.AsSpan(comma).Contains(Path.DirectorySeparatorChar))
                hint = hint[..comma];
            yield return hint.Trim().Trim('"');
        }

        yield return exePath;

        var dir = Path.GetDirectoryName(exePath);
        if (dir is null)
            yield break;

        var stem = Path.GetFileNameWithoutExtension(exePath);

        foreach (var searchDir in new[] { dir, Path.GetDirectoryName(dir) })
        {
            if (searchDir is null || !Directory.Exists(searchDir))
                continue;

            string[] icos;
            try { icos = Directory.GetFiles(searchDir, "*.ico"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { icos = Array.Empty<string>(); }

            foreach (var ico in icos.OrderByDescending(f => NameLooksRelated(f, stem)))
                yield return ico;

            string[] exes;
            try { exes = Directory.GetFiles(searchDir, "*.exe"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { exes = Array.Empty<string>(); }

            foreach (var exe in exes
                         .Where(e => !string.Equals(e, exePath, StringComparison.OrdinalIgnoreCase))
                         .Where(e => NameLooksRelated(e, stem))
                         .OrderByDescending(e => new FileInfo(e).Length))
            {
                yield return exe;
            }
        }
    }

    private static bool NameLooksRelated(string file, string stem)
    {
        var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
        stem = stem.ToLowerInvariant();
        return name.Contains(stem, StringComparison.Ordinal)
               || stem.Contains(name, StringComparison.Ordinal)
               || name is "app" or "icon" or "logo";
    }

    private string? Extract(IReadOnlyList<string> sources, string cacheFile)
    {
        try
        {
            Directory.CreateDirectory(_paths.IconCacheDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Could not create the icon cache directory");
            return null;
        }

        Bitmap? firstAny = null;
        Bitmap? chosen = null;

        foreach (var source in sources)
        {
            var bitmap = ExtractBest(source);
            if (bitmap is null)
                continue;

            firstAny ??= bitmap;

            if (!IsGeneric(bitmap) && !IsBlank(bitmap))
            {
                chosen = bitmap;
                break;
            }

            if (!ReferenceEquals(bitmap, firstAny))
                bitmap.Dispose();
        }

        chosen ??= firstAny;
        if (chosen is null)
        {
            _log.Debug("No icon could be extracted from any of {Count} source(s)", sources.Count);
            return null;
        }

        try
        {
            var tmp = cacheFile + ".tmp";
            chosen.Save(tmp, ImageFormat.Png);
            File.Move(tmp, cacheFile, overwrite: true);
            return cacheFile;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not write icon cache file");
            return null;
        }
        finally
        {
            chosen.Dispose();
            if (firstAny is not null && !ReferenceEquals(firstAny, chosen))
                firstAny.Dispose();
        }
    }

    private static Bitmap? ExtractBest(string file)
    {
        foreach (var size in new[] { 256, 128, 64, 48, 32 })
        {
            var bmp = ExtractAtSize(file, size);
            if (bmp is not null)
                return bmp;
        }

        try
        {
            using var associated = Icon.ExtractAssociatedIcon(file);
            return associated?.ToBitmap();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Bitmap? ExtractAtSize(string file, int size)
    {
        var handles = new IntPtr[1];
        var ids = new int[1];
        var count = NativeMethods.PrivateExtractIcons(file, 0, size, size, handles, ids, 1, 0);
        if (count <= 0 || handles[0] == IntPtr.Zero)
            return null;

        try
        {
            using var icon = Icon.FromHandle(handles[0]);
            return new Bitmap(icon.ToBitmap());
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException)
        {
            return null;
        }
        finally
        {
            NativeMethods.DestroyIcon(handles[0]);
        }
    }

    private bool IsGeneric(Bitmap bitmap)
    {
        var signature = _genericExeSignature.Value;
        return signature is not null && Signature(bitmap) == signature;
    }

    private static bool IsBlank(Bitmap bitmap)
    {
        using var small = new Bitmap(bitmap, new Size(16, 16));
        int opaque = 0;
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 16; x++)
            if (small.GetPixel(x, y).A > 16)
                opaque++;
        return opaque < 6;
    }

    private static string Signature(Bitmap bitmap)
    {
        using var small = new Bitmap(bitmap, new Size(16, 16));
        Span<byte> bytes = stackalloc byte[16 * 16 * 4];
        var i = 0;
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 16; x++)
        {
            var p = small.GetPixel(x, y);
            bytes[i++] = p.A; bytes[i++] = p.R; bytes[i++] = p.G; bytes[i++] = p.B;
        }
        return PathHelper.StableToken(Convert.ToHexString(bytes));
    }

    private string? ComputeGenericExeSignature()
    {
        try
        {
            var shinfo = new NativeMethods.SHFILEINFO();
            var result = NativeMethods.SHGetFileInfo(
                "orbit-generic-probe.exe", NativeMethods.FILE_ATTRIBUTE_NORMAL, ref shinfo,
                (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(),
                NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON | NativeMethods.SHGFI_USEFILEATTRIBUTES);

            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                return null;

            try
            {
                using var icon = Icon.FromHandle(shinfo.hIcon);
                using var bmp = icon.ToBitmap();
                return Signature(bmp);
            }
            finally
            {
                NativeMethods.DestroyIcon(shinfo.hIcon);
            }
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Could not compute the generic executable icon signature");
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static class NativeMethods
    {
        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(
            string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int PrivateExtractIcons(
            string lpszFile, int nIconIndex, int cxIcon, int cyIcon,
            IntPtr[] phicon, int[] piconid, int nIcons, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}

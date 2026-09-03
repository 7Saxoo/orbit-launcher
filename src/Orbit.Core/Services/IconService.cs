using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Orbit.Core.Infrastructure;
using Serilog;

namespace Orbit.Core.Services;

/// <summary>
/// Disk-cached icon extractor. The cache key folds in the file's last write
/// time and size, so an updated executable transparently gets a fresh icon
/// while unchanged files are never re-read.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class IconService : IIconService
{
    private readonly OrbitPaths _paths;
    private readonly ILogger _log;

    public IconService(OrbitPaths paths, ILogger log)
    {
        _paths = paths;
        _log = log.ForContext<IconService>();
    }

    public async Task<string?> EnsureIconAsync(string executablePath, CancellationToken ct = default)
    {
        var path = PathHelper.Normalize(executablePath);
        if (path.Length == 0 || !File.Exists(path))
            return null;

        string cacheFile;
        try
        {
            var info = new FileInfo(path);
            var key = PathHelper.StableToken($"{path}|{info.LastWriteTimeUtc.Ticks}|{info.Length}");
            cacheFile = Path.Combine(_paths.IconCacheDirectory, key + ".png");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Could not stat {Path} for icon caching", path);
            return null;
        }

        if (File.Exists(cacheFile))
            return cacheFile;

        return await Task.Run(() => Extract(path, cacheFile), ct).ConfigureAwait(false);
    }

    private string? Extract(string exePath, string cacheFile)
    {
        try
        {
            Directory.CreateDirectory(_paths.IconCacheDirectory);

            using var bitmap = ExtractLargeIcon(exePath) ?? ExtractAssociated(exePath);
            if (bitmap is null)
            {
                _log.Debug("No icon could be extracted from {Path}", exePath);
                return null;
            }

            // Write to a temp file then move, so a crash never leaves a
            // half-written PNG that would be treated as a valid cache hit.
            var tmp = cacheFile + ".tmp";
            bitmap.Save(tmp, ImageFormat.Png);
            File.Move(tmp, cacheFile, overwrite: true);
            _log.Debug("Cached icon for {Path} -> {Cache}", exePath, cacheFile);
            return cacheFile;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Icon extraction failed for {Path}", exePath);
            return null;
        }
    }

    private static Bitmap? ExtractLargeIcon(string exePath)
    {
        var shinfo = new NativeMethods.SHFILEINFO();
        var result = NativeMethods.SHGetFileInfo(
            exePath, 0, ref shinfo, (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(),
            NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);

        if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
            return null;

        try
        {
            using var icon = Icon.FromHandle(shinfo.hIcon);
            return icon.ToBitmap();
        }
        finally
        {
            NativeMethods.DestroyIcon(shinfo.hIcon);
        }
    }

    private static Bitmap? ExtractAssociated(string exePath)
    {
        using var icon = Icon.ExtractAssociatedIcon(exePath);
        return icon?.ToBitmap();
    }

    [SupportedOSPlatform("windows")]
    private static class NativeMethods
    {
        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;

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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}

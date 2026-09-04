using System.Diagnostics;
using Orbit.Core.Services;
using Orbit.Tests.TestSupport;
using Serilog.Core;

namespace Orbit.Tests;

/// <summary>Real icon extraction against a genuine Windows executable.</summary>
public class IconServiceTests
{
    private static string ThisProcessExe => Process.GetCurrentProcess().MainModule!.FileName;

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public async Task EnsureIcon_extracts_a_png_and_caches_it()
    {
        using var ws = new TempWorkspace();
        var service = new IconService(ws.Paths, Logger.None);

        var first = await service.EnsureIconAsync(ThisProcessExe);

        Assert.NotNull(first);
        Assert.True(File.Exists(first));
        Assert.StartsWith(ws.Paths.IconCacheDirectory, first!);

        var header = new byte[8];
        await using (var fs = File.OpenRead(first))
            _ = await fs.ReadAsync(header);
        Assert.Equal(PngSignature, header);

        // Second call must hit the cache: same path, same file, untouched.
        var writtenAt = File.GetLastWriteTimeUtc(first);
        var second = await service.EnsureIconAsync(ThisProcessExe);

        Assert.Equal(first, second);
        Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(second!));
    }

    [Fact]
    public async Task EnsureIcon_returns_null_for_a_missing_file()
    {
        using var ws = new TempWorkspace();
        var service = new IconService(ws.Paths, Logger.None);

        var result = await service.EnsureIconAsync(
            Path.Combine(ws.Root, "does-not-exist.exe"));

        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureIcon_uses_a_sibling_ico_when_the_exe_has_none()
    {
        using var ws = new TempWorkspace();
        var service = new IconService(ws.Paths, Logger.None);

        // A tiny data file masquerading as an .exe (no icon resource at all).
        var appDir = Path.Combine(ws.Root, "SomeApp");
        Directory.CreateDirectory(appDir);
        var exe = Path.Combine(appDir, "someapp.exe");
        await File.WriteAllBytesAsync(exe, new byte[512]);

        // A genuine .ico next to it.
        WriteSolidIco(Path.Combine(appDir, "someapp.ico"));

        var result = await service.EnsureIconAsync(exe, iconHint: null);

        Assert.NotNull(result);
        Assert.True(File.Exists(result));
    }

    private static void WriteSolidIco(string path)
    {
        using var bmp = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.Clear(System.Drawing.Color.OrangeRed);

        var hicon = bmp.GetHicon();
        try
        {
            using var icon = System.Drawing.Icon.FromHandle(hicon);
            using var fs = File.Create(path);
            icon.Save(fs);
        }
        finally
        {
            NativeDestroyIcon(hicon);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "DestroyIcon")]
    private static extern bool NativeDestroyIcon(IntPtr handle);

    [Fact]
    public async Task EnsureIcon_prefers_an_explicit_hint()
    {
        using var ws = new TempWorkspace();
        var service = new IconService(ws.Paths, Logger.None);

        var exe = Path.Combine(ws.Root, "plain.exe");
        await File.WriteAllBytesAsync(exe, new byte[256]);

        var result = await service.EnsureIconAsync(exe, iconHint: $"{ThisProcessExe},0");

        Assert.NotNull(result);
        Assert.True(File.Exists(result));
    }
}

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
}

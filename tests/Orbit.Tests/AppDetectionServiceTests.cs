using Orbit.Core.Detection;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Orbit.Tests.TestSupport;
using Serilog.Core;

namespace Orbit.Tests;

public class AppDetectionServiceTests
{
    private readonly FakeAppRepository _repo = new();
    private readonly FakeExecutableInspector _inspector = new();

    private static DetectedApp App(string path, string name, string source = "Steam") => new()
    {
        Name = name,
        ExecutablePath = path,
        Kind = AppKind.Game,
        Source = source,
        Category = source
    };

    private AppDetectionService Build(params IInstalledAppSource[] sources) =>
        new(sources, _repo, _inspector, Logger.None);

    [Fact]
    public async Task Scan_filters_known_entries_and_dedupes()
    {
        await _repo.AddAsync(new AppEntry { Name = "Owned", ExecutablePath = @"C:\Games\Owned\owned.exe" });

        var service = Build(new FakeInstalledAppSource("Steam",
            App(@"C:\Games\New\new.exe", "New Game"),
            App(@"c:\games\new\NEW.exe", "New Game dup"),      // same path, different case
            App(@"C:\Games\Owned\owned.exe", "Owned again")));  // already in library

        var result = await service.ScanAsync();

        Assert.Single(result.NewItems);
        Assert.Equal("New Game", result.NewItems[0].Name);
        Assert.Equal(1, result.AlreadyInLibrary);
    }

    [Fact]
    public async Task Scan_drops_entries_whose_file_is_not_available()
    {
        var ghost = PathHelper.Normalize(@"C:\Games\Ghost\ghost.exe");
        _inspector.MissingPaths.Add(ghost);

        var service = Build(new FakeInstalledAppSource("Steam",
            App(@"C:\Games\Real\real.exe", "Real"),
            App(ghost, "Ghost")));

        var result = await service.ScanAsync();

        Assert.Single(result.NewItems);
        Assert.Equal("Real", result.NewItems[0].Name);
    }

    [Fact]
    public async Task Scan_keeps_going_when_one_source_throws()
    {
        var boom = new ThrowingSource();
        var service = Build(boom, new FakeInstalledAppSource("Steam", App(@"C:\ok\ok.exe", "OK")));

        var result = await service.ScanAsync();

        Assert.Single(result.NewItems);
    }

    [Fact]
    public async Task Scan_ignores_non_exe_paths()
    {
        var service = Build(new FakeInstalledAppSource("Registry",
            App(@"C:\Tools\thing.msi", "Installer"),
            App(@"C:\Tools\thing.exe", "Real Tool")));

        var result = await service.ScanAsync();

        Assert.Single(result.NewItems);
        Assert.Equal("Real Tool", result.NewItems[0].Name);
    }

    private sealed class ThrowingSource : IInstalledAppSource
    {
        public string DisplayName => "Broken";
        public IEnumerable<DetectedApp> Scan(CancellationToken ct) =>
            throw new InvalidOperationException("registry exploded");
    }
}

using Orbit.Core.Detection;
using Orbit.Tests.TestSupport;

namespace Orbit.Tests;

public class MainExecutableFinderTests
{
    private static void Touch(string path, int sizeBytes = 16)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[sizeBytes]);
    }

    [Fact]
    public void Prefers_the_executable_matching_the_hint()
    {
        using var ws = new TempWorkspace();
        var root = Path.Combine(ws.Root, "The Witcher 3");
        Touch(Path.Combine(root, "bin", "x64", "witcher3.exe"), 4_000_000);
        Touch(Path.Combine(root, "REDprelauncher.exe"), 1_000_000);

        var found = MainExecutableFinder.Find(root, "The Witcher 3");

        Assert.NotNull(found);
        Assert.Equal("witcher3.exe", Path.GetFileName(found!), ignoreCase: true);
    }

    [Fact]
    public void Skips_uninstallers_and_redistributable_folders()
    {
        using var ws = new TempWorkspace();
        var root = Path.Combine(ws.Root, "SomeGame");
        Touch(Path.Combine(root, "unins000.exe"));
        Touch(Path.Combine(root, "_CommonRedist", "vcredist_x64.exe"));
        Touch(Path.Combine(root, "SomeGame.exe"), 500_000);

        var found = MainExecutableFinder.Find(root, "SomeGame");

        Assert.Equal("SomeGame.exe", Path.GetFileName(found!), ignoreCase: true);
    }

    [Fact]
    public void Returns_null_for_a_missing_directory()
    {
        Assert.Null(MainExecutableFinder.Find(@"C:\definitely\not\here", "x"));
    }
}

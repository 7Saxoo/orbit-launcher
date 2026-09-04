using Orbit.Core.Identification;

namespace Orbit.Tests.Identification;

public class PathHeuristicsProviderTests
{
    private readonly PathHeuristicsProvider _provider = new();

    private static ExeSignals Signals(string path, string? company = null, string? description = null) => new()
    {
        NormalizedPath = path,
        FileName = Path.GetFileName(path),
        FileNameNoExt = Path.GetFileNameWithoutExtension(path),
        ParentFolderName = Path.GetFileName(Path.GetDirectoryName(path)!),
        Segments = path.ToLowerInvariant().Split('\\', StringSplitOptions.RemoveEmptyEntries),
        CompanyName = company,
        FileDescription = description
    };

    private async Task<AppIdentification> Run(ExeSignals s) =>
        (await _provider.IdentifyAsync(s, null, CancellationToken.None))!;

    [Fact]
    public async Task Steam_common_folder_is_a_game()
    {
        var r = await Run(Signals(@"D:\SteamLibrary\steamapps\common\Elden Ring\eldenring.exe"));
        Assert.Equal(IdentificationKind.Game, r.Kind);
        Assert.True(r.Confidence >= 0.8);
        Assert.Equal("Jeux", r.SuggestedCategory);
        Assert.Contains("Steam", r.Source);
    }

    [Fact]
    public async Task WindowsApps_folder_is_an_application()
    {
        var r = await Run(Signals(@"C:\Program Files\WindowsApps\Microsoft.App_1.0\app.exe"));
        Assert.Equal(IdentificationKind.Application, r.Kind);
        Assert.Equal("Applications", r.SuggestedCategory);
    }

    [Fact]
    public async Task Known_publisher_is_an_application()
    {
        var r = await Run(Signals(@"C:\Tools\ff\firefox.exe", company: "Mozilla Corporation"));
        Assert.Equal(IdentificationKind.Application, r.Kind);
        Assert.True(r.Confidence >= 0.55);
    }

    [Fact]
    public async Task Unremarkable_path_is_unknown()
    {
        var r = await Run(Signals(@"D:\stuff\thing\thing.exe"));
        Assert.Equal(IdentificationKind.Unknown, r.Kind);
        Assert.True(r.Confidence < AppIdentificationService.MinConfidence);
    }

    [Fact]
    public async Task Launcher_named_exe_in_store_folder_has_lower_confidence()
    {
        var game = await Run(Signals(@"D:\SteamLibrary\steamapps\common\X\x.exe"));
        var launcher = await Run(Signals(@"D:\SteamLibrary\steamapps\common\X\launcher.exe"));
        Assert.True(launcher.Confidence < game.Confidence);
    }
}

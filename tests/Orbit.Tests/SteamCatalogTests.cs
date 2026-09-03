using Orbit.Core.Detection;

namespace Orbit.Tests;

public class SteamCatalogTests
{
    [Fact]
    public void ParseLibraryFolders_reads_modern_shape()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"  "C:\\Program Files (x86)\\Steam"
                }
                "1"
                {
                    "path"  "D:\\SteamLibrary"
                }
            }
            """;

        var libs = SteamCatalog.ParseLibraryFolders(vdf);

        Assert.Equal(2, libs.Count);
        Assert.Contains(@"C:\Program Files (x86)\Steam", libs);
        Assert.Contains(@"D:\SteamLibrary", libs);
    }

    [Fact]
    public void ParseLibraryFolders_reads_legacy_shape()
    {
        const string vdf = """
            "LibraryFolders"
            {
                "TimeNextStatsReport" "123"
                "ContentStatsID"      "456"
                "1" "D:\\Jeux\\SteamLibrary"
            }
            """;

        var libs = SteamCatalog.ParseLibraryFolders(vdf);
        Assert.Single(libs);
        Assert.Equal(@"D:\Jeux\SteamLibrary", libs[0]);
    }

    [Fact]
    public void ParseAppManifest_returns_game_details()
    {
        const string acf = """
            "AppState"
            {
                "appid"      "292030"
                "name"       "The Witcher 3: Wild Hunt"
                "installdir" "The Witcher 3"
            }
            """;

        var game = SteamCatalog.ParseAppManifest(acf);

        Assert.NotNull(game);
        Assert.Equal("292030", game!.AppId);
        Assert.Equal("The Witcher 3: Wild Hunt", game.Name);
        Assert.Equal("The Witcher 3", game.InstallDir);
    }

    [Fact]
    public void ParseAppManifest_skips_steamworks_redistributables()
    {
        const string acf = """
            "AppState"
            {
                "appid"      "228980"
                "name"       "Steamworks Common Redistributables"
                "installdir" "Steamworks Shared"
            }
            """;

        Assert.Null(SteamCatalog.ParseAppManifest(acf));
    }

    [Fact]
    public void ParseAppManifest_requires_an_install_dir()
    {
        const string acf = """
            "AppState"
            {
                "appid" "1"
                "name"  "Broken"
            }
            """;

        Assert.Null(SteamCatalog.ParseAppManifest(acf));
    }
}

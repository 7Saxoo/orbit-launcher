using Orbit.Core.Identification;
using Orbit.Core.Models;

namespace Orbit.Tests.Identification;

public class CategoryClassifierTests
{
    [Theory]
    [InlineData("Brave", "Brave Software", @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe", "Navigateur")]
    [InlineData("Mozilla Firefox", "Mozilla", @"C:\Program Files\Mozilla Firefox\firefox.exe", "Navigateur")]
    [InlineData("Git", "The Git Development Community", @"C:\Program Files\Git\bin\git.exe", "Développement")]
    [InlineData("Visual Studio Code", "Microsoft", @"C:\Users\x\AppData\Local\Programs\Microsoft VS Code\Code.exe", "Développement")]
    [InlineData("Discord", "Discord Inc.", @"C:\Users\x\AppData\Local\Discord\app\Discord.exe", "Communication")]
    [InlineData("VLC media player", "VideoLAN", @"C:\Program Files\VideoLAN\VLC\vlc.exe", "Multimédia")]
    [InlineData("7-Zip", "Igor Pavlov", @"C:\Program Files\7-Zip\7zFM.exe", "Utilitaires")]
    [InlineData("Steam", "Valve", @"C:\Program Files (x86)\Steam\steam.exe", "Jeu — launcher")]
    public void Classifies_known_applications(string name, string publisher, string path, string expected)
    {
        Assert.Equal(expected, CategoryClassifier.Classify(name, publisher, path, AppKind.Application));
    }

    [Fact]
    public void A_game_is_always_Jeux()
    {
        Assert.Equal("Jeux", CategoryClassifier.Classify("Elden Ring", "FromSoftware", @"D:\x\eldenring.exe", AppKind.Game));
    }

    [Fact]
    public void A_steam_common_path_is_a_game_even_when_marked_application()
    {
        Assert.Equal("Jeux", CategoryClassifier.Classify(
            "Some Game", null, @"D:\SteamLibrary\steamapps\common\Some Game\game.exe", AppKind.Application));
    }

    [Fact]
    public void Unknown_apps_fall_back_to_Applications()
    {
        Assert.Equal("Applications", CategoryClassifier.Classify("Widget 3000", "Acme", @"C:\x\w.exe", AppKind.Application));
    }
}

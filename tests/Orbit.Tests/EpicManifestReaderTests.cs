using Orbit.Core.Detection;
using Orbit.Core.Models;

namespace Orbit.Tests;

public class EpicManifestReaderTests
{
    [Fact]
    public void Parses_a_valid_manifest()
    {
        const string json = """
            {
              "DisplayName": "Rocket League",
              "InstallLocation": "D:\\Epic\\rocketleague",
              "LaunchExecutable": "Binaries\\Win64\\RocketLeague.exe",
              "DeveloperName": "Psyonix LLC"
            }
            """;

        var app = EpicManifestReader.Parse(json);

        Assert.NotNull(app);
        Assert.Equal("Rocket League", app!.Name);
        Assert.Equal(AppKind.Game, app.Kind);
        Assert.Equal("Epic Games", app.Source);
        Assert.Equal(@"D:\Epic\rocketleague\Binaries\Win64\RocketLeague.exe", app.ExecutablePath);
        Assert.Equal("Psyonix LLC", app.Publisher);
    }

    [Theory]
    [InlineData("{ \"DisplayName\": \"X\", \"InstallLocation\": \"C:\\\\g\" }")] // no LaunchExecutable
    [InlineData("{ \"LaunchExecutable\": \"g.exe\" }")]                          // no name / location
    [InlineData("not json at all")]
    public void Returns_null_for_incomplete_or_invalid_input(string json)
    {
        Assert.Null(EpicManifestReader.Parse(json));
    }

    [Fact]
    public void Returns_null_when_launch_target_is_not_an_exe()
    {
        const string json = """
            {
              "DisplayName": "Weird",
              "InstallLocation": "C:\\games\\weird",
              "LaunchExecutable": "run.bat"
            }
            """;

        Assert.Null(EpicManifestReader.Parse(json));
    }
}

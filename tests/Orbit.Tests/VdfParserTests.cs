using Orbit.Core.Detection;

namespace Orbit.Tests;

public class VdfParserTests
{
    [Fact]
    public void Parses_nested_blocks_and_values()
    {
        const string vdf = """
            "AppState"
            {
                "appid"      "230410"
                "name"       "Warframe"
                "installdir" "Warframe"
                "UserConfig"
                {
                    "language" "french"
                }
            }
            """;

        var root = VdfParser.Parse(vdf);

        Assert.Equal("230410", root["AppState"]!.ValueOf("appid"));
        Assert.Equal("Warframe", root["AppState"]!.ValueOf("name"));
        Assert.Equal("french", root["AppState"]!["UserConfig"]!.ValueOf("language"));
    }

    [Fact]
    public void Ignores_line_comments_and_handles_escaped_quotes()
    {
        const string vdf = """
            // a comment
            "root"
            {
                "path" "C:\\Games\\\"Weird\" Name"
            }
            """;

        var root = VdfParser.Parse(vdf);
        Assert.Equal("C:\\Games\\\"Weird\" Name", root["root"]!.ValueOf("path"));
    }
}

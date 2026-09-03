using Orbit.Core.Infrastructure;

namespace Orbit.Tests;

public class PathHelperTests
{
    [Theory]
    [InlineData("  C:\\Program Files\\App\\app.exe  ", "C:\\Program Files\\App\\app.exe")]
    [InlineData("\"C:\\Program Files (x86)\\Test\\App.exe\"", "C:\\Program Files (x86)\\Test\\App.exe")]
    [InlineData("D:\\Games\\Mon Jeu\\Game.exe", "D:\\Games\\Mon Jeu\\Game.exe")]
    [InlineData("C:\\Jeux\\Caf\u00e9\\Jeu accentu\u00e9.exe", "C:\\Jeux\\Caf\u00e9\\Jeu accentu\u00e9.exe")]
    public void Normalize_trims_unquotes_and_keeps_spaces_and_accents(string input, string expected)
    {
        Assert.Equal(expected, PathHelper.Normalize(input));
    }

    [Fact]
    public void Normalize_expands_environment_variables()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        Assert.Equal(expected, PathHelper.Normalize("%WINDIR%\\explorer.exe"),
            ignoreCase: true);
    }

    [Fact]
    public void Normalize_of_null_or_blank_is_empty()
    {
        Assert.Equal(string.Empty, PathHelper.Normalize(null));
        Assert.Equal(string.Empty, PathHelper.Normalize("   "));
    }

    [Theory]
    [InlineData("game.EXE", true)]
    [InlineData("C:\\a\\b\\tool.exe", true)]
    [InlineData("C:\\a\\b\\readme.txt", false)]
    [InlineData("C:\\a\\b\\noext", false)]
    public void HasExecutableExtension_is_case_insensitive(string path, bool expected)
    {
        Assert.Equal(expected, PathHelper.HasExecutableExtension(path));
    }

    [Theory]
    [InlineData("C:\\Program Files\\a.exe", "\"C:\\Program Files\\a.exe\"")]
    [InlineData("C:\\Tools\\a.exe", "C:\\Tools\\a.exe")]
    [InlineData("\"already quoted path\"", "\"already quoted path\"")]
    public void QuoteIfNeeded_only_quotes_when_whitespace_present(string input, string expected)
    {
        Assert.Equal(expected, PathHelper.QuoteIfNeeded(input));
    }

    [Theory]
    [InlineData("C:\\Games\\App.exe", "c:\\games\\app.exe", true)]
    [InlineData("C:\\Games\\App.exe\\", "C:/Games/App.exe", true)]
    [InlineData("C:\\Games\\App.exe", "C:\\Games\\Other.exe", false)]
    [InlineData("", "", false)]
    public void AreSamePath_ignores_case_separator_and_trailing_slash(string a, string b, bool expected)
    {
        Assert.Equal(expected, PathHelper.AreSamePath(a, b));
    }

    [Fact]
    public void GetContainingDirectory_returns_parent_folder()
    {
        Assert.Equal("C:\\Program Files\\Mon App",
            PathHelper.GetContainingDirectory("C:\\Program Files\\Mon App\\app.exe"));
    }

    [Fact]
    public void StableToken_is_deterministic_and_case_folded()
    {
        var a = PathHelper.StableToken("C:\\Games\\App.exe|123|456");
        var b = PathHelper.StableToken("c:\\games\\app.exe|123|456");
        var c = PathHelper.StableToken("C:\\Games\\App.exe|999|456");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(16, a.Length);
    }
}

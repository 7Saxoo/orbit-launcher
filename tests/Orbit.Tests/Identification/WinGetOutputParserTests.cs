using Orbit.Core.Identification;

namespace Orbit.Tests.Identification;

public class WinGetOutputParserTests
{
    private const string Sample =
        "Nom              Id                    Version   Source\n" +
        "-----------------------------------------------------------\n" +
        "Mozilla Firefox  Mozilla.Firefox       131.0     winget\n" +
        "Firefox Beta     Mozilla.Firefox.Beta  132.0b1   winget\n";

    [Fact]
    public void Parses_rows_into_packages()
    {
        var packages = WinGetOutputParser.Parse(Sample);

        Assert.Equal(2, packages.Count);
        Assert.Equal("Mozilla Firefox", packages[0].Name);
        Assert.Equal("Mozilla.Firefox", packages[0].Id);
        Assert.Equal("131.0", packages[0].Version);
        Assert.Equal("winget", packages[0].Source);
    }

    [Fact]
    public void Empty_or_headerless_output_yields_nothing()
    {
        Assert.Empty(WinGetOutputParser.Parse(""));
        Assert.Empty(WinGetOutputParser.Parse("no table here\njust text"));
    }
}

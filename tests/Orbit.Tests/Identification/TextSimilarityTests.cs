using Orbit.Core.Identification;

namespace Orbit.Tests.Identification;

public class TextSimilarityTests
{
    [Fact]
    public void Identical_names_score_one()
    {
        Assert.Equal(1.0, TextSimilarity.Score("Elden Ring", "elden ring"), 3);
    }

    [Fact]
    public void Containment_scores_high()
    {
        Assert.True(TextSimilarity.Score("Cyberpunk", "Cyberpunk 2077") >= 0.8);
    }

    [Fact]
    public void Unrelated_names_score_low()
    {
        Assert.True(TextSimilarity.Score("Firefox", "Elden Ring") < 0.4);
    }

    [Theory]
    [InlineData("The Witcher 3: Wild Hunt - GAME of the Year", "witcher 3 wild hunt of year")]
    [InlineData("Rocket_League.exe", "rocket league")]
    public void Normalize_strips_case_punctuation_and_noise(string input, string expected)
    {
        Assert.Equal(expected, TextSimilarity.Normalize(input));
    }
}

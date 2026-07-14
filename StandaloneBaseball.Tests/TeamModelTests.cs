using System.Linq;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class TeamModelTests
{
    [Fact]
    public void NormalizeText_PreservesTeamNameAndMascotAndClampsScoreboardName()
    {
        string fullTeamName = string.Join(" ", Enumerable.Repeat("Long School Name", 40));
        string fullMascot = string.Join(" ", Enumerable.Repeat("Long Mascot Name", 40));
        var team = new Team
        {
            City = fullTeamName,
            Nickname = fullMascot,
            ScoreboardAbbreviation = "longabbr",
            CoachName = "Coach",
            ScoreboardTemplate = new TeamScoreboardTemplate()
        };

        team.NormalizeText();

        Assert.Equal(fullTeamName, team.City);
        Assert.Equal(fullMascot, team.Nickname);
        Assert.Equal("LONGAB", team.ScoreboardAbbreviation);
        Assert.Equal("LONGAB", team.ScoreboardName);
        Assert.Equal("LONGAB", team.ScoreboardTemplate.PreferredAbbreviation);
        Assert.Equal(4, team.UniformSets.Select(u => u.Category).Distinct().Count());
    }
}

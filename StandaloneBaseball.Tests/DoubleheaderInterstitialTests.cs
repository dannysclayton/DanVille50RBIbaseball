using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class DoubleheaderInterstitialTests
{
    [Fact]
    public void IsSecondGameOfDoubleheader_RequiresMatchingGameOneOnSameWeekAndDay()
    {
        Team away = RegressionTestData.CreateTeam("Away");
        Team home = RegressionTestData.CreateTeam("Home");
        var first = Game(away, home, 4, "Sunday", 1);
        var second = Game(away, home, 4, "Sunday", 2);
        var season = new Season { Schedule = new List<ScheduledGame> { first, second } };

        Assert.False(DoubleheaderInterstitialRules.IsSecondGameOfDoubleheader(season, first));
        Assert.True(DoubleheaderInterstitialRules.IsSecondGameOfDoubleheader(season, second));
    }

    [Fact]
    public void IsSecondGameOfDoubleheader_DoesNotTreatUnrelatedSecondGameAsDoubleheader()
    {
        Team away = RegressionTestData.CreateTeam("Away");
        Team home = RegressionTestData.CreateTeam("Home");
        Team other = RegressionTestData.CreateTeam("Other");
        var first = Game(away, home, 4, "Sunday", 1);
        var unrelated = Game(away, other, 4, "Sunday", 2);
        var season = new Season { Schedule = new List<ScheduledGame> { first, unrelated } };

        Assert.False(DoubleheaderInterstitialRules.IsSecondGameOfDoubleheader(season, unrelated));
    }

    private static ScheduledGame Game(Team away, Team home, int week, string day, int dayGameNumber)
    {
        return new ScheduledGame
        {
            AwayTeamId = away.Id,
            HomeTeamId = home.Id,
            Week = week,
            DayLabel = day,
            DayGameNumber = dayGameNumber
        };
    }
}

using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class SeasonOpeningInterstitialTests
{
    [Fact]
    public void IsFirstScheduledGame_UsesGlobalScheduleOrder()
    {
        var later = new ScheduledGame { GameNumber = 2, Week = 1, WeekGameNumber = 2 };
        var first = new ScheduledGame { GameNumber = 1, Week = 1, WeekGameNumber = 1 };
        var season = new Season { Schedule = new List<ScheduledGame> { later, first } };

        Assert.True(SeasonOpeningInterstitialRules.IsFirstScheduledGame(season, first));
        Assert.False(SeasonOpeningInterstitialRules.IsFirstScheduledGame(season, later));
    }

    [Fact]
    public void IsFirstScheduledGame_RequiresScheduledSeasonGame()
    {
        var scheduled = new ScheduledGame { GameNumber = 1 };
        Assert.False(SeasonOpeningInterstitialRules.IsFirstScheduledGame(null, scheduled));
        Assert.False(SeasonOpeningInterstitialRules.IsFirstScheduledGame(new Season(), scheduled));
        Assert.False(SeasonOpeningInterstitialRules.IsFirstScheduledGame(new Season { Schedule = new List<ScheduledGame> { scheduled } }, null));
    }
}

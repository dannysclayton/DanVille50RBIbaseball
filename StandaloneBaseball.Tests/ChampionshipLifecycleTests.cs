using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class ChampionshipLifecycleTests
{
    [Fact]
    public void WorldSeriesWinnerPersistsChampionCoachAndClearsAllInjuries()
    {
        Team winner = RegressionTestData.CreateTeam("Winner");
        Team loser = RegressionTestData.CreateTeam("Loser");
        Player rosterInjury = winner.Roster[0];
        rosterInjury.InjuryStatus = PlayerInjuryStatus.Out;
        rosterInjury.InjuryName = "Elbow strain";
        rosterInjury.InjuryGamesRemaining = 20;
        rosterInjury.InjurySeverity = 3;
        var reserveInjury = new Player
        {
            Name = "Injured Reserve",
            InjuryStatus = PlayerInjuryStatus.DayToDay,
            InjuryName = "Knee soreness",
            InjuryGamesRemaining = 2,
            InjurySeverity = 1
        };
        loser.InjuredReserve.Add(reserveInjury);
        var league = RegressionTestData.CreateLeague(winner, loser);
        var season = new Season();
        var series = new PlayoffSeries
        {
            Round = 6,
            RoundName = "World Series",
            BracketGroup = "World Series",
            TeamAId = winner.Id,
            TeamBId = loser.Id,
            TeamACoachId = winner.CoachId,
            TeamBCoachId = loser.CoachId,
            WinnerTeamId = winner.Id
        };

        bool changed = ChampionshipLifecycleEngine.TryRecordChampion(league, season, series, out Team champion);

        Assert.True(changed);
        Assert.Same(winner, champion);
        Assert.Equal(winner.Id, season.ChampionTeamId);
        Assert.Equal(winner.CoachId, series.WinnerCoachId);
        AssertHealthy(rosterInjury);
        AssertHealthy(reserveInjury);
    }

    [Fact]
    public void RecordingSameChampionTwiceIsIdempotent()
    {
        Team winner = RegressionTestData.CreateTeam("Winner");
        Team loser = RegressionTestData.CreateTeam("Loser");
        var league = RegressionTestData.CreateLeague(winner, loser);
        var season = new Season();
        var series = new PlayoffSeries
        {
            RoundName = "World Series",
            TeamAId = winner.Id,
            TeamBId = loser.Id,
            WinnerTeamId = winner.Id
        };

        Assert.True(ChampionshipLifecycleEngine.TryRecordChampion(league, season, series, out _));
        Assert.False(ChampionshipLifecycleEngine.TryRecordChampion(league, season, series, out Team champion));
        Assert.Same(winner, champion);
    }

    [Fact]
    public void NonFinalSeriesCannotCrownChampion()
    {
        Team winner = RegressionTestData.CreateTeam("Winner");
        Team loser = RegressionTestData.CreateTeam("Loser");
        var league = RegressionTestData.CreateLeague(winner, loser);
        var season = new Season();
        var series = new PlayoffSeries
        {
            RoundName = "Area",
            TeamAId = winner.Id,
            TeamBId = loser.Id,
            WinnerTeamId = winner.Id
        };

        Assert.False(ChampionshipLifecycleEngine.TryRecordChampion(league, season, series, out Team champion));
        Assert.Null(champion);
        Assert.Null(season.ChampionTeamId);
    }

    private static void AssertHealthy(Player player)
    {
        Assert.Equal(PlayerInjuryStatus.Healthy, player.InjuryStatus);
        Assert.Equal("", player.InjuryName);
        Assert.Equal(0, player.InjuryGamesRemaining);
        Assert.Equal(0, player.InjurySeverity);
    }
}

using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class PitchingFatigueRegressionTests
{
    [Theory]
    [InlineData(3, 10, 5, 90)]
    [InlineData(4, 5, 3, 95)]
    [InlineData(5, 0, 0, 100)]
    public void RotationSizeAppliesPitchCountAndInjuryPenalties(
        int rotationSize, int pitchPenalty, int injuryBonus, int expectedLimit)
    {
        Team team = RegressionTestData.CreateTeam("Team", pitcherCount: 5);
        team.PitchingPlan.RotationSize = rotationSize;
        Player starter = team.Roster.First(player => player.Role == PlayerRole.Pitcher);

        Assert.Equal(pitchPenalty, PitchingRotationEngine.RotationPitchCountPenaltyPercent(team));
        Assert.Equal(injuryBonus, PitchingRotationEngine.RotationInjuryRiskBonusPercent(team));
        Assert.Equal(expectedLimit, PitchingRotationEngine.ApplyStarterPitchCountPenalty(starter, team, 100));
    }

    [Fact]
    public void StarterReliefOutsOverThreeAccumulateAcrossGames()
    {
        Team team = RegressionTestData.CreateTeam("Team", pitcherCount: 5);
        var season = new Season();
        Player starter = team.Roster.First(player => player.Role == PlayerRole.Pitcher);

        AddReliefGame(season, team, starter, 4);
        PitchingRotationEngine.UpdateSeasonPitcherUsage(season, team, season.Games[^1]);
        AddReliefGame(season, team, starter, 4);
        PitchingRotationEngine.UpdateSeasonPitcherUsage(season, team, season.Games[^1]);

        Assert.Equal(8, starter.StarterReliefOutsSinceLastStart);
        Assert.Equal(20, starter.NextStartPitchCountPenaltyPercent);
        Assert.Equal(80, PitchingRotationEngine.ApplyStarterPitchCountPenalty(starter, team, 100));
        Assert.Contains("20%", season.PitcherUsage[starter.Id].Notes);
    }

    [Fact]
    public void StarterAppearanceResetsReliefPenaltyAndAdvancesRotation()
    {
        Team team = RegressionTestData.CreateTeam("Team", pitcherCount: 5);
        var season = new Season();
        Player starter = team.Roster.First(player => player.Id == team.PitchingPlan.StarterRotationIds[0]);
        starter.StarterReliefOutsSinceLastStart = 7;
        starter.NextStartPitchCountPenaltyPercent = 30;
        var result = RegressionTestData.Result(team, RegressionTestData.CreateTeam("Opponent"), 2, 1);
        result.Lines.Add(new PlayerGameLine
        {
            TeamId = team.Id,
            PlayerId = starter.Id,
            PlayerName = starter.Name,
            Pitcher = true,
            StartingPitcher = true,
            IPOuts = 15
        });
        season.Games.Add(result);

        PitchingRotationEngine.UpdateSeasonPitcherUsage(season, team, result);

        Assert.Equal(0, starter.StarterReliefOutsSinceLastStart);
        Assert.Equal(0, starter.NextStartPitchCountPenaltyPercent);
        Assert.Equal(1, team.PitchingPlan.NextStarterSlot);
    }

    [Fact]
    public void RelieverPenaltyIncreasesForConsecutiveGamesAndResetsAfterRest()
    {
        Team team = RegressionTestData.CreateTeam("Team", pitcherCount: 6);
        Player reliever = team.Roster.First(player =>
            player.Role == PlayerRole.Pitcher && !team.PitchingPlan.StarterRotationIds.Contains(player.Id));
        var season = new Season();

        for (int game = 1; game <= 3; game++)
        {
            AddReliefGame(season, team, reliever, 3);
            PitchingRotationEngine.UpdateSeasonPitcherUsage(season, team, season.Games[^1]);
            Assert.Equal(game, reliever.ConsecutiveReliefGames);
            Assert.Equal((game - 1) * 10,
                PitchingRotationEngine.RelieverBackToBackPenaltyPercent(reliever.ConsecutiveReliefGames));
        }

        var restGame = RegressionTestData.Result(team, RegressionTestData.CreateTeam("Rest Opponent"), 3, 2);
        season.Games.Add(restGame);
        PitchingRotationEngine.UpdateSeasonPitcherUsage(season, team, restGame);

        Assert.Equal(0, reliever.ConsecutiveReliefGames);
        Assert.Equal(0, season.PitcherUsage[reliever.Id].ConsecutiveReliefGames);
    }

    private static void AddReliefGame(Season season, Team team, Player pitcher, int outs)
    {
        var opponent = RegressionTestData.CreateTeam("Opponent " + (season.Games.Count + 1));
        var result = RegressionTestData.Result(team, opponent, 3, 2);
        result.Lines.Add(new PlayerGameLine
        {
            TeamId = team.Id,
            PlayerId = pitcher.Id,
            PlayerName = pitcher.Name,
            Pitcher = true,
            StartingPitcher = false,
            IPOuts = outs
        });
        season.Games.Add(result);
    }
}

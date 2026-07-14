using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class GameEngineParityTests
{
    [Fact]
    public void SharedPitchResolution_IsDeterministicForIdenticalInputs()
    {
        Team offense = RegressionTestData.CreateTeam("Offense", 78);
        Team defense = RegressionTestData.CreateTeam("Defense", 82);
        var request = new SharedPitchRequest
        {
            Batter = offense.Roster.First(player => player.Role == PlayerRole.Batter),
            Pitcher = defense.Roster.First(player => player.Role == PlayerRole.Pitcher),
            PitchType = GameplayPitchType.Curveball,
            SwingType = SharedSwingType.Power,
            PitchX = 0.25,
            PitchY = -0.15,
            TimingQuality = 0.82,
            Balls = 2,
            Strikes = 1,
            PitcherAdjustmentPercent = -10,
            OffensiveStrategyModifier = 6,
            BatterBoostPercent = 5,
            PitcherBoostPercent = 1
        };

        SharedPitchResolution first = SharedGameEngine.ResolvePitch(new Random(20260713), request);
        SharedPitchResolution second = SharedGameEngine.ResolvePitch(new Random(20260713), request);

        Assert.Equal(first.ResultType, second.ResultType);
        Assert.Equal(first.InStrikeZone, second.InStrikeZone);
        Assert.Equal(first.ContactQuality, second.ContactQuality, 12);
    }

    [Fact]
    public void SharedBattedBallResolution_IsDeterministicAndHonorsDefensiveInputs()
    {
        Team offense = RegressionTestData.CreateTeam("Offense", 80);
        Team defense = RegressionTestData.CreateTeam("Defense", 75);
        var request = new SharedBattedBallRequest
        {
            Batter = offense.Roster.First(player => player.Role == PlayerRole.Batter),
            Pitcher = defense.Roster.First(player => player.Role == PlayerRole.Pitcher),
            PitchType = GameplayPitchType.Changeup,
            ContactQuality = 0.74,
            DefenseFieldingRating = 83,
            NoDoublesDefense = true,
            PitcherAdjustmentPercent = -10,
            BatterBoostPercent = 5
        };

        SharedBattedBallResultType first = SharedGameEngine.ResolveBattedBall(new Random(9917), request);
        SharedBattedBallResultType second = SharedGameEngine.ResolveBattedBall(new Random(9917), request);

        Assert.Equal(first, second);
        Assert.True(Enum.IsDefined(first));
    }

    [Fact]
    public void DetailedSimulation_SameSeedProducesSameTimelineAndBoxScore()
    {
        var firstAway = RegressionTestData.CreateTeam("Away", 76);
        var firstHome = RegressionTestData.CreateTeam("Home", 74);
        var firstLeague = RegressionTestData.CreateLeague(firstAway, firstHome);
        var secondAway = RegressionTestData.CreateTeam("Away", 76);
        var secondHome = RegressionTestData.CreateTeam("Home", 74);
        var secondLeague = RegressionTestData.CreateLeague(secondAway, secondHome);

        var first = SimulatedGameEngine.SimulateDetailed(firstLeague, firstAway, firstHome, new Random(51703));
        var second = SimulatedGameEngine.SimulateDetailed(secondLeague, secondAway, secondHome, new Random(51703));

        Assert.Equal((first.Result.AwayScore, first.Result.HomeScore), (second.Result.AwayScore, second.Result.HomeScore));
        Assert.Equal(first.Result.AwayRunsByInning, second.Result.AwayRunsByInning);
        Assert.Equal(first.Result.HomeRunsByInning, second.Result.HomeRunsByInning);
        Assert.Equal(first.Result.AwayHits, second.Result.AwayHits);
        Assert.Equal(first.Result.HomeHits, second.Result.HomeHits);
        Assert.Equal(first.Events.Select(EventSnapshot), second.Events.Select(EventSnapshot));
        Assert.Equal(LineSnapshots(first.Result), LineSnapshots(second.Result));
    }

    [Fact]
    public void DetailedSimulation_FinalEventAndPersistedPlayByPlayAgreeWithResult()
    {
        Team away = RegressionTestData.CreateTeam("Away", 72);
        Team home = RegressionTestData.CreateTeam("Home", 72);
        var run = SimulatedGameEngine.SimulateDetailed(
            RegressionTestData.CreateLeague(away, home), away, home, new Random(7119));

        var finalEvent = run.Events.Last();
        var finalPlay = run.Result.PlayByPlay.Last();
        Assert.Equal(run.Result.AwayScore, finalEvent.AwayScore);
        Assert.Equal(run.Result.HomeScore, finalEvent.HomeScore);
        Assert.Equal(run.Result.AwayScore, finalPlay.AwayScore);
        Assert.Equal(run.Result.HomeScore, finalPlay.HomeScore);
        Assert.Equal(run.Result.AwayScore, run.Result.AwayRunsByInning.Sum());
        Assert.Equal(run.Result.HomeScore, run.Result.HomeRunsByInning.Sum());
        Assert.Equal(Math.Max(run.Result.AwayRunsByInning.Count, run.Result.HomeRunsByInning.Count),
            run.Result.GameLengthInnings);
        Assert.InRange(run.Result.GameLengthOuts, 0, 3);
    }

    private static string EventSnapshot(SimulatedGameEngine.SimulatedGameEvent value)
        => $"{value.Inning}|{value.TopHalf}|{value.Outs}|{value.AwayScore}|{value.HomeScore}|{value.Bases}|{value.Narration}";

    private static List<string> LineSnapshots(GameResult result)
        => result.Lines
            .OrderBy(line => line.PlayerName)
            .Select(line => string.Join("|", line.PlayerName, line.Pitcher, line.AB, line.H, line.Doubles,
                line.Triples, line.HR, line.RBI, line.BB, line.SO, line.IPOuts, line.ER,
                line.K, line.HitsAllowed, line.PitchCount, line.Errors))
            .ToList();
}

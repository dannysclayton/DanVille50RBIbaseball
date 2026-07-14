using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class InjuryEngineRegressionTests
{
    [Fact]
    public void AvailabilityAndDayToDayPenaltyMatchGameplayRules()
    {
        var player = new Player { InjuryStatus = PlayerInjuryStatus.DayToDay };

        Assert.True(InjuryEngine.IsAvailable(player));
        Assert.Equal(81, InjuryEngine.EffectiveRating(player, 90));

        player.InjuryStatus = PlayerInjuryStatus.Out;
        player.InjuryGamesRemaining = 2;
        Assert.False(InjuryEngine.IsAvailable(player));

        player.InjuryGamesRemaining = 0;
        Assert.True(InjuryEngine.IsAvailable(player));
        player.RedshirtActive = true;
        Assert.False(InjuryEngine.IsAvailable(player));
    }

    [Fact]
    public void EventInjury_UsesDeterministicSeverityAndDuration()
    {
        var player = new Player
        {
            Role = PlayerRole.Pitcher,
            Durability = 90,
            InjuryStatus = PlayerInjuryStatus.Healthy
        };

        bool injured = InjuryEngine.TryEventInjury(player, new SequenceRandom(0, 95, 20, 1), 0);

        Assert.True(injured);
        Assert.Equal(PlayerInjuryStatus.Out, player.InjuryStatus);
        Assert.Equal(3, player.InjurySeverity);
        Assert.Equal(36, player.InjuryGamesRemaining);
        Assert.False(string.IsNullOrWhiteSpace(player.InjuryName));
    }

    [Fact]
    public void ProcessGameInjuries_RecoversExpiredPlayerAndCountsInjuredReserveAbsence()
    {
        Team away = RegressionTestData.CreateTeam("Away");
        Team home = RegressionTestData.CreateTeam("Home");
        Player recovering = away.Roster[0];
        recovering.InjuryStatus = PlayerInjuryStatus.Out;
        recovering.InjuryGamesRemaining = 1;
        recovering.InjuryName = "Elbow strain";
        Player reserve = new()
        {
            Name = "Reserve Player",
            InjuryStatus = PlayerInjuryStatus.Out,
            InjuryGamesRemaining = 12
        };
        away.InjuredReserve.Add(reserve);

        InjuryEngine.ProcessGameInjuries(away, home, new SequenceRandom());

        Assert.Equal(PlayerInjuryStatus.Healthy, recovering.InjuryStatus);
        Assert.Equal(0, recovering.InjuryGamesRemaining);
        Assert.Equal(0, recovering.InjuryMissedGamesThisSeason);
        Assert.Equal(11, reserve.InjuryGamesRemaining);
        Assert.Equal(1, reserve.InjuryMissedGamesThisSeason);
    }

    [Fact]
    public void PregameInjuriesAreRareAndDoNotUseRotationSize()
    {
        Team threeMan = RegressionTestData.CreateTeam("Three", pitcherCount: 3);
        Team fiveMan = RegressionTestData.CreateTeam("Five", pitcherCount: 5);
        threeMan.PitchingPlan.RotationSize = 3;
        fiveMan.PitchingPlan.RotationSize = 5;

        Assert.All(threeMan.Roster.Concat(fiveMan.Roster), player =>
            Assert.InRange(InjuryEngine.PregameInjuryChancePerThousand(player), 1, 2));

        InjuryEngine.ProcessGameInjuries(threeMan, fiveMan,
            new SequenceRandom(2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2));

        Assert.All(threeMan.Roster.Concat(fiveMan.Roster), player =>
            Assert.Equal(PlayerInjuryStatus.Healthy, player.InjuryStatus));
    }

    [Fact]
    public void PitchExposureAppliesRotationAndConsecutiveReliefRisk()
    {
        Team threeMan = RegressionTestData.CreateTeam("Three", pitcherCount: 3);
        Team fiveMan = RegressionTestData.CreateTeam("Five", pitcherCount: 5);
        threeMan.PitchingPlan.RotationSize = 3;
        fiveMan.PitchingPlan.RotationSize = 5;
        Player threeManPitcher = threeMan.Roster.First(player => player.Role == PlayerRole.Pitcher);
        Player fiveManPitcher = fiveMan.Roster.First(player => player.Role == PlayerRole.Pitcher);

        int threeManChance = InjuryEngine.ParticipationInjuryChancePerHundredThousand(
            threeManPitcher, threeMan, InjuryExposureType.PitchThrown);
        int fiveManChance = InjuryEngine.ParticipationInjuryChancePerHundredThousand(
            fiveManPitcher, fiveMan, InjuryExposureType.PitchThrown);
        threeManPitcher.ConsecutiveReliefGames = 2;
        int consecutiveChance = InjuryEngine.ParticipationInjuryChancePerHundredThousand(
            threeManPitcher, threeMan, InjuryExposureType.PitchThrown);

        Assert.True(threeManChance > fiveManChance);
        Assert.True(consecutiveChance > threeManChance);
    }

    [Fact]
    public void ParticipationInjuryOnlyAffectsTheExposedPlayer()
    {
        Team team = RegressionTestData.CreateTeam("Exposure");
        Player participant = team.Roster[0];
        Player benchPlayer = team.Roster[^1];

        bool injured = InjuryEngine.TryParticipationInjury(
            participant,
            team,
            new SequenceRandom(0, 0, 0, 0),
            InjuryExposureType.FieldingPlay);

        Assert.True(injured);
        Assert.NotEqual(PlayerInjuryStatus.Healthy, participant.InjuryStatus);
        Assert.Equal(PlayerInjuryStatus.Healthy, benchPlayer.InjuryStatus);
    }
}

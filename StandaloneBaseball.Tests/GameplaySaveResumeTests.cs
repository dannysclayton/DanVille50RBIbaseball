using System.Runtime.ExceptionServices;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class GameplaySaveResumeTests
{
    [Fact]
    public void GameplayForm_ApplyThenSnapshotPreservesHistoryAndControlAssignments()
    {
        var away = CreateTeam("Away");
        var home = CreateTeam("Home");
        var source = CreateSavedState(away, home);
        GameplayState snapshot = null;
        Exception failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                using var form = new GameplayForm(away, home);
                form.StopGameLoop();
                form.ApplyGameplayState(source);
                snapshot = form.CreateGameplayStateSnapshot();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Gameplay save/resume test timed out.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        Assert.NotNull(snapshot);
        Assert.Equal(GameMode.PlayerVsPlayer, snapshot.Mode);
        Assert.Equal(home.Id, snapshot.UserControlledTeamId);
        Assert.Equal(home.Id, snapshot.KeyboardControlledTeamId);
        Assert.Equal(away.Id, snapshot.ControllerControlledTeamId);
        Assert.Equal(new[] { 2 }, snapshot.AwayRunsByInning);
        Assert.Equal(new[] { 1 }, snapshot.HomeRunsByInning);
        Assert.Equal(3, snapshot.AwayLeftOnBase);
        Assert.Equal(4, snapshot.HomeLeftOnBase);
        Assert.Equal(2, snapshot.CompletedHalfInnings.Count);
        Assert.Equal(2, snapshot.PlayByPlay.Count);
        Assert.Equal("Home run scored.", snapshot.PlayByPlay[1].Description);
        AssertLiveRules(source.LiveRules, snapshot.LiveRules);
    }

    [Fact]
    public void LeagueStore_RoundTripPreservesGameplayHistoryAndControlAssignments()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DansRBI-GameplaySaveTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "resume" + LeagueStore.Extension);
        var away = CreateTeam("Away");
        var home = CreateTeam("Home");
        var league = new LeagueFile
        {
            Teams = new List<Team> { away, home },
            InProgressGames = new List<InProgressGameSave>
            {
                new InProgressGameSave
                {
                    AwayTeamId = away.Id,
                    HomeTeamId = home.Id,
                    State = CreateSavedState(away, home)
                }
            }
        };

        try
        {
            LeagueStore.Save(path, league);
            var restored = LeagueStore.Load(path).InProgressGames.Single().State;

            Assert.Equal(GameMode.PlayerVsPlayer, restored.Mode);
            Assert.Equal(home.Id, restored.UserControlledTeamId);
            Assert.Equal(home.Id, restored.KeyboardControlledTeamId);
            Assert.Equal(away.Id, restored.ControllerControlledTeamId);
            Assert.Equal(new[] { 2 }, restored.AwayRunsByInning);
            Assert.Equal(new[] { 1 }, restored.HomeRunsByInning);
            Assert.Equal(3, restored.AwayLeftOnBase);
            Assert.Equal(4, restored.HomeLeftOnBase);
            Assert.Equal(2, restored.CompletedHalfInnings.Count);
            Assert.Equal(2, restored.PlayByPlay.Count);
            Assert.Equal("1B, 3B", restored.PlayByPlay[1].Bases);
            AssertLiveRules(league.InProgressGames[0].State.LiveRules, restored.LiveRules);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static GameplayState CreateSavedState(Team away, Team home)
    {
        var state = GameplayState.Create(away, home, GameMode.PlayerVsPlayer);
        state.UserControlledTeamId = home.Id;
        state.KeyboardControlledTeamId = home.Id;
        state.ControllerControlledTeamId = away.Id;
        state.Inning = 2;
        state.Half = HalfInning.Top;
        state.AwayScore = 2;
        state.HomeScore = 1;
        state.AwayRunsByInning.Add(2);
        state.HomeRunsByInning.Add(1);
        state.AwayLeftOnBase = 3;
        state.HomeLeftOnBase = 4;
        state.CompletedHalfInnings.Add(new HalfInningSnapshot
        {
            Inning = 1,
            Half = HalfInning.Top,
            BattingTeamId = away.Id,
            RunsScored = 2,
            AwayScore = 2,
            HomeScore = 0
        });
        state.CompletedHalfInnings.Add(new HalfInningSnapshot
        {
            Inning = 1,
            Half = HalfInning.Bottom,
            BattingTeamId = home.Id,
            RunsScored = 1,
            AwayScore = 2,
            HomeScore = 1
        });
        state.PlayByPlay.Add(new GamePlayByPlayEntry
        {
            Sequence = 1,
            Inning = 1,
            Half = HalfInning.Top,
            AwayScore = 1,
            HomeScore = 0,
            Bases = "Bases empty",
            Description = "Run scored."
        });
        state.PlayByPlay.Add(new GamePlayByPlayEntry
        {
            Sequence = 2,
            Inning = 1,
            Half = HalfInning.Top,
            AwayScore = 2,
            HomeScore = 0,
            Bases = "1B, 3B",
            Description = "Home run scored."
        });
        Guid awayPitcherId = away.Roster.First(player => player.Role == PlayerRole.Pitcher).Id;
        Guid homePitcherId = home.Roster.First(player => player.Role == PlayerRole.Pitcher).Id;
        state.LiveRules = new GameplayLiveRulesState
        {
            AwayStarterPitcherId = awayPitcherId,
            HomeStarterPitcherId = homePitcherId,
            AwayEmergencyPitcherId = away.Roster.First(player => player.Role == PlayerRole.Batter).Id,
            WinningPitcherCandidateId = awayPitcherId,
            LosingPitcherCandidateId = homePitcherId,
            AwayStarterPitchCount = 77,
            HomeStarterPitchCount = 64,
            AwayStarterPostLimitBaserunnersThisInning = 2,
            HomeStarterPostLimitBaserunnersThisInning = 3,
            AwayMoundVisitsThisInning = 1,
            HomeMoundVisitsThisInning = 2,
            AwayCoachVisitBoostActive = true,
            PitchersRemovedByRunRule = { homePitcherId }
        };
        state.LiveRules.ReliefPitcherFatigue[awayPitcherId] = new GameplayReliefPitcherState
        {
            OutsRecorded = 7,
            PostLimitBaserunnersThisInning = 3,
            FirstBatterBoostAvailable = false,
            FirstBatterFaced = true,
            AppearanceInitialized = true,
            EnteredInSaveSituation = true,
            EnteredWithThreeRunLead = true,
            EnteredWithTyingRunThreat = true,
            LeadPreserved = false
        };
        state.LiveRules.PitcherRunRules[homePitcherId] = new GameplayPitcherRunRuleState
        {
            RunsAllowedByInning = { [1] = 5, [2] = 1 },
            EarnedRunsAllowedByInning = { [1] = 4, [2] = 1 },
            FinalizedInnings = { 1, 2 },
            ConsecutiveScorelessInnings = 6,
            AdvancementBoostPercent = 20,
            EarnedRunReductionImmune = true
        };
        return state;
    }

    private static void AssertLiveRules(GameplayLiveRulesState expected, GameplayLiveRulesState actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.AwayStarterPitcherId, actual.AwayStarterPitcherId);
        Assert.Equal(expected.HomeStarterPitcherId, actual.HomeStarterPitcherId);
        Assert.Equal(expected.AwayEmergencyPitcherId, actual.AwayEmergencyPitcherId);
        Assert.Equal(expected.WinningPitcherCandidateId, actual.WinningPitcherCandidateId);
        Assert.Equal(expected.LosingPitcherCandidateId, actual.LosingPitcherCandidateId);
        Assert.Equal(expected.AwayStarterPitchCount, actual.AwayStarterPitchCount);
        Assert.Equal(expected.HomeStarterPitchCount, actual.HomeStarterPitchCount);
        Assert.Equal(expected.AwayMoundVisitsThisInning, actual.AwayMoundVisitsThisInning);
        Assert.Equal(expected.HomeMoundVisitsThisInning, actual.HomeMoundVisitsThisInning);
        Assert.Equal(expected.PitchersRemovedByRunRule, actual.PitchersRemovedByRunRule);
        Assert.Equal(expected.ReliefPitcherFatigue.Keys, actual.ReliefPitcherFatigue.Keys);
        var expectedRelief = expected.ReliefPitcherFatigue.Single().Value;
        var actualRelief = actual.ReliefPitcherFatigue.Single().Value;
        Assert.Equal(expectedRelief.OutsRecorded, actualRelief.OutsRecorded);
        Assert.Equal(expectedRelief.PostLimitBaserunnersThisInning, actualRelief.PostLimitBaserunnersThisInning);
        Assert.Equal(expectedRelief.EnteredInSaveSituation, actualRelief.EnteredInSaveSituation);
        Assert.Equal(expectedRelief.EnteredWithThreeRunLead, actualRelief.EnteredWithThreeRunLead);
        Assert.Equal(expectedRelief.EnteredWithTyingRunThreat, actualRelief.EnteredWithTyingRunThreat);
        Assert.Equal(expectedRelief.LeadPreserved, actualRelief.LeadPreserved);
        var expectedRunRule = expected.PitcherRunRules.Single().Value;
        var actualRunRule = actual.PitcherRunRules.Single().Value;
        Assert.Equal(expectedRunRule.RunsAllowedByInning, actualRunRule.RunsAllowedByInning);
        Assert.Equal(expectedRunRule.EarnedRunsAllowedByInning, actualRunRule.EarnedRunsAllowedByInning);
        Assert.Equal(expectedRunRule.FinalizedInnings, actualRunRule.FinalizedInnings);
        Assert.Equal(expectedRunRule.AdvancementBoostPercent, actualRunRule.AdvancementBoostPercent);
        Assert.Equal(expectedRunRule.EarnedRunReductionImmune, actualRunRule.EarnedRunReductionImmune);
    }

    private static Team CreateTeam(string name)
    {
        var team = new Team { City = name, Nickname = "Club" };
        team.Roster.Add(Pitcher(name + " Pitcher"));
        team.Roster.Add(Hitter(name + " Catcher", "C"));
        team.Roster.Add(Hitter(name + " First", "1B"));
        team.Roster.Add(Hitter(name + " Second", "2B"));
        team.Roster.Add(Hitter(name + " Third", "3B"));
        team.Roster.Add(Hitter(name + " Shortstop", "SS"));
        team.Roster.Add(Hitter(name + " Left", "LF"));
        team.Roster.Add(Hitter(name + " Center", "CF"));
        team.Roster.Add(Hitter(name + " Right", "RF"));
        return team;
    }

    private static Player Pitcher(string name)
        => new()
        {
            Name = name,
            Role = PlayerRole.Pitcher,
            Positions = "P",
            Pitching = 80,
            Stamina = 80,
            Accuracy = 80,
            Fielding = 70
        };

    private static Player Hitter(string name, string position)
        => new()
        {
            Name = name,
            Role = PlayerRole.Batter,
            Positions = position,
            Contact = 65,
            Power = 55,
            Speed = 60,
            BaseRunning = 60,
            Fielding = 70,
            ArmStrength = 60,
            Accuracy = 60
        };
}

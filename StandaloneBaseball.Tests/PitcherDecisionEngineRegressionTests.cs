using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class PitcherDecisionEngineRegressionTests
{
    [Fact]
    public void EligibleStarterGetsWinAndRelieversGetHoldAndSave()
    {
        Guid away = Guid.NewGuid();
        Guid home = Guid.NewGuid();
        var starter = Appearance(away, "Starter", starter: true, order: 0, outs: 15);
        var holder = Appearance(away, "Holder", starter: false, order: 1, outs: 3, saveSituation: true);
        var closer = Appearance(away, "Closer", starter: false, order: 2, outs: 3, saveSituation: true, finished: true);
        closer.EnteredWithThreeRunLead = true;
        var loser = Appearance(home, "Loser", starter: true, order: 0, outs: 21, finished: true);

        PitcherDecisionResult result = PitcherDecisionEngine.Apply(Request(
            away, home, 5, 2, 9, starter.PlayerId, loser.PlayerId, starter, holder, closer, loser));

        Assert.Equal(starter.PlayerId, result.WinningPitcherId);
        Assert.Equal(loser.PlayerId, result.LosingPitcherId);
        Assert.Equal(closer.PlayerId, result.SavePitcherId);
        Assert.Equal(1, holder.Line.Holds);
        Assert.Equal(1, closer.Line.Saves);
    }

    [Fact]
    public void FiveInningGameRequiresFourStarterInningsForWin()
    {
        Guid away = Guid.NewGuid();
        Guid home = Guid.NewGuid();
        var starter = Appearance(away, "Short Starter", starter: true, order: 0, outs: 11);
        var reliever = Appearance(away, "Winning Relief", starter: false, order: 1, outs: 4, finished: true);
        var loser = Appearance(home, "Loser", starter: true, order: 0, outs: 12, finished: true);

        PitcherDecisionResult result = PitcherDecisionEngine.Apply(Request(
            away, home, 4, 1, 5, starter.PlayerId, loser.PlayerId, starter, reliever, loser));

        Assert.Equal(reliever.PlayerId, result.WinningPitcherId);
        Assert.Equal(0, starter.Line.Wins);
        Assert.Equal(1, reliever.Line.Wins);
    }

    [Fact]
    public void TyingRunThreatAllowsSaveWithLessThanOneInning()
    {
        Guid away = Guid.NewGuid();
        Guid home = Guid.NewGuid();
        var winner = Appearance(away, "Winner", starter: true, order: 0, outs: 26);
        var closer = Appearance(away, "One Out Closer", starter: false, order: 1, outs: 1, saveSituation: true, finished: true);
        closer.EnteredWithTyingRunThreat = true;
        var loser = Appearance(home, "Loser", starter: true, order: 0, outs: 24, finished: true);

        PitcherDecisionResult result = PitcherDecisionEngine.Apply(Request(
            away, home, 6, 4, 9, winner.PlayerId, loser.PlayerId, winner, closer, loser));

        Assert.Equal(closer.PlayerId, result.SavePitcherId);
        Assert.Equal(1, closer.Line.Saves);
    }

    [Fact]
    public void ThreeEffectiveInningsQualifyForSaveWithLargeLead()
    {
        Guid away = Guid.NewGuid();
        Guid home = Guid.NewGuid();
        var winner = Appearance(away, "Winner", starter: true, order: 0, outs: 18);
        var finisher = Appearance(away, "Long Finisher", starter: false, order: 1, outs: 9, finished: true);
        var loser = Appearance(home, "Loser", starter: true, order: 0, outs: 24, finished: true);

        PitcherDecisionResult result = PitcherDecisionEngine.Apply(Request(
            away, home, 12, 2, 9, winner.PlayerId, loser.PlayerId, winner, finisher, loser));

        Assert.Equal(finisher.PlayerId, result.SavePitcherId);
    }

    [Fact]
    public void BriefIneffectiveCandidateIsBypassedByScorerDiscretion()
    {
        Guid away = Guid.NewGuid();
        Guid home = Guid.NewGuid();
        var starter = Appearance(away, "Ineligible Starter", starter: true, order: 0, outs: 12);
        var ineffective = Appearance(away, "Ineffective", starter: false, order: 1, outs: 3);
        ineffective.Line.ER = 2;
        ineffective.Line.RunsAllowed = 2;
        var effective = Appearance(away, "Effective", starter: false, order: 2, outs: 5, finished: true);
        effective.Line.K = 3;
        var loser = Appearance(home, "Loser", starter: true, order: 0, outs: 24, finished: true);

        PitcherDecisionResult result = PitcherDecisionEngine.Apply(Request(
            away, home, 7, 5, 9, ineffective.PlayerId, loser.PlayerId, starter, ineffective, effective, loser));

        Assert.Equal(effective.PlayerId, result.WinningPitcherId);
    }

    [Fact]
    public void PitcherCanReceiveBlownSaveAndLaterWin()
    {
        Guid away = Guid.NewGuid();
        Guid home = Guid.NewGuid();
        var starter = Appearance(away, "Starter", starter: true, order: 0, outs: 18);
        var reliever = Appearance(away, "Comeback Winner", starter: false, order: 1, outs: 3, saveSituation: true, finished: true, leadPreserved: false);
        reliever.EnteredWithThreeRunLead = true;
        var loser = Appearance(home, "Loser", starter: false, order: 1, outs: 3, finished: true);

        PitcherDecisionResult result = PitcherDecisionEngine.Apply(Request(
            away, home, 8, 7, 9, reliever.PlayerId, loser.PlayerId, starter, reliever, loser));

        Assert.Equal(reliever.PlayerId, result.WinningPitcherId);
        Assert.Equal(1, reliever.Line.Wins);
        Assert.Equal(1, reliever.Line.BlownSaves);
        Assert.Contains(reliever.PlayerId, result.BlownSavePitcherIds);
        Assert.Null(result.SavePitcherId);
    }

    [Fact]
    public void SoleStarterGetsCompleteGameAndShutout()
    {
        Guid away = Guid.NewGuid();
        Guid home = Guid.NewGuid();
        var winner = Appearance(away, "Complete Winner", starter: true, order: 0, outs: 27, finished: true);
        var loser = Appearance(home, "Loser", starter: true, order: 0, outs: 24, finished: true);

        PitcherDecisionEngine.Apply(Request(
            away, home, 3, 0, 9, winner.PlayerId, loser.PlayerId, winner, loser));

        Assert.Equal(1, winner.Line.CompleteGames);
        Assert.Equal(1, winner.Line.Shutouts);
    }

    [Fact]
    public void ResponsiblePitcherGetsLossWithoutRecordingAnOut()
    {
        Guid away = Guid.NewGuid();
        Guid home = Guid.NewGuid();
        var winner = Appearance(away, "Winner", starter: true, order: 0, outs: 27, finished: true);
        var responsible = Appearance(home, "Responsible", starter: false, order: 1, outs: 0);
        var finisher = Appearance(home, "Finisher", starter: false, order: 2, outs: 3, finished: true);

        PitcherDecisionResult result = PitcherDecisionEngine.Apply(Request(
            away, home, 4, 3, 9, winner.PlayerId, responsible.PlayerId, winner, responsible, finisher));

        Assert.Equal(responsible.PlayerId, result.LosingPitcherId);
        Assert.Equal(1, responsible.Line.Losses);
        Assert.Equal(0, finisher.Line.Losses);
    }

    private static PitcherDecisionRequest Request(
        Guid away,
        Guid home,
        int awayScore,
        int homeScore,
        int innings,
        Guid winningCandidate,
        Guid losingCandidate,
        params PitcherDecisionAppearance[] appearances)
        => new()
        {
            AwayTeamId = away,
            HomeTeamId = home,
            AwayScore = awayScore,
            HomeScore = homeScore,
            RegulationInnings = innings,
            WinningPitcherCandidateId = winningCandidate,
            LosingPitcherCandidateId = losingCandidate,
            Appearances = appearances.ToList()
        };

    private static PitcherDecisionAppearance Appearance(
        Guid teamId,
        string name,
        bool starter,
        int order,
        int outs,
        bool saveSituation = false,
        bool finished = false,
        bool leadPreserved = true)
    {
        Guid playerId = Guid.NewGuid();
        return new PitcherDecisionAppearance
        {
            TeamId = teamId,
            PlayerId = playerId,
            PlayerName = name,
            Starter = starter,
            AppearanceOrder = order,
            FinishedGame = finished,
            EnteredInSaveSituation = saveSituation,
            LeadPreserved = leadPreserved,
            Line = new PlayerGameLine
            {
                TeamId = teamId,
                PlayerId = playerId,
                PlayerName = name,
                Pitcher = true,
                StartingPitcher = starter,
                IPOuts = outs
            }
        };
    }
}

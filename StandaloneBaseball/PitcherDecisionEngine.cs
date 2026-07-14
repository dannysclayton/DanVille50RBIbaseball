#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class PitcherDecisionAppearance
    {
        public Guid TeamId { get; set; }
        public Guid PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public bool Starter { get; set; }
        public bool FinishedGame { get; set; }
        public int AppearanceOrder { get; set; }
        public bool EnteredInSaveSituation { get; set; }
        public bool EnteredWithThreeRunLead { get; set; }
        public bool EnteredWithTyingRunThreat { get; set; }
        public bool LeadPreserved { get; set; } = true;
        public PlayerGameLine Line { get; set; } = new PlayerGameLine();
    }

    public sealed class PitcherDecisionRequest
    {
        public Guid AwayTeamId { get; set; }
        public Guid HomeTeamId { get; set; }
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public int RegulationInnings { get; set; } = 9;
        public Guid? WinningPitcherCandidateId { get; set; }
        public Guid? LosingPitcherCandidateId { get; set; }
        public List<PitcherDecisionAppearance> Appearances { get; set; } = new List<PitcherDecisionAppearance>();
    }

    public sealed class PitcherDecisionResult
    {
        public Guid? WinningPitcherId { get; set; }
        public string WinningPitcherName { get; set; } = "";
        public Guid? LosingPitcherId { get; set; }
        public string LosingPitcherName { get; set; } = "";
        public Guid? SavePitcherId { get; set; }
        public string SavePitcherName { get; set; } = "";
        public List<Guid> HoldPitcherIds { get; set; } = new List<Guid>();
        public List<Guid> BlownSavePitcherIds { get; set; } = new List<Guid>();
    }

    public static class PitcherDecisionEngine
    {
        public static PitcherDecisionResult Apply(PitcherDecisionRequest? request)
        {
            var result = new PitcherDecisionResult();
            if (request == null)
                return result;

            var appearances = (request.Appearances ?? new List<PitcherDecisionAppearance>())
                .Where(appearance => appearance != null && appearance.PlayerId != Guid.Empty && appearance.Line != null)
                .OrderBy(appearance => appearance.AppearanceOrder)
                .ToList();
            ResetDecisions(appearances);
            AssignCompleteGames(appearances, request);
            AssignBlownSaves(appearances, result);

            if (request.AwayScore == request.HomeScore)
                return result;

            Guid winnerTeamId = request.AwayScore > request.HomeScore ? request.AwayTeamId : request.HomeTeamId;
            Guid loserTeamId = winnerTeamId == request.AwayTeamId ? request.HomeTeamId : request.AwayTeamId;
            List<PitcherDecisionAppearance> winnerStaff = appearances.Where(a => a.TeamId == winnerTeamId && a.Line.IPOuts > 0).ToList();
            List<PitcherDecisionAppearance> loserAppearances = appearances.Where(a => a.TeamId == loserTeamId).ToList();
            List<PitcherDecisionAppearance> loserStaff = loserAppearances.Where(a => a.Line.IPOuts > 0).ToList();

            PitcherDecisionAppearance? winningPitcher = SelectWinningPitcher(
                winnerStaff,
                request.WinningPitcherCandidateId,
                Math.Clamp(request.RegulationInnings, 5, 9));
            PitcherDecisionAppearance? losingPitcher = FindCandidate(loserAppearances, request.LosingPitcherCandidateId)
                ?? loserStaff.LastOrDefault(appearance => appearance.FinishedGame)
                ?? loserStaff.LastOrDefault();

            if (winningPitcher != null)
            {
                winningPitcher.Line.Wins = 1;
                result.WinningPitcherId = winningPitcher.PlayerId;
                result.WinningPitcherName = winningPitcher.PlayerName;
            }
            if (losingPitcher != null)
            {
                losingPitcher.Line.Losses = 1;
                result.LosingPitcherId = losingPitcher.PlayerId;
                result.LosingPitcherName = losingPitcher.PlayerName;
            }

            PitcherDecisionAppearance? finisher = winnerStaff.LastOrDefault(appearance => appearance.FinishedGame)
                ?? winnerStaff.LastOrDefault();
            if (QualifiesForSave(finisher, winningPitcher))
            {
                finisher!.Line.Saves = 1;
                result.SavePitcherId = finisher.PlayerId;
                result.SavePitcherName = finisher.PlayerName;
            }

            foreach (PitcherDecisionAppearance appearance in winnerStaff.Where(appearance =>
                !appearance.Starter &&
                !appearance.FinishedGame &&
                appearance.EnteredInSaveSituation &&
                appearance.LeadPreserved &&
                appearance.Line.IPOuts > 0 &&
                appearance.Line.Wins == 0 &&
                appearance.Line.Losses == 0 &&
                appearance.Line.Saves == 0))
            {
                appearance.Line.Holds = 1;
                result.HoldPitcherIds.Add(appearance.PlayerId);
            }

            return result;
        }

        public static int RequiredStarterOutsForWin(int regulationInnings)
            => regulationInnings <= 5 ? 12 : 15;

        private static PitcherDecisionAppearance? SelectWinningPitcher(
            List<PitcherDecisionAppearance> winnerStaff,
            Guid? candidateId,
            int regulationInnings)
        {
            PitcherDecisionAppearance? candidate = FindCandidate(winnerStaff, candidateId);
            if (candidate != null)
            {
                if (candidate.Starter && candidate.Line.IPOuts >= RequiredStarterOutsForWin(regulationInnings))
                    return candidate;
                if (!candidate.Starter && !IsBriefAndIneffective(candidate))
                    return candidate;
            }

            int minimumOrder = candidate?.AppearanceOrder ?? int.MinValue;
            var eligibleRelievers = winnerStaff
                .Where(appearance => !appearance.Starter && appearance.Line.IPOuts > 0)
                .Where(appearance => appearance.AppearanceOrder >= minimumOrder)
                .ToList();
            if (candidate != null && IsBriefAndIneffective(candidate))
            {
                var alternatives = eligibleRelievers.Where(appearance => appearance.PlayerId != candidate.PlayerId).ToList();
                if (alternatives.Count > 0)
                    eligibleRelievers = alternatives;
            }

            PitcherDecisionAppearance? scorerSelection = eligibleRelievers
                .OrderByDescending(EffectivenessScore)
                .ThenByDescending(appearance => appearance.Line.IPOuts)
                .ThenBy(appearance => appearance.AppearanceOrder)
                .FirstOrDefault();
            return scorerSelection ?? candidate ?? winnerStaff.LastOrDefault();
        }

        private static bool IsBriefAndIneffective(PitcherDecisionAppearance appearance)
            => appearance.Line.IPOuts <= 3 && appearance.Line.ER >= 2 ||
               appearance.Line.IPOuts < 6 && appearance.Line.RunsAllowed >= 3;

        private static int EffectivenessScore(PitcherDecisionAppearance appearance)
            => appearance.Line.IPOuts * 4 +
               appearance.Line.K * 2 -
               appearance.Line.ER * 8 -
               appearance.Line.HitsAllowed * 2 -
               appearance.Line.WalksAllowed * 2 -
               appearance.Line.HomeRunsAllowed * 2;

        private static bool QualifiesForSave(
            PitcherDecisionAppearance? finisher,
            PitcherDecisionAppearance? winningPitcher)
        {
            if (finisher == null || finisher.Starter || !finisher.LeadPreserved || finisher.Line.IPOuts <= 0 ||
                finisher.PlayerId == winningPitcher?.PlayerId)
            {
                return false;
            }

            bool threeRunPath = finisher.EnteredWithThreeRunLead && finisher.Line.IPOuts >= 3;
            bool tyingRunPath = finisher.EnteredWithTyingRunThreat;
            bool threeInningPath = finisher.Line.IPOuts >= 9;
            return threeRunPath || tyingRunPath || threeInningPath;
        }

        private static void AssignBlownSaves(
            IEnumerable<PitcherDecisionAppearance> appearances,
            PitcherDecisionResult result)
        {
            foreach (PitcherDecisionAppearance appearance in appearances.Where(appearance =>
                !appearance.Starter && appearance.EnteredInSaveSituation && !appearance.LeadPreserved))
            {
                appearance.Line.BlownSaves = 1;
                result.BlownSavePitcherIds.Add(appearance.PlayerId);
            }
        }

        private static void AssignCompleteGames(
            List<PitcherDecisionAppearance> appearances,
            PitcherDecisionRequest request)
        {
            AssignCompleteGameForTeam(appearances, request.AwayTeamId, request.HomeScore);
            AssignCompleteGameForTeam(appearances, request.HomeTeamId, request.AwayScore);
        }

        private static void AssignCompleteGameForTeam(
            IEnumerable<PitcherDecisionAppearance> appearances,
            Guid teamId,
            int opponentScore)
        {
            var staff = appearances.Where(appearance => appearance.TeamId == teamId && appearance.Line.IPOuts > 0).ToList();
            if (staff.Count != 1 || !staff[0].Starter || !staff[0].FinishedGame)
                return;
            staff[0].Line.CompleteGames = 1;
            if (opponentScore == 0)
                staff[0].Line.Shutouts = 1;
        }

        private static PitcherDecisionAppearance? FindCandidate(
            IEnumerable<PitcherDecisionAppearance> appearances,
            Guid? candidateId)
            => !candidateId.HasValue || candidateId.Value == Guid.Empty
                ? null
                : appearances.LastOrDefault(appearance => appearance.PlayerId == candidateId.Value);

        private static void ResetDecisions(IEnumerable<PitcherDecisionAppearance> appearances)
        {
            foreach (PitcherDecisionAppearance appearance in appearances)
            {
                appearance.Line.Wins = 0;
                appearance.Line.Losses = 0;
                appearance.Line.Saves = 0;
                appearance.Line.Holds = 0;
                appearance.Line.BlownSaves = 0;
                appearance.Line.CompleteGames = 0;
                appearance.Line.Shutouts = 0;
            }
        }
    }
}

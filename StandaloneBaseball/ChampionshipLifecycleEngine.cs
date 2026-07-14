#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace StandaloneBaseball
{
    public static class ChampionshipLifecycleEngine
    {
        public static bool TryRecordChampion(LeagueFile? league, Season? season, PlayoffSeries? series, [NotNullWhen(true)] out Team? champion)
        {
            champion = null;
            if (league == null || season == null || series == null ||
                !series.WinnerTeamId.HasValue || !IsFinalChampionshipSeries(series))
                return false;

            champion = (league.Teams ?? new List<Team>())
                .FirstOrDefault(team => team != null && team.Id == series.WinnerTeamId.Value);
            if (champion == null)
                return false;

            champion.NormalizeText();
            if (!series.WinnerCoachId.HasValue || series.WinnerCoachId == Guid.Empty)
            {
                if (series.WinnerTeamId == series.TeamAId)
                    series.WinnerCoachId = series.TeamACoachId == Guid.Empty ? champion.CoachId : series.TeamACoachId;
                else if (series.WinnerTeamId == series.TeamBId)
                    series.WinnerCoachId = series.TeamBCoachId == Guid.Empty ? champion.CoachId : series.TeamBCoachId;
                else
                    series.WinnerCoachId = champion.CoachId;
            }

            bool changed = season.ChampionTeamId != champion.Id;
            season.ChampionTeamId = champion.Id;
            if (changed)
                ResetInjuriesAfterWorldSeries(league);
            return changed;
        }

        public static bool IsFinalChampionshipSeries(PlayoffSeries series)
        {
            return series != null &&
                (string.Equals(series.RoundName, "Final Championship", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(series.RoundName, "World Series", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(series.BracketGroup, "League Championship", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(series.BracketGroup, "World Series", StringComparison.OrdinalIgnoreCase));
        }

        private static void ResetInjuriesAfterWorldSeries(LeagueFile league)
        {
            foreach (var player in (league.Teams ?? new List<Team>())
                         .Where(team => team != null)
                         .SelectMany(team => (team.Roster ?? Enumerable.Empty<Player>())
                             .Concat(team.InjuredReserve ?? Enumerable.Empty<Player>()))
                         .Where(player => player != null)
                         .GroupBy(player => player.Id)
                         .Select(group => group.First()))
            {
                player.InjuryStatus = PlayerInjuryStatus.Healthy;
                player.InjuryName = "";
                player.InjuryGamesRemaining = 0;
                player.InjurySeverity = 0;
            }
        }
    }
}

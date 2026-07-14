using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    internal static class DoubleheaderInterstitialRules
    {
        public static bool IsSecondGameOfDoubleheader(Season? season, ScheduledGame? scheduled)
        {
            if (season == null || scheduled == null || scheduled.DayGameNumber != 2)
                return false;

            return (season.Schedule ?? new List<ScheduledGame>()).Any(candidate =>
                candidate != null &&
                candidate.Id != scheduled.Id &&
                candidate.Week == scheduled.Week &&
                candidate.DayGameNumber == 1 &&
                string.Equals(candidate.DayLabel, scheduled.DayLabel, StringComparison.OrdinalIgnoreCase) &&
                SameMatchup(candidate, scheduled));
        }

        private static bool SameMatchup(ScheduledGame first, ScheduledGame second)
        {
            return (first.AwayTeamId == second.AwayTeamId && first.HomeTeamId == second.HomeTeamId) ||
                (first.AwayTeamId == second.HomeTeamId && first.HomeTeamId == second.AwayTeamId);
        }
    }
}

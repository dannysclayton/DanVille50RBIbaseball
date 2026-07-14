using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    internal static class SeasonOpeningInterstitialRules
    {
        public static bool IsFirstScheduledGame(Season? season, ScheduledGame? scheduled)
        {
            if (season == null || scheduled == null)
                return false;

            ScheduledGame? first = (season.Schedule ?? new List<ScheduledGame>())
                .Where(game => game != null)
                .OrderBy(game => game.GameNumber <= 0 ? int.MaxValue : game.GameNumber)
                .ThenBy(game => game.Week)
                .ThenBy(game => game.WeekGameNumber)
                .FirstOrDefault();
            return first?.Id == scheduled.Id;
        }
    }
}

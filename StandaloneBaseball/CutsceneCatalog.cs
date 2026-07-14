using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class CutsceneCatalog
    {
        private static readonly HashSet<CutsceneTrigger> TeamOnly = new HashSet<CutsceneTrigger>
        {
            CutsceneTrigger.HomeRun,
            CutsceneTrigger.GrandSlam,
            CutsceneTrigger.RunScored,
            CutsceneTrigger.Strikeout,
            CutsceneTrigger.PitcherChange,
            CutsceneTrigger.FinalOut
        };

        public static IReadOnlyList<CutsceneTrigger> LeagueTriggers { get; } = Enum.GetValues(typeof(CutsceneTrigger))
            .Cast<CutsceneTrigger>()
            .Where(trigger => !TeamOnly.Contains(trigger))
            .ToList();

        public static IReadOnlyList<CutsceneTrigger> TeamTriggers { get; } = Enum.GetValues(typeof(CutsceneTrigger))
            .Cast<CutsceneTrigger>()
            .ToList();

        public static bool IsTeamOnly(CutsceneTrigger trigger)
            => TeamOnly.Contains(trigger);
    }
}

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class GameUniformResolver
    {
        public static TeamUniformSet? ResolveUniform(
            Team? team,
            bool homeRole,
            Guid? requestedUniformId,
            ScheduledGame? scheduled = null,
            int gameNumber = 0,
            IEnumerable<ScheduledGame>? schedule = null,
            bool rotateSavedUniforms = true,
            TeamUniformCategory? autoCategory = null)
        {
            if (team == null)
                return null;

            team.EnsureDefaultUniformSets();
            var requested = team.UniformById(requestedUniformId);
            if (requested != null && IsRoleCategory(requested.Category, homeRole))
                return requested;

            var category = ValidAutoCategory(homeRole, autoCategory) ?? DefaultCategory(homeRole);
            var choices = UniformChoicesForCategory(team, category).ToList();
            if (choices.Count == 0)
                return team.DefaultUniform();

            if (!rotateSavedUniforms)
                return choices[0];

            int seed = RotationSeed(team, homeRole, category, scheduled, gameNumber, schedule);
            int index = seed > 0 ? (seed - 1) % choices.Count : 0;
            return choices[index];
        }

        private static int RotationSeed(Team? team, bool homeRole, TeamUniformCategory category, ScheduledGame? scheduled, int gameNumber, IEnumerable<ScheduledGame>? schedule)
        {
            if (team != null && scheduled != null && schedule != null)
            {
                var ordered = schedule
                    .Where(g => g != null)
                    .Where(g => homeRole ? g.HomeTeamId == team.Id : g.AwayTeamId == team.Id)
                    .Where(g => ScheduledUniformCategory(team, homeRole, g) == category)
                    .OrderBy(g => g.GameNumber <= 0 ? int.MaxValue : g.GameNumber)
                    .ThenBy(g => g.Week)
                    .ThenBy(g => g.WeekGameNumber)
                    .ThenBy(g => g.Id)
                    .ToList();
                int index = ordered.FindIndex(g => g.Id == scheduled.Id);
                if (index >= 0)
                    return index + 1;

                int scheduledGameNumber = scheduled.GameNumber > 0 ? scheduled.GameNumber : gameNumber;
                if (scheduledGameNumber > 0)
                    return ordered.Count(g => g.GameNumber > 0 && g.GameNumber <= scheduledGameNumber);
            }

            int seed = scheduled?.GameNumber > 0 ? scheduled.GameNumber : gameNumber;
            return seed <= 0 ? 1 : seed;
        }

        public static IEnumerable<TeamUniformSet> UniformChoicesForRole(Team team, bool homeRole)
        {
            if (team == null)
                yield break;

            team.EnsureDefaultUniformSets();
            var categories = homeRole
                ? new[] { TeamUniformCategory.Home, TeamUniformCategory.HomeAlternate }
                : new[] { TeamUniformCategory.Visitor, TeamUniformCategory.VisitorAlternate };

            foreach (var category in categories)
            {
                foreach (var uniform in (team.UniformSets ?? new List<TeamUniformSet>())
                    .Where(u => u != null && u.Category == category))
                {
                    yield return uniform;
                }
            }
        }

        public static IEnumerable<TeamUniformSet> UniformChoicesForCategory(Team team, TeamUniformCategory category)
        {
            if (team == null)
                yield break;

            team.EnsureDefaultUniformSets();
            foreach (var uniform in (team.UniformSets ?? new List<TeamUniformSet>())
                .Where(u => u != null && u.Category == category))
            {
                yield return uniform;
            }
        }

        public static TeamUniformCategory DefaultCategory(bool homeRole)
            => homeRole ? TeamUniformCategory.Home : TeamUniformCategory.Visitor;

        public static TeamUniformCategory? ValidAutoCategory(bool homeRole, TeamUniformCategory? category)
            => category.HasValue && IsRoleCategory(category.Value, homeRole) ? category.Value : null;

        public static TeamUniformCategory ScheduledUniformCategory(Team? team, bool homeRole, ScheduledGame? scheduled)
        {
            if (team != null && scheduled != null)
            {
                Guid? explicitId = homeRole ? scheduled.HomeUniformSetId : scheduled.AwayUniformSetId;
                var explicitUniform = team.UniformById(explicitId);
                if (explicitUniform != null && IsRoleCategory(explicitUniform.Category, homeRole))
                    return explicitUniform.Category;

                TeamUniformCategory? auto = homeRole ? scheduled.HomeUniformAutoCategory : scheduled.AwayUniformAutoCategory;
                var valid = ValidAutoCategory(homeRole, auto);
                if (valid.HasValue)
                    return valid.Value;
            }

            return DefaultCategory(homeRole);
        }

        public static bool IsRoleCategory(TeamUniformCategory category, bool homeRole)
        {
            return homeRole
                ? category == TeamUniformCategory.Home || category == TeamUniformCategory.HomeAlternate
                : category == TeamUniformCategory.Visitor || category == TeamUniformCategory.VisitorAlternate;
        }
    }
}

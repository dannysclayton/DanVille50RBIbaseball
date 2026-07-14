#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class TeamStanding
    {
        public Guid TeamId { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Ties { get; set; }
        public int RunsFor { get; set; }
        public int RunsAgainst { get; set; }
        public int RunDiff => RunsFor - RunsAgainst;
        public double Pct => Wins + Losses + Ties == 0 ? 0 : (Wins + Ties * 0.5) / (Wins + Losses + Ties);
    }

    public static class PlayoffEngine
    {
        private const int MinimumPlayoffRounds = 6;

        public static void EnsureDefaultStructure(LeagueFile league)
        {
            league.Structure ??= LeagueStructure.CreateDefault();
            league.Structure.Conferences ??= new List<Conference>();
            if (league.Structure.Conferences.Count == 0)
                league.Structure.Conferences.Add(CreateConference(1));

            foreach (var conf in league.Structure.Conferences)
            {
                conf.Regions ??= new List<Region>();
                while (conf.Regions.Count < 2)
                    conf.Regions.Add(new Region { Name = "Region " + (conf.Regions.Count + 1) });

                foreach (var region in conf.Regions)
                {
                    region.Districts ??= new List<District>();
                    while (region.Districts.Count < 2)
                        region.Districts.Add(new District { Name = "District " + (region.Districts.Count + 1) });
                    if (region.Districts.Count % 2 != 0)
                        region.Districts.Add(new District { Name = "District " + (region.Districts.Count + 1) });
                }
            }

            AssignTeamsEvenly(league);
        }

        public static string ValidateStructure(LeagueFile league)
        {
            if (league.Structure == null || league.Structure.Conferences == null || league.Structure.Conferences.Count < 1)
                return "The league must have at least 1 conference.";

            foreach (var conf in league.Structure.Conferences)
            {
                if (conf.Regions == null || conf.Regions.Count < 2)
                    return conf.Name + " must have at least 2 regions.";

                int districtCount = 0;
                foreach (var region in conf.Regions)
                {
                    if (region.Districts == null || region.Districts.Count == 0)
                        return conf.Name + " / " + region.Name + " must have districts.";
                    if (region.Districts.Count % 2 != 0)
                        return conf.Name + " / " + region.Name + " must have an even number of districts.";
                    districtCount += region.Districts.Count;
                }
                if (districtCount % 2 != 0)
                    return conf.Name + " must have an even number of total districts.";
            }

            return LeagueHierarchyEngine.Validate(league);
        }

        public static Conference CreateConference(int number)
        {
            var conf = new Conference { Name = "Conference " + number };
            for (int r = 1; r <= 2; r++)
            {
                var region = new Region { Name = "Region " + r };
                for (int d = 1; d <= 2; d++)
                    region.Districts.Add(new District { Name = "District " + d });
                conf.Regions.Add(region);
            }
            return conf;
        }

        public static List<TeamStanding> ComputeStandings(Season season, IEnumerable<Guid> teamIds)
        {
            var map = teamIds.Distinct().ToDictionary(id => id, id => new TeamStanding { TeamId = id });
            TeamStanding Get(Guid id)
            {
                if (!map.TryGetValue(id, out var st))
                {
                    st = new TeamStanding { TeamId = id };
                    map[id] = st;
                }
                return st;
            }

            foreach (var game in season.Games)
            {
                if (game.IsPlayoff)
                    continue;

                var away = Get(game.AwayTeamId);
                var home = Get(game.HomeTeamId);
                away.RunsFor += game.AwayScore;
                away.RunsAgainst += game.HomeScore;
                home.RunsFor += game.HomeScore;
                home.RunsAgainst += game.AwayScore;
                if (game.AwayScore > game.HomeScore) { away.Wins++; home.Losses++; }
                else if (game.HomeScore > game.AwayScore) { home.Wins++; away.Losses++; }
                else { away.Ties++; home.Ties++; }
            }

            return map.Values
                .OrderByDescending(s => s.Pct)
                .ThenByDescending(s => s.RunDiff)
                .ThenByDescending(s => s.RunsFor)
                .ToList();
        }

        public static List<PlayoffSeries> GeneratePlayoffs(LeagueFile league, Season season, out string error)
        {
            EnsureDefaultStructure(league);
            error = ValidateStructure(league);
            if (error != null) return new List<PlayoffSeries>();

            var result = new List<PlayoffSeries>();
            var allStandings = ComputeStandings(season, league.Teams.Select(t => t.Id));
            var qualified = new HashSet<Guid>();
            int round = 1;

            foreach (var conf in league.Structure.Conferences)
            {
                var districts = conf.Regions.SelectMany(r => r.Districts).ToList();
                var regionByDistrict = conf.Regions
                    .SelectMany(region => region.Districts.Select(district => new { DistrictId = district.Id, Region = region }))
                    .ToDictionary(item => item.DistrictId, item => item.Region);
                for (int i = 0; i < districts.Count; i += 2)
                {
                    var a = districts[i];
                    var b = districts[i + 1];
                    Region region = regionByDistrict[a.Id];
                    var aq = Qualify(a, allStandings);
                    var bq = Qualify(b, allStandings);
                    if (aq == null || bq == null)
                    {
                        error = "Each district needs at least 3 teams with standings data to seed champion, runner-up, and wildcard.";
                        return new List<PlayoffSeries>();
                    }

                    qualified.Add(aq.Champion);
                    qualified.Add(aq.RunnerUp);
                    qualified.Add(aq.Wildcard);
                    qualified.Add(bq.Champion);
                    qualified.Add(bq.RunnerUp);
                    qualified.Add(bq.Wildcard);

                    string group = conf.Name + ": " + a.Name + " / " + b.Name;
                    var firstBiDistrict = MakeSeries(round, "Bi-District", 3, conf.Id, region.Id, group, aq.RunnerUp, bq.Wildcard,
                        a.Name + " runner-up vs " + b.Name + " wildcard", new[] { a.Id, b.Id });
                    var secondBiDistrict = MakeSeries(round, "Bi-District", 3, conf.Id, region.Id, group, bq.RunnerUp, aq.Wildcard,
                        b.Name + " runner-up vs " + a.Name + " wildcard", new[] { a.Id, b.Id });
                    result.Add(firstBiDistrict);
                    result.Add(secondBiDistrict);

                    result.Add(MakeSeries(2, "Area", 5, conf.Id, region.Id, group, aq.Champion, Guid.Empty,
                        a.Name + " champion gets first-round bye; faces the opposite-district runner-up/wildcard winner",
                        new[] { a.Id }, new[] { secondBiDistrict.Id }));
                    result.Add(MakeSeries(2, "Area", 5, conf.Id, region.Id, group, bq.Champion, Guid.Empty,
                        b.Name + " champion gets first-round bye; faces the opposite-district runner-up/wildcard winner",
                        new[] { b.Id }, new[] { firstBiDistrict.Id }));
                }
            }

            int conferencePlaceholderRound = 3;
            foreach (var conf in league.Structure.Conferences)
            {
                foreach (var region in conf.Regions)
                {
                    var feeders = result
                        .Where(s => s.Round == 2 && s.ConferenceId == conf.Id && s.RegionId == region.Id)
                        .ToList();
                    for (int feeder = 0; feeder < feeders.Count; feeder += 2)
                    {
                        var pairedFeeders = feeders.Skip(feeder).Take(2).ToList();
                        result.Add(new PlayoffSeries
                        {
                            Round = conferencePlaceholderRound,
                            RoundName = "Regional Quarter Finals",
                            BestOf = 7,
                            ConferenceId = conf.Id,
                            RegionId = region.Id,
                            DistrictIds = pairedFeeders.SelectMany(series => series.DistrictIds).Distinct().ToList(),
                            FeederSeriesIds = pairedFeeders.Select(series => series.Id).ToList(),
                            BracketGroup = conf.Name + ": " + region.Name,
                            Notes = "Area winners advance through their region before meeting another region in the conference."
                        });
                    }
                }
            }

            int remaining = league.Structure.Conferences.Count;
            int laterRound = 5;
            while (remaining > 1)
            {
                int seriesCount = Math.Max(1, remaining / 2);
                for (int i = 0; i < seriesCount; i++)
                {
                    result.Add(new PlayoffSeries
                    {
                        Round = laterRound,
                        RoundName = remaining <= 2 ? "World Series" : "League Playoffs",
                        BestOf = 7,
                        BracketGroup = remaining <= 2 ? "World Series" : "League Round " + (laterRound - 3),
                        Notes = "Conference champions advance into this best-of-7 round."
                    });
                }
                remaining = (remaining + 1) / 2;
                laterRound++;
            }

            int naturalMaxRound = result.Count == 0 ? 0 : result.Max(s => s.Round);
            int finalRound = Math.Max(MinimumPlayoffRounds, Math.Min(9, naturalMaxRound));
            AddAtLargeWildcardSeries(result, league, season, allStandings, qualified, finalRound - naturalMaxRound);
            EnsureRoundPlaceholders(result, finalRound);
            ApplyRoundNames(result, finalRound);
            AssignHomeAdvantage(league, season, result);

            return result.OrderBy(s => s.Round).ThenBy(s => s.BracketGroup).ToList();
        }

        public static bool AdvanceBracket(LeagueFile league, Season season)
        {
            if (league == null || season?.Playoffs == null || season.Playoffs.Count == 0)
                return false;

            bool changed = false;
            int finalRound = season.Playoffs.Max(s => s.Round);
            for (int round = 1; round < finalRound; round++)
            {
                var current = season.Playoffs
                    .Where(s => s.Round == round && (s.TeamAId != Guid.Empty || s.TeamBId != Guid.Empty))
                    .ToList();
                if (current.Count == 0 || current.Any(s => !s.WinnerTeamId.HasValue))
                    continue;

                int targetRound = round + 1;
                var targets = season.Playoffs.Where(s => s.Round == targetRound).ToList();
                var alreadyPlaced = targets
                    .SelectMany(s => new[] { s.TeamAId, s.TeamBId })
                    .Where(id => id != Guid.Empty)
                    .ToHashSet();
                var advancing = BuildAdvancingTeams(league, current, alreadyPlaced);
                if (advancing.Count == 0)
                    continue;

                var allAdvancing = BuildAdvancingTeams(league, current, new HashSet<Guid>());
                bool preserveRegions = allAdvancing
                    .Where(team => team.RegionId.HasValue)
                    .GroupBy(team => team.RegionId.GetValueOrDefault())
                    .Any(group => group.Count() > 1);
                bool preserveConferences = !preserveRegions && allAdvancing
                    .Where(team => team.ConferenceId.HasValue)
                    .GroupBy(team => team.ConferenceId.GetValueOrDefault())
                    .Any(group => group.Count() > 1);

                if (preserveRegions || preserveConferences)
                {
                    int removed = season.Playoffs.RemoveAll(s =>
                        s.Round == targetRound &&
                        (preserveRegions ? !s.RegionId.HasValue : !s.ConferenceId.HasValue) &&
                        s.TeamAId == Guid.Empty &&
                        s.TeamBId == Guid.Empty &&
                        !s.WinnerTeamId.HasValue);
                    changed |= removed > 0;
                    targets = season.Playoffs.Where(s => s.Round == targetRound).ToList();
                }

                foreach (var target in targets.Where(s => !s.WinnerTeamId.HasValue))
                {
                    if (preserveRegions && !target.RegionId.HasValue)
                        continue;
                    if (preserveConferences && !target.ConferenceId.HasValue)
                        continue;

                    if (target.TeamAId == Guid.Empty)
                    {
                        var candidate = TakeAdvancingTeam(advancing, target);
                        if (candidate != null)
                        {
                            target.TeamAId = candidate.TeamId;
                            target.TeamACoachId = CoachIdFor(league, candidate.TeamId);
                            changed = true;
                        }
                    }

                    if (target.TeamBId == Guid.Empty)
                    {
                        var candidate = TakeAdvancingTeam(advancing, target);
                        if (candidate != null)
                        {
                            target.TeamBId = candidate.TeamId;
                            target.TeamBCoachId = CoachIdFor(league, candidate.TeamId);
                            changed = true;
                        }
                    }

                    if (target.TeamAId != Guid.Empty && target.TeamBId != Guid.Empty)
                        AssignHomeAdvantage(league, season, target, overwrite: true);
                }

                if (preserveRegions)
                {
                    foreach (var regionGroup in advancing
                                 .Where(team => team.RegionId.HasValue)
                                 .GroupBy(team => team.RegionId.GetValueOrDefault())
                                 .ToList())
                    {
                        var regionTeams = regionGroup.ToList();
                        AddAdvancementSeries(
                            league,
                            season,
                            targetRound,
                            finalRound,
                            regionTeams[0].ConferenceId,
                            regionGroup.Key,
                            regionTeams);
                        advancing.RemoveAll(team => team.RegionId == regionGroup.Key);
                        changed = true;
                    }
                }
                else if (preserveConferences)
                {
                    foreach (var conferenceGroup in advancing
                                 .Where(team => team.ConferenceId.HasValue)
                                 .GroupBy(team => team.ConferenceId.GetValueOrDefault())
                                 .ToList())
                    {
                        var conferenceTeams = conferenceGroup.ToList();
                        AddAdvancementSeries(league, season, targetRound, finalRound, conferenceGroup.Key, null, conferenceTeams);
                        advancing.RemoveAll(team => team.ConferenceId == conferenceGroup.Key);
                        changed = true;
                    }
                }

                if (advancing.Count > 0)
                {
                    AddAdvancementSeries(league, season, targetRound, finalRound, null, null, advancing);
                    advancing.Clear();
                    changed = true;
                }

                foreach (var target in season.Playoffs.Where(s => s.Round == targetRound && !s.WinnerTeamId.HasValue))
                {
                    Guid onlyTeam = target.TeamAId != Guid.Empty && target.TeamBId == Guid.Empty
                        ? target.TeamAId
                        : target.TeamBId != Guid.Empty && target.TeamAId == Guid.Empty
                            ? target.TeamBId
                            : Guid.Empty;
                    if (onlyTeam == Guid.Empty)
                        continue;

                    target.HomeAdvantageTeamId = onlyTeam;
                    target.WinnerTeamId = onlyTeam;
                    target.WinnerCoachId = CoachIdFor(league, onlyTeam);
                    target.Notes = "Automatic advancement bye.";
                    changed = true;
                }
            }

            return changed;
        }

        private sealed class AdvancingTeam
        {
            public Guid TeamId { get; set; }
            public Guid SourceSeriesId { get; set; }
            public Guid? ConferenceId { get; set; }
            public Guid? RegionId { get; set; }
            public Guid? DistrictId { get; set; }
            public List<Guid> DistrictIds { get; set; } = new List<Guid>();
            public string BracketGroup { get; set; } = "";
        }

        private static List<AdvancingTeam> BuildAdvancingTeams(LeagueFile league, IEnumerable<PlayoffSeries> series, HashSet<Guid> excluded)
        {
            var result = new List<AdvancingTeam>();
            var seen = new HashSet<Guid>();
            foreach (var item in series)
            {
                if (!item.WinnerTeamId.HasValue || item.WinnerTeamId.Value == Guid.Empty)
                    continue;

                Guid teamId = item.WinnerTeamId.Value;
                if (excluded.Contains(teamId) || !seen.Add(teamId))
                    continue;

                var placement = LeagueHierarchyEngine.FindTeamPlacement(league, teamId);
                result.Add(new AdvancingTeam
                {
                    TeamId = teamId,
                    SourceSeriesId = item.Id,
                    ConferenceId = placement?.ConferenceId ?? item.ConferenceId,
                    RegionId = placement?.RegionId ?? item.RegionId,
                    DistrictId = placement?.DistrictId,
                    DistrictIds = (item.DistrictIds ?? new List<Guid>())
                        .Concat(placement == null ? Enumerable.Empty<Guid>() : new[] { placement.DistrictId })
                        .Where(id => id != Guid.Empty)
                        .Distinct()
                        .ToList(),
                    BracketGroup = item.BracketGroup ?? ""
                });
            }
            return result;
        }

        private static AdvancingTeam TakeAdvancingTeam(List<AdvancingTeam> advancing, PlayoffSeries target)
        {
            var eligible = advancing.Where(team =>
                (target.FeederSeriesIds == null || target.FeederSeriesIds.Count == 0 || target.FeederSeriesIds.Contains(team.SourceSeriesId)) &&
                (!target.RegionId.HasValue || team.RegionId == target.RegionId) &&
                (!target.ConferenceId.HasValue || team.ConferenceId == target.ConferenceId)).ToList();
            if (eligible.Count == 0)
                return null;

            var selected = eligible.FirstOrDefault(team =>
                !string.IsNullOrWhiteSpace(target.BracketGroup) &&
                string.Equals(team.BracketGroup, target.BracketGroup, StringComparison.OrdinalIgnoreCase))
                ?? eligible[0];
            advancing.Remove(selected);
            return selected;
        }

        private static void AddAdvancementSeries(
            LeagueFile league,
            Season season,
            int targetRound,
            int finalRound,
            Guid? conferenceId,
            Guid? regionId,
            List<AdvancingTeam> advancing)
        {
            while (advancing.Count > 0)
            {
                var first = advancing[0];
                advancing.RemoveAt(0);
                var second = advancing.Count > 0 ? advancing[0] : null;
                if (second != null)
                    advancing.RemoveAt(0);

                string conferenceName = conferenceId.HasValue
                    ? LeagueHierarchyEngine.FindConference(league, conferenceId.Value)?.Name
                    : null;
                string regionName = regionId.HasValue
                    ? LeagueHierarchyEngine.FindRegion(league, regionId.Value)?.Name
                    : null;
                var added = new PlayoffSeries
                {
                    Round = targetRound,
                    RoundName = RoundNameFor(targetRound, finalRound),
                    BestOf = targetRound == 2 ? 5 : 7,
                    ConferenceId = conferenceId,
                    RegionId = regionId,
                    DistrictIds = first.DistrictIds
                        .Concat(second?.DistrictIds ?? Enumerable.Empty<Guid>())
                        .Distinct()
                        .ToList(),
                    FeederSeriesIds = new[] { first.SourceSeriesId, second?.SourceSeriesId ?? Guid.Empty }
                        .Where(id => id != Guid.Empty)
                        .Distinct()
                        .ToList(),
                    BracketGroup = !string.IsNullOrWhiteSpace(regionName)
                        ? (string.IsNullOrWhiteSpace(conferenceName) ? regionName : conferenceName + ": " + regionName)
                        : !string.IsNullOrWhiteSpace(conferenceName)
                            ? conferenceName
                            : RoundNameFor(targetRound, finalRound),
                    TeamAId = first.TeamId,
                    TeamBId = second?.TeamId ?? Guid.Empty,
                    TeamACoachId = CoachIdFor(league, first.TeamId),
                    TeamBCoachId = second == null ? Guid.Empty : CoachIdFor(league, second.TeamId)
                };
                AssignHomeAdvantage(league, season, added, overwrite: true);
                if (second == null)
                {
                    added.WinnerTeamId = first.TeamId;
                    added.WinnerCoachId = added.TeamACoachId;
                    added.Notes = "Automatic advancement bye.";
                }
                season.Playoffs.Add(added);
            }
        }

        private static Guid CoachIdFor(LeagueFile league, Guid teamId)
            => league?.Teams?.FirstOrDefault(team => team.Id == teamId)?.CoachId ?? Guid.Empty;

        public static void AssignHomeAdvantage(LeagueFile league, Season season, IEnumerable<PlayoffSeries> series)
        {
            foreach (var item in series ?? Enumerable.Empty<PlayoffSeries>())
                AssignHomeAdvantage(league, season, item, overwrite: true);
        }

        public static void AssignHomeAdvantage(LeagueFile league, Season season, PlayoffSeries series, bool overwrite = false)
        {
            if (league == null || season == null || series == null)
                return;
            if (series.TeamAId == Guid.Empty && series.TeamBId == Guid.Empty)
                return;
            if (!overwrite && series.HomeAdvantageTeamId.HasValue && series.HomeAdvantageTeamId.Value != Guid.Empty)
                return;

            if (series.TeamAId != Guid.Empty && series.TeamBId == Guid.Empty)
            {
                series.HomeAdvantageTeamId = series.TeamAId;
                return;
            }
            if (series.TeamBId != Guid.Empty && series.TeamAId == Guid.Empty)
            {
                series.HomeAdvantageTeamId = series.TeamBId;
                return;
            }

            int aPriority = QualificationHomePriority(league, season, series.TeamAId);
            int bPriority = QualificationHomePriority(league, season, series.TeamBId);
            if (aPriority != bPriority)
            {
                series.HomeAdvantageTeamId = aPriority > bPriority ? series.TeamAId : series.TeamBId;
                return;
            }

            int aRank = RankingEngine.TeamPlayoffSeedRank(season, series.TeamAId);
            int bRank = RankingEngine.TeamPlayoffSeedRank(season, series.TeamBId);
            if (aRank == int.MaxValue || bRank == int.MaxValue)
            {
                var standings = ComputeStandings(season, league.Teams?.Select(t => t.Id) ?? Enumerable.Empty<Guid>());
                var rank = standings
                    .Select((standing, index) => new { standing.TeamId, Rank = index + 1 })
                    .ToDictionary(x => x.TeamId, x => x.Rank);
                aRank = rank.TryGetValue(series.TeamAId, out int a) ? a : int.MaxValue;
                bRank = rank.TryGetValue(series.TeamBId, out int b) ? b : int.MaxValue;
            }
            series.HomeAdvantageTeamId = aRank <= bRank ? series.TeamAId : series.TeamBId;
        }

        public static int QualificationHomePriority(LeagueFile league, Season season, Guid teamId)
        {
            if (league?.Structure?.Conferences == null || season == null || teamId == Guid.Empty)
                return 1;

            var standings = ComputeStandings(season, league.Teams?.Select(t => t.Id) ?? Enumerable.Empty<Guid>());
            foreach (var district in league.Structure.Conferences
                         .SelectMany(c => c.Regions ?? new List<Region>())
                         .SelectMany(r => r.Districts ?? new List<District>()))
            {
                var qualifiers = Qualify(district, standings);
                if (qualifiers == null)
                    continue;
                if (qualifiers.Champion == teamId)
                    return 3;
                if (qualifiers.RunnerUp == teamId)
                    return 2;
                if (qualifiers.Wildcard == teamId)
                    return 1;
            }

            return 1;
        }

        public static Guid HomeTeamForSeriesGame(PlayoffSeries series, int gameNumber)
        {
            if (series == null || gameNumber <= 0)
                return Guid.Empty;

            Guid advantage = series.HomeAdvantageTeamId.GetValueOrDefault();
            if (advantage == Guid.Empty || (advantage != series.TeamAId && advantage != series.TeamBId))
                advantage = series.TeamAId != Guid.Empty ? series.TeamAId : series.TeamBId;

            Guid other = advantage == series.TeamAId ? series.TeamBId : series.TeamAId;
            if (other == Guid.Empty)
                return advantage;

            return gameNumber % 2 == 1 ? advantage : other;
        }

        public static string RoundNameFor(int round, int maxRound)
        {
            maxRound = Math.Clamp(maxRound, MinimumPlayoffRounds, 9);
            if (round <= 1) return "Bi-District";
            if (round == 2) return "Area";
            if (round == 3) return "Regional Quarter Finals";
            if (round >= maxRound) return "World Series";

            if (maxRound <= 6)
            {
                return round switch
                {
                    4 => "Regional",
                    5 => "Semi-Finals",
                    _ => "World Series"
                };
            }

            if (maxRound <= 8)
            {
                return round switch
                {
                    4 => "Regional Semi-Finals",
                    5 => "Regional",
                    6 => "Conference Semi-Finals",
                    7 => "Semi-Finals",
                    _ => "World Series"
                };
            }

            return round switch
            {
                4 => "Regional Semi-Finals",
                5 => "Regional",
                6 => "Conference Quarter-Finals",
                7 => "Conference Semi-Finals",
                8 => "Semi-Finals",
                _ => "World Series"
            };
        }

        private static void ApplyRoundNames(List<PlayoffSeries> series, int finalRound)
        {
            foreach (var item in series)
            {
                item.RoundName = RoundNameFor(item.Round, finalRound);
                if (item.Round == finalRound && string.IsNullOrWhiteSpace(item.BracketGroup))
                    item.BracketGroup = "World Series";
                if (item.Round == finalRound && !string.Equals(item.BracketGroup, "World Series", StringComparison.OrdinalIgnoreCase))
                    item.BracketGroup = "World Series";
            }
        }

        private static void EnsureRoundPlaceholders(List<PlayoffSeries> result, int finalRound)
        {
            for (int round = 3; round <= finalRound; round++)
            {
                if (result.Any(s => s.Round == round))
                    continue;

                result.Add(new PlayoffSeries
                {
                    Round = round,
                    RoundName = RoundNameFor(round, finalRound),
                    BestOf = 7,
                    BracketGroup = round == finalRound ? "World Series" : RoundNameFor(round, finalRound),
                    Notes = round == finalRound
                        ? "Final best-of-7 World Series. Fill winners from prior round."
                        : "Best-of-7 placeholder. Fill winners from prior round."
                });
            }
        }

        private static void AddAtLargeWildcardSeries(
            List<PlayoffSeries> result,
            LeagueFile league,
            Season season,
            List<TeamStanding> allStandings,
            HashSet<Guid> qualified,
            int missingRounds)
        {
            if (missingRounds <= 0)
                return;

            int desiredTeams = Math.Max(2, missingRounds * 2);
            var atLarge = ComputeConferenceRecords(league, season, allStandings)
                .Where(s => !qualified.Contains(s.TeamId))
                .OrderBy(s => RankingEngine.TeamPlayoffSeedRank(season, s.TeamId))
                .ThenByDescending(s => s.Pct)
                .ThenByDescending(s => s.RunDiff)
                .ThenByDescending(s => s.RunsFor)
                .Take(desiredTeams)
                .ToList();

            if (atLarge.Count < 2)
                return;

            if (atLarge.Count % 2 != 0)
                atLarge.RemoveAt(atLarge.Count - 1);

            int pair = 1;
            int left = 0;
            int right = atLarge.Count - 1;
            while (left < right)
            {
                var best = atLarge[left++];
                var worst = atLarge[right--];
                result.Add(MakeSeries(
                    1,
                    "Bi-District",
                    3,
                    best.ConferenceId,
                    null,
                    "At-Large Wildcards",
                    best.TeamId,
                    worst.TeamId,
                    "Additional wildcard balance series #" + pair + ": best available conference record vs lowest available conference record."));
                qualified.Add(best.TeamId);
                qualified.Add(worst.TeamId);
                pair++;
            }
        }

        private sealed class ConferenceRecord
        {
            public Guid TeamId { get; set; }
            public Guid? ConferenceId { get; set; }
            public int Wins { get; set; }
            public int Losses { get; set; }
            public int Ties { get; set; }
            public int RunsFor { get; set; }
            public int RunsAgainst { get; set; }
            public int RunDiff => RunsFor - RunsAgainst;
            public double Pct => Wins + Losses + Ties == 0 ? 0 : (Wins + Ties * 0.5) / (Wins + Losses + Ties);
        }

        private static List<ConferenceRecord> ComputeConferenceRecords(LeagueFile league, Season season, List<TeamStanding> allStandings)
        {
            var conferenceByTeam = league.Structure.Conferences
                .SelectMany(c => c.Regions.SelectMany(r => r.Districts.SelectMany(d => d.TeamIds.Select(teamId => new { TeamId = teamId, ConferenceId = (Guid?)c.Id }))))
                .GroupBy(x => x.TeamId)
                .ToDictionary(g => g.Key, g => g.First().ConferenceId);
            var scheduledById = (season.Schedule ?? new List<ScheduledGame>()).ToDictionary(g => g.Id);
            var records = league.Teams.ToDictionary(t => t.Id, t => new ConferenceRecord
            {
                TeamId = t.Id,
                ConferenceId = conferenceByTeam.TryGetValue(t.Id, out var confId) ? confId : null
            });

            foreach (var game in season.Games ?? new List<GameResult>())
            {
                if (game.IsPlayoff)
                    continue;

                bool conferenceGame = game.ScheduledGameId.HasValue &&
                    scheduledById.TryGetValue(game.ScheduledGameId.Value, out var scheduled) &&
                    scheduled.Type == ScheduledGameType.Conference;
                if (!conferenceGame)
                    continue;

                if (!records.TryGetValue(game.AwayTeamId, out var away) || !records.TryGetValue(game.HomeTeamId, out var home))
                    continue;

                away.RunsFor += game.AwayScore;
                away.RunsAgainst += game.HomeScore;
                home.RunsFor += game.HomeScore;
                home.RunsAgainst += game.AwayScore;
                if (game.AwayScore > game.HomeScore)
                {
                    away.Wins++;
                    home.Losses++;
                }
                else if (game.HomeScore > game.AwayScore)
                {
                    home.Wins++;
                    away.Losses++;
                }
                else
                {
                    away.Ties++;
                    home.Ties++;
                }
            }

            foreach (var standing in allStandings)
            {
                if (!records.TryGetValue(standing.TeamId, out var record) || record.Wins + record.Losses + record.Ties > 0)
                    continue;
                record.Wins = standing.Wins;
                record.Losses = standing.Losses;
                record.Ties = standing.Ties;
                record.RunsFor = standing.RunsFor;
                record.RunsAgainst = standing.RunsAgainst;
            }

            return records.Values
                .OrderByDescending(r => r.Pct)
                .ThenByDescending(r => r.RunDiff)
                .ThenByDescending(r => r.RunsFor)
                .ToList();
        }

        private sealed class DistrictQualifiers
        {
            public Guid Champion { get; set; }
            public Guid RunnerUp { get; set; }
            public Guid Wildcard { get; set; }
        }

        private static DistrictQualifiers Qualify(District district, List<TeamStanding> standings)
        {
            var teams = standings.Where(s => district.TeamIds.Contains(s.TeamId)).Take(3).ToList();
            if (teams.Count < 3) return null;
            return new DistrictQualifiers
            {
                Champion = teams[0].TeamId,
                RunnerUp = teams[1].TeamId,
                Wildcard = teams[2].TeamId
            };
        }

        private static PlayoffSeries MakeSeries(
            int round,
            string name,
            int bestOf,
            Guid? confId,
            Guid? regionId,
            string group,
            Guid a,
            Guid b,
            string notes,
            IEnumerable<Guid>? districtIds = null,
            IEnumerable<Guid>? feederSeriesIds = null)
        {
            return new PlayoffSeries
            {
                Round = round,
                RoundName = name,
                BestOf = bestOf,
                ConferenceId = confId,
                RegionId = regionId,
                DistrictIds = (districtIds ?? Enumerable.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList(),
                FeederSeriesIds = (feederSeriesIds ?? Enumerable.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList(),
                BracketGroup = group,
                TeamAId = a,
                TeamBId = b,
                Notes = notes
            };
        }

        private static void AssignTeamsEvenly(LeagueFile league)
        {
            var districts = league.Structure.Conferences
                .SelectMany(c => c.Regions)
                .SelectMany(r => r.Districts)
                .ToList();
            if (districts.Count == 0) return;

            var assigned = new HashSet<Guid>(districts.SelectMany(d => d.TeamIds));
            var missing = league.Teams.Select(t => t.Id).Where(id => !assigned.Contains(id)).ToList();
            int cursor = 0;
            foreach (var id in missing)
            {
                districts[cursor % districts.Count].TeamIds.Add(id);
                cursor++;
            }

            var valid = league.Teams.Select(t => t.Id).ToHashSet();
            foreach (var district in districts)
                district.TeamIds = district.TeamIds.Where(valid.Contains).Distinct().ToList();
        }
    }
}

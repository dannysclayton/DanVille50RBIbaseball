using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class LeagueHierarchyPlacement
    {
        public Guid TeamId { get; set; }
        public Guid ConferenceId { get; set; }
        public string ConferenceName { get; set; } = "";
        public Guid RegionId { get; set; }
        public string RegionName { get; set; } = "";
        public Guid DistrictId { get; set; }
        public string DistrictName { get; set; } = "";
    }

    public static class LeagueHierarchyEngine
    {
        public static string? Validate(LeagueFile league)
        {
            if (league?.Structure?.Conferences == null)
                return "The league hierarchy is missing conferences.";

            var conferenceIds = new HashSet<Guid>();
            var regionIds = new HashSet<Guid>();
            var districtIds = new HashSet<Guid>();
            var assignedTeams = new HashSet<Guid>();
            foreach (var conference in league.Structure.Conferences)
            {
                if (conference.Id == Guid.Empty || !conferenceIds.Add(conference.Id))
                    return "Every conference must have a unique identifier.";

                foreach (var region in conference.Regions ?? Enumerable.Empty<Region>())
                {
                    if (region.Id == Guid.Empty || !regionIds.Add(region.Id))
                        return "Every region must have a unique identifier.";

                    foreach (var district in region.Districts ?? Enumerable.Empty<District>())
                    {
                        if (district.Id == Guid.Empty || !districtIds.Add(district.Id))
                            return "Every district must have a unique identifier.";

                        foreach (Guid teamId in district.TeamIds ?? Enumerable.Empty<Guid>())
                        {
                            if (teamId == Guid.Empty)
                                return district.Name + " contains an invalid team assignment.";
                            if (!assignedTeams.Add(teamId))
                                return "A team can belong to only one district, region, and conference.";
                        }
                    }
                }
            }

            var leagueTeamIds = (league.Teams ?? new List<Team>()).Select(team => team.Id).ToHashSet();
            if (assignedTeams.Any(teamId => !leagueTeamIds.Contains(teamId)))
                return "The hierarchy contains a team that is not in the league.";
            if (leagueTeamIds.Any(teamId => !assignedTeams.Contains(teamId)))
                return "Every league team must be assigned to a district.";
            return null;
        }

        public static IReadOnlyDictionary<Guid, LeagueHierarchyPlacement> BuildTeamPlacements(LeagueFile league)
        {
            var placements = new Dictionary<Guid, LeagueHierarchyPlacement>();
            foreach (var conference in league?.Structure?.Conferences ?? Enumerable.Empty<Conference>())
            {
                foreach (var region in conference.Regions ?? Enumerable.Empty<Region>())
                {
                    foreach (var district in region.Districts ?? Enumerable.Empty<District>())
                    {
                        foreach (Guid teamId in district.TeamIds ?? Enumerable.Empty<Guid>())
                        {
                            if (teamId == Guid.Empty || placements.ContainsKey(teamId))
                                continue;

                            placements[teamId] = new LeagueHierarchyPlacement
                            {
                                TeamId = teamId,
                                ConferenceId = conference.Id,
                                ConferenceName = conference.Name ?? "",
                                RegionId = region.Id,
                                RegionName = region.Name ?? "",
                                DistrictId = district.Id,
                                DistrictName = district.Name ?? ""
                            };
                        }
                    }
                }
            }
            return placements;
        }

        public static LeagueHierarchyPlacement? FindTeamPlacement(LeagueFile league, Guid teamId)
        {
            if (teamId == Guid.Empty)
                return null;
            return BuildTeamPlacements(league).TryGetValue(teamId, out var placement) ? placement : null;
        }

        public static Conference? FindConference(LeagueFile league, Guid conferenceId)
            => (league?.Structure?.Conferences ?? new List<Conference>())
                .FirstOrDefault(conference => conference.Id == conferenceId);

        public static Region? FindRegion(LeagueFile league, Guid regionId)
            => (league?.Structure?.Conferences ?? new List<Conference>())
                .SelectMany(conference => conference.Regions ?? new List<Region>())
                .FirstOrDefault(region => region.Id == regionId);

        public static District? FindDistrict(LeagueFile league, Guid districtId)
            => (league?.Structure?.Conferences ?? new List<Conference>())
                .SelectMany(conference => conference.Regions ?? new List<Region>())
                .SelectMany(region => region.Districts ?? new List<District>())
                .FirstOrDefault(district => district.Id == districtId);
    }
}

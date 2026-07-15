using System;
using System.Collections.Generic;
using System.Linq;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class PlayoffEngineTests
{
    [Fact]
    public void RoundNameFor_UsesConfiguredSixRoundStructure()
    {
        Assert.Equal("Bi-District", PlayoffEngine.RoundNameFor(1, 6));
        Assert.Equal("Area", PlayoffEngine.RoundNameFor(2, 6));
        Assert.Equal("Regional Quarter Finals", PlayoffEngine.RoundNameFor(3, 6));
        Assert.Equal("Regional", PlayoffEngine.RoundNameFor(4, 6));
        Assert.Equal("Semi-Finals", PlayoffEngine.RoundNameFor(5, 6));
        Assert.Equal("World Series", PlayoffEngine.RoundNameFor(6, 6));
    }

    [Fact]
    public void RoundNameFor_UsesExpandedNineRoundStructure()
    {
        Assert.Equal("Regional Semi-Finals", PlayoffEngine.RoundNameFor(4, 9));
        Assert.Equal("Regional", PlayoffEngine.RoundNameFor(5, 9));
        Assert.Equal("Conference Quarter-Finals", PlayoffEngine.RoundNameFor(6, 9));
        Assert.Equal("Conference Semi-Finals", PlayoffEngine.RoundNameFor(7, 9));
        Assert.Equal("Semi-Finals", PlayoffEngine.RoundNameFor(8, 9));
        Assert.Equal("World Series", PlayoffEngine.RoundNameFor(9, 9));
    }

    [Fact]
    public void HomeTeamForSeriesGame_AlternatesFromHigherSeedHomeAdvantage()
    {
        var higherSeed = Guid.NewGuid();
        var lowerSeed = Guid.NewGuid();
        var series = new PlayoffSeries
        {
            TeamAId = higherSeed,
            TeamBId = lowerSeed,
            HomeAdvantageTeamId = higherSeed
        };

        Assert.Equal(higherSeed, PlayoffEngine.HomeTeamForSeriesGame(series, 1));
        Assert.Equal(lowerSeed, PlayoffEngine.HomeTeamForSeriesGame(series, 2));
        Assert.Equal(higherSeed, PlayoffEngine.HomeTeamForSeriesGame(series, 3));
    }

    [Fact]
    public void AdvanceBracket_PreservesRegionThenConferenceBeforeNationalMerge()
    {
        var league = BuildHierarchyLeague();
        var placements = LeagueHierarchyEngine.BuildTeamPlacements(league);
        var season = new Season();

        foreach (var regionGroup in placements.Values.GroupBy(placement => placement.RegionId))
        {
            foreach (var placement in regionGroup)
            {
                season.Playoffs.Add(new PlayoffSeries
                {
                    Round = 2,
                    RoundName = "Area",
                    ConferenceId = placement.ConferenceId,
                    RegionId = placement.RegionId,
                    DistrictIds = new List<Guid> { placement.DistrictId },
                    TeamAId = placement.TeamId,
                    WinnerTeamId = placement.TeamId
                });
            }
        }

        foreach (var conference in league.Structure.Conferences)
        {
            season.Playoffs.Add(new PlayoffSeries
            {
                Round = 3,
                ConferenceId = conference.Id,
                BracketGroup = conference.Name
            });
        }
        season.Playoffs.Add(new PlayoffSeries { Round = 4, BracketGroup = "Old global placeholder" });
        season.Playoffs.Add(new PlayoffSeries { Round = 5, BracketGroup = "National round" });

        Assert.True(PlayoffEngine.AdvanceBracket(league, season));

        var regionalSeries = season.Playoffs.Where(series => series.Round == 3).ToList();
        Assert.Equal(4, regionalSeries.Count);
        Assert.All(regionalSeries, series =>
        {
            Assert.True(series.RegionId.HasValue);
            Assert.True(series.ConferenceId.HasValue);
            Assert.NotEqual(Guid.Empty, series.TeamAId);
            Assert.NotEqual(Guid.Empty, series.TeamBId);
            Assert.Equal(2, series.FeederSeriesIds.Count);
            Assert.Equal(series.RegionId, placements[series.TeamAId].RegionId);
            Assert.Equal(series.RegionId, placements[series.TeamBId].RegionId);
            Assert.Equal(series.ConferenceId, placements[series.TeamAId].ConferenceId);
            Assert.Equal(series.ConferenceId, placements[series.TeamBId].ConferenceId);
        });

        foreach (var series in regionalSeries)
            series.WinnerTeamId = series.TeamAId;
        Assert.True(PlayoffEngine.AdvanceBracket(league, season));

        var conferenceSeries = season.Playoffs.Where(series => series.Round == 4).ToList();
        Assert.Equal(2, conferenceSeries.Count);
        Assert.All(conferenceSeries, series =>
        {
            Assert.True(series.ConferenceId.HasValue);
            Assert.False(series.RegionId.HasValue);
            Assert.Equal(series.ConferenceId, placements[series.TeamAId].ConferenceId);
            Assert.Equal(series.ConferenceId, placements[series.TeamBId].ConferenceId);
        });

        foreach (var series in conferenceSeries)
            series.WinnerTeamId = series.TeamAId;
        Assert.True(PlayoffEngine.AdvanceBracket(league, season));

        var nationalSeries = Assert.Single(season.Playoffs, series => series.Round == 5);
        Assert.False(nationalSeries.ConferenceId.HasValue);
        Assert.NotEqual(Guid.Empty, nationalSeries.TeamAId);
        Assert.NotEqual(Guid.Empty, nationalSeries.TeamBId);
        Assert.NotEqual(
            placements[nationalSeries.TeamAId].ConferenceId,
            placements[nationalSeries.TeamBId].ConferenceId);
    }

    [Fact]
    public void GeneratePlayoffs_StoresDistrictRegionConferenceAndFeederLinks()
    {
        var league = BuildGenerationLeague();
        var playoffs = PlayoffEngine.GeneratePlayoffs(league, new Season(), out string error);

        Assert.Null(error);
        var biDistrict = playoffs.Where(series => series.Round == 1 && series.BracketGroup != "At-Large Wildcards").ToList();
        var area = playoffs.Where(series => series.Round == 2).ToList();
        var regional = playoffs.Where(series => series.Round == 3).ToList();

        Assert.Equal(4, biDistrict.Count);
        Assert.All(biDistrict, series =>
        {
            Assert.True(series.ConferenceId.HasValue);
            Assert.True(series.RegionId.HasValue);
            Assert.Equal(2, series.DistrictIds.Count);
        });
        Assert.Equal(4, area.Count);
        Assert.All(area, series =>
        {
            Assert.True(series.ConferenceId.HasValue);
            Assert.True(series.RegionId.HasValue);
            Assert.Single(series.DistrictIds);
            Assert.Single(series.FeederSeriesIds);
            Assert.Contains(series.FeederSeriesIds[0], biDistrict.Select(feeder => feeder.Id));
        });
        Assert.Equal(2, regional.Count);
        Assert.All(regional, series =>
        {
            Assert.True(series.ConferenceId.HasValue);
            Assert.True(series.RegionId.HasValue);
            Assert.Equal(2, series.DistrictIds.Count);
            Assert.Equal(2, series.FeederSeriesIds.Count);
            Assert.All(series.FeederSeriesIds, feederId => Assert.Contains(feederId, area.Select(feeder => feeder.Id)));
        });
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void GeneratedBracket_AdvancesEveryRoundToOneWorldSeriesWinnerWithoutMixingConferences(int conferenceCount)
    {
        var league = BuildFullBracketLeague(conferenceCount);
        var season = new Season();
        season.Playoffs = PlayoffEngine.GeneratePlayoffs(league, season, out string error);
        Assert.Null(error);
        var placements = LeagueHierarchyEngine.BuildTeamPlacements(league);

        for (int pass = 0; pass < 30; pass++)
        {
            foreach (var series in season.Playoffs
                         .Where(series => series.TeamAId != Guid.Empty &&
                             series.TeamBId != Guid.Empty && !series.WinnerTeamId.HasValue)
                         .ToList())
            {
                if (series.ConferenceId.HasValue)
                {
                    Assert.Equal(series.ConferenceId, placements[series.TeamAId].ConferenceId);
                    Assert.Equal(series.ConferenceId, placements[series.TeamBId].ConferenceId);
                }
                if (series.RegionId.HasValue)
                {
                    Assert.Equal(series.RegionId, placements[series.TeamAId].RegionId);
                    Assert.Equal(series.RegionId, placements[series.TeamBId].RegionId);
                }
                series.WinnerTeamId = series.TeamAId;
            }

            PlayoffEngine.AdvanceBracket(league, season);
            var final = season.Playoffs.SingleOrDefault(series =>
                series.Round == season.Playoffs.Max(value => value.Round));
            if (final?.WinnerTeamId.HasValue == true)
                break;
        }

        var worldSeries = Assert.Single(
            season.Playoffs,
            series => series.Round == season.Playoffs.Max(value => value.Round));
        Assert.Equal("World Series", worldSeries.RoundName);
        Assert.True(worldSeries.WinnerTeamId.HasValue);
        Assert.Contains(worldSeries.WinnerTeamId.Value, new[] { worldSeries.TeamAId, worldSeries.TeamBId });
        Assert.Equal(7, worldSeries.BestOf);
    }

    [Fact]
    public void HomeAdvantagePrioritizesDistrictChampionOverHigherRankedWildcard()
    {
        var league = BuildGenerationLeague();
        var season = new Season();
        var district = league.Structure.Conferences[0].Regions[0].Districts[0];
        Team champion = league.Teams.First(team => team.Id == district.TeamIds[0]);
        Team wildcard = league.Teams.First(team => team.Id == district.TeamIds[2]);
        season.Games.Add(RegressionTestData.Result(champion, league.Teams.First(team => team.Id == district.TeamIds[1]), 9, 1));
        season.Games.Add(RegressionTestData.Result(wildcard, champion, 1, 2));
        RankingEngine.SavePoll(season, new SeasonRankingPoll
        {
            Type = RankingPollType.Weekly,
            Week = 10,
            Rankings =
            {
                new SeasonRankingEntry { TeamId = wildcard.Id, Rank = 1 },
                new SeasonRankingEntry { TeamId = champion.Id, Rank = 20 }
            }
        });
        var series = new PlayoffSeries { TeamAId = wildcard.Id, TeamBId = champion.Id };

        PlayoffEngine.AssignHomeAdvantage(league, season, series, overwrite: true);

        Assert.Equal(champion.Id, series.HomeAdvantageTeamId);
        Assert.Equal(champion.Id, PlayoffEngine.HomeTeamForSeriesGame(series, 1));
        Assert.Equal(wildcard.Id, PlayoffEngine.HomeTeamForSeriesGame(series, 2));
    }

    private static LeagueFile BuildHierarchyLeague()
    {
        var league = new LeagueFile { Structure = new LeagueStructure() };
        for (int conferenceNumber = 1; conferenceNumber <= 2; conferenceNumber++)
        {
            var conference = new Conference { Name = "Conference " + conferenceNumber };
            for (int regionNumber = 1; regionNumber <= 2; regionNumber++)
            {
                var region = new Region { Name = "Region " + regionNumber };
                for (int districtNumber = 1; districtNumber <= 2; districtNumber++)
                {
                    var team = new Team
                    {
                        City = "C" + conferenceNumber + "R" + regionNumber + "D" + districtNumber,
                        Nickname = "Club"
                    };
                    league.Teams.Add(team);
                    region.Districts.Add(new District
                    {
                        Name = "District " + districtNumber,
                        TeamIds = new List<Guid> { team.Id }
                    });
                }
                conference.Regions.Add(region);
            }
            league.Structure.Conferences.Add(conference);
        }
        return league;
    }

    private static LeagueFile BuildGenerationLeague()
    {
        var league = new LeagueFile { Structure = new LeagueStructure() };
        var conference = new Conference { Name = "Conference 1" };
        for (int regionNumber = 1; regionNumber <= 2; regionNumber++)
        {
            var region = new Region { Name = "Region " + regionNumber };
            for (int districtNumber = 1; districtNumber <= 2; districtNumber++)
            {
                var district = new District { Name = "District " + districtNumber };
                for (int teamNumber = 1; teamNumber <= 3; teamNumber++)
                {
                    var team = new Team
                    {
                        City = "R" + regionNumber + "D" + districtNumber + "T" + teamNumber,
                        Nickname = "Club"
                    };
                    league.Teams.Add(team);
                    district.TeamIds.Add(team.Id);
                }
                region.Districts.Add(district);
            }
            conference.Regions.Add(region);
        }
        league.Structure.Conferences.Add(conference);
        return league;
    }

    private static LeagueFile BuildFullBracketLeague(int conferenceCount)
    {
        var league = new LeagueFile { Structure = new LeagueStructure() };
        for (int conferenceNumber = 1; conferenceNumber <= conferenceCount; conferenceNumber++)
        {
            var conference = new Conference { Name = "Conference " + conferenceNumber };
            for (int regionNumber = 1; regionNumber <= 2; regionNumber++)
            {
                var region = new Region { Name = "Region " + regionNumber };
                for (int districtNumber = 1; districtNumber <= 2; districtNumber++)
                {
                    var district = new District { Name = "District " + districtNumber };
                    for (int teamNumber = 1; teamNumber <= 3; teamNumber++)
                    {
                        var team = new Team
                        {
                            City = $"C{conferenceNumber}R{regionNumber}D{districtNumber}T{teamNumber}",
                            Nickname = "Club"
                        };
                        league.Teams.Add(team);
                        district.TeamIds.Add(team.Id);
                    }
                    region.Districts.Add(district);
                }
                conference.Regions.Add(region);
            }
            league.Structure.Conferences.Add(conference);
        }
        return league;
    }
}

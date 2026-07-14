using System.Linq;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class ScheduleGeneratorTests
{
    [Fact]
    public void Generate_FourGameSeriesSchedulesFridaySaturdayAndSundayDoubleheader()
    {
        var league = TestLeagueWithSingleDistrict(4);
        var rules = new SeasonScheduleRules
        {
            SeriesLength = 4,
            DistrictHomeGames = 4,
            DistrictAwayGames = 4
        };

        var schedule = ScheduleGenerator.Generate(league, rules, out string error);

        Assert.Null(error);
        Assert.Equal(16, schedule.Count);
        Assert.All(schedule, game => Assert.True(game.Week > 0));
        Assert.All(schedule, game => Assert.True(game.GameNumber > 0));

        var seriesBlocks = schedule
            .GroupBy(g => g.Type + ":" + g.AwayTeamId.ToString("N") + ":" + g.HomeTeamId.ToString("N"))
            .ToList();
        Assert.All(seriesBlocks, block =>
        {
            Assert.Equal(4, block.Count());
            Assert.Equal(new[] { "Friday", "Saturday", "Sunday DH1", "Sunday DH2" },
                block.OrderBy(g => g.GameNumber).Select(g => g.DayLabel).ToArray());
        });
    }

    [Fact]
    public void Generate_RejectsUnbalancedHomeAwayCounts()
    {
        var league = TestLeagueWithSingleDistrict(4);
        var rules = new SeasonScheduleRules
        {
            SeriesLength = 3,
            DistrictHomeGames = 2,
            DistrictAwayGames = 1
        };

        var schedule = ScheduleGenerator.Generate(league, rules, out string error);

        Assert.Empty(schedule);
        Assert.Equal("District home and away counts must match so every scheduled game has one home team and one away team.", error);
    }

    private static LeagueFile TestLeagueWithSingleDistrict(int teamCount)
    {
        var league = new LeagueFile
        {
            Structure = new LeagueStructure()
        };
        var conference = PlayoffEngine.CreateConference(1);
        league.Structure.Conferences.Add(conference);

        var district = conference.Regions[0].Districts[0];
        for (int i = 1; i <= teamCount; i++)
        {
            var team = new Team { City = "Team" + i, Nickname = "Club" + i };
            league.Teams.Add(team);
            district.TeamIds.Add(team.Id);
        }

        return league;
    }
}

using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class RankingEngineRegressionTests
{
    [Fact]
    public void FirstPreseasonPollUsesVarsityRatingsSeniorClassAndCoachLevel()
    {
        Team strong = RegressionTestData.CreateTeam("Strong", 90);
        Team weak = RegressionTestData.CreateTeam("Weak", 50);
        strong.Coaches[0].Style = CoachStyle.Championship;
        weak.Coaches[0].Style = CoachStyle.BelowAverage;
        var league = RegressionTestData.CreateLeague(weak, strong);
        var season = new Season();
        league.Seasons.Add(season);

        SeasonRankingPoll poll = RankingEngine.GeneratePoll(league, season, RankingPollType.PreSeason);

        Assert.Equal(strong.Id, poll.Rankings[0].TeamId);
        Assert.Equal(1, poll.Rankings[0].Rank);
        Assert.True(poll.Rankings[0].Score > poll.Rankings[1].Score);
    }

    [Fact]
    public void PollRanksEveryTeamButOfficialListRemainsTopTwentyFive()
    {
        Team[] teams = Enumerable.Range(1, 30)
            .Select(index => RegressionTestData.CreateTeam("Team " + index, 50 + index))
            .ToArray();
        var league = RegressionTestData.CreateLeague(teams);
        var poll = RankingEngine.GeneratePoll(league, new Season(), RankingPollType.PreSeason);

        Assert.Equal(30, poll.Rankings.Count);
        Assert.Equal(25, RankingEngine.OfficialCount(league));
        Assert.Equal(Enumerable.Range(1, 30), poll.Rankings.Select(entry => entry.Rank).Distinct());
    }

    [Fact]
    public void EqualPollScoresShareHigherRankAndReceiveTieNote()
    {
        Team alpha = RegressionTestData.CreateTeam("Alpha", 70);
        Team beta = RegressionTestData.CreateTeam("Beta", 70);
        var poll = RankingEngine.GeneratePoll(
            RegressionTestData.CreateLeague(alpha, beta), new Season(), RankingPollType.PreSeason);

        Assert.All(poll.Rankings, entry => Assert.Equal(1, entry.Rank));
        Assert.All(poll.Rankings, entry => Assert.Contains("Tied for #1", entry.Notes));
    }

    [Fact]
    public void WeeklyPollOnlyCountsGamesThroughRequestedWeek()
    {
        Team alpha = RegressionTestData.CreateTeam("Alpha", 70);
        Team beta = RegressionTestData.CreateTeam("Beta", 70);
        var league = RegressionTestData.CreateLeague(alpha, beta);
        var weekOne = new ScheduledGame { Week = 1, AwayTeamId = alpha.Id, HomeTeamId = beta.Id };
        var weekTwo = new ScheduledGame { Week = 2, AwayTeamId = beta.Id, HomeTeamId = alpha.Id };
        var season = new Season { Schedule = { weekOne, weekTwo } };
        season.Games.Add(new GameResult
        {
            ScheduledGameId = weekOne.Id,
            AwayTeamId = alpha.Id,
            HomeTeamId = beta.Id,
            AwayScore = 5,
            HomeScore = 1
        });
        season.Games.Add(new GameResult
        {
            ScheduledGameId = weekTwo.Id,
            AwayTeamId = beta.Id,
            HomeTeamId = alpha.Id,
            AwayScore = 8,
            HomeScore = 0
        });

        var poll = RankingEngine.GeneratePoll(league, season, RankingPollType.Weekly, 1);
        var alphaEntry = poll.Rankings.Single(entry => entry.TeamId == alpha.Id);
        var betaEntry = poll.Rankings.Single(entry => entry.TeamId == beta.Id);

        Assert.Equal((1, 0), (alphaEntry.Wins, alphaEntry.Losses));
        Assert.Equal((0, 1), (betaEntry.Wins, betaEntry.Losses));
    }

    [Fact]
    public void FinalPollLocksWorldSeriesChampionAtNumberOne()
    {
        Team champion = RegressionTestData.CreateTeam("Champion", 45);
        Team dominant = RegressionTestData.CreateTeam("Dominant", 95);
        var league = RegressionTestData.CreateLeague(champion, dominant);
        var season = new Season { ChampionTeamId = champion.Id };
        for (int i = 0; i < 5; i++)
            season.Games.Add(RegressionTestData.Result(champion, dominant, 0, 10));

        var final = RankingEngine.GeneratePoll(league, season, RankingPollType.Final);

        Assert.Equal(champion.Id, final.Rankings[0].TeamId);
        Assert.Equal(1, final.Rankings[0].Rank);
    }

    [Fact]
    public void PlayoffSeedUsesLastRegularSeasonPollInsteadOfFinalPoll()
    {
        Team alpha = RegressionTestData.CreateTeam("Alpha");
        Team beta = RegressionTestData.CreateTeam("Beta");
        var season = new Season();
        RankingEngine.SavePoll(season, Poll(RankingPollType.Weekly, 8, (alpha, 1), (beta, 2)));
        RankingEngine.SavePoll(season, Poll(RankingPollType.Final, 0, (beta, 1), (alpha, 2)));

        Assert.Equal(1, RankingEngine.TeamPlayoffSeedRank(season, alpha.Id));
        Assert.Equal(2, RankingEngine.TeamPlayoffSeedRank(season, beta.Id));
        Assert.Equal(1, RankingEngine.TeamRank(season, beta.Id));
    }

    private static SeasonRankingPoll Poll(
        RankingPollType type, int week, params (Team Team, int Rank)[] entries)
    {
        return new SeasonRankingPoll
        {
            Type = type,
            Week = week,
            Rankings = entries.Select(value => new SeasonRankingEntry
            {
                TeamId = value.Team.Id,
                TeamName = value.Team.DisplayName,
                Rank = value.Rank,
                Score = entries.Length - value.Rank
            }).ToList()
        };
    }
}

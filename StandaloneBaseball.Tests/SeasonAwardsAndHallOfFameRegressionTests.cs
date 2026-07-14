using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class SeasonAwardsAndHallOfFameRegressionTests
{
    [Fact]
    public void AwardRacesSelectNamedStatLeadersAndChampionshipCoach()
    {
        Team champion = RegressionTestData.CreateTeam("Champion", 72);
        Team opponent = RegressionTestData.CreateTeam("Opponent", 68);
        Player slugger = champion.Roster.First(player => player.Role == PlayerRole.Batter);
        Player ace = champion.Roster.First(player => player.Role == PlayerRole.Pitcher);
        var season = new Season { ChampionTeamId = champion.Id };
        season.Playoffs.Add(new PlayoffSeries
        {
            RoundName = "World Series",
            TeamAId = champion.Id,
            TeamBId = opponent.Id,
            WinnerTeamId = champion.Id,
            WinnerCoachId = champion.CoachId
        });
        var result = RegressionTestData.Result(champion, opponent, 12, 2);
        result.Lines.Add(HitterLine(champion, slugger, hits: 48, homeRuns: 22, rbi: 71));
        result.Lines.Add(PitcherLine(champion, ace, wins: 12, strikeouts: 140, saves: 4));
        result.Lines.Add(HitterLine(opponent,
            opponent.Roster.First(player => player.Role == PlayerRole.Batter), hits: 22, homeRuns: 5, rbi: 20));
        result.Lines.Add(PitcherLine(opponent,
            opponent.Roster.First(player => player.Role == PlayerRole.Pitcher), wins: 3, strikeouts: 40, saves: 1));
        season.Games.Add(result);
        var league = RegressionTestData.CreateLeague(champion, opponent);
        league.Seasons.Add(season);

        List<object> candidates = InvokeList(CreateMainFormShell(league), "BuildSeasonAwardCandidates", season);

        AssertAwardWinner(candidates, "Babe Ruth Award", slugger.Id);
        AssertAwardWinner(candidates, "Nolan Ryan Award", ace.Id);
        AssertAwardWinner(candidates, "Cy Young Award", ace.Id);
        AssertAwardWinner(candidates, "Johnny Oates Award", champion.CoachId);
        Assert.Contains(candidates, candidate =>
            Property<string>(candidate, "AwardName") == "Ivan Rodriguez Award" &&
            Property<int>(candidate, "Rank") == 1);
    }

    [Fact]
    public void JohnnyOatesWinnerCreatesOrUpdatesCoachHallOfFameEntry()
    {
        Team champion = RegressionTestData.CreateTeam("Champion");
        Team opponent = RegressionTestData.CreateTeam("Opponent");
        var season = new Season { ChampionTeamId = champion.Id };
        season.Awards.Add(new SeasonAwardSelection
        {
            AwardName = "Johnny Oates Award",
            Category = "Coach Award",
            Winner = true,
            Rank = 1,
            PlayerId = champion.CoachId,
            TeamId = champion.Id,
            PlayerName = champion.Coaches[0].Name,
            TeamName = champion.DisplayName
        });
        season.Playoffs.Add(new PlayoffSeries
        {
            RoundName = "World Series",
            TeamAId = champion.Id,
            TeamBId = opponent.Id,
            WinnerTeamId = champion.Id,
            WinnerCoachId = champion.CoachId
        });
        var league = RegressionTestData.CreateLeague(champion, opponent);
        league.Seasons.Add(season);
        MainForm main = CreateMainFormShell(league);

        Invoke(main, "EnsureJohnnyOatesHallOfFameEntry", season);
        Invoke(main, "EnsureJohnnyOatesHallOfFameEntry", season);

        HallOfFameEntry entry = Assert.Single(league.HallOfFameEntries);
        Assert.Equal("Coach", entry.EntryType);
        Assert.Equal(champion.CoachId, entry.CoachId);
        Assert.Equal(champion.Id, entry.TeamId);
        Assert.Equal(1, entry.Championships);
        Assert.Contains("Johnny Oates Award winner", entry.Reason);
    }

    [Fact]
    public void HallOfFameBuildAppliesSeniorCareerProjectionAndSeparatePlayoffBonus()
    {
        Team team = RegressionTestData.CreateTeam("Legacy", 75);
        Team opponent = RegressionTestData.CreateTeam("Opponent", 70);
        Player player = team.Roster.First(value => value.Role == PlayerRole.Batter);
        player.Classification = PlayerClassification.Senior;
        player.InitialClassification = PlayerClassification.Senior;
        var season = new Season { ChampionTeamId = team.Id };
        var regular = RegressionTestData.Result(team, opponent, 8, 2);
        regular.Lines.Add(HitterLine(team, player, hits: 10, homeRuns: 2, rbi: 8));
        var playoff = RegressionTestData.Result(team, opponent, 7, 3);
        playoff.IsPlayoff = true;
        playoff.Lines.Add(HitterLine(team, player, hits: 8, homeRuns: 4, rbi: 11, atBats: 10));
        season.Games.Add(regular);
        season.Games.Add(playoff);
        var league = RegressionTestData.CreateLeague(team, opponent);
        league.Seasons.Add(season);

        object candidate = InvokeList(CreateMainFormShell(league), "BuildHallOfFameCandidates")
            .Single(value => Property<Guid>(value, "PlayerId") == player.Id);
        object stats = Property<object>(candidate, "Stats");
        object playoffStats = Property<object>(candidate, "PlayoffStats");

        Assert.Equal(72, Property<int>(stats, "H"));
        Assert.Equal(24, Property<int>(stats, "HR"));
        Assert.Equal(8, Property<int>(playoffStats, "H"));
        Assert.Equal(4, Property<int>(playoffStats, "HR"));
        Assert.True(Property<int>(candidate, "PlayoffBonus") > 0);
        Assert.True(Property<int>(candidate, "HallScore") > Property<int>(candidate, "PlayoffBonus"));
        Assert.Contains("+3 projected", Property<string>(candidate, "ExtrapolationReason"));
        Assert.Contains("playoff HR", Property<string>(candidate, "PlayoffBonusReason"));
    }

    [Theory]
    [InlineData(PlayerClassification.Freshman, 0)]
    [InlineData(PlayerClassification.Sophomore, 1)]
    [InlineData(PlayerClassification.Junior, 2)]
    [InlineData(PlayerClassification.Senior, 3)]
    public void HallOfFameCareerProjectionMatchesInitialClassification(
        PlayerClassification classification, int expectedMissingSeasons)
    {
        Type candidateType = typeof(MainForm).GetNestedType("HallOfFameCandidate", BindingFlags.NonPublic);
        MethodInfo method = typeof(MainForm).GetMethod("MissingHallProjectionSeasons",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(candidateType);
        Assert.NotNull(method);
        Assert.Equal(expectedMissingSeasons, (int)method.Invoke(null, new object[] { classification }));
    }

    private static PlayerGameLine HitterLine(
        Team team, Player player, int hits, int homeRuns, int rbi, int atBats = 100)
    {
        return new PlayerGameLine
        {
            TeamId = team.Id,
            PlayerId = player.Id,
            PlayerName = player.Name,
            Classification = player.Classification,
            InitialClassification = player.InitialClassification,
            AB = atBats,
            H = hits,
            Doubles = Math.Max(0, hits / 5),
            HR = homeRuns,
            RBI = rbi,
            R = Math.Max(1, rbi / 2),
            BB = 12,
            SB = 5,
            Putouts = 30,
            Assists = 10
        };
    }

    private static PlayerGameLine PitcherLine(
        Team team, Player player, int wins, int strikeouts, int saves)
    {
        return new PlayerGameLine
        {
            TeamId = team.Id,
            PlayerId = player.Id,
            PlayerName = player.Name,
            Pitcher = true,
            StartingPitcher = true,
            Classification = player.Classification,
            InitialClassification = player.InitialClassification,
            IPOuts = 180,
            ER = 18,
            K = strikeouts,
            HitsAllowed = 35,
            WalksAllowed = 12,
            Wins = wins,
            Saves = saves
        };
    }

    private static MainForm CreateMainFormShell(LeagueFile league)
    {
        var main = (MainForm)RuntimeHelpers.GetUninitializedObject(typeof(MainForm));
        FieldInfo field = typeof(MainForm).GetField("_league", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(main, league);
        return main;
    }

    private static List<object> InvokeList(MainForm main, string methodName, params object[] arguments)
    {
        var result = Invoke(main, methodName, arguments) as IEnumerable;
        Assert.NotNull(result);
        return result.Cast<object>().ToList();
    }

    private static object Invoke(MainForm main, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(MainForm).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(main, arguments);
    }

    private static T Property<T>(object value, string name)
    {
        PropertyInfo property = value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return (T)property.GetValue(value);
    }

    private static void AssertAwardWinner(IEnumerable<object> candidates, string awardName, Guid expectedId)
    {
        object winner = candidates.Single(candidate =>
            Property<string>(candidate, "AwardName") == awardName && Property<int>(candidate, "Rank") == 1);
        Assert.Equal(expectedId, Property<Guid>(winner, "PlayerId"));
        Assert.True(Property<bool>(winner, "Winner"));
    }
}

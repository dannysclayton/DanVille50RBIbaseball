using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class StatisticalModelTests
{
    [Fact]
    public void PlayerGameLine_DerivesCompleteBattingAndDefensiveStatistics()
    {
        var line = new PlayerGameLine
        {
            AB = 10,
            BB = 2,
            HBP = 1,
            SH = 1,
            SF = 1,
            Doubles = 2,
            Triples = 1,
            HR = 3,
            Putouts = 5,
            Assists = 3,
            Errors = 2,
            StolenBasesAllowed = 6,
            CatcherCaughtStealing = 4
        };

        Assert.Equal(15, line.PlateAppearances);
        Assert.Equal(6, line.ExtraBaseHits);
        Assert.Equal(10, line.TotalChances);
        Assert.Equal(10, line.CatcherStealAttempts);
        Assert.Equal(0.4, line.CatcherCaughtStealingPercentage, 6);
    }

    [Fact]
    public void LeagueStore_RoundTripPreservesCompleteStatisticalLine()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DansRBI-StatTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "stats" + LeagueStore.Extension);
        Directory.CreateDirectory(directory);
        try
        {
            var line = new PlayerGameLine
            {
                ReachedOnError = 2,
                RunsAllowed = 5,
                DoublesAllowed = 3,
                TriplesAllowed = 1,
                Holds = 4,
                CompleteGames = 2,
                Shutouts = 1,
                DefensiveOuts = 81,
                Putouts = 8,
                Assists = 4,
                Errors = 1,
                StolenBasesAllowed = 7,
                CatcherCaughtStealing = 5
            };
            var league = new LeagueFile
            {
                Seasons =
                {
                    new Season
                    {
                        Games = { new GameResult { Lines = { line } } }
                    }
                }
            };

            LeagueStore.Save(path, league);
            PlayerGameLine restored = LeagueStore.Load(path).Seasons.Single().Games.Single().Lines.Single();

            Assert.Equal(line.ReachedOnError, restored.ReachedOnError);
            Assert.Equal(line.RunsAllowed, restored.RunsAllowed);
            Assert.Equal(line.DoublesAllowed, restored.DoublesAllowed);
            Assert.Equal(line.TriplesAllowed, restored.TriplesAllowed);
            Assert.Equal(line.Holds, restored.Holds);
            Assert.Equal(line.CompleteGames, restored.CompleteGames);
            Assert.Equal(line.Shutouts, restored.Shutouts);
            Assert.Equal(line.DefensiveOuts, restored.DefensiveOuts);
            Assert.Equal(line.TotalChances, restored.TotalChances);
            Assert.Equal(line.CatcherCaughtStealingPercentage, restored.CatcherCaughtStealingPercentage, 6);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SimulatedGame_TracksRunsAllowedExtraBaseHitsAndDefensiveInningsConsistently()
    {
        Team away = CreateTeam("Away");
        Team home = CreateTeam("Home");
        var league = new LeagueFile
        {
            Rules = new LeagueRules
            {
                Innings = 5,
                MercyRuleEnabled = false,
                ExtraInnings = true,
                ExtraInningRunnerOnSecond = true
            },
            Teams = { away, home }
        };

        GameResult result = SimulatedGameEngine.Simulate(league, away, home, new Random(260713));
        List<PlayerGameLine> awayLines = result.Lines.Where(line => line.TeamId == away.Id).ToList();
        List<PlayerGameLine> homeLines = result.Lines.Where(line => line.TeamId == home.Id).ToList();

        Assert.Equal(result.HomeScore, awayLines.Where(line => line.Pitcher).Sum(line => line.RunsAllowed));
        Assert.Equal(result.AwayScore, homeLines.Where(line => line.Pitcher).Sum(line => line.RunsAllowed));
        Assert.Equal(homeLines.Where(line => !line.Pitcher).Sum(line => line.Doubles), awayLines.Where(line => line.Pitcher).Sum(line => line.DoublesAllowed));
        Assert.Equal(homeLines.Where(line => !line.Pitcher).Sum(line => line.Triples), awayLines.Where(line => line.Pitcher).Sum(line => line.TriplesAllowed));
        Assert.Equal(awayLines.Where(line => !line.Pitcher).Sum(line => line.Doubles), homeLines.Where(line => line.Pitcher).Sum(line => line.DoublesAllowed));
        Assert.Equal(awayLines.Where(line => !line.Pitcher).Sum(line => line.Triples), homeLines.Where(line => line.Pitcher).Sum(line => line.TriplesAllowed));
        Assert.Equal(awayLines.Where(line => line.Pitcher).Sum(line => line.IPOuts) * 9, awayLines.Sum(line => line.DefensiveOuts));
        Assert.Equal(homeLines.Where(line => line.Pitcher).Sum(line => line.IPOuts) * 9, homeLines.Sum(line => line.DefensiveOuts));
    }

    private static Team CreateTeam(string name)
    {
        var team = new Team { City = name, Nickname = "Club" };
        for (int i = 1; i <= 5; i++)
        {
            team.Roster.Add(new Player
            {
                Name = name + " Pitcher " + i,
                Role = PlayerRole.Pitcher,
                Positions = "P",
                Pitching = 78 + i,
                Stamina = 82,
                Accuracy = 80,
                Fielding = 70,
                CareerPitchCount = 180,
                Classification = PlayerClassification.Senior
            });
        }

        string[] positions = { "C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "1B" };
        for (int i = 0; i < positions.Length; i++)
        {
            team.Roster.Add(new Player
            {
                Name = name + " Batter " + (i + 1),
                Role = PlayerRole.Batter,
                Positions = positions[i],
                Contact = 68 + i,
                Power = 58 + i,
                Speed = 62,
                BaseRunning = 64,
                Fielding = 72,
                ArmStrength = 68,
                Accuracy = 70,
                PopTime = positions[i] == "C" ? 75 : 50,
                TagRating = 68,
                Classification = PlayerClassification.Senior
            });
        }

        team.BaseLineup = LineupEngine.CreateBaseLineup(team);
        return team;
    }
}

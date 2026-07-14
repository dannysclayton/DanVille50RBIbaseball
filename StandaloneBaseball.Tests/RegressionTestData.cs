using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

internal static class RegressionTestData
{
    public static Team CreateTeam(string name, int rating = 70, int pitcherCount = 5)
    {
        var team = new Team
        {
            City = name,
            Nickname = "Club",
            ScoreboardAbbreviation = name.Length <= 6 ? name : name[..6]
        };
        team.Coaches.Add(new Coach
        {
            Id = team.CoachId,
            Name = name + " Coach",
            Role = "Head Coach",
            Style = CoachStyle.Average,
            Strategy = CoachStrategy.Conservative,
            Active = true
        });

        for (int i = 0; i < pitcherCount; i++)
        {
            team.Roster.Add(new Player
            {
                Name = name + " Pitcher " + (i + 1),
                Role = PlayerRole.Pitcher,
                Positions = "P",
                Classification = PlayerClassification.Senior,
                InitialClassification = PlayerClassification.Freshman,
                Pitching = Math.Clamp(rating + i, 1, 99),
                Stamina = Math.Clamp(rating + 5, 1, 99),
                Accuracy = rating,
                Fielding = rating,
                Durability = 90,
                CareerPitchCount = 180,
                PitchArsenal =
                {
                    new PlayerPitchProfile { PitchType = GameplayPitchType.Fastball, Enabled = true, Effectiveness = rating },
                    new PlayerPitchProfile { PitchType = GameplayPitchType.Changeup, Enabled = true, Effectiveness = rating - 4 },
                    new PlayerPitchProfile { PitchType = GameplayPitchType.Curveball, Enabled = true, Effectiveness = rating - 7 }
                }
            });
        }

        string[] positions = { "C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH" };
        for (int i = 0; i < positions.Length; i++)
        {
            team.Roster.Add(new Player
            {
                Name = name + " Batter " + (i + 1),
                Role = PlayerRole.Batter,
                Positions = positions[i],
                Classification = i == 0 ? PlayerClassification.Senior : PlayerClassification.Junior,
                InitialClassification = PlayerClassification.Freshman,
                Contact = Math.Clamp(rating + i, 1, 99),
                Power = Math.Clamp(rating - 5 + i, 1, 99),
                Speed = rating,
                BaseRunning = rating,
                Fielding = rating,
                ArmStrength = rating,
                Accuracy = rating,
                PopTime = positions[i] == "C" ? rating + 5 : 50,
                TagRating = rating,
                Durability = 90,
                PitchArsenal =
                {
                    new PlayerPitchProfile { PitchType = GameplayPitchType.Fastball, Enabled = false, Effectiveness = 25 }
                },
                PitchStrengths = { GameplayPitchType.Fastball },
                PitchWeaknesses = { GameplayPitchType.Knuckleball }
            });
        }

        team.BaseLineup = LineupEngine.CreateBaseLineup(team);
        team.PitchingPlan = PitchingRotationEngine.CreatePitchingPlan(team, Math.Clamp(pitcherCount, 3, 5));
        return team;
    }

    public static LeagueFile CreateLeague(params Team[] teams)
    {
        return new LeagueFile
        {
            Name = "Regression Dynasty",
            Structure = new LeagueStructure(),
            Teams = teams.ToList(),
            Rules = new LeagueRules
            {
                Innings = 5,
                MercyRuleEnabled = false,
                ExtraInnings = true,
                ExtraInningRunnerOnSecond = true
            }
        };
    }

    public static GameResult Result(Team away, Team home, int awayScore, int homeScore)
    {
        return new GameResult
        {
            AwayTeamId = away.Id,
            HomeTeamId = home.Id,
            AwayCoachId = away.CoachId,
            HomeCoachId = home.CoachId,
            AwayScore = awayScore,
            HomeScore = homeScore
        };
    }
}

internal sealed class SequenceRandom : Random
{
    private readonly Queue<int> _values;

    public SequenceRandom(params int[] values)
    {
        _values = new Queue<int>(values ?? Array.Empty<int>());
    }

    public override int Next(int maxValue)
    {
        if (maxValue <= 0)
            return 0;
        int value = _values.Count == 0 ? maxValue - 1 : _values.Dequeue();
        return Math.Clamp(value, 0, maxValue - 1);
    }

    public override int Next(int minValue, int maxValue)
    {
        if (maxValue <= minValue)
            return minValue;
        return minValue + Next(maxValue - minValue);
    }

    public override double NextDouble()
        => Next(1_000_000) / 1_000_000.0;
}

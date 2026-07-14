using System;
using System.Linq;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class LineupEngineTests
{
    [Fact]
    public void BuildLineupCard_FillsMandatoryPositionsAndExcludesUnavailablePlayers()
    {
        var redshirt = Hitter("Redshirt Star", "1B", contact: 99, power: 99);
        redshirt.RedshirtActive = true;
        var injured = Hitter("Injured Star", "RF", contact: 99, power: 99);
        injured.InjuryStatus = PlayerInjuryStatus.Out;
        injured.InjuryGamesRemaining = 8;

        var team = new Team
        {
            City = "Test",
            Nickname = "Nine",
            Roster =
            {
                Pitcher("Ace"),
                Hitter("Catcher", "C", fielding: 82),
                Hitter("First Base", "1B", fielding: 80),
                Hitter("Second Base", "2B", fielding: 80),
                Hitter("Third Base", "3B", fielding: 80),
                Hitter("Shortstop", "SS", fielding: 86),
                Hitter("Left Field", "LF", fielding: 78),
                Hitter("Center Field", "CF", fielding: 88),
                Hitter("Right Field", "RF", fielding: 78),
                Hitter("Designated Hitter", "1B", contact: 88, power: 92),
                redshirt,
                injured
            }
        };

        var card = LineupEngine.BuildLineupCard(team);
        var usedIds = card.BattingOrder.Select(s => s.Player.Id)
            .Concat(card.DefensiveAssignments.Values.Select(p => p.Id))
            .ToHashSet();

        Assert.True(card.IsValid, card.Status);
        Assert.True(card.HasDesignatedHitter);
        Assert.Equal(9, card.BattingOrder.Count);
        Assert.All(new[] { "C", "P", "1B", "2B", "3B", "SS", "LF", "CF", "RF" }, position =>
            Assert.True(card.DefensiveAssignments.ContainsKey(position), "Missing " + position));
        Assert.DoesNotContain(redshirt.Id, usedIds);
        Assert.DoesNotContain(injured.Id, usedIds);
        Assert.DoesNotContain(card.StartingPitcher.Id, card.BattingOrder.Select(s => s.Player.Id));
    }

    [Fact]
    public void BuildLineupCard_RecalculatesWhenSavedLineupContainsUnavailablePlayer()
    {
        var team = new Team
        {
            Roster =
            {
                Pitcher("Ace"),
                Hitter("Catcher", "C"),
                Hitter("First Base", "1B"),
                Hitter("Second Base", "2B"),
                Hitter("Third Base", "3B"),
                Hitter("Shortstop", "SS"),
                Hitter("Left Field", "LF"),
                Hitter("Center Field", "CF"),
                Hitter("Right Field", "RF"),
                Hitter("Replacement", "RF", contact: 70, power: 70)
            }
        };
        team.BaseLineup = LineupEngine.CreateBaseLineup(team);
        var savedBatter = team.Roster.First(p => p.Name == "Right Field");
        savedBatter.RedshirtActive = true;

        var card = LineupEngine.BuildLineupCard(team);

        Assert.True(card.IsValid, card.Status);
        Assert.DoesNotContain(savedBatter.Id, card.BattingOrder.Select(s => s.Player.Id));
        Assert.DoesNotContain(savedBatter.Id, card.DefensiveAssignments.Values.Select(p => p.Id));
    }

    private static Player Pitcher(string name)
        => new()
        {
            Name = name,
            Role = PlayerRole.Pitcher,
            Positions = "P",
            Pitching = 90,
            Stamina = 88,
            Accuracy = 86,
            Fielding = 70
        };

    private static Player Hitter(string name, string positions, int contact = 65, int power = 55, int fielding = 70)
        => new()
        {
            Name = name,
            Role = PlayerRole.Batter,
            Positions = positions,
            Contact = contact,
            Power = power,
            Speed = 60,
            BaseRunning = 60,
            Fielding = fielding,
            ArmStrength = 60,
            Accuracy = 60,
            PopTime = positions == "C" ? 70 : 50,
            TagRating = 60
        };
}

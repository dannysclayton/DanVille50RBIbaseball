using System;
using System.Collections.Generic;
using System.Drawing;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class GameUniformResolverTests
{
    [Fact]
    public void ResolveUniform_UsesExplicitScheduledChoiceWhenItMatchesGameRole()
    {
        var team = TeamWithUniforms();
        team.UniformSets.RemoveAll(u => u.Category == TeamUniformCategory.VisitorAlternate);
        var visitorAlt = new TeamUniformSet
        {
            Category = TeamUniformCategory.VisitorAlternate,
            Name = "Visitor Black",
            JerseyArgb = Color.Black.ToArgb(),
            PantsArgb = Color.Gray.ToArgb(),
            CapHelmetArgb = Color.Black.ToArgb()
        };
        team.UniformSets.Add(visitorAlt);

        var scheduled = new ScheduledGame
        {
            GameNumber = 7,
            AwayUniformSetId = visitorAlt.Id
        };

        var selected = GameUniformResolver.ResolveUniform(team, homeRole: false, scheduled.AwayUniformSetId, scheduled);

        Assert.Same(visitorAlt, selected);
    }

    [Fact]
    public void ResolveUniform_IgnoresHomeUniformWhenTeamIsAway()
    {
        var team = TeamWithUniforms();
        var homeAlt = new TeamUniformSet
        {
            Category = TeamUniformCategory.HomeAlternate,
            Name = "Home Gold",
            JerseyArgb = Color.Gold.ToArgb(),
            PantsArgb = Color.White.ToArgb(),
            CapHelmetArgb = Color.Gold.ToArgb()
        };
        team.UniformSets.Add(homeAlt);

        var selected = GameUniformResolver.ResolveUniform(team, homeRole: false, homeAlt.Id, new ScheduledGame { GameNumber = 1 });

        Assert.Equal(TeamUniformCategory.Visitor, selected.Category);
    }

    [Fact]
    public void ResolveUniform_AutomaticSelectionRotatesInsideSelectedCategory()
    {
        var team = TeamWithUniforms();
        team.UniformSets.RemoveAll(u => u.Category == TeamUniformCategory.VisitorAlternate);
        var visitorAlt1 = new TeamUniformSet
        {
            Category = TeamUniformCategory.VisitorAlternate,
            Name = "Visitor Alt 1",
            JerseyArgb = Color.Navy.ToArgb(),
            PantsArgb = Color.LightGray.ToArgb(),
            CapHelmetArgb = Color.Navy.ToArgb()
        };
        var visitorAlt2 = new TeamUniformSet { Category = TeamUniformCategory.VisitorAlternate, Name = "Visitor Alt 2" };
        team.UniformSets.Add(visitorAlt1);
        team.UniformSets.Add(visitorAlt2);

        var opponent = Guid.NewGuid();
        var gameOne = new ScheduledGame { GameNumber = 1, AwayTeamId = team.Id, HomeTeamId = opponent, AwayUniformAutoCategory = TeamUniformCategory.VisitorAlternate };
        var gameTwo = new ScheduledGame { GameNumber = 2, AwayTeamId = team.Id, HomeTeamId = opponent, AwayUniformAutoCategory = TeamUniformCategory.VisitorAlternate };
        var gameThree = new ScheduledGame { GameNumber = 3, AwayTeamId = team.Id, HomeTeamId = opponent, AwayUniformAutoCategory = TeamUniformCategory.VisitorAlternate };
        var schedule = new List<ScheduledGame> { gameOne, gameTwo, gameThree };

        Assert.Same(visitorAlt1, GameUniformResolver.ResolveUniform(team, false, null, gameOne, schedule: schedule, autoCategory: TeamUniformCategory.VisitorAlternate));
        Assert.Same(visitorAlt2, GameUniformResolver.ResolveUniform(team, false, null, gameTwo, schedule: schedule, autoCategory: TeamUniformCategory.VisitorAlternate));
        Assert.Same(visitorAlt1, GameUniformResolver.ResolveUniform(team, false, null, gameThree, schedule: schedule, autoCategory: TeamUniformCategory.VisitorAlternate));
    }

    [Theory]
    [InlineData(TeamUniformCategory.Home, true)]
    [InlineData(TeamUniformCategory.HomeAlternate, true)]
    [InlineData(TeamUniformCategory.Visitor, false)]
    [InlineData(TeamUniformCategory.VisitorAlternate, false)]
    public void ResolveUniform_WithScheduleRotatesEveryUniformTypeByItsOwnSequence(TeamUniformCategory category, bool homeRole)
    {
        var team = TeamWithUniforms();
        team.UniformSets.RemoveAll(u => u.Category == category);
        var first = new TeamUniformSet { Category = category, Name = TeamUniformSet.CategoryLabel(category) + " 1" };
        var second = new TeamUniformSet { Category = category, Name = TeamUniformSet.CategoryLabel(category) + " 2" };
        team.UniformSets.Add(first);
        team.UniformSets.Add(second);
        var opponent = Guid.NewGuid();

        var gameOne = ScheduledUniformGame(team.Id, opponent, homeRole, 1, category);
        var oppositeRoleGame = ScheduledUniformGame(team.Id, opponent, !homeRole, 2, category);
        var gameTwo = ScheduledUniformGame(team.Id, opponent, homeRole, 5, category);
        var gameThree = ScheduledUniformGame(team.Id, opponent, homeRole, 9, category);
        var schedule = new List<ScheduledGame> { gameOne, oppositeRoleGame, gameTwo, gameThree };

        Assert.Same(first, GameUniformResolver.ResolveUniform(team, homeRole, null, gameOne, schedule: schedule, autoCategory: category));
        Assert.Same(second, GameUniformResolver.ResolveUniform(team, homeRole, null, gameTwo, schedule: schedule, autoCategory: category));
        Assert.Same(first, GameUniformResolver.ResolveUniform(team, homeRole, null, gameThree, schedule: schedule, autoCategory: category));
    }

    [Fact]
    public void ResolveUniform_WhenRotationDisabledUsesFirstPresentedUniform()
    {
        var team = TeamWithUniforms();
        var visitorAlt = new TeamUniformSet
        {
            Category = TeamUniformCategory.VisitorAlternate,
            Name = "Visitor Alt"
        };
        team.UniformSets.Add(visitorAlt);

        var selected = GameUniformResolver.ResolveUniform(
            team,
            homeRole: false,
            requestedUniformId: null,
            scheduled: new ScheduledGame { GameNumber = 12 },
            rotateSavedUniforms: false);

        Assert.Equal(TeamUniformCategory.Visitor, selected.Category);
        Assert.NotSame(visitorAlt, selected);
    }

    private static Team TeamWithUniforms()
    {
        var team = new Team
        {
            City = "Test",
            Nickname = "Uniforms",
            PrimaryArgb = Color.Blue.ToArgb(),
            SecondaryArgb = Color.Red.ToArgb()
        };
        team.EnsureDefaultUniformSets();
        return team;
    }

    private static ScheduledGame ScheduledUniformGame(Guid teamId, Guid opponentId, bool homeRole, int gameNumber, TeamUniformCategory category)
    {
        var game = new ScheduledGame { GameNumber = gameNumber };
        if (homeRole)
        {
            game.HomeTeamId = teamId;
            game.AwayTeamId = opponentId;
            game.HomeUniformAutoCategory = category;
        }
        else
        {
            game.AwayTeamId = teamId;
            game.HomeTeamId = opponentId;
            game.AwayUniformAutoCategory = category;
        }
        return game;
    }
}

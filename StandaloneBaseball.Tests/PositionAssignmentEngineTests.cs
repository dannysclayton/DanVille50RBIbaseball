using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class PositionAssignmentEngineTests
{
    [Fact]
    public void ApplyFieldingPenalty_UsesNoPenaltyMatrix()
    {
        var cornerInfielder = new Player { Positions = "1B", Fielding = 80 };
        var middleInfielder = new Player { Positions = "2B", Fielding = 80 };
        var outfielder = new Player { Positions = "LF", Fielding = 80 };
        var pitcher = new Player { Role = PlayerRole.Pitcher, Positions = "SP", Fielding = 80 };

        Assert.False(PositionAssignmentEngine.IsPenalizedFit(cornerInfielder, "3B"));
        Assert.Equal(80, PositionAssignmentEngine.ApplyFieldingPenalty(cornerInfielder, "3B", 80));
        Assert.False(PositionAssignmentEngine.IsPenalizedFit(middleInfielder, "SS"));
        Assert.False(PositionAssignmentEngine.IsPenalizedFit(outfielder, "RF"));
        Assert.False(PositionAssignmentEngine.IsPenalizedFit(pitcher, "P"));
    }

    [Fact]
    public void ApplyFieldingPenalty_ReducesUnqualifiedPositionByTwentyFivePercent()
    {
        var player = new Player { Positions = "1B", Fielding = 80 };

        Assert.True(PositionAssignmentEngine.IsPenalizedFit(player, "SS"));
        Assert.Equal(60, PositionAssignmentEngine.ApplyFieldingPenalty(player, "SS", 80));
    }
}

#nullable enable annotations

using System.Reflection;

namespace StandaloneBaseball.Tests;

public sealed class PlayStation3ControllerProfileTests
{
    [Theory]
    [InlineData(XInputButtons.A, GameplayInputCommand.BatSwing, GameplayInputCommand.PitchRelease, GameplayInputCommand.ThrowHome)]
    [InlineData(XInputButtons.B, GameplayInputCommand.BatContactSwing, GameplayInputCommand.ThrowFirst, GameplayInputCommand.SelectCurveball)]
    [InlineData(XInputButtons.X, GameplayInputCommand.BatPowerSwing, GameplayInputCommand.ThrowThird, GameplayInputCommand.SelectChangeup)]
    [InlineData(XInputButtons.Y, GameplayInputCommand.CallSacrificeBunt, GameplayInputCommand.ThrowSecond, GameplayInputCommand.SelectSlider)]
    public void FaceButtons_FollowPlayStationBaseballConventions(
        XInputButtons button,
        GameplayInputCommand first,
        GameplayInputCommand second,
        GameplayInputCommand third)
    {
        IReadOnlyList<GameplayInputCommand> commands = CommandsFor(button);

        Assert.Contains(first, commands);
        Assert.Contains(second, commands);
        Assert.Contains(third, commands);
    }

    [Fact]
    public void ShoulderTriggerAndSystemButtons_ExposePlayStationFunctions()
    {
        IReadOnlyList<GameplayInputCommand> commands = CommandsFor(
            XInputButtons.LeftShoulder | XInputButtons.RightShoulder | XInputButtons.Start |
            XInputButtons.Back | XInputButtons.RightThumb,
            leftTrigger: 100,
            rightTrigger: 100);

        Assert.Contains(GameplayInputCommand.AdvanceRunners, commands);
        Assert.Contains(GameplayInputCommand.RetreatRunners, commands);
        Assert.Contains(GameplayInputCommand.SelectSplitter, commands);
        Assert.Contains(GameplayInputCommand.CallSteal, commands);
        Assert.Contains(GameplayInputCommand.SelectForkball, commands);
        Assert.Contains(GameplayInputCommand.HoldRunners, commands);
        Assert.Contains(GameplayInputCommand.SelectKnuckleball, commands);
        Assert.Contains(GameplayInputCommand.TogglePause, commands);
        Assert.Contains(GameplayInputCommand.ToggleWatch, commands);
        Assert.Contains(GameplayInputCommand.ToggleCamera, commands);
    }

    [Fact]
    public void Profile_DocumentsEveryGameplayContext()
    {
        string[] contexts = PlayStation3ControllerProfile.Bindings.Select(binding => binding.Context).Distinct().ToArray();

        Assert.Contains("Menus", contexts);
        Assert.Contains("Batting", contexts);
        Assert.Contains("Pitching", contexts);
        Assert.Contains("Fielding", contexts);
        Assert.Contains("Baserunning", contexts);
        Assert.Contains("Game", contexts);
    }

    private static IReadOnlyList<GameplayInputCommand> CommandsFor(
        XInputButtons buttons,
        byte leftTrigger = 0,
        byte rightTrigger = 0)
    {
        var input = new XInputGameplayControllerInput();
        SetField(input, "_currentButtons", buttons);
        SetField(input, "_currentLeftTrigger", leftTrigger);
        SetField(input, "_currentRightTrigger", rightTrigger);
        var commands = new List<GameplayInputCommand>();
        MethodInfo method = typeof(XInputGameplayControllerInput).GetMethod(
            "AddCommandsForButtonEdges", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(input, new object[] { commands });
        return commands;
    }

    private static void SetField<T>(XInputGameplayControllerInput input, string name, T value)
    {
        typeof(XInputGameplayControllerInput).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(input, value);
    }
}

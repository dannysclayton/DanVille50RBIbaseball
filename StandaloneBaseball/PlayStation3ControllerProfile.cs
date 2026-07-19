using System.Collections.Generic;

namespace StandaloneBaseball
{
    internal sealed class PlayStation3ControlBinding
    {
        public PlayStation3ControlBinding(string context, string control, string action)
        {
            Context = context;
            Control = control;
            Action = action;
        }

        public string Context { get; }
        public string Control { get; }
        public string Action { get; }
    }

    internal static class PlayStation3ControllerProfile
    {
        public const string Name = "PlayStation 3";
        public const byte TriggerThreshold = 64;

        public static IReadOnlyList<PlayStation3ControlBinding> Bindings { get; } = new[]
        {
            new PlayStation3ControlBinding("Menus", "Directional buttons / Left Stick", "Move the menu selection"),
            new PlayStation3ControlBinding("Menus", "Cross / Start", "Accept or open the selected menu item"),
            new PlayStation3ControlBinding("Batting", "Cross", "Normal swing"),
            new PlayStation3ControlBinding("Batting", "Circle", "Contact swing"),
            new PlayStation3ControlBinding("Batting", "Square", "Power swing"),
            new PlayStation3ControlBinding("Batting", "Triangle", "Call a sacrifice bunt"),
            new PlayStation3ControlBinding("Pitching", "Left Stick", "Aim the pitch location"),
            new PlayStation3ControlBinding("Pitching", "Cross", "Deliver the selected pitch"),
            new PlayStation3ControlBinding("Pitching", "Directional Up / L3", "Select fastball"),
            new PlayStation3ControlBinding("Pitching", "Circle / Directional Left", "Select curveball"),
            new PlayStation3ControlBinding("Pitching", "Triangle / Directional Right", "Select slider"),
            new PlayStation3ControlBinding("Pitching", "Square / Directional Down", "Select changeup"),
            new PlayStation3ControlBinding("Pitching", "R1", "Select splitter"),
            new PlayStation3ControlBinding("Pitching", "L2", "Select forkball"),
            new PlayStation3ControlBinding("Pitching", "R2", "Select knuckleball"),
            new PlayStation3ControlBinding("Fielding", "Left Stick", "Move the highlighted fielder"),
            new PlayStation3ControlBinding("Fielding", "Circle", "Throw to first base"),
            new PlayStation3ControlBinding("Fielding", "Triangle", "Throw to second base"),
            new PlayStation3ControlBinding("Fielding", "Square", "Throw to third base"),
            new PlayStation3ControlBinding("Fielding", "Cross", "Throw home"),
            new PlayStation3ControlBinding("Baserunning", "L1", "Advance all runners or initiate the available advance"),
            new PlayStation3ControlBinding("Baserunning", "R1", "Return or hold runners"),
            new PlayStation3ControlBinding("Baserunning", "L2", "Call or attempt a steal"),
            new PlayStation3ControlBinding("Baserunning", "R2", "Stop runners"),
            new PlayStation3ControlBinding("Game", "Start", "Pause or resume"),
            new PlayStation3ControlBinding("Game", "Select", "Toggle user control / CPU watch mode"),
            new PlayStation3ControlBinding("Game", "R3", "Cycle the highlighted fielder or camera focus")
        };

        public static string Status(string? detectedDeviceName)
        {
            return string.IsNullOrWhiteSpace(detectedDeviceName)
                ? Name + " profile"
                : Name + " profile - " + detectedDeviceName;
        }
    }
}

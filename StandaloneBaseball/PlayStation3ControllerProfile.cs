#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StandaloneBaseball
{
    public enum PlayStationControllerGeneration
    {
        PlayStation3,
        PlayStation4,
        PlayStation5
    }

    internal sealed class PlayStationControlBinding
    {
        public PlayStationControlBinding(string context, string control, string action)
        {
            Context = context;
            Control = control;
            Action = action;
        }

        public string Context { get; }
        public string Control { get; }
        public string Action { get; }
    }

    internal sealed class PlayStationControllerDefinition
    {
        public PlayStationControllerDefinition(
            PlayStationControllerGeneration generation,
            string name,
            string pauseButton,
            string utilityButton,
            string connectionNote)
        {
            Generation = generation;
            Name = name;
            PauseButton = pauseButton;
            UtilityButton = utilityButton;
            ConnectionNote = connectionNote;
            Bindings = BuildBindings(pauseButton, utilityButton);
        }

        public PlayStationControllerGeneration Generation { get; }
        public string Name { get; }
        public string PauseButton { get; }
        public string UtilityButton { get; }
        public string ConnectionNote { get; }
        public IReadOnlyList<PlayStationControlBinding> Bindings { get; }
        public bool UsesModernNativeHidLayout => Generation != PlayStationControllerGeneration.PlayStation3;

        public string Status(string? detectedDeviceName)
        {
            return string.IsNullOrWhiteSpace(detectedDeviceName)
                ? Name + " profile"
                : Name + " profile - " + detectedDeviceName;
        }

        public override string ToString() => Name;

        private static IReadOnlyList<PlayStationControlBinding> BuildBindings(string pauseButton, string utilityButton)
        {
            return new[]
            {
                new PlayStationControlBinding("Menus", "Directional buttons / Left Stick", "Move the menu selection"),
                new PlayStationControlBinding("Menus", "Cross / " + pauseButton, "Accept or open the selected menu item"),
                new PlayStationControlBinding("Batting", "Cross", "Normal swing"),
                new PlayStationControlBinding("Batting", "Circle", "Contact swing"),
                new PlayStationControlBinding("Batting", "Square", "Power swing"),
                new PlayStationControlBinding("Batting", "Triangle", "Call a sacrifice bunt"),
                new PlayStationControlBinding("Pitching", "Left Stick", "Aim the pitch location"),
                new PlayStationControlBinding("Pitching", "Cross", "Deliver the selected pitch"),
                new PlayStationControlBinding("Pitching", "Directional Up / L3", "Select fastball"),
                new PlayStationControlBinding("Pitching", "Circle / Directional Left", "Select curveball"),
                new PlayStationControlBinding("Pitching", "Triangle / Directional Right", "Select slider"),
                new PlayStationControlBinding("Pitching", "Square / Directional Down", "Select changeup"),
                new PlayStationControlBinding("Pitching", "R1", "Select splitter"),
                new PlayStationControlBinding("Pitching", "L2", "Select forkball"),
                new PlayStationControlBinding("Pitching", "R2", "Select knuckleball"),
                new PlayStationControlBinding("Fielding", "Left Stick", "Move the highlighted fielder"),
                new PlayStationControlBinding("Fielding", "Circle", "Throw to first base"),
                new PlayStationControlBinding("Fielding", "Triangle", "Throw to second base"),
                new PlayStationControlBinding("Fielding", "Square", "Throw to third base"),
                new PlayStationControlBinding("Fielding", "Cross", "Throw home"),
                new PlayStationControlBinding("Baserunning", "L1", "Advance all runners or initiate the available advance"),
                new PlayStationControlBinding("Baserunning", "R1", "Return or hold runners"),
                new PlayStationControlBinding("Baserunning", "L2", "Call or attempt a steal"),
                new PlayStationControlBinding("Baserunning", "R2", "Stop runners"),
                new PlayStationControlBinding("Game", pauseButton, "Pause or resume"),
                new PlayStationControlBinding("Game", utilityButton, "Toggle user control / CPU watch mode"),
                new PlayStationControlBinding("Game", "R3", "Cycle the highlighted fielder or camera focus")
            };
        }
    }

    internal static class PlayStationControllerProfiles
    {
        public const byte TriggerThreshold = 64;

        private static readonly PlayStationControllerDefinition Ps3 = new PlayStationControllerDefinition(
            PlayStationControllerGeneration.PlayStation3,
            "PlayStation 3 (DualShock 3)",
            "Start",
            "Select",
            "DualShock 3 is supported through XInput compatibility software such as DsHidMini.");

        private static readonly PlayStationControllerDefinition Ps4 = new PlayStationControllerDefinition(
            PlayStationControllerGeneration.PlayStation4,
            "PlayStation 4 (DualShock 4)",
            "OPTIONS",
            "SHARE",
            "DualShock 4 works through XInput compatibility software or the native Windows joystick interface.");

        private static readonly PlayStationControllerDefinition Ps5 = new PlayStationControllerDefinition(
            PlayStationControllerGeneration.PlayStation5,
            "PlayStation 5 (DualSense)",
            "Options",
            "Create",
            "DualSense works through XInput compatibility software or the native Windows joystick interface.");

        public static IReadOnlyList<PlayStationControllerDefinition> All { get; } = new[] { Ps3, Ps4, Ps5 };
        public static PlayStationControllerDefinition Current => For(ControllerSettingsStore.Current.Profile);

        public static PlayStationControllerDefinition For(PlayStationControllerGeneration generation)
        {
            return generation switch
            {
                PlayStationControllerGeneration.PlayStation4 => Ps4,
                PlayStationControllerGeneration.PlayStation5 => Ps5,
                _ => Ps3
            };
        }
    }

    internal sealed class ControllerSettings
    {
        public PlayStationControllerGeneration Profile { get; set; } = PlayStationControllerGeneration.PlayStation3;

        public ControllerSettings Clone() => new ControllerSettings { Profile = Normalize(Profile) };

        internal void Normalize() => Profile = Normalize(Profile);

        internal static PlayStationControllerGeneration Normalize(PlayStationControllerGeneration profile)
        {
            return Enum.IsDefined(typeof(PlayStationControllerGeneration), profile)
                ? profile
                : PlayStationControllerGeneration.PlayStation3;
        }
    }

    internal static class ControllerSettingsStore
    {
        private static readonly object Sync = new object();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private static ControllerSettings? _current;

        public static string SettingsPath => Path.Combine(UserDataPaths.RootDirectory, "Settings", "controller-settings.json");

        public static ControllerSettings Current
        {
            get
            {
                lock (Sync)
                    return _current ??= LoadFromFile(SettingsPath);
            }
        }

        public static void Save(ControllerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ControllerSettings normalized = settings.Clone();
            SaveToFile(SettingsPath, normalized);
            lock (Sync)
                _current = normalized;
        }

        internal static ControllerSettings LoadFromFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return new ControllerSettings();
                ControllerSettings? settings = JsonSerializer.Deserialize<ControllerSettings>(File.ReadAllText(path), JsonOptions);
                settings ??= new ControllerSettings();
                settings.Normalize();
                return settings;
            }
            catch
            {
                return new ControllerSettings();
            }
        }

        internal static void SaveToFile(string path, ControllerSettings settings)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            string temporary = fullPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporary, fullPath, overwrite: true);
        }

        internal static void ResetCacheForTests()
        {
            lock (Sync)
                _current = null;
        }
    }

    internal static class PlayStation3ControllerProfile
    {
        public const byte TriggerThreshold = PlayStationControllerProfiles.TriggerThreshold;
        public static string Name => PlayStationControllerProfiles.For(PlayStationControllerGeneration.PlayStation3).Name;
        public static IReadOnlyList<PlayStationControlBinding> Bindings =>
            PlayStationControllerProfiles.For(PlayStationControllerGeneration.PlayStation3).Bindings;
        public static string Status(string? detectedDeviceName) =>
            PlayStationControllerProfiles.For(PlayStationControllerGeneration.PlayStation3).Status(detectedDeviceName);
    }
}

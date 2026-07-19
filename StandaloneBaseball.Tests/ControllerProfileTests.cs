#nullable enable annotations

namespace StandaloneBaseball.Tests;

public sealed class ControllerProfileTests
{
    [Fact]
    public void MissingOrInvalidSettings_DefaultToPlayStation3()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string missing = Path.Combine(directory, "missing.json");
            Assert.Equal(PlayStationControllerGeneration.PlayStation3,
                ControllerSettingsStore.LoadFromFile(missing).Profile);

            string invalid = Path.Combine(directory, "invalid.json");
            File.WriteAllText(invalid, "{ \"Profile\": 999 }");
            Assert.Equal(PlayStationControllerGeneration.PlayStation3,
                ControllerSettingsStore.LoadFromFile(invalid).Profile);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(PlayStationControllerGeneration.PlayStation3, "Start", "Select")]
    [InlineData(PlayStationControllerGeneration.PlayStation4, "OPTIONS", "SHARE")]
    [InlineData(PlayStationControllerGeneration.PlayStation5, "Options", "Create")]
    public void Profiles_UseGenerationSpecificSystemButtonNames(
        PlayStationControllerGeneration generation,
        string pauseButton,
        string utilityButton)
    {
        PlayStationControllerDefinition profile = PlayStationControllerProfiles.For(generation);

        Assert.Equal(pauseButton, profile.PauseButton);
        Assert.Equal(utilityButton, profile.UtilityButton);
        Assert.Contains(profile.Bindings, binding => binding.Context == "Game" && binding.Control == pauseButton);
        Assert.Contains(profile.Bindings, binding => binding.Context == "Game" && binding.Control == utilityButton);
    }

    [Theory]
    [InlineData(PlayStationControllerGeneration.PlayStation4)]
    [InlineData(PlayStationControllerGeneration.PlayStation5)]
    public void ModernNativeLayout_TranslatesPlayStationFaceAndSystemButtons(
        PlayStationControllerGeneration generation)
    {
        XInputButtons mapped = LegacyJoystickController.MapButtons(
            0x0001 | 0x0002 | 0x0004 | 0x0008 | 0x0100 | 0x0200 | 0x0400 | 0x0800,
            0x0000FFFF,
            generation);

        Assert.True(mapped.HasFlag(XInputButtons.X)); // Square
        Assert.True(mapped.HasFlag(XInputButtons.A)); // Cross
        Assert.True(mapped.HasFlag(XInputButtons.B)); // Circle
        Assert.True(mapped.HasFlag(XInputButtons.Y)); // Triangle
        Assert.True(mapped.HasFlag(XInputButtons.Back)); // SHARE / Create
        Assert.True(mapped.HasFlag(XInputButtons.Start)); // OPTIONS / Options
        Assert.True(mapped.HasFlag(XInputButtons.LeftThumb));
        Assert.True(mapped.HasFlag(XInputButtons.RightThumb));
    }

    [Theory]
    [InlineData(PlayStationControllerGeneration.PlayStation4)]
    [InlineData(PlayStationControllerGeneration.PlayStation5)]
    public void ModernNativeLayout_ExposesL2AndR2AsTriggers(PlayStationControllerGeneration generation)
    {
        Assert.Equal(byte.MaxValue, LegacyJoystickController.TriggerValue(0x0040, 0x0040, generation));
        Assert.Equal(byte.MaxValue, LegacyJoystickController.TriggerValue(0x0080, 0x0080, generation));
        Assert.Equal(0, LegacyJoystickController.TriggerValue(0, 0x0040, generation));
    }

    [Theory]
    [InlineData(PlayStationControllerGeneration.PlayStation4)]
    [InlineData(PlayStationControllerGeneration.PlayStation5)]
    public void SelectedProfile_RoundTripsThroughSettingsFile(PlayStationControllerGeneration generation)
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "controller-settings.json");
            ControllerSettingsStore.SaveToFile(path, new ControllerSettings { Profile = generation });

            Assert.Equal(generation, ControllerSettingsStore.LoadFromFile(path).Profile);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DanVille-controller-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

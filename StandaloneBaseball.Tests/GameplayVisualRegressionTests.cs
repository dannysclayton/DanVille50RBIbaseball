#nullable enable annotations

using System.Drawing;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class GameplayVisualRegressionTests
{
    [Fact]
    public void GameplaySurface_GeneratesUniformSpriteWhenNoSpriteAssetWasAssigned()
    {
        WinFormsTestHost.Run(() =>
        {
            Team away = TeamWithRoster("Away", Color.Cyan, Color.White, 11);
            Team home = TeamWithRoster("Home", Color.Magenta, Color.Gold, 22);
            var state = new GameplayRenderingGameState();
            state.SetTeams(away, home);

            using var surface = new GameplayRenderingSurface { Size = new Size(1000, 700) };
            surface.SetState(state);
            Player player = state.Fielders.First(marker => marker.Player != null).Player!;
            MethodInfo method = typeof(GameplayRenderingSurface).GetMethod(
                "LoadSpriteSheet", BindingFlags.Instance | BindingFlags.NonPublic)!;

            Image image = Assert.IsAssignableFrom<Image>(method.Invoke(surface, new object?[] { player, home }));
            Assert.Equal(SpriteSheetGeneratorOptions.FrameWidth * SpriteSheetGeneratorOptions.Columns, image.Width);
            Assert.Equal(SpriteSheetGeneratorOptions.FrameHeight * SpriteSheetGeneratorOptions.Rows, image.Height);
        });
    }

    [Fact]
    public void GameplaySurface_InvalidAssignedSpriteFallsBackToGeneratedUniformSprite()
    {
        WinFormsTestHost.Run(() =>
        {
            Team home = TeamWithRoster("Home", Color.Navy, Color.Gold, 23);
            var state = new GameplayRenderingGameState();
            state.SetTeams(TeamWithRoster("Away", Color.Red, Color.White, 12), home);
            Player player = state.Fielders.First(marker => marker.Player != null).Player!;
            player.SpriteSheetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");

            using var surface = new GameplayRenderingSurface { Size = new Size(1000, 700) };
            MethodInfo method = typeof(GameplayRenderingSurface).GetMethod(
                "LoadSpriteSheet", BindingFlags.Instance | BindingFlags.NonPublic)!;

            Image image = Assert.IsAssignableFrom<Image>(method.Invoke(surface, new object?[] { player, home }));
            Assert.Equal(SpriteSheetGeneratorOptions.FrameWidth * SpriteSheetGeneratorOptions.Columns, image.Width);
            Assert.Equal(SpriteSheetGeneratorOptions.FrameHeight * SpriteSheetGeneratorOptions.Rows, image.Height);
        });
    }

    [Fact]
    public void GameplayCamera_UsesDistinctAtBatTrackingAndThrowViews()
    {
        Team away = TeamWithRoster("Away", Color.Blue, Color.White, 31);
        Team home = TeamWithRoster("Home", Color.Red, Color.White, 32);
        var state = new GameplayRenderingGameState();
        state.SetTeams(away, home);

        state.CameraPhase = GameplayCameraPhase.AtBat;
        RectangleF atBat = GameplayRenderingSurface.CameraViewportFor(state);
        state.CameraPhase = GameplayCameraPhase.BallTracking;
        state.BallPosition = new PointF(0.18f, 0.24f);
        state.ActiveFielderIndex = 0;
        state.Fielders[0].Position = new PointF(0.22f, 0.28f);
        RectangleF tracking = GameplayRenderingSurface.CameraViewportFor(state);
        state.CameraPhase = GameplayCameraPhase.ThrowToBase;
        state.ThrowTarget = new PointF(0.64f, 0.72f);
        RectangleF throwing = GameplayRenderingSurface.CameraViewportFor(state);

        Assert.NotEqual(atBat, tracking);
        Assert.True(throwing.Width < tracking.Width);
        Assert.True(throwing.Height < tracking.Height);
        Assert.True(tracking.Contains(state.BallPosition));
    }

    [Fact]
    public void ThreeDimensionalPayload_ContainsLiveMatchupCameraAndFielders()
    {
        Team away = TeamWithRoster("Away", Color.Blue, Color.White, 81);
        Team home = TeamWithRoster("Home", Color.Red, Color.Gold, 82);
        var state = new GameplayRenderingGameState();
        state.SetTeams(away, home);
        state.CameraPhase = GameplayCameraPhase.AtBat;
        state.PitchTypeLabel = "Curveball";
        state.BatterTargetBase = 2;
        state.PresentationKind = GameplayPresentationKind.Steal;
        state.PresentationProgress = 0.67f;
        state.PresentationFromBase = 1;
        state.PresentationTargetBase = 2;
        state.PresentationSuccessful = true;
        state.PresentationVariant = "Safe";
        state.FieldPreset = new BaseballFieldPreset
        {
            Name = "Regression Field",
            GrassColor = Color.LimeGreen,
            DarkGrassColor = Color.DarkGreen,
            InfieldColor = Color.SandyBrown,
            ClayColor = Color.Sienna,
            WallColor = Color.Teal,
            SeatColor = Color.Navy,
            StructureColor = Color.Gray,
            AccentColor = Color.Gold
        };
        home.ScoreboardTemplate.Enabled = true;
        home.ScoreboardTemplate.SchoolNameText = "Regression School";
        home.ScoreboardTemplate.PreferredAbbreviation = "RGT";
        home.ScoreboardTemplate.MascotText = "Testers";
        home.ScoreboardTemplate.BoardColorLayout = ScoreboardBoardColorLayout.Quarters;
        home.ScoreboardTemplate.Ads = new List<string> { "FIRST AD", "SECOND AD" };

        using JsonDocument document = JsonDocument.Parse(GameplayRenderingSurface.BuildThreeDimensionalStatePayload(
            state, "data:image/png;base64,TESTLOGO", "data:image/jpeg;base64,TESTBOARD"));
        JsonElement root = document.RootElement;
        Assert.Equal("AtBat", root.GetProperty("cameraPhase").GetString());
        Assert.Equal("Curveball", root.GetProperty("pitchType").GetString());
        Assert.Equal(2, root.GetProperty("batterTargetBase").GetInt32());
        Assert.Equal("Steal", root.GetProperty("presentationKind").GetString());
        Assert.Equal(1, root.GetProperty("presentationFromBase").GetInt32());
        Assert.Equal(2, root.GetProperty("presentationTargetBase").GetInt32());
        Assert.True(root.GetProperty("presentationSuccessful").GetBoolean());
        Assert.Equal("Safe", root.GetProperty("presentationVariant").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("pitcherThrows").GetString()));
        Assert.Equal(9, root.GetProperty("fielders").GetArrayLength());
        Assert.True(root.GetProperty("fielders")[0].TryGetProperty("throws", out _));
        Assert.StartsWith("#", root.GetProperty("offensePrimary").GetString());
        Assert.Equal("Regression Field", root.GetProperty("field").GetProperty("name").GetString());
        Assert.Equal("#32CD32", root.GetProperty("field").GetProperty("grass").GetString());
        Assert.True(root.GetProperty("scoreboard").GetProperty("enabled").GetBoolean());
        Assert.Equal("Quarters", root.GetProperty("scoreboard").GetProperty("layout").GetString());
        Assert.Equal("RGT", root.GetProperty("scoreboard").GetProperty("abbreviation").GetString());
        Assert.Equal(2, root.GetProperty("scoreboard").GetProperty("ads").GetArrayLength());
        Assert.Equal("data:image/png;base64,TESTLOGO", root.GetProperty("scoreboard").GetProperty("logoDataUri").GetString());
        Assert.Equal("data:image/jpeg;base64,TESTBOARD", root.GetProperty("scoreboard").GetProperty("backgroundDataUri").GetString());
    }

    [Fact]
    public void ThreeDimensionalRendererAssets_AreOfflineAndComplete()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Assets", "Gameplay3D");
        string html = File.ReadAllText(Path.Combine(folder, "index.html"));
        string script = File.ReadAllText(Path.Combine(folder, "gameplay3d.js"));
        string rig = File.ReadAllText(Path.Combine(folder, "baseball-character-rig.js"));

        Assert.True(File.Exists(Path.Combine(folder, "three.module.min.js")));
        Assert.True(File.Exists(Path.Combine(folder, "three.core.min.js")));
        Assert.True(File.Exists(Path.Combine(folder, "addons", "loaders", "GLTFLoader.js")));
        Assert.True(File.Exists(Path.Combine(folder, "addons", "utils", "SkeletonUtils.js")));
        Assert.True(File.Exists(Path.Combine(folder, "models", "player_base.glb")));
        Assert.True(File.Exists(Path.Combine(folder, "models", "player_run.glb")));
        Assert.True(File.Exists(Path.Combine(folder, "models", "player_walk.glb")));
        Assert.Contains("THREE.Skeleton", rig);
        Assert.Contains("THREE.SkinnedMesh", rig);
        Assert.Contains("THREE.AnimationMixer", rig);
        Assert.Contains("BatterIdle_R", rig);
        Assert.Contains("Swing_L", rig);
        Assert.Contains("Pitch_R", rig);
        Assert.Contains("CatcherCrouch", rig);
        Assert.Contains("CatcherPopThrow_R", rig);
        Assert.Contains("CatcherReceive", rig);
        Assert.Contains("UmpireStrikeout", rig);
        Assert.Contains("StrikeoutReaction_R", rig);
        Assert.Contains("PitcherStrikeoutReset", rig);
        Assert.Contains("SweepTag", rig);
        Assert.Contains("RunnerLead", rig);
        Assert.Contains("PerspectiveCamera", script);
        Assert.Contains("preserveDrawingBuffer: true", script);
        Assert.Contains("initializeBaseballCharacterAssets", script);
        Assert.Contains("SkeletonUtils.js", rig);
        Assert.Contains("player_base.glb", rig);
        Assert.Contains("player_run.glb", rig);
        Assert.Contains("player_walk.glb", rig);
        Assert.Contains("mirrorImportedClip", rig);
        Assert.Contains("meshy-mirrored", rig);
        Assert.Contains("applyFieldPreset", script);
        Assert.Contains("applyScoreboard", script);
        Assert.Contains("scoreboardBackground", script);
        Assert.Contains("presentationKind", script);
        Assert.Contains("id=\"boardIdentity\"", html);
        Assert.Contains("id=\"boardLogo\"", html);
        Assert.Contains("id=\"venue\"", html);
        Assert.Contains("type=\"importmap\"", html);
        Assert.Contains("@media (max-width: 900px)", html);
        Assert.Contains("@media (max-width: 620px)", html);
        Assert.DoesNotContain("http://", html + script + rig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html + script + rig, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MeshyRuntimeAssets_ContainSkinMaterialsAndIndependentAnimationClips()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Assets", "Gameplay3D", "models");
        using JsonDocument baseModel = ReadGlbJson(Path.Combine(folder, "player_base.glb"));
        using JsonDocument runModel = ReadGlbJson(Path.Combine(folder, "player_run.glb"));
        using JsonDocument walkModel = ReadGlbJson(Path.Combine(folder, "player_walk.glb"));

        JsonElement root = baseModel.RootElement;
        Assert.True(root.GetProperty("meshes").GetArrayLength() >= 1);
        Assert.True(root.GetProperty("skins").GetArrayLength() >= 1);
        string[] materials = root.GetProperty("materials").EnumerateArray()
            .Select(value => value.GetProperty("name").GetString() ?? "")
            .ToArray();
        Assert.Contains("DRBI_Jersey", materials);
        Assert.Contains("DRBI_Pants", materials);
        Assert.Contains("DRBI_Cap", materials);
        Assert.Contains("DRBI_Accent", materials);
        Assert.Contains("DRBI_SkinDetail", materials);
        Assert.Contains("Pitch_R", AnimationNames(root));
        Assert.Contains("Run", AnimationNames(runModel.RootElement));
        Assert.Contains("Walk", AnimationNames(walkModel.RootElement));
    }

    [Fact]
    public void GameplayProjection_MapsCameraViewportToStableStageCoordinates()
    {
        var stage = new Rectangle(20, 100, 960, 560);
        var viewport = new RectangleF(0.14f, 0.42f, 0.72f, 0.53f);
        Rectangle field = GameplayRenderingSurface.ProjectFieldBounds(stage, viewport);

        float homeX = field.Left + field.Width * 0.5f;
        float homeY = field.Top + field.Height * 0.86f;
        Assert.InRange(homeX, stage.Left, stage.Right);
        Assert.InRange(homeY, stage.Top, stage.Bottom);
    }

    [Fact]
    public void RunnerPath_VisitsEachBaseForExtraBaseHits()
    {
        PointF first = GameplayRenderingSurface.RunnerPathPoint(1f, 1);
        PointF second = GameplayRenderingSurface.RunnerPathPoint(1f, 2);
        PointF third = GameplayRenderingSurface.RunnerPathPoint(1f, 3);
        PointF home = GameplayRenderingSurface.RunnerPathPoint(1f, 4);

        Assert.InRange(first.X, 0.63f, 0.65f);
        Assert.InRange(second.Y, 0.57f, 0.59f);
        Assert.InRange(third.X, 0.35f, 0.37f);
        Assert.InRange(home.Y, 0.85f, 0.87f);
    }

    [Fact]
    public void BallFlights_SeparateGroundLineFlyAndThrowArcs()
    {
        float ground = GameplayForm.BallHeightForFlight(GameplayBallFlightType.GroundBall, 0.5f);
        float line = GameplayForm.BallHeightForFlight(GameplayBallFlightType.LineDrive, 0.5f);
        float fly = GameplayForm.BallHeightForFlight(GameplayBallFlightType.FlyBall, 0.5f);
        float thrown = GameplayForm.BallHeightForFlight(GameplayBallFlightType.Throw, 0.5f);

        Assert.True(ground < line);
        Assert.True(line < fly);
        Assert.True(thrown > ground);
        Assert.InRange(GameplayForm.BallHeightForFlight(GameplayBallFlightType.FlyBall, 0f), 0f, 0.0001f);
        Assert.InRange(GameplayForm.BallHeightForFlight(GameplayBallFlightType.FlyBall, 1f), 0f, 0.0001f);
    }

    [Fact]
    public void PhysicalTravelProgress_IsMonotonicAndPreservesEndpoints()
    {
        Assert.Equal(0f, GameplayForm.PhysicalTravelProgress(0f, 0.08f));
        Assert.Equal(1f, GameplayForm.PhysicalTravelProgress(1f, 0.08f));

        float previous = 0f;
        for (int index = 1; index <= 20; index++)
        {
            float current = GameplayForm.PhysicalTravelProgress(index / 20f, 0.08f);
            Assert.True(current > previous);
            previous = current;
        }
    }

    [Fact]
    public void GameplaySurface_DrawsCurrentBatterInOffenseUniform()
    {
        WinFormsTestHost.Run(() =>
        {
            Team away = TeamWithRoster("Away", Color.Cyan, Color.White, 33);
            Team home = TeamWithRoster("Home", Color.Magenta, Color.Gold, 44);
            var state = new GameplayRenderingGameState();
            state.SetTeams(away, home);
            state.Phase = GameplayRenderingPhase.Ready;

            using var surface = new GameplayRenderingSurface { Size = new Size(1000, 700) };
            surface.SetState(state);
            using var bitmap = new Bitmap(surface.Width, surface.Height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            MethodInfo onPaint = typeof(GameplayRenderingSurface).GetMethod(
                "OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!;
            using var args = new PaintEventArgs(graphics, surface.ClientRectangle);
            onPaint.Invoke(surface, new object[] { args });

            string? snapshotPath = Environment.GetEnvironmentVariable("GAMEPLAY_SNAPSHOT_PATH");
            if (!string.IsNullOrWhiteSpace(snapshotPath))
                bitmap.Save(snapshotPath);

            int offensePixels = CountNearColor(bitmap, new Rectangle(410, 490, 125, 150), Color.Cyan, 35);
            Assert.True(offensePixels > 20, "The current batter should be visible near home plate in the batting team's colors.");
        });
    }

    [Fact]
    public void GameplaySurface_RendersBallTrackingViewWithAVisibleActiveFielder()
    {
        WinFormsTestHost.Run(() =>
        {
            Team away = TeamWithRoster("Away", Color.Cyan, Color.White, 73);
            Team home = TeamWithRoster("Home", Color.Magenta, Color.Gold, 74);
            var state = new GameplayRenderingGameState();
            state.SetTeams(away, home);
            state.Phase = GameplayRenderingPhase.BallInPlay;
            state.CameraPhase = GameplayCameraPhase.BallTracking;
            state.ActiveFielderIndex = 6;
            state.Fielders[6].Position = new PointF(0.24f, 0.37f);
            state.BallPosition = new PointF(0.20f, 0.31f);
            state.CameraFocus = state.BallPosition;
            state.BallFlightType = GameplayBallFlightType.FlyBall;
            state.BallHeight = 0.25f;
            state.BallTrail = 0.5f;

            using var surface = new GameplayRenderingSurface { Size = new Size(1280, 720) };
            surface.SetState(state);
            using var bitmap = new Bitmap(surface.Width, surface.Height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            MethodInfo onPaint = typeof(GameplayRenderingSurface).GetMethod(
                "OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!;
            using var args = new PaintEventArgs(graphics, surface.ClientRectangle);
            onPaint.Invoke(surface, new object[] { args });

            string? snapshotPath = Environment.GetEnvironmentVariable("GAMEPLAY_TRACKING_SNAPSHOT_PATH");
            if (!string.IsNullOrWhiteSpace(snapshotPath))
                bitmap.Save(snapshotPath);
            Assert.True(CountNearColor(bitmap, surface.ClientRectangle, Color.Magenta, 35) > 20);
        });
    }

    [Fact]
    public void GameplayForm_ProvidesVisiblePrimaryGameplayControlAndPlayableTiming()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new GameplayForm(
                TeamWithRoster("Away", Color.Blue, Color.White, 55),
                TeamWithRoster("Home", Color.Red, Color.White, 66));

            Assert.Contains(Descendants(form).OfType<Button>(), button => button.Text == "Pitch / Swing");
            Assert.Contains(Descendants(form).OfType<Label>(), label => label.Text == "Game controls");

            float pitchStep = (float)typeof(GameplayForm).GetField(
                "PitchProgressPerTick", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
            float battedBallStep = (float)typeof(GameplayForm).GetField(
                "BallInPlayProgressPerTick", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
            Assert.True(pitchStep <= 0.02f, "A pitch must remain visible long enough for a user-controlled swing.");
            Assert.True(battedBallStep <= 0.015f, "A batted ball must remain visible long enough to follow the play.");
        });
    }

    private static Team TeamWithRoster(string name, Color primary, Color secondary, int seed)
    {
        var team = new Team
        {
            City = name,
            Nickname = "Club",
            ScoreboardAbbreviation = name.Substring(0, Math.Min(3, name.Length)).ToUpperInvariant(),
            PrimaryArgb = primary.ToArgb(),
            SecondaryArgb = secondary.ToArgb()
        };
        Simulator.FillRandomRoster(team, new Random(seed));
        foreach (Player player in team.Roster)
            player.SpriteSheetPath = "";
        team.SpriteSheetPath = "";
        return team;
    }

    private static JsonDocument ReadGlbJson(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 20);
        Assert.Equal(0x46546C67u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 4));
        int jsonLength = checked((int)BitConverter.ToUInt32(bytes, 12));
        Assert.Equal(0x4E4F534Au, BitConverter.ToUInt32(bytes, 16));
        string json = Encoding.UTF8.GetString(bytes, 20, jsonLength).TrimEnd('\0', ' ', '\t', '\r', '\n');
        return JsonDocument.Parse(json);
    }

    private static string[] AnimationNames(JsonElement root) =>
        root.GetProperty("animations").EnumerateArray()
            .Select(value => value.GetProperty("name").GetString() ?? "")
            .ToArray();

    private static int CountNearColor(Bitmap image, Rectangle region, Color target, int tolerance)
    {
        int count = 0;
        Rectangle bounds = Rectangle.Intersect(region, new Rectangle(Point.Empty, image.Size));
        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                Color pixel = image.GetPixel(x, y);
                if (Math.Abs(pixel.R - target.R) <= tolerance &&
                    Math.Abs(pixel.G - target.G) <= tolerance &&
                    Math.Abs(pixel.B - target.B) <= tolerance)
                    count++;
            }
        }
        return count;
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child))
                yield return descendant;
        }
    }
}

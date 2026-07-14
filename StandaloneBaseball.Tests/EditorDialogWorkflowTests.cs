using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class EditorDialogWorkflowTests
{
    [Fact]
    public void DynastySetup_BuildsRulesFromControlsAndDisablesRunnerRuleWithExtraInnings()
    {
        WinFormsTestHost.Run(() =>
        {
            var original = new LeagueRules
            {
                Innings = 7,
                ExtraInnings = true,
                ExtraInningRunnerOnSecond = true,
                Schedule = new SeasonScheduleRules
                {
                    SeriesLength = 4,
                    DistrictHomeGames = 6,
                    DistrictAwayGames = 6
                }
            };
            using var dialog = new DynastySetupDialog(original, "  Summer League  ", "  Pat Owner  ", " C:\\Assets ");
            var extraInnings = WinFormsTestHost.Field<CheckBox>(dialog, "_extraInningsBox");
            var extraRunner = WinFormsTestHost.Field<CheckBox>(dialog, "_extraRunnerBox");

            Assert.Equal("Summer League", WinFormsTestHost.Field<TextBox>(dialog, "_nameBox").Text);
            Assert.Equal("Pat Owner", WinFormsTestHost.Field<TextBox>(dialog, "_ownerNameBox").Text);
            Assert.True(extraRunner.Enabled);

            extraInnings.Checked = false;
            var built = Assert.IsType<LeagueRules>(WinFormsTestHost.Invoke(dialog, "BuildRules"));

            Assert.False(extraRunner.Enabled);
            Assert.False(built.ExtraInnings);
            Assert.False(built.ExtraInningRunnerOnSecond);
            Assert.Equal(7, built.Innings);
            Assert.Equal(4, built.Schedule.SeriesLength);
            Assert.Equal(6, built.Schedule.DistrictHomeGames);
        });
    }

    [Fact]
    public void DynastySetup_RejectsUnbalancedScheduleAndNormalizesNames()
    {
        var schedule = new SeasonScheduleRules { ConferenceHomeGames = 4, ConferenceAwayGames = 3 };
        var arguments = new object[] { schedule, null };

        Assert.False((bool)WinFormsTestHost.InvokeStatic(typeof(DynastySetupDialog), "ValidateScheduleRules", arguments));
        Assert.Contains("Conference", Assert.IsType<string>(arguments[1]));
        Assert.Equal("New Baseball Dynasty", WinFormsTestHost.InvokeStatic(typeof(DynastySetupDialog), "NormalizeName", "   "));
        Assert.Equal("Casey Manager", WinFormsTestHost.InvokeStatic(typeof(DynastySetupDialog), "NormalizeOwnerName", "  Casey Manager  "));
    }

    [Fact]
    public void CutsceneEditor_FiltersClonesAndNormalizesSavedRows()
    {
        WinFormsTestHost.Run(() =>
        {
            var source = new CutsceneDefinition
            {
                Id = Guid.Empty,
                Name = "   ",
                Trigger = CutsceneTrigger.HomeRun,
                UniformFolder = (TeamCutsceneUniformFolder)999,
                MediaPath = null,
                DurationSeconds = 500
            };
            var excluded = new CutsceneDefinition { Name = "Anthem", Trigger = CutsceneTrigger.NationalAnthem };
            using var dialog = new CutsceneEditorDialog(
                new[] { source, excluded },
                Path.GetTempPath(),
                Path.GetTempPath(),
                allowedTriggers: new[] { CutsceneTrigger.HomeRun },
                uniformFolders: new[] { TeamCutsceneUniformFolder.Home });

            Assert.Single(dialog.Cutscenes);
            Assert.NotSame(source, dialog.Cutscenes[0]);
            var grid = WinFormsTestHost.Field<DataGridView>(dialog, "_grid");
            Assert.Equal(6, grid.Columns.Count);

            dialog.DialogResult = DialogResult.OK;
            WinFormsTestHost.Invoke(dialog, "OnFormClosing", new FormClosingEventArgs(CloseReason.None, false));
            var saved = Assert.Single(dialog.Cutscenes);

            Assert.NotEqual(Guid.Empty, saved.Id);
            Assert.Equal("HomeRun", saved.Name);
            Assert.Equal(TeamCutsceneUniformFolder.Any, saved.UniformFolder);
            Assert.Equal("", saved.MediaPath);
            Assert.Equal(120, saved.DurationSeconds);
            Assert.Equal(Guid.Empty, source.Id);
        });
    }

    [Fact]
    public void FieldEditor_NewAndDuplicatePreserveIndependentFieldState()
    {
        WinFormsTestHost.Run(() =>
        {
            var league = new LeagueFile();
            league.CustomFields.Clear();
            var type = typeof(MainForm).Assembly.GetType("StandaloneBaseball.FieldEditorDialog", throwOnError: true);
            using var dialog = (Form)Activator.CreateInstance(type, league, null, null, Path.GetTempPath());

            WinFormsTestHost.Invoke(dialog, "NewField");
            Assert.Single(league.CustomFields);
            league.CustomFields[0].Name = "Riverside Park";
            league.CustomFields[0].Overlays.Add(new FieldImageOverlay { Name = "Press Box", X = 0.25f });

            WinFormsTestHost.Invoke(dialog, "DuplicateField");

            Assert.Equal(2, league.CustomFields.Count);
            var copy = league.CustomFields.Single(field => field.Name.EndsWith(" Copy", StringComparison.Ordinal));
            Assert.NotEqual(league.CustomFields[0].Id, copy.Id);
            Assert.NotSame(league.CustomFields[0].Overlays[0], copy.Overlays[0]);
            copy.Overlays[0].X = 0.75f;
            Assert.Equal(0.25f, league.CustomFields[0].Overlays[0].X);
            Assert.True((bool)type.GetProperty("Modified")!.GetValue(dialog));
        });
    }

    [Fact]
    public void FieldEditor_ExportsPortablePackageWithDeduplicatedAssets()
    {
        string directory = Path.Combine(Path.GetTempPath(), "field-editor-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string asset = Path.Combine(directory, "logo.png");
            File.WriteAllBytes(asset, new byte[] { 1, 2, 3, 4 });
            string package = Path.Combine(directory, "field.dbfield");
            var field = new CustomBaseballField
            {
                Name = "Test Field",
                BackgroundAssetPath = asset,
                Overlays = new List<FieldImageOverlay>
                {
                    new FieldImageOverlay { Name = "Logo", AssetPath = asset }
                }
            };
            var type = typeof(MainForm).Assembly.GetType("StandaloneBaseball.FieldEditorDialog", throwOnError: true);

            WinFormsTestHost.InvokeStatic(type, "ExportFieldPackage", field, package);

            using var archive = ZipFile.OpenRead(package);
            Assert.Contains(archive.Entries, entry => entry.FullName == "field.json");
            Assert.Contains(archive.Entries, entry => entry.FullName == "assets/logo.png");
            Assert.Contains(archive.Entries, entry => entry.FullName == "assets/logo_2.png");
            using var json = archive.GetEntry("field.json")!.Open();
            var exported = JsonSerializer.Deserialize<CustomBaseballField>(json);
            Assert.Equal("assets/logo.png", exported.BackgroundAssetPath);
            Assert.Equal("assets/logo_2.png", exported.Overlays[0].AssetPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

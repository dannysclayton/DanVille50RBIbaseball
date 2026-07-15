using System.ComponentModel;
using System.Windows.Forms;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class PostGameResultDialogTests
{
    [Fact]
    public void DialogBuildsCompleteSortablePlayerBoxScoresForBothTeams()
    {
        WinFormsTestHost.Run(() =>
        {
            Team away = Team("Danville", "Dragons", "DAN");
            Team home = Team("Riverton", "Rockets", "RIV");
            Guid aliceId = Guid.NewGuid();
            Guid bellaId = Guid.NewGuid();
            var result = new GameResult
            {
                AwayTeamId = away.Id,
                HomeTeamId = home.Id,
                AwayScore = 7,
                HomeScore = 4,
                AwayHits = 9,
                HomeHits = 6,
                AwayErrors = 1,
                HomeErrors = 2,
                AwayLeftOnBase = 8,
                HomeLeftOnBase = 5,
                AwayRunsByInning = new List<int> { 1, 2, 0, 4 },
                HomeRunsByInning = new List<int> { 0, 1, 2, 1 },
                AwayStartingLineup = new List<GameLineupEntry>
                {
                    new GameLineupEntry { PlayerId = aliceId, PlayerName = "Alice", BattingOrder = 2, AppearanceOrder = 2, DefensivePosition = "CF" },
                    new GameLineupEntry { PlayerId = bellaId, PlayerName = "Bella", BattingOrder = 1, AppearanceOrder = 1, DesignatedHitter = true }
                },
                Lines = new List<PlayerGameLine>
                {
                    CompleteBattingLine(away.Id, aliceId, "Alice"),
                    new PlayerGameLine { TeamId = away.Id, PlayerId = bellaId, PlayerName = "Bella", AB = 5, H = 4, R = 2 },
                    CompletePitchingLine(away.Id, "Ace Starter", startingPitcher: true, inningsOuts: 7),
                    new PlayerGameLine { TeamId = away.Id, PlayerName = "Reliever", Pitcher = true, IPOuts = 9, BattersFaced = 11 },
                    new PlayerGameLine { TeamId = home.Id, PlayerName = "Home Batter", AB = 4, H = 2, RBI = 1 },
                    new PlayerGameLine { TeamId = home.Id, PlayerName = "Home Pitcher", Pitcher = true, StartingPitcher = true, IPOuts = 12, ER = 3 }
                }
            };

            using var dialog = new PostGameResultDialog(away, home, result, null, "1-0", "Commit to Season", canCommit: true);
            DataGridView lineScore = WinFormsTestHost.Field<DataGridView>(dialog, "_lineScoreGrid");
            DataGridView awayBatting = WinFormsTestHost.Field<DataGridView>(dialog, "_awayBattingGrid");
            DataGridView awayPitching = WinFormsTestHost.Field<DataGridView>(dialog, "_awayPitchingGrid");
            DataGridView homeBatting = WinFormsTestHost.Field<DataGridView>(dialog, "_homeBattingGrid");
            DataGridView homePitching = WinFormsTestHost.Field<DataGridView>(dialog, "_homePitchingGrid");
            TabControl tabs = WinFormsTestHost.Field<TabControl>(dialog, "_playerTabs");

            Assert.Equal(2, lineScore.Rows.Count);
            Assert.Equal("1 2 0 4", lineScore.Rows[0].Cells["line"].Value);
            Assert.Equal(9, lineScore.Rows[0].Cells["hits"].Value);
            Assert.Equal(4, tabs.TabPages.Count);
            Assert.Equal(new[] { "DAN Batting", "DAN Pitching", "RIV Batting", "RIV Pitching" },
                tabs.TabPages.Cast<TabPage>().Select(page => page.Text));

            AssertGridBehavior(awayBatting);
            AssertGridBehavior(awayPitching);
            Assert.Equal(BattingColumns, awayBatting.Columns.Cast<DataGridViewColumn>().Select(column => column.Name));
            Assert.Equal(PitchingColumns, awayPitching.Columns.Cast<DataGridViewColumn>().Select(column => column.Name));

            Assert.Equal(2, awayBatting.Rows.Count);
            Assert.Equal("Bella", awayBatting.Rows[0].Cells["player"].Value);
            Assert.Equal("DH", awayBatting.Rows[0].Cells["position"].Value);
            DataGridViewRow alice = awayBatting.Rows.Cast<DataGridViewRow>()
                .Single(row => Equals(row.Cells["player"].Value, "Alice"));
            Assert.Equal("CF", alice.Cells["position"].Value);
            Assert.Equal(8, alice.Cells["plateAppearances"].Value);
            Assert.Equal(3, alice.Cells["hits"].Value);
            Assert.Equal(2, alice.Cells["flyOuts"].Value);
            Assert.Equal(1, alice.Cells["groundedIntoDoublePlays"].Value);
            Assert.Equal(1, alice.Cells["reachedOnError"].Value);

            Assert.Equal(2, awayPitching.Rows.Count);
            DataGridViewRow starter = awayPitching.Rows[0];
            Assert.Equal("Ace Starter", starter.Cells["pitcher"].Value);
            Assert.Equal("SP", starter.Cells["role"].Value);
            Assert.Equal(2.1m, starter.Cells["inningsPitched"].Value);
            Assert.Equal(5, starter.Cells["hitsAllowed"].Value);
            Assert.Equal(1, starter.Cells["wins"].Value);
            Assert.Equal(1, starter.Cells["completeGames"].Value);
            Assert.Equal(1, starter.Cells["shutouts"].Value);

            awayBatting.Sort(awayBatting.Columns["hits"], ListSortDirection.Descending);
            Assert.Equal("Bella", awayBatting.Rows[0].Cells["player"].Value);
            Assert.Single(homeBatting.Rows.Cast<DataGridViewRow>());
            Assert.Single(homePitching.Rows.Cast<DataGridViewRow>());
        });
    }

    [Fact]
    public void DialogHandlesNullAndEmptyLinesWithoutLosingTeamLineScore()
    {
        WinFormsTestHost.Run(() =>
        {
            Team away = Team("Danville", "Dragons", "DAN");
            Team home = Team("Riverton", "Rockets", "RIV");
            var result = new GameResult
            {
                AwayTeamId = away.Id,
                HomeTeamId = home.Id,
                AwayScore = 2,
                HomeScore = 2,
                Lines = null,
                AwayRunsByInning = null,
                HomeRunsByInning = null,
                AwayStartingLineup = null,
                HomeStartingLineup = null
            };

            using var dialog = new PostGameResultDialog(away, home, result, null, "", "Commit Result", canCommit: false);
            DataGridView lineScore = WinFormsTestHost.Field<DataGridView>(dialog, "_lineScoreGrid");
            TabControl tabs = WinFormsTestHost.Field<TabControl>(dialog, "_playerTabs");

            Assert.Equal(FormBorderStyle.Sizable, dialog.FormBorderStyle);
            Assert.Equal(2, lineScore.Rows.Count);
            Assert.All(lineScore.Rows.Cast<DataGridViewRow>(), row => Assert.Equal("-", row.Cells["line"].Value));
            Assert.Equal(4, tabs.TabPages.Count);
            Assert.All(PlayerGrids(dialog), grid => Assert.Empty(grid.Rows.Cast<DataGridViewRow>()));
            Assert.All(tabs.TabPages.Cast<TabPage>(), page =>
            {
                Label message = Assert.Single(Descendants<Label>(page), label => !string.IsNullOrWhiteSpace(label.Text));
                Assert.Contains("No ", message.Text);
                Assert.Contains("lines recorded", message.Text);
            });

            Button commit = WinFormsTestHost.Field<Button>(dialog, "_commitButton");
            Button dismiss = WinFormsTestHost.Field<Button>(dialog, "_dismissButton");
            Assert.False(commit.Enabled);
            Assert.True(dismiss.Enabled);
            Assert.False(dialog.CommitRequested);
        });
    }

    [Fact]
    public void BattingPositionsShowRecordedAssignmentsAndChangesInsteadOfEligibility()
    {
        WinFormsTestHost.Run(() =>
        {
            Team away = Team("Danville", "Dragons", "DAN");
            Team home = Team("Riverton", "Rockets", "RIV");
            Guid starterId = Guid.NewGuid();
            Guid substituteId = Guid.NewGuid();
            var result = new GameResult
            {
                AwayTeamId = away.Id,
                HomeTeamId = home.Id,
                AwayStartingLineup = new List<GameLineupEntry>
                {
                    new GameLineupEntry
                    {
                        PlayerId = starterId,
                        PlayerName = "Starter",
                        BattingOrder = 1,
                        AppearanceOrder = 1,
                        DefensivePosition = "RF",
                        Positions = "CF/SS/2B",
                        PositionHistory = new List<GamePositionChange>
                        {
                            new GamePositionChange { Inning = 1, Half = HalfInning.Top, Position = "CF" },
                            new GamePositionChange { Inning = 5, Half = HalfInning.Bottom, Position = "RF" }
                        }
                    },
                    new GameLineupEntry
                    {
                        PlayerId = substituteId,
                        PlayerName = "Substitute",
                        BattingOrder = 1,
                        AppearanceOrder = 10,
                        DefensivePosition = "1B",
                        Positions = "C/3B",
                        IsStarter = false,
                        EnteredInning = 6,
                        PositionHistory = new List<GamePositionChange>
                        {
                            new GamePositionChange { Inning = 6, Half = HalfInning.Top, Position = "LF" },
                            new GamePositionChange { Inning = 8, Half = HalfInning.Top, Position = "1B" }
                        }
                    }
                },
                Lines = new List<PlayerGameLine>
                {
                    new PlayerGameLine { TeamId = away.Id, PlayerId = starterId, PlayerName = "Starter", AB = 3 },
                    new PlayerGameLine { TeamId = away.Id, PlayerId = substituteId, PlayerName = "Substitute", AB = 1 }
                }
            };

            using var dialog = new PostGameResultDialog(away, home, result, null, "", "Commit Result", canCommit: false);
            DataGridView batting = WinFormsTestHost.Field<DataGridView>(dialog, "_awayBattingGrid");

            Assert.Equal("CF/RF", PlayerRow(batting, "Starter").Cells["position"].Value);
            Assert.Equal("LF/1B", PlayerRow(batting, "Substitute").Cells["position"].Value);
        });
    }

    [Fact]
    public void PitchersFollowRecordedAppearanceOrderWithDeterministicLegacyFallback()
    {
        WinFormsTestHost.Run(() =>
        {
            Team away = Team("Danville", "Dragons", "DAN");
            Team home = Team("Riverton", "Rockets", "RIV");
            Guid starterId = Guid.NewGuid();
            Guid firstRelieverId = Guid.NewGuid();
            Guid secondRelieverId = Guid.NewGuid();
            Guid legacyStarterId = Guid.NewGuid();
            Guid legacyFirstId = Guid.NewGuid();
            Guid legacySecondId = Guid.NewGuid();
            var result = new GameResult
            {
                AwayTeamId = away.Id,
                HomeTeamId = home.Id,
                AwayStartingLineup = new List<GameLineupEntry>
                {
                    PitcherEntry(starterId, "Starter", appearanceOrder: 8, inning: 1),
                    PitcherEntry(secondRelieverId, "Second Reliever", appearanceOrder: 11, inning: 8),
                    PitcherEntry(firstRelieverId, "First Reliever", appearanceOrder: 10, inning: 6)
                },
                HomeStartingLineup = new List<GameLineupEntry>
                {
                    LegacyPitcherEntry(legacySecondId, "Zulu Relief", appearanceOrder: 12),
                    LegacyPitcherEntry(legacyStarterId, "Legacy Starter", appearanceOrder: 9),
                    LegacyPitcherEntry(legacyFirstId, "Alpha Relief", appearanceOrder: 11)
                },
                Lines = new List<PlayerGameLine>
                {
                    PitchingLine(away.Id, secondRelieverId, "Second Reliever", inningsOuts: 9),
                    PitchingLine(away.Id, starterId, "Starter", inningsOuts: 3, startingPitcher: true),
                    PitchingLine(away.Id, firstRelieverId, "First Reliever", inningsOuts: 12),
                    PitchingLine(home.Id, legacySecondId, "Zulu Relief", inningsOuts: 12),
                    PitchingLine(home.Id, legacyFirstId, "Alpha Relief", inningsOuts: 3),
                    PitchingLine(home.Id, legacyStarterId, "Legacy Starter", inningsOuts: 6, startingPitcher: true)
                }
            };

            using var dialog = new PostGameResultDialog(away, home, result, null, "", "Commit Result", canCommit: false);
            DataGridView awayPitching = WinFormsTestHost.Field<DataGridView>(dialog, "_awayPitchingGrid");
            DataGridView homePitching = WinFormsTestHost.Field<DataGridView>(dialog, "_homePitchingGrid");

            Assert.Equal(new[] { "Starter", "First Reliever", "Second Reliever" }, PitcherNames(awayPitching));
            Assert.Equal(new[] { "Legacy Starter", "Alpha Relief", "Zulu Relief" }, PitcherNames(homePitching));
        });
    }

    private static readonly string[] BattingColumns =
    {
        "player", "position", "plateAppearances", "atBats", "runs", "hits", "doubles", "triples", "homeRuns",
        "runsBattedIn", "walks", "intentionalWalks", "strikeouts", "stolenBases", "caughtStealing", "hitByPitch",
        "sacrificeHits", "sacrificeFlies", "flyOuts", "groundOuts", "popOuts", "groundedIntoDoublePlays", "reachedOnError"
    };

    private static readonly string[] PitchingColumns =
    {
        "pitcher", "role", "inningsPitched", "hitsAllowed", "runsAllowed", "earnedRuns", "doublesAllowed",
        "triplesAllowed", "homeRunsAllowed", "walksAllowed", "intentionalWalksAllowed", "strikeouts", "hitBatters",
        "wildPitches", "balks", "battersFaced", "pitchCount", "wins", "losses", "saves", "holds", "blownSaves",
        "completeGames", "shutouts"
    };

    private static Team Team(string city, string nickname, string abbreviation)
        => new Team { City = city, Nickname = nickname, ScoreboardAbbreviation = abbreviation };

    private static PlayerGameLine CompleteBattingLine(Guid teamId, Guid playerId, string name)
        => new PlayerGameLine
        {
            TeamId = teamId,
            PlayerId = playerId,
            PlayerName = name,
            AB = 4,
            R = 2,
            H = 3,
            Doubles = 1,
            Triples = 1,
            HR = 1,
            RBI = 4,
            BB = 1,
            IBB = 1,
            SO = 1,
            SB = 1,
            CS = 1,
            HBP = 1,
            SH = 1,
            SF = 1,
            FlyOuts = 2,
            GroundOuts = 1,
            PopOuts = 1,
            GroundedIntoDoublePlays = 1,
            ReachedOnError = 1
        };

    private static PlayerGameLine CompletePitchingLine(Guid teamId, string name, bool startingPitcher, int inningsOuts)
        => new PlayerGameLine
        {
            TeamId = teamId,
            PlayerName = name,
            Pitcher = true,
            StartingPitcher = startingPitcher,
            IPOuts = inningsOuts,
            HitsAllowed = 5,
            RunsAllowed = 2,
            ER = 1,
            DoublesAllowed = 1,
            TriplesAllowed = 1,
            HomeRunsAllowed = 1,
            WalksAllowed = 2,
            IntentionalWalksAllowed = 1,
            K = 6,
            HitBatters = 1,
            WildPitches = 1,
            Balks = 1,
            BattersFaced = 12,
            PitchCount = 48,
            Wins = 1,
            Losses = 0,
            Saves = 1,
            Holds = 1,
            BlownSaves = 1,
            CompleteGames = 1,
            Shutouts = 1
        };

    private static GameLineupEntry PitcherEntry(Guid playerId, string name, int appearanceOrder, int inning)
        => new GameLineupEntry
        {
            PlayerId = playerId,
            PlayerName = name,
            AppearanceOrder = appearanceOrder,
            DefensivePosition = "P",
            PositionHistory = new List<GamePositionChange>
            {
                new GamePositionChange { Inning = inning, Half = HalfInning.Top, Position = "P" }
            }
        };

    private static GameLineupEntry LegacyPitcherEntry(Guid playerId, string name, int appearanceOrder)
        => new GameLineupEntry
        {
            PlayerId = playerId,
            PlayerName = name,
            AppearanceOrder = appearanceOrder,
            DefensivePosition = "P",
            PositionHistory = null
        };

    private static PlayerGameLine PitchingLine(
        Guid teamId,
        Guid playerId,
        string name,
        int inningsOuts,
        bool startingPitcher = false)
        => new PlayerGameLine
        {
            TeamId = teamId,
            PlayerId = playerId,
            PlayerName = name,
            Pitcher = true,
            StartingPitcher = startingPitcher,
            IPOuts = inningsOuts
        };

    private static DataGridViewRow PlayerRow(DataGridView grid, string playerName)
        => grid.Rows.Cast<DataGridViewRow>()
            .Single(row => Equals(row.Cells["player"].Value, playerName));

    private static string[] PitcherNames(DataGridView grid)
        => grid.Rows.Cast<DataGridViewRow>()
            .Select(row => Convert.ToString(row.Cells["pitcher"].Value) ?? "")
            .ToArray();

    private static void AssertGridBehavior(DataGridView grid)
    {
        Assert.True(grid.ReadOnly);
        Assert.False(grid.AllowUserToAddRows);
        Assert.False(grid.AllowUserToDeleteRows);
        Assert.True(grid.AllowUserToOrderColumns);
        Assert.Equal(DataGridViewAutoSizeColumnsMode.None, grid.AutoSizeColumnsMode);
        Assert.Equal(ScrollBars.Both, grid.ScrollBars);
        Assert.All(grid.Columns.Cast<DataGridViewColumn>(),
            column => Assert.Equal(DataGridViewColumnSortMode.Automatic, column.SortMode));
    }

    private static IEnumerable<DataGridView> PlayerGrids(PostGameResultDialog dialog)
    {
        yield return WinFormsTestHost.Field<DataGridView>(dialog, "_awayBattingGrid");
        yield return WinFormsTestHost.Field<DataGridView>(dialog, "_awayPitchingGrid");
        yield return WinFormsTestHost.Field<DataGridView>(dialog, "_homeBattingGrid");
        yield return WinFormsTestHost.Field<DataGridView>(dialog, "_homePitchingGrid");
    }

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
                yield return match;
            foreach (T descendant in Descendants<T>(child))
                yield return descendant;
        }
    }
}

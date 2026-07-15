using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed partial class MainForm
    {
        private sealed class StatisticsEntityItem
        {
            public Guid? Id { get; set; }
            public string Text { get; set; } = "";
            public override string ToString() => Text;
        }

        private sealed class HierarchyStatisticsPage
        {
            public string Level { get; set; } = "";
            public required ComboBox EntityCombo { get; set; }
            public required ComboBox ScopeCombo { get; set; }
            public required ComboBox SeasonCombo { get; set; }
            public required DataGridView TeamGrid { get; set; }
            public required DataGridView PlayerGrid { get; set; }
            public required DataGridView LeadersGrid { get; set; }
        }

        private sealed class PlayerStatContext
        {
            public required Team Team { get; set; }
            public required PlayerSeasonStatLine Stats { get; set; }
        }

        private readonly Dictionary<string, HierarchyStatisticsPage> _hierarchyStatisticsPages =
            new Dictionary<string, HierarchyStatisticsPage>(StringComparer.OrdinalIgnoreCase);
        private bool _refreshingHierarchyStatistics;

        private void BuildHierarchyStatisticsTab(TabPage host)
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };
            foreach (string level in new[] { "League", "Conference", "Region", "District", "Team" })
            {
                var page = new TabPage(level);
                BuildHierarchyStatisticsPage(page, level);
                tabs.TabPages.Add(page);
            }
            host.Controls.Add(tabs);
        }

        private void BuildHierarchyStatisticsPage(TabPage tab, string level)
        {
            var controls = new HierarchyStatisticsPage
            {
                Level = level,
                EntityCombo = new ComboBox { Width = 245, DropDownStyle = ComboBoxStyle.DropDownList },
                ScopeCombo = new ComboBox { Width = 105, DropDownStyle = ComboBoxStyle.DropDownList },
                SeasonCombo = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList },
                TeamGrid = CreateReadOnlyGrid(),
                PlayerGrid = CreateReadOnlyGrid(),
                LeadersGrid = CreateReadOnlyGrid()
            };
            _hierarchyStatisticsPages[level] = controls;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
            bar.Controls.Add(new Label { Text = level, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(4, 10, 8, 0) });
            controls.EntityCombo.SelectedIndexChanged += (s, e) => RefreshHierarchyStatisticsPage(controls);
            bar.Controls.Add(controls.EntityCombo);
            bar.Controls.Add(new Label { Text = "Scope", AutoSize = true, Margin = new Padding(12, 10, 4, 0) });
            controls.ScopeCombo.Items.AddRange(new object[] { "Season", "Playoffs", "Career", "All-Time" });
            controls.ScopeCombo.SelectedIndex = 0;
            controls.ScopeCombo.SelectedIndexChanged += (s, e) => RefreshHierarchyStatisticsPage(controls);
            bar.Controls.Add(controls.ScopeCombo);
            bar.Controls.Add(new Label { Text = "Season", AutoSize = true, Margin = new Padding(12, 10, 4, 0) });
            controls.SeasonCombo.SelectedIndexChanged += (s, e) => RefreshHierarchyStatisticsPage(controls);
            bar.Controls.Add(controls.SeasonCombo);

            AddHierarchyTeamColumns(controls.TeamGrid);
            AddHierarchyPlayerColumns(controls.PlayerGrid);
            AddGridColumn(controls.LeadersGrid, "Type", 90);
            AddGridColumn(controls.LeadersGrid, "Category", 180);
            AddGridColumn(controls.LeadersGrid, "Leader", 190);
            AddGridColumn(controls.LeadersGrid, "Team", 190);
            AddGridColumn(controls.LeadersGrid, "Value", 90);
            AddGridColumn(controls.LeadersGrid, "Scope", 150);

            AddButton(bar, "Export Teams Excel", (s, e) => ExportGrid(controls.TeamGrid, level + " Team Statistics", ExportFormat.Excel));
            AddButton(bar, "Export Teams Word", (s, e) => ExportGrid(controls.TeamGrid, level + " Team Statistics", ExportFormat.Word));
            if (string.Equals(level, "Team", StringComparison.OrdinalIgnoreCase))
            {
                AddButton(bar, "Export Players Excel", (s, e) => ExportGrid(controls.PlayerGrid, "Team Player Statistics", ExportFormat.Excel));
                AddButton(bar, "Export Players Word", (s, e) => ExportGrid(controls.PlayerGrid, "Team Player Statistics", ExportFormat.Word));
            }
            else
            {
                AddButton(bar, "Export Leaders Excel", (s, e) => ExportGrid(controls.LeadersGrid, level + " Statistical Leaders", ExportFormat.Excel));
                AddButton(bar, "Export Leaders Word", (s, e) => ExportGrid(controls.LeadersGrid, level + " Statistical Leaders", ExportFormat.Word));
            }
            root.Controls.Add(bar, 0, 0);

            var dataTabs = new TabControl { Dock = DockStyle.Fill };
            var teamTab = new TabPage(string.Equals(level, "Team", StringComparison.OrdinalIgnoreCase) ? "Team Statistics" : "Team Information");
            teamTab.Controls.Add(controls.TeamGrid);
            dataTabs.TabPages.Add(teamTab);
            var detailTab = new TabPage(string.Equals(level, "Team", StringComparison.OrdinalIgnoreCase) ? "Player Statistics" : "Leaders");
            detailTab.Controls.Add(string.Equals(level, "Team", StringComparison.OrdinalIgnoreCase) ? controls.PlayerGrid : controls.LeadersGrid);
            dataTabs.TabPages.Add(detailTab);
            root.Controls.Add(dataTabs, 0, 1);
            tab.Controls.Add(root);
        }

        private static void AddHierarchyTeamColumns(DataGridView grid)
        {
            foreach (var column in new (string Name, int Width)[]
            {
                ("Team", 190), ("Conference", 130), ("Region", 120), ("District", 120), ("Scope", 115),
                ("G", 44), ("W", 44), ("L", 44), ("T", 44), ("Pct", 58), ("RS", 50), ("RA", 50), ("Diff", 52),
                ("AVG", 58), ("OBP", 58), ("SLG", 58), ("OPS", 58), ("PA", 52), ("XBH", 50), ("AB", 52), ("R", 46), ("H", 46),
                ("2B", 44), ("3B", 44), ("HR", 46), ("RBI", 50), ("BB", 46), ("IBB", 48), ("SO", 46),
                ("SB", 46), ("CS", 46), ("HBP", 48), ("SH", 44), ("SF", 44), ("GO", 46), ("FO", 46),
                ("POp", 48), ("GIDP", 52), ("ROE", 48), ("IP", 54), ("ERA", 60), ("WHIP", 62), ("RA-P", 52), ("K", 46), ("PBB", 50),
                ("PIBB", 54), ("H-A", 52), ("2B-A", 52), ("3B-A", 52), ("HR-A", 54), ("HB", 46), ("WP", 46), ("BK", 46),
                ("HLD", 48), ("BS", 44), ("CG", 44), ("SHO", 48), ("FPCT", 62), ("Def IP", 56), ("TC", 46), ("PO", 46), ("A", 44), ("E", 44),
                ("DP", 44), ("PB", 44), ("SBA-C", 54), ("CS-C", 50), ("CS%", 56), ("INJ-G", 58),
                ("Champion", 82)
            })
                AddGridColumn(grid, column.Name, column.Width);
        }

        private static void AddHierarchyPlayerColumns(DataGridView grid)
        {
            foreach (var column in new (string Name, int Width)[]
            {
                ("Player", 170), ("Team", 180), ("Role", 70), ("Class", 90), ("Pos", 78), ("Injury", 125),
                ("Medical", 66), ("Redshirt", 70), ("V Years", 58), ("Call-Up", 62), ("INJ-G", 58), ("G", 44), ("R", 44), ("PA", 50), ("XBH", 48), ("AB", 50), ("H", 46),
                ("2B", 44), ("3B", 44), ("HR", 46), ("RBI", 50), ("BB", 46), ("IBB", 48), ("SO", 46),
                ("SB", 46), ("CS", 46), ("HBP", 48), ("SH", 44), ("SF", 44), ("GO", 46), ("FO", 46),
                ("POp", 48), ("GIDP", 52), ("ROE", 48), ("AVG", 58), ("OBP", 58), ("SLG", 58), ("OPS", 58), ("TB", 46),
                ("IP", 54), ("W", 44), ("L", 44), ("SV", 44), ("HLD", 48), ("BS", 44), ("CG", 44), ("SHO", 48), ("K", 46), ("ER", 46), ("RA", 46), ("ERA", 60),
                ("WHIP", 62), ("H-A", 52), ("2B-A", 52), ("3B-A", 52), ("BB-A", 54), ("IBB-A", 56), ("HR-A", 54), ("HB", 46),
                ("WP", 46), ("BK", 46), ("BF", 46), ("PC", 48), ("FPCT", 62), ("Def IP", 56), ("TC", 46), ("PO", 46), ("A", 44),
                ("E", 44), ("DP", 44), ("PB", 44), ("SBA-C", 54), ("CS-C", 50), ("CS%", 56), ("Scope", 120)
            })
                AddGridColumn(grid, column.Name, column.Width);
        }

        private void RefreshHierarchyStatistics()
        {
            if (_hierarchyStatisticsPages.Count == 0 || _refreshingHierarchyStatistics)
                return;
            _refreshingHierarchyStatistics = true;
            try
            {
                foreach (HierarchyStatisticsPage page in _hierarchyStatisticsPages.Values)
                {
                    PopulateHierarchyEntities(page);
                    PopulateHierarchySeasons(page);
                    RefreshHierarchyStatisticsPageCore(page);
                }
            }
            finally
            {
                _refreshingHierarchyStatistics = false;
            }
        }

        private void PopulateHierarchyEntities(HierarchyStatisticsPage page)
        {
            Guid? previous = (page.EntityCombo.SelectedItem as StatisticsEntityItem)?.Id;
            page.EntityCombo.Items.Clear();
            foreach (StatisticsEntityItem item in StatisticsEntities(page.Level))
                page.EntityCombo.Items.Add(item);
            int index = 0;
            if (previous.HasValue)
            {
                for (int i = 0; i < page.EntityCombo.Items.Count; i++)
                {
                    if ((page.EntityCombo.Items[i] as StatisticsEntityItem)?.Id == previous)
                    {
                        index = i;
                        break;
                    }
                }
            }
            if (page.EntityCombo.Items.Count > 0)
                page.EntityCombo.SelectedIndex = index;
            page.EntityCombo.Enabled = !string.Equals(page.Level, "League", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<StatisticsEntityItem> StatisticsEntities(string level)
        {
            if (string.Equals(level, "League", StringComparison.OrdinalIgnoreCase))
            {
                yield return new StatisticsEntityItem { Text = _league?.Name ?? "League" };
                yield break;
            }
            if (string.Equals(level, "Team", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Team team in (_league?.Teams ?? new List<Team>()).OrderBy(team => team.DisplayName))
                    yield return new StatisticsEntityItem { Id = team.Id, Text = team.DisplayName };
                yield break;
            }
            foreach (Conference conference in _league?.Structure?.Conferences ?? new List<Conference>())
            {
                if (string.Equals(level, "Conference", StringComparison.OrdinalIgnoreCase))
                    yield return new StatisticsEntityItem { Id = conference.Id, Text = conference.Name };
                foreach (Region region in conference.Regions ?? new List<Region>())
                {
                    if (string.Equals(level, "Region", StringComparison.OrdinalIgnoreCase))
                        yield return new StatisticsEntityItem { Id = region.Id, Text = conference.Name + " - " + region.Name };
                    foreach (District district in region.Districts ?? new List<District>())
                    {
                        if (string.Equals(level, "District", StringComparison.OrdinalIgnoreCase))
                            yield return new StatisticsEntityItem { Id = district.Id, Text = conference.Name + " - " + region.Name + " - " + district.Name };
                    }
                }
            }
        }

        private void PopulateHierarchySeasons(HierarchyStatisticsPage page)
        {
            Guid? previous = (page.SeasonCombo.SelectedItem as SeasonItem)?.Season?.Id;
            page.SeasonCombo.Items.Clear();
            foreach (Season season in _league?.Seasons ?? new List<Season>())
                page.SeasonCombo.Items.Add(new SeasonItem { Season = season, Text = "Season " + CurrentSeasonNumber(season) + " - " + season.Name });
            int index = Math.Max(0, page.SeasonCombo.Items.Count - 1);
            if (previous.HasValue)
            {
                for (int i = 0; i < page.SeasonCombo.Items.Count; i++)
                {
                    if ((page.SeasonCombo.Items[i] as SeasonItem)?.Season?.Id == previous)
                    {
                        index = i;
                        break;
                    }
                }
            }
            if (page.SeasonCombo.Items.Count > 0)
                page.SeasonCombo.SelectedIndex = index;
        }

        private void RefreshHierarchyStatisticsPage(HierarchyStatisticsPage page)
        {
            if (_refreshingHierarchyStatistics)
                return;
            RefreshHierarchyStatisticsPageCore(page);
        }

        private void RefreshHierarchyStatisticsPageCore(HierarchyStatisticsPage page)
        {
            page.TeamGrid.Rows.Clear();
            page.PlayerGrid.Rows.Clear();
            page.LeadersGrid.Rows.Clear();
            if (_league?.Teams == null)
                return;

            Guid? entityId = (page.EntityCombo.SelectedItem as StatisticsEntityItem)?.Id;
            var teamIds = StatisticsTeamIds(page.Level, entityId);
            var teams = _league.Teams.Where(team => teamIds.Contains(team.Id)).OrderBy(team => team.DisplayName).ToList();
            string scope = Convert.ToString(page.ScopeCombo.SelectedItem) ?? "Season";
            var seasons = _league.Seasons ?? new List<Season>();
            Season? season = (page.SeasonCombo.SelectedItem as SeasonItem)?.Season ?? seasons.LastOrDefault();
            bool career = scope == "Career" || scope == "All-Time";
            bool playoffs = scope == "Playoffs";
            string scopeLabel = career ? scope : (season == null ? scope : "Season " + CurrentSeasonNumber(season) + (playoffs ? " Playoffs" : ""));

            var teamStats = new List<(Team Team, TeamSeasonStatLine Stats)>();
            var playerStats = new List<PlayerStatContext>();
            foreach (Team team in teams)
            {
                TeamSeasonStatLine teamLine = career
                    ? TeamCareerStats(team, seasons, playoffsOnly: false)
                    : TeamSeasonStats(season, team.Id, playoffs);
                teamLine.TeamName = team.DisplayName;
                teamStats.Add((team, teamLine));
                AddHierarchyTeamRow(page.TeamGrid, team, teamLine, scopeLabel);

                List<PlayerSeasonStatLine> players = career
                    ? PlayerCareerStats(seasons, team)
                    : PlayerSeasonStats(season, team, playoffs);
                foreach (PlayerSeasonStatLine player in players)
                {
                    playerStats.Add(new PlayerStatContext { Team = team, Stats = player });
                    if (string.Equals(page.Level, "Team", StringComparison.OrdinalIgnoreCase))
                        AddHierarchyPlayerRow(page.PlayerGrid, team, player, scopeLabel);
                }
            }

            if (!string.Equals(page.Level, "Team", StringComparison.OrdinalIgnoreCase))
                AddHierarchyLeaderRows(page.LeadersGrid, teamStats, playerStats, scopeLabel);
            page.SeasonCombo.Enabled = !career;
        }

        private HashSet<Guid> StatisticsTeamIds(string level, Guid? entityId)
        {
            if (string.Equals(level, "League", StringComparison.OrdinalIgnoreCase) || !entityId.HasValue)
                return (_league?.Teams ?? new List<Team>()).Select(team => team.Id).ToHashSet();
            if (string.Equals(level, "Team", StringComparison.OrdinalIgnoreCase))
                return new HashSet<Guid> { entityId.Value };

            var ids = new HashSet<Guid>();
            foreach (Conference conference in _league?.Structure?.Conferences ?? new List<Conference>())
            {
                bool conferenceMatch = string.Equals(level, "Conference", StringComparison.OrdinalIgnoreCase) && conference.Id == entityId.Value;
                foreach (Region region in conference.Regions ?? new List<Region>())
                {
                    bool regionMatch = string.Equals(level, "Region", StringComparison.OrdinalIgnoreCase) && region.Id == entityId.Value;
                    foreach (District district in region.Districts ?? new List<District>())
                    {
                        bool districtMatch = string.Equals(level, "District", StringComparison.OrdinalIgnoreCase) && district.Id == entityId.Value;
                        if (conferenceMatch || regionMatch || districtMatch)
                            ids.UnionWith(district.TeamIds ?? new List<Guid>());
                    }
                }
            }
            return ids;
        }

        private void AddHierarchyTeamRow(DataGridView grid, Team team, TeamSeasonStatLine stats, string scope)
        {
            TeamPlacement? placement = FindTeamPlacement(team.Id);
            grid.Rows.Add(
                team.DisplayName, placement?.Conference?.Name ?? "", placement?.Region?.Name ?? "", placement?.District?.Name ?? "", scope,
                stats.Games, stats.Wins, stats.Losses, stats.Ties,
                Math.Round(stats.Games == 0 ? 0.0 : (stats.Wins + stats.Ties * 0.5) / stats.Games, 3),
                stats.RunsFor, stats.RunsAgainst, stats.RunDiff,
                Math.Round(AverageValue(stats.H, stats.AB), 3), Math.Round(ObpValue(stats.H, stats.BB, stats.HBP, stats.AB, stats.SF), 3),
                Math.Round(SlgValue(stats.TotalBases, stats.AB), 3),
                Math.Round(ObpValue(stats.H, stats.BB, stats.HBP, stats.AB, stats.SF) + SlgValue(stats.TotalBases, stats.AB), 3),
                stats.PlateAppearances, stats.ExtraBaseHits, stats.AB, stats.R > 0 ? stats.R : stats.RunsFor, stats.H, stats.Doubles, stats.Triples, stats.HR, stats.RBI,
                stats.BB, stats.IBB, stats.SO, stats.SB, stats.CS, stats.HBP, stats.SH, stats.SF,
                stats.GroundOuts, stats.FlyOuts, stats.PopOuts, stats.GroundedIntoDoublePlays, stats.ReachedOnError,
                FormatInnings(stats.IPOuts), Math.Round(EraValue(stats.ER, stats.IPOuts), 2),
                Math.Round(WhipValue(stats.PitchingBB, stats.HitsAllowed, stats.IPOuts), 2), stats.RunsAllowed, stats.PitchingK,
                stats.PitchingBB, stats.PitchingIBB, stats.HitsAllowed, stats.DoublesAllowed, stats.TriplesAllowed, stats.HomeRunsAllowed, stats.HitBatters,
                stats.WildPitches, stats.Balks, stats.Holds, stats.BlownSaves, stats.CompleteGames, stats.Shutouts,
                Math.Round(FieldingPctValue(stats.Putouts, stats.Assists, stats.Errors), 3), FormatInnings(stats.DefensiveOuts), stats.TotalChances,
                stats.Putouts, stats.Assists, stats.Errors, stats.DoublePlaysTurned, stats.PassedBalls,
                stats.StolenBasesAllowed, stats.CatcherCaughtStealing,
                stats.CatcherStealAttempts <= 0 ? "" : stats.CatcherCaughtStealingPercentage.ToString("0.0%"), stats.InjuryGamesMissed,
                stats.Champion ? "Yes" : "");
        }

        private static void AddHierarchyPlayerRow(DataGridView grid, Team team, PlayerSeasonStatLine line, string scope)
        {
            grid.Rows.Add(
                line.PlayerName, team?.DisplayName ?? "", line.Pitcher ? "Pitcher" : "Batter",
                line.Classification == PlayerClassification.Unassigned ? "" : line.Classification.ToString(),
                line.Positions, line.Injury, line.MedicalTag ? "Yes" : line.MedicalEligible ? "Eligible" : "", line.Redshirt ? "Yes" : "",
                line.VarsitySeasons, line.CallUpSeason <= 0 ? "" : line.CallUpSeason.ToString(),
                line.GamesMissedInjury, line.Games, line.R, line.PlateAppearances, line.ExtraBaseHits, line.AB, line.H, line.Doubles, line.Triples, line.HR, line.RBI,
                line.BB, line.IBB, line.SO, line.SB, line.CS, line.HBP, line.SH, line.SF,
                line.GroundOuts, line.FlyOuts, line.PopOuts, line.GroundedIntoDoublePlays, line.ReachedOnError,
                Math.Round(AverageValue(line.H, line.AB), 3), Math.Round(ObpValue(line.H, line.BB, line.HBP, line.AB, line.SF), 3),
                Math.Round(SlgValue(line.TotalBases, line.AB), 3),
                Math.Round(ObpValue(line.H, line.BB, line.HBP, line.AB, line.SF) + SlgValue(line.TotalBases, line.AB), 3),
                line.TotalBases, FormatInnings(line.IPOuts), line.PitchingWins, line.PitchingLosses, line.Saves,
                line.Holds, line.BlownSaves, line.CompleteGames, line.Shutouts, line.K, line.ER, line.RunsAllowed,
                Math.Round(EraValue(line.ER, line.IPOuts), 2), Math.Round(WhipValue(line.WalksAllowed, line.HitsAllowed, line.IPOuts), 2),
                line.HitsAllowed, line.DoublesAllowed, line.TriplesAllowed, line.WalksAllowed, line.IntentionalWalksAllowed, line.HomeRunsAllowed, line.HitBatters,
                line.WildPitches, line.Balks, line.BattersFaced, line.PitchCount,
                Math.Round(FieldingPctValue(line.Putouts, line.Assists, line.Errors), 3), FormatInnings(line.DefensiveOuts), line.TotalChances,
                line.Putouts, line.Assists, line.Errors, line.DefensiveDoublePlays, line.PassedBalls,
                line.StolenBasesAllowed, line.CatcherCaughtStealing,
                line.CatcherStealAttempts <= 0 ? "" : line.CatcherCaughtStealingPercentage.ToString("0.0%"), scope);
        }

        private static void AddHierarchyLeaderRows(
            DataGridView grid,
            List<(Team Team, TeamSeasonStatLine Stats)> teams,
            List<PlayerStatContext> players,
            string scope)
        {
            var activeTeams = teams.Where(row => row.Stats.Games > 0).ToList();
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.Wins > 0).ToList(), "Wins", row => row.Stats.Wins, row => row.Stats.Wins.ToString(), scope);
            AddTeamLeader(grid, activeTeams, "Winning Percentage", row => (row.Stats.Wins + row.Stats.Ties * 0.5) / row.Stats.Games, row => ((row.Stats.Wins + row.Stats.Ties * 0.5) / row.Stats.Games).ToString("0.000"), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.RunsFor > 0).ToList(), "Runs Scored", row => row.Stats.RunsFor, row => row.Stats.RunsFor.ToString(), scope);
            AddTeamLeader(grid, activeTeams, "Run Differential", row => row.Stats.RunDiff, row => row.Stats.RunDiff.ToString(), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.HR > 0).ToList(), "Home Runs", row => row.Stats.HR, row => row.Stats.HR.ToString(), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.ExtraBaseHits > 0).ToList(), "Extra-Base Hits", row => row.Stats.ExtraBaseHits, row => row.Stats.ExtraBaseHits.ToString(), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.AB > 0).ToList(), "OPS", row => ObpValue(row.Stats.H, row.Stats.BB, row.Stats.HBP, row.Stats.AB, row.Stats.SF) + SlgValue(row.Stats.TotalBases, row.Stats.AB), row => (ObpValue(row.Stats.H, row.Stats.BB, row.Stats.HBP, row.Stats.AB, row.Stats.SF) + SlgValue(row.Stats.TotalBases, row.Stats.AB)).ToString("0.000"), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.IPOuts > 0).ToList(), "ERA", row => EraValue(row.Stats.ER, row.Stats.IPOuts), row => EraValue(row.Stats.ER, row.Stats.IPOuts).ToString("0.00"), scope, lowerIsBetter: true);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.PitchingK > 0).ToList(), "Pitcher Strikeouts", row => row.Stats.PitchingK, row => row.Stats.PitchingK.ToString(), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.Holds > 0).ToList(), "Holds", row => row.Stats.Holds, row => row.Stats.Holds.ToString(), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.CompleteGames > 0).ToList(), "Complete Games", row => row.Stats.CompleteGames, row => row.Stats.CompleteGames.ToString(), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.Shutouts > 0).ToList(), "Shutouts", row => row.Stats.Shutouts, row => row.Stats.Shutouts.ToString(), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.CatcherStealAttempts >= 5).ToList(), "Catcher Caught-Stealing Percentage", row => row.Stats.CatcherCaughtStealingPercentage, row => row.Stats.CatcherCaughtStealingPercentage.ToString("0.0%"), scope);
            AddTeamLeader(grid, activeTeams.Where(row => row.Stats.DoublePlaysTurned > 0).ToList(), "Double Plays Turned", row => row.Stats.DoublePlaysTurned, row => row.Stats.DoublePlaysTurned.ToString(), scope);
            AddTeamLeader(grid, activeTeams, "Fewest Errors", row => row.Stats.Errors, row => row.Stats.Errors.ToString(), scope, lowerIsBetter: true);

            AddPlayerLeader(grid, players.Where(row => row.Stats.AB > 0).ToList(), "Batting Average", row => AverageValue(row.Stats.H, row.Stats.AB), row => AverageValue(row.Stats.H, row.Stats.AB).ToString("0.000"), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.H > 0).ToList(), "Hits", row => row.Stats.H, row => row.Stats.H.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.HR > 0).ToList(), "Home Runs", row => row.Stats.HR, row => row.Stats.HR.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.ExtraBaseHits > 0).ToList(), "Extra-Base Hits", row => row.Stats.ExtraBaseHits, row => row.Stats.ExtraBaseHits.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.RBI > 0).ToList(), "RBI", row => row.Stats.RBI, row => row.Stats.RBI.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.SB > 0).ToList(), "Stolen Bases", row => row.Stats.SB, row => row.Stats.SB.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.PitchingWins > 0).ToList(), "Pitching Wins", row => row.Stats.PitchingWins, row => row.Stats.PitchingWins.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.K > 0).ToList(), "Strikeouts", row => row.Stats.K, row => row.Stats.K.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.Holds > 0).ToList(), "Holds", row => row.Stats.Holds, row => row.Stats.Holds.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.CompleteGames > 0).ToList(), "Complete Games", row => row.Stats.CompleteGames, row => row.Stats.CompleteGames.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.Shutouts > 0).ToList(), "Shutouts", row => row.Stats.Shutouts, row => row.Stats.Shutouts.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.CatcherStealAttempts >= 5).ToList(), "Catcher Caught-Stealing Percentage", row => row.Stats.CatcherCaughtStealingPercentage, row => row.Stats.CatcherCaughtStealingPercentage.ToString("0.0%"), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.IPOuts > 0).ToList(), "ERA", row => EraValue(row.Stats.ER, row.Stats.IPOuts), row => EraValue(row.Stats.ER, row.Stats.IPOuts).ToString("0.00"), scope, lowerIsBetter: true);
            AddPlayerLeader(grid, players.Where(row => row.Stats.GroundedIntoDoublePlays > 0).ToList(), "Grounded Into Double Plays", row => row.Stats.GroundedIntoDoublePlays, row => row.Stats.GroundedIntoDoublePlays.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.DefensiveDoublePlays > 0).ToList(), "Defensive Double Plays", row => row.Stats.DefensiveDoublePlays, row => row.Stats.DefensiveDoublePlays.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.Errors > 0).ToList(), "Errors", row => row.Stats.Errors, row => row.Stats.Errors.ToString(), scope);
            AddPlayerLeader(grid, players.Where(row => row.Stats.GamesMissedInjury > 0).ToList(), "Injury Games Missed", row => row.Stats.GamesMissedInjury, row => row.Stats.GamesMissedInjury.ToString(), scope);
        }

        private static void AddTeamLeader(
            DataGridView grid,
            List<(Team Team, TeamSeasonStatLine Stats)> rows,
            string category,
            Func<(Team Team, TeamSeasonStatLine Stats), double> rank,
            Func<(Team Team, TeamSeasonStatLine Stats), string> value,
            string scope,
            bool lowerIsBetter = false)
        {
            if (rows == null || rows.Count == 0)
                return;
            double best = lowerIsBetter ? rows.Min(rank) : rows.Max(rank);
            foreach (var row in rows.Where(row => Math.Abs(rank(row) - best) < 0.0001))
                grid.Rows.Add("Team", category, row.Team.DisplayName, row.Team.DisplayName, value(row), scope);
        }

        private static void AddPlayerLeader(
            DataGridView grid,
            List<PlayerStatContext> rows,
            string category,
            Func<PlayerStatContext, double> rank,
            Func<PlayerStatContext, string> value,
            string scope,
            bool lowerIsBetter = false)
        {
            if (rows == null || rows.Count == 0)
                return;
            double best = lowerIsBetter ? rows.Min(rank) : rows.Max(rank);
            foreach (PlayerStatContext row in rows.Where(row => Math.Abs(rank(row) - best) < 0.0001))
                grid.Rows.Add("Player", category, row.Stats.PlayerName, row.Team?.DisplayName ?? "", value(row), scope);
        }
    }
}

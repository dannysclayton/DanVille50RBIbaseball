using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed partial class MainForm
    {
        private void ManageInjuredReserve()
        {
            Team? team = SelectedTeam();
            if (team == null)
            {
                MessageBox.Show(this, "Select a team first.");
                return;
            }

            team.Roster ??= new List<Player>();
            team.JvPool ??= new List<Player>();
            team.InjuredReserve ??= new List<Player>();
            using var form = new Form
            {
                Text = "Injured Reserve - " + team.DisplayName,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1180, 570),
                MinimumSize = new Size(980, 500),
                MinimizeBox = false,
                MaximizeBox = true
            };

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            form.Controls.Add(root);

            var status = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Players must be Out for more than 20 games. Select one eligible varsity player and one JV replacement."
            };
            root.Controls.Add(status, 0, 0);

            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            root.Controls.Add(columns, 0, 1);

            DataGridView eligibleGrid = BuildIrPlayerGrid("Eligible Injured Varsity");
            DataGridView jvGrid = BuildIrPlayerGrid("JV Replacement Pool");
            DataGridView irGrid = BuildIrPlayerGrid("Current Injured Reserve");
            columns.Controls.Add(WrapIrGrid("Eligible Injured Varsity", eligibleGrid), 0, 0);
            columns.Controls.Add(WrapIrGrid("JV Replacement Pool", jvGrid), 1, 0);
            columns.Controls.Add(WrapIrGrid("Current Injured Reserve", irGrid), 2, 0);

            void refresh()
            {
                eligibleGrid.Rows.Clear();
                foreach (Player player in team.Roster.Where(IsInjuredReserveEligible).OrderByDescending(IrEligibilityGames).ThenBy(player => player.Name))
                    AddIrPlayerRow(eligibleGrid, player, "Eligible");

                jvGrid.Rows.Clear();
                foreach (Player player in team.JvPool.OrderBy(player => player.Role).ThenBy(player => player.Positions).ThenBy(player => player.Name))
                    AddIrPlayerRow(jvGrid, player, player.RedshirtUsed ? "RS used" : "RS available");

                irGrid.Rows.Clear();
                foreach (Player player in team.InjuredReserve.OrderBy(player => player.Name))
                    AddIrPlayerRow(irGrid, player, player.MedicalTagEligible ? "Medical eligible" : "IR");

                status.Text = "Active " + team.Roster.Count + "/" + PlayerProgressionEngine.TargetRosterSize +
                    " | JV " + team.JvPool.Count + " | IR " + team.InjuredReserve.Count +
                    " | Eligibility requires more than 20 injury games.";
            }

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            AddButton(buttons, "Close", (s, e) => form.Close());
            AddButton(buttons, "Return Cleared Player", (s, e) =>
            {
                if (irGrid.CurrentRow?.Tag is not Player player)
                    return;
                if (player.InjuryStatus == PlayerInjuryStatus.Out && player.InjuryGamesRemaining > 0)
                {
                    MessageBox.Show(form, player.Name + " has not been medically cleared.");
                    return;
                }
                if (team.Roster.Count >= PlayerProgressionEngine.TargetRosterSize)
                {
                    Player? optioned = ChooseVarsityPlayerToOption(form, team, player);
                    if (optioned == null)
                        return;
                    team.Roster.Remove(optioned);
                    optioned.RedshirtActive = false;
                    team.JvPool.Add(optioned);
                }
                team.InjuredReserve.Remove(player);
                team.Roster.Add(player);
                RebuildTeamPlansAfterRosterMove(team);
                MarkDirty(LeagueAutosaveReason.RosterChanged);
                refresh();
                LoadSelectedTeam();
            });
            AddButton(buttons, "Place on IR + Call Up", (s, e) =>
            {
                if (eligibleGrid.CurrentRow?.Tag is not Player injured)
                {
                    MessageBox.Show(form, "Select an eligible injured varsity player.");
                    return;
                }
                if (jvGrid.CurrentRow?.Tag is not Player replacement)
                {
                    MessageBox.Show(form, "Select a JV replacement.");
                    return;
                }
                if (!IsInjuredReserveEligible(injured))
                {
                    MessageBox.Show(form, "That player is no longer eligible for Injured Reserve.");
                    refresh();
                    return;
                }

                int seasonNumber = CurrentRosterManagementSeasonNumber();
                DialogResult confirmation = MessageBox.Show(form,
                    "Place " + injured.Name + " on Injured Reserve and call up " + replacement.Name + " for Season " + seasonNumber + "?\n\n" +
                    injured.Name + " will become Medical tag eligible. " + replacement.Name + " will retain the " +
                    replacement.Classification + " classification and this season will count in the player's varsity career.",
                    "Confirm Injured Reserve move", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (confirmation != DialogResult.OK)
                    return;

                team.Roster.Remove(injured);
                injured.MedicalTagEligible = true;
                injured.InjuredReserveSeasonNumber = seasonNumber;
                injured.RedshirtActive = false;
                if (!team.InjuredReserve.Contains(injured))
                    team.InjuredReserve.Add(injured);

                team.JvPool.Remove(replacement);
                PlayerProgressionEngine.PrepareJvCallUp(replacement, seasonNumber, _rng);
                team.Roster.Add(replacement);
                RebuildTeamPlansAfterRosterMove(team);
                MarkDirty(LeagueAutosaveReason.RosterChanged);
                refresh();
                LoadSelectedTeam();
            });
            root.Controls.Add(buttons, 0, 2);
            refresh();
            form.ShowDialog(this);
            LoadSelectedTeam();
            RefreshHierarchyStatistics();
        }

        private static Player? ChooseVarsityPlayerToOption(IWin32Window owner, Team team, Player returningPlayer)
        {
            using var dialog = new Form
            {
                Text = "Option Player to JV",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(650, 430),
                MinimizeBox = false,
                MaximizeBox = false
            };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "The active roster is full. Select a varsity player to option to JV before returning " + returningPlayer.Name + "."
            }, 0, 0);
            DataGridView grid = BuildIrPlayerGrid("Active varsity players");
            foreach (Player player in (team.Roster ?? new List<Player>()).OrderBy(player => player.Role).ThenBy(player => player.Name))
                AddIrPlayerRow(grid, player, "Active");
            root.Controls.Add(grid, 0, 1);
            Player? selected = null;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            AddButton(buttons, "Cancel", (s, e) => dialog.Close());
            AddButton(buttons, "Option to JV", (s, e) =>
            {
                selected = grid.CurrentRow?.Tag as Player;
                if (selected != null)
                {
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                }
            });
            root.Controls.Add(buttons, 0, 2);
            dialog.Controls.Add(root);
            return dialog.ShowDialog(owner) == DialogResult.OK ? selected : null;
        }

        private static DataGridView BuildIrPlayerGrid(string accessibleName)
        {
            var grid = CreateReadOnlyGrid();
            grid.AccessibleName = accessibleName;
            AddGridColumn(grid, "Name", 150);
            AddGridColumn(grid, "Role", 66);
            AddGridColumn(grid, "Class", 82);
            AddGridColumn(grid, "Pos", 72);
            AddGridColumn(grid, "Out", 48);
            AddGridColumn(grid, "Missed", 58);
            AddGridColumn(grid, "Status", 96);
            return grid;
        }

        private static Control WrapIrGrid(string title, DataGridView grid)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(4) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            panel.Controls.Add(grid, 0, 1);
            return panel;
        }

        private static void AddIrPlayerRow(DataGridView grid, Player player, string status)
        {
            int row = grid.Rows.Add(player.Name, player.Role, player.Classification, player.Positions,
                player.InjuryGamesRemaining, player.InjuryMissedGamesThisSeason, status);
            grid.Rows[row].Tag = player;
        }

        private static bool IsInjuredReserveEligible(Player player)
        {
            if (player == null || player.InjuryStatus != PlayerInjuryStatus.Out || player.MedicalTag || player.MedicalTagEligible)
                return false;
            return IrEligibilityGames(player) > 20;
        }

        private static int IrEligibilityGames(Player player)
            => Math.Max(0, player?.InjuryGamesRemaining ?? 0) + Math.Max(0, player?.InjuryMissedGamesThisSeason ?? 0);

        private int CurrentRosterManagementSeasonNumber()
        {
            var seasons = _league?.Seasons ?? new List<Season>();
            Season? current = seasons.LastOrDefault(season => !season.OffseasonProcessed) ?? seasons.LastOrDefault();
            return current == null ? 1 : CurrentSeasonNumber(current);
        }

        private void RebuildTeamPlansAfterRosterMove(Team team)
        {
            EnsureTeamBaseLineup(team, recalculate: true);
            EnsureTeamPitchingPlan(team, recalculate: true);
            SaveTeamBaseLineupFile(team);
            SaveTeamPitchingPlanFile(team);
        }
    }
}

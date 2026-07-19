using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class ContextHelpItem
    {
        public string Section { get; set; } = "General";
        public string ControlName { get; set; } = "";
        public string Instructions { get; set; } = "";
    }

    internal static class ContextHelpService
    {
        internal const string HelpButtonName = "ContextualMenuHelpButton";
        private static readonly HashSet<Form> AttachedForms = new HashSet<Form>();
        private static bool _installed;

        public static void Install()
        {
            if (_installed)
                return;
            _installed = true;
            Application.Idle += (_, _) => AttachToOpenForms();
        }

        internal static void AttachToOpenForms()
        {
            AttachedForms.RemoveWhere(form => form.IsDisposed);
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
                Attach(form);
        }

        internal static bool Attach(Form form)
        {
            if (form == null || form.IsDisposed || !ShouldAttach(form) || AttachedForms.Contains(form))
                return false;

            var help = new Button
            {
                Name = HelpButtonName,
                Text = "?  Help",
                Size = new Size(92, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(20, 70, 130),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                AccessibleName = "Help for this screen",
                AccessibleDescription = "Opens instructions for every control on the current screen.",
                TabStop = true
            };
            help.FlatAppearance.BorderColor = Color.FromArgb(150, 195, 235);

            void PositionButton()
            {
                int menuOffset = form.MainMenuStrip?.Height ?? 0;
                help.Location = new Point(Math.Max(8, form.ClientSize.Width - help.Width - 12), menuOffset + 8);
                help.BringToFront();
            }

            help.Click += (_, _) =>
            {
                IReadOnlyList<ContextHelpItem> items = ContextHelpContentBuilder.Build(form);
                using var dialog = new ContextHelpDialog(form.Text, ContextHelpContentBuilder.Overview(form), items);
                dialog.ShowDialog(form);
            };
            form.Controls.Add(help);
            PositionButton();
            form.Resize += (_, _) => PositionButton();
            form.Shown += (_, _) => PositionButton();
            form.FormClosed += (_, _) => AttachedForms.Remove(form);
            new ToolTip().SetToolTip(help, "Explain every control on this screen");
            AttachedForms.Add(form);
            return true;
        }

        private static bool ShouldAttach(Form form)
        {
            return form is not ContextHelpDialog &&
                   form is not LaunchForm &&
                   form is not LoadingTransitionForm &&
                   form is not GameLoadingForm &&
                   form is not TimedImageInterstitialForm &&
                   form is not CutscenePlaybackForm &&
                   form is not NationalAnthemForm;
        }
    }

    internal static class ContextHelpContentBuilder
    {
        public static IReadOnlyList<ContextHelpItem> Build(Form form)
        {
            var items = new List<ContextHelpItem>();
            AddScreenSpecificItems(form, items);
            foreach (Control control in Descendants(form))
            {
                if (control.Name == ContextHelpService.HelpButtonName || control is Label || control is GroupBox || control is Panel)
                    continue;

                if (control is TabControl tabs)
                {
                    foreach (TabPage page in tabs.TabPages)
                    {
                        items.Add(new ContextHelpItem
                        {
                            Section = "Navigation",
                            ControlName = "Tab: " + Clean(page.Text),
                            Instructions = "Select this tab to open the " + Clean(page.Text).ToLowerInvariant() + " tools and information."
                        });
                    }
                    continue;
                }

                string? instructions = InstructionsFor(control);
                if (string.IsNullOrWhiteSpace(instructions))
                    continue;
                if (!control.Enabled)
                    instructions += " It is currently unavailable because another required selection or game condition has not been met.";

                items.Add(new ContextHelpItem
                {
                    Section = SectionFor(control),
                    ControlName = NameFor(control),
                    Instructions = instructions
                });
            }

            foreach (MenuStrip menu in Descendants(form).OfType<MenuStrip>())
                AddMenuItems(items, menu.Items, "Menu");

            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.ControlName))
                .GroupBy(item => item.Section + "\u001f" + item.ControlName + "\u001f" + item.Instructions, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.Section, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ControlName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddScreenSpecificItems(Form form, ICollection<ContextHelpItem> items)
        {
            if (form is MainMenuForm)
            {
                items.Add(new ContextHelpItem { Section = "Main Menu", ControlName = "Start Dynasty", Instructions = "Create a new named dynasty, choose its owner, league structure, teams, schedule, rules, user-controlled teams, and replay-export preferences." });
                items.Add(new ContextHelpItem { Section = "Main Menu", ControlName = "Continue Dynasty", Instructions = "Open an existing dynasty save. If its primary save is damaged, the backup-recovery system can offer available recovery copies." });
                items.Add(new ContextHelpItem { Section = "Main Menu", ControlName = "Game", Instructions = "Open the Game tab to select a scheduled matchup and play, watch, simulate, or resume it using the selected teams and rules." });
                items.Add(new ContextHelpItem { Section = "Main Menu", ControlName = "Teams", Instructions = "Open team management to create or edit teams, rosters, coaches, lineups, rotations, uniforms, media, fields, scoreboards, and team history." });
                items.Add(new ContextHelpItem { Section = "Main Menu", ControlName = "Seasons", Instructions = "Open season and dynasty history, schedules, standings, polls, statistics, playoffs, awards, records, Hall of Fame information, and archives." });
                items.Add(new ContextHelpItem { Section = "Main Menu", ControlName = "Replays", Instructions = "Open the portable replay library to import, validate, organize, and watch replay files stored with the game." });
                items.Add(new ContextHelpItem { Section = "Main Menu", ControlName = "Settings", Instructions = "Configure asset-library locations, cutscene defaults, uniform rotation, audio levels, controller behavior, and other application preferences." });
                items.Add(new ContextHelpItem { Section = "Main Menu", ControlName = "Keyboard and controller navigation", Instructions = "Use the mouse, Up/Down or W/S, or the PlayStation 3 directional buttons/Left Stick to select an option. Use Enter, Space, Cross, or Start to open it." });
            }

            if (form is GameplayForm)
            {
                foreach (PlayStation3ControlBinding binding in PlayStation3ControllerProfile.Bindings)
                {
                    items.Add(new ContextHelpItem
                    {
                        Section = "PlayStation 3 - " + binding.Context,
                        ControlName = binding.Control,
                        Instructions = binding.Action + "."
                    });
                }
            }
        }

        public static string Overview(Form form)
        {
            if (form is MainMenuForm)
                return "Choose a destination with the mouse, keyboard arrows, or PlayStation 3 directional buttons/Left Stick. Press Enter, Space, Cross, or Start to open the selected item.";
            if (form is MainForm)
                return "Use the main tabs to manage teams, schedules, games, statistics, playoffs, awards, records, replays, and dynasty history. Changes that affect the dynasty are saved through the dynasty save system.";
            if (form is GameplayForm)
                return "This screen runs the live game. Use Game Controls for input mode and saving, then use the available offensive, defensive, steal-defense, and coaching commands as game situations change.";
            if (form is LiveSimulationForm)
                return "This screen shows a live computer simulation. Use its playback controls to follow the score, scoreboard, and play-by-play as the shared game engine resolves each play.";
            if (form.Text.Contains("Settings", StringComparison.OrdinalIgnoreCase))
                return "Settings on this screen change application, media, cutscene, uniform-rotation, controller, and asset-library behavior. Open each tab to review its available settings.";
            return "The list below describes every interactive control currently defined on this screen, including controls located on other tabs.";
        }

        private static string? InstructionsFor(Control control)
        {
            if (!string.IsNullOrWhiteSpace(control.AccessibleDescription))
                return control.AccessibleDescription.Trim();

            string name = NameFor(control);
            string lower = name.ToLowerInvariant();
            if (control is Button)
                return ButtonInstructions(lower, name);
            if (control is CheckBox)
                return "Check this box to enable " + lower + "; clear it to disable that setting.";
            if (control is RadioButton)
                return "Select this option to use " + lower + " instead of the other choices in its group.";
            if (control is ComboBox)
                return "Open the list and choose the " + lower + " value to use.";
            if (control is NumericUpDown number)
                return "Type a value or use the arrows to set " + lower + " from " + number.Minimum + " through " + number.Maximum + ".";
            if (control is TrackBar slider)
                return "Move the slider to adjust " + lower + " from " + slider.Minimum + " through " + slider.Maximum + ".";
            if (control is DateTimePicker)
                return "Choose the date or time used for " + lower + ".";
            if (control is TextBoxBase text)
                return text.Multiline
                    ? "Enter or edit the multi-line text used for " + lower + "."
                    : "Enter or edit the value used for " + lower + ".";
            if (control is CheckedListBox)
                return "Select entries and check each item that should be included in " + lower + ".";
            if (control is ListBox)
                return "Select an item from this list to view or use it for " + lower + ".";
            if (control is TreeView)
                return "Expand the branches and select an entry to navigate or edit " + lower + ".";
            if (control is DataGridView grid)
            {
                string action = grid.ReadOnly ? "review" : "review or edit";
                return "Use this table to " + action + " " + lower + ". Click a column heading to sort when sorting is enabled, and use the scroll bars to reach additional rows or columns.";
            }
            if (control is PropertyGrid)
                return "Select a property row and edit its value to customize " + lower + ".";
            return null;
        }

        private static string ButtonInstructions(string lower, string display)
        {
            if (lower.Contains("close") || lower.Contains("cancel") || lower.Contains("dismiss"))
                return "Close this screen without performing another command.";
            if (lower.Contains("save") || lower.Contains("commit"))
                return "Save or commit the current " + Subject(lower) + ".";
            if (lower.Contains("export"))
                return "Export the current " + Subject(lower) + " to the file format and location you choose.";
            if (lower.Contains("import") || lower.Contains("upload"))
                return "Choose an external file and import it into the current " + Subject(lower) + ".";
            if (lower.Contains("browse") || lower.Contains("choose") || lower.Contains("select") || lower.Contains("photo") || lower.Contains("logo"))
                return "Open the appropriate selector and choose the item used for " + lower + ".";
            if (lower.Contains("add") || lower.Contains("new") || lower.Contains("create"))
                return "Create or add a new " + Subject(lower) + ".";
            if (lower.Contains("remove") || lower.Contains("delete") || lower.Contains("clear"))
                return "Remove the selected " + Subject(lower) + ".";
            if (lower.Contains("edit") || lower.Contains("update") || lower.Contains("apply"))
                return "Apply changes to the selected " + Subject(lower) + ".";
            if (lower.Contains("play") || lower.Contains("start") || lower.Contains("continue") || lower.Contains("watch") || lower.Contains("sim"))
                return "Begin or continue the selected " + Subject(lower) + ".";
            if (lower.Contains("refresh") || lower.Contains("reload"))
                return "Reload this screen from the current saved information.";
            if (lower.Contains("full screen"))
                return "Toggle this screen between windowed and full-screen display.";
            return "Select this command to perform “" + display + "” on the current screen.";
        }

        private static string Subject(string text)
        {
            string cleaned = Regex.Replace(text, "\\b(save|commit|export|import|upload|add|new|create|remove|delete|clear|edit|update|apply|play|start|continue|watch|sim|to|in-game)\\b", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim(' ', '.', ':');
            return string.IsNullOrWhiteSpace(cleaned) ? "information" : cleaned;
        }

        private static string SectionFor(Control control)
        {
            Control? current = control.Parent;
            while (current != null)
            {
                if (current is TabPage tab && !string.IsNullOrWhiteSpace(tab.Text))
                    return Clean(tab.Text);
                if (current is GroupBox group && !string.IsNullOrWhiteSpace(group.Text))
                    return Clean(group.Text);
                current = current.Parent;
            }
            return "General";
        }

        private static string NameFor(Control control)
        {
            string text = Clean(control.Text);
            if (!string.IsNullOrWhiteSpace(text) &&
                (control is Button || control is CheckBox || control is RadioButton || control is TabPage))
                return text;

            Label? label = ClosestLabel(control);
            if (label != null)
                return Clean(label.Text);
            if (!string.IsNullOrWhiteSpace(control.AccessibleName))
                return Clean(control.AccessibleName);
            return Humanize(control.Name, control.GetType().Name);
        }

        private static Label? ClosestLabel(Control control)
        {
            if (control.Parent == null)
                return null;
            return control.Parent.Controls.OfType<Label>()
                .Where(label => !string.IsNullOrWhiteSpace(label.Text))
                .OrderBy(label => Math.Abs(label.Top - control.Top) + Math.Abs(label.Right - control.Left))
                .FirstOrDefault();
        }

        private static string Humanize(string name, string fallback)
        {
            string value = string.IsNullOrWhiteSpace(name) ? fallback : name.TrimStart('_');
            value = Regex.Replace(value, "(Box|Button|Combo|Grid|List|Picker|Control)$", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1 $2");
            value = value.Replace('_', ' ').Trim();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string Clean(string? text)
        {
            const string escapedAmpersand = "\u001f";
            string cleaned = (text ?? "").Replace("&&", escapedAmpersand).Replace("&", "").Replace(escapedAmpersand, "&");
            return Regex.Replace(cleaned, "\\s+", " ").Trim();
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

        private static void AddMenuItems(ICollection<ContextHelpItem> items, ToolStripItemCollection menuItems, string section)
        {
            foreach (ToolStripItem item in menuItems)
            {
                if (item is ToolStripSeparator || string.IsNullOrWhiteSpace(item.Text))
                    continue;
                string name = Clean(item.Text);
                items.Add(new ContextHelpItem
                {
                    Section = section,
                    ControlName = name,
                    Instructions = item is ToolStripMenuItem menu && menu.HasDropDownItems
                        ? "Open this menu to view the available " + name.ToLowerInvariant() + " commands."
                        : "Select this menu command to perform “" + name + "”."
                });
                if (item is ToolStripMenuItem submenu && submenu.HasDropDownItems)
                    AddMenuItems(items, submenu.DropDownItems, section + " > " + name);
            }
        }
    }

    internal sealed class ContextHelpDialog : Form
    {
        private readonly IReadOnlyList<ContextHelpItem> _allItems;
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _search = new TextBox();

        public ContextHelpDialog(string screenTitle, string overview, IReadOnlyList<ContextHelpItem> items)
        {
            _allItems = items ?? Array.Empty<ContextHelpItem>();
            Text = "Help - " + (string.IsNullOrWhiteSpace(screenTitle) ? "Current Screen" : screenTitle);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 520);
            Size = new Size(980, 720);
            BackColor = Color.FromArgb(28, 30, 34);
            ForeColor = Color.White;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var introduction = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(920, 0),
                Text = overview + Environment.NewLine + Environment.NewLine +
                       "Unavailable controls are explained too; they become active after the required selection or game condition is met.",
                Padding = new Padding(4, 2, 4, 12)
            };
            root.Controls.Add(introduction, 0, 0);

            var searchRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            searchRow.Controls.Add(new Label { Text = "Search help", AutoSize = true, Margin = new Padding(0, 7, 8, 0) });
            _search.Width = 360;
            _search.PlaceholderText = "Type a setting, command, tab, or field name";
            _search.TextChanged += (_, _) => RefreshRows();
            searchRow.Controls.Add(_search);
            root.Controls.Add(searchRow, 0, 1);

            ConfigureGrid();
            root.Controls.Add(_grid, 0, 2);

            var close = new Button { Text = "Close", AutoSize = true, Padding = new Padding(14, 5, 14, 5) };
            close.Click += (_, _) => Close();
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(close);
            root.Controls.Add(buttons, 0, 3);
            Controls.Add(root);
            AcceptButton = close;
            CancelButton = close;
            RefreshRows();
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = true;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.BackgroundColor = Color.FromArgb(22, 24, 28);
            _grid.ForeColor = Color.White;
            _grid.DefaultCellStyle.BackColor = Color.FromArgb(35, 38, 44);
            _grid.DefaultCellStyle.ForeColor = Color.White;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 88, 150);
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(18, 57, 100);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.EnableHeadersVisualStyles = false;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Section", HeaderText = "Section", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Control", HeaderText = "Setting or command", Width = 220 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Instructions", HeaderText = "How to use it", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        }

        private void RefreshRows()
        {
            string filter = _search.Text.Trim();
            IEnumerable<ContextHelpItem> filtered = _allItems;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                filtered = filtered.Where(item =>
                    item.Section.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.ControlName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Instructions.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            _grid.Rows.Clear();
            foreach (ContextHelpItem item in filtered)
                _grid.Rows.Add(item.Section, item.ControlName, item.Instructions);
        }
    }
}

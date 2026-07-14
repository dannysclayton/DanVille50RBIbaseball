using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class TeamScoreboardTemplateDialog : Form
    {
        private readonly Team _team;
        private readonly string _logoPath;
        private readonly CheckBox _enabledBox = new CheckBox();
        private readonly TextBox _schoolBox = new TextBox();
        private readonly TextBox _abbrBox = new TextBox();
        private readonly TextBox _mascotBox = new TextBox();
        private readonly TextBox _adsBox = new TextBox();
        private readonly ComboBox _layoutCombo = new ComboBox();
        private readonly Panel _boardColor1 = new Panel();
        private readonly Panel _boardColor2 = new Panel();
        private readonly Panel _boardColor3 = new Panel();
        private readonly Panel _boardColor4 = new Panel();
        private readonly Panel _accentColor = new Panel();
        private readonly Panel _textColor = new Panel();
        private readonly Panel _adColor = new Panel();
        private readonly Panel _preview = new Panel();
        private bool _loading;

        public bool Modified { get; private set; }

        public TeamScoreboardTemplateDialog(Team team, string logoPath)
        {
            _team = team ?? throw new ArgumentNullException(nameof(team));
            _logoPath = logoPath ?? "";
            _team.ScoreboardTemplate ??= new TeamScoreboardTemplate();
            _team.ScoreboardTemplate.Normalize(_team);

            Text = "Home Scoreboard Template";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 700);
            MinimumSize = new Size(820, 600);
            BuildUi();
            LoadTemplate();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 15 };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 12; i++)
                editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.Controls.Add(editor, 0, 0);

            _enabledBox.Text = "Use for home games";
            _enabledBox.Dock = DockStyle.Fill;
            _enabledBox.CheckedChanged += (s, e) => SaveTemplate();
            editor.Controls.Add(_enabledBox, 0, 0);
            editor.SetColumnSpan(_enabledBox, 2);

            AddLabeled(editor, "School", _schoolBox, 1);
            AddLabeled(editor, "EV Text", _abbrBox, 2);
            AddLabeled(editor, "Mascot", _mascotBox, 3);
            _abbrBox.MaxLength = Team.MaxScoreboardAbbreviationLength;
            _schoolBox.TextChanged += (s, e) => SaveTemplate();
            _abbrBox.TextChanged += (s, e) => SaveTemplate();
            _mascotBox.TextChanged += (s, e) => SaveTemplate();

            _layoutCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _layoutCombo.Items.AddRange(Enum.GetValues(typeof(ScoreboardBoardColorLayout)).Cast<object>().ToArray());
            _layoutCombo.SelectedIndexChanged += (s, e) => SaveTemplate();
            AddLabeled(editor, "Layout", _layoutCombo, 4);

            AddColorRow(editor, "Board 1", _boardColor1, 5, () => _team.ScoreboardTemplate.BoardArgb, c => _team.ScoreboardTemplate.BoardArgb = c.ToArgb());
            AddColorRow(editor, "Board 2", _boardColor2, 6, () => _team.ScoreboardTemplate.BoardSecondArgb, c => _team.ScoreboardTemplate.BoardSecondArgb = c.ToArgb());
            AddColorRow(editor, "Board 3", _boardColor3, 7, () => _team.ScoreboardTemplate.BoardThirdArgb, c => _team.ScoreboardTemplate.BoardThirdArgb = c.ToArgb());
            AddColorRow(editor, "Board 4", _boardColor4, 8, () => _team.ScoreboardTemplate.BoardFourthArgb, c => _team.ScoreboardTemplate.BoardFourthArgb = c.ToArgb());
            AddColorRow(editor, "Accent", _accentColor, 9, () => _team.ScoreboardTemplate.AccentArgb, c => _team.ScoreboardTemplate.AccentArgb = c.ToArgb());
            AddColorRow(editor, "Text", _textColor, 10, () => _team.ScoreboardTemplate.TextArgb, c => _team.ScoreboardTemplate.TextArgb = c.ToArgb());
            AddColorRow(editor, "Ad Strip", _adColor, 11, () => _team.ScoreboardTemplate.AdStripArgb, c => _team.ScoreboardTemplate.AdStripArgb = c.ToArgb());

            editor.Controls.Add(new Label { Text = "Ads", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0, 7, 0, 0) }, 0, 12);
            _adsBox.Dock = DockStyle.Fill;
            _adsBox.Multiline = true;
            _adsBox.ScrollBars = ScrollBars.Vertical;
            _adsBox.TextChanged += (s, e) => SaveTemplate();
            editor.Controls.Add(_adsBox, 1, 12);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            AddButton(actions, "Team Colors", (s, e) =>
            {
                _team.ScoreboardTemplate.BoardColorLayout = ScoreboardBoardColorLayout.VerticalHalves;
                _team.ScoreboardTemplate.BoardArgb = _team.PrimaryArgb;
                _team.ScoreboardTemplate.BoardSecondArgb = _team.SecondaryArgb;
                _team.ScoreboardTemplate.BoardThirdArgb = _team.PrimaryArgb;
                _team.ScoreboardTemplate.BoardFourthArgb = _team.SecondaryArgb;
                _team.ScoreboardTemplate.AccentArgb = _team.SecondaryArgb;
                _team.ScoreboardTemplate.TextArgb = ReadableTextColor(Color.FromArgb(_team.PrimaryArgb)).ToArgb();
                LoadTemplate();
                SaveTemplate();
            });
            AddButton(actions, "Reset Text", (s, e) =>
            {
                _schoolBox.Text = _team.City;
                _abbrBox.Text = _team.ScoreboardName;
                _mascotBox.Text = _team.Nickname;
                SaveTemplate();
            });
            editor.Controls.Add(actions, 0, 13);
            editor.SetColumnSpan(actions, 2);

            var closeRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            AddButton(closeRow, "Close", (s, e) => Close());
            editor.Controls.Add(closeRow, 0, 14);
            editor.SetColumnSpan(closeRow, 2);

            _preview.Dock = DockStyle.Fill;
            _preview.BackColor = Color.Black;
            _preview.Paint += (s, e) => ScoreboardTemplateRenderer.Draw(
                e.Graphics,
                _preview.ClientRectangle,
                _team,
                _logoPath,
                _team.ScoreboardName + " 3  -  VIS 2",
                "Bottom 5");
            root.Controls.Add(_preview, 1, 0);
        }

        private void LoadTemplate()
        {
            _loading = true;
            var template = _team.ScoreboardTemplate;
            _enabledBox.Checked = template.Enabled;
            _schoolBox.Text = template.SchoolNameText;
            _abbrBox.Text = template.PreferredAbbreviation;
            _mascotBox.Text = template.MascotText;
            _layoutCombo.SelectedItem = template.BoardColorLayout;
            _adsBox.Text = string.Join(Environment.NewLine, template.Ads ?? new System.Collections.Generic.List<string>());
            ApplyColorPanels();
            _loading = false;
            _preview.Invalidate();
        }

        private void SaveTemplate()
        {
            if (_loading)
                return;

            var template = _team.ScoreboardTemplate;
            template.Enabled = _enabledBox.Checked;
            template.SchoolNameText = _schoolBox.Text.Trim();
            template.PreferredAbbreviation = Team.Limit(_abbrBox.Text.Trim(), Team.MaxScoreboardAbbreviationLength).ToUpperInvariant();
            template.MascotText = _mascotBox.Text.Trim();
            if (_layoutCombo.SelectedItem is ScoreboardBoardColorLayout layout)
                template.BoardColorLayout = layout;
            template.Ads = _adsBox.Lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).Take(8).ToList();
            template.Normalize(_team);
            Modified = true;
            ApplyColorPanels();
            _preview.Invalidate();
        }

        private void AddColorRow(TableLayoutPanel panel, string label, Panel swatch, int row, Func<int> getter, Action<Color> setter)
        {
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            var host = new FlowLayoutPanel { Dock = DockStyle.Fill };
            swatch.Width = 38;
            swatch.Height = 24;
            swatch.BorderStyle = BorderStyle.FixedSingle;
            swatch.Margin = new Padding(0, 5, 8, 0);
            host.Controls.Add(swatch);
            AddButton(host, "Pick...", (s, e) =>
            {
                using var dlg = new ColorDialog { Color = Color.FromArgb(getter()), FullOpen = true };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                setter(dlg.Color);
                Modified = true;
                ApplyColorPanels();
                _preview.Invalidate();
            });
            panel.Controls.Add(host, 1, row);
        }

        private void ApplyColorPanels()
        {
            _boardColor1.BackColor = Color.FromArgb(_team.ScoreboardTemplate.BoardArgb);
            _boardColor2.BackColor = Color.FromArgb(_team.ScoreboardTemplate.BoardSecondArgb);
            _boardColor3.BackColor = Color.FromArgb(_team.ScoreboardTemplate.BoardThirdArgb);
            _boardColor4.BackColor = Color.FromArgb(_team.ScoreboardTemplate.BoardFourthArgb);
            _accentColor.BackColor = Color.FromArgb(_team.ScoreboardTemplate.AccentArgb);
            _textColor.BackColor = Color.FromArgb(_team.ScoreboardTemplate.TextArgb);
            _adColor.BackColor = Color.FromArgb(_team.ScoreboardTemplate.AdStripArgb);
        }

        private static void AddLabeled(TableLayoutPanel panel, string label, Control control, int row)
        {
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            control.Dock = DockStyle.Fill;
            panel.Controls.Add(control, 1, row);
        }

        private static Button AddButton(Control host, string text, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding(4) };
            button.Click += click;
            host.Controls.Add(button);
            return button;
        }

        private static Color ReadableTextColor(Color background)
        {
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return brightness >= 145 ? Color.FromArgb(20, 24, 32) : Color.White;
        }
    }
}

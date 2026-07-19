using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class SpriteCreatorDialog : Form
    {
        private readonly Team _team;
        private readonly Player? _player;
        private readonly string _outputDir;
        private readonly List<string> _sourcePaths = new List<string>();
        private readonly ListBox _sourceList;
        private readonly PictureBox _preview;
        private readonly RadioButton _teamTarget;
        private readonly RadioButton _playerTarget;
        private Bitmap? _previewImage;

        public string SavedSpriteSheetPath { get; private set; } = "";
        public bool SavedForPlayer => _playerTarget.Checked && _player != null;

        public SpriteCreatorDialog(Team team, Player? player, string outputDir, string initialDirectory)
        {
            _team = team ?? throw new ArgumentNullException(nameof(team));
            _player = player;
            _outputDir = outputDir ?? throw new ArgumentNullException(nameof(outputDir));

            Text = "Sprite Creator - " + team.DisplayName;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            Width = 900;
            Height = 620;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(root);

            var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, ColumnCount = 1 };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.Controls.Add(left, 0, 0);

            left.Controls.Add(new Label { Text = "Output Target", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _teamTarget = new RadioButton { Text = "Team sprite page", Dock = DockStyle.Fill, Checked = true };
            _playerTarget = new RadioButton
            {
                Text = _player == null ? "Selected player sprite page" : "Selected player: " + _player.Name,
                Dock = DockStyle.Fill,
                Enabled = _player != null
            };
            left.Controls.Add(_teamTarget, 0, 1);
            left.Controls.Add(_playerTarget, 0, 2);

            var addPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var add = new Button { Text = "Add Images...", AutoSize = true };
            var clear = new Button { Text = "Clear", AutoSize = true };
            add.Click += (s, e) => AddImages(initialDirectory);
            clear.Click += (s, e) => { _sourcePaths.Clear(); RefreshSources(); GeneratePreview(); };
            addPanel.Controls.Add(add);
            addPanel.Controls.Add(clear);
            left.Controls.Add(addPanel, 0, 3);

            _sourceList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            left.Controls.Add(_sourceList, 0, 4);

            var previewButton = new Button { Text = "Generate Preview", Dock = DockStyle.Fill };
            previewButton.Click += (s, e) => GeneratePreview();
            left.Controls.Add(previewButton, 0, 5);

            left.Controls.Add(new Label
            {
                Text = "The sheet is 4 x 5 frames at 64 x 64 pixels. Added images are fit into frames; empty frames use a generated RBI-style player.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            }, 0, 6);

            _preview = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 32, 40),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            root.Controls.Add(_preview, 1, 0);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100 };
            var save = new Button { Text = "Save Sprite Page", Width = 140 };
            save.Click += (s, e) => SaveSpritePage();
            bottom.Controls.Add(cancel);
            bottom.Controls.Add(save);
            root.Controls.Add(bottom, 0, 1);
            root.SetColumnSpan(bottom, 2);

            AcceptButton = save;
            CancelButton = cancel;
            GeneratePreview();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _previewImage?.Dispose();
            base.Dispose(disposing);
        }

        private void AddImages(string initialDirectory)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Choose player images for the sprite generator",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
                Multiselect = true,
                InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            foreach (string path in dlg.FileNames.Where(File.Exists))
            {
                if (!_sourcePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    _sourcePaths.Add(path);
            }

            RefreshSources();
            GeneratePreview();
        }

        private void RefreshSources()
        {
            _sourceList.Items.Clear();
            foreach (string path in _sourcePaths)
                _sourceList.Items.Add(Path.GetFileName(path));
        }

        private void GeneratePreview()
        {
            Bitmap sheet = SpriteSheetGenerator.Generate(BuildOptions());
            _previewImage?.Dispose();
            _previewImage = sheet;
            _preview.Image = _previewImage;
        }

        private void SaveSpritePage()
        {
            Directory.CreateDirectory(_outputDir);
            string baseName = SavedForPlayer
                ? Sanitize(_player?.Name ?? "player") + "_sprite_page.png"
                : "team_sprite_page.png";
            string outputPath = Path.Combine(_outputDir, baseName);

            using Bitmap sheet = SpriteSheetGenerator.Generate(BuildOptions());
            SpriteSheetGenerator.SavePng(sheet, outputPath);
            SavedSpriteSheetPath = outputPath;
            DialogResult = DialogResult.OK;
            Close();
        }

        private SpriteSheetGeneratorOptions BuildOptions()
        {
            return new SpriteSheetGeneratorOptions
            {
                Team = _team,
                Player = SavedForPlayer ? _player : null,
                SourceImagePaths = _sourcePaths.ToArray(),
                Label = SavedForPlayer ? _player?.Name ?? "" : _team.DisplayName,
                CleanGameplayFrames = true
            };
        }

        private static string Sanitize(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "player" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }
    }
}

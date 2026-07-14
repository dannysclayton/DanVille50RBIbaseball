using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class TeamMusicPickerDialog : Form
    {
        private static readonly string[] AudioExtensions = { ".mp3", ".wav", ".wma" };
        private readonly string _playlistDir;
        private readonly string _importInitialDir;
        private readonly CheckedListBox _trackList = new CheckedListBox();
        private readonly Label _folderLabel = new Label();

        public TeamMusicPickerDialog(string teamName, string playlistDir, IEnumerable<string> selectedTracks, string importInitialDir)
        {
            _playlistDir = playlistDir;
            _importInitialDir = importInitialDir;
            SelectedTracks = (selectedTracks ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();

            Text = "Team Music - " + teamName;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 460);
            MinimumSize = new Size(540, 360);

            Directory.CreateDirectory(_playlistDir);
            BuildLayout();
            LoadTracks();
        }

        public List<string> SelectedTracks { get; private set; }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _folderLabel.AutoSize = true;
            _folderLabel.Text = "Playlist folder: " + _playlistDir;
            root.Controls.Add(_folderLabel, 0, 0);

            _trackList.Dock = DockStyle.Fill;
            _trackList.CheckOnClick = true;
            _trackList.HorizontalScrollbar = true;
            root.Controls.Add(_trackList, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            AddButton(buttons, "OK", (s, e) => AcceptSelection());
            AddButton(buttons, "Cancel", (s, e) => DialogResult = DialogResult.Cancel);
            AddButton(buttons, "Open Folder", (s, e) => OpenPlaylistFolder());
            AddButton(buttons, "Import Tracks...", (s, e) => ImportTracks());
            root.Controls.Add(buttons, 0, 2);

            Controls.Add(root);
        }

        private static Button AddButton(Control host, string text, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(6)
            };
            button.Click += click;
            host.Controls.Add(button);
            return button;
        }

        private void LoadTracks()
        {
            _trackList.Items.Clear();
            var selected = new HashSet<string>(SelectedTracks, StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.EnumerateFiles(_playlistDir)
                         .Where(IsAudioFile)
                         .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                int index = _trackList.Items.Add(new AudioTrackItem(path));
                _trackList.SetItemChecked(index, selected.Contains(path));
            }
        }

        private void ImportTracks()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Import music to team playlist",
                Filter = "Audio files|*.mp3;*.wav;*.wma|All files (*.*)|*.*",
                Multiselect = true,
                InitialDirectory = Directory.Exists(_importInitialDir) ? _importInitialDir : _playlistDir
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            foreach (string source in dlg.FileNames.Where(IsAudioFile))
            {
                string dest = UniqueDestinationPath(Path.Combine(_playlistDir, Path.GetFileName(source)));
                File.Copy(source, dest, false);
            }

            LoadTracks();
        }

        private void OpenPlaylistFolder()
        {
            Directory.CreateDirectory(_playlistDir);
            Process.Start(new ProcessStartInfo
            {
                FileName = _playlistDir,
                UseShellExecute = true
            });
        }

        private void AcceptSelection()
        {
            SelectedTracks = _trackList.CheckedItems
                .OfType<AudioTrackItem>()
                .Select(item => item.Path)
                .ToList();
            DialogResult = DialogResult.OK;
        }

        private static bool IsAudioFile(string path)
        {
            string ext = Path.GetExtension(path);
            return AudioExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private static string UniqueDestinationPath(string path)
        {
            if (!File.Exists(path))
                return path;

            string dir = Path.GetDirectoryName(path) ?? ".";
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 2; ; i++)
            {
                string candidate = Path.Combine(dir, name + " (" + i + ")" + ext);
                if (!File.Exists(candidate))
                    return candidate;
            }
        }

        private sealed class AudioTrackItem
        {
            public AudioTrackItem(string path)
            {
                Path = path;
            }

            public string Path { get; }

            public override string ToString() => System.IO.Path.GetFileNameWithoutExtension(Path);
        }
    }
}

#nullable enable annotations

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public static class AssetLibraryManager
    {
        public static readonly string[] StandardFolders = { "Audio", "Images", "Video", "Teams" };

        public static string Create(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Choose a location for the asset library.", nameof(path));

            string fullPath = Path.GetFullPath(path.Trim());
            Directory.CreateDirectory(fullPath);
            foreach (string folder in StandardFolders)
                Directory.CreateDirectory(Path.Combine(fullPath, folder));
            return fullPath;
        }
    }

    public sealed class AssetLibrarySetupDialog : Form
    {
        private readonly ComboBox _modeBox;
        private readonly TextBox _pathBox;
        private readonly Button _acceptButton;

        public string SelectedPath { get; private set; }

        public AssetLibrarySetupDialog(string? currentPath = null)
        {
            Text = "Set Up Asset Library";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(650, 218);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Asset Library",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold)
            }, 0, 0);

            _modeBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                Anchor = AnchorStyles.Left
            };
            _modeBox.Items.AddRange(new object[] { "Create New Library", "Use Existing Library" });
            _modeBox.SelectedIndex = Directory.Exists(currentPath) ? 1 : 0;
            _modeBox.SelectedIndexChanged += (s, e) => UpdateMode();
            root.Controls.Add(_modeBox, 0, 1);

            var pathRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            _pathBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 9, 8, 9) };
            _pathBox.Text = string.IsNullOrWhiteSpace(currentPath) ? DefaultNewLibraryPath() : currentPath.Trim();
            var browseButton = new Button { Text = "Browse...", Dock = DockStyle.Fill, Margin = new Padding(0, 7, 0, 7) };
            browseButton.Click += (s, e) => BrowseForLocation();
            pathRow.Controls.Add(_pathBox, 0, 0);
            pathRow.Controls.Add(browseButton, 1, 0);
            root.Controls.Add(pathRow, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            _acceptButton = new Button { AutoSize = true };
            _acceptButton.Click += (s, e) => AcceptSelection();
            var cancelButton = new Button { Text = "Cancel", AutoSize = true };
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            buttons.Controls.Add(_acceptButton);
            buttons.Controls.Add(cancelButton);
            root.Controls.Add(buttons, 0, 3);

            AcceptButton = _acceptButton;
            CancelButton = cancelButton;
            UpdateMode();
        }

        private bool IsCreateMode => _modeBox.SelectedIndex == 0;

        private void UpdateMode()
        {
            _acceptButton.Text = IsCreateMode ? "Create Library" : "Use Library";
            if (IsCreateMode && string.IsNullOrWhiteSpace(_pathBox.Text))
                _pathBox.Text = DefaultNewLibraryPath();
        }

        private void BrowseForLocation()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = IsCreateMode
                    ? "Choose the parent folder where the asset library will be created."
                    : "Choose an existing asset library folder.",
                SelectedPath = BrowseStartPath()
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            _pathBox.Text = IsCreateMode
                ? Path.Combine(dialog.SelectedPath, "Dan's RBI Baseball 2026 Asset Library")
                : dialog.SelectedPath;
        }

        private string BrowseStartPath()
        {
            string path = (_pathBox.Text ?? "").Trim();
            if (Directory.Exists(path))
                return path;
            string parent = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
            return Directory.Exists(parent) ? parent : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private void AcceptSelection()
        {
            try
            {
                string requestedPath = (_pathBox.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(requestedPath))
                    throw new ArgumentException("Choose a location for the asset library.");

                string path = Path.GetFullPath(requestedPath);
                if (IsCreateMode)
                    path = AssetLibraryManager.Create(path);
                else if (!Directory.Exists(path))
                    throw new DirectoryNotFoundException("Choose an existing asset library folder.");

                SelectedPath = path;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) when (ex is ArgumentException
                || ex is DirectoryNotFoundException
                || ex is IOException
                || ex is UnauthorizedAccessException
                || ex is NotSupportedException)
            {
                MessageBox.Show(this, ex.Message, "Asset library", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string DefaultNewLibraryPath()
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Dan's RBI Baseball 2026",
                "Asset Library");
    }
}

#nullable enable annotations

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class ControllerSettingsControl : UserControl
    {
        private readonly ControllerSettings _settings;
        private readonly ComboBox _profile = new ComboBox();
        private readonly Label _connection = new Label();
        private readonly TextBox _mapping = new TextBox();
        private readonly System.Windows.Forms.Timer _pollTimer = new System.Windows.Forms.Timer { Interval = 500 };
        private string? _detectedDeviceId;

        public ControllerSettingsControl()
        {
            _settings = ControllerSettingsStore.Current.Clone();
            Dock = DockStyle.Fill;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "PlayStation controller profile",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold)
            }, 0, 0);

            var selector = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            selector.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            selector.Controls.Add(new Label
            {
                Text = "Controller type",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            _profile.Dock = DockStyle.Fill;
            _profile.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (PlayStationControllerDefinition profile in PlayStationControllerProfiles.All)
                _profile.Items.Add(profile);
            _profile.SelectedItem = PlayStationControllerProfiles.For(_settings.Profile);
            _profile.SelectedIndexChanged += (s, e) => UpdateProfilePreview();
            selector.Controls.Add(_profile, 1, 0);
            root.Controls.Add(selector, 0, 1);

            _connection.Dock = DockStyle.Fill;
            _connection.TextAlign = ContentAlignment.MiddleLeft;
            _connection.AutoEllipsis = true;
            root.Controls.Add(_connection, 0, 2);

            root.Controls.Add(new Label
            {
                Text = "PS3 remains the default. PS4 and PS5 preserve the baseball face-button layout while using their generation-specific OPTIONS, SHARE, and Create names. Touchpad, motion, speaker, adaptive-trigger, and PS-button features are not required by gameplay.",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 3);

            _mapping.Dock = DockStyle.Fill;
            _mapping.Multiline = true;
            _mapping.ReadOnly = true;
            _mapping.ScrollBars = ScrollBars.Vertical;
            _mapping.BackColor = SystemColors.Window;
            root.Controls.Add(_mapping, 0, 4);

            _pollTimer.Tick += (s, e) => RefreshConnectionStatus();
            _pollTimer.Start();
            UpdateProfilePreview();
            RefreshConnectionStatus();
        }

        public void Save()
        {
            if (_profile.SelectedItem is PlayStationControllerDefinition profile)
                _settings.Profile = profile.Generation;
            ControllerSettingsStore.Save(_settings);
        }

        private PlayStationControllerDefinition SelectedProfile =>
            _profile.SelectedItem as PlayStationControllerDefinition ?? PlayStationControllerProfiles.For(_settings.Profile);

        private void UpdateProfilePreview()
        {
            PlayStationControllerDefinition profile = SelectedProfile;
            _settings.Profile = profile.Generation;
            _mapping.Text = profile.ConnectionNote + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, profile.Bindings.Select(binding =>
                    binding.Context + ": " + binding.Control + " - " + binding.Action));
            RefreshConnectionStatus();
        }

        private void RefreshConnectionStatus()
        {
            if (GameControllerDiscovery.TryReadPreferredOrFirst(-1, _detectedDeviceId, out GameControllerReading reading))
            {
                _detectedDeviceId = reading.DeviceId;
                _connection.Text = "Detected: " + reading.DisplayName + " | Selected mapping: " + SelectedProfile.Name;
                _connection.ForeColor = Color.DarkGreen;
            }
            else
            {
                _detectedDeviceId = null;
                _connection.Text = "No controller detected. Connect or pair the controller, then leave this screen open for automatic detection.";
                _connection.ForeColor = Color.DarkGoldenrod;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _pollTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}

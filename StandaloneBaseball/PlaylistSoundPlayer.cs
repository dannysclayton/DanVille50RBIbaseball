using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class PlaylistSoundPlayer : IDisposable
    {
        private const int DefaultTrackMilliseconds = 180000;
        private readonly LaunchSoundPlayer _player = new LaunchSoundPlayer();
        private readonly System.Windows.Forms.Timer _advanceTimer = new System.Windows.Forms.Timer();
        private List<string> _tracks = new List<string>();
        private int _index;
        private bool _singleTrackLoop;

        public PlaylistSoundPlayer()
        {
            _advanceTimer.Tick += (s, e) => PlayNextTrack();
        }

        public void PlayLoop(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Stop();
                return;
            }

            Stop();
            _singleTrackLoop = true;
            _tracks = new List<string> { path };
            _player.PlayLoop(path);
        }

        public void PlayPlaylistLoop(IEnumerable<string> paths)
        {
            var tracks = (paths ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Stop();
            if (tracks.Count == 0)
                return;

            _tracks = tracks;
            _index = 0;
            _singleTrackLoop = tracks.Count == 1;
            if (_singleTrackLoop)
            {
                _player.PlayLoop(_tracks[0]);
                return;
            }

            PlayCurrentTrackOnce();
        }

        public void Stop()
        {
            _advanceTimer.Stop();
            _player.Stop();
            _tracks.Clear();
            _index = 0;
            _singleTrackLoop = false;
        }

        public void Dispose()
        {
            Stop();
            _advanceTimer.Dispose();
            _player.Dispose();
        }

        private void PlayCurrentTrackOnce()
        {
            if (_tracks.Count == 0)
                return;

            string path = _tracks[_index];
            _player.PlayOnce(path);
            int duration = LaunchSoundPlayer.GetDurationMilliseconds(path, DefaultTrackMilliseconds);
            _advanceTimer.Interval = Math.Max(1000, Math.Min(duration + 750, int.MaxValue));
            _advanceTimer.Start();
        }

        private void PlayNextTrack()
        {
            if (_tracks.Count == 0 || _singleTrackLoop)
                return;

            _advanceTimer.Stop();
            _index = (_index + 1) % _tracks.Count;
            PlayCurrentTrackOnce();
        }
    }
}

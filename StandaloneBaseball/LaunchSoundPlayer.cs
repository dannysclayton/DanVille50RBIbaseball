#nullable enable annotations

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace StandaloneBaseball
{
    internal sealed class LaunchSoundPlayer : IDisposable
    {
        private const string SoundFolder = "Game Sound Effects";
        private static readonly string[] AudioExtensions = { ".wav", ".mp3", ".wma" };
        private readonly string _alias = "launch_" + Guid.NewGuid().ToString("N");
        private bool _opened;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr callback);

        public static string FindLaunchLoop() =>
            FindSound("launch", "theme", "menu", "intro", "background", "loop");

        public static string FindStartSound() =>
            FindSound("start", "click", "button", "press");

        public static string FindLoadingLoop() =>
            FindSound("loading", "transition", "wait", "game_day", "gameday");

        public static string FindMenuLoop() =>
            FindSound("menu", "page", "screen", "navigation");

        public static string FindMenuButtonSound() =>
            FindSound("menu_click", "menu_button", "select", "accept", "confirm", "button", "click");

        public static string FindDefaultTeamMusic() =>
            FindSound("sim_default_team_music", "team_music", "tecmo", "super_baseball");

        public static string ResolveTeamMusic(Team? team)
        {
            string path = AssetPathResolver.ResolveExistingFile(team?.TeamMusicPath);
            if (!string.IsNullOrWhiteSpace(path))
                return path;
            return FindDefaultTeamMusic();
        }

        public static string[] ResolveAssignedTeamMusicPlaylist(Team? team)
        {
            if (team == null)
                return Array.Empty<string>();

            var tracks = (team.TeamMusicPlaylist ?? new System.Collections.Generic.List<string>())
                .Select(AssetPathResolver.ResolveExistingFile)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string legacyTrack = AssetPathResolver.ResolveExistingFile(team.TeamMusicPath);
            if (tracks.Count == 0 && !string.IsNullOrWhiteSpace(legacyTrack))
                tracks.Add(legacyTrack);

            return tracks.ToArray();
        }

        public static string[] ResolveTeamMusicPlaylist(Team? team)
        {
            string[] tracks = ResolveAssignedTeamMusicPlaylist(team);
            if (tracks.Length > 0)
                return tracks;

            string fallback = FindDefaultTeamMusic();
            return string.IsNullOrWhiteSpace(fallback) || !File.Exists(fallback)
                ? Array.Empty<string>()
                : new[] { fallback };
        }

        public static string FindGameIntro() =>
            FindSound("game_intro", "main_theme", "main theme", "game_theme", "rbi_intro", "intro_game");

        public static string FindGameOpening() =>
            FindSound("game_opening", "opening", "game_open", "rbi_opening");

        public static string FindPlayoffPregame() =>
            FindSound("playoff_pregame_game_of_thrones", "playoff_pregame", "game_of_thrones");

        public static string FindPlayBall() =>
            FindSound("play_ball", "play ball", "first_pitch", "first pitch");

        public static string FindTopHalfMatchupLoop() =>
            FindSound("top_half_matchup_loop", "today", "matchup");

        public static string FindBottomHalfThemeLoop() =>
            FindSound("bottom_half_theme_loop", "tecmo", "super_baseball", "bottom_half");

        public static string FindTopThirdTheme() =>
            FindSound("top_third_mexican_dance", "mexican_dance");

        public static string FindTopFourthTheme() =>
            FindSound("top_fourth_italian_dance", "italian_dance");

        public static string FindTopSeventhTheme() =>
            FindSound("top_seventh_lucky_7", "lucky_7", "lucky 7");

        public static string FindTopFinalTheme() =>
            FindSound("top_final_countdown", "final_countdown", "final countdown");

        public static string FindChangeSide() =>
            FindSound("change_side", "change side", "side_change");

        public static string FindRandomSeventhInningStretch(Random rng)
        {
            string[] tracks = FindSounds("seventh_inning_stretch", "take_me_out");
            if (tracks.Length == 0)
                return "";
            rng ??= new Random();
            return tracks[rng.Next(tracks.Length)];
        }

        public static string FindRandomHomeRunnersPrompt(Random rng)
        {
            string[] tracks = FindSounds("home_runners", "hey_batter", "classic_organ");
            if (tracks.Length == 0)
                return "";
            rng ??= new Random();
            return tracks[rng.Next(tracks.Length)];
        }

        public static string FindVisitorRunnersPrompt() =>
            FindSound("visitor_runners_crowd_murmur", "crowd_murmur");

        public static string FindRunnerOnThirdPrompt() =>
            FindSound("runner_on_third_good_bad_ugly", "runner_on_third", "good_bad_ugly");

        public static string FindScoredRunCall() =>
            FindSound("scored_a_run", "scored a run", "run_scored");

        public static string FindRandomPitcherChange(Random rng)
        {
            string[] tracks = FindSounds("pitcher_change", "managers_role", "jeopardy");
            if (tracks.Length == 0)
                return "";
            rng ??= new Random();
            return tracks[rng.Next(tracks.Length)];
        }

        public static string FindRandomPitchThrow(Random rng)
        {
            string[] tracks = FindSounds("pitch_throw_swoosh", "pitch_throw");
            if (tracks.Length == 0)
                return "";
            rng ??= new Random();
            return tracks[rng.Next(tracks.Length)];
        }

        public static string FindRandomStrikeCall(Random rng)
        {
            string[] tracks = FindSounds("strike_call", "stee_rike");
            if (tracks.Length == 0)
                return "";
            rng ??= new Random();
            return tracks[rng.Next(tracks.Length)];
        }

        public static string FindBallCall() =>
            FindSound("ball_call", "ball");

        public static string FindTakeYourBaseCall() =>
            FindSound("take_your_base", "take your base", "walk");

        public static string FindSafeCall() =>
            FindSound("safe_call", "safe");

        public static string FindFoulBallCall() =>
            FindSound("foul_ball_call", "foul_ball", "foul ball");

        public static string FindFoulFlyBallCall() =>
            FindSound("foul_fly_thud", "foul_fly", "thud");

        public static string FindBatHitsBallCall() =>
            FindSound("baseball_bat_hits_ball", "bat_hits_ball", "bat hits ball", "bat_hit");

        public static string FindFlyBallCall() =>
            FindSound("fly_ball_call", "fly_ball", "fly ball");

        public static string FindChanceBgm() =>
            FindSound("chance_bgm", "chance bgm", "chance");

        public static string FindUghImpactCall() =>
            FindSound("ugh_impact_call", "ugh");

        public static string FindRandomHomeRunCall(Random rng)
        {
            string[] tracks = FindSounds("home_run_call");
            if (tracks.Length == 0)
                return "";
            rng ??= new Random();
            return tracks[rng.Next(tracks.Length)];
        }

        public static string FindGrandSlamCall() =>
            FindSound("grand_slam_goodbye_extended_intro", "grand_slam", "grand slam");

        public static string FindWorldSeriesChampions() =>
            FindSound("world_series_champions", "world series champions");

        public static string FindLineupPositionCall(string position)
        {
            string key = (position ?? "").Trim().ToUpperInvariant();
            return key switch
            {
                "C" => FindSound("lineup_position_catcher", "catcher"),
                "P" => FindSound("lineup_position_pitcher", "pitcher"),
                "1B" => FindSound("lineup_position_first", "first"),
                "2B" => FindSound("lineup_position_second", "second"),
                "3B" => FindSound("lineup_position_third", "third"),
                "SS" => FindSound("lineup_position_shortstop", "shortstop"),
                "LF" => FindSound("lineup_position_left_field", "left field"),
                "CF" => FindSound("lineup_position_center_field", "center field"),
                "RF" => FindSound("lineup_position_right_field", "right field"),
                _ => ""
            };
        }

        public static string FindOutCall() =>
            FindSound("out_call", "out");

        public static string FindYoureOutCall() =>
            FindSound("youre_out_call", "you're out", "youre_out");

        public static string FindVisitorOutCrowdCheer() =>
            FindSound("visitor_out_crowd_cheer", "crowd_cheer");

        public static string FindGameOver() =>
            FindSound("game_over", "game over");

        public static string FindPostGameLoop() =>
            FindSound("post_game_loop", "after_the_game", "after the game");

        public static string FindThatsTheBallGame() =>
            FindSound("thats_the_ball_game", "that's the ball game", "thats_the_ball_game");

        public static string FindRandomNationalAnthem(Random? rng)
        {
            string[] tracks = FindSounds("national_anthem", "anthem");
            if (tracks.Length == 0)
                return "";
            rng ??= new Random();
            return tracks[rng.Next(tracks.Length)];
        }

        public static int GetDurationMilliseconds(string? path, int fallbackMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return fallbackMilliseconds;

            string alias = "duration_" + Guid.NewGuid().ToString("N");
            string escaped = path.Replace("\"", "");
            try
            {
                if (mciSendString("open \"" + escaped + "\" alias " + alias, null, 0, IntPtr.Zero) != 0)
                    return fallbackMilliseconds;
                mciSendString("set " + alias + " time format milliseconds", null, 0, IntPtr.Zero);
                var buffer = new StringBuilder(64);
                if (mciSendString("status " + alias + " length", buffer, buffer.Capacity, IntPtr.Zero) == 0 &&
                    int.TryParse(buffer.ToString(), out int milliseconds) &&
                    milliseconds > 0)
                {
                    return milliseconds;
                }
            }
            finally
            {
                mciSendString("close " + alias, null, 0, IntPtr.Zero);
            }

            return fallbackMilliseconds;
        }

        public void PlayLoop(string? path)
        {
            if (!Open(path))
                return;
            mciSendString("play " + _alias + " repeat", null, 0, IntPtr.Zero);
        }

        public void PlayOnce(string? path)
        {
            if (!Open(path))
                return;
            mciSendString("play " + _alias, null, 0, IntPtr.Zero);
        }

        public void Pause()
        {
            if (!_opened)
                return;
            mciSendString("pause " + _alias, null, 0, IntPtr.Zero);
        }

        public void Resume()
        {
            if (!_opened)
                return;
            if (mciSendString("resume " + _alias, null, 0, IntPtr.Zero) != 0)
                mciSendString("play " + _alias, null, 0, IntPtr.Zero);
        }

        public void Stop()
        {
            if (!_opened)
                return;
            mciSendString("stop " + _alias, null, 0, IntPtr.Zero);
            mciSendString("close " + _alias, null, 0, IntPtr.Zero);
            _opened = false;
        }

        public void Dispose() => Stop();

        private bool Open(string? path)
        {
            Stop();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            string escaped = path.Replace("\"", "");
            int result = mciSendString("open \"" + escaped + "\" alias " + _alias, null, 0, IntPtr.Zero);
            _opened = result == 0;
            return _opened;
        }

        private static string FindSound(params string[] preferredTerms)
        {
            return FindSounds(preferredTerms).FirstOrDefault() ?? "";
        }

        private static string[] FindSounds(params string[] preferredTerms)
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "Assets", SoundFolder);
            if (!Directory.Exists(folder))
                return Array.Empty<string>();

            var files = Directory.EnumerateFiles(folder)
                .Where(path => AudioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var matches = new System.Collections.Generic.List<string>();
            foreach (string term in preferredTerms)
            {
                matches.AddRange(files.Where(path =>
                    Path.GetFileNameWithoutExtension(path).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            return matches
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}

using System;
using System.IO;

namespace StandaloneBaseball
{
    public static class UserDataPaths
    {
        public static string RootDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DanVille50",
            "Dan's RBI Baseball 2026");

        public static string SchoolsCsvPath => Path.Combine(RootDirectory, "Data", "schools.csv");
        public static string LeagueCutsceneDirectory => Path.Combine(RootDirectory, "Global Assets", "Cutscenes");
        public static string TeamMusicPlaylistDirectory => Path.Combine(RootDirectory, "Global Assets", "Team Music Playlist");
        public static string UnsavedTeamAssetsDirectory => Path.Combine(RootDirectory, "Working Team Assets");

        public static string PackagedSchoolsCsvPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Data", "schools.csv");
        public static string PackagedLeagueCutsceneDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "Cutscenes");
        public static string PackagedTeamMusicPlaylistDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "Team Music Playlist");

        public static string EnsureSchoolsCsv()
            => EnsureSchoolsCsv(RootDirectory, AppContext.BaseDirectory);

        public static string EnsureLeagueCutsceneDirectory()
            => EnsureSeededDirectory(LeagueCutsceneDirectory, PackagedLeagueCutsceneDirectory);

        public static string EnsureTeamMusicPlaylistDirectory()
            => EnsureSeededDirectory(TeamMusicPlaylistDirectory, PackagedTeamMusicPlaylistDirectory);

        internal static string EnsureSchoolsCsv(string userRoot, string packageRoot)
        {
            string target = Path.Combine(Path.GetFullPath(userRoot), "Data", "schools.csv");
            if (File.Exists(target))
                return target;

            string? parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
            string seed = Path.Combine(Path.GetFullPath(packageRoot), "Assets", "Data", "schools.csv");
            if (File.Exists(seed))
                File.Copy(seed, target, overwrite: false);
            else
                File.WriteAllText(target, "name,mascot,city,state,primary_color,secondary_color,team_logo_image" + Environment.NewLine);
            return target;
        }

        internal static string EnsureSeededDirectory(string targetDirectory, string seedDirectory)
        {
            string target = Path.GetFullPath(targetDirectory);
            Directory.CreateDirectory(target);
            if (!string.IsNullOrWhiteSpace(seedDirectory) && Directory.Exists(seedDirectory))
            {
                string seedRoot = Path.GetFullPath(seedDirectory);
                foreach (string source in Directory.GetFiles(seedRoot, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(seedRoot, source);
                    string destination = Path.GetFullPath(Path.Combine(target, relative));
                    if (!destination.StartsWith(target.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    string? parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);
                    if (!File.Exists(destination))
                        File.Copy(source, destination, overwrite: false);
                }
            }
            return target;
        }
    }
}

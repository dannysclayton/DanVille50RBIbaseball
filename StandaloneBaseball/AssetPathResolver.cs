#nullable enable annotations

using System;
using System.IO;

namespace StandaloneBaseball
{
    internal static class AssetPathResolver
    {
        private static string _leagueDirectory = "";

        public static void SetLeagueFilePath(string? path)
        {
            _leagueDirectory = string.IsNullOrWhiteSpace(path)
                ? ""
                : Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        }

        public static void ClearLeagueFilePath() => _leagueDirectory = "";

        public static string ToPortablePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            string fullPath = Path.GetFullPath(path);
            if (PathStartsInside(fullPath, _leagueDirectory))
                return Path.GetRelativePath(_leagueDirectory, fullPath);
            if (PathStartsInside(fullPath, AppContext.BaseDirectory))
                return Path.GetRelativePath(AppContext.BaseDirectory, fullPath);

            return fullPath;
        }

        public static string ResolvePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            if (!string.IsNullOrWhiteSpace(_leagueDirectory))
            {
                string leaguePath = Path.GetFullPath(Path.Combine(_leagueDirectory, path));
                if (File.Exists(leaguePath) || Directory.Exists(leaguePath))
                    return leaguePath;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        }

        public static string ResolveExistingFile(string? path)
        {
            string resolved = ResolvePath(path);
            return !string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved) ? resolved : "";
        }

        public static bool FileExists(string? path) => !string.IsNullOrWhiteSpace(ResolveExistingFile(path));

        private static bool PathStartsInside(string? path, string? root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
                return false;

            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}

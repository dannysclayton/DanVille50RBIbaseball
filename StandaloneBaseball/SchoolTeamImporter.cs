using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class SchoolTeamRecord
    {
        public string Name { get; set; } = "";
        public string Mascot { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string PrimaryColor { get; set; } = "";
        public string SecondaryColor { get; set; } = "";
        public string LogoCatalogPath { get; set; } = "";
        public string LogoPath { get; set; } = "";
        public string HomeUniformImagePath { get; set; } = "";
        public string AwayUniformImagePath { get; set; } = "";
        public string AlternateHomeUniformImagePath { get; set; } = "";
        public string AlternateAwayUniformImagePath { get; set; } = "";

        public string DisplayName
            => string.IsNullOrWhiteSpace(Mascot) ? Name : (Name + " " + Mascot).Trim();

        public bool LogoAvailable
            => !string.IsNullOrWhiteSpace(LogoPath) && File.Exists(LogoPath);
    }

    public static class SchoolTeamImporter
    {
        public static List<SchoolTeamRecord> Load(string csvPath)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
                throw new ArgumentException("CSV path is required.", nameof(csvPath));
            if (!File.Exists(csvPath))
                throw new FileNotFoundException("The schools CSV file was not found.", csvPath);

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length == 0) return new List<SchoolTeamRecord>();

            var headers = ParseCsvLine(lines[0]);
            var index = headers
                .Select((name, i) => new { name = NormalizeHeader(name), i })
                .Where(x => !string.IsNullOrWhiteSpace(x.name))
                .GroupBy(x => x.name)
                .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);

            string csvDir = Path.GetDirectoryName(csvPath) ?? "";
            var records = new List<SchoolTeamRecord>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cells = ParseCsvLine(lines[i]);
                string Get(string name)
                {
                    return index.TryGetValue(NormalizeHeader(name), out int n) && n >= 0 && n < cells.Count
                        ? cells[n].Trim()
                        : "";
                }

                string logoCatalogPath = Get("team_logo_image");
                var record = new SchoolTeamRecord
                {
                    Name = Get("name"),
                    Mascot = Get("mascot"),
                    City = Get("city"),
                    State = Get("state"),
                    PrimaryColor = Get("primary_color"),
                    SecondaryColor = Get("secondary_color"),
                    LogoCatalogPath = logoCatalogPath,
                    LogoPath = ResolveFilePath(logoCatalogPath, csvDir),
                    HomeUniformImagePath = ResolveFilePath(Get("home_uniform_image"), csvDir),
                    AwayUniformImagePath = ResolveFilePath(Get("away_uniform_image"), csvDir),
                    AlternateHomeUniformImagePath = ResolveFilePath(Get("alternative_home_uniform_image"), csvDir),
                    AlternateAwayUniformImagePath = ResolveFilePath(Get("alternative_away_uniform_image"), csvDir)
                };

                if (!string.IsNullOrWhiteSpace(record.Name) || !string.IsNullOrWhiteSpace(record.Mascot))
                    records.Add(record);
            }

            return records;
        }

        private static string ResolveFilePath(string value, string csvDir)
        {
            value = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value)) return "";

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
                return uri.LocalPath;

            if (Path.IsPathRooted(value))
                return value;

            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(csvDir, value)),
                Path.GetFullPath(Path.Combine(Directory.GetParent(csvDir)?.FullName ?? csvDir, value))
            };

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        private static string NormalizeHeader(string value)
            => (value ?? "").Trim().ToLowerInvariant();

        private static List<string> ParseCsvLine(string line)
        {
            var cells = new List<string>();
            var cell = new System.Text.StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (c == ',' && !quoted)
                {
                    cells.Add(cell.ToString());
                    cell.Clear();
                }
                else
                {
                    cell.Append(c);
                }
            }

            cells.Add(cell.ToString());
            return cells;
        }
    }
}

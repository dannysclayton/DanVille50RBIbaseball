#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace StandaloneBaseball
{
    public sealed class RosterImportResult
    {
        public string SourcePath { get; set; } = "";
        public List<Player> Players { get; set; } = new List<Player>();
        public List<Player> JvPlayers { get; set; } = new List<Player>();
        public string Message { get; set; } = "";
    }

    public static class RosterSpreadsheetImporter
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly Regex SeasonPattern = new Regex(@"^\d{4}(-\d{2})?$", RegexOptions.Compiled);

        public static RosterImportResult Import(string path, Random rng)
        {
            var rows = ReadWorkbookRows(path);
            var players = BuildPlayers(rows, rng)
                .GroupBy(p => NormalizeKey(p.Name))
                .Select(g => g.First())
                .ToList();

            if (players.Count == 0)
                return new RosterImportResult { SourcePath = path, Message = "No player rows were found." };

            var varsity = players.Take(PlayerProgressionEngine.TargetRosterSize).ToList();
            var jv = players.Skip(PlayerProgressionEngine.TargetRosterSize).ToList();
            NormalizeVarsityRoster(varsity, jv, rng);
            return new RosterImportResult
            {
                SourcePath = path,
                Players = varsity,
                JvPlayers = jv,
                Message = "Imported " + varsity.Count + " varsity player(s) and " + jv.Count + " JV pool player(s) from " + Path.GetFileName(path) + "."
            };
        }

        private static List<Player> BuildPlayers(List<List<string>> rows, Random rng)
        {
            var groups = new Dictionary<string, List<Player>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int playerCol = FindHeaderColumn(row, "player", "name");
                if (playerCol < 0)
                    continue;

                int seasonCol = FindHeaderColumn(row, "season", "year");
                int gradeCol = FindHeaderColumn(row, "grade", "class", "yr");
                int positionCol = FindHeaderColumn(row, "position", "pos");

                for (int r = i + 1; r < rows.Count; r++)
                {
                    var data = rows[r];
                    if (FindHeaderColumn(data, "player", "name") >= 0)
                    {
                        i = r - 1;
                        break;
                    }

                    string name = Cell(data, playerCol);
                    if (!IsLikelyPlayerName(name))
                        continue;

                    string season = seasonCol >= 0 ? Cell(data, seasonCol) : "";
                    if (string.IsNullOrWhiteSpace(season) || !SeasonPattern.IsMatch(season.Trim()))
                        season = "Roster";

                    string positions = NormalizePositions(positionCol >= 0 ? Cell(data, positionCol) : "");
                    PlayerRole role = IsPitchingPosition(positions) ? PlayerRole.Pitcher : PlayerRole.Batter;
                    var player = Simulator.RandomPlayer(rng, role, CleanPlayerName(name));
                    if (!string.IsNullOrWhiteSpace(positions))
                    {
                        player.Positions = positions;
                        player.Role = role;
                    }

                    var classification = ParseClassification(gradeCol >= 0 ? Cell(data, gradeCol) : "");
                    if (classification != PlayerClassification.Unassigned)
                    {
                        player.Classification = classification;
                        player.InitialClassification = classification;
                    }

                    if (!groups.TryGetValue(season, out var list))
                    {
                        list = new List<Player>();
                        groups[season] = list;
                    }
                    list.Add(player);
                }
            }

            return groups
                .OrderByDescending(g => SeasonRank(g.Key))
                .ThenByDescending(g => g.Value.Count)
                .SelectMany(g => g.Value)
                .ToList();
        }

        private static List<List<string>> ReadWorkbookRows(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            var shared = ReadSharedStrings(archive);
            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
                ?? archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
                                                       e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (sheetEntry == null)
                return new List<List<string>>();

            using var stream = sheetEntry.Open();
            var doc = XDocument.Load(stream);
            var rows = new List<List<string>>();
            foreach (var row in doc.Descendants(MainNs + "row"))
            {
                var values = new SortedDictionary<int, string>();
                foreach (var cell in row.Elements(MainNs + "c"))
                {
                    int index = ColumnIndex((string?)cell.Attribute("r"));
                    if (index < 0)
                        index = values.Count;
                    values[index] = CellText(cell, shared);
                }

                if (values.Count == 0)
                    continue;
                int width = values.Keys.Max() + 1;
                var list = Enumerable.Repeat("", width).ToList();
                foreach (var item in values)
                    list[item.Key] = item.Value;
                rows.Add(list);
            }
            return rows;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return new List<string>();

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);
            return doc.Descendants(MainNs + "si")
                .Select(si => string.Concat(si.Descendants(MainNs + "t").Select(t => t.Value)))
                .ToList();
        }

        private static string CellText(XElement cell, List<string> shared)
        {
            string type = (string?)cell.Attribute("t") ?? "";
            if (type == "inlineStr")
                return string.Concat(cell.Descendants(MainNs + "t").Select(t => t.Value)).Trim();

            string value = cell.Element(MainNs + "v")?.Value ?? "";
            if (type == "s" && int.TryParse(value, out int sharedIndex) && sharedIndex >= 0 && sharedIndex < shared.Count)
                return shared[sharedIndex].Trim();
            return value.Trim();
        }

        private static int ColumnIndex(string? cellRef)
        {
            if (string.IsNullOrWhiteSpace(cellRef))
                return -1;
            int value = 0;
            bool found = false;
            foreach (char c in cellRef)
            {
                if (!char.IsLetter(c))
                    break;
                found = true;
                value = value * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }
            return found ? value - 1 : -1;
        }

        private static int FindHeaderColumn(List<string> row, params string[] names)
        {
            for (int i = 0; i < row.Count; i++)
            {
                string cell = NormalizeKey(row[i]);
                foreach (string name in names)
                {
                    string key = NormalizeKey(name);
                    if (cell == key || cell.Contains(key))
                        return i;
                }
            }
            return -1;
        }

        private static string Cell(List<string> row, int index)
            => index >= 0 && index < row.Count ? row[index] ?? "" : "";

        private static bool IsLikelyPlayerName(string value)
        {
            string name = CleanPlayerName(value);
            if (name.Length < 3 || name.Length > 48)
                return false;
            string key = NormalizeKey(name);
            if (key.Contains("noroster") || key.Contains("dataavailable") || key.Contains("player") || key.Contains("coach"))
                return false;
            return name.Any(char.IsLetter) && name.Count(char.IsWhiteSpace) >= 1;
        }

        private static string CleanPlayerName(string value)
        {
            string name = Regex.Replace(value ?? "", @"\s+", " ").Trim();
            name = Regex.Replace(name, @"^[#\d\.\-\s]+", "").Trim();
            return name;
        }

        private static string NormalizePositions(string value)
        {
            string raw = (value ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            raw = raw.Replace("RHP", "P").Replace("LHP", "P").Replace("PITCHER", "P");
            raw = raw.Replace("CATCHER", "C").Replace("OUTFIELD", "OF").Replace("INF", "IF");
            raw = raw.Replace(",", "/").Replace(";", "/").Replace(" ", "/");
            var parts = raw.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Select(p => p == "IF" ? "2B" : p)
                .Where(p => new[] { "P", "C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "OF", "DH" }.Contains(p))
                .Distinct()
                .ToList();
            return string.Join("/", parts);
        }

        private static bool IsPitchingPosition(string positions)
            => (positions ?? "").Split('/').Any(p => p == "P");

        private static PlayerClassification ParseClassification(string value)
        {
            string text = (value ?? "").Trim().ToUpperInvariant();
            if (text == "9" || text.StartsWith("FR") || text.Contains("FRESH")) return PlayerClassification.Freshman;
            if (text == "10" || text.StartsWith("SO") || text.Contains("SOPH")) return PlayerClassification.Sophomore;
            if (text == "11" || text.StartsWith("JR") || text.Contains("JUN")) return PlayerClassification.Junior;
            if (text == "12" || text.StartsWith("SR") || text.Contains("SEN")) return PlayerClassification.Senior;
            return PlayerClassification.Unassigned;
        }

        private static void NormalizeVarsityRoster(List<Player> players, List<Player> jv, Random rng)
        {
            while (players.Count(p => p.Role == PlayerRole.Pitcher) < PlayerProgressionEngine.MinimumPitchers)
            {
                var jvPitcher = jv.FirstOrDefault(p => p.Role == PlayerRole.Pitcher || IsPitchingPosition(p.Positions));
                if (jvPitcher == null)
                    break;

                var swap = players.LastOrDefault(p => p.Role != PlayerRole.Pitcher);
                if (swap == null)
                    break;

                players.Remove(swap);
                jv.Remove(jvPitcher);
                jv.Insert(0, swap);
                jvPitcher.Role = PlayerRole.Pitcher;
                if (string.IsNullOrWhiteSpace(jvPitcher.Positions) || !IsPitchingPosition(jvPitcher.Positions))
                    jvPitcher.Positions = string.IsNullOrWhiteSpace(jvPitcher.Positions) ? "P" : "P/" + jvPitcher.Positions;
                players.Add(jvPitcher);
            }

            while (players.Count(p => p.Role == PlayerRole.Pitcher) < PlayerProgressionEngine.MinimumPitchers && players.Count < PlayerProgressionEngine.TargetRosterSize)
                players.Add(Simulator.RandomPlayer(rng, PlayerRole.Pitcher));
            while (players.Count < PlayerProgressionEngine.TargetRosterSize)
                players.Add(Simulator.RandomPlayer(rng, PlayerRole.Batter));

            int pitchers = players.Count(p => p.Role == PlayerRole.Pitcher);
            for (int i = players.Count - 1; pitchers < PlayerProgressionEngine.MinimumPitchers && i >= 0; i--)
            {
                if (players[i].Role == PlayerRole.Pitcher)
                    continue;
                players[i].Role = PlayerRole.Pitcher;
                players[i].Positions = string.IsNullOrWhiteSpace(players[i].Positions) ? "P" : "P/" + players[i].Positions;
                players[i].Pitching = Math.Max(players[i].Pitching, Simulator.RandomDevelopmentRating(rng, 35, 85));
                players[i].Stamina = Math.Max(players[i].Stamina, Simulator.RandomDevelopmentRating(rng, 30, 85));
                pitchers++;
            }
        }

        private static int SeasonRank(string season)
        {
            var match = Regex.Match(season ?? "", @"\d{4}");
            return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)
                ? year
                : 0;
        }

        private static string NormalizeKey(string value)
            => new string((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}

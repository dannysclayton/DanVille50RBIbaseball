using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#nullable enable annotations

namespace StandaloneBaseball
{
    public sealed class SchoolsCsvInstallResult
    {
        public string RuntimePath { get; set; } = "";
        public string ProjectAssetPath { get; set; } = "";
        public int SchoolCount { get; set; }

        public IEnumerable<string> UpdatedPaths()
        {
            if (!string.IsNullOrWhiteSpace(RuntimePath))
                yield return RuntimePath;
            if (!string.IsNullOrWhiteSpace(ProjectAssetPath) &&
                !ProjectAssetPath.Equals(RuntimePath, StringComparison.OrdinalIgnoreCase))
                yield return ProjectAssetPath;
        }
    }

    public sealed class SchoolLogoCatalogEntry
    {
        public string SchoolName { get; set; } = "";
        public string Mascot { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string PrimaryColor { get; set; } = "";
        public string SecondaryColor { get; set; } = "";
        public string SourceLogoPath { get; set; } = "";
    }

    public sealed class SchoolsCsvLogoUpdateResult
    {
        public int LogoCount { get; set; }
        public List<string> UpdatedPaths { get; } = new List<string>();
    }

    public static class SchoolTeamCsvCatalog
    {
        public static string RuntimeSchoolsCsvPath
            => UserDataPaths.SchoolsCsvPath;

        public static string PackagedSchoolsCsvPath
            => UserDataPaths.PackagedSchoolsCsvPath;

        public static string ProjectAssetSchoolsCsvPath
        {
            get
            {
                string projectRoot = FindProjectRoot();
                return string.IsNullOrWhiteSpace(projectRoot)
                    ? ""
                    : Path.Combine(projectRoot, "Assets", "Data", "schools.csv");
            }
        }

        public static string PreferredSchoolsCsvPath
        {
            get
            {
                return UserDataPaths.EnsureSchoolsCsv();
            }
        }

        public static string PreferredInitialDirectory()
        {
            foreach (string path in new[] { PreferredSchoolsCsvPath, RuntimeSchoolsCsvPath, PackagedSchoolsCsvPath })
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    return dir;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        public static SchoolsCsvInstallResult InstallFrom(string sourceCsvPath)
        {
            var records = SchoolTeamImporter.Load(sourceCsvPath);
            foreach (var record in records)
            {
                record.LogoCatalogPath = "";
                record.LogoPath = "";
                record.HomeUniformImagePath = "";
                record.AwayUniformImagePath = "";
                record.AlternateHomeUniformImagePath = "";
                record.AlternateAwayUniformImagePath = "";
            }

            string runtimePath = UserDataPaths.EnsureSchoolsCsv();
            WritePortableCsv(runtimePath, records);

            return new SchoolsCsvInstallResult
            {
                RuntimePath = runtimePath,
                ProjectAssetPath = "",
                SchoolCount = records.Count
            };
        }

        public static SchoolsCsvLogoUpdateResult UpdateTeamLogos(IEnumerable<SchoolLogoCatalogEntry> entries)
        {
            var usableEntries = (entries ?? Enumerable.Empty<SchoolLogoCatalogEntry>())
                .Where(entry => entry != null &&
                    !string.IsNullOrWhiteSpace(entry.SchoolName) &&
                    !string.IsNullOrWhiteSpace(entry.SourceLogoPath) &&
                    File.Exists(entry.SourceLogoPath))
                .ToList();
            var result = new SchoolsCsvLogoUpdateResult { LogoCount = usableEntries.Count };
            if (usableEntries.Count == 0)
                return result;

            string csvPath = UserDataPaths.EnsureSchoolsCsv();
            UpdateTeamLogosForCatalog(csvPath, usableEntries);
            result.UpdatedPaths.Add(csvPath);

            return result;
        }

        internal static void UpdateTeamLogosForCatalog(string csvPath, IEnumerable<SchoolLogoCatalogEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
                throw new ArgumentException("A schools CSV path is required.", nameof(csvPath));

            var records = File.Exists(csvPath)
                ? SchoolTeamImporter.Load(csvPath)
                : new List<SchoolTeamRecord>();
            string csvDirectory = Path.GetDirectoryName(Path.GetFullPath(csvPath)) ?? AppContext.BaseDirectory;
            string assetsDirectory = Directory.GetParent(csvDirectory)?.FullName ?? csvDirectory;

            foreach (var entry in entries ?? Enumerable.Empty<SchoolLogoCatalogEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.SchoolName) ||
                    string.IsNullOrWhiteSpace(entry.SourceLogoPath) || !File.Exists(entry.SourceLogoPath))
                    continue;

                SchoolTeamRecord? record = FindSchool(records, entry.SchoolName, entry.Mascot);
                if (record == null)
                {
                    record = new SchoolTeamRecord
                    {
                        Name = entry.SchoolName.Trim(),
                        Mascot = (entry.Mascot ?? "").Trim(),
                        City = (entry.City ?? "").Trim(),
                        State = (entry.State ?? "").Trim(),
                        PrimaryColor = (entry.PrimaryColor ?? "").Trim(),
                        SecondaryColor = (entry.SecondaryColor ?? "").Trim()
                    };
                    records.Add(record);
                }

                string extension = Path.GetExtension(entry.SourceLogoPath);
                if (string.IsNullOrWhiteSpace(extension))
                    extension = ".png";
                string logoDirectory = Path.Combine(
                    assetsDirectory,
                    "Schools",
                    "Logos",
                    CatalogFolderName(record.Name, record.Mascot));
                Directory.CreateDirectory(logoDirectory);
                foreach (string oldLogo in Directory.GetFiles(logoDirectory, "logo.*"))
                {
                    if (!oldLogo.Equals(entry.SourceLogoPath, StringComparison.OrdinalIgnoreCase))
                        File.Delete(oldLogo);
                }

                string destination = Path.Combine(logoDirectory, "logo" + extension.ToLowerInvariant());
                if (!Path.GetFullPath(entry.SourceLogoPath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                    File.Copy(entry.SourceLogoPath, destination, overwrite: true);

                record.LogoPath = destination;
                record.LogoCatalogPath = Path.GetRelativePath(csvDirectory, destination);
            }

            WritePortableCsv(csvPath, records);
        }

        internal static void WritePortableCsv(string targetPath, IEnumerable<SchoolTeamRecord> records)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("A schools CSV target path is required.", nameof(targetPath));

            string? dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            using var writer = new StreamWriter(targetPath, append: false, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.WriteLine("name,mascot,city,state,primary_color,secondary_color,team_logo_image");
            foreach (var record in records ?? Enumerable.Empty<SchoolTeamRecord>())
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    CsvCell(record?.Name),
                    CsvCell(record?.Mascot),
                    CsvCell(record?.City),
                    CsvCell(record?.State),
                    CsvCell(record?.PrimaryColor),
                    CsvCell(record?.SecondaryColor),
                    CsvCell(PortableLogoPath(record))
                }));
            }
        }

        private static SchoolTeamRecord? FindSchool(IEnumerable<SchoolTeamRecord>? records, string schoolName, string? mascot)
        {
            string nameKey = IdentityKey(schoolName);
            string mascotKey = IdentityKey(mascot);
            return (records ?? Enumerable.Empty<SchoolTeamRecord>()).FirstOrDefault(record =>
                IdentityKey(record?.Name) == nameKey && IdentityKey(record?.Mascot) == mascotKey)
                ?? (records ?? Enumerable.Empty<SchoolTeamRecord>()).FirstOrDefault(record =>
                    IdentityKey(record?.Name) == nameKey);
        }

        private static string PortableLogoPath(SchoolTeamRecord? record)
        {
            string value = (record?.LogoCatalogPath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) ||
                Uri.TryCreate(value, UriKind.Absolute, out _))
                return "";

            string normalized = value.Replace('/', Path.DirectorySeparatorChar);
            return normalized.StartsWith(".." + Path.DirectorySeparatorChar + "Schools" + Path.DirectorySeparatorChar + "Logos" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? normalized
                : "";
        }

        private static string CatalogFolderName(string schoolName, string mascot)
        {
            string label = string.Join("-", new[] { schoolName, mascot }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
                .Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                label = label.Replace(invalid, '_');
            label = new string(label.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or ' '
                ? character
                : '_').ToArray()).Trim().Replace(' ', '-');
            if (label.Length > 48)
                label = label.Substring(0, 48).TrimEnd('-');
            if (string.IsNullOrWhiteSpace(label))
                label = "school";

            string identity = IdentityKey(schoolName) + "|" + IdentityKey(mascot);
            byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity));
            return label + "-" + Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant();
        }

        private static string IdentityKey(string? value)
            => new string((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

        private static string CsvCell(string? value)
        {
            value ??= "";
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
                ? value
                : "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "StandaloneBaseball.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return "";
        }
    }
}

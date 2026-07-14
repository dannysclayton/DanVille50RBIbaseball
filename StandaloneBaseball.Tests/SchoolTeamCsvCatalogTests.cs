using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class SchoolTeamCsvCatalogTests
{
    [Fact]
    public void WritePortableCsv_PreservesSchoolDataAndExcludesExternalAssets()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DansRBI-SchoolsCsvTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "schools.csv");
        Directory.CreateDirectory(directory);
        try
        {
            var record = new SchoolTeamRecord
            {
                Name = "Example, Academy",
                Mascot = "Knights \"Blue\"",
                City = "Dallas",
                State = "TX",
                PrimaryColor = "#112233",
                SecondaryColor = "#FFFFFF",
                LogoPath = @"C:\Users\developer\logo.png",
                HomeUniformImagePath = "file:///C:/private/home.png",
                AwayUniformImagePath = @"C:\private\away.png"
            };

            SchoolTeamCsvCatalog.WritePortableCsv(path, new[] { record });

            string text = File.ReadAllText(path);
            Assert.StartsWith("name,mascot,city,state,primary_color,secondary_color,team_logo_image", text);
            Assert.DoesNotContain("uniform_image", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:\\Users", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("file:///", text, StringComparison.OrdinalIgnoreCase);

            var restored = Assert.Single(SchoolTeamImporter.Load(path));
            Assert.Equal(record.Name, restored.Name);
            Assert.Equal(record.Mascot, restored.Mascot);
            Assert.Equal(record.PrimaryColor, restored.PrimaryColor);
            Assert.Equal(record.SecondaryColor, restored.SecondaryColor);
            Assert.Equal("", restored.LogoPath);
            Assert.Equal("", restored.HomeUniformImagePath);
            Assert.Equal("", restored.AwayUniformImagePath);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UpdateTeamLogosForCatalog_CopiesLogoAndWritesPortableReference()
    {
        string root = Path.Combine(Path.GetTempPath(), "DansRBI-SchoolsCsvTests", Guid.NewGuid().ToString("N"));
        string csvPath = Path.Combine(root, "Assets", "Data", "schools.csv");
        string sourceLogo = Path.Combine(root, "uploaded-logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
        File.WriteAllBytes(sourceLogo, new byte[] { 1, 2, 3, 4 });
        try
        {
            SchoolTeamCsvCatalog.WritePortableCsv(csvPath, new[]
            {
                new SchoolTeamRecord
                {
                    Name = "Example Academy",
                    Mascot = "Knights",
                    PrimaryColor = "#112233",
                    SecondaryColor = "#FFFFFF"
                }
            });

            SchoolTeamCsvCatalog.UpdateTeamLogosForCatalog(csvPath, new[]
            {
                new SchoolLogoCatalogEntry
                {
                    SchoolName = "Example Academy",
                    Mascot = "Knights",
                    SourceLogoPath = sourceLogo
                }
            });

            string text = File.ReadAllText(csvPath);
            Assert.DoesNotContain(sourceLogo, text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("..\\Schools\\Logos\\", text, StringComparison.OrdinalIgnoreCase);

            SchoolTeamRecord restored = Assert.Single(SchoolTeamImporter.Load(csvPath));
            Assert.True(restored.LogoAvailable);
            Assert.StartsWith(Path.Combine(root, "Assets", "Schools", "Logos"), restored.LogoPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(restored.LogoPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

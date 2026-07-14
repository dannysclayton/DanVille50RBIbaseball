using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class LineupCardExporterTests
{
    [Fact]
    public void BuildPage_UsesSavedBattingOrderAndDefensiveRole()
    {
        Team team = RegressionTestData.CreateTeam("Saved");
        team.BaseLineup.BattingOrder.Reverse();
        for (int index = 0; index < team.BaseLineup.BattingOrder.Count; index++)
            team.BaseLineup.BattingOrder[index].BattingOrder = index + 1;

        TeamBaseLineupSlot firstSavedSlot = team.BaseLineup.BattingOrder[0];
        Player expected = team.Roster.Single(player => player.Id == firstSavedSlot.PlayerId);

        LineupCardDocumentPage page = LineupCardExporter.BuildPage(team, null);

        Assert.Equal(9, page.Rows.Count);
        Assert.Equal(expected.Name, page.Rows[0].PlayerName);
        Assert.Equal(firstSavedSlot.DesignatedHitter ? "DH" : firstSavedSlot.DefensivePosition, page.Rows[0].Role);
        Assert.Equal(LineupCardExporter.BatGrade(expected), page.Rows[0].BatGrade);
    }

    [Theory]
    [InlineData(10, 10, "1")]
    [InlineData(45, 45, "3")]
    [InlineData(60, 60, "4")]
    [InlineData(75, 75, "5")]
    [InlineData(90, 90, "6")]
    public void BatGrade_MapsContactAndPowerToOriginalSixPointScale(int contact, int power, string expected)
    {
        Assert.Equal(expected, LineupCardExporter.BatGrade(new Player { Contact = contact, Power = power }));
    }

    [Fact]
    public void WriteDocx_EmbedsEachTeamsOwnLogoAndCreatesOneCardPerTeam()
    {
        string directory = TempDirectory();
        string firstLogo = Path.Combine(directory, "first.png");
        string secondLogo = Path.Combine(directory, "second.png");
        string output = Path.Combine(directory, "cards.docx");
        try
        {
            SaveLogo(firstLogo, Color.Red);
            SaveLogo(secondLogo, Color.Blue);
            Team first = RegressionTestData.CreateTeam("Alpha");
            Team second = RegressionTestData.CreateTeam("Bravo");

            LineupCardExporter.WriteDocx(output, "League Cards", new[]
            {
                LineupCardExporter.BuildPage(first, firstLogo),
                LineupCardExporter.BuildPage(second, secondLogo)
            });

            using var archive = ZipFile.OpenRead(output);
            ZipArchiveEntry document = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("word/document.xml"));
            string xml = Read(document);
            Assert.Contains("Alpha Club", xml);
            Assert.Contains("Bravo Club", xml);
            Assert.Contains("w:type=\"page\"", xml);

            ZipArchiveEntry firstEmbedded = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("word/media/team-logo-1.png"));
            ZipArchiveEntry secondEmbedded = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("word/media/team-logo-2.png"));
            Assert.False(ReadBytes(firstEmbedded).SequenceEqual(ReadBytes(secondEmbedded)));

            string relationships = Read(Assert.IsType<ZipArchiveEntry>(archive.GetEntry("word/_rels/document.xml.rels")));
            Assert.Contains("rIdLogo1", relationships);
            Assert.Contains("rIdLogo2", relationships);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WriteBlankTemplate_CreatesNineRowsAndLogoPlaceholder()
    {
        string directory = TempDirectory();
        string output = Path.Combine(directory, "template.docx");
        try
        {
            LineupCardExporter.WriteBlankTemplate(output);

            using var archive = ZipFile.OpenRead(output);
            string xml = Read(Assert.IsType<ZipArchiveEntry>(archive.GetEntry("word/document.xml")));
            Assert.Contains("TEAM NAME MASCOT", xml);
            Assert.Contains("LINEUP CARD", xml);
            Assert.Contains("LOGO", xml);
            Assert.Equal(11, Count(xml, "<w:tr>"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void SaveLogo(string path, Color color)
    {
        using var bitmap = new Bitmap(80, 60);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        bitmap.Save(path, ImageFormat.Png);
    }

    private static string TempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "LineupCardExporterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Read(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static int Count(string value, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}

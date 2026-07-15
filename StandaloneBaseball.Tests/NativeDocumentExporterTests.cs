using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class NativeDocumentExporterTests
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void WriteXlsx_CreatesOpenXmlWorkbookPackageWithTypedCells()
    {
        string path = TempPath(".xlsx");
        try
        {
            NativeDocumentExporter.WriteXlsx(path, "Stats", Sections());

            using var archive = ZipFile.OpenRead(path);
            Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
            Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
            Assert.NotNull(archive.GetEntry("xl/worksheets/sheet1.xml"));
            Assert.NotNull(archive.GetEntry("xl/styles.xml"));
            AssertAllXmlEntriesParse(archive);

            XDocument worksheet = LoadXml(archive, "xl/worksheets/sheet1.xml");
            Assert.Equal(
                new[] { "1", "2", "3", "4", "5", "6", "7" },
                worksheet.Descendants(SpreadsheetNamespace + "row").Select(row => (string)row.Attribute("r")));

            AssertCell(worksheet, "A1", "inlineStr", "Stats", "1");
            AssertCell(worksheet, "A3", "inlineStr", "Team Stats", "1");
            AssertCell(worksheet, "A4", "inlineStr", "Team", "2");
            AssertCell(worksheet, "B4", "inlineStr", "Wins", "2");

            AssertCell(worksheet, "A5", "inlineStr", "Danville & Co.");
            AssertCell(worksheet, "B5", null, "10");
            AssertCell(worksheet, "C5", null, "0.625");
            AssertCell(worksheet, "D5", "b", "1");
            AssertCell(worksheet, "E5", "d", "2026-07-14T19:30:45Z", "3");

            AssertCell(worksheet, "A6", "inlineStr", "Richmond");
            AssertCell(worksheet, "B6", "inlineStr", "9");
            AssertCell(worksheet, "C6", "inlineStr", "0.500");
            AssertCell(worksheet, "D6", "b", "0");
            AssertCell(worksheet, "E6", "d", "2026-07-15T08:05:00-05:00", "3");

            XDocument styles = LoadXml(archive, "xl/styles.xml");
            XElement cellFormats = Assert.Single(styles.Descendants(SpreadsheetNamespace + "cellXfs"));
            Assert.Equal("4", (string)cellFormats.Attribute("count"));
            XElement dateFormat = cellFormats.Elements(SpreadsheetNamespace + "xf").ElementAt(3);
            Assert.Equal("22", (string)dateFormat.Attribute("numFmtId"));
            Assert.Equal("1", (string)dateFormat.Attribute("borderId"));
            Assert.Equal("1", (string)dateFormat.Attribute("applyNumberFormat"));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void WriteDocx_CreatesOpenXmlDocumentPackage()
    {
        string path = TempPath(".docx");
        try
        {
            NativeDocumentExporter.WriteDocx(path, "Stats", Sections());

            using var archive = ZipFile.OpenRead(path);
            Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
            Assert.NotNull(archive.GetEntry("word/document.xml"));
            AssertAllXmlEntriesParse(archive);

            XDocument document = LoadXml(archive, "word/document.xml");
            XNamespace wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            Assert.Contains("Danville & Co.", document.Descendants(wordNamespace + "t").Select(element => element.Value));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static IEnumerable<ExportSection> Sections()
        => new[]
        {
            new ExportSection
            {
                Title = "Team Stats",
                Headers = new List<string> { "Team", "Wins", "Percentage", "Active", "First Pitch" },
                Rows = new List<List<object>>
                {
                    new() { "Danville & Co.", 10, 0.625m, true, new DateTime(2026, 7, 14, 19, 30, 45, DateTimeKind.Utc) },
                    new() { "Richmond", "9", "0.500", false, new DateTimeOffset(2026, 7, 15, 8, 5, 0, TimeSpan.FromHours(-5)) }
                }
            }
        };

    private static void AssertCell(XDocument worksheet, string reference, string type, string value, string style = null)
    {
        XElement cell = Assert.Single(
            worksheet.Descendants(SpreadsheetNamespace + "c"),
            element => (string)element.Attribute("r") == reference);

        Assert.Equal(type, (string)cell.Attribute("t"));
        Assert.Equal(style, (string)cell.Attribute("s"));
        XElement valueElement = type == "inlineStr"
            ? cell.Element(SpreadsheetNamespace + "is")?.Element(SpreadsheetNamespace + "t")
            : cell.Element(SpreadsheetNamespace + "v");
        Assert.NotNull(valueElement);
        Assert.Equal(value, valueElement.Value);
    }

    private static XDocument LoadXml(ZipArchive archive, string entryName)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryName);
        Assert.NotNull(entry);
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static void AssertAllXmlEntriesParse(ZipArchive archive)
    {
        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.Name.EndsWith(".xml") || entry.Name.EndsWith(".rels")))
        {
            using Stream stream = entry.Open();
            XDocument.Load(stream);
        }
    }

    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), "StandaloneBaseballTests-" + Guid.NewGuid().ToString("N") + extension);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

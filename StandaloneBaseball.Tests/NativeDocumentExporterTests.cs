using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class NativeDocumentExporterTests
{
    [Fact]
    public void WriteXlsx_CreatesOpenXmlWorkbookPackage()
    {
        string path = TempPath(".xlsx");
        try
        {
            NativeDocumentExporter.WriteXlsx(path, "Stats", Sections());

            using var archive = ZipFile.OpenRead(path);
            Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
            Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
            Assert.NotNull(archive.GetEntry("xl/worksheets/sheet1.xml"));
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
                Headers = new List<string> { "Team", "Wins" },
                Rows = new List<List<string>>
                {
                    new() { "Danville", "10" }
                }
            }
        };

    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), "StandaloneBaseballTests-" + Guid.NewGuid().ToString("N") + extension);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

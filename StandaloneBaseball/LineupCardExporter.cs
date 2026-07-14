using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace StandaloneBaseball
{
    internal sealed class LineupCardDocumentRow
    {
        public int Number { get; set; }
        public string PlayerName { get; set; } = "";
        public string Role { get; set; } = "";
        public string BatGrade { get; set; } = "";
        public string Positions { get; set; } = "";
        public string Changes { get; set; } = "";
    }

    internal sealed class LineupCardDocumentPage
    {
        public Guid TeamId { get; set; }
        public string TeamName { get; set; } = "";
        public string Mascot { get; set; } = "";
        public string ScoreboardAbbreviation { get; set; } = "";
        public int PrimaryArgb { get; set; }
        public int SecondaryArgb { get; set; }
        public string LogoPath { get; set; } = "";
        public List<LineupCardDocumentRow> Rows { get; set; } = new List<LineupCardDocumentRow>();
    }

    internal static class LineupCardExporter
    {
        private const long LogoMaxWidthEmu = 1_250_000;
        private const long LogoMaxHeightEmu = 850_000;

        public static LineupCardDocumentPage BuildPage(Team team, string? logoPath)
        {
            if (team == null)
                throw new ArgumentNullException(nameof(team));

            LineupCard lineup = LineupEngine.BuildLineupCard(team);
            var rows = lineup.BattingOrder
                .OrderBy(slot => slot.BattingOrder)
                .Take(9)
                .Select((slot, index) => new LineupCardDocumentRow
                {
                    Number = index + 1,
                    PlayerName = slot.Player?.Name ?? "",
                    Role = slot.DesignatedHitter ? "DH" : (slot.DefensivePosition ?? ""),
                    BatGrade = BatGrade(slot.Player),
                    Positions = slot.Player?.Positions ?? ""
                })
                .ToList();

            while (rows.Count < 9)
                rows.Add(new LineupCardDocumentRow { Number = rows.Count + 1 });

            return new LineupCardDocumentPage
            {
                TeamId = team.Id,
                TeamName = team.City,
                Mascot = team.Nickname,
                ScoreboardAbbreviation = team.ScoreboardName,
                PrimaryArgb = team.PrimaryArgb,
                SecondaryArgb = team.SecondaryArgb,
                LogoPath = logoPath ?? "",
                Rows = rows
            };
        }

        public static LineupCardDocumentPage BuildPage(Team team, string? logoPath, IEnumerable<GameLineupEntry>? snapshot)
        {
            var entries = (snapshot ?? Enumerable.Empty<GameLineupEntry>())
                .Where(entry => entry != null)
                .OrderBy(entry => entry.BattingOrder)
                .ToList();
            if (entries.Count == 0)
                return BuildPage(team, logoPath);

            var page = new LineupCardDocumentPage
            {
                TeamId = team.Id,
                TeamName = team.City,
                Mascot = team.Nickname,
                ScoreboardAbbreviation = team.ScoreboardName,
                PrimaryArgb = team.PrimaryArgb,
                SecondaryArgb = team.SecondaryArgb,
                LogoPath = logoPath ?? "",
                Rows = entries.Select(entry => new LineupCardDocumentRow
                {
                    Number = entry.BattingOrder,
                    PlayerName = entry.PlayerName,
                    Role = entry.DesignatedHitter ? "DH" : entry.DefensivePosition,
                    BatGrade = entry.BatGrade,
                    Positions = entry.Positions,
                    Changes = LineupChanges(entry)
                }).ToList()
            };
            return page;
        }

        private static string LineupChanges(GameLineupEntry entry)
        {
            var parts = new List<string>();
            if (!entry.IsStarter)
            {
                string when = (entry.EnteredHalf == HalfInning.Top ? "Top " : "Bottom ") + entry.EnteredInning;
                parts.Add("Entered " + when + (string.IsNullOrWhiteSpace(entry.ReplacedPlayerName) ? "" : " for " + entry.ReplacedPlayerName));
            }
            foreach (GamePositionChange change in entry.PositionHistory ?? new List<GamePositionChange>())
            {
                string when = (change.Half == HalfInning.Top ? "Top " : "Bottom ") + change.Inning;
                parts.Add(change.Position + " - " + when + (string.IsNullOrWhiteSpace(change.Reason) ? "" : " (" + change.Reason + ")"));
            }
            if (entry.ExitedInning.HasValue)
                parts.Add("Exited " + (entry.ExitedHalf == HalfInning.Bottom ? "Bottom " : "Top ") + entry.ExitedInning.Value);
            return string.Join("; ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        public static string BatGrade(Player? player)
        {
            if (player == null)
                return "";

            int offense = (player.Contact + player.Power) / 2;
            int grade = Math.Clamp((int)Math.Ceiling(offense / 15.0), 1, 6);
            return grade.ToString();
        }

        public static void WriteDocx(string path, string title, IEnumerable<LineupCardDocumentPage> pages)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Export path is required.", nameof(path));

            var safePages = (pages ?? Enumerable.Empty<LineupCardDocumentPage>())
                .Where(page => page != null)
                .ToList();
            if (safePages.Count == 0)
                throw new InvalidOperationException("At least one team is required for a lineup-card export.");

            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            if (File.Exists(path))
                File.Delete(path);

            var logos = safePages
                .Select((page, index) => LoadLogo(page, index + 1))
                .ToList();

            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            AddText(archive, "[Content_Types].xml", ContentTypes());
            AddText(archive, "_rels/.rels", PackageRelationships());
            AddText(archive, "docProps/app.xml", AppProperties());
            AddText(archive, "docProps/core.xml", CoreProperties(title));
            AddText(archive, "word/document.xml", DocumentXml(safePages, logos));

            if (logos.Any(logo => logo != null))
                AddText(archive, "word/_rels/document.xml.rels", DocumentRelationships(logos));

            foreach (var logo in logos.Where(logo => logo != null))
                AddBinary(archive, "word/media/" + logo!.FileName, logo.Bytes);
        }

        public static void WriteBlankTemplate(string path)
        {
            var page = new LineupCardDocumentPage
            {
                TeamName = "TEAM NAME",
                Mascot = "MASCOT",
                ScoreboardAbbreviation = "LOGO",
                PrimaryArgb = unchecked((int)0xFF173F8A),
                SecondaryArgb = unchecked((int)0xFFC9D3E8),
                Rows = Enumerable.Range(1, 9)
                    .Select(number => new LineupCardDocumentRow { Number = number })
                    .ToList()
            };
            WriteDocx(path, "Lineup Card Template", new[] { page });
        }

        private static LogoPart? LoadLogo(LineupCardDocumentPage page, int index)
        {
            if (string.IsNullOrWhiteSpace(page.LogoPath) || !File.Exists(page.LogoPath))
                return null;

            try
            {
                using var source = Image.FromFile(page.LogoPath);
                using var stream = new MemoryStream();
                source.Save(stream, ImageFormat.Png);

                double scale = Math.Min(
                    LogoMaxWidthEmu / (double)Math.Max(1, source.Width),
                    LogoMaxHeightEmu / (double)Math.Max(1, source.Height));
                return new LogoPart
                {
                    RelationshipId = "rIdLogo" + index,
                    FileName = "team-logo-" + index + ".png",
                    Bytes = stream.ToArray(),
                    WidthEmu = Math.Max(1, (long)Math.Round(source.Width * scale)),
                    HeightEmu = Math.Max(1, (long)Math.Round(source.Height * scale)),
                    DrawingId = index
                };
            }
            catch
            {
                return null;
            }
        }

        private static string DocumentXml(IReadOnlyList<LineupCardDocumentPage> pages, IReadOnlyList<LogoPart?> logos)
        {
            var body = new StringBuilder();
            for (int index = 0; index < pages.Count; index++)
            {
                body.Append(CardPageXml(pages[index], logos[index]));
                if (index < pages.Count - 1)
                    body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");
            }

            body.Append("<w:sectPr><w:pgSz w:w=\"12240\" w:h=\"15840\"/>" +
                        "<w:pgMar w:top=\"720\" w:right=\"900\" w:bottom=\"720\" w:left=\"900\" w:header=\"360\" w:footer=\"360\" w:gutter=\"0\"/></w:sectPr>");

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
                   "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" " +
                   "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" " +
                   "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" " +
                   "xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">" +
                   "<w:body>" + body + "</w:body></w:document>";
        }

        private static string CardPageXml(LineupCardDocumentPage page, LogoPart? logo)
        {
            string primary = ColorHex(page.PrimaryArgb);
            string secondary = ColorHex(page.SecondaryArgb);
            string headerText = ContrastingText(page.PrimaryArgb);
            string fullName = string.Join(" ", new[] { page.TeamName, page.Mascot }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
            if (string.IsNullOrWhiteSpace(fullName))
                fullName = "TEAM NAME - MASCOT";

            var xml = new StringBuilder();
            xml.Append("<w:tbl><w:tblPr><w:tblW w:w=\"10440\" w:type=\"dxa\"/><w:tblBorders>" +
                       BorderXml(primary, 12) + "</w:tblBorders></w:tblPr><w:tblGrid><w:gridCol w:w=\"1800\"/><w:gridCol w:w=\"8640\"/></w:tblGrid><w:tr>");
            xml.Append("<w:tc><w:tcPr><w:tcW w:w=\"1800\" w:type=\"dxa\"/><w:shd w:val=\"clear\" w:fill=\"" + secondary + "\"/><w:vAlign w:val=\"center\"/></w:tcPr>");
            xml.Append(logo == null ? LogoFallback(page) : LogoDrawing(logo));
            xml.Append("</w:tc>");
            xml.Append("<w:tc><w:tcPr><w:tcW w:w=\"8640\" w:type=\"dxa\"/><w:shd w:val=\"clear\" w:fill=\"" + primary + "\"/><w:vAlign w:val=\"center\"/></w:tcPr>" +
                       TextParagraph(fullName, 34, true, headerText, "center", after: 60) +
                       TextParagraph("LINEUP CARD", 20, true, headerText, "center", after: 60) + "</w:tc></w:tr></w:tbl>");
            xml.Append("<w:p><w:pPr><w:spacing w:after=\"120\"/></w:pPr></w:p>");

            xml.Append("<w:tbl><w:tblPr><w:tblW w:w=\"10440\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblBorders>" + BorderXml(primary, 8) + "</w:tblBorders></w:tblPr>" +
                       "<w:tblGrid><w:gridCol w:w=\"500\"/><w:gridCol w:w=\"2500\"/><w:gridCol w:w=\"850\"/><w:gridCol w:w=\"650\"/><w:gridCol w:w=\"1700\"/><w:gridCol w:w=\"3240\"/></w:tblGrid>");
            xml.Append(LineupRow(new[] { "#", "Player", "Role", "Bat", "Eligible Positions", "Game Changes" }, true, primary, headerText));
            foreach (var row in page.Rows)
                xml.Append(LineupRow(new[] { row.Number > 0 ? row.Number.ToString() : "-", row.PlayerName, row.Role, row.BatGrade, row.Positions, row.Changes }, false, secondary, "000000"));
            xml.Append("</w:tbl>");
            xml.Append("<w:p><w:pPr><w:spacing w:before=\"240\" w:after=\"100\"/></w:pPr>" +
                       "<w:r><w:rPr><w:b/><w:sz w:val=\"20\"/></w:rPr><w:t>Umpire:</w:t></w:r>" +
                       "<w:r><w:tab/><w:t>________________________________</w:t></w:r></w:p>");
            xml.Append("<w:p><w:pPr><w:spacing w:after=\"0\"/></w:pPr>" +
                       "<w:r><w:rPr><w:b/><w:sz w:val=\"20\"/></w:rPr><w:t>Notes:</w:t></w:r>" +
                       "<w:r><w:tab/><w:t>_________________________________</w:t></w:r></w:p>");
            return xml.ToString();
        }

        private static string LineupRow(IReadOnlyList<string> values, bool header, string fill, string textColor)
        {
            int[] widths = { 500, 2500, 850, 650, 1700, 3240 };
            var row = new StringBuilder("<w:tr><w:trPr><w:trHeight w:val=\"520\" w:hRule=\"atLeast\"/></w:trPr>");
            for (int index = 0; index < widths.Length; index++)
            {
                row.Append("<w:tc><w:tcPr><w:tcW w:w=\"").Append(widths[index]).Append("\" w:type=\"dxa\"/>");
                if (header)
                    row.Append("<w:shd w:val=\"clear\" w:fill=\"").Append(fill).Append("\"/>");
                row.Append("<w:vAlign w:val=\"center\"/></w:tcPr>")
                   .Append(TextParagraph(values[index], header ? 20 : 19, header, textColor, index is 0 or 2 or 3 ? "center" : "left", after: 0))
                   .Append("</w:tc>");
            }
            return row.Append("</w:tr>").ToString();
        }

        private static string LogoFallback(LineupCardDocumentPage page)
        {
            string value = string.IsNullOrWhiteSpace(page.ScoreboardAbbreviation) ? "LOGO" : page.ScoreboardAbbreviation;
            return TextParagraph(value, 24, true, ContrastingText(page.SecondaryArgb), "center", after: 0);
        }

        private static string LogoDrawing(LogoPart logo)
        {
            return "<w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:before=\"60\" w:after=\"60\"/></w:pPr><w:r><w:drawing>" +
                   "<wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"" + logo.WidthEmu + "\" cy=\"" + logo.HeightEmu + "\"/>" +
                   "<wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:docPr id=\"" + logo.DrawingId + "\" name=\"Team Logo " + logo.DrawingId + "\"/>" +
                   "<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr><a:graphic>" +
                   "<a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr>" +
                   "<pic:cNvPr id=\"0\" name=\"" + Xml(logo.FileName) + "\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill>" +
                   "<a:blip r:embed=\"" + logo.RelationshipId + "\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr>" +
                   "<a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"" + logo.WidthEmu + "\" cy=\"" + logo.HeightEmu + "\"/></a:xfrm>" +
                   "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline>" +
                   "</w:drawing></w:r></w:p>";
        }

        private static string TextParagraph(string text, int halfPoints, bool bold, string color, string justification, int after)
        {
            return "<w:p><w:pPr><w:jc w:val=\"" + justification + "\"/><w:spacing w:after=\"" + after + "\"/></w:pPr><w:r><w:rPr>" +
                   (bold ? "<w:b/>" : "") + "<w:color w:val=\"" + color + "\"/><w:sz w:val=\"" + halfPoints + "\"/><w:szCs w:val=\"" + halfPoints + "\"/>" +
                   "</w:rPr><w:t xml:space=\"preserve\">" + Xml(text) + "</w:t></w:r></w:p>";
        }

        private static string BorderXml(string color, int size)
        {
            string side(string name) => "<w:" + name + " w:val=\"single\" w:sz=\"" + size + "\" w:space=\"0\" w:color=\"" + color + "\"/>";
            return side("top") + side("left") + side("bottom") + side("right") + side("insideH") + side("insideV");
        }

        private static string DocumentRelationships(IEnumerable<LogoPart?> logos)
        {
            string relationships = string.Join("", logos.Where(logo => logo != null).Select(logo =>
                "<Relationship Id=\"" + logo!.RelationshipId + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/" + Xml(logo.FileName) + "\"/>"));
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + relationships + "</Relationships>";
        }

        private static string ContentTypes()
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
               "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
               "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
               "<Default Extension=\"png\" ContentType=\"image/png\"/>" +
               "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
               "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
               "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>" +
               "</Types>";

        private static string PackageRelationships()
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
               "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
               "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
               "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
               "</Relationships>";

        private static string AppProperties()
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">" +
               "<Application>Dan's RBI Baseball 2026</Application></Properties>";

        private static string CoreProperties(string title)
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">" +
               "<dc:title>" + Xml(title) + "</dc:title><dc:creator>Dan's RBI Baseball 2026</dc:creator></cp:coreProperties>";

        private static string ColorHex(int argb)
        {
            Color color = Color.FromArgb(argb);
            return $"{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static string ContrastingText(int argb)
        {
            Color color = Color.FromArgb(argb);
            double luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
            return luminance >= 155 ? "000000" : "FFFFFF";
        }

        private static string Xml(string? value) => SecurityElement.Escape(value ?? "") ?? "";

        private static void AddText(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private static void AddBinary(ZipArchive archive, string name, byte[] content)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(content, 0, content.Length);
        }

        private sealed class LogoPart
        {
            public string RelationshipId { get; set; } = "";
            public string FileName { get; set; } = "";
            public byte[] Bytes { get; set; } = Array.Empty<byte>();
            public long WidthEmu { get; set; }
            public long HeightEmu { get; set; }
            public int DrawingId { get; set; }
        }
    }
}

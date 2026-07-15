using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace StandaloneBaseball
{
    internal sealed class ExportSection
    {
        public string Title { get; set; } = "";
        public List<string> Headers { get; set; } = new List<string>();
        public IEnumerable<IEnumerable<object?>> Rows { get; set; } = Enumerable.Empty<IEnumerable<object?>>();
    }

    internal static class NativeDocumentExporter
    {
        public static void WriteXlsx(string path, string title, IEnumerable<ExportSection> sections)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Export path is required.", nameof(path));

            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            AddEntry(archive, "[Content_Types].xml", XlsxContentTypes());
            AddEntry(archive, "_rels/.rels", PackageRels("xl/workbook.xml"));
            AddEntry(archive, "docProps/app.xml", AppProperties("Microsoft Excel"));
            AddEntry(archive, "docProps/core.xml", CoreProperties(title));
            AddEntry(archive, "xl/workbook.xml", WorkbookXml());
            AddEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRels());
            AddEntry(archive, "xl/styles.xml", XlsxStyles());
            AddEntry(archive, "xl/worksheets/sheet1.xml", WorksheetXml(title, sections));
        }

        public static void WriteDocx(string path, string title, IEnumerable<ExportSection> sections)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Export path is required.", nameof(path));

            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            AddEntry(archive, "[Content_Types].xml", DocxContentTypes());
            AddEntry(archive, "_rels/.rels", PackageRels("word/document.xml"));
            AddEntry(archive, "docProps/app.xml", AppProperties("Microsoft Word"));
            AddEntry(archive, "docProps/core.xml", CoreProperties(title));
            AddEntry(archive, "word/document.xml", DocumentXml(title, sections));
        }

        private static string WorksheetXml(string title, IEnumerable<ExportSection> sections)
        {
            var rows = new StringBuilder();
            int rowIndex = 1;
            rows.Append(Row(rowIndex++, new[] { title }, style: 1));
            rows.Append(Row(rowIndex++, Array.Empty<object?>()));

            foreach (var section in SafeSections(sections))
            {
                if (!string.IsNullOrWhiteSpace(section.Title))
                    rows.Append(Row(rowIndex++, new[] { section.Title }, style: 1));
                if (section.Headers.Count > 0)
                    rows.Append(Row(rowIndex++, section.Headers, style: 2));
                foreach (var row in section.Rows)
                    rows.Append(Row(rowIndex++, row));
                rows.Append(Row(rowIndex++, Array.Empty<object?>()));
            }

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                   "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                   "<sheetViews><sheetView workbookViewId=\"0\"/></sheetViews>" +
                   "<sheetFormatPr defaultRowHeight=\"15\"/>" +
                   "<cols><col min=\"1\" max=\"80\" width=\"18\" customWidth=\"1\"/></cols>" +
                   "<sheetData>" + rows + "</sheetData>" +
                   "</worksheet>";
        }

        private static string Row(int rowIndex, IEnumerable<object?> values, int style = 0)
        {
            var cells = new StringBuilder();
            int column = 1;
            foreach (object? value in values ?? Array.Empty<object?>())
            {
                cells.Append(Cell(ColumnName(column) + rowIndex, value, style));
                column++;
            }

            return "<row r=\"" + rowIndex + "\">" + cells + "</row>";
        }

        private static string Cell(string reference, object? value, int style)
        {
            if (TryNumericValue(value, out string numericValue))
                return ValueCell(reference, numericValue, null, style);

            if (value is bool booleanValue)
                return ValueCell(reference, booleanValue ? "1" : "0", "b", style);

            if (value is DateTime dateTimeValue)
                return ValueCell(reference, XmlConvert.ToString(dateTimeValue, XmlDateTimeSerializationMode.RoundtripKind), "d", style == 0 ? 3 : style);

            if (value is DateTimeOffset dateTimeOffsetValue)
                return ValueCell(reference, XmlConvert.ToString(dateTimeOffsetValue), "d", style == 0 ? 3 : style);

            if (value is DateOnly dateValue)
                return ValueCell(reference, dateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "d", style == 0 ? 3 : style);

            string styleAttribute = StyleAttribute(style);
            return "<c r=\"" + reference + "\" t=\"inlineStr\"" + styleAttribute + ">" +
                   "<is><t xml:space=\"preserve\">" + Xml(CellText(value)) + "</t></is></c>";
        }

        private static string ValueCell(string reference, string value, string? type, int style)
        {
            string typeAttribute = type == null ? "" : " t=\"" + type + "\"";
            return "<c r=\"" + reference + "\"" + typeAttribute + StyleAttribute(style) + "><v>" + Xml(value) + "</v></c>";
        }

        private static string StyleAttribute(int style)
            => style > 0 ? " s=\"" + style + "\"" : "";

        private static bool TryNumericValue(object? value, out string numericValue)
        {
            switch (value)
            {
                case byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
                    numericValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
                    return true;
                case float floatValue when float.IsFinite(floatValue):
                    numericValue = floatValue.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case double doubleValue when double.IsFinite(doubleValue):
                    numericValue = doubleValue.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case Half halfValue when Half.IsFinite(halfValue):
                    numericValue = halfValue.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                default:
                    numericValue = "";
                    return false;
            }
        }

        private static string CellText(object? value)
            => value as string ?? Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";

        private static string DocumentXml(string title, IEnumerable<ExportSection> sections)
        {
            var body = new StringBuilder();
            body.Append(Paragraph(title, "Title"));
            foreach (var section in SafeSections(sections))
            {
                if (!string.IsNullOrWhiteSpace(section.Title))
                    body.Append(Paragraph(section.Title, "Heading1"));
                body.Append(Table(section));
            }

            body.Append("<w:sectPr><w:pgSz w:w=\"15840\" w:h=\"12240\" w:orient=\"landscape\"/>" +
                        "<w:pgMar w:top=\"720\" w:right=\"720\" w:bottom=\"720\" w:left=\"720\" w:header=\"360\" w:footer=\"360\" w:gutter=\"0\"/></w:sectPr>");

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                   "<w:body>" + body + "</w:body></w:document>";
        }

        private static string Paragraph(string text, string style)
        {
            string styleXml = string.IsNullOrWhiteSpace(style) ? "" : "<w:pPr><w:pStyle w:val=\"" + Xml(style) + "\"/></w:pPr>";
            return "<w:p>" + styleXml + "<w:r><w:t xml:space=\"preserve\">" + Xml(text) + "</w:t></w:r></w:p>";
        }

        private static string Table(ExportSection section)
        {
            var table = new StringBuilder();
            table.Append("<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"0\" w:type=\"auto\"/>" +
                         "<w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"999999\"/>" +
                         "<w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"999999\"/>" +
                         "<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"999999\"/>" +
                         "<w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"999999\"/>" +
                         "<w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"999999\"/>" +
                         "<w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"999999\"/></w:tblBorders></w:tblPr>");

            if (section.Headers.Count > 0)
                table.Append(TableRow(section.Headers, header: true));
            foreach (var row in section.Rows)
                table.Append(TableRow(row, header: false));
            table.Append("</w:tbl>");
            return table.ToString();
        }

        private static string TableRow(IEnumerable<object?> values, bool header)
        {
            var row = new StringBuilder("<w:tr>");
            foreach (object? value in values ?? Array.Empty<object?>())
            {
                row.Append("<w:tc><w:tcPr><w:tcW w:w=\"2400\" w:type=\"dxa\"/>");
                if (header)
                    row.Append("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"173F8A\"/>");
                row.Append("</w:tcPr><w:p><w:r>");
                if (header)
                    row.Append("<w:rPr><w:b/><w:color w:val=\"FFFFFF\"/></w:rPr>");
                row.Append("<w:t xml:space=\"preserve\">").Append(Xml(CellText(value))).Append("</w:t></w:r></w:p></w:tc>");
            }
            row.Append("</w:tr>");
            return row.ToString();
        }

        private static IEnumerable<ExportSection> SafeSections(IEnumerable<ExportSection> sections)
            => sections?.Where(section => section != null) ?? Enumerable.Empty<ExportSection>();

        private static void AddEntry(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string XlsxContentTypes()
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
               "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
               "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
               "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
               "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
               "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
               "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
               "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>" +
               "</Types>";

        private static string DocxContentTypes()
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
               "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
               "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
               "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
               "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
               "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>" +
               "</Types>";

        private static string PackageRels(string target)
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
               "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"" + Xml(target) + "\"/>" +
               "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
               "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
               "</Relationships>";

        private static string WorkbookXml()
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
               "<sheets><sheet name=\"Export\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

        private static string WorkbookRels()
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
               "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
               "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
               "</Relationships>";

        private static string XlsxStyles()
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
               "<fonts count=\"3\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"16\"/><name val=\"Calibri\"/></font><font><b/><color rgb=\"FFFFFFFF\"/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
               "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF173F8A\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
               "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style=\"thin\"><color rgb=\"FF999999\"/></left><right style=\"thin\"><color rgb=\"FF999999\"/></right><top style=\"thin\"><color rgb=\"FF999999\"/></top><bottom style=\"thin\"><color rgb=\"FF999999\"/></bottom><diagonal/></border></borders>" +
               "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
               "<cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyFont=\"1\"/><xf numFmtId=\"22\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs>" +
               "</styleSheet>";

        private static string AppProperties(string app)
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">" +
               "<Application>" + Xml(app) + "</Application></Properties>";

        private static string CoreProperties(string title)
            => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
               "<dc:title>" + Xml(title) + "</dc:title><dc:creator>Dan's RBI Baseball 2026</dc:creator>" +
               "<cp:lastModifiedBy>Dan's RBI Baseball 2026</cp:lastModifiedBy><dcterms:created xsi:type=\"dcterms:W3CDTF\">" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "</dcterms:created></cp:coreProperties>";

        private static string ColumnName(int index)
        {
            var name = new StringBuilder();
            while (index > 0)
            {
                int remainder = (index - 1) % 26;
                name.Insert(0, (char)('A' + remainder));
                index = (index - remainder - 1) / 26;
            }
            return name.ToString();
        }

        private static string Xml(string value)
            => System.Security.SecurityElement.Escape(value ?? "") ?? "";
    }
}

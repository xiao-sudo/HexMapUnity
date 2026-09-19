#if UNITY_EDITOR 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using HexMap.Gvg.Authoring;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEditor;

namespace HexMap.Gvg.Editor
{
    /// <summary>
    /// Full-rebuild Excel (.xlsx) exporter. The workbook is rebuilt from scratch so
    /// it matches the canonical form the importer expects: a 5-row header block
    /// followed by the sorted data region. Logical columns are recomputed from the
    /// authoring asset; redundant columns are written back verbatim from the last
    /// import (see <see cref="GvgExcelDocumentRedundancy"/>).
    /// </summary>
    public static class GvgMapExcelExporter
    {
        public const string FileNameFormat = "GVGMap_{0}.xlsx";

        private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";

        /// <summary>
        /// Exports the asset to <c>Assets/HexMap/Gvg/Exports/&lt;assetName&gt;/GVGMap_&lt;MapId&gt;.xlsx</c>.
        /// The asset must be saved so an asset path can be derived.
        /// </summary>
        public static string ExportExcel(GvgMapAuthoringAsset asset, RuntimeHexMap map)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (map == null) throw new ArgumentNullException(nameof(map));

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new InvalidOperationException("GVG map authoring asset must be saved before exporting Excel.");
            }

            var assetName = Path.GetFileNameWithoutExtension(assetPath);
            var exportDirectory = Path.Combine("Assets/HexMap/Gvg/Exports", assetName);
            return ExportExcel(asset, map, exportDirectory);
        }

        /// <summary>
        /// Exports the asset to an explicit directory. Used by EditMode tests that
        /// export unsaved assets to a temporary directory; the file name is derived
        /// from <see cref="FileNameFormat"/>.
        /// </summary>
        public static string ExportExcel(GvgMapAuthoringAsset asset, RuntimeHexMap map, string exportDirectory)
        {
            if (string.IsNullOrEmpty(exportDirectory))
                throw new ArgumentException("Export directory is required.", nameof(exportDirectory));

            var fileName = string.Format(CultureInfo.InvariantCulture, FileNameFormat, asset.MapId);
            return ExportExcelToFile(asset, map, Path.Combine(exportDirectory, fileName));
        }

        /// <summary>
        /// Exports the asset to an explicit file path chosen by the user (directory
        /// and file name). The workbook is rebuilt to the canonical form regardless of
        /// the target location.
        /// </summary>
        public static string ExportExcelToFile(GvgMapAuthoringAsset asset, RuntimeHexMap map, string fullPath)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (string.IsNullOrEmpty(fullPath))
                throw new ArgumentException("Export file path is required.", nameof(fullPath));

            var validation = GvgMapAuthoringUtility.Validate(asset, map);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Issues[0].Message);
            }

            var exportDirectory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(exportDirectory))
            {
                Directory.CreateDirectory(exportDirectory);
            }

            WriteWorkbook(fullPath, asset);
            return fullPath;
        }

        private static void WriteWorkbook(string fullPath, GvgMapAuthoringAsset asset)
        {
            var builder = new ExcelWorkbookBuilder(asset);

            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "_rels/.rels", builder.BuildRootRelationships());
                WriteEntry(archive, "docProps/core.xml", builder.BuildCoreProperties());
                WriteEntry(archive, "docProps/app.xml", builder.BuildAppProperties());
                WriteEntry(archive, "xl/workbook.xml", builder.BuildWorkbook());
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", builder.BuildWorkbookRelationships());
                // BuildSheet populates the shared-string table, the style table, and
                // the comment list, so it must run before the parts that read them.
                WriteEntry(archive, "xl/worksheets/sheet1.xml", builder.BuildSheet());
                WriteEntry(archive, "xl/styles.xml", builder.BuildStyles());
                WriteEntry(archive, "xl/sharedStrings.xml", builder.BuildSharedStrings());
                WriteEntry(archive, "[Content_Types].xml", builder.BuildContentTypes());

                if (builder.HasComments)
                {
                    WriteEntry(archive, "xl/comments1.xml", builder.BuildComments());
                    WriteEntry(archive, "xl/drawings/vmlDrawing1.vml", builder.BuildVmlDrawing());
                    WriteEntry(archive, "xl/worksheets/_rels/sheet1.xml.rels", builder.BuildSheetRelationships());
                }
            }
        }

        private static void WriteEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private sealed class ExcelWorkbookBuilder
        {
            private readonly GvgMapAuthoringAsset m_Asset;
            private readonly List<GvgExcelColumnInfo> m_Columns = new List<GvgExcelColumnInfo>();
            private readonly List<GvgExcelHeaderRow> m_HeaderRows = new List<GvgExcelHeaderRow>();
            private readonly List<GvgPlotAuthoringData> m_SortedPlots = new List<GvgPlotAuthoringData>();
            private readonly SharedStringTable m_SharedStrings = new SharedStringTable();
            private readonly StyleTable m_Styles = new StyleTable();
            private readonly List<CommentPosition> m_Comments = new List<CommentPosition>();

            public ExcelWorkbookBuilder(GvgMapAuthoringAsset asset)
            {
                m_Asset = asset;
                ResolveColumnsAndHeader(asset);
                SortPlots(asset);
            }

            public bool HasComments
            {
                get { return m_Comments.Count > 0; }
            }

            private void ResolveColumnsAndHeader(GvgMapAuthoringAsset asset)
            {
                var redundancy = asset.ExcelRedundancy;
                if (redundancy != null && redundancy.Columns.Count > 0)
                {
                    for (var index = 0; index < redundancy.Columns.Count; index++)
                    {
                        m_Columns.Add(new GvgExcelColumnInfo(
                            redundancy.Columns[index].Name,
                            redundancy.Columns[index].Kind));
                    }

                    if (redundancy.HeaderRows.Count == 5)
                    {
                        for (var index = 0; index < redundancy.HeaderRows.Count; index++)
                        {
                            m_HeaderRows.Add(redundancy.HeaderRows[index].Clone());
                        }

                        return;
                    }
                }

                BuildCanonicalColumns();
                BuildCanonicalHeaderRows();
            }

            private void BuildCanonicalColumns()
            {
                m_Columns.Add(new GvgExcelColumnInfo(GvgMapExcelImporter.ColumnPlotId, GvgExcelColumnKind.Int));
                m_Columns.Add(new GvgExcelColumnInfo(GvgMapExcelImporter.ColumnHexIds, GvgExcelColumnKind.IntArray));
                m_Columns.Add(new GvgExcelColumnInfo(GvgMapExcelImporter.ColumnGenerationType, GvgExcelColumnKind.Int));
                m_Columns.Add(new GvgExcelColumnInfo(GvgMapExcelImporter.ColumnPlotType, GvgExcelColumnKind.Int));
                m_Columns.Add(new GvgExcelColumnInfo(GvgMapExcelImporter.ColumnAffiliatedCampId, GvgExcelColumnKind.Int));
                m_Columns.Add(new GvgExcelColumnInfo(GvgMapExcelImporter.ColumnStart, GvgExcelColumnKind.Int));
                m_Columns.Add(new GvgExcelColumnInfo(GvgMapExcelImporter.ColumnEnd, GvgExcelColumnKind.Int));
            }

            private void BuildCanonicalHeaderRows()
            {
                var row0 = new GvgExcelHeaderRow();
                var row1 = new GvgExcelHeaderRow();
                var row2 = new GvgExcelHeaderRow();
                var row3 = new GvgExcelHeaderRow();
                var row4 = new GvgExcelHeaderRow();

                for (var index = 0; index < m_Columns.Count; index++)
                {
                    var name = m_Columns[index].Name;
                    row0.MutableCells.Add(new GvgExcelHeaderCell(name, name, string.Empty, string.Empty, string.Empty));
                    row1.MutableCells.Add(new GvgExcelHeaderCell(name, TypeToken(m_Columns[index].Kind), string.Empty, string.Empty, string.Empty));
                    row2.MutableCells.Add(new GvgExcelHeaderCell(name, name, string.Empty, string.Empty, string.Empty));
                    row3.MutableCells.Add(new GvgExcelHeaderCell(name, string.Empty, string.Empty, string.Empty, string.Empty));
                    row4.MutableCells.Add(new GvgExcelHeaderCell(name, "c/s", string.Empty, string.Empty, string.Empty));
                }

                m_HeaderRows.Add(row0);
                m_HeaderRows.Add(row1);
                m_HeaderRows.Add(row2);
                m_HeaderRows.Add(row3);
                m_HeaderRows.Add(row4);
            }

            private static string TypeToken(GvgExcelColumnKind kind)
            {
                switch (kind)
                {
                    case GvgExcelColumnKind.Int:
                        return "int";
                    case GvgExcelColumnKind.IntArray:
                        return "int[]";
                    default:
                        return "string";
                }
            }

            private void SortPlots(GvgMapAuthoringAsset asset)
            {
                for (var index = 0; index < asset.Plots.Count; index++)
                {
                    var plot = asset.Plots[index];
                    if (plot != null)
                    {
                        m_SortedPlots.Add(plot);
                    }
                }

                m_SortedPlots.Sort(ComparePlotIds);
            }

            private static int ComparePlotIds(GvgPlotAuthoringData left, GvgPlotAuthoringData right)
            {
                return left.PlotId.CompareTo(right.PlotId);
            }

            public string BuildContentTypes()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<Types xmlns=\"").Append(ContentTypesNamespace).Append("\">");
                builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
                builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
                if (HasComments)
                {
                    builder.Append("<Default Extension=\"vml\" ContentType=\"application/vnd.openxmlformats-officedocument.vmlDrawing\"/>");
                }

                builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
                builder.Append("<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
                builder.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
                if (HasComments)
                {
                    builder.Append("<Override PartName=\"/xl/comments1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.comments+xml\"/>");
                }

                builder.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
                builder.Append("<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");
                builder.Append("</Types>");
                return builder.ToString();
            }

            public string BuildRootRelationships()
            {
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"" + PackageRelationshipNamespace + "\">" +
                    "<Relationship Id=\"rId1\" Type=\"" + RelationshipNamespace + "/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"" + RelationshipNamespace + "/extended-properties\" Target=\"docProps/app.xml\"/>" +
                    "<Relationship Id=\"rId3\" Type=\"" + PackageRelationshipNamespace + "/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
                    "</Relationships>";
            }

            public string BuildCoreProperties()
            {
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
                    "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" " +
                    "xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                    "<dc:creator>Unity GVG Authoring</dc:creator>" +
                    "<cp:lastModifiedBy>Unity GVG Authoring</cp:lastModifiedBy>" +
                    "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" + IsoNow() + "</dcterms:created>" +
                    "<dcterms:modified xsi:type=\"dcterms:W3CDTF\">" + IsoNow() + "</dcterms:modified>" +
                    "</cp:coreProperties>";
            }

            public string BuildAppProperties()
            {
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" " +
                    "xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">" +
                    "<Application>Unity GVG Authoring</Application>" +
                    "</Properties>";
            }

            private static string IsoNow()
            {
                return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            }

            public string BuildWorkbook()
            {
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"" + MainNamespace + "\" xmlns:r=\"" + RelationshipNamespace + "\">" +
                    "<sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                    "</workbook>";
            }

            public string BuildWorkbookRelationships()
            {
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"" + PackageRelationshipNamespace + "\">" +
                    "<Relationship Id=\"rId1\" Type=\"" + RelationshipNamespace + "/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"" + RelationshipNamespace + "/styles\" Target=\"styles.xml\"/>" +
                    "<Relationship Id=\"rId3\" Type=\"" + RelationshipNamespace + "/sharedStrings\" Target=\"sharedStrings.xml\"/>" +
                    "</Relationships>";
            }

            public string BuildStyles()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<styleSheet xmlns=\"").Append(MainNamespace).Append("\">");
                builder.Append("<fonts count=\"").Append(m_Styles.Fonts.Count).Append("\">");
                for (var index = 0; index < m_Styles.Fonts.Count; index++)
                {
                    var font = m_Styles.Fonts[index];
                    builder.Append("<font>");
                    if (font.Bold)
                    {
                        builder.Append("<b/>");
                    }

                    builder.Append("<sz val=\"11\"/>");
                    if (font.Color.Length > 0)
                    {
                        builder.Append("<color rgb=\"").Append(font.Color).Append("\"/>");
                    }

                    builder.Append("<name val=\"").Append(font.Name).Append("\"/></font>");
                }

                builder.Append("</fonts>");
                builder.Append("<fills count=\"").Append(m_Styles.FillColors.Count).Append("\">");
                builder.Append("<fill><patternFill patternType=\"none\"/></fill>");
                builder.Append("<fill><patternFill patternType=\"gray125\"/></fill>");
                for (var index = 2; index < m_Styles.FillColors.Count; index++)
                {
                    builder.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"")
                        .Append(m_Styles.FillColors[index]).Append("\"/><bgColor indexed=\"64\"/></patternFill></fill>");
                }

                builder.Append("</fills>");
                builder.Append("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>");
                builder.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
                builder.Append("<cellXfs count=\"").Append(m_Styles.XfCount).Append("\">");
                for (var index = 0; index < m_Styles.XfCount; index++)
                {
                    builder.Append("<xf numFmtId=\"0\" fontId=\"").Append(m_Styles.XfFont(index))
                        .Append("\" fillId=\"").Append(m_Styles.XfFill(index))
                        .Append("\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>");
                }

                builder.Append("</cellXfs>");
                builder.Append("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
                builder.Append("</styleSheet>");
                return builder.ToString();
            }

            public string BuildSharedStrings()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<sst xmlns=\"").Append(MainNamespace).Append("\" count=\"")
                    .Append(m_SharedStrings.Count).Append("\" uniqueCount=\"").Append(m_SharedStrings.Count).Append("\">");
                for (var index = 0; index < m_SharedStrings.Count; index++)
                {
                    var value = m_SharedStrings[index];
                    builder.Append("<si><t");
                    if (NeedsPreserveSpace(value)) builder.Append(" xml:space=\"preserve\"");
                    builder.Append(">").Append(Escape(value)).Append("</t></si>");
                }

                builder.Append("</sst>");
                return builder.ToString();
            }

            public string BuildComments()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<comments xmlns=\"").Append(MainNamespace).Append("\">");
                builder.Append("<authors><author>Unity GVG Authoring</author></authors>");
                builder.Append("<commentList>");
                for (var index = 0; index < m_Comments.Count; index++)
                {
                    var comment = m_Comments[index];
                    builder.Append("<comment ref=\"").Append(comment.Reference).Append("\" authorId=\"0\">");
                    builder.Append("<text><t xml:space=\"preserve\">").Append(Escape(comment.Text)).Append("</t></text>");
                    builder.Append("</comment>");
                }

                builder.Append("</commentList></comments>");
                return builder.ToString();
            }

            public string BuildVmlDrawing()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<xml xmlns:v=\"urn:schemas-microsoft-com:vml\" ")
                    .Append("xmlns:o=\"urn:schemas-microsoft-com:office:office\" ")
                    .Append("xmlns:x=\"urn:schemas-microsoft-com:office:excel\">");
                builder.Append("<o:shapelayout v:ext=\"edit\"><o:idmap v:ext=\"edit\" data=\"1\"/></o:shapelayout>");
                builder.Append("<v:shapetype id=\"_x0000_t202\" coordsize=\"21600,21600\" o:spt=\"202\" ")
                    .Append("path=\"m0,0l0,21600,21600,21600,21600,0xe\">");
                builder.Append("<v:stroke joinstyle=\"miter\"/>");
                builder.Append("<v:path gradientshapeok=\"t\" o:connecttype=\"rect\"/>");
                builder.Append("</v:shapetype>");
                for (var index = 0; index < m_Comments.Count; index++)
                {
                    var comment = m_Comments[index];
                    builder.Append("<v:shape id=\"_x0000_s").Append(1025 + index)
                        .Append("\" o:spt=\"202\" type=\"#_x0000_t202\" ")
                        .Append("style=\"position:absolute;visibility:hidden\" fillcolor=\"#FFFFE1\" o:insetmode=\"auto\">");
                    builder.Append("<v:fill color2=\"#FFFFE1\"/>");
                    builder.Append("<v:shadow on=\"t\" color=\"black\" obscured=\"t\"/>");
                    builder.Append("<v:path o:connecttype=\"none\"/>");
                    builder.Append("<v:textbox style=\"mso-direction-alt:auto\"><div style=\"text-align:left\"/></v:textbox>");
                    builder.Append("<x:ClientData ObjectType=\"Note\">");
                    builder.Append("<x:MoveWithCells/><x:SizeWithCells/>");
                    builder.Append("<x:Anchor>1, 15, 0, 2, 3, 15, 3, 16</x:Anchor>");
                    builder.Append("<x:AutoFill>False</x:AutoFill>");
                    builder.Append("<x:Row>").Append(comment.RowIndex).Append("</x:Row>");
                    builder.Append("<x:Column>").Append(comment.ColumnIndex).Append("</x:Column>");
                    builder.Append("</x:ClientData></v:shape>");
                }

                builder.Append("</xml>");
                return builder.ToString();
            }

            public string BuildSheetRelationships()
            {
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"" + PackageRelationshipNamespace + "\">" +
                    "<Relationship Id=\"rId1\" Type=\"" + RelationshipNamespace + "/comments\" Target=\"../comments1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"" + RelationshipNamespace + "/vmlDrawing\" Target=\"../drawings/vmlDrawing1.vml\"/>" +
                    "</Relationships>";
            }

            public string BuildSheet()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<worksheet xmlns=\"").Append(MainNamespace).Append("\" xmlns:r=\"").Append(RelationshipNamespace).Append("\">");
                builder.Append("<sheetData>");

                for (var rowIndex = 0; rowIndex < m_HeaderRows.Count; rowIndex++)
                {
                    AppendHeaderRow(builder, rowIndex + 1, m_HeaderRows[rowIndex]);
                }

                for (var rowIndex = 0; rowIndex < m_SortedPlots.Count; rowIndex++)
                {
                    AppendDataRow(builder, rowIndex + 6, m_SortedPlots[rowIndex]);
                }

                builder.Append("</sheetData>");
                if (HasComments)
                {
                    builder.Append("<legacyDrawing r:id=\"rId2\"/>");
                }

                builder.Append("</worksheet>");
                return builder.ToString();
            }

            private void AppendHeaderRow(StringBuilder builder, int rowNumber, GvgExcelHeaderRow row)
            {
                builder.Append("<row r=\"").Append(rowNumber).Append("\">");
                for (var columnIndex = 0; columnIndex < m_Columns.Count; columnIndex++)
                {
                    var cell = columnIndex < row.Cells.Count ? row.Cells[columnIndex] : null;
                    var text = cell == null ? string.Empty : cell.Text;
                    var fillColor = cell == null ? string.Empty : cell.FillColor;
                    var fontColor = cell == null ? string.Empty : cell.FontColor;
                    var comment = cell == null ? string.Empty : cell.Comment;

                    if (text.Length == 0 && fillColor.Length == 0 && fontColor.Length == 0 && comment.Length == 0)
                    {
                        continue;
                    }

                    var reference = CellReference(rowNumber, columnIndex);
                    // Header cells are bold Microsoft YaHei.
                    var styleIndex = m_Styles.GetStyleIndex(fillColor, fontColor, true);
                    builder.Append("<c r=\"").Append(reference).Append("\" s=\"").Append(styleIndex).Append("\"");
                    if (text.Length > 0)
                    {
                        builder.Append(" t=\"s\"><v>").Append(m_SharedStrings.Add(text)).Append("</v></c>");
                    }
                    else
                    {
                        builder.Append("/>");
                    }

                    if (comment.Length > 0)
                    {
                        m_Comments.Add(new CommentPosition(columnIndex, rowNumber - 1, reference, comment));
                    }
                }

                builder.Append("</row>");
            }

            private void AppendDataRow(StringBuilder builder, int rowNumber, GvgPlotAuthoringData plot)
            {
                var redundancy = m_Asset.ExcelRedundancy;
                builder.Append("<row r=\"").Append(rowNumber).Append("\">");
                for (var columnIndex = 0; columnIndex < m_Columns.Count; columnIndex++)
                {
                    var column = m_Columns[columnIndex];
                    var content = GetCellContent(plot, column, redundancy);
                    if (content == null) content = string.Empty;

                    if (column.Kind == GvgExcelColumnKind.Int)
                    {
                        if (content.Length == 0) content = "0";
                        builder.Append("<c r=\"").Append(CellReference(rowNumber, columnIndex))
                            .Append("\"><v>").Append(Escape(content)).Append("</v></c>");
                    }
                    else
                    {
                        if (content.Length == 0) continue;
                        builder.Append("<c r=\"").Append(CellReference(rowNumber, columnIndex))
                            .Append("\" t=\"s\"><v>").Append(m_SharedStrings.Add(content)).Append("</v></c>");
                    }
                }

                builder.Append("</row>");
            }

            private static string GetCellContent(
                GvgPlotAuthoringData plot,
                GvgExcelColumnInfo column,
                GvgExcelDocumentRedundancy redundancy)
            {
                switch (column.Name)
                {
                    case GvgMapExcelImporter.ColumnPlotId:
                        return plot.PlotId.ToString(CultureInfo.InvariantCulture);
                    case GvgMapExcelImporter.ColumnHexIds:
                        return FormatHexIds(plot.HexIds);
                    case GvgMapExcelImporter.ColumnGenerationType:
                        return (plot.Start == 0 ? 0 : 1).ToString(CultureInfo.InvariantCulture);
                    case GvgMapExcelImporter.ColumnPlotType:
                        return ((int)plot.PlotType).ToString(CultureInfo.InvariantCulture);
                    case GvgMapExcelImporter.ColumnAffiliatedCampId:
                        return plot.AffiliatedCampId.ToString(CultureInfo.InvariantCulture);
                    case GvgMapExcelImporter.ColumnStart:
                        return plot.Start.ToString(CultureInfo.InvariantCulture);
                    case GvgMapExcelImporter.ColumnEnd:
                        return plot.End.ToString(CultureInfo.InvariantCulture);
                    default:
                        if (redundancy != null)
                        {
                            return redundancy.GetColumnContent(plot.PlotId, column.Name);
                        }

                        return string.Empty;
                }
            }

            private static string FormatHexIds(IReadOnlyList<int> hexIds)
            {
                var sorted = new List<int>(hexIds);
                sorted.Sort();
                var values = new string[sorted.Count];
                for (var index = 0; index < sorted.Count; index++)
                {
                    values[index] = sorted[index].ToString(CultureInfo.InvariantCulture);
                }

                return "[" + string.Join(",", values) + "]";
            }

            private static string CellReference(int rowNumber, int columnIndex)
            {
                return ColumnName(columnIndex) + rowNumber.ToString(CultureInfo.InvariantCulture);
            }

            private static string ColumnName(int columnIndex)
            {
                var name = string.Empty;
                var value = columnIndex + 1;
                while (value > 0)
                {
                    var remainder = (value - 1) % 26;
                    name = (char)('A' + remainder) + name;
                    value = (value - 1) / 26;
                }

                return name;
            }

            private static bool NeedsPreserveSpace(string value)
            {
                if (value.Length == 0) return true;
                var first = value[0];
                var last = value[value.Length - 1];
                return first == ' ' || first == '\t' || first == '\n' || first == '\r' ||
                    last == ' ' || last == '\t' || last == '\n' || last == '\r';
            }

            private static string Escape(string value)
            {
                return SecurityElement.Escape(value) ?? string.Empty;
            }
        }

        private sealed class SharedStringTable
        {
            private readonly List<string> m_Values = new List<string>();
            private readonly Dictionary<string, int> m_Index = new Dictionary<string, int>();

            public int Count
            {
                get { return m_Values.Count; }
            }

            public string this[int index]
            {
                get { return m_Values[index]; }
            }

            public int Add(string value)
            {
                if (value == null) value = string.Empty;
                int index;
                if (m_Index.TryGetValue(value, out index)) return index;
                index = m_Values.Count;
                m_Values.Add(value);
                m_Index.Add(value, index);
                return index;
            }
        }

        private sealed class FontDescriptor
        {
            public readonly string Name;
            public readonly bool Bold;
            public readonly string Color;

            public FontDescriptor(string name, bool bold, string color)
            {
                Name = name ?? string.Empty;
                Bold = bold;
                Color = color ?? string.Empty;
            }

            public bool Matches(string name, bool bold, string color)
            {
                return Bold == bold
                    && string.Equals(Name, name, StringComparison.Ordinal)
                    && string.Equals(Color, color, StringComparison.Ordinal);
            }
        }

        private sealed class StyleTable
        {
            private const string FontName = "\u5fae\u8f6f\u96c5\u9ed1"; // Microsoft YaHei

            public readonly List<FontDescriptor> Fonts = new List<FontDescriptor>();
            public readonly List<string> FillColors = new List<string>();
            private readonly List<int> m_XfFonts = new List<int>();
            private readonly List<int> m_XfFills = new List<int>();
            private readonly Dictionary<string, int> m_StyleIndex = new Dictionary<string, int>();

            public StyleTable()
            {
                // font 0 = default: Microsoft YaHei, regular, no explicit color.
                Fonts.Add(new FontDescriptor(FontName, false, string.Empty));
                // fill 0 = none, fill 1 = gray125 (both have no readable fg color)
                FillColors.Add(string.Empty);
                FillColors.Add(string.Empty);
                m_XfFonts.Add(0);
                m_XfFills.Add(0);
                m_StyleIndex.Add(MakeKey(string.Empty, string.Empty, false), 0);
            }

            public int XfCount
            {
                get { return m_XfFonts.Count; }
            }

            public int XfFont(int index)
            {
                return m_XfFonts[index];
            }

            public int XfFill(int index)
            {
                return m_XfFills[index];
            }

            public int GetStyleIndex(string fillColor, string fontColor, bool bold)
            {
                if (fillColor == null) fillColor = string.Empty;
                if (fontColor == null) fontColor = string.Empty;
                var key = MakeKey(fillColor, fontColor, bold);
                int styleIndex;
                if (m_StyleIndex.TryGetValue(key, out styleIndex)) return styleIndex;

                var fontId = FindOrAddFont(FontName, bold, fontColor);
                var fillId = fillColor.Length == 0 ? 0 : FillColors.IndexOf(fillColor);
                if (fillId < 0)
                {
                    fillId = FillColors.Count;
                    FillColors.Add(fillColor);
                }

                styleIndex = m_XfFonts.Count;
                m_XfFonts.Add(fontId);
                m_XfFills.Add(fillId);
                m_StyleIndex.Add(key, styleIndex);
                return styleIndex;
            }

            private int FindOrAddFont(string name, bool bold, string color)
            {
                for (var index = 0; index < Fonts.Count; index++)
                {
                    if (Fonts[index].Matches(name, bold, color)) return index;
                }

                Fonts.Add(new FontDescriptor(name, bold, color));
                return Fonts.Count - 1;
            }

            private static string MakeKey(string fillColor, string fontColor, bool bold)
            {
                return (bold ? "B" : "R") + "|" + fillColor + "|" + fontColor;
            }
        }

        private sealed class CommentPosition
        {
            public readonly int ColumnIndex;
            public readonly int RowIndex;
            public readonly string Reference;
            public readonly string Text;

            public CommentPosition(int columnIndex, int rowIndex, string reference, string text)
            {
                ColumnIndex = columnIndex;
                RowIndex = rowIndex;
                Reference = reference;
                Text = text;
            }
        }
    }
}
#endif
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using HexMap.Gvg.Authoring;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEngine;

namespace HexMap.Gvg.Editor
{
    public sealed class GvgMapExcelImportResult
    {
        internal GvgMapExcelImportResult(
            string sourcePath,
            int importedRowCount,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings,
            bool applied)
        {
            SourcePath = sourcePath;
            ImportedRowCount = importedRowCount;
            Errors = errors;
            Warnings = warnings;
            Applied = applied;
        }

        public string SourcePath { get; private set; }
        public int ImportedRowCount { get; private set; }
        public IReadOnlyList<string> Errors { get; private set; }
        public IReadOnlyList<string> Warnings { get; private set; }
        public bool Applied { get; private set; }
        public bool IsValid { get { return Errors.Count == 0; } }
    }

    /// <summary>
    /// Imports a GVG map Excel workbook by column name. The column-name row is the
    /// row within the first 5 rows that contains the <see cref="ColumnHexIds"/>
    /// header; every data row is then read through a name-to-column mapping, so no
    /// column-letter assumptions are made. Columns not in the logical constant table
    /// are redundant and are recorded verbatim so the exporter can write them back.
    /// </summary>
    public static class GvgMapExcelImporter
    {
        private static readonly Regex s_HexIdPattern = new Regex(
            "[0-9]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex s_ArrayPattern = new Regex(
            "^\\s*\\[.*\\]\\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        /// <summary>Logical PlotId column header name.</summary>
        public const string ColumnPlotId = "ID";

        /// <summary>Logical HexIds column header name.</summary>
        public const string ColumnHexIds = "Coordinates";

        /// <summary>Legacy GenerationType column header name (recomputed from Start).</summary>
        public const string ColumnGenerationType = "Type";

        /// <summary>Logical PlotType column header name.</summary>
        public const string ColumnPlotType = "GridType";

        /// <summary>Logical AffiliatedCampId column header name.</summary>
        public const string ColumnAffiliatedCampId = "Safe";

        /// <summary>Logical Start column header name.</summary>
        public const string ColumnStart = "Start";

        /// <summary>Logical End column header name.</summary>
        public const string ColumnEnd = "End";

        private static readonly string[] s_LogicalColumns =
        {
            ColumnPlotId,
            ColumnHexIds,
            ColumnGenerationType,
            ColumnPlotType,
            ColumnAffiliatedCampId,
            ColumnStart,
            ColumnEnd
        };

        public static GvgMapExcelImportResult Import(string path, GvgMapAuthoringAsset targetAsset, RuntimeHexMap map)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Excel path is required.", nameof(path));
            if (targetAsset == null) throw new ArgumentNullException(nameof(targetAsset));
            if (map == null) throw new ArgumentNullException(nameof(map));

            var errors = new List<string>();
            var warnings = new List<string>();
            var document = ReadDocument(path, errors, warnings);
            if (errors.Count > 0)
            {
                return new GvgMapExcelImportResult(path, document.Rows.Count, errors, warnings, false);
            }

            var importedPlots = BuildPlots(document, errors, warnings);
            if (errors.Count > 0)
            {
                return new GvgMapExcelImportResult(path, document.Rows.Count, errors, warnings, false);
            }

            var candidate = ScriptableObject.CreateInstance<GvgMapAuthoringAsset>();
            try
            {
                candidate.MapId = targetAsset.MapId;
                candidate.ReplacePlots(importedPlots);
                candidate.ReplaceExcelRedundancy(BuildRedundancy(document));

                GvgMapAuthoringUtility.NormalizePlotIds(candidate, map);

                var validation = GvgMapAuthoringUtility.Validate(candidate, map);
                for (var index = 0; index < validation.Issues.Count; index++)
                {
                    errors.Add(validation.Issues[index].Message);
                }

                if (errors.Count > 0)
                {
                    return new GvgMapExcelImportResult(path, document.Rows.Count, errors, warnings, false);
                }

                for (var index = 0; index < candidate.Plots.Count; index++)
                {
                    var plot = candidate.Plots[index];
                    if (plot.PlotId != importedPlots[index].PlotId)
                    {
                        warnings.Add("PlotId for imported row " + (index + 1) + " was normalized to " + plot.PlotId + ".");
                    }
                }

                targetAsset.ReplacePlots(candidate.Plots);
                targetAsset.ReplaceExcelRedundancy(candidate.ExcelRedundancy);
                return new GvgMapExcelImportResult(path, document.Rows.Count, errors, warnings, true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        private sealed class RawCell
        {
            public string Text = string.Empty;
            public string FillColor = string.Empty;
            public string FontColor = string.Empty;
            public string Comment = string.Empty;
            public int ColumnIndex;
        }

        private sealed class RawRow
        {
            public int Number;
            public readonly Dictionary<int, RawCell> ByColumn = new Dictionary<int, RawCell>();
        }

        private sealed class ParsedRow
        {
            public int SourceId;
            public List<int> HexIds = new List<int>();
            public PlotType PlotType;
            public int Start;
            public int End;
            public int AffiliatedCampId;
            public Dictionary<string, string> ColumnContent = new Dictionary<string, string>();
        }

        private sealed class ExcelDocument
        {
            public readonly List<string> ColumnOrder = new List<string>();
            public readonly Dictionary<string, int> NameToIndex = new Dictionary<string, int>();
            public readonly Dictionary<string, GvgExcelColumnKind> Kinds = new Dictionary<string, GvgExcelColumnKind>();
            public readonly List<List<RawCell>> HeaderBlock = new List<List<RawCell>>();
            public readonly List<ParsedRow> Rows = new List<ParsedRow>();
        }

        private sealed class WorkbookStyles
        {
            public readonly List<string> FontColors = new List<string>();
            public readonly List<string> FillColors = new List<string>();
            public readonly List<int> XfFontIds = new List<int>();
            public readonly List<int> XfFillIds = new List<int>();

            public void Resolve(int styleIndex, out string fillColor, out string fontColor)
            {
                fillColor = string.Empty;
                fontColor = string.Empty;
                if (styleIndex < 0 || styleIndex >= XfFillIds.Count || styleIndex >= XfFontIds.Count) return;

                var fillId = XfFillIds[styleIndex];
                var fontId = XfFontIds[styleIndex];
                if (fillId >= 0 && fillId < FillColors.Count) fillColor = FillColors[fillId];
                if (fontId >= 0 && fontId < FontColors.Count) fontColor = FontColors[fontId];
            }
        }

        private static ExcelDocument ReadDocument(
            string path,
            List<string> errors,
            List<string> warnings)
        {
            var document = new ExcelDocument();
            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    var sharedStrings = ReadSharedStrings(archive);
                    var styles = ReadStyles(archive);
                    var comments = ReadComments(archive);
                    var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml");
                    if (worksheet == null)
                    {
                        errors.Add("The workbook does not contain xl/worksheets/sheet1.xml.");
                        return document;
                    }

                    var xml = new XmlDocument();
                    using (var stream = worksheet.Open())
                    {
                        xml.Load(stream);
                    }

                    var rows = new List<RawRow>();
                    var rowNodes = xml.GetElementsByTagName("row");
                    for (var index = 0; index < rowNodes.Count; index++)
                    {
                        var rowNode = rowNodes[index] as XmlElement;
                        if (rowNode == null) continue;
                        rows.Add(ReadRow(rowNode, index + 1, sharedStrings, styles, comments));
                    }

                    BuildHeaderBlock(document, rows, errors);
                    if (errors.Count > 0) return document;
                    BuildDataRows(document, rows, errors, warnings);
                }
            }
            catch (Exception exception)
            {
                errors.Add("Failed to read Excel workbook: " + exception.Message);
            }

            return document;
        }

        private static void BuildHeaderBlock(ExcelDocument document, List<RawRow> rows, List<string> errors)
        {
            // The column-name row is the row within the first 5 rows that contains the
            // Coordinates header.
            RawRow headerRow = null;
            for (var index = 0; index < rows.Count && index < 5; index++)
            {
                if (rows[index].ByColumn.Values.Any(cell =>
                    string.Equals(cell.Text, ColumnHexIds, StringComparison.OrdinalIgnoreCase)))
                {
                    headerRow = rows[index];
                    break;
                }
            }

            if (headerRow == null)
            {
                errors.Add("The workbook does not contain the expected " + ColumnHexIds + " header row within the first 5 rows.");
                return;
            }

            var nameToIndex = new Dictionary<string, int>();
            var ordered = headerRow.ByColumn.OrderBy(pair => pair.Key).ToList();
            for (var index = 0; index < ordered.Count; index++)
            {
                var columnIndex = ordered[index].Key;
                var name = ordered[index].Value.Text;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (nameToIndex.ContainsKey(name))
                {
                    errors.Add("Duplicate column name: " + name + ".");
                }
                else
                {
                    nameToIndex.Add(name, columnIndex);
                    document.ColumnOrder.Add(name);
                    document.NameToIndex.Add(name, columnIndex);
                }
            }

            for (var index = 0; index < s_LogicalColumns.Length; index++)
            {
                if (!nameToIndex.ContainsKey(s_LogicalColumns[index]))
                {
                    errors.Add("缺少列：" + s_LogicalColumns[index]);
                }
            }

            if (errors.Count > 0) return;

            // Per-column types come from the explicit type row (a row among the first 5
            // whose cells are all type tokens); otherwise they are inferred from data.
            var typeRow = FindTypeRow(rows, headerRow);
            for (var index = 0; index < document.ColumnOrder.Count; index++)
            {
                var name = document.ColumnOrder[index];
                var columnIndex = nameToIndex[name];
                document.Kinds[name] = typeRow != null
                    ? TypeTokenToKind(GetCellText(typeRow, columnIndex))
                    : GvgExcelColumnKind.String;
            }

            // Header block: first 5 rows, one cell per column in column order.
            for (var rowIndex = 0; rowIndex < 5; rowIndex++)
            {
                var cells = new List<RawCell>();
                for (var index = 0; index < document.ColumnOrder.Count; index++)
                {
                    var name = document.ColumnOrder[index];
                    var columnIndex = nameToIndex[name];
                    var cell = rowIndex < rows.Count && rows[rowIndex].ByColumn.TryGetValue(columnIndex, out var raw)
                        ? raw
                        : null;
                    cells.Add(cell ?? new RawCell { ColumnIndex = columnIndex });
                }

                document.HeaderBlock.Add(cells);
            }
        }

        private static void BuildDataRows(ExcelDocument document, List<RawRow> rows, List<string> errors, List<string> warnings)
        {
            // Data starts after the 5-row header block.
            for (var index = 5; index < rows.Count; index++)
            {
                var raw = rows[index];
                var valuesByName = new Dictionary<string, string>();
                for (var columnIndex = 0; columnIndex < document.ColumnOrder.Count; columnIndex++)
                {
                    var name = document.ColumnOrder[columnIndex];
                    var cell = raw.ByColumn.TryGetValue(document.NameToIndex[name], out var rawCell)
                        ? rawCell
                        : null;
                    valuesByName[name] = cell == null ? string.Empty : cell.Text;
                }

                string idValue;
                if (valuesByName.TryGetValue(ColumnPlotId, out idValue) &&
                    string.Equals(idValue.Trim(), "c/s", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string coordinatesValue;
                if (!valuesByName.TryGetValue(ColumnHexIds, out coordinatesValue) ||
                    string.IsNullOrWhiteSpace(coordinatesValue))
                {
                    continue;
                }

                ParsedRow row;
                if (!TryParseRow(valuesByName, raw.Number, warnings, out row, errors)) continue;
                document.Rows.Add(row);
            }

            if (document.Rows.Count == 0)
            {
                errors.Add("The workbook contains no importable Plot rows.");
            }
        }

        private static string GetCellText(RawRow row, int columnIndex)
        {
            RawCell cell;
            return row.ByColumn.TryGetValue(columnIndex, out cell) ? cell.Text : string.Empty;
        }

        private static RawRow FindTypeRow(List<RawRow> rows, RawRow headerRow)
        {
            for (var index = 0; index < rows.Count && index < 5; index++)
            {
                var row = rows[index];
                if (ReferenceEquals(row, headerRow)) continue;

                var hasToken = false;
                var allTokens = true;
                foreach (var pair in row.ByColumn)
                {
                    var value = pair.Value.Text.Trim();
                    if (value.Length == 0) continue;
                    if (!IsTypeToken(value))
                    {
                        allTokens = false;
                        break;
                    }

                    hasToken = true;
                }

                if (hasToken && allTokens) return row;
            }

            return null;
        }

        private static bool IsTypeToken(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "int":
                case "integer":
                case "int32":
                case "int64":
                case "long":
                case "short":
                case "byte":
                case "float":
                case "double":
                case "bool":
                case "boolean":
                case "string":
                case "note":
                case "list":
                case "array":
                case "int[]":
                case "string[]":
                    return true;
                default:
                    return value.EndsWith("[]", StringComparison.Ordinal);
            }
        }

        private static GvgExcelColumnKind TypeTokenToKind(string token)
        {
            if (string.IsNullOrEmpty(token)) return GvgExcelColumnKind.String;
            var value = token.Trim().ToLowerInvariant();
            if (value == "int[]" || value == "string[]" || value == "list" || value == "array" || value.EndsWith("[]", StringComparison.Ordinal))
            {
                return GvgExcelColumnKind.IntArray;
            }

            if (value == "int" || value == "integer" || value == "int32" || value == "int64" ||
                value == "long" || value == "short" || value == "byte" ||
                value == "float" || value == "double" || value == "bool" || value == "boolean")
            {
                return GvgExcelColumnKind.Int;
            }

            return GvgExcelColumnKind.String;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var strings = new List<string>();
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return strings;

            var document = new XmlDocument();
            using (var stream = entry.Open())
            {
                document.Load(stream);
            }

            var nodes = document.GetElementsByTagName("si");
            for (var index = 0; index < nodes.Count; index++)
            {
                strings.Add(nodes[index].InnerText);
            }

            return strings;
        }

        private static WorkbookStyles ReadStyles(ZipArchive archive)
        {
            var styles = new WorkbookStyles();
            var entry = archive.GetEntry("xl/styles.xml");
            if (entry == null) return styles;

            var document = new XmlDocument();
            using (var stream = entry.Open())
            {
                document.Load(stream);
            }

            var fonts = document.GetElementsByTagName("font");
            for (var index = 0; index < fonts.Count; index++)
            {
                var colors = ((XmlElement)fonts[index]).GetElementsByTagName("color");
                styles.FontColors.Add(colors.Count > 0 ? ((XmlElement)colors[0]).GetAttribute("rgb") : string.Empty);
            }

            var fills = document.GetElementsByTagName("fill");
            for (var index = 0; index < fills.Count; index++)
            {
                var patterns = ((XmlElement)fills[index]).GetElementsByTagName("patternFill");
                var color = string.Empty;
                if (patterns.Count > 0)
                {
                    var fgColors = ((XmlElement)patterns[0]).GetElementsByTagName("fgColor");
                    if (fgColors.Count > 0) color = ((XmlElement)fgColors[0]).GetAttribute("rgb");
                }

                styles.FillColors.Add(color);
            }

            var cellXfs = document.GetElementsByTagName("cellXfs");
            if (cellXfs.Count > 0)
            {
                var xfs = ((XmlElement)cellXfs[0]).GetElementsByTagName("xf");
                for (var index = 0; index < xfs.Count; index++)
                {
                    var xf = (XmlElement)xfs[index];
                    int fontId;
                    int fillId;
                    if (!int.TryParse(xf.GetAttribute("fontId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out fontId)) fontId = 0;
                    if (!int.TryParse(xf.GetAttribute("fillId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out fillId)) fillId = 0;
                    styles.XfFontIds.Add(fontId);
                    styles.XfFillIds.Add(fillId);
                }
            }

            return styles;
        }

        private static Dictionary<int, Dictionary<int, string>> ReadComments(ZipArchive archive)
        {
            var comments = new Dictionary<int, Dictionary<int, string>>();
            var entry = archive.GetEntry("xl/comments1.xml");
            if (entry == null) return comments;

            var document = new XmlDocument();
            using (var stream = entry.Open())
            {
                document.Load(stream);
            }

            var nodes = document.GetElementsByTagName("comment");
            for (var index = 0; index < nodes.Count; index++)
            {
                var comment = nodes[index] as XmlElement;
                if (comment == null) continue;

                var reference = comment.GetAttribute("ref");
                var textNodes = comment.GetElementsByTagName("text");
                var text = textNodes.Count > 0 ? textNodes[0].InnerText : string.Empty;

                var columnIndex = ColumnIndexFromReference(reference, -1);
                var digits = new string(reference.SkipWhile(ch => !char.IsDigit(ch)).ToArray());
                int rowNumber;
                if (columnIndex < 0 || !int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out rowNumber)) continue;

                Dictionary<int, string> byColumn;
                if (!comments.TryGetValue(rowNumber, out byColumn))
                {
                    byColumn = new Dictionary<int, string>();
                    comments.Add(rowNumber, byColumn);
                }

                byColumn[columnIndex] = text;
            }

            return comments;
        }

        private static RawRow ReadRow(
            XmlElement rowNode,
            int fallbackRowNumber,
            List<string> sharedStrings,
            WorkbookStyles styles,
            Dictionary<int, Dictionary<int, string>> comments)
        {
            var row = new RawRow { Number = ReadRowNumber(rowNode, fallbackRowNumber) };
            var cells = rowNode.GetElementsByTagName("c");
            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index] as XmlElement;
                if (cell == null) continue;

                var reference = cell.GetAttribute("r");
                var columnIndex = ColumnIndexFromReference(reference, index);
                var text = ReadCellValue(cell, sharedStrings);

                string fillColor;
                string fontColor;
                styles.Resolve(ReadStyleIndex(cell), out fillColor, out fontColor);

                var raw = new RawCell
                {
                    Text = text,
                    FillColor = fillColor,
                    FontColor = fontColor,
                    Comment = ReadComment(comments, row.Number, columnIndex),
                    ColumnIndex = columnIndex
                };
                row.ByColumn[columnIndex] = raw;
            }

            return row;
        }

        private static int ReadRowNumber(XmlElement rowNode, int fallback)
        {
            int number;
            return int.TryParse(rowNode.GetAttribute("r"), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? number
                : fallback;
        }

        private static int ReadStyleIndex(XmlElement cell)
        {
            int styleIndex;
            return int.TryParse(cell.GetAttribute("s"), NumberStyles.Integer, CultureInfo.InvariantCulture, out styleIndex)
                ? styleIndex
                : -1;
        }

        private static int ColumnIndexFromReference(string reference, int fallback)
        {
            var letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
            if (letters.Length == 0) return fallback;

            var index = 0;
            for (var i = 0; i < letters.Length; i++)
            {
                index = index * 26 + (char.ToUpperInvariant(letters[i]) - 'A' + 1);
            }

            return index - 1;
        }

        private static string ReadComment(Dictionary<int, Dictionary<int, string>> comments, int rowNumber, int columnIndex)
        {
            Dictionary<int, string> byColumn;
            if (comments != null && comments.TryGetValue(rowNumber, out byColumn))
            {
                string text;
                if (byColumn.TryGetValue(columnIndex, out text)) return text;
            }

            return string.Empty;
        }

        private static string ReadCellValue(XmlElement cell, List<string> sharedStrings)
        {
            var type = cell.GetAttribute("t");
            var valueNodes = cell.GetElementsByTagName("v");

            if (type == "s")
            {
                if (valueNodes.Count == 0) return string.Empty;
                int sharedIndex;
                if (int.TryParse(valueNodes[0].InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out sharedIndex) &&
                    sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                {
                    return sharedStrings[sharedIndex];
                }

                return string.Empty;
            }

            if (type == "str" || type == "inlineStr")
            {
                var inlineNodes = cell.GetElementsByTagName("is");
                return inlineNodes.Count > 0 ? inlineNodes[0].InnerText : string.Empty;
            }

            if (valueNodes.Count > 0) return valueNodes[0].InnerText;

            var inline = cell.GetElementsByTagName("is");
            return inline.Count > 0 ? inline[0].InnerText : string.Empty;
        }

        private static bool TryParseRow(
            Dictionary<string, string> values,
            int rowNumber,
            List<string> warnings,
            out ParsedRow row,
            List<string> errors)
        {
            row = null;
            int sourceId;
            if (!TryParseInt(values, ColumnPlotId, rowNumber, "ID", out sourceId, errors)) return false;

            var hexIds = new List<int>();
            string coordinates;
            if (!values.TryGetValue(ColumnHexIds, out coordinates)) coordinates = string.Empty;
            foreach (Match match in s_HexIdPattern.Matches(coordinates))
            {
                int hexId;
                if (int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out hexId))
                {
                    hexIds.Add(hexId);
                }
            }

            if (hexIds.Count == 0)
            {
                errors.Add("Row " + rowNumber + " has no HexIds in Coordinates.");
                return false;
            }

            int plotTypeValue;
            if (!TryParseInt(values, ColumnPlotType, rowNumber, "PlotType", out plotTypeValue, errors)) return false;
            if (!Enum.IsDefined(typeof(PlotType), plotTypeValue))
            {
                errors.Add("Row " + rowNumber + " has undefined PlotType: " + plotTypeValue + ".");
                return false;
            }

            int start;
            if (!TryParseInt(values, ColumnStart, rowNumber, "Start", out start, errors)) return false;
            int end;
            if (!TryParseInt(values, ColumnEnd, rowNumber, "End", out end, errors)) return false;

            var affiliatedCampId = Plot.NoAffiliatedCampId;
            string safeText;
            if (values.TryGetValue(ColumnAffiliatedCampId, out safeText) && !string.IsNullOrWhiteSpace(safeText))
            {
                int safe;
                if (!TryParseInt(values, ColumnAffiliatedCampId, rowNumber, "Safe", out safe, errors)) return false;
                if (safe < Plot.NoAffiliatedCampId)
                {
                    errors.Add("Row " + rowNumber + " has a Safe value smaller than -1.");
                    return false;
                }

                affiliatedCampId = safe == 0 ? Plot.NoAffiliatedCampId : safe;
            }

            int generationType;
            if (TryParseInt(values, ColumnGenerationType, rowNumber, "legacy GenerationType", out generationType, errors))
            {
                var expectedInitial = start == 0 ? 0 : 1;
                if (generationType != expectedInitial)
                {
                    warnings.Add("Row " + rowNumber + " legacy GenerationType does not match Start and will be ignored.");
                }
            }

            row = new ParsedRow
            {
                SourceId = sourceId,
                HexIds = hexIds,
                PlotType = (PlotType)plotTypeValue,
                Start = start,
                End = end,
                AffiliatedCampId = affiliatedCampId,
                ColumnContent = new Dictionary<string, string>(values)
            };
            return true;
        }

        private static bool TryParseInt(
            Dictionary<string, string> values,
            string column,
            int rowNumber,
            string name,
            out int value,
            List<string> errors)
        {
            value = 0;
            string text;
            if (!values.TryGetValue(column, out text) ||
                !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                errors.Add("Row " + rowNumber + " has invalid " + name + " value.");
                return false;
            }

            return true;
        }

        private static List<GvgPlotAuthoringData> BuildPlots(
            ExcelDocument document,
            List<string> errors,
            List<string> warnings)
        {
            var plots = new List<GvgPlotAuthoringData>(document.Rows.Count);
            var singleRowsByHex = new Dictionary<int, List<GvgPlotAuthoringData>>();

            for (var index = 0; index < document.Rows.Count; index++)
            {
                var row = document.Rows[index];
                var plot = new GvgPlotAuthoringData(
                    row.SourceId,
                    row.HexIds,
                    row.PlotType,
                    row.Start,
                    row.End,
                    row.AffiliatedCampId);
                plots.Add(plot);

                if (plot.IsMultiCell)
                {
                    if (plot.Start != 0 || plot.End != -1)
                    {
                        errors.Add("Imported multi-cell row " + (index + 1) + " must use Start=0 and End=-1.");
                    }

                    continue;
                }

                var hexId = plot.HexIds[0];
                List<GvgPlotAuthoringData> layers;
                if (!singleRowsByHex.TryGetValue(hexId, out layers))
                {
                    layers = new List<GvgPlotAuthoringData>();
                    singleRowsByHex.Add(hexId, layers);
                }

                layers.Add(plot);
            }

            foreach (var pair in singleRowsByHex)
            {
                var layers = pair.Value;
                layers.Sort(CompareLayers);
                for (var index = 1; index < layers.Count; index++)
                {
                    var previous = layers[index - 1];
                    var current = layers[index];
                    if (previous.PlotType != current.PlotType)
                    {
                        errors.Add("HexId " + pair.Key + " has multiple PlotTypes.");
                    }

                    if (previous.End == -1)
                    {
                        previous.End = current.Start;
                        warnings.Add("HexId " + pair.Key + " previous End=-1 was corrected to " + current.Start + ".");
                    }
                    else if (previous.End != current.Start)
                    {
                        errors.Add("HexId " + pair.Key + " has a gap or overlap between time layers.");
                    }
                }

                if (layers.Count > 0 && layers[0].Start < 0)
                {
                    errors.Add("HexId " + pair.Key + " has a negative Start.");
                }

                if (layers.Count > 0 && layers[layers.Count - 1].End != -1)
                {
                    errors.Add("HexId " + pair.Key + " final time layer must use End=-1.");
                }
            }

            return plots;
        }

        private static GvgExcelDocumentRedundancy BuildRedundancy(ExcelDocument document)
        {
            var redundancy = new GvgExcelDocumentRedundancy();

            var headerRows = new List<GvgExcelHeaderRow>();
            for (var rowIndex = 0; rowIndex < document.HeaderBlock.Count; rowIndex++)
            {
                var blockRow = document.HeaderBlock[rowIndex];
                var headerRow = new GvgExcelHeaderRow();
                var cells = new List<GvgExcelHeaderCell>();
                for (var columnIndex = 0; columnIndex < document.ColumnOrder.Count; columnIndex++)
                {
                    var cell = blockRow[columnIndex];
                    cells.Add(new GvgExcelHeaderCell(
                        document.ColumnOrder[columnIndex],
                        cell.Text,
                        cell.FillColor,
                        cell.FontColor,
                        cell.Comment));
                }

                headerRow.ReplaceCells(cells);
                headerRows.Add(headerRow);
            }

            redundancy.ReplaceHeaderRows(headerRows);

            var columns = new List<GvgExcelColumnInfo>();
            for (var index = 0; index < document.ColumnOrder.Count; index++)
            {
                var name = document.ColumnOrder[index];
                columns.Add(new GvgExcelColumnInfo(name, document.Kinds[name]));
            }

            redundancy.ReplaceColumns(columns);

            var dataRows = new List<GvgExcelRowData>();
            for (var index = 0; index < document.Rows.Count; index++)
            {
                var parsed = document.Rows[index];
                var data = new GvgExcelRowData { PlotId = parsed.SourceId };
                var contents = new List<GvgExcelColumnContent>();
                for (var columnIndex = 0; columnIndex < document.ColumnOrder.Count; columnIndex++)
                {
                    var name = document.ColumnOrder[columnIndex];
                    string content;
                    contents.Add(new GvgExcelColumnContent(
                        name,
                        parsed.ColumnContent.TryGetValue(name, out content) ? content : string.Empty));
                }

                data.ReplaceColumns(contents);
                dataRows.Add(data);
            }

            redundancy.ReplaceRows(dataRows);
            return redundancy;
        }

        private static int CompareLayers(GvgPlotAuthoringData left, GvgPlotAuthoringData right)
        {
            var result = left.Start.CompareTo(right.Start);
            return result != 0 ? result : left.PlotId.CompareTo(right.PlotId);
        }
    }
}
#endif
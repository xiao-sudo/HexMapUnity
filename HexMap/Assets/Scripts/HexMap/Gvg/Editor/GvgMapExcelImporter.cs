using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using HexMap.Gvg.Authoring;
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

    public static class GvgMapExcelImporter
    {
        private static readonly Regex s_HexIdPattern = new Regex(
            "[0-9]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static GvgMapExcelImportResult Import(string path, GvgMapAuthoringAsset targetAsset)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Excel path is required.", nameof(path));
            if (targetAsset == null) throw new ArgumentNullException(nameof(targetAsset));

            var errors = new List<string>();
            var warnings = new List<string>();
            var rows = ReadRows(path, errors, warnings);
            if (errors.Count > 0)
            {
                return new GvgMapExcelImportResult(path, rows.Count, errors, warnings, false);
            }

            var importedPlots = NormalizeRows(rows, targetAsset, errors, warnings);
            if (errors.Count > 0)
            {
                return new GvgMapExcelImportResult(path, rows.Count, errors, warnings, false);
            }

            var candidate = ScriptableObject.CreateInstance<GvgMapAuthoringAsset>();
            try
            {
                candidate.MapId = targetAsset.MapId;
                candidate.Radius = targetAsset.Radius;
                candidate.Orientation = targetAsset.Orientation;
                candidate.Plane = targetAsset.Plane;
                candidate.OuterRadius = targetAsset.OuterRadius;
                candidate.SecondaryScale = targetAsset.SecondaryScale;
                candidate.ReplacePlots(importedPlots);
                GvgMapAuthoringUtility.NormalizePlotIds(candidate);

                var validation = GvgMapAuthoringUtility.Validate(candidate);
                for (var index = 0; index < validation.Issues.Count; index++)
                {
                    errors.Add(validation.Issues[index].Message);
                }

                if (errors.Count > 0)
                {
                    return new GvgMapExcelImportResult(path, rows.Count, errors, warnings, false);
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
                return new GvgMapExcelImportResult(path, rows.Count, errors, warnings, true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        private static List<ImportedRow> ReadRows(
            string path,
            List<string> errors,
            List<string> warnings)
        {
            var headerFound = false;
            var rows = new List<ImportedRow>();
            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    var sharedStrings = ReadSharedStrings(archive);
                    var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml");
                    if (worksheet == null)
                    {
                        errors.Add("The workbook does not contain xl/worksheets/sheet1.xml.");
                        return rows;
                    }

                    var document = new XmlDocument();
                    using (var stream = worksheet.Open())
                    {
                        document.Load(stream);
                    }

                    var rowNodes = document.GetElementsByTagName("row");
                    for (var rowIndex = 0; rowIndex < rowNodes.Count; rowIndex++)
                    {
                        var rowNode = rowNodes[rowIndex] as XmlElement;
                        if (rowNode == null) continue;

                        var values = ReadRow(rowNode, sharedStrings);
                        if (!headerFound)
                        {
                            if (values.ContainsKey("C") && values["C"] == "Coordinates")
                            {
                                headerFound = true;
                            }

                            continue;
                        }

                        if (!values.ContainsKey("C") || string.IsNullOrWhiteSpace(values["C"])) continue;
                        if (values.ContainsKey("A") && string.Equals(values["A"], "c/s", StringComparison.OrdinalIgnoreCase)) continue;

                        ImportedRow row;
                        if (!TryParseRow(values, rowIndex + 1, warnings, out row, errors)) continue;
                        rows.Add(row);
                    }
                }
            }
            catch (Exception exception)
            {
                errors.Add("Failed to read Excel workbook: " + exception.Message);
            }

            if (!headerFound)
            {
                errors.Add("The workbook does not contain the expected Coordinates header row.");
            }

            if (rows.Count == 0)
            {
                errors.Add("The workbook contains no importable Plot rows.");
            }

            return rows;
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

        private static Dictionary<string, string> ReadRow(XmlElement rowNode, List<string> sharedStrings)
        {
            var values = new Dictionary<string, string>();
            var cells = rowNode.GetElementsByTagName("c");
            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index] as XmlElement;
                if (cell == null) continue;

                var reference = cell.GetAttribute("r");
                var column = new string(reference.TakeWhile(char.IsLetter).ToArray());
                var valueNode = cell.GetElementsByTagName("v").Count == 0
                    ? null
                    : cell.GetElementsByTagName("v")[0];

                var value = valueNode == null ? string.Empty : valueNode.InnerText;
                if (cell.GetAttribute("t") == "s" && value.Length > 0)
                {
                    int sharedIndex;
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sharedIndex) &&
                        sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                    {
                        value = sharedStrings[sharedIndex];
                    }
                }

                values[column] = value;
            }

            return values;
        }

        private static bool TryParseRow(
            Dictionary<string, string> values,
            int rowNumber,
            List<string> warnings,
            out ImportedRow row,
            List<string> errors)
        {
            row = null;
            int sourceId;
            if (!TryParseInt(values, "A", rowNumber, "ID", out sourceId, errors)) return false;

            var hexIds = new List<int>();
            foreach (Match match in s_HexIdPattern.Matches(values["C"]))
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
            if (!TryParseInt(values, "E", rowNumber, "PlotType", out plotTypeValue, errors)) return false;
            if (!Enum.IsDefined(typeof(PlotType), plotTypeValue))
            {
                errors.Add("Row " + rowNumber + " has undefined PlotType: " + plotTypeValue + ".");
                return false;
            }

            int start;
            if (!TryParseInt(values, "I", rowNumber, "Start", out start, errors)) return false;
            int end;
            if (!TryParseInt(values, "J", rowNumber, "End", out end, errors)) return false;

            int generationType;
            if (TryParseInt(values, "D", rowNumber, "legacy GenerationType", out generationType, errors))
            {
                var expectedInitial = start == 0 ? 0 : 1;
                if (generationType != expectedInitial)
                {
                    warnings.Add("Row " + rowNumber + " legacy GenerationType does not match Start and will be ignored.");
                }
            }

            row = new ImportedRow(
                sourceId,
                hexIds,
                (PlotType)plotTypeValue,
                start,
                end);
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

        private static List<GvgPlotAuthoringData> NormalizeRows(
            List<ImportedRow> rows,
            GvgMapAuthoringAsset targetAsset,
            List<string> errors,
            List<string> warnings)
        {
            var plots = new List<GvgPlotAuthoringData>(rows.Count);
            var singleRowsByHex = new Dictionary<int, List<GvgPlotAuthoringData>>();

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var plot = new GvgPlotAuthoringData(
                    row.SourceId,
                    row.HexIds,
                    row.PlotType,
                    row.Start,
                    row.End);
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

        private static int CompareLayers(GvgPlotAuthoringData left, GvgPlotAuthoringData right)
        {
            var result = left.Start.CompareTo(right.Start);
            return result != 0 ? result : left.PlotId.CompareTo(right.PlotId);
        }

        private sealed class ImportedRow
        {
            public ImportedRow(int sourceId, List<int> hexIds, PlotType plotType, int start, int end)
            {
                SourceId = sourceId;
                HexIds = hexIds;
                PlotType = plotType;
                Start = start;
                End = end;
            }

            public int SourceId { get; private set; }
            public List<int> HexIds { get; private set; }
            public PlotType PlotType { get; private set; }
            public int Start { get; private set; }
            public int End { get; private set; }
        }
    }
}

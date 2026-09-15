using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using HexMap.Gvg.Authoring;
using HexMap.Gvg.Editor;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using NUnit.Framework;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace HexMap.Gvg.Tests
{
    /// <summary>
    /// Round-trip tests for the Excel (.xlsx) import/export pipeline. A tiny xlsx
    /// workbook is generated in-memory with <see cref="TestWorkbook"/>, imported into
    /// a GVG authoring asset, exported again, and the result is re-imported so the
    /// three representations (imported asset, exported workbook, re-imported asset)
    /// can be compared.
    /// </summary>
    [TestFixture]
    public sealed class GvgMapExcelRoundTripTests
    {
        private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        [Test]
        public void ImportExportImport_RoundTripsStably()
        {
            var map = CreateMap(1);
            var firstAsset = CreateAsset();
            var secondAsset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                var sourcePath = Path.Combine(directory, "source.xlsx");
                CreateWorkbook(sourcePath, BuildDefaultWorkbook());

                var import = GvgMapExcelImporter.Import(sourcePath, firstAsset, map);
                Assert.That(import.IsValid, Is.True, string.Join(" | ", import.Errors));
                Assert.That(import.Applied, Is.True);
                Assert.That(import.ImportedRowCount, Is.EqualTo(6));

                AssertImportedPlots(firstAsset);
                AssertImportedRedundancy(firstAsset);

                var exportPath = GvgMapExcelExporter.ExportExcel(firstAsset, map, directory);
                Assert.That(File.Exists(exportPath), Is.True);

                var secondImport = GvgMapExcelImporter.Import(exportPath, secondAsset, map);
                Assert.That(secondImport.IsValid, Is.True, string.Join(" | ", secondImport.Errors));
                Assert.That(secondImport.Applied, Is.True);

                AssertEquivalentPlots(firstAsset, secondAsset);
                AssertEquivalentRedundancy(firstAsset.ExcelRedundancy, secondAsset.ExcelRedundancy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstAsset);
                UnityEngine.Object.DestroyImmediate(secondAsset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Export_UsesLogicalAndRedundantColumnNames()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                var sourcePath = Path.Combine(directory, "source.xlsx");
                CreateWorkbook(sourcePath, BuildDefaultWorkbook());
                var import = GvgMapExcelImporter.Import(sourcePath, asset, map);
                Assert.That(import.IsValid, Is.True, string.Join(" | ", import.Errors));

                var exportPath = GvgMapExcelExporter.ExportExcel(asset, map, directory);
                var cells = ReadWorkbookCells(exportPath);

                // The 5-row header block is preserved.
                for (var row = 1; row <= 5; row++)
                {
                    Assert.That(cells.ContainsKey(row), Is.True, "Header row " + row + " is missing.");
                }

                var logicalRow = cells[3];
                Assert.That(logicalRow[0], Is.EqualTo("ID"));
                Assert.That(logicalRow[1], Is.EqualTo("Coordinates"));
                Assert.That(logicalRow[2], Is.EqualTo("Type"));
                Assert.That(logicalRow[3], Is.EqualTo("GridType"));
                Assert.That(logicalRow[4], Is.EqualTo("Safe"));
                Assert.That(logicalRow[5], Is.EqualTo("Start"));
                Assert.That(logicalRow[6], Is.EqualTo("End"));
                Assert.That(logicalRow[7], Is.EqualTo("Note"), "Redundant string column must be exported.");
                Assert.That(logicalRow[8], Is.EqualTo("Coin"), "Redundant int column must be exported.");

                var markerRow = cells[5];
                Assert.That(markerRow[0], Is.EqualTo("c/s"));

                // Sorted data region: multi-cell plot first, then single-cell plots.
                var dataRows = cells.Where(pair => pair.Key >= 6).OrderBy(pair => pair.Key).ToList();
                Assert.That(dataRows.Count, Is.EqualTo(6));
                Assert.That(dataRows[0].Value[0], Is.EqualTo("12000"));
                Assert.That(dataRows[0].Value[7], Is.EqualTo("大营"));
                Assert.That(dataRows[0].Value[8], Is.EqualTo("100"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Import_IgnoresColumnOrder()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                var path = Path.Combine(directory, "reordered.xlsx");
                CreateWorkbook(path, BuildReorderedWorkbook());

                var import = GvgMapExcelImporter.Import(path, asset, map);
                Assert.That(import.IsValid, Is.True, string.Join(" | ", import.Errors));
                Assert.That(import.Applied, Is.True);

                AssertImportedPlots(asset);

                var redundancy = asset.ExcelRedundancy;
                Assert.That(redundancy, Is.Not.Null);
                Assert.That(redundancy.GetColumnContent(12000, "Coin"), Is.EqualTo("100"));
                Assert.That(redundancy.GetColumnContent(12000, "Note"), Is.EqualTo("大营"));
                Assert.That(redundancy.GetColumnContent(12000, "Coordinates"), Is.EqualTo("[0,1]"));
                Assert.That(redundancy.GetColumnContent(2, "Note"), Is.EqualTo("出生点"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Import_RecognizesNewRedundantColumn()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                var path = Path.Combine(directory, "bonus.xlsx");
                var workbook = BuildDefaultWorkbook();
                workbook.Columns.Add(new TestColumn
                {
                    DisplayName = "额外奖励",
                    TypeToken = "int",
                    LogicalName = "Bonus",
                    IsInt = true
                });
                workbook.DataRows[0].Add("7");
                workbook.DataRows[1].Add("1");
                workbook.DataRows[2].Add("0");
                workbook.DataRows[3].Add("0");
                workbook.DataRows[4].Add("0");
                workbook.DataRows[5].Add("0");
                CreateWorkbook(path, workbook);

                var import = GvgMapExcelImporter.Import(path, asset, map);
                Assert.That(import.IsValid, Is.True, string.Join(" | ", import.Errors));

                var redundancy = asset.ExcelRedundancy;
                Assert.That(redundancy, Is.Not.Null);
                Assert.That(redundancy.GetColumnKind("Bonus"), Is.EqualTo(GvgExcelColumnKind.Int));
                Assert.That(redundancy.GetColumnContent(12000, "Bonus"), Is.EqualTo("7"));
                Assert.That(redundancy.GetColumnContent(2, "Bonus"), Is.EqualTo("1"));

                // The new redundant column must be written back verbatim on export.
                var exportPath = GvgMapExcelExporter.ExportExcel(asset, map, directory);
                var cells = ReadWorkbookCells(exportPath);
                Assert.That(cells[3][9], Is.EqualTo("Bonus"));
                Assert.That(cells[6][9], Is.EqualTo("7"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Import_MissingLogicalColumn_ReportsMissingColumn()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                var path = Path.Combine(directory, "missing.xlsx");
                var workbook = BuildDefaultWorkbook();
                workbook.Columns.RemoveAt(0);
                for (var row = 0; row < workbook.DataRows.Count; row++)
                {
                    workbook.DataRows[row].RemoveAt(0);
                }

                CreateWorkbook(path, workbook);
                var result = GvgMapExcelImporter.Import(path, asset, map);

                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Applied, Is.False);
                Assert.That(result.Errors.Any(error => error.Contains("缺少列：ID")), Is.True,
                    string.Join(" | ", result.Errors));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Import_DuplicateLogicalColumn_ReportsError()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                var path = Path.Combine(directory, "duplicate.xlsx");
                var workbook = BuildDefaultWorkbook();
                workbook.Columns.Add(new TestColumn
                {
                    DisplayName = "ID副本",
                    TypeToken = "int",
                    LogicalName = "ID",
                    IsInt = true
                });
                for (var row = 0; row < workbook.DataRows.Count; row++)
                {
                    workbook.DataRows[row].Add("0");
                }

                CreateWorkbook(path, workbook);
                var result = GvgMapExcelImporter.Import(path, asset, map);

                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Applied, Is.False);
                Assert.That(result.Errors.Any(error => error.Contains("Duplicate column name: ID.")), Is.True,
                    string.Join(" | ", result.Errors));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Export_RecomputesTypeAndSafe()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                asset.MapId = "TYPESAFE";
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(11000, new[] { 0, 1 }, PlotType.Camp, 0, -1),
                    new GvgPlotAuthoringData(12000, new[] { 2, 3 }, PlotType.Normal, 0, -1, 11000),
                    new GvgPlotAuthoringData(4, new[] { 4 }, PlotType.Normal, 0, 100),
                    new GvgPlotAuthoringData(400, new[] { 4 }, PlotType.Normal, 100, -1),
                    new GvgPlotAuthoringData(5, new[] { 5 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(6, new[] { 6 }, PlotType.Normal, 0, -1)
                });
                GvgMapAuthoringUtility.NormalizePlotIds(asset, map);

                var exportPath = GvgMapExcelExporter.ExportExcel(asset, map, directory);
                var cells = ReadWorkbookCells(exportPath);
                var dataRows = cells.Where(pair => pair.Key >= 6).ToDictionary(pair => pair.Key, pair => pair.Value);

                // Canonical export uses column order ID, Coordinates, Type, GridType, Safe, Start, End.
                Assert.That(GetDataRow(dataRows, "400")[2], Is.EqualTo("1"), "Type must be 1 when Start != 0.");
                Assert.That(GetDataRow(dataRows, "4")[2], Is.EqualTo("0"), "Type must be 0 when Start == 0.");
                Assert.That(GetDataRow(dataRows, "12000")[2], Is.EqualTo("0"));
                Assert.That(GetDataRow(dataRows, "12000")[4], Is.EqualTo("11000"), "Safe must write AffiliatedCampId.");
                Assert.That(GetDataRow(dataRows, "11000")[4], Is.EqualTo("-1"), "Unaffiliated Camp writes Safe -1.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Export_SortsMultiCellBeforeSingleCell()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                asset.MapId = "SORT";
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(12000, new[] { 0, 1 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(2, new[] { 2 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(3, new[] { 3 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(4, new[] { 4 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(5, new[] { 5 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(6, new[] { 6 }, PlotType.Normal, 0, -1)
                });
                GvgMapAuthoringUtility.NormalizePlotIds(asset, map);

                var exportPath = GvgMapExcelExporter.ExportExcel(asset, map, directory);
                var cells = ReadWorkbookCells(exportPath);
                var ids = cells.Where(pair => pair.Key >= 6)
                    .OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value[0])
                    .ToList();

                Assert.That(ids, Is.EqualTo(new List<string> { "12000", "2", "3", "4", "5", "6" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Export_SortsByPlotTypeWithNormalLast()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                asset.MapId = "SORTTYPE";
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(11000, new[] { 0, 1 }, PlotType.Camp, 0, -1),
                    new GvgPlotAuthoringData(2, new[] { 2 }, PlotType.Camp, 0, -1),
                    new GvgPlotAuthoringData(3, new[] { 3 }, PlotType.Grass, 0, -1),
                    new GvgPlotAuthoringData(4, new[] { 4 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(5, new[] { 5 }, PlotType.SmallCity, 0, -1),
                    new GvgPlotAuthoringData(6, new[] { 6 }, PlotType.Normal, 0, -1)
                });
                GvgMapAuthoringUtility.NormalizePlotIds(asset, map);

                var exportPath = GvgMapExcelExporter.ExportExcel(asset, map, directory);
                var cells = ReadWorkbookCells(exportPath);
                var ids = cells.Where(pair => pair.Key >= 6)
                    .OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value[0])
                    .ToList();

                // Multi-cell plot first, then single cells by PlotType with Normal last.
                Assert.That(ids, Is.EqualTo(new List<string> { "11000", "2", "3", "5", "4", "6" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void EditorDeletePlot_AddsDefaultRedundancyRows()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                var sourcePath = Path.Combine(directory, "source.xlsx");
                CreateWorkbook(sourcePath, BuildDefaultWorkbook());
                var import = GvgMapExcelImporter.Import(sourcePath, asset, map);
                Assert.That(import.IsValid, Is.True, string.Join(" | ", import.Errors));

                Assert.That(GvgMapAuthoringUtility.TryDeletePlot(asset, map, 12000), Is.True);

                var redundancy = asset.ExcelRedundancy;
                Assert.That(redundancy, Is.Not.Null);
                Assert.That(redundancy.FindRow(12000), Is.Null, "Deleted Plot's redundancy row must be dropped.");
                Assert.That(redundancy.FindRow(0), Is.Not.Null, "New default Plot must get a redundancy row.");
                Assert.That(redundancy.FindRow(1), Is.Not.Null);
                Assert.That(redundancy.GetColumnContent(0, "Coin"), Is.EqualTo("0"), "Int default must be 0.");
                Assert.That(redundancy.GetColumnContent(0, "Note"), Is.EqualTo(string.Empty), "String default must be empty.");
                Assert.That(asset.Plots.Count, Is.EqualTo(7));

                var exportPath = GvgMapExcelExporter.ExportExcel(asset, map, directory);
                var cells = ReadWorkbookCells(exportPath);
                var ids = cells.Where(pair => pair.Key >= 6).Select(pair => pair.Value[0]).ToList();
                Assert.That(ids, Is.Not.Contains("12000"));
                Assert.That(ids.Count, Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void NormalizePlotIds_ReKeysRedundancyRows()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(12000, new[] { 0 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(1, new[] { 1 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(2, new[] { 2 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(3, new[] { 3 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(4, new[] { 4 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(5, new[] { 5 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(6, new[] { 6 }, PlotType.Normal, 0, -1)
                });

                var redundancy = new GvgExcelDocumentRedundancy();
                redundancy.ReplaceColumns(new[]
                {
                    new GvgExcelColumnInfo("Coin", GvgExcelColumnKind.Int),
                    new GvgExcelColumnInfo("Note", GvgExcelColumnKind.String)
                });
                var sourceRow = new GvgExcelRowData { PlotId = 12000 };
                sourceRow.ReplaceColumns(new[]
                {
                    new GvgExcelColumnContent("Coin", "100"),
                    new GvgExcelColumnContent("Note", "大营")
                });
                redundancy.AddRow(sourceRow);
                asset.ReplaceExcelRedundancy(redundancy);

                var mapping = GvgMapAuthoringUtility.NormalizePlotIds(asset, map);
                var reconciled = asset.ExcelRedundancy;

                Assert.That(mapping[12000], Is.EqualTo(0));
                Assert.That(reconciled.FindRow(12000), Is.Null);
                Assert.That(reconciled.FindRow(0), Is.Not.Null);
                Assert.That(reconciled.GetColumnContent(0, "Coin"), Is.EqualTo("100"));
                Assert.That(reconciled.GetColumnContent(0, "Note"), Is.EqualTo("大营"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Import_RejectsOutOfRadiusPlot()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                var path = Path.Combine(directory, "outofradius.xlsx");
                var workbook = BuildDefaultWorkbook();
                workbook.DataRows[0][1] = "[0,100]";
                CreateWorkbook(path, workbook);

                var result = GvgMapExcelImporter.Import(path, asset, map);

                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Applied, Is.False);
                Assert.That(result.Errors.Any(error => error.Contains("outside radius")), Is.True,
                    string.Join(" | ", result.Errors));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Export_RejectsInvalidPlotOutsideRadius()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            var directory = CreateTempDirectory();
            try
            {
                asset.MapId = "BAD";
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(12000, new[] { 0, 100 }, PlotType.Normal, 0, -1)
                });

                var exception = Assert.Throws<InvalidOperationException>(
                    () => GvgMapExcelExporter.ExportExcel(asset, map, directory));
                Assert.That(exception.Message, Does.Contain("outside radius"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                DeleteDirectory(directory);
            }
        }

        private static RuntimeHexMap CreateMap(int radius)
        {
            return new RuntimeHexMap(new HexMapDefinition(radius));
        }

        private static GvgMapAuthoringAsset CreateAsset()
        {
            return ScriptableObject.CreateInstance<GvgMapAuthoringAsset>();
        }

        private static void AssertImportedPlots(GvgMapAuthoringAsset asset)
        {
            Assert.That(asset.Plots.Count, Is.EqualTo(6));

            var multi = asset.Plots.First(plot => plot.IsMultiCell);
            Assert.That(multi.PlotId, Is.EqualTo(12000));
            Assert.That(multi.HexIds, Is.EqualTo(new List<int> { 0, 1 }));
            Assert.That(multi.PlotType, Is.EqualTo(PlotType.Normal));
            Assert.That(multi.Start, Is.EqualTo(0));
            Assert.That(multi.End, Is.EqualTo(-1));

            for (var hexId = 2; hexId <= 6; hexId++)
            {
                var plot = asset.Plots.FirstOrDefault(candidate => candidate.HexIds.Contains(hexId));
                Assert.That(plot, Is.Not.Null, "Single-cell Plot for HexId " + hexId + " is missing.");
                Assert.That(plot.PlotId, Is.EqualTo(hexId));
                Assert.That(plot.PlotType, Is.EqualTo(PlotType.Normal));
                Assert.That(plot.Start, Is.EqualTo(0));
                Assert.That(plot.End, Is.EqualTo(-1));
            }
        }

        private static void AssertImportedRedundancy(GvgMapAuthoringAsset asset)
        {
            var redundancy = asset.ExcelRedundancy;
            Assert.That(redundancy, Is.Not.Null);
            Assert.That(redundancy.Columns.Count, Is.EqualTo(9));
            Assert.That(redundancy.HeaderRows.Count, Is.EqualTo(5));

            // Column types are detected from the explicit type row.
            Assert.That(redundancy.GetColumnKind("ID"), Is.EqualTo(GvgExcelColumnKind.Int));
            Assert.That(redundancy.GetColumnKind("Coordinates"), Is.EqualTo(GvgExcelColumnKind.IntArray));
            Assert.That(redundancy.GetColumnKind("Note"), Is.EqualTo(GvgExcelColumnKind.String));
            Assert.That(redundancy.GetColumnKind("Coin"), Is.EqualTo(GvgExcelColumnKind.Int));

            // Redundant cell content is recorded verbatim.
            Assert.That(redundancy.GetColumnContent(12000, "Coin"), Is.EqualTo("100"));
            Assert.That(redundancy.GetColumnContent(12000, "Note"), Is.EqualTo("大营"));
            Assert.That(redundancy.GetColumnContent(2, "Note"), Is.EqualTo("出生点"));

            // Header text, fill/font colors, and comments are preserved.
            var logicalHeaderRow = redundancy.HeaderRows[2];
            var noteHeader = logicalHeaderRow.Cells[7];
            Assert.That(noteHeader.ColumnName, Is.EqualTo("Note"));
            Assert.That(noteHeader.Text, Is.EqualTo("Note"));
            Assert.That(noteHeader.FillColor, Is.EqualTo("FF037EAA"));
            Assert.That(noteHeader.FontColor, Is.EqualTo("FFFFFFFF"));
            Assert.That(noteHeader.Comment, Is.EqualTo("表头备注"));

            var displayHeaderRow = redundancy.HeaderRows[0];
            Assert.That(displayHeaderRow.Cells[0].Text, Is.EqualTo("编号"));
            var markerRow = redundancy.HeaderRows[4];
            Assert.That(markerRow.Cells[0].Text, Is.EqualTo("c/s"));
        }

        private static void AssertEquivalentPlots(GvgMapAuthoringAsset left, GvgMapAuthoringAsset right)
        {
            Assert.That(left.Plots.Count, Is.EqualTo(right.Plots.Count));
            var leftSorted = left.Plots.OrderBy(plot => plot.PlotId).ToList();
            var rightSorted = right.Plots.OrderBy(plot => plot.PlotId).ToList();
            for (var index = 0; index < leftSorted.Count; index++)
            {
                Assert.That(rightSorted[index].PlotId, Is.EqualTo(leftSorted[index].PlotId), "PlotId mismatch.");
                Assert.That(rightSorted[index].HexIds, Is.EqualTo(leftSorted[index].HexIds), "HexIds mismatch.");
                Assert.That(rightSorted[index].PlotType, Is.EqualTo(leftSorted[index].PlotType), "PlotType mismatch.");
                Assert.That(rightSorted[index].Start, Is.EqualTo(leftSorted[index].Start), "Start mismatch.");
                Assert.That(rightSorted[index].End, Is.EqualTo(leftSorted[index].End), "End mismatch.");
                Assert.That(rightSorted[index].AffiliatedCampId, Is.EqualTo(leftSorted[index].AffiliatedCampId), "AffiliatedCampId mismatch.");
            }
        }

        private static void AssertEquivalentRedundancy(GvgExcelDocumentRedundancy left, GvgExcelDocumentRedundancy right)
        {
            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);

            Assert.That(right.Columns.Count, Is.EqualTo(left.Columns.Count));
            for (var index = 0; index < left.Columns.Count; index++)
            {
                Assert.That(right.Columns[index].Name, Is.EqualTo(left.Columns[index].Name), "Column name mismatch.");
                Assert.That(right.Columns[index].Kind, Is.EqualTo(left.Columns[index].Kind), "Column kind mismatch.");
            }

            Assert.That(right.HeaderRows.Count, Is.EqualTo(left.HeaderRows.Count));
            for (var row = 0; row < left.HeaderRows.Count; row++)
            {
                Assert.That(right.HeaderRows[row].Cells.Count, Is.EqualTo(left.HeaderRows[row].Cells.Count));
                for (var column = 0; column < left.HeaderRows[row].Cells.Count; column++)
                {
                    var expected = left.HeaderRows[row].Cells[column];
                    var actual = right.HeaderRows[row].Cells[column];
                    Assert.That(actual.Text, Is.EqualTo(expected.Text), "Header text mismatch at row " + row + ".");
                    Assert.That(actual.FillColor, Is.EqualTo(expected.FillColor), "Header fill mismatch at row " + row + ".");
                    Assert.That(actual.FontColor, Is.EqualTo(expected.FontColor), "Header font mismatch at row " + row + ".");
                    Assert.That(actual.Comment, Is.EqualTo(expected.Comment), "Header comment mismatch at row " + row + ".");
                }
            }

            Assert.That(right.Rows.Count, Is.EqualTo(left.Rows.Count));
            for (var index = 0; index < left.Rows.Count; index++)
            {
                var expected = left.Rows[index];
                var actual = right.FindRow(expected.PlotId);
                Assert.That(actual, Is.Not.Null, "Re-imported redundancy row for PlotId " + expected.PlotId + " is missing.");
                for (var column = 0; column < expected.Columns.Count; column++)
                {
                    var expectedColumn = expected.Columns[column];
                    var actualContent = string.Empty;
                    for (var actualIndex = 0; actualIndex < actual.Columns.Count; actualIndex++)
                    {
                        if (actual.Columns[actualIndex].ColumnName == expectedColumn.ColumnName)
                        {
                            actualContent = actual.Columns[actualIndex].Content;
                            break;
                        }
                    }

                    Assert.That(actualContent, Is.EqualTo(expectedColumn.Content),
                        "Redundant content mismatch for PlotId " + expected.PlotId + " column " + expectedColumn.ColumnName + ".");
                }
            }
        }

        private static Dictionary<int, string> GetDataRow(
            Dictionary<int, Dictionary<int, string>> dataRows,
            string plotId)
        {
            var row = dataRows.Values.FirstOrDefault(values => values.ContainsKey(0) && values[0] == plotId);
            Assert.That(row, Is.Not.Null, "Data row for PlotId " + plotId + " not found.");
            return row;
        }

        private static TestWorkbook BuildDefaultWorkbook()
        {
            var workbook = new TestWorkbook();
            AddColumn(workbook, "编号", "int", "ID", true);
            AddColumn(workbook, "坐标", "int[]", "Coordinates", false);
            AddColumn(workbook, "类型", "int", "Type", true);
            AddColumn(workbook, "格子类型", "int", "GridType", true);
            AddColumn(workbook, "归属", "int", "Safe", true);
            AddColumn(workbook, "开始", "int", "Start", true);
            AddColumn(workbook, "结束", "int", "End", true);
            AddColumn(workbook, "备注", "Note", "Note", false, "表头备注");
            AddColumn(workbook, "金币", "int", "Coin", true);

            workbook.DataRows.Add(Row("12000", "[0,1]", "0", "2", "-1", "0", "-1", "大营", "100"));
            workbook.DataRows.Add(Row("2", "[2]", "0", "2", "-1", "0", "-1", "出生点", "0"));
            workbook.DataRows.Add(Row("3", "[3]", "0", "2", "-1", "0", "-1", "", "0"));
            workbook.DataRows.Add(Row("4", "[4]", "0", "2", "-1", "0", "-1", "", "0"));
            workbook.DataRows.Add(Row("5", "[5]", "0", "2", "-1", "0", "-1", "", "0"));
            workbook.DataRows.Add(Row("6", "[6]", "0", "2", "-1", "0", "-1", "", "0"));
            return workbook;
        }

        private static TestWorkbook BuildReorderedWorkbook()
        {
            var workbook = new TestWorkbook();
            AddColumn(workbook, "金币", "int", "Coin", true);
            AddColumn(workbook, "归属", "int", "Safe", true);
            AddColumn(workbook, "坐标", "int[]", "Coordinates", false);
            AddColumn(workbook, "备注", "Note", "Note", false);
            AddColumn(workbook, "编号", "int", "ID", true);
            AddColumn(workbook, "开始", "int", "Start", true);
            AddColumn(workbook, "格子类型", "int", "GridType", true);
            AddColumn(workbook, "类型", "int", "Type", true);
            AddColumn(workbook, "结束", "int", "End", true);

            workbook.DataRows.Add(Row("100", "-1", "[0,1]", "大营", "12000", "0", "2", "0", "-1"));
            workbook.DataRows.Add(Row("0", "-1", "[2]", "出生点", "2", "0", "2", "0", "-1"));
            workbook.DataRows.Add(Row("0", "-1", "[3]", "", "3", "0", "2", "0", "-1"));
            workbook.DataRows.Add(Row("0", "-1", "[4]", "", "4", "0", "2", "0", "-1"));
            workbook.DataRows.Add(Row("0", "-1", "[5]", "", "5", "0", "2", "0", "-1"));
            workbook.DataRows.Add(Row("0", "-1", "[6]", "", "6", "0", "2", "0", "-1"));
            return workbook;
        }

        private static List<string> Row(params string[] values)
        {
            return new List<string>(values);
        }

        private static void AddColumn(
            TestWorkbook workbook,
            string displayName,
            string typeToken,
            string logicalName,
            bool isInt,
            string comment = "")
        {
            workbook.Columns.Add(new TestColumn
            {
                DisplayName = displayName,
                TypeToken = typeToken,
                LogicalName = logicalName,
                IsInt = isInt,
                Comment = comment
            });
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "GvgMapExcelRoundTripTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static void CreateWorkbook(string path, TestWorkbook workbook)
        {
            var contentTypes = workbook.BuildContentTypes();
            var rootRelationships = workbook.BuildRootRelationships();
            var workbookXml = workbook.BuildWorkbook();
            var workbookRelationships = workbook.BuildWorkbookRelationships();
            var styles = workbook.BuildStyles();
            var sheet = workbook.BuildSheet();
            var sharedStrings = workbook.BuildSharedStrings();
            var comments = workbook.HasComments ? workbook.BuildComments() : null;

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml", contentTypes);
                WriteEntry(archive, "_rels/.rels", rootRelationships);
                WriteEntry(archive, "xl/workbook.xml", workbookXml);
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", workbookRelationships);
                WriteEntry(archive, "xl/styles.xml", styles);
                WriteEntry(archive, "xl/sharedStrings.xml", sharedStrings);
                WriteEntry(archive, "xl/worksheets/sheet1.xml", sheet);
                if (comments != null)
                {
                    WriteEntry(archive, "xl/comments1.xml", comments);
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

        private static Dictionary<int, Dictionary<int, string>> ReadWorkbookCells(string path)
        {
            var sharedStrings = ReadSharedStrings(path);
            var result = new Dictionary<int, Dictionary<int, string>>();
            using (var archive = ZipFile.OpenRead(path))
            {
                var entry = archive.GetEntry("xl/worksheets/sheet1.xml");
                Assert.That(entry, Is.Not.Null, "Workbook is missing xl/worksheets/sheet1.xml.");
                var document = new XmlDocument();
                using (var stream = entry.Open())
                {
                    document.Load(stream);
                }

                var rowNodes = document.GetElementsByTagName("row");
                for (var index = 0; index < rowNodes.Count; index++)
                {
                    var row = (XmlElement)rowNodes[index];
                    int rowNumber;
                    if (!int.TryParse(row.GetAttribute("r"), NumberStyles.Integer, CultureInfo.InvariantCulture, out rowNumber))
                    {
                        continue;
                    }

                    var cells = new Dictionary<int, string>();
                    var cellNodes = row.GetElementsByTagName("c");
                    for (var cellIndex = 0; cellIndex < cellNodes.Count; cellIndex++)
                    {
                        var cell = (XmlElement)cellNodes[cellIndex];
                        var columnIndex = ColumnIndexFromReference(cell.GetAttribute("r"));
                        if (columnIndex < 0) continue;

                        var valueNodes = cell.GetElementsByTagName("v");
                        var value = valueNodes.Count > 0 ? valueNodes[0].InnerText : string.Empty;
                        if (cell.GetAttribute("t") == "s" && value.Length > 0)
                        {
                            int sharedIndex;
                            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sharedIndex) &&
                                sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                            {
                                value = sharedStrings[sharedIndex];
                            }
                            else
                            {
                                value = string.Empty;
                            }
                        }

                        cells[columnIndex] = value;
                    }

                    result[rowNumber] = cells;
                }
            }

            return result;
        }

        private static List<string> ReadSharedStrings(string path)
        {
            var result = new List<string>();
            using (var archive = ZipFile.OpenRead(path))
            {
                var entry = archive.GetEntry("xl/sharedStrings.xml");
                if (entry == null) return result;
                var document = new XmlDocument();
                using (var stream = entry.Open())
                {
                    document.Load(stream);
                }

                var nodes = document.GetElementsByTagName("si");
                for (var index = 0; index < nodes.Count; index++)
                {
                    result.Add(nodes[index].InnerText);
                }
            }

            return result;
        }

        private static int ColumnIndexFromReference(string reference)
        {
            var letters = new StringBuilder();
            for (var index = 0; index < reference.Length && char.IsLetter(reference[index]); index++)
            {
                letters.Append(reference[index]);
            }

            if (letters.Length == 0) return -1;
            var value = 0;
            for (var index = 0; index < letters.Length; index++)
            {
                value = value * 26 + (letters[index] - 'A' + 1);
            }

            return value - 1;
        }

        private static string CellReference(int rowNumber, int columnIndex)
        {
            var letters = string.Empty;
            var value = columnIndex + 1;
            while (value > 0)
            {
                var remainder = (value - 1) % 26;
                letters = (char)('A' + remainder) + letters;
                value = (value - 1) / 26;
            }

            return letters + rowNumber.ToString(CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return value.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        /// <summary>
        /// Minimal xlsx workbook builder used only to feed the importer in tests.
        /// </summary>
        private sealed class TestWorkbook
        {
            public List<TestColumn> Columns { get; private set; }
            public List<List<string>> DataRows { get; private set; }

            private readonly List<string> m_SharedStrings = new List<string>();
            private readonly Dictionary<string, int> m_SharedIndex = new Dictionary<string, int>();

            public TestWorkbook()
            {
                Columns = new List<TestColumn>();
                DataRows = new List<List<string>>();
            }

            public bool HasComments
            {
                get
                {
                    for (var index = 0; index < Columns.Count; index++)
                    {
                        if (Columns[index].Comment.Length > 0) return true;
                    }

                    return false;
                }
            }

            public string BuildContentTypes()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
                builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
                builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
                builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
                builder.Append("<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
                builder.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
                if (HasComments)
                {
                    builder.Append("<Override PartName=\"/xl/comments1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.comments+xml\"/>");
                }

                builder.Append("</Types>");
                return builder.ToString();
            }

            public string BuildRootRelationships()
            {
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>";
            }

            public string BuildWorkbook()
            {
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"" + MainNamespace + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"GVGMap\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                    "</workbook>";
            }

            public string BuildWorkbookRelationships()
            {
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>" +
                    "</Relationships>";
            }

            public string BuildStyles()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<styleSheet xmlns=\"").Append(MainNamespace).Append("\">");
                builder.Append("<fonts count=\"2\">");
                builder.Append("<font><sz val=\"11\"/><name val=\"Calibri\"/></font>");
                builder.Append("<font><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>");
                builder.Append("</fonts>");
                builder.Append("<fills count=\"3\">");
                builder.Append("<fill><patternFill patternType=\"none\"/></fill>");
                builder.Append("<fill><patternFill patternType=\"gray125\"/></fill>");
                builder.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF037EAA\"/><bgColor indexed=\"64\"/></patternFill></fill>");
                builder.Append("</fills>");
                builder.Append("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>");
                builder.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
                builder.Append("<cellXfs count=\"2\">");
                builder.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");
                builder.Append("<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>");
                builder.Append("</cellXfs>");
                builder.Append("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
                builder.Append("</styleSheet>");
                return builder.ToString();
            }

            public string BuildSheet()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<worksheet xmlns=\"").Append(MainNamespace).Append("\"><sheetData>");

                for (var row = 1; row <= 5; row++)
                {
                    builder.Append("<row r=\"").Append(row).Append("\">");
                    if (row == 4)
                    {
                        builder.Append("</row>");
                        continue;
                    }

                    for (var column = 0; column < Columns.Count; column++)
                    {
                        var columnSpec = Columns[column];
                        string text;
                        switch (row)
                        {
                            case 1: text = columnSpec.DisplayName; break;
                            case 2: text = columnSpec.TypeToken; break;
                            case 3: text = columnSpec.LogicalName; break;
                            default: text = "c/s"; break;
                        }

                        var styleIndex = row == 3 ? 1 : 0;
                        AppendTextCell(builder, row, column, text, styleIndex);
                    }

                    builder.Append("</row>");
                }

                for (var row = 0; row < DataRows.Count; row++)
                {
                    var rowNumber = row + 6;
                    builder.Append("<row r=\"").Append(rowNumber).Append("\">");
                    var values = DataRows[row];
                    for (var column = 0; column < Columns.Count; column++)
                    {
                        var value = column < values.Count ? values[column] : string.Empty;
                        if (Columns[column].IsInt)
                        {
                            if (value.Length == 0) value = "0";
                            builder.Append("<c r=\"").Append(CellReference(rowNumber, column))
                                .Append("\"><v>").Append(Escape(value)).Append("</v></c>");
                        }
                        else if (value.Length > 0)
                        {
                            var sharedIndex = AddSharedString(value);
                            builder.Append("<c r=\"").Append(CellReference(rowNumber, column))
                                .Append("\" t=\"s\"><v>").Append(sharedIndex).Append("</v></c>");
                        }
                    }

                    builder.Append("</row>");
                }

                builder.Append("</sheetData></worksheet>");
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
                    builder.Append("<si><t>").Append(Escape(m_SharedStrings[index])).Append("</t></si>");
                }

                builder.Append("</sst>");
                return builder.ToString();
            }

            public string BuildComments()
            {
                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                builder.Append("<comments xmlns=\"").Append(MainNamespace).Append("\"><authors><author>Test</author></authors><commentList>");
                for (var column = 0; column < Columns.Count; column++)
                {
                    var comment = Columns[column].Comment;
                    if (comment.Length == 0) continue;
                    builder.Append("<comment ref=\"").Append(CellReference(3, column)).Append("\" authorId=\"0\">")
                        .Append("<text><t xml:space=\"preserve\">").Append(Escape(comment)).Append("</t></text></comment>");
                }

                builder.Append("</commentList></comments>");
                return builder.ToString();
            }

            private void AppendTextCell(StringBuilder builder, int row, int column, string text, int styleIndex)
            {
                builder.Append("<c r=\"").Append(CellReference(row, column)).Append("\" s=\"").Append(styleIndex).Append("\"");
                if (text.Length > 0)
                {
                    builder.Append(" t=\"s\"><v>").Append(AddSharedString(text)).Append("</v></c>");
                }
                else
                {
                    builder.Append("/>");
                }
            }

            private int AddSharedString(string value)
            {
                int index;
                if (m_SharedIndex.TryGetValue(value, out index)) return index;
                index = m_SharedStrings.Count;
                m_SharedStrings.Add(value);
                m_SharedIndex.Add(value, index);
                return index;
            }
        }

        private sealed class TestColumn
        {
            public string DisplayName { get; set; } = string.Empty;
            public string TypeToken { get; set; } = string.Empty;
            public string LogicalName { get; set; } = string.Empty;
            public bool IsInt { get; set; }
            public string Comment { get; set; } = string.Empty;
        }
    }
}

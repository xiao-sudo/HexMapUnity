using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using HexMap.Gvg.Authoring;
using UnityEngine;

namespace HexMap.Gvg.Tests
{
    [TestFixture]
    public sealed class GvgMapAuthoringTests
    {
        [Test]
        public void DefaultGenerationCreatesOneSingleCellPlotForEveryHex()
        {
            var asset = CreateAsset(1);
            try
            {
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset);

                Assert.That(asset.Plots.Count, Is.EqualTo(7));
                foreach (var plot in asset.Plots)
                {
                    Assert.That(plot.HexIds.Count, Is.EqualTo(1));
                    Assert.That(plot.PlotId, Is.EqualTo(plot.HexIds[0]));
                    Assert.That(plot.PlotType, Is.EqualTo(PlotType.Normal));
                }
                Assert.That(GvgMapAuthoringUtility.Validate(asset).IsValid, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ValidationRejectsAuthoringCoverageAndPlotIdRuleErrors()
        {
            var asset = CreateAsset(1);
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(4, new[] { 0 }, PlotType.Normal),
                    new GvgPlotAuthoringData(9, new[] { 1, 2 }, PlotType.Normal),
                    new GvgPlotAuthoringData(10, new[] { 2 }, PlotType.Normal),
                    new GvgPlotAuthoringData(11, new[] { 100 }, PlotType.Normal)
                });

                var validation = GvgMapAuthoringUtility.Validate(asset);

                Assert.That(validation.IsValid, Is.False);
                Assert.That(ContainsIssue(validation, "Single-cell Plot must use its HexId"), Is.True);
                Assert.That(ContainsIssue(validation, "Multi-cell Plot must use a negative PlotId"), Is.True);
                Assert.That(ContainsIssue(validation, "belongs to multiple Plots"), Is.True);
                Assert.That(ContainsIssue(validation, "outside radius"), Is.True);
                Assert.That(ContainsIssue(validation, "unassigned HexIds"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void EditingOperationsMaintainFullCoverageAndUniqueOwnership()
        {
            var asset = CreateAsset(1);
            try
            {
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset);

                Assert.That(GvgMapAuthoringUtility.TryMergeToMultiPlot(asset, 0, new[] { 1 }), Is.True);
                var multiPlot = FindPlot(asset, -1);
                Assert.That(multiPlot.HexIds, Is.EquivalentTo(new[] { 0, 1 }));

                Assert.That(GvgMapAuthoringUtility.TryPaintAdd(asset, -1, 2), Is.True);
                multiPlot = FindPlot(asset, -1);
                Assert.That(multiPlot.HexIds, Is.EquivalentTo(new[] { 0, 1, 2 }));
                Assert.That(asset.Plots.Any(plot => plot.PlotId == 2), Is.False);

                Assert.That(GvgMapAuthoringUtility.TryPaintRemove(asset, -1, 2), Is.True);
                multiPlot = FindPlot(asset, -1);
                Assert.That(multiPlot.HexIds, Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(FindPlot(asset, 2).HexIds, Is.EqualTo(new[] { 2 }));

                Assert.That(GvgMapAuthoringUtility.TryDeletePlot(asset, -1), Is.True);
                Assert.That(asset.Plots.Count, Is.EqualTo(7));
                Assert.That(GvgMapAuthoringUtility.Validate(asset).IsValid, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RebuildRadiusExpandsAndShrinksCoverage()
        {
            var asset = CreateAsset(1);
            try
            {
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset);
                GvgMapAuthoringUtility.TryMergeToMultiPlot(asset, 0, new[] { 6 });

                GvgMapAuthoringUtility.RebuildRadius(asset, 2);

                Assert.That(asset.Radius, Is.EqualTo(2));
                Assert.That(asset.Plots.SelectMany(plot => plot.HexIds).Distinct().Count(), Is.EqualTo(19));
                Assert.That(FindPlot(asset, -1).HexIds, Is.EquivalentTo(new[] { 0, 6 }));

                GvgMapAuthoringUtility.RebuildRadius(asset, 0);

                Assert.That(asset.Radius, Is.EqualTo(0));
                Assert.That(asset.Plots.Count, Is.EqualTo(1));
                Assert.That(asset.Plots[0].PlotId, Is.EqualTo(0));
                Assert.That(asset.Plots[0].HexIds, Is.EqualTo(new[] { 0 }));
                Assert.That(GvgMapAuthoringUtility.Validate(asset).IsValid, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CsvOutputSortsRowsAndQuotesHexIdArrays()
        {
            var asset = CreateAsset(1);
            try
            {
                asset.MapId = "Map,One";
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset);
                GvgMapAuthoringUtility.TryMergeToMultiPlot(asset, 0, new[] { 6 });
                FindPlot(asset, -1).PlotType = PlotType.BigCity;

                var mapCsv = GvgMapAuthoringCsv.CreateMapCsv(asset);
                var plotsCsv = GvgMapAuthoringCsv.CreatePlotsCsv(asset);
                var cellsCsv = GvgMapAuthoringCsv.CreateCellsCsv(asset);

                Assert.That(mapCsv, Does.StartWith("MapId,Radius,Orientation,Plane,OuterRadius"));
                Assert.That(mapCsv, Does.Contain("\"Map,One\",1,0,1,1"));
                Assert.That(plotsCsv.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[1], Is.EqualTo("-1,\"[0,6]\",4"));
                Assert.That(cellsCsv.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[1], Is.EqualTo("0,0,0,-1"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ExportWriterWritesUtf8BomAndReadmeDocumentsEnumsAndDefaults()
        {
            var asset = CreateAsset(0);
            var directory = Path.Combine(Path.GetTempPath(), "GvgMapAuthoringTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset);

                GvgMapAuthoringExportWriter.WriteFiles(directory, GvgMapAuthoringCsv.CreateFiles(asset));

                var mapBytes = File.ReadAllBytes(Path.Combine(directory, GvgMapAuthoringCsv.MapFileName));
                Assert.That(mapBytes[0], Is.EqualTo(0xEF));
                Assert.That(mapBytes[1], Is.EqualTo(0xBB));
                Assert.That(mapBytes[2], Is.EqualTo(0xBF));

                var readme = File.ReadAllText(Path.Combine(directory, GvgMapAuthoringCsv.ReadmeFileName));
                Assert.That(readme, Does.Contain("0 = Camp"));
                Assert.That(readme, Does.Contain("PlotState = Open"));
                Assert.That(readme, Does.Contain("CSV import is not implemented"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RuntimeProjectionAppliesDefaultRulesAndTypeOverrides()
        {
            var asset = CreateAsset(1);
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(-1, new[] { 0, 1 }, PlotType.Obstacle),
                    new GvgPlotAuthoringData(2, new[] { 2 }, PlotType.Camp),
                    new GvgPlotAuthoringData(3, new[] { 3 }, PlotType.Normal),
                    new GvgPlotAuthoringData(4, new[] { 4 }, PlotType.Normal),
                    new GvgPlotAuthoringData(5, new[] { 5 }, PlotType.Normal),
                    new GvgPlotAuthoringData(6, new[] { 6 }, PlotType.Normal)
                });

                var runtimePlots = GvgMapAuthoringUtility.CreateRuntimePlots(asset);
                var obstacle = runtimePlots.First(plot => plot.PlotId == -1);
                var camp = runtimePlots.First(plot => plot.PlotId == 2);
                var normal = runtimePlots.First(plot => plot.PlotId == 3);

                Assert.That(obstacle.BlockingState, Is.EqualTo(BlockingState.Blocked));
                Assert.That(obstacle.OwnerFaction, Is.EqualTo(FactionId.Neutral));
                Assert.That(obstacle.PlotState, Is.EqualTo(PlotState.Open));
                Assert.That(camp.OwnershipMode, Is.EqualTo(OwnershipMode.Fixed));
                Assert.That(normal.OwnershipMode, Is.EqualTo(OwnershipMode.Capturable));
                Assert.That(normal.BlockingState, Is.EqualTo(BlockingState.Passable));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static GvgMapAuthoringAsset CreateAsset(int radius)
        {
            var asset = ScriptableObject.CreateInstance<GvgMapAuthoringAsset>();
            asset.Radius = radius;
            asset.OuterRadius = 1f;
            return asset;
        }

        private static GvgPlotAuthoringData FindPlot(GvgMapAuthoringAsset asset, int plotId)
        {
            var plot = asset.Plots.FirstOrDefault(candidate => candidate.PlotId == plotId);
            Assert.That(plot, Is.Not.Null);
            return plot;
        }

        private static bool ContainsIssue(GvgMapAuthoringValidationResult validation, string text)
        {
            return validation.Issues.Any(issue => issue.Message.Contains(text));
        }
    }
}
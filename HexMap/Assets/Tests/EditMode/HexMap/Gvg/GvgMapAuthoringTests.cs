using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using HexMap.Gvg.Authoring;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEngine;

namespace HexMap.Gvg.Tests
{
    [TestFixture]
    public sealed class GvgMapAuthoringTests
    {
        [Test]
        public void DefaultGenerationCreatesOneOpenSingleCellLayerForEveryHex()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset, map);

                Assert.That(asset.Plots.Count, Is.EqualTo(7));
                foreach (var plot in asset.Plots)
                {
                    Assert.That(plot.HexIds.Count, Is.EqualTo(1));
                    Assert.That(plot.PlotId, Is.EqualTo(plot.HexIds[0]));
                    Assert.That(plot.PlotType, Is.EqualTo(PlotType.Normal));
                    Assert.That(plot.Start, Is.EqualTo(0));
                    Assert.That(plot.End, Is.EqualTo(-1));
                }

                Assert.That(GvgMapAuthoringUtility.Validate(asset, map).IsValid, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void SecondaryScaleCanBeConfiguredAndRejectsInvalidValues()
        {
            var map = CreateMap(0);
            var asset = CreateAsset();
            try
            {
                Assert.That(map.Count, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ValidationAllowsMultipleContiguousLayersForOneHex()
        {
            var map = CreateMap(0);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(0, new[] { 0 }, PlotType.Normal, 100, 200),
                    new GvgPlotAuthoringData(400, new[] { 0 }, PlotType.Normal, 200, -1)
                });

                var validation = GvgMapAuthoringUtility.Validate(asset, map);

                Assert.That(validation.IsValid, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ValidationRejectsTimeGapsAndMixedTypesForOneHex()
        {
            var map = CreateMap(0);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(0, new[] { 0 }, PlotType.Obstacle, 0, 100),
                    new GvgPlotAuthoringData(400, new[] { 0 }, PlotType.Normal, 200, -1)
                });

                var validation = GvgMapAuthoringUtility.Validate(asset, map);

                Assert.That(validation.IsValid, Is.False);
                Assert.That(ContainsIssue(validation, "contiguous"), Is.True);
                Assert.That(ContainsIssue(validation, "multiple single-cell PlotTypes"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NormalizePlotIdsUsesHexIdForFirstLayerAndTypedRangeForMultiPlot()
        {
            var map = CreateMap(11);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(12, new[] { 0 }, PlotType.Normal, 0, 100),
                    new GvgPlotAuthoringData(13, new[] { 0 }, PlotType.Normal, 100, -1),
                    new GvgPlotAuthoringData(14, new[] { 1, 2 }, PlotType.SmallCity, 0, -1),
                    new GvgPlotAuthoringData(15, new[] { 3, 4 }, PlotType.Camp, 0, -1),
                    new GvgPlotAuthoringData(16, new[] { 5, 6 }, PlotType.Normal, 0, -1)
                });

                GvgMapAuthoringUtility.NormalizePlotIds(asset, map);

                var layers = asset.Plots.Where(plot => plot.HexIds.Count == 1 && plot.HexIds[0] == 0)
                    .OrderBy(plot => plot.Start)
                    .ToList();
                Assert.That(layers[0].PlotId, Is.EqualTo(0));
                Assert.That(layers[1].PlotId, Is.GreaterThanOrEqualTo(400));
                Assert.That(asset.Plots.Where(plot => plot.IsMultiCell).Select(plot => plot.PlotId),
                    Is.EquivalentTo(new[] { 14000, 11000, 12000 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NormalizePlotIdsReassignsImportedMultiIdsThatCollideWithHexIds()
        {
            var map = CreateMap(11);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(11, new[] { 102, 139 }, PlotType.SmallCity, 0, -1),
                    new GvgPlotAuthoringData(35, new[] { 7, 8 }, PlotType.Obstacle, 0, -1),
                    new GvgPlotAuthoringData(36, new[] { 12, 14 }, PlotType.Obstacle, 0, -1),
                    new GvgPlotAuthoringData(37, new[] { 13, 15 }, PlotType.Obstacle, 0, -1),
                    new GvgPlotAuthoringData(298, new[] { 11 }, PlotType.Normal, 0, -1)
                });

                GvgMapAuthoringUtility.NormalizePlotIds(asset, map);

                Assert.That(asset.Plots.Single(plot => plot.HexIds.Count == 1).PlotId, Is.EqualTo(11));
                Assert.That(asset.Plots.Where(plot => plot.IsMultiCell).Select(plot => plot.PlotId),
                    Is.EquivalentTo(new[] { 14000, 17000, 17001, 17002 }));
                Assert.That(asset.Plots.Select(plot => plot.PlotId).Distinct().Count(),
                    Is.EqualTo(asset.Plots.Count));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
        public void EditingOperationsMaintainCoverageAndUseTypedMultiPlotIds()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset, map);

                Assert.That(GvgMapAuthoringUtility.TryMergeToMultiPlot(asset, map, 0, new[] { 1 }), Is.True);
                var multiPlot = FindMultiPlot(asset);
                Assert.That(multiPlot.HexIds, Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(multiPlot.PlotId, Is.EqualTo(12000));

                Assert.That(GvgMapAuthoringUtility.TryPaintAdd(asset, map, multiPlot.PlotId, 2), Is.True);
                multiPlot = FindMultiPlot(asset);
                Assert.That(multiPlot.HexIds, Is.EquivalentTo(new[] { 0, 1, 2 }));
                Assert.That(asset.Plots.Any(plot => plot.PlotId == 2), Is.False);

                Assert.That(GvgMapAuthoringUtility.TryPaintRemove(asset, map, multiPlot.PlotId, 2), Is.True);
                multiPlot = FindMultiPlot(asset);
                Assert.That(multiPlot.HexIds, Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(FindPlot(asset, 2).HexIds, Is.EqualTo(new[] { 2 }));

                Assert.That(GvgMapAuthoringUtility.TryDeletePlot(asset, map, multiPlot.PlotId), Is.True);
                Assert.That(asset.Plots.Count, Is.EqualTo(7));
                Assert.That(GvgMapAuthoringUtility.Validate(asset, map).IsValid, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MergingAnotherPlotDoesNotRenumberExistingMultiCellPlot()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset, map);
                Assert.That(GvgMapAuthoringUtility.TryMergeToMultiPlot(asset, map, 5, new[] { 6 }), Is.True);
                var existingMultiPlotId = FindPlotContainingHex(asset, 5).PlotId;

                Assert.That(GvgMapAuthoringUtility.TryMergeToMultiPlot(asset, map, 0, new[] { 1 }), Is.True);

                Assert.That(FindPlotContainingHex(asset, 5).PlotId, Is.EqualTo(existingMultiPlotId));
                Assert.That(FindPlotContainingHex(asset, 6).PlotId, Is.EqualTo(existingMultiPlotId));
                Assert.That(FindPlotContainingHex(asset, 0).PlotId, Is.Not.EqualTo(existingMultiPlotId));
                Assert.That(GvgMapAuthoringUtility.Validate(asset, map).IsValid, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RadiusChangesAreRejectedInTheCurrentVersion()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset, map);

                Assert.Throws<InvalidOperationException>(
                    () => GvgMapAuthoringUtility.RebuildRadius(asset, map, 2));
                Assert.That(map.Radius.Radius, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CsvOutputUsesOneFileAndExportsTimeRanges()
        {
            var map = CreateMap(0);
            var asset = CreateAsset();
            try
            {
                asset.MapId = "MapOne";
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(0, new[] { 0 }, PlotType.Normal, 0, 338400),
                    new GvgPlotAuthoringData(400, new[] { 0 }, PlotType.Normal, 338400, -1)
                });

                var csv = GvgMapAuthoringCsv.CreateGvgMapCsv(asset);
                var lines = csv.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                Assert.That(lines[0], Is.EqualTo("PlotId,HexIds,PlotType,Start,End,AffiliatedCampId"));
                Assert.That(lines[1], Is.EqualTo("0,\"[0]\",2,0,338400,-1"));
                Assert.That(lines[2], Is.EqualTo("400,\"[0]\",2,338400,-1,-1"));
                Assert.That(GvgMapAuthoringCsv.CreateFiles(asset).Keys,
                    Is.EquivalentTo(new[] { "GVGMap_MapOne.csv" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ExportWriterWritesUtf8BomAndReadmeDocumentsNewContract()
        {
            var map = CreateMap(0);
            var asset = CreateAsset();
            var directory = Path.Combine(Path.GetTempPath(), "GvgMapAuthoringTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                GvgMapAuthoringUtility.ResetToDefaultPlots(asset, map);
                GvgMapAuthoringExportWriter.WriteFiles(directory, GvgMapAuthoringCsv.CreateFiles(asset));

                var csvBytes = File.ReadAllBytes(Path.Combine(directory, "GVGMap_" + asset.MapId + ".csv"));
                Assert.That(csvBytes[0], Is.EqualTo(0xEF));
                Assert.That(csvBytes[1], Is.EqualTo(0xBB));
                Assert.That(csvBytes[2], Is.EqualTo(0xBF));

                var readme = GvgMapAuthoringCsv.CreateReadme(asset);
                Assert.That(readme, Does.Contain("PlotId,HexIds,PlotType,Start,End,AffiliatedCampId"));
                Assert.That(readme, Does.Contain("End=-1"));
                Assert.That(readme, Does.Contain("11000+"));
                Assert.That(readme, Does.Not.Contain("GenerationType"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RuntimeProjectionUsesStartForInitialState()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(0, new[] { 0 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(1, new[] { 1 }, PlotType.Normal, 100, -1),
                    new GvgPlotAuthoringData(2, new[] { 2 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(3, new[] { 3 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(4, new[] { 4 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(5, new[] { 5 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(6, new[] { 6 }, PlotType.Normal, 0, -1)
                });

                var runtimePlots = GvgMapAuthoringUtility.CreateRuntimePlots(asset, map);
                Assert.That(runtimePlots.First(plot => plot.PlotId == 0).PlotState, Is.EqualTo(PlotState.Open));
                Assert.That(runtimePlots.First(plot => plot.PlotId == 1).PlotState, Is.EqualTo(PlotState.NotOpen));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ValidationRejectsTimedMultiCellPlot()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(12000, new[] { 0, 1 }, PlotType.Normal, 1, -1)
                });

                var validation = GvgMapAuthoringUtility.Validate(asset, map);

                Assert.That(validation.IsValid, Is.False);
                Assert.That(ContainsIssue(validation, "Start=0 and End=-1"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }


        [Test]
        public void AuthoringClonePreservesAffiliatedCampId()
        {
            var plot = new GvgPlotAuthoringData(
                12000,
                new[] { 0, 1 },
                PlotType.Normal,
                0,
                -1,
                11000);

            var clone = plot.Clone();

            Assert.That(clone.AffiliatedCampId, Is.EqualTo(11000));
        }

        [Test]
        public void RuntimeProjectionUsesCampPlotIdAsDefaultAffiliation()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(11000, new[] { 0, 1 }, PlotType.Camp, 0, -1),
                    new GvgPlotAuthoringData(2, new[] { 2 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(3, new[] { 3 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(4, new[] { 4 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(5, new[] { 5 }, PlotType.Normal, 0, -1),
                    new GvgPlotAuthoringData(6, new[] { 6 }, PlotType.Normal, 0, -1)
                });

                var runtimePlot = GvgMapAuthoringUtility.CreateRuntimePlots(asset, map)[0];

                Assert.That(runtimePlot.AffiliatedCampId, Is.EqualTo(11000));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ValidationRejectsAffiliationOnTimedSingleCellPlot()
        {
            var map = CreateMap(0);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(0, new[] { 0 }, PlotType.Normal, 0, 100, 11000),
                    new GvgPlotAuthoringData(400, new[] { 0 }, PlotType.Normal, 100, -1)
                });

                var validation = GvgMapAuthoringUtility.Validate(asset, map);

                Assert.That(validation.IsValid, Is.False);
                Assert.That(ContainsIssue(validation, "AffiliatedCampId"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NormalizeRetainsCampPlotIdReferencedByAnAttachedPlot()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(11000, new[] { 0, 1 }, PlotType.Camp, 0, -1),
                    new GvgPlotAuthoringData(12000, new[] { 2, 3 }, PlotType.Normal, 0, -1, 11000)
                });

                GvgMapAuthoringUtility.NormalizePlotIds(asset, map);

                Assert.That(asset.Plots.Any(plot => plot.PlotId == 11000 && plot.PlotType == PlotType.Camp), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NormalizeRemapsAffiliatedCampIdWhenCampPlotIdChanges()
        {
            var map = CreateMap(1);
            var asset = CreateAsset();
            try
            {
                asset.ReplacePlots(new[]
                {
                    new GvgPlotAuthoringData(3, new[] { 0, 1 }, PlotType.Camp, 0, -1),
                    new GvgPlotAuthoringData(12000, new[] { 2, 3 }, PlotType.Normal, 0, -1, 3)
                });

                GvgMapAuthoringUtility.NormalizePlotIds(asset, map);

                var campPlot = asset.Plots.First(plot => plot.PlotType == PlotType.Camp);
                var attachedPlot = asset.Plots.First(plot => plot.AffiliatedCampId != Plot.NoAffiliatedCampId);
                Assert.That(campPlot.PlotId, Is.EqualTo(GvgMapAuthoringUtility.CalculateMultiPlotIdBase(PlotType.Camp)));
                Assert.That(attachedPlot.AffiliatedCampId, Is.EqualTo(campPlot.PlotId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
        private static RuntimeHexMap CreateMap(int radius)
        {
            return new RuntimeHexMap(new HexMapDefinition(radius));
        }

        private static GvgMapAuthoringAsset CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<GvgMapAuthoringAsset>();
            return asset;
        }

        private static GvgPlotAuthoringData FindPlot(GvgMapAuthoringAsset asset, int plotId)
        {
            var plot = asset.Plots.FirstOrDefault(candidate => candidate.PlotId == plotId);
            Assert.That(plot, Is.Not.Null);
            return plot;
        }

        private static GvgPlotAuthoringData FindPlotContainingHex(GvgMapAuthoringAsset asset, int hexId)
        {
            var plot = asset.Plots.FirstOrDefault(candidate => candidate.HexIds.Contains(hexId));
            Assert.That(plot, Is.Not.Null);
            return plot;
        }

        private static GvgPlotAuthoringData FindMultiPlot(GvgMapAuthoringAsset asset)
        {
            var plot = asset.Plots.FirstOrDefault(candidate => candidate.IsMultiCell);
            Assert.That(plot, Is.Not.Null);
            return plot;
        }

        private static bool ContainsIssue(GvgMapAuthoringValidationResult validation, string text)
        {
            return validation.Issues.Any(issue => issue.Message.Contains(text));
        }
    }
}

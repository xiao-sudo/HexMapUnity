using System;
using System.Collections.Generic;
using System.Linq;
using HexMap.Core;
using HexMap.Gvg;
using HexMap.Gvg.Authoring;
using HexMap.Runtime;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEngine;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.Gvg.Tests
{
    [TestFixture]
    public sealed class GvgMapRuntimeComposerTests
    {
        [Test]
        public void ComposeAndFindPathBetweenSingleCellPlots()
        {
            var map = CreateMap(2);
            var rows = DefaultRows(map);

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.True, error);
            Assert.That(registry.Count, Is.EqualTo(map.Count));

            var service = new PlotPathService(registry);
            var result = new PathResult(new List<HexCell>(map.Count));
            service.FindPath(CellIdAt(map, 0, 0), CellIdAt(map, 2, 0), Plot.NoFactionId, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Cost, Is.GreaterThan(0));
            Assert.That(result.ReachedTarget.Coordinate, Is.EqualTo(CellAt(map, 2, 0).Coordinate));
        }

        [Test]
        public void FindPathAcceptsAnyCellInMultiCellTargetPlot()
        {
            var map = CreateMap(2);
            var rows = DefaultRows(map);
            var targetCells = new[] { CellIdAt(map, 2, 0), CellIdAt(map, 2, -1) };
            RemoveRowsForCells(rows, targetCells);
            rows.Add(new GvgPlotRuntimeData(12000, targetCells, PlotType.Normal));

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.True, error);

            var service = new PlotPathService(registry);
            var result = new PathResult(new List<HexCell>(map.Count));
            service.FindPath(CellIdAt(map, 0, 0), 12000, Plot.NoFactionId, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                result.ReachedTarget.Coordinate,
                Is.EqualTo(CellAt(map, 2, 0).Coordinate).Or.EqualTo(CellAt(map, 2, -1).Coordinate));
        }

        [Test]
        public void ObstaclePlotBlocksPassage()
        {
            var map = CreateMap(2);
            var rows = DefaultRows(map);
            var obstacleCell = CellAt(map, 1, 0);
            RemoveRowsForCells(rows, new[] { obstacleCell.Id });
            rows.Add(new GvgPlotRuntimeData(obstacleCell.Id, new[] { obstacleCell.Id }, PlotType.Obstacle));

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.True, error);
            Assert.That(registry.GetPlot(obstacleCell.Id).BlockingState, Is.EqualTo(BlockingState.Blocked));

            var service = new PlotPathService(registry);
            var result = new PathResult(new List<HexCell>(map.Count));
            service.FindPath(CellIdAt(map, 0, 0), CellIdAt(map, 2, 0), Plot.NoFactionId, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Cells.Select(cell => cell.Id), Has.No.Member(obstacleCell.Id));
        }

        [Test]
        public void NotOpenPlotBlocksPassage()
        {
            var map = CreateMap(2);
            var rows = DefaultRows(map);
            var closedCell = CellAt(map, 1, 0);
            RemoveRowsForCells(rows, new[] { closedCell.Id });
            rows.Add(new GvgPlotRuntimeData(closedCell.Id, new[] { closedCell.Id }, PlotType.Normal, 100, -1, Plot.NoAffiliatedCampId));

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.True, error);
            Assert.That(registry.GetPlot(closedCell.Id).PlotState, Is.EqualTo(PlotState.NotOpen));

            var service = new PlotPathService(registry);
            var result = new PathResult(new List<HexCell>(map.Count));
            service.FindPath(CellIdAt(map, 0, 0), CellIdAt(map, 2, 0), Plot.NoFactionId, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Cells.Select(cell => cell.Id), Has.No.Member(closedCell.Id));
        }

        [Test]
        public void TimeLayersKeepOneOpenPlotPerCell()
        {
            var map = CreateMap(2);
            var rows = DefaultRows(map);
            var layeredCell = CellAt(map, 1, 0);
            rows.Add(new GvgPlotRuntimeData(100, new[] { layeredCell.Id }, PlotType.Normal, 5, -1, Plot.NoAffiliatedCampId));

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.True, error);

            IReadOnlyList<Plot> cellPlots;
            Assert.That(registry.TryGetPlotsForCell(layeredCell.Id, out cellPlots), Is.True);
            Assert.That(cellPlots.Count, Is.EqualTo(2));

            var service = new PlotPathService(registry);
            var result = new PathResult(new List<HexCell>(map.Count));
            service.FindPath(CellIdAt(map, 0, 0), CellIdAt(map, 2, 0), Plot.NoFactionId, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Cells.Select(cell => cell.Id), Has.Member(layeredCell.Id));
        }

        [Test]
        public void OutOfBoundsHexIdProducesClearError()
        {
            var map = CreateMap(0);
            var rows = DefaultRows(map);
            rows.Add(new GvgPlotRuntimeData(1, new[] { 999 }, PlotType.Normal));

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.False);
            Assert.That(error, Does.Contain("999"));
            Assert.That(error, Does.Contain("outside the map topology"));
        }

        [Test]
        public void DuplicatePlotIdProducesClearError()
        {
            var map = CreateMap(2);
            var rows = DefaultRows(map);
            rows.Add(new GvgPlotRuntimeData(0, new[] { 5 }, PlotType.Normal));

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.False);
            Assert.That(error, Does.Contain("Duplicate PlotId 0"));
        }

        [Test]
        public void DuplicateHexIdInRowProducesClearError()
        {
            var map = CreateMap(2);
            var rows = DefaultRows(map);
            rows.Add(new GvgPlotRuntimeData(100, new[] { 5, 5 }, PlotType.Normal));

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.False);
            Assert.That(error, Does.Contain("duplicate HexId 5"));
        }

        [Test]
        public void TwoOpenRowsForOneCellProducesClearError()
        {
            var map = CreateMap(0);
            var rows = new List<GvgPlotRuntimeData>
            {
                new GvgPlotRuntimeData(0, new[] { 0 }, PlotType.Normal),
                new GvgPlotRuntimeData(100, new[] { 0 }, PlotType.Normal)
            };

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.False);
            Assert.That(error, Does.Contain("more than one open plot row"));
        }

        [Test]
        public void EmptyRowsComposeToEmptyRegistry()
        {
            var map = CreateMap(1);

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, new List<GvgPlotRuntimeData>(), out registry, out error), Is.True, error);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void NullMapOrNullRowsAreRejected()
        {
            PlotRegistry registry;
            string error;

            Assert.That(GvgMapRuntimeComposer.TryCompose(null, new List<GvgPlotRuntimeData>(), out registry, out error), Is.False);
            Assert.That(GvgMapRuntimeComposer.TryCompose(CreateMap(0), null, out registry, out error), Is.False);
        }

        [Test]
        public void AdapterAndComposerMatchLegacyCreateRuntimePlots()
        {
            var map = CreateMap(2);
            var asset = CreateAsset();
            try
            {
                PopulateAsset(asset, map);

                var legacyPlots = GvgMapAuthoringUtility.CreateRuntimePlots(asset, map);
                var rows = GvgMapAuthoringRuntimeAdapter.ToRuntimeData(asset);
                PlotRegistry registry;
                string error;
                Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.True, error);

                Assert.That(registry.Count, Is.EqualTo(legacyPlots.Count));
                foreach (var legacyPlot in legacyPlots)
                {
                    var runtimePlot = registry.GetPlot(legacyPlot.PlotId);
                    Assert.That(runtimePlot.PlotType, Is.EqualTo(legacyPlot.PlotType));
                    Assert.That(runtimePlot.PlotState, Is.EqualTo(legacyPlot.PlotState));
                    Assert.That(runtimePlot.BlockingState, Is.EqualTo(legacyPlot.BlockingState));
                    Assert.That(runtimePlot.AffiliatedCampId, Is.EqualTo(legacyPlot.AffiliatedCampId));
                    Assert.That(runtimePlot.OwnerFactionId, Is.EqualTo(legacyPlot.OwnerFactionId));
                    Assert.That(
                        runtimePlot.Cells.Select(cell => cell.Id),
                        Is.EqualTo(legacyPlot.Cells.Select(cell => cell.Id)));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AdapterCarriesOwnerFactionIdThroughToRuntimePlots()
        {
            var map = CreateMap(0);
            var asset = CreateAsset();
            try
            {
                var cell = CellAt(map, 0, 0);
                var plot = new GvgPlotAuthoringData(
                    cell.Id,
                    new[] { cell.Id },
                    PlotType.Normal,
                    0,
                    -1,
                    Plot.NoAffiliatedCampId,
                    7);
                asset.ReplacePlots(new[] { plot });

                var rows = GvgMapAuthoringRuntimeAdapter.ToRuntimeData(asset);
                Assert.That(rows.Count, Is.EqualTo(1));
                Assert.That(rows[0].OwnerFactionId, Is.EqualTo(7));

                PlotRegistry registry;
                string error;
                Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.True, error);
                Assert.That(registry.GetPlot(cell.Id).OwnerFactionId, Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void PathWorldCentersCanBeProducedFromViewLayout()
        {
            var viewObject = new GameObject("Hex Map View");
            try
            {
                var mapView = viewObject.AddComponent<HexMapView>();
                mapView.Radius = 2;
                mapView.OuterRadius = 1.5f;
                mapView.SecondaryScale = 0.8f;

                RuntimeHexMap map;
                HexLayout layout;
                string snapshotError;
                Assert.That(mapView.TryCreateSnapshots(out map, out layout, out snapshotError), Is.True, snapshotError);

                PlotRegistry registry;
                string error;
                Assert.That(GvgMapRuntimeComposer.TryCompose(map, DefaultRows(map), out registry, out error), Is.True, error);

                var service = new PlotPathService(registry);
                var result = new PathResult(new List<HexCell>(map.Count));
                service.FindPath(CellIdAt(map, 0, 0), CellIdAt(map, 2, 0), Plot.NoFactionId, result);
                Assert.That(result.IsSuccess, Is.True);

                var centers = new List<Vector3>(result.Count);
                Assert.That(result.CopyWorldCentersTo(layout, centers), Is.True);
                Assert.That(centers.Count, Is.EqualTo(result.Count));
                Assert.That(centers.All(center => float.IsNaN(center.x) == false), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
            }
        }

        private static RuntimeHexMap CreateMap(int radius)
        {
            return new RuntimeHexMap(new HexMapDefinition(radius));
        }

        private static HexCell CellAt(RuntimeHexMap map, int q, int r)
        {
            return map.Query(new HexCoord(q, r)).Cell;
        }

        private static int CellIdAt(RuntimeHexMap map, int q, int r)
        {
            return CellAt(map, q, r).Id;
        }

        [Test]
        public void ComposerRejectsUninitializedStructRow()
        {
            var map = CreateMap(0);
            var rows = new List<GvgPlotRuntimeData> { default(GvgPlotRuntimeData) };

            PlotRegistry registry;
            string error;
            Assert.That(GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error), Is.False);
            Assert.That(error, Does.Contain("uninitialized entry"));
        }
        private static List<GvgPlotRuntimeData> DefaultRows(RuntimeHexMap map)
        {
            var rows = new List<GvgPlotRuntimeData>(map.Count);
            for (var index = 0; index < map.Cells.Count; index++)
            {
                var cell = map.Cells[index];
                rows.Add(new GvgPlotRuntimeData(cell.Id, new[] { cell.Id }, PlotType.Normal));
            }

            return rows;
        }

        private static void RemoveRowsForCells(List<GvgPlotRuntimeData> rows, IReadOnlyCollection<int> cellIds)
        {
            rows.RemoveAll(row => row.HexIds.Count == 1 && cellIds.Contains(row.HexIds[0]));
        }

        private static GvgMapAuthoringAsset CreateAsset()
        {
            return ScriptableObject.CreateInstance<GvgMapAuthoringAsset>();
        }

        private static void PopulateAsset(GvgMapAuthoringAsset asset, RuntimeHexMap map)
        {
            var campCells = new[] { CellIdAt(map, 0, 0), CellIdAt(map, 1, 0) };
            var multiCells = new[] { CellIdAt(map, -1, 0), CellIdAt(map, 0, -1) };
            var obstacleCell = CellAt(map, 1, -1);
            var timedCell = CellAt(map, -1, 1);

            var plots = new List<GvgPlotAuthoringData>();
            foreach (var cell in map.Cells)
            {
                if (campCells.Contains(cell.Id) || multiCells.Contains(cell.Id) ||
                    cell.Id == obstacleCell.Id || cell.Id == timedCell.Id)
                {
                    continue;
                }

                plots.Add(new GvgPlotAuthoringData(cell.Id, new[] { cell.Id }, PlotType.Normal, 0, -1));
            }

            plots.Add(new GvgPlotAuthoringData(11000, campCells, PlotType.Camp, 0, -1));
            plots.Add(new GvgPlotAuthoringData(12000, multiCells, PlotType.Normal, 0, -1));
            plots.Add(new GvgPlotAuthoringData(obstacleCell.Id, new[] { obstacleCell.Id }, PlotType.Obstacle, 0, -1));
            plots.Add(new GvgPlotAuthoringData(timedCell.Id, new[] { timedCell.Id }, PlotType.Normal, 0, 1));
            plots.Add(new GvgPlotAuthoringData(100, new[] { timedCell.Id }, PlotType.Normal, 1, -1));

            asset.ReplacePlots(plots);
        }
    }
}

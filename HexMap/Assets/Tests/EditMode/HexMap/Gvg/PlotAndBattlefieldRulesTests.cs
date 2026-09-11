using System;
using System.Collections.Generic;
using NUnit.Framework;
using HexMap.Core;
using HexMap.Gvg;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.Gvg.Tests
{
    [TestFixture]
    public sealed class PlotAndBattlefieldRulesTests
    {
        [Test]
        public void RegistryMapsEveryPlotCellBackToItsPlot()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var firstCell = CellAt(map, 0, 0);
            var secondCell = CellAt(map, 1, 0);
            var plot = CreatePlot(1, new[] { firstCell, secondCell });
            var registry = new PlotRegistry(map, new[] { plot });

            Plot found;
            Assert.That(registry.TryGetPlot(firstCell, out found), Is.True);
            Assert.That(found, Is.SameAs(plot));
            Assert.That(registry.GetPlotForCell(secondCell.Id), Is.SameAs(plot));
        }

        [Test]
        public void RegistryAllowsMultipleClosedOrTemporalPlotsForOneCell()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(0));
            var cell = CellAt(map, 0, 0);
            var first = CreatePlot(0, new[] { cell }, state: PlotState.NotOpen);
            var second = CreatePlot(400, new[] { cell }, state: PlotState.Open);
            var registry = new PlotRegistry(map, new[] { first, second });

            IReadOnlyList<Plot> plots;
            Assert.That(registry.TryGetPlotsForCell(cell.Id, out plots), Is.True);
            Assert.That(plots.Count, Is.EqualTo(2));

            Plot active;
            Assert.That(registry.TryGetPlotForCell(cell.Id, out active), Is.True);
            Assert.That(active, Is.SameAs(second));
        }

        [Test]
        public void RegistryRejectsMultipleOpenPlotsForOneCell()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(0));
            var cell = CellAt(map, 0, 0);
            var first = CreatePlot(0, new[] { cell });
            var second = CreatePlot(400, new[] { cell });
            var registry = new PlotRegistry(map, new[] { first, second });

            Plot active;
            Assert.That(registry.TryGetPlotForCell(cell.Id, out active), Is.False);
        }

        [Test]
        public void PlotRejectsDuplicateCellsAndPassableObstacle()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var cell = CellAt(map, 0, 0);

            Assert.Throws<ArgumentException>(() => new Plot(
                1,
                new[] { cell, cell },
                PlotType.Normal,
                PlotState.Open,
                FactionId.Neutral,
                OwnershipMode.Capturable,
                BlockingState.Passable));

            Assert.Throws<ArgumentException>(() => new Plot(
                2,
                new[] { cell },
                PlotType.Obstacle,
                PlotState.Open,
                FactionId.Neutral,
                OwnershipMode.Capturable,
                BlockingState.Passable));
        }

        [Test]
        public void PlotAllowsTypedPositivePlotIdsForMultiCellPlots()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var plot = CreatePlot(12000, new[] { CellAt(map, 0, 0), CellAt(map, 1, 0) });

            Assert.That(plot.PlotId, Is.EqualTo(12000));
        }

        [Test]
        public void PlotPolicySeparatesPassingFromEntering()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var own = CellAt(map, 0, 0);
            var enemy = CellAt(map, 1, 0);
            var fixedEnemy = CellAt(map, 0, 1);
            var blocked = CellAt(map, -1, 0);
            var closed = CellAt(map, -1, 1);
            var plots = new[]
            {
                CreatePlot(1, new[] { own }, FactionId.Red),
                CreatePlot(2, new[] { enemy }, FactionId.Blue),
                CreatePlot(3, new[] { fixedEnemy }, FactionId.Blue, OwnershipMode.Fixed),
                CreatePlot(4, new[] { blocked }, FactionId.Red, OwnershipMode.Capturable, BlockingState.Blocked),
                CreatePlot(5, new[] { closed }, FactionId.Red, OwnershipMode.Capturable, BlockingState.Passable, PlotState.NotOpen)
            };
            var policy = new PlotPathPolicy(new PlotRegistry(map, plots), FactionId.Red);

            Assert.That(policy.CanPass(own), Is.True);
            Assert.That(policy.CanEnter(own), Is.True);
            Assert.That(policy.CanPass(enemy), Is.False);
            Assert.That(policy.CanEnter(enemy), Is.True);
            Assert.That(policy.CanPass(fixedEnemy), Is.False);
            Assert.That(policy.CanEnter(fixedEnemy), Is.False);
            Assert.That(policy.CanPass(blocked), Is.False);
            Assert.That(policy.CanEnter(blocked), Is.False);
            Assert.That(policy.CanPass(closed), Is.False);
            Assert.That(policy.CanEnter(closed), Is.False);
        }

        [Test]
        public void PlotPathServiceAcceptsAnyCellInAMultiCellTargetPlot()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var startCell = CellAt(map, -1, 0);
            var targetCellA = CellAt(map, 1, 0);
            var targetCellB = CellAt(map, 1, -1);
            var plots = new List<Plot>
            {
                CreatePlot(1, new[] { startCell }, FactionId.Red)
            };
            var nextId = 2;
            for (var index = 0; index < map.Cells.Count; index++)
            {
                var cell = map.Cells[index];
                if (cell.Id == startCell.Id || cell.Id == targetCellA.Id || cell.Id == targetCellB.Id)
                    continue;
                plots.Add(CreatePlot(nextId++, new[] { cell }, FactionId.Red));
            }

            plots.Add(CreatePlot(
                12000,
                new[] { targetCellA, targetCellB },
                FactionId.Blue));

            var registry = new PlotRegistry(map, plots);
            var service = new PlotPathService(registry);
            var result = new PathResult(new List<HexCell>(map.Count));

            service.FindPath(1, 12000, FactionId.Red, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ReachedTarget.Coordinate, Is.EqualTo(targetCellA.Coordinate).Or.EqualTo(targetCellB.Coordinate));
        }

        [Test]
        public void SameOpenPlotReturnsZeroStepPath()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var cell = CellAt(map, 0, 0);
            var plot = CreatePlot(
                1,
                new[] { cell },
                FactionId.Blue,
                OwnershipMode.Fixed,
                BlockingState.Passable,
                PlotState.Open);
            var service = new PlotPathService(new PlotRegistry(map, new[] { plot }));
            var result = new PathResult(new List<HexCell>(1));

            service.FindPath(1, 1, FactionId.Red, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Cost, Is.EqualTo(0));
            Assert.That(result.ReachedTarget, Is.EqualTo(cell));
        }

        [Test]
        public void PlotTypeUsesTheSpecifiedNumericValues()
        {
            Assert.That((int)PlotType.Camp, Is.EqualTo(1));
            Assert.That((int)PlotType.Normal, Is.EqualTo(2));
            Assert.That((int)PlotType.Grass, Is.EqualTo(3));
            Assert.That((int)PlotType.SmallCity, Is.EqualTo(4));
            Assert.That((int)PlotType.BigCity, Is.EqualTo(5));
            Assert.That((int)PlotType.Capital, Is.EqualTo(6));
            Assert.That((int)PlotType.Obstacle, Is.EqualTo(7));
            Assert.That((int)PlotState.NotOpen, Is.EqualTo(0));
            Assert.That((int)PlotState.Open, Is.EqualTo(1));
        }

        [Test]
        public void PlotOpenAndCloseAreIdempotent()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(0));
            var plot = CreatePlot(0, new[] { CellAt(map, 0, 0) }, state: PlotState.NotOpen);

            Assert.That(plot.Open(), Is.True);
            Assert.That(plot.PlotState, Is.EqualTo(PlotState.Open));
            Assert.That(plot.Open(), Is.False);
            Assert.That(plot.Close(), Is.True);
            Assert.That(plot.PlotState, Is.EqualTo(PlotState.NotOpen));
            Assert.That(plot.Close(), Is.False);
        }

        private static HexCell CellAt(RuntimeHexMap map, int q, int r)
        {
            return map.Query(new HexCoord(q, r)).Cell;
        }

        private static Plot CreatePlot(
            int id,
            IReadOnlyList<HexCell> cells,
            FactionId owner = FactionId.Neutral,
            OwnershipMode ownershipMode = OwnershipMode.Capturable,
            BlockingState blockingState = BlockingState.Passable,
            PlotState state = PlotState.Open)
        {
            return new Plot(
                id,
                cells,
                PlotType.Normal,
                state,
                owner,
                ownershipMode,
                blockingState);
        }
    }
}
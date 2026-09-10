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
            var plot = CreatePlot(1, new[] { firstCell, secondCell }, firstCell);
            var registry = new PlotRegistry(map, new[] { plot });

            Plot found;
            Assert.That(registry.TryGetPlot(firstCell, out found), Is.True);
            Assert.That(found, Is.SameAs(plot));
            Assert.That(registry.GetPlotForCell(secondCell.Id), Is.SameAs(plot));
        }

        [Test]
        public void RegistryRejectsMultiplePlotOwnership()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var cell = CellAt(map, 0, 0);
            var first = CreatePlot(1, new[] { cell }, cell);
            var second = CreatePlot(2, new[] { cell }, cell);
            var registry = new PlotRegistry(map, new[] { first });

            Assert.Throws<ArgumentException>(() => registry.Add(second));
        }

        [Test]
        public void PlotRejectsInvalidRepresentativeAndPassableObstacle()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var cell = CellAt(map, 0, 0);
            var otherCell = CellAt(map, 1, 0);

            Assert.Throws<ArgumentException>(() => new Plot(
                1,
                new[] { cell },
                otherCell,
                PlotType.Normal,
                PlotState.Open,
                FactionId.Neutral,
                OwnershipMode.Capturable,
                BlockingState.Passable));

            Assert.Throws<ArgumentException>(() => new Plot(
                2,
                new[] { cell },
                cell,
                PlotType.Obstacle,
                PlotState.Open,
                FactionId.Neutral,
                OwnershipMode.Capturable,
                BlockingState.Passable));
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
                CreatePlot(1, new[] { own }, own, FactionId.Red),
                CreatePlot(2, new[] { enemy }, enemy, FactionId.Blue),
                CreatePlot(3, new[] { fixedEnemy }, fixedEnemy, FactionId.Blue, OwnershipMode.Fixed),
                CreatePlot(4, new[] { blocked }, blocked, FactionId.Red, OwnershipMode.Capturable, BlockingState.Blocked),
                CreatePlot(5, new[] { closed }, closed, FactionId.Red, OwnershipMode.Capturable, BlockingState.Passable, PlotState.NotOpened)
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
                CreatePlot(1, new[] { startCell }, startCell, FactionId.Red)
            };
            var nextId = 2;
            for (var index = 0; index < map.Cells.Count; index++)
            {
                var cell = map.Cells[index];
                if (cell.Id == startCell.Id || cell.Id == targetCellA.Id || cell.Id == targetCellB.Id)
                    continue;
                plots.Add(CreatePlot(nextId++, new[] { cell }, cell, FactionId.Red));
            }

            plots.Add(CreatePlot(
                99,
                new[] { targetCellA, targetCellB },
                targetCellA,
                FactionId.Blue));

            var registry = new PlotRegistry(map, plots);
            var service = new PlotPathService(registry);
            var result = new PathResult(new List<HexCell>(map.Count));

            service.FindPath(1, 99, FactionId.Red, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ReachedTarget.Coordinate, Is.EqualTo(targetCellA.Coordinate).Or.EqualTo(targetCellB.Coordinate));
        }

        [Test]
        public void SameBattlePlotReturnsZeroStepPath()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var cell = CellAt(map, 0, 0);
            var plot = CreatePlot(
                1,
                new[] { cell },
                cell,
                FactionId.Blue,
                OwnershipMode.Fixed,
                BlockingState.Passable,
                PlotState.Battle);
            var service = new PlotPathService(new PlotRegistry(map, new[] { plot }));
            var result = new PathResult(new List<HexCell>(1));

            service.FindPath(1, 1, FactionId.Red, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Cost, Is.EqualTo(0));
            Assert.That(result.ReachedTarget, Is.EqualTo(cell));
        }

        private static Plot CreatePlot(
            int id,
            IReadOnlyList<HexCell> cells,
            HexCell representative,
            FactionId owner = FactionId.Neutral,
            OwnershipMode ownershipMode = OwnershipMode.Capturable,
            BlockingState blockingState = BlockingState.Passable,
            PlotState state = PlotState.Open)
        {
            return new Plot(
                id,
                cells,
                representative,
                PlotType.Normal,
                state,
                owner,
                ownershipMode,
                blockingState);
        }

        private static HexCell CellAt(RuntimeHexMap map, int q, int r)
        {
            return map.Query(new HexCoord(q, r)).Cell;
        }
    }
}

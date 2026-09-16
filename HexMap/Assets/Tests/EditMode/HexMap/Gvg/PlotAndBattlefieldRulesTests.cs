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
        private const int RedFaction = 10;
        private const int BlueFaction = 20;

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
                BlockingState.Passable));

            Assert.Throws<ArgumentException>(() => new Plot(
                2,
                new[] { cell },
                PlotType.Obstacle,
                PlotState.Open,
                BlockingState.Passable));
        }

        [Test]
        public void PlotRejectsOwnerFactionIdBelowNoFactionId()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(0));
            var cell = CellAt(map, 0, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => new Plot(
                1,
                new[] { cell },
                PlotType.Normal,
                PlotState.Open,
                BlockingState.Passable,
                Plot.NoFactionId - 1));
        }

        [Test]
        public void PlotDefaultsOwnerFactionIdToNoFactionId()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(0));
            var plot = CreatePlot(1, new[] { CellAt(map, 0, 0) });

            Assert.That(plot.OwnerFactionId, Is.EqualTo(Plot.NoFactionId));
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
            var affiliatedEnemy = CellAt(map, 0, 1);
            var blocked = CellAt(map, -1, 0);
            var closed = CellAt(map, -1, 1);
            var plots = new[]
            {
                CreatePlot(1, new[] { own }, RedFaction),
                CreatePlot(2, new[] { enemy }, BlueFaction),
                CreatePlot(3, new[] { affiliatedEnemy }, Plot.NoFactionId, affiliatedCampId: 12000),
                CreatePlot(4, new[] { blocked }, RedFaction, BlockingState.Blocked),
                CreatePlot(5, new[] { closed }, RedFaction, state: PlotState.NotOpen)
            };
            var resolver = new TestCampFactionResolver();
            resolver.Set(12000, BlueFaction);
            var policy = new PlotPathPolicy(new PlotRegistry(map, plots), resolver, RedFaction);
            var bluePolicy = new PlotPathPolicy(new PlotRegistry(map, plots), resolver, BlueFaction);

            Assert.That(policy.CanPass(own), Is.True);
            Assert.That(policy.CanEnter(own), Is.True);
            Assert.That(policy.CanPass(enemy), Is.False);
            Assert.That(policy.CanEnter(enemy), Is.True);
            Assert.That(policy.CanPass(affiliatedEnemy), Is.False);
            Assert.That(policy.CanEnter(affiliatedEnemy), Is.False);
            Assert.That(bluePolicy.CanPass(affiliatedEnemy), Is.False);
            Assert.That(bluePolicy.CanEnter(affiliatedEnemy), Is.True);
            Assert.That(policy.CanPass(blocked), Is.False);
            Assert.That(policy.CanEnter(blocked), Is.False);
            Assert.That(policy.CanPass(closed), Is.False);
            Assert.That(policy.CanEnter(closed), Is.False);
        }

        [Test]
        public void AffiliatedPlotRequiresTheCampCurrentFaction()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(0));
            var cell = CellAt(map, 0, 0);
            var plot = CreatePlot(1, new[] { cell }, affiliatedCampId: 12000);
            var registry = new PlotRegistry(map, new[] { plot });
            var resolver = new TestCampFactionResolver();
            resolver.Set(12000, RedFaction);

            Assert.That(new PlotPathPolicy(registry, resolver, RedFaction).CanEnter(cell), Is.True);
            Assert.That(new PlotPathPolicy(registry, resolver, BlueFaction).CanEnter(cell), Is.False);
            Assert.That(new PlotPathPolicy(registry, resolver, RedFaction).CanPass(cell), Is.False);
        }

        [Test]
        public void PlotPolicyCanSwitchMovingFactionOnReusedInstance()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(0));
            var cell = CellAt(map, 0, 0);
            var plot = CreatePlot(1, new[] { cell }, RedFaction);
            var policy = new PlotPathPolicy(new PlotRegistry(map, new[] { plot }), BlueFaction);

            Assert.That(policy.CanPass(cell), Is.False);

            policy.SetMovingFaction(RedFaction);

            Assert.That(policy.CanPass(cell), Is.True);

            policy.SetMovingFaction(BlueFaction);

            Assert.That(policy.CanPass(cell), Is.False);
        }

        [Test]
        public void PlotPathServiceWithoutResolverRejectsAffiliatedTarget()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var startCell = CellAt(map, -1, 0);
            var targetCell = CellAt(map, 1, 0);
            var plots = new List<Plot>
            {
                CreatePlot(1, new[] { startCell }, RedFaction),
                CreatePlot(2, new[] { targetCell }, Plot.NoFactionId, affiliatedCampId: 12000)
            };
            foreach (var cell in map.Cells)
            {
                if (cell.Id == startCell.Id || cell.Id == targetCell.Id) continue;
                plots.Add(CreatePlot(cell.Id + 100, new[] { cell }, RedFaction));
            }

            // No resolver is supplied, so the default resolver maps every camp to
            // Plot.NoFactionId (false). The affiliated target camp cannot be resolved
            // to the mover faction, so the target must not be enterable.
            var service = new PlotPathService(new PlotRegistry(map, plots));
            var result = new PathResult(new List<HexCell>(map.Count));

            service.FindPath(1, 2, RedFaction, result);

            Assert.That(result.IsSuccess, Is.False);
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
                CreatePlot(1, new[] { startCell }, RedFaction)
            };
            var nextId = 2;
            for (var index = 0; index < map.Cells.Count; index++)
            {
                var cell = map.Cells[index];
                if (cell.Id == startCell.Id || cell.Id == targetCellA.Id || cell.Id == targetCellB.Id)
                    continue;
                plots.Add(CreatePlot(nextId++, new[] { cell }, RedFaction));
            }

            plots.Add(CreatePlot(
                12000,
                new[] { targetCellA, targetCellB },
                BlueFaction));

            var registry = new PlotRegistry(map, plots);
            var service = new PlotPathService(registry);
            var result = new PathResult(new List<HexCell>(map.Count));

            service.FindPath(1, 12000, RedFaction, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ReachedTarget.Coordinate, Is.EqualTo(targetCellA.Coordinate).Or.EqualTo(targetCellB.Coordinate));
        }

        [Test]
        public void PlotPathServiceUsesCampFactionResolverForAffiliatedTarget()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var startCell = CellAt(map, -1, 0);
            var targetCell = CellAt(map, 1, 0);
            var plots = new List<Plot>
            {
                CreatePlot(1, new[] { startCell }, RedFaction),
                CreatePlot(2, new[] { targetCell }, Plot.NoFactionId, affiliatedCampId: 12000)
            };
            foreach (var cell in map.Cells)
            {
                if (cell.Id == startCell.Id || cell.Id == targetCell.Id) continue;
                plots.Add(CreatePlot(cell.Id + 100, new[] { cell }, RedFaction));
            }

            var resolver = new TestCampFactionResolver();
            resolver.Set(12000, RedFaction);
            var service = new PlotPathService(new PlotRegistry(map, plots), resolver);
            var result = new PathResult(new List<HexCell>(map.Count));

            service.FindPath(1, 2, RedFaction, result);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ReachedTarget, Is.EqualTo(targetCell));
        }

        [Test]
        public void PlotPathServiceReusesOnePolicyAcrossMovingFactions()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var startCell = CellAt(map, 0, 0);
            var targetCell = CellAt(map, 1, 0);
            var plots = new List<Plot>
            {
                CreatePlot(1, new[] { startCell }, RedFaction),
                CreatePlot(2, new[] { targetCell }, RedFaction, affiliatedCampId: 12000)
            };
            var resolver = new TestCampFactionResolver();
            resolver.Set(12000, RedFaction);
            var service = new PlotPathService(new PlotRegistry(map, plots), resolver);
            var result = new PathResult(new List<HexCell>(map.Count));

            service.FindPath(1, 2, RedFaction, result);

            Assert.That(result.IsSuccess, Is.True);

            service.FindPath(1, 2, BlueFaction, result);

            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        public void SameOpenPlotReturnsZeroStepPath()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(1));
            var cell = CellAt(map, 0, 0);
            var plot = CreatePlot(
                1,
                new[] { cell },
                BlueFaction);
            var service = new PlotPathService(new PlotRegistry(map, new[] { plot }));
            var result = new PathResult(new List<HexCell>(1));

            service.FindPath(1, 1, RedFaction, result);

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
            int ownerFactionId = Plot.NoFactionId,
            BlockingState blockingState = BlockingState.Passable,
            PlotState state = PlotState.Open,
            int affiliatedCampId = Plot.NoAffiliatedCampId)
        {
            return new Plot(
                id,
                cells,
                PlotType.Normal,
                state,
                blockingState,
                ownerFactionId,
                affiliatedCampId);
        }

        private sealed class TestCampFactionResolver : ICampFactionResolver
        {
            private readonly Dictionary<int, int> m_FactionsByCampId =
                new Dictionary<int, int>();

            public void Set(int campId, int factionId)
            {
                m_FactionsByCampId[campId] = factionId;
            }

            public bool TryGetFaction(int campId, out int factionId)
            {
                return m_FactionsByCampId.TryGetValue(campId, out factionId);
            }
        }
    }
}

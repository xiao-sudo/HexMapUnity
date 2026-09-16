using System.Collections.Generic;
using HexMap.Gvg;
using HexMap.Runtime;
using NUnit.Framework;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.UnityRuntime.Tests
{
    /// <summary>
    /// EditMode coverage for the mock runtime plot-table generator used by the
    /// RuntimeGvgDemo editor test environment (issue 05).
    /// </summary>
    [TestFixture]
    public sealed class RuntimeGvgDemoDataTests
    {
        [Test]
        public void DefaultTableCoversEveryCellExactlyOnce()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(3));
            var rows = RuntimeGvgDemoData.CreateDefaultTable(map);

            // One multi-cell plot covers two cells with a single row, so the table has
            // one fewer row than the map has cells.
            Assert.That(rows.Count, Is.EqualTo(map.Count - 1));

            var coveredIds = new HashSet<int>();
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                for (var hexIndex = 0; hexIndex < row.HexIds.Count; hexIndex++)
                {
                    Assert.That(
                        coveredIds.Add(row.HexIds[hexIndex]),
                        Is.True,
                        "HexId " + row.HexIds[hexIndex] + " is covered more than once.");
                }
            }

            Assert.That(coveredIds.Count, Is.EqualTo(map.Count));
        }

        [Test]
        public void DefaultTableIncludesObstacleNotOpenAndMultiCellPlots()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(3));
            var rows = RuntimeGvgDemoData.CreateDefaultTable(map);

            var hasObstacle = false;
            var hasNotOpen = false;
            var hasMultiCell = false;
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row.PlotType == PlotType.Obstacle)
                {
                    hasObstacle = true;
                }

                if (row.Start != 0)
                {
                    hasNotOpen = true;
                }

                if (row.HexIds.Count > 1)
                {
                    hasMultiCell = true;
                }
            }

            Assert.That(hasObstacle, Is.True, "The demo table should contain Obstacle plots.");
            Assert.That(hasNotOpen, Is.True, "The demo table should contain a NotOpen (start != 0) plot.");
            Assert.That(hasMultiCell, Is.True, "The demo table should contain a multi-cell plot.");
        }

        [Test]
        public void DefaultTableComposesIntoAWorkingRegistry()
        {
            var map = new RuntimeHexMap(new HexMapDefinition(3));
            var rows = RuntimeGvgDemoData.CreateDefaultTable(map);

            PlotRegistry registry;
            string error;
            Assert.That(
                GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error),
                Is.True,
                error);

            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.Count, Is.EqualTo(rows.Count));
        }

        [Test]
        public void DefaultTableComposesForSmallRadii()
        {
            for (var radius = 1; radius <= 4; radius++)
            {
                var map = new RuntimeHexMap(new HexMapDefinition(radius));
                var rows = RuntimeGvgDemoData.CreateDefaultTable(map);

                PlotRegistry registry;
                string error;
                Assert.That(
                    GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error),
                    Is.True,
                    "Radius " + radius + " failed: " + error);
                Assert.That(registry.Count, Is.GreaterThan(0));
            }
        }
    }
}

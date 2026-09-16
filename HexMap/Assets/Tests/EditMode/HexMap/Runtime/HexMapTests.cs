using System;
using NUnit.Framework;
using HexMap.Core;
using HexMap.Runtime;

namespace HexMap.Runtime.Tests
{
    [TestFixture]
    public sealed class HexMapTests
    {
        [Test]
        public void RadiusOneGeneratesSevenCellsInStableAxialOrder()
        {
            var map = new HexMap(new HexMapDefinition(1));

            Assert.That(map.Count, Is.EqualTo(7));
            Assert.That(map.Cells[0].Coordinate, Is.EqualTo(new HexCoord(-1, 0)));
            Assert.That(map.Cells[3].Coordinate, Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(map.Cells[6].Coordinate, Is.EqualTo(new HexCoord(1, 0)));
        }

        [Test]
        public void RadiusTwoGeneratesTheStandardNineteenCellHexagon()
        {
            var map = new HexMap(new HexMapDefinition(2));

            Assert.That(map.Count, Is.EqualTo(19));
            foreach (var cell in map.Cells)
            {
                Assert.That(map.Radius.Contains(cell.Coordinate), Is.True);
                Assert.That(HexCoord.Distance(new HexCoord(0, 0), cell.Coordinate), Is.LessThanOrEqualTo(2));
            }
        }

        [Test]
        public void CellIdsStartAtTheCenterAndExpandByDistance()
        {
            var map = new HexMap(new HexMapDefinition(1));

            Assert.That(map.Query(new HexCoord(0, 0)).Cell.Id, Is.EqualTo(0));
            Assert.That(map.Query(new HexCoord(-1, 0)).Cell.Id, Is.EqualTo(1));
            Assert.That(map.Query(new HexCoord(-1, 1)).Cell.Id, Is.EqualTo(2));
            Assert.That(map.Query(new HexCoord(0, -1)).Cell.Id, Is.EqualTo(3));
            Assert.That(map.Query(new HexCoord(0, 1)).Cell.Id, Is.EqualTo(4));
            Assert.That(map.Query(new HexCoord(1, -1)).Cell.Id, Is.EqualTo(5));
            Assert.That(map.Query(new HexCoord(1, 0)).Cell.Id, Is.EqualTo(6));
        }

        [Test]
        public void CellIdsCanBeQueriedBackToTheirCoordinates()
        {
            var map = new HexMap(new HexMapDefinition(2));

            HexCell cell;
            Assert.That(map.TryGetCell(0, out cell), Is.True);
            Assert.That(cell.Coordinate, Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(map.Query(cell.Id).Cell.Coordinate, Is.EqualTo(cell.Coordinate));
            Assert.That(map.TryGetCell(-1, out cell), Is.False);
            Assert.That(map.Query(1000).Status, Is.EqualTo(HexCellQueryStatus.Missing));
        }

        [Test]
        public void ExpandingTheRadiusPreservesExistingCellIds()
        {
            var smallerMap = new HexMap(new HexMapDefinition(1));
            var largerMap = new HexMap(new HexMapDefinition(2));

            foreach (var cell in smallerMap.Cells)
            {
                Assert.That(largerMap.Query(cell.Coordinate).Cell.Id, Is.EqualTo(cell.Id));
            }
        }

        [Test]
        public void QueryDistinguishesFoundAndOutsideMap()
        {
            var map = new HexMap(new HexMapDefinition(1));

            Assert.That(map.Query(new HexCoord(0, 0)).Status, Is.EqualTo(HexCellQueryStatus.Found));
            Assert.That(map.Query(new HexCoord(1, 0)).Status, Is.EqualTo(HexCellQueryStatus.Found));
            Assert.That(map.Query(new HexCoord(2, 0)).Status, Is.EqualTo(HexCellQueryStatus.OutsideMap));
        }

        [Test]
        public void QueryReturnsTheCoordinateOwnedByTheFoundCell()
        {
            var coordinate = new HexCoord(-2, 3);
            var map = new HexMap(new HexMapDefinition(4));

            var result = map.Query(coordinate);

            Assert.That(result.HasCell, Is.True);
            Assert.That(result.Cell.Coordinate, Is.EqualTo(coordinate));
        }

        [Test]
        public void InvalidDefinitionsAreRejectedBeforeMapGeneration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HexMapDefinition(-1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexMapDefinition(HexMapRadius.MaxSupportedRadius + 1));
        }

        [Test]
        public void SimplifiedCellIdFormulaMatchesHexMapAtRadiusEleven()
        {
            const int radius = 11;
            var map = new HexMap(new HexMapDefinition(radius));

            foreach (var cell in map.Cells)
            {
                var expected = ComputeCellId(cell.Coordinate);
                Assert.That(cell.Id, Is.EqualTo(expected),
                    "CellId mismatch for coordinate ({0},{1})",
                    cell.Coordinate.Q, cell.Coordinate.R);
            }
        }

        private static int ComputeCellId(HexCoord coordinate)
        {
            var distance = HexCoord.Distance(new HexCoord(0, 0), coordinate);

            if (distance == 0)
            {
                return 0;
            }

            var firstIdInRing = 1 + 3 * distance * (distance - 1);
            return firstIdInRing + ComputeRingOffset(coordinate, distance);
        }

        private static int ComputeRingOffset(HexCoord coordinate, int distance)
        {
            if (coordinate.Q == -distance)
            {
                return coordinate.R;
            }

            if (coordinate.Q == distance)
            {
                return 5 * distance - 1 + coordinate.R + distance;
            }

            var offset = distance + 1 + 2 * (coordinate.Q + distance - 1);
            if (coordinate.R > 0)
            {
                offset++;
            }

            return offset;
        }
    }
}

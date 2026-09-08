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
            var map = new HexMap(new HexMapDefinition(1, Array.Empty<HexCoord>()));

            Assert.That(map.Count, Is.EqualTo(7));
            Assert.That(map.Cells[0].Coordinate, Is.EqualTo(new HexCoord(-1, 0)));
            Assert.That(map.Cells[3].Coordinate, Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(map.Cells[6].Coordinate, Is.EqualTo(new HexCoord(1, 0)));
        }

        [Test]
        public void RadiusTwoGeneratesTheStandardNineteenCellHexagon()
        {
            var map = new HexMap(new HexMapDefinition(2, Array.Empty<HexCoord>()));

            Assert.That(map.Count, Is.EqualTo(19));
            foreach (var cell in map.Cells)
            {
                Assert.That(map.Radius.Contains(cell.Coordinate), Is.True);
                Assert.That(HexCoord.Distance(new HexCoord(0, 0), cell.Coordinate), Is.LessThanOrEqualTo(2));
            }
        }

        [Test]
        public void QueryDistinguishesFoundMissingAndOutsideMap()
        {
            var map = new HexMap(new HexMapDefinition(
                1,
                new[] { new HexCoord(1, 0) }));

            Assert.That(map.Query(new HexCoord(0, 0)).Status, Is.EqualTo(HexCellQueryStatus.Found));
            Assert.That(map.Query(new HexCoord(1, 0)).Status, Is.EqualTo(HexCellQueryStatus.Missing));
            Assert.That(map.Query(new HexCoord(2, 0)).Status, Is.EqualTo(HexCellQueryStatus.OutsideMap));
        }

        [Test]
        public void QueryReturnsTheCoordinateOwnedByTheFoundCell()
        {
            var coordinate = new HexCoord(-2, 3);
            var map = new HexMap(new HexMapDefinition(
                4,
                Array.Empty<HexCoord>()));

            var result = map.Query(coordinate);

            Assert.That(result.HasCell, Is.True);
            Assert.That(result.Cell.Coordinate, Is.EqualTo(coordinate));
        }

        [Test]
        public void InvalidDefinitionsAreRejectedBeforeMapGeneration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HexMapRadius(-1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexMapRadius(HexMapRadius.MaxSupportedRadius + 1));
            Assert.Throws<ArgumentException>(() => new HexMapDefinition(
                1,
                new[] { new HexCoord(0, 0), new HexCoord(0, 0) }));
            Assert.Throws<ArgumentException>(() => new HexMapDefinition(
                1,
                new[] { new HexCoord(2, 0) }));
        }
    }
}
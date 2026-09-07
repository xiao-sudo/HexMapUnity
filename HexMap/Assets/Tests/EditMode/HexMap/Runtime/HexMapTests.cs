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
        public void GeneratesCellsInStableAxialOrder()
        {
            var definition = new HexMapDefinition(
                new HexMapBounds(0, 1, 0, 1),
                new[] { new HexCoord(1, 1) });

            var map = new HexMap(definition);

            Assert.That(map.Count, Is.EqualTo(3));
            Assert.That(map.Cells[0].Coordinate, Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(map.Cells[1].Coordinate, Is.EqualTo(new HexCoord(0, 1)));
            Assert.That(map.Cells[2].Coordinate, Is.EqualTo(new HexCoord(1, 0)));
        }

        [Test]
        public void QueryDistinguishesFoundMissingAndOutsideMap()
        {
            var map = new HexMap(new HexMapDefinition(
                new HexMapBounds(0, 1, 0, 1),
                new[] { new HexCoord(1, 1) }));

            Assert.That(map.Query(new HexCoord(0, 0)).Status, Is.EqualTo(HexCellQueryStatus.Found));
            Assert.That(map.Query(new HexCoord(1, 1)).Status, Is.EqualTo(HexCellQueryStatus.Missing));
            Assert.That(map.Query(new HexCoord(2, 0)).Status, Is.EqualTo(HexCellQueryStatus.OutsideMap));
        }

        [Test]
        public void QueryReturnsTheCoordinateOwnedByTheFoundCell()
        {
            var coordinate = new HexCoord(-2, 3);
            var map = new HexMap(new HexMapDefinition(
                new HexMapBounds(-2, -2, 3, 3),
                Array.Empty<HexCoord>()));

            var result = map.Query(coordinate);

            Assert.That(result.HasCell, Is.True);
            Assert.That(result.Cell.Coordinate, Is.EqualTo(coordinate));
        }

        [Test]
        public void InvalidDefinitionsAreRejectedBeforeMapGeneration()
        {
            Assert.Throws<ArgumentException>(() => new HexMapBounds(1, 0, 0, 1));
            Assert.Throws<ArgumentException>(() => new HexMapDefinition(
                new HexMapBounds(0, 1, 0, 1),
                new[] { new HexCoord(0, 0), new HexCoord(0, 0) }));
            Assert.Throws<ArgumentException>(() => new HexMapDefinition(
                new HexMapBounds(0, 1, 0, 1),
                new[] { new HexCoord(2, 0) }));
        }
    }
}
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HexMap.Core.Tests
{
    [TestFixture]
    public sealed class HexCoordTests
    {
        [Test]
        public void CubeProjectionPreservesInvariant()
        {
            var coordinate = new HexCoord(-4, 7);

            Assert.That(coordinate.S, Is.EqualTo(-3));
            Assert.That(coordinate.Q + coordinate.R + coordinate.S, Is.EqualTo(0));
            Assert.That(coordinate.Cube, Is.EqualTo(new HexCubeCoord(-4, 7, -3)));
        }

        [Test]
        public void EqualCoordinatesHaveEqualHashCodesAndWorkAsDictionaryKeys()
        {
            var first = new HexCoord(2, -3);
            var equivalent = new HexCoord(2, -3);
            var different = new HexCoord(2, -2);
            var cells = new Dictionary<HexCoord, string>
            {
                { first, "center" }
            };

            Assert.That(equivalent, Is.EqualTo(first));
            Assert.That(equivalent.GetHashCode(), Is.EqualTo(first.GetHashCode()));
            Assert.That(different, Is.Not.EqualTo(first));
            Assert.That(cells[equivalent], Is.EqualTo("center"));
        }

        [Test]
        public void SixNeighborDirectionsUseTheCanonicalAxialOrder()
        {
            var center = new HexCoord(0, 0);
            var expected = new[]
            {
                new HexCoord(1, 0),
                new HexCoord(1, -1),
                new HexCoord(0, -1),
                new HexCoord(-1, 0),
                new HexCoord(-1, 1),
                new HexCoord(0, 1)
            };

            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(center.GetNeighbor((HexDirection)index), Is.EqualTo(expected[index]));
            }
        }

        [Test]
        public void NeighborDistanceIsOneAndNeighborRelationshipIsReversible()
        {
            var center = new HexCoord(4, -2);

            foreach (HexDirection direction in Enum.GetValues(typeof(HexDirection)))
            {
                var neighbor = center.GetNeighbor(direction);

                Assert.That(HexCoord.Distance(center, neighbor), Is.EqualTo(1));
                Assert.That(neighbor.GetNeighbor(direction.Opposite()), Is.EqualTo(center));
            }
        }

        [Test]
        public void DistanceUsesCubeCoordinates()
        {
            Assert.That(HexCoord.Distance(new HexCoord(0, 0), new HexCoord(2, -1)), Is.EqualTo(2));
            Assert.That(HexCoord.Distance(new HexCoord(-3, 4), new HexCoord(1, -2)), Is.EqualTo(6));
            Assert.That(HexCoord.Distance(new HexCoord(5, -8), new HexCoord(5, -8)), Is.EqualTo(0));
        }

        [Test]
        public void CoordinatesRejectAnUnrepresentableDerivedCubeComponent()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexCoord(int.MaxValue, int.MaxValue));
            Assert.Throws<ArgumentException>(
                () => new HexCubeCoord(int.MaxValue, int.MaxValue, 2));
        }
    }
}
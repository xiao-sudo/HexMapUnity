using System;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.Core.Tests
{
    [TestFixture]
    public sealed class HexLayoutTests
    {
        [Test]
        public void HexToWorldUsesTheConfiguredOrientationAndPlane()
        {
            var pointyXz = new HexLayout(HexOrientation.Pointy, HexPlane.XZ, 1f, Vector3.zero);
            var flatXy = new HexLayout(HexOrientation.Flat, HexPlane.XY, 1f, Vector3.zero);

            var pointyWorld = pointyXz.HexToWorld(new HexCoord(1, 0));
            var flatWorld = flatXy.HexToWorld(new HexCoord(0, 1));

            Assert.That(pointyWorld.x, Is.EqualTo(Mathf.Sqrt(3f)).Within(0.00001f));
            Assert.That(pointyWorld.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(pointyWorld.z, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(flatWorld.x, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(flatWorld.y, Is.EqualTo(Mathf.Sqrt(3f)).Within(0.00001f));
            Assert.That(flatWorld.z, Is.EqualTo(0f).Within(0.00001f));
        }

        [Test]
        public void AllOrientationsAndPlanesRoundTripHexCenters()
        {
            var coordinates = new[]
            {
                new HexCoord(0, 0),
                new HexCoord(1, 0),
                new HexCoord(1, -1),
                new HexCoord(-2, 3),
                new HexCoord(4, -5),
                new HexCoord(-3, -2)
            };

            foreach (HexOrientation orientation in System.Enum.GetValues(typeof(HexOrientation)))
            {
                foreach (HexPlane plane in System.Enum.GetValues(typeof(HexPlane)))
                {
                    var layout = new HexLayout(
                        orientation,
                        plane,
                        2.75f,
                        new Vector3(10f, 20f, 30f));

                    foreach (var coordinate in coordinates)
                    {
                        var world = layout.HexToWorld(coordinate);
                        var actual = layout.WorldToHex(world);

                        Assert.That(
                            actual,
                            Is.EqualTo(coordinate),
                            string.Format("{0}/{1} failed for {2}.", orientation, plane, coordinate));
                    }
                }
            }
        }

        [Test]
        public void AllOrientationsAndPlanesRoundAdjacentBoundary()
        {
            foreach (HexOrientation orientation in System.Enum.GetValues(typeof(HexOrientation)))
            {
                foreach (HexPlane plane in System.Enum.GetValues(typeof(HexPlane)))
                {
                    var layout = new HexLayout(orientation, plane, 1f, Vector3.zero);
                    var center = layout.HexToWorld(new HexCoord(0, 0));
                    var east = layout.HexToWorld(new HexCoord(1, 0));
                    var midpoint = (center + east) * 0.5f;

                    Assert.That(
                        layout.WorldToHex(midpoint + Vector3.left * 0.001f),
                        Is.EqualTo(new HexCoord(0, 0)),
                        string.Format("{0}/{1} left boundary", orientation, plane));
                    Assert.That(
                        layout.WorldToHex(midpoint),
                        Is.EqualTo(new HexCoord(1, 0)),
                        string.Format("{0}/{1} boundary tie break", orientation, plane));
                    Assert.That(
                        layout.WorldToHex(midpoint + Vector3.right * 0.001f),
                        Is.EqualTo(new HexCoord(1, 0)),
                        string.Format("{0}/{1} right boundary", orientation, plane));
                }
            }
        }

        [Test]
        public void LayoutOriginAndRadiusDoNotChangeTheRoundTripCoordinate()
        {
            var layout = new HexLayout(
                HexOrientation.Flat,
                HexPlane.XY,
                0.375f,
                new Vector3(-12f, 8f, 99f));

            var coordinate = new HexCoord(-7, 4);

            Assert.That(layout.WorldToHex(layout.HexToWorld(coordinate)), Is.EqualTo(coordinate));
        }

        [Test]
        public void SecondaryScaleChangesTheSecondaryAxisForEveryOrientationAndPlane()
        {
            const float scale = 0.75f;
            const float radius = 2f;

            foreach (HexOrientation orientation in Enum.GetValues(typeof(HexOrientation)))
            {
                foreach (HexPlane plane in Enum.GetValues(typeof(HexPlane)))
                {
                    var layout = new HexLayout(orientation, plane, radius, Vector3.zero, scale);
                    var world = layout.HexToWorld(new HexCoord(0, 1));
                    var expectedSecondary = orientation == HexOrientation.Pointy
                        ? radius * 1.5f * scale
                        : radius * Mathf.Sqrt(3f) * scale;
                    var actualSecondary = plane == HexPlane.XY ? world.y : world.z;

                    Assert.That(actualSecondary, Is.EqualTo(expectedSecondary).Within(0.00001f),
                        string.Format("{0}/{1} secondary axis", orientation, plane));
                    Assert.That(plane == HexPlane.XY ? world.z : world.y, Is.EqualTo(0f).Within(0.00001f));
                }
            }
        }

        [Test]
        public void ScaledLayoutsRoundTripHexCentersForEveryOrientationAndPlane()
        {
            var coordinates = new[]
            {
                new HexCoord(0, 0),
                new HexCoord(1, 0),
                new HexCoord(1, -1),
                new HexCoord(-2, 3),
                new HexCoord(4, -5),
                new HexCoord(-3, -2)
            };

            foreach (HexOrientation orientation in Enum.GetValues(typeof(HexOrientation)))
            {
                foreach (HexPlane plane in Enum.GetValues(typeof(HexPlane)))
                {
                    var layout = new HexLayout(
                        orientation,
                        plane,
                        2.75f,
                        new Vector3(10f, 20f, 30f),
                        0.8f
                        );

                    foreach (var coordinate in coordinates)
                    {
                        Assert.That(
                            layout.WorldToHex(layout.HexToWorld(coordinate)),
                            Is.EqualTo(coordinate),
                            string.Format("{0}/{1} failed for {2}.", orientation, plane, coordinate));
                    }
                }
            }
        }

        [Test]
        public void NonPositiveOrNonFiniteSecondaryScaleIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexLayout(HexOrientation.Pointy, HexPlane.XY, 1f, Vector3.zero, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexLayout(HexOrientation.Pointy, HexPlane.XY, 1f, Vector3.zero, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexLayout(HexOrientation.Pointy, HexPlane.XY, 1f, Vector3.zero, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexLayout(HexOrientation.Pointy, HexPlane.XY, 1f, Vector3.zero, float.PositiveInfinity));
        }

        [Test]
        public void NonPositiveOrNonFiniteRadiusIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexLayout(HexOrientation.Pointy, HexPlane.XY, 0f, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexLayout(HexOrientation.Pointy, HexPlane.XY, -1f, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexLayout(HexOrientation.Pointy, HexPlane.XY, float.NaN, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HexLayout(HexOrientation.Pointy, HexPlane.XY, float.PositiveInfinity, Vector3.zero));
        }

        [Test]
        public void DefaultLayoutIsRejectedWhenUsed()
        {
            var layout = default(HexLayout);

            Assert.Throws<InvalidOperationException>(
                () => layout.HexToWorld(new HexCoord(0, 0)));
            Assert.Throws<InvalidOperationException>(
                () => layout.WorldToHex(Vector3.zero));
        }
    }
}
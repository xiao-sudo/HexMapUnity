using System;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.Core.Tests
{
    [TestFixture]
    public sealed class OrthographicMapFramingTests
    {
        private const int ProductionRadius = 11;
        private const float ProductionOuterRadius = 1f;
        private const float ProductionSecondaryScale = 0.9f;
        private const float ProductionViewMargin = 1.1f;
        private const float LandscapeAspect = 16f / 9f;

        private static HexLayout Layout(HexOrientation orientation, float secondaryScale, HexPlane plane)
        {
            return new HexLayout(orientation, plane, ProductionOuterRadius, Vector3.zero, secondaryScale);
        }

        private static HexLayout ProductionLayout(HexOrientation orientation)
        {
            return Layout(orientation, ProductionSecondaryScale, HexPlane.XZ);
        }

        // The envelope is the center lattice spread plus one cell half profile. The mesh applies
        // the secondary scale to the in-plane component, so the two half extents differ.
        private static float CenterAxisExtent(HexOrientation orientation, float secondaryScale)
        {
            return orientation == HexOrientation.Pointy
                ? Mathf.Sqrt(3f) * ProductionRadius * ProductionOuterRadius
                : 1.5f * ProductionRadius * ProductionOuterRadius * secondaryScale;
        }

        private static float CenterSecondaryExtent(HexOrientation orientation, float secondaryScale)
        {
            return orientation == HexOrientation.Pointy
                ? 1.5f * ProductionRadius * ProductionOuterRadius * secondaryScale
                : Mathf.Sqrt(3f) * ProductionRadius * ProductionOuterRadius * secondaryScale;
        }

        private static float CellAxisExtent(HexOrientation orientation)
        {
            return orientation == HexOrientation.Pointy
                ? ProductionOuterRadius * Mathf.Sqrt(3f) * 0.5f
                : ProductionOuterRadius;
        }

        private static float CellSecondaryExtent(HexOrientation orientation, float secondaryScale)
        {
            return orientation == HexOrientation.Pointy
                ? ProductionOuterRadius * secondaryScale
                : ProductionOuterRadius * Mathf.Sqrt(3f) * 0.5f * secondaryScale;
        }

        [Test]
        public void PointyEnvelopeIncludesTheOutermostCellVertices()
        {
            var framing = OrthographicMapFraming.Create(
                ProductionLayout(HexOrientation.Pointy),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            // The center lattice alone would give 19.05256 / 14.85; the outermost cells' tips extend past it.
            Assert.That(framing.MapHalfWidth, Is.EqualTo(19.91858f).Within(0.001f));
            Assert.That(framing.MapHalfDepth, Is.EqualTo(15.75f).Within(0.00001f));
            Assert.That(framing.MapWidth, Is.EqualTo(framing.MapHalfWidth * 2f).Within(0.00001f));
            Assert.That(framing.MapDepth, Is.EqualTo(31.5f).Within(0.00001f));
            Assert.That(
                framing.MapHalfWidth,
                Is.EqualTo(CenterAxisExtent(HexOrientation.Pointy, ProductionSecondaryScale) + CellAxisExtent(HexOrientation.Pointy)).Within(0.00001f));
            Assert.That(
                framing.MapHalfDepth,
                Is.EqualTo(CenterSecondaryExtent(HexOrientation.Pointy, ProductionSecondaryScale) + CellSecondaryExtent(HexOrientation.Pointy, ProductionSecondaryScale)).Within(0.00001f));
        }

        [Test]
        public void FlatEnvelopeIncludesTheOutermostCellVertices()
        {
            var framing = OrthographicMapFraming.Create(
                ProductionLayout(HexOrientation.Flat),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            Assert.That(framing.MapHalfWidth, Is.EqualTo(15.85f).Within(0.00001f));
            Assert.That(framing.MapHalfDepth, Is.EqualTo(17.92673f).Within(0.001f));
            Assert.That(
                framing.MapHalfWidth,
                Is.EqualTo(CenterAxisExtent(HexOrientation.Flat, ProductionSecondaryScale) + CellAxisExtent(HexOrientation.Flat)).Within(0.00001f));
            Assert.That(
                framing.MapHalfDepth,
                Is.EqualTo(CenterSecondaryExtent(HexOrientation.Flat, ProductionSecondaryScale) + CellSecondaryExtent(HexOrientation.Flat, ProductionSecondaryScale)).Within(0.00001f));
        }

        [Test]
        public void PlaneDoesNotChangeTheEnvelope()
        {
            var onXz = OrthographicMapFraming.Create(
                Layout(HexOrientation.Pointy, ProductionSecondaryScale, HexPlane.XZ),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);
            var onXy = OrthographicMapFraming.Create(
                Layout(HexOrientation.Pointy, ProductionSecondaryScale, HexPlane.XY),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            Assert.That(onXy.MapHalfWidth, Is.EqualTo(onXz.MapHalfWidth).Within(0.00001f));
            Assert.That(onXy.MapHalfDepth, Is.EqualTo(onXz.MapHalfDepth).Within(0.00001f));
            Assert.That(onXy.OrthographicSize, Is.EqualTo(onXz.OrthographicSize).Within(0.00001f));
            Assert.That(onXy.MaxOffset, Is.EqualTo(onXz.MaxOffset).Within(0.00001f));
        }

        [Test]
        public void OrthographicSizeIsTheHalfHeightThatFitsEveryRow()
        {
            var framing = OrthographicMapFraming.Create(
                ProductionLayout(HexOrientation.Pointy),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            // Camera.orthographicSize is a half height, so it must be at least the map half depth.
            Assert.That(framing.OrthographicSize, Is.EqualTo(framing.MapHalfDepth * ProductionViewMargin).Within(0.00001f));
            Assert.That(framing.OrthographicSize, Is.EqualTo(17.325f).Within(0.001f));
            Assert.That(framing.VisibleHeight, Is.EqualTo(framing.OrthographicSize * 2f).Within(0.00001f));
            Assert.That(framing.VisibleHeight, Is.EqualTo(34.65f).Within(0.001f));
            Assert.That(framing.VisibleHeight, Is.GreaterThanOrEqualTo(framing.MapDepth));
        }

        [Test]
        public void ProductionPointyMapIn16By9FramesEveryRowButCannotPan()
        {
            var framing = OrthographicMapFraming.Create(
                ProductionLayout(HexOrientation.Pointy),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            // Seeing every row means the frame is at least as wide as this map in any 16:9 viewport.
            Assert.That(framing.VisibleWidth, Is.EqualTo(61.6f).Within(0.01f));
            Assert.That(framing.MapWidth, Is.EqualTo(39.83716f).Within(0.001f));
            Assert.That(framing.ShowsEveryColumn, Is.True);
            Assert.That(framing.IsLockedToCenter, Is.True);
            Assert.That(framing.MinOffset, Is.EqualTo(0f));
            Assert.That(framing.MaxOffset, Is.EqualTo(0f));
        }

        [Test]
        public void FlatteningTheMapBelowTheThresholdRestoresPanningWithoutLosingRows()
        {
            // Panning needs mapHalfWidth - visibleHeight * aspect / 2 > 0, i.e.
            // (sqrt(3) * R + sqrt(3) / 2) / (2.25 * R + 2) * 2 / (aspect * margin) > secondaryScale.
            // At 16:9 with margin 1.1 that threshold is about 0.5661.
            var aboveThreshold = OrthographicMapFraming.Create(
                Layout(HexOrientation.Pointy, 0.6f, HexPlane.XZ),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);
            var belowThreshold = OrthographicMapFraming.Create(
                Layout(HexOrientation.Pointy, 0.5f, HexPlane.XZ),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            Assert.That(aboveThreshold.IsLockedToCenter, Is.True);
            Assert.That(belowThreshold.IsLockedToCenter, Is.False);
            Assert.That(belowThreshold.MaxOffset, Is.EqualTo(1.8076f).Within(0.001f));
            Assert.That(belowThreshold.VisibleHeight, Is.GreaterThanOrEqualTo(belowThreshold.MapDepth));
        }

        [Test]
        public void NarrowerViewportsAlwaysKeepEveryRowAndPanFurther()
        {
            // At the production scale this map is too deep to pan at 16:9 or 4:3, so use a scale
            // that is wide enough for all three viewports to pan.
            var layout = Layout(HexOrientation.Pointy, 0.5f, HexPlane.XZ);
            var tall = OrthographicMapFraming.Create(layout, ProductionRadius, ProductionViewMargin, 1f);
            var square = OrthographicMapFraming.Create(layout, ProductionRadius, ProductionViewMargin, 4f / 3f);
            var landscape = OrthographicMapFraming.Create(layout, ProductionRadius, ProductionViewMargin, LandscapeAspect);

            Assert.That(tall.VisibleHeight, Is.EqualTo(square.VisibleHeight).Within(0.00001f));
            Assert.That(square.VisibleHeight, Is.EqualTo(landscape.VisibleHeight).Within(0.00001f));
            Assert.That(tall.MaxOffset, Is.GreaterThan(square.MaxOffset));
            Assert.That(square.MaxOffset, Is.GreaterThan(landscape.MaxOffset));
            Assert.That(landscape.MaxOffset, Is.GreaterThan(0f));
        }

        [Test]
        public void EveryRowStaysInsideTheFrameForEveryOrientationAndPlane()
        {
            foreach (HexOrientation orientation in Enum.GetValues(typeof(HexOrientation)))
            {
                foreach (HexPlane plane in Enum.GetValues(typeof(HexPlane)))
                {
                    foreach (var secondaryScale in new[] { 0.2f, 0.35f, 0.5f, 0.9f, 1f, 1.5f })
                    {
                        foreach (var aspect in new[] { 0.5625f, 1f, 4f / 3f, LandscapeAspect, 2.4f })
                        {
                            foreach (var margin in new[] { 1f, 1.1f, 3f })
                            {
                                var layout = Layout(orientation, secondaryScale, plane);
                                var framing = OrthographicMapFraming.Create(layout, 7, margin, aspect);

                                Assert.That(
                                    framing.VisibleHeight,
                                    Is.GreaterThanOrEqualTo(framing.MapDepth),
                                    string.Format(
                                        "{0}/{1} s={2} aspect={3} margin={4}: rows would be clipped.",
                                        orientation,
                                        plane,
                                        secondaryScale,
                                        aspect,
                                        margin));
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void VisibleWidthIsAlwaysTheVisibleHeightTimesTheAspect()
        {
            foreach (var aspect in new[] { 0.5f, 1f, LandscapeAspect, 3.5f })
            {
                foreach (HexOrientation orientation in Enum.GetValues(typeof(HexOrientation)))
                {
                    var framing = OrthographicMapFraming.Create(
                        ProductionLayout(orientation),
                        ProductionRadius,
                        1.4f,
                        aspect);

                    Assert.That(
                        framing.VisibleWidth,
                        Is.EqualTo(framing.VisibleHeight * aspect).Within(0.00001f),
                        string.Format("{0} at aspect {1}", orientation, aspect));
                }
            }
        }

        [Test]
        public void FlatAndPointyEnvelopesAreNotSwappedBecauseCellProfilesDiffer()
        {
            var pointy = OrthographicMapFraming.Create(
                ProductionLayout(HexOrientation.Pointy),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);
            var flat = OrthographicMapFraming.Create(
                ProductionLayout(HexOrientation.Flat),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            // The center lattices swap, but each orientation adds its own cell profile, so the
            // envelopes are not a clean swap of one another.
            Assert.That(
                flat.MapHalfWidth,
                Is.EqualTo(
                    CenterSecondaryExtent(HexOrientation.Pointy, ProductionSecondaryScale)
                    + CellAxisExtent(HexOrientation.Flat)).Within(0.00001f));
            Assert.That(flat.MapHalfWidth, Is.LessThan(pointy.MapHalfWidth));
            Assert.That(flat.MapHalfDepth, Is.GreaterThan(pointy.MapHalfDepth));
            Assert.That(flat.MapHalfWidth, Is.Not.EqualTo(pointy.MapHalfDepth).Within(0.01f));
            Assert.That(flat.MapHalfDepth, Is.Not.EqualTo(pointy.MapHalfWidth).Within(0.01f));
        }

        [Test]
        public void ClampOffsetKeepsOffsetsInsideTheRange()
        {
            var framing = OrthographicMapFraming.Create(
                Layout(HexOrientation.Pointy, 0.5f, HexPlane.XZ),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            Assert.That(framing.ClampOffset(0f), Is.EqualTo(0f));
            Assert.That(framing.ClampOffset(framing.MaxOffset * 0.5f), Is.EqualTo(framing.MaxOffset * 0.5f).Within(0.00001f));
            Assert.That(framing.ClampOffset(1000f), Is.EqualTo(framing.MaxOffset).Within(0.00001f));
            Assert.That(framing.ClampOffset(-1000f), Is.EqualTo(framing.MinOffset).Within(0.00001f));
            Assert.That(framing.ClampOffset(float.PositiveInfinity), Is.EqualTo(0f));
            Assert.That(framing.ClampOffset(float.NaN), Is.EqualTo(0f));
        }

        [Test]
        public void TrySetOffsetRejectsNonFiniteOffsetsAndReportsClamping()
        {
            var framing = OrthographicMapFraming.Create(
                Layout(HexOrientation.Pointy, 0.5f, HexPlane.XZ),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            float clamped;
            string error;

            Assert.That(framing.TrySetOffset(0.5f, out clamped, out error), Is.True);
            Assert.That(clamped, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(error, Is.Empty);

            Assert.That(framing.TrySetOffset(1000f, out clamped, out error), Is.True);
            Assert.That(clamped, Is.EqualTo(framing.MaxOffset).Within(0.00001f));
            Assert.That(error, Is.Empty);

            Assert.That(framing.TrySetOffset(float.NaN, out clamped, out error), Is.False);
            Assert.That(clamped, Is.EqualTo(0f));
            Assert.That(error, Is.Not.Empty);

            Assert.That(framing.TrySetOffset(float.NegativeInfinity, out clamped, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void LockedCameraStillAcceptsOffsetsWithoutMoving()
        {
            var locked = ProductionLayout(HexOrientation.Pointy);
            var framing = OrthographicMapFraming.Create(
                locked,
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            float clamped;
            string error;

            Assert.That(framing.IsLockedToCenter, Is.True);
            Assert.That(framing.TrySetOffset(12f, out clamped, out error), Is.True);
            Assert.That(clamped, Is.EqualTo(0f));
            Assert.That(framing.ClampOffset(-12f), Is.EqualTo(0f));
            Assert.That(framing.NormalizeOffset(12f), Is.EqualTo(0.5f).Within(0.00001f));
        }

        [Test]
        public void NormalizeOffsetMapsTheRangeToZeroThroughOne()
        {
            var framing = OrthographicMapFraming.Create(
                Layout(HexOrientation.Pointy, 0.5f, HexPlane.XZ),
                ProductionRadius,
                ProductionViewMargin,
                LandscapeAspect);

            Assert.That(framing.NormalizeOffset(framing.MinOffset), Is.EqualTo(0f).Within(0.00001f));
            Assert.That(framing.NormalizeOffset(0f), Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(framing.NormalizeOffset(framing.MaxOffset), Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void NonPositiveRadiusIsRejected()
        {
            OrthographicMapFraming framing;
            string error;

            Assert.That(
                OrthographicMapFraming.TryCreate(
                    ProductionLayout(HexOrientation.Pointy),
                    0,
                    ProductionViewMargin,
                    LandscapeAspect,
                    out framing,
                    out error),
                Is.False);
            Assert.That(error, Is.Not.Empty);

            Assert.That(
                OrthographicMapFraming.TryCreate(
                    ProductionLayout(HexOrientation.Pointy),
                    -3,
                    ProductionViewMargin,
                    LandscapeAspect,
                    out framing,
                    out error),
                Is.False);
        }

        [Test]
        public void NonPositiveOrNonFiniteAspectIsRejected()
        {
            OrthographicMapFraming framing;
            string error;

            foreach (var aspect in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.That(
                    OrthographicMapFraming.TryCreate(
                        ProductionLayout(HexOrientation.Pointy),
                        ProductionRadius,
                        ProductionViewMargin,
                        aspect,
                        out framing,
                        out error),
                    Is.False,
                    string.Format("aspect {0} should be rejected.", aspect));
                Assert.That(error, Is.Not.Empty);
            }
        }

        [Test]
        public void ViewMarginBelowOneIsRejectedBecauseRowsWouldBeClipped()
        {
            OrthographicMapFraming framing;
            string error;

            foreach (var margin in new[] { 0f, 0.5f, 0.999f, float.NaN, float.PositiveInfinity })
            {
                Assert.That(
                    OrthographicMapFraming.TryCreate(
                        ProductionLayout(HexOrientation.Pointy),
                        ProductionRadius,
                        margin,
                        LandscapeAspect,
                        out framing,
                        out error),
                    Is.False,
                    string.Format("margin {0} should be rejected.", margin));
                Assert.That(error, Is.Not.Empty);
            }
        }

        [Test]
        public void UninitializedLayoutIsRejectedWithAReason()
        {
            OrthographicMapFraming framing;
            string error;

            Assert.That(
                OrthographicMapFraming.TryCreate(
                    default(HexLayout),
                    ProductionRadius,
                    ProductionViewMargin,
                    LandscapeAspect,
                    out framing,
                    out error),
                Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void CreateThrowsWhenTheInputsCannotDescribeAFrame()
        {
            Assert.Throws<ArgumentException>(
                () => OrthographicMapFraming.Create(
                    ProductionLayout(HexOrientation.Pointy),
                    0,
                    ProductionViewMargin,
                    LandscapeAspect));
        }
    }
}

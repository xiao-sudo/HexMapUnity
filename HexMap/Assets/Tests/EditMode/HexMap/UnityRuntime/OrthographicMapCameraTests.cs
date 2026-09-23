using HexMap.Core;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.UnityRuntime.Tests
{
    [TestFixture]
    public sealed class OrthographicMapCameraTests
    {
        private const float PortraitAspect = 9f / 16f;
        private const float LandscapeAspect = 16f / 9f;
        private const float DefaultViewMargin = 1.1f;

        private static readonly Vector3 DownwardForward = new Vector3(0f, -1f, 0f);
        private static readonly Vector3 ScreenUp = new Vector3(0f, 0f, 1f);
        private static readonly Vector3 ScreenRight = new Vector3(1f, 0f, 0f);

        private GameObject m_MapObject;
        private GameObject m_CameraObject;
        private GameObject m_SettingsObject;

        [TearDown]
        public void TearDown()
        {
            if (m_SettingsObject != null)
            {
                Object.DestroyImmediate(m_SettingsObject);
            }

            if (m_CameraObject != null)
            {
                Object.DestroyImmediate(m_CameraObject);
            }

            if (m_MapObject != null)
            {
                Object.DestroyImmediate(m_MapObject);
            }

            m_SettingsObject = null;
            m_CameraObject = null;
            m_MapObject = null;
        }

        private HexMapView CreateMap(int radius = 3, float secondaryScale = 1f, HexOrientation orientation = HexOrientation.Pointy)
        {
            m_MapObject = new GameObject("Hex Map View");
            var mapView = m_MapObject.AddComponent<HexMapView>();
            mapView.Radius = radius;
            mapView.Orientation = orientation;
            mapView.SecondaryScale = secondaryScale;
            mapView.Build();
            return mapView;
        }

        private Camera CreateCamera(float aspect)
        {
            m_CameraObject = new GameObject("Map Camera");
            var camera = m_CameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = aspect;
            return camera;
        }

        private OrthographicMapCamera CreateController(HexMapView mapView, Camera camera)
        {
            var controller = m_CameraObject.AddComponent<OrthographicMapCamera>();
            controller.HexMapView = mapView;
            controller.Camera = camera;
            return controller;
        }

        [Test]
        public void RefreshWritesTheWholeCameraNotJustTheFraming()
        {
            var mapView = CreateMap();
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            // radius 3, outer radius 1, secondary scale 1, pointy: half depth = 4.5 + 1
            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);
            Assert.That(framing.MapHalfDepth, Is.EqualTo(5.5f).Within(0.00001f));
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize, Is.EqualTo(framing.OrthographicSize).Within(0.00001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(6.05f).Within(0.00001f));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0f, 30f, 0f)));
            Assert.That(camera.nearClipPlane, Is.EqualTo(28f).Within(0.00001f));
            Assert.That(camera.farClipPlane, Is.EqualTo(32f).Within(0.00001f));

            Assert.That(Vector3.Dot(camera.transform.forward, DownwardForward), Is.EqualTo(1f).Within(0.00001f));
            Assert.That(Vector3.Dot(camera.transform.up, ScreenUp), Is.EqualTo(1f).Within(0.00001f));
            Assert.That(Vector3.Dot(camera.transform.right, ScreenRight), Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void PortraitViewportKeepsEveryRowInsideAndAllowsPanning()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);

            Assert.That(controller.Zoom, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(framing.VisibleHeight, Is.GreaterThanOrEqualTo(framing.MapDepth));
            Assert.That(framing.ShowsEveryColumn, Is.False);
            Assert.That(controller.IsLockedToCenter, Is.False);
            Assert.That(framing.MaxOffset, Is.EqualTo(10.1733f).Within(0.001f));
            Assert.That(controller.MaxOffset.x, Is.EqualTo(10.1733f).Within(0.001f));

            // Zoom 1 leaves no vertical room, so panning starts out purely horizontal.
            Assert.That(controller.MaxOffset.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(controller.MaxOffset.x, Is.EqualTo(10.1733f).Within(0.001f));
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero));

            AssertEveryRowIsInsideTheFrustum(mapView, camera, framing);
        }

        [Test]
        public void ZoomingInOpensVerticalPanningAndWidensBothRanges()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            controller.Zoom = 2f;

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);
            Assert.That(framing.Zoom, Is.EqualTo(2f).Within(0.00001f));
            Assert.That(framing.OrthographicSize, Is.EqualTo(8.6625f).Within(0.001f));
            Assert.That(framing.VisibleWidth, Is.EqualTo(9.74531f).Within(0.001f));
            Assert.That(framing.VisibleHeight, Is.EqualTo(17.325f).Within(0.001f));

            // The shipped framing only guarantees every row at zoom 1; zooming in is allowed to drop them.
            Assert.That(framing.VisibleHeight, Is.LessThan(framing.MapDepth));

            Assert.That(controller.MaxOffset.x, Is.EqualTo(15.04592f).Within(0.001f));
            Assert.That(controller.MaxOffset.y, Is.EqualTo(7.0875f).Within(0.001f));
            Assert.That(controller.MinOffset.x, Is.EqualTo(-15.04592f).Within(0.001f));
            Assert.That(controller.MinOffset.y, Is.EqualTo(-7.0875f).Within(0.001f));
        }

        [Test]
        public void MaxZoomComesFromTheVisibleWidthRatio()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            // visible width / map width == 1 / (zoom * aspect), so the ratio inverts to the zoom limit.
            Assert.That(controller.MaxZoom, Is.EqualTo(1f / (0.15f * PortraitAspect)).Within(0.001f));
            Assert.That(controller.MaxZoom, Is.EqualTo(11.85185f).Within(0.001f));

            controller.TargetZoom = 1000f;
            Assert.That(controller.TargetZoom, Is.EqualTo(controller.MaxZoom).Within(0.00001f));

            controller.MinVisibleWidthRatio = 0.3f;
            Assert.That(controller.MaxZoom, Is.EqualTo(1f / (0.3f * PortraitAspect)).Within(0.001f));
            Assert.That(controller.MaxZoom, Is.LessThan(11.85185f));

            controller.MinVisibleWidthRatio = 0.15f;
            controller.TargetZoom = 0.5f;
            Assert.That(controller.TargetZoom, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void ZoomEasesTowardsTheTargetOverSeveralFrames()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            controller.Zoom = 1f;
            controller.TargetZoom = 3f;

            var steps = 0;
            while (!Mathf.Approximately(controller.Zoom, 3f) && steps < 1000)
            {
                controller.Tick(1f / 60f);
                steps++;
            }

            Assert.That(controller.Zoom, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(steps, Is.GreaterThan(1), "the zoom must not snap in a single step");

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);
            Assert.That(framing.OrthographicSize, Is.EqualTo(17.325f / 3f).Within(0.001f));
        }

        [Test]
        public void FocusOnACenterCellLeavesTheCameraCentered()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;

            Assert.That(controller.FocusOn(new HexCoord(0, 0), out error), Is.True, error);
            Assert.That(controller.HasFocus, Is.True);
            Assert.That(controller.Focus.x, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AnEdgeFocusIsClampedSoItCannotReachTheViewportCenter()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;

            Assert.That(controller.FocusOn(new HexCoord(11, 0), out error), Is.True, error);

            // The cell sits at the map's edge, so its world centre is beyond the panning range.
            Assert.That(controller.Focus.x, Is.EqualTo(19.05256f).Within(0.001f));
            Assert.That(controller.Center.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));
            Assert.That(controller.Center.x, Is.LessThan(controller.Focus.x));

            Assert.That(controller.FocusOn(new HexCoord(-11, 0), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MinOffset.x).Within(0.00001f));

            Assert.That(controller.FocusOn(new HexCoord(0, 11), out error), Is.True, error);
            Assert.That(controller.Center.y, Is.EqualTo(controller.MaxOffset.y).Within(0.00001f));
        }

        [Test]
        public void ZoomOneIsAlwaysCenteredButRemembersTheFocus()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;
            Assert.That(controller.FocusOn(new HexCoord(11, 0), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.GreaterThan(0f));

            controller.Zoom = 1f;
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero), "the widest level is the see-everything state");
            Assert.That(controller.HasFocus, Is.True, "the focus itself is remembered");

            controller.Zoom = 2f;
            Assert.That(controller.Center.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f), "zooming back in re-aims");
        }

        [Test]
        public void AGestureZoomDoesNotPullTheCameraTowardsTheFocus()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;
            Assert.That(controller.TrySetOffset(new Vector2(12f, 5f), out error), Is.True, error);
            Assert.That(controller.Center, Is.EqualTo(new Vector2(12f, 5f)));

            controller.BeginGesture();
            controller.Zoom = 3f;
            Assert.That(controller.Center, Is.EqualTo(new Vector2(12f, 5f)), "a gesture keeps the dragged centre");
            controller.EndGesture();

            // The focus is still recorded, so the next non-gesture zoom change re-aims at it.
            Assert.That(controller.FocusOn(new HexCoord(0, 0), out error), Is.True, error);
            controller.Zoom = 4f;
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero), "a non-gesture zoom re-aims at the focus");
        }

        [Test]
        public void AnAnchoredZoomKeepsTheAnchoredPointStill()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;
            Assert.That(controller.TrySetOffset(new Vector2(10f, 0f), out error), Is.True, error);

            OrthographicMapFraming before;
            Assert.That(controller.TryGetFraming(out before), Is.True);
            var anchor = new Vector2(0.5f, 0f);
            var anchoredBefore = controller.Center.x + anchor.x * before.VisibleWidth * 0.5f;

            Assert.That(controller.TryZoomTo(3f, anchor, out error), Is.True, error);

            OrthographicMapFraming after;
            Assert.That(controller.TryGetFraming(out after), Is.True);
            var anchoredAfter = controller.Center.x + anchor.x * after.VisibleWidth * 0.5f;

            Assert.That(anchoredAfter, Is.EqualTo(anchoredBefore).Within(0.0001f), "the anchored world point stays put");
            Assert.That(controller.Center.x, Is.GreaterThan(10f), "the centre shifts towards the anchor");

            // A centred anchor must not move the centre at all.
            var centered = controller.Center;
            Assert.That(controller.TryZoomTo(4f, Vector2.zero, out error), Is.True, error);
            Assert.That((controller.Center - centered).magnitude, Is.LessThan(0.00001f));
        }

        [Test]
        public void AnAnchoredZoomOutranksTheFocus()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;
            Assert.That(controller.FocusOn(new HexCoord(0, 0), out error), Is.True, error);
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero));

            // Pinching away from the focus must move the view, not snap back to the focus.
            Assert.That(controller.TryZoomTo(3f, new Vector2(0.6f, 0f), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.GreaterThan(0f), "the anchored zoom wins over the focus");
        }

        [Test]
        public void OffsetClampsPerAxisAndRejectsNonFiniteComponents()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;

            Assert.That(controller.TrySetOffset(new Vector2(1000f, 1000f), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(controller.MaxOffset.y).Within(0.00001f));

            Assert.That(controller.TrySetOffset(new Vector2(-1000f, -1000f), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MinOffset.x).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(controller.MinOffset.y).Within(0.00001f));

            Assert.That(controller.TrySetOffset(new Vector2(float.NaN, 1f), out error), Is.False);
            Assert.That(error, Does.Contain("X"));
            Assert.That(controller.TrySetOffset(new Vector2(1f, float.NaN), out error), Is.False);
            Assert.That(error, Does.Contain("Y"));

            Assert.That(controller.NormalizedOffset.x, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(controller.NormalizedOffset.y, Is.EqualTo(0f).Within(0.00001f));
        }

        [Test]
        public void AZeroZoomFallsBackToOne()
        {
            var mapView = CreateMap();
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            controller.Zoom = 0f;

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            Assert.That(controller.Zoom, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void FocusOutsideTheMapIsReported()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            Assert.That(controller.FocusOn(new HexCoord(12, 0), out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(controller.FocusOn(new HexCoord(0, 0), out error), Is.True, error);
            Assert.That(controller.FocusOn(new HexCoord(-11, 11), out error), Is.True, error);
        }

        [Test]
        public void LandscapeViewportLocksTheCameraAndPanningDoesNothing()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(LandscapeAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);

            // This is geometry, not a defect: a wide frame swallows this map whole.
            Assert.That(framing.ShowsEveryColumn, Is.True);
            Assert.That(controller.IsLockedToCenter, Is.True);
            Assert.That(controller.MinOffset, Is.EqualTo(Vector2.zero));
            Assert.That(controller.MaxOffset, Is.EqualTo(Vector2.zero));

            var before = camera.transform.position;
            Assert.That(controller.TrySetOffset(new Vector2(5f, 5f), out error), Is.True, error);
            Assert.That(camera.transform.position, Is.EqualTo(before));
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void OffsetMovesTheCameraAlongTheMapPlaneAxesAndClamps()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;

            Assert.That(controller.TrySetOffset(new Vector2(3f, 0f), out error), Is.True, error);
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(3f, 30f, 0f)));

            // The second component runs along the map's local plane axis, which is world Z on XZ.
            Assert.That(controller.TrySetOffset(new Vector2(0f, 2f), out error), Is.True, error);
            Assert.That(camera.transform.position.z, Is.EqualTo(2f).Within(0.00001f));

            Assert.That(controller.TrySetOffset(new Vector2(1000f, 1000f), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(controller.MaxOffset.y).Within(0.00001f));
            Assert.That(camera.transform.position.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));

            Assert.That(controller.TrySetOffset(new Vector2(-1000f, -1000f), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MinOffset.x).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(controller.MinOffset.y).Within(0.00001f));

            Assert.That(controller.TrySetOffset(new Vector2(float.NaN, 0f), out error), Is.False);
            Assert.That(error, Is.Not.Empty);

            Assert.That(controller.TrySetOffset(new Vector2(1f, 0f), out error), Is.True, error);
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(1f, 30f, 0f)));
            Assert.That(Vector3.Dot(camera.transform.forward, DownwardForward), Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void WritingALargeOffsetIsClampedAndApplied()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;

            Assert.That(controller.TrySetOffset(new Vector2(500f, 500f), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(controller.MaxOffset.y).Within(0.00001f));
            Assert.That(camera.transform.position.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));

            Assert.That(controller.TrySetOffset(new Vector2(-500f, -500f), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MinOffset.x).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(controller.MinOffset.y).Within(0.00001f));
        }

        [Test]
        public void FlatOrientationReframesAndStillKeepsEveryRowInside()
        {
            var flat = CreateMap(radius: 11, secondaryScale: 0.9f, orientation: HexOrientation.Flat);
            var flatCamera = CreateCamera(PortraitAspect);
            var flatController = CreateController(flat, flatCamera);

            string error;
            Assert.That(flatController.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming flatFraming;
            Assert.That(flatController.TryGetFraming(out flatFraming), Is.True);
            Assert.That(flatFraming.VisibleHeight, Is.GreaterThanOrEqualTo(flatFraming.MapDepth));
            // Flat: half depth = sqrt(3) * 11 * 0.9 + sqrt(3)/2 * 0.9, half width = 1.5 * 11 * 0.9 + 1
            Assert.That(flatFraming.MapHalfDepth, Is.EqualTo(17.9267255f).Within(0.001f));
            Assert.That(flatFraming.MapHalfWidth, Is.EqualTo(14.85f + 1f).Within(0.00001f));
            Assert.That(flatController.IsLockedToCenter, Is.False);
            AssertEveryRowIsInsideTheFrustum(flat, flatCamera, flatFraming);

            var pointy = CreateMap(radius: 11, secondaryScale: 0.9f, orientation: HexOrientation.Pointy);
            var pointyCamera = CreateCamera(PortraitAspect);
            var pointyController = CreateController(pointy, pointyCamera);
            Assert.That(pointyController.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming pointyFraming;
            Assert.That(pointyController.TryGetFraming(out pointyFraming), Is.True);

            // The envelopes are not a clean swap of one another.
            Assert.That(flatFraming.OrthographicSize, Is.GreaterThan(pointyFraming.OrthographicSize));
            Assert.That(
                Mathf.Abs(flatController.MaxOffset.x - pointyController.MaxOffset.x),
                Is.GreaterThan(0.5f),
                "the two orientations must not share a panning range");
        }

        [Test]
        public void ForcingTheWrongPlaneIsReportedInsteadOfSilentlyIgnored()
        {
            var mapView = CreateMap();
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            controller.PlaneMode = MapPlaneMode.ForceXY;

            string error;
            Assert.That(controller.TryRefresh(out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(controller.HasFraming, Is.False);
        }

        [Test]
        public void ViewportChangeKeepsEveryRowVisibleAndRecalculatesTheRange()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming portrait;
            Assert.That(controller.TryGetFraming(out portrait), Is.True);
            var sizeInPortrait = camera.orthographicSize;
            Assert.That(controller.TrySetOffset(controller.MaxOffset, out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));

            // A landscape viewport swallows the map, so the offset is clamped back to the center.
            camera.aspect = LandscapeAspect;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming landscape;
            Assert.That(controller.TryGetFraming(out landscape), Is.True);
            Assert.That(camera.orthographicSize, Is.EqualTo(sizeInPortrait).Within(0.00001f));
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0f, 30f, 0f)));
            AssertEveryRowIsInsideTheFrustum(mapView, camera, landscape);
        }

        [Test]
        public void TargetAspectIsKeptWhenTheRealAspectIsWithinTolerance()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect + 0.0001f);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);
            Assert.That(framing.VisibleWidth, Is.EqualTo(framing.VisibleHeight * PortraitAspect).Within(0.0001f));
        }

        [Test]
        public void NonIdentityMapTransformAndScaleAreRespected()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            mapView.transform.position = new Vector3(100f, 5f, -7f);
            mapView.transform.localScale = new Vector3(2f, 2f, 2f);

            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);

            Assert.That(camera.transform.position.x, Is.EqualTo(100f).Within(0.00001f));
            Assert.That(camera.transform.position.y, Is.EqualTo(65f).Within(0.00001f));
            Assert.That(camera.transform.position.z, Is.EqualTo(-7f).Within(0.00001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(framing.OrthographicSize * 2f).Within(0.001f));

            Assert.That(controller.TrySetOffset(new Vector2(2f, 0f), out error), Is.True, error);
            Assert.That(camera.transform.position.x, Is.EqualTo(104f).Within(0.00001f));
            Assert.That(Vector3.Dot(camera.transform.forward, DownwardForward), Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void PerspectiveCameraIsReportedAsAMisconfiguration()
        {
            var mapView = CreateMap();
            var camera = CreateCamera(PortraitAspect);
            camera.orthographic = false;
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.False);
            Assert.That(error, Does.Contain("orthographic"));
            Assert.That(controller.HasFraming, Is.False);
        }

        [Test]
        public void UnbuiltMapIsReportedInsteadOfThrowing()
        {
            m_MapObject = new GameObject("Hex Map View");
            var mapView = m_MapObject.AddComponent<HexMapView>();
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void MissingReferencesAndInvalidClipsAreReported()
        {
            var mapView = CreateMap();
            var camera = CreateCamera(PortraitAspect);

            var controller = m_CameraObject.AddComponent<OrthographicMapCamera>();
            string error;
            Assert.That(controller.TryRefresh(out error), Is.False);
            Assert.That(error, Does.Contain("HexMapView"));

            controller.HexMapView = mapView;
            Assert.That(controller.TryRefresh(out error), Is.False);
            Assert.That(error, Does.Contain("Camera"));

            controller.Camera = camera;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            controller.ViewMargin = 0.5f;
            Assert.That(controller.TryRefresh(out error), Is.False);
            Assert.That(error, Is.Not.Empty);

            controller.ViewMargin = DefaultViewMargin;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
        }

        [Test]
        public void LayerSettingsMaskIsAppliedToTheCameraAndSelfChecks()
        {
            var mapView = CreateMap();
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            m_SettingsObject = new GameObject("Map Layer Settings");
            var settings = m_SettingsObject.AddComponent<OrthographicMapLayerSettings>();
            settings.HexMapView = mapView;
            controller.LayerSettings = settings;

            string error;
            var cellLayer = mapView.CellLayer;

            // The default mask is everything, which must cover the map.
            Assert.That(settings.TryValidate(out error), Is.True, error);

            // A mask that omits the cell layer must stop the refresh, not silently blank the map.
            var blindLayer = cellLayer == 0 ? 1 : 0;
            settings.CullingMask = 1 << blindLayer;
            Assert.That(settings.TryValidate(out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(controller.TryRefresh(out error), Is.False);
            Assert.That(error, Does.Contain("cell layer"));

            // A mask that covers the cell layer is accepted and copied onto the camera.
            settings.CullingMask = ~0;
            Assert.That(settings.TryValidate(out error), Is.True, error);
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            Assert.That(camera.cullingMask, Is.EqualTo(~0));

            var narrowMask = (1 << cellLayer) | (1 << blindLayer);
            settings.CullingMask = narrowMask;
            Assert.That(settings.TryValidate(out error), Is.True, error);
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            Assert.That(camera.cullingMask, Is.EqualTo(narrowMask));
        }

        [Test]
        public void LayerSettingsReportAMaskThatExcludesTheMapLayer()
        {
            var mapView = CreateMap();

            m_SettingsObject = new GameObject("Map Layer Settings");
            var settings = m_SettingsObject.AddComponent<OrthographicMapLayerSettings>();
            settings.HexMapView = mapView;

            var otherLayer = mapView.CellLayer == 0 ? 1 : 0;
            settings.CullingMask = 1 << otherLayer;

            string error;
            Assert.That(settings.TryValidate(out error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void LayerSettingsReportAMissingView()
        {
            m_SettingsObject = new GameObject("Map Layer Settings");
            var settings = m_SettingsObject.AddComponent<OrthographicMapLayerSettings>();

            string error;
            Assert.That(settings.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("HexMapView"));
        }

        private static void AssertEveryRowIsInsideTheFrustum(
            HexMapView mapView,
            Camera camera,
            OrthographicMapFraming framing)
        {
            var mapTransform = mapView.transform;

            // Screen up is the map's local +Z turned into world space, which still points straight up
            // for a flat XZ map but is not simply world +Z in general.
            var worldUp = mapTransform.TransformDirection(Vector3.forward).normalized;

            // Asserting only that forward is perpendicular would accept a camera that is upside down,
            // so pin the up axis to the map's own up and pin forward to straight down.
            Assert.That(
                Vector3.Dot(camera.transform.up, worldUp),
                Is.EqualTo(1f).Within(0.00001f),
                "the camera's up must follow the map's own up axis");
            Assert.That(
                Vector3.Dot(camera.transform.forward, DownwardForward),
                Is.EqualTo(1f).Within(0.00001f),
                "the camera must look straight down at the map");
            Assert.That(
                camera.transform.position.y,
                Is.GreaterThan(mapTransform.TransformPoint(framing.Origin).y),
                "the camera must sit above the map plane");

            var mapOrigin = mapTransform.TransformPoint(framing.Origin);
            foreach (var depth in new[] { -framing.MapHalfDepth, framing.MapHalfDepth })
            {
                var point = mapOrigin + worldUp * depth;
                var distance = Vector3.Dot(point - camera.transform.position, camera.transform.forward);

                Assert.That(
                    distance,
                    Is.InRange(camera.nearClipPlane, camera.farClipPlane),
                    string.Format("map depth edge {0} must lie inside the clip range.", depth));
            }
        }
    }
}

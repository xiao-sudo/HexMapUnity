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
        public void MaxZoomIsTheClosestLevelTheComponentAllows()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            // A zoom level divides the frame that fits the whole map depth, so the ceiling is a plain
            // number: it means the same on another map, another radius or another screen shape. Deriving
            // it from the viewport aspect made the limit depend on the screen in a way no sentence could
            // describe, and the aspect entered twice once the visible width was the quantity limited.
            Assert.That(controller.MaxZoom, Is.EqualTo(12f).Within(0.00001f));

            controller.Zoom = 1000f;
            Assert.That(controller.Zoom, Is.EqualTo(controller.MaxZoom).Within(0.00001f));

            controller.MaxZoom = 5f;
            controller.Zoom = 1000f;
            Assert.That(controller.Zoom, Is.EqualTo(5f).Within(0.00001f));

            controller.MaxZoom = 1f;
            controller.Zoom = 1000f;
            Assert.That(
                controller.Zoom,
                Is.EqualTo(1f).Within(0.00001f),
                "a ceiling of 1 is a camera that pans but never zooms");

            controller.Zoom = 0.5f;
            Assert.That(controller.Zoom, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void ZoomIsClampedBeforeTheFirstRefresh()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            // The ceiling no longer waits for a framing to exist, so a zoom written from Awake or from
            // the inspector cannot sit above the allowed range until the next refresh.
            controller.Zoom = 1000f;

            Assert.That(controller.Zoom, Is.EqualTo(controller.MaxZoom).Within(0.00001f));
        }

        [Test]
        public void AnImpossibleMaxZoomIsRefusedInsteadOfBecomingAZeroSize()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);
            controller.MaxZoom = 0f;

            string error;
            Assert.That(controller.TryRefresh(out error), Is.False);
            Assert.That(error, Does.Contain("Max zoom"));

            // A zoom written while the ceiling is nonsense stops at the floor rather than at zero.
            controller.Zoom = 1000f;
            Assert.That(controller.Zoom, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void RefreshingIfStaleLeavesACurrentCameraAlone()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            // A frame the camera already wrote must survive a call that finds nothing stale, or every
            // gesture would undo the view the player is looking at.
            var size = camera.orthographicSize;
            camera.transform.position = new Vector3(1f, 2f, 3f);

            Assert.That(controller.TryRefreshIfStale(out error), Is.True, error);
            Assert.That(error, Is.Empty);
            Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(0.00001f));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void RefreshingIfStaleCatchesAViewportChangeThatNoCallerReported()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            // This is what a window resize or a device rotation does: the engine changes the camera's
            // aspect with no callback and no setter of ours behind it, so nothing but this check can
            // notice it. Vertical panning is unaffected, which is why only the horizontal range moves.
            camera.aspect = LandscapeAspect;
            var staleMaxX = controller.MaxOffset.x;

            Assert.That(controller.TryRefreshIfStale(out error), Is.True, error);

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);
            Assert.That(framing.Aspect, Is.EqualTo(LandscapeAspect).Within(0.00001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(framing.OrthographicSize).Within(0.00001f));

            // A wider frame covers more of the map, so less of it can be panned into view.
            Assert.That(controller.MaxOffset.x, Is.LessThan(staleMaxX));
        }

        [Test]
        public void RefreshingIfStaleRetriesAFramingThatFailedOnLoad()
        {
            m_MapObject = new GameObject("Hex Map View");
            var mapView = m_MapObject.AddComponent<HexMapView>();
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            // The map is not built yet, so there is nothing to frame and nothing to retry against.
            string error;
            Assert.That(controller.TryRefreshIfStale(out error), Is.False);
            Assert.That(error, Does.Contain("built"));

            mapView.Build();
            Assert.That(controller.TryRefreshIfStale(out error), Is.True, error);
            Assert.That(controller.HasFraming, Is.True);
        }

        [Test]
        public void CreatingAFramingWithoutACameraIsReportedInsteadOfThrowing()
        {
            // The camera comes first because CreateMap reassigns m_CameraObject; CreateCamera is what
            // creates the GameObject, and the controller below goes on it.
            var camera = CreateCamera(PortraitAspect);
            var mapView = CreateMap();
            var controller = camera.gameObject.AddComponent<OrthographicMapCamera>();
            controller.HexMapView = mapView;

            OrthographicMapFraming framing;
            string error;

            // The camera is only read for its aspect, so the null check is easy to forget and the
            // failure would otherwise be a NullReferenceException rather than a reported refusal.
            Assert.That(controller.TryCreateFraming(out framing, out error), Is.False);
            Assert.That(error, Does.Contain("Camera"));
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
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ZoomingToACellClampsTheAimAgainstTheZoomItAsksFor()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            var northEdge = new HexCoord(0, 11);

            // Zoom 1 leaves no vertical room at all, so this is the sequence the one-call form exists to
            // make unrepresentable: the aim is clamped against a range of zero and the north edge lands on
            // the horizontal axis, where nothing that happens afterwards can recover it.
            Assert.That(controller.FocusOn(northEdge, out error), Is.True, error);
            Assert.That(controller.Zoom, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(0f).Within(0.00001f));

            controller.Zoom = 4f;
            Assert.That(controller.Center.y, Is.EqualTo(0f).Within(0.00001f), "aiming first loses the aim");

            // The cell form sets the zoom before it clamps, so the same request reaches the range the
            // zoom 4 frame actually leaves.
            Assert.That(controller.TryZoomToCell(northEdge, 4f, out error), Is.True, error);
            Assert.That(controller.Zoom, Is.EqualTo(4f).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(controller.MaxOffset.y).Within(0.00001f));
            Assert.That(controller.Center.y, Is.GreaterThan(0f));

            // Its mirror ends up at the opposite end of the same range.
            var north = controller.Center.y;
            Assert.That(controller.TryZoomToCell(new HexCoord(0, -11), 4f, out error), Is.True, error);
            Assert.That(controller.Center.y, Is.EqualTo(-north).Within(0.00001f));
        }

        [Test]
        public void ZoomingToACellAndToItsWorldCentreAgree()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            // The cell form is only a conversion plus ordering, so it must agree with the world-point
            // form on the point the layout puts the cell at.
            const float zoom = 2.5f;
            var coordinate = new HexCoord(4, -7);
            var world = mapView.transform.TransformPoint(mapView.Layout.HexToWorld(coordinate));

            Assert.That(controller.TryZoomToCell(coordinate, zoom, out error), Is.True, error);
            var fromCell = controller.Center;

            Assert.That(controller.TryZoomToPoint(zoom, world, out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(fromCell.x).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(fromCell.y).Within(0.00001f));
        }

        [Test]
        public void ZoomingToACellReportsAnOutsideCoordinateAndANonFiniteZoom()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            Assert.That(controller.TryZoomToCell(new HexCoord(12, 0), 2f, out error), Is.False);
            Assert.That(error, Does.Contain("outside the map"));

            Assert.That(controller.TryZoomToCell(new HexCoord(0, 0), float.NaN, out error), Is.False);
            Assert.That(error, Does.Contain("zoom"));

            // (0,0) and a corner on the ring are both inside, which is the boundary the distance test
            // has to get right.
            Assert.That(controller.TryZoomToCell(new HexCoord(0, 0), 2f, out error), Is.True, error);
            Assert.That(controller.TryZoomToCell(new HexCoord(-11, 11), 2f, out error), Is.True, error);
        }

        [Test]
        public void AimingBeforeTheFirstRefreshIsRefusedInsteadOfThrowing()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            // The applied layout and transform are snapshots a refresh leaves behind, so aiming without
            // one would dereference them. All three aiming entry points have to refuse instead.
            string error;
            Assert.That(controller.FocusOn(new HexCoord(0, 0), out error), Is.False);
            Assert.That(error, Does.Contain("Refresh the camera"));

            Assert.That(controller.TryZoomToCell(new HexCoord(0, 0), 2f, out error), Is.False);
            Assert.That(error, Does.Contain("Refresh the camera"));

            Assert.That(controller.FocusOnWorld(Vector3.zero, out error), Is.False);
            Assert.That(error, Does.Contain("Refresh the camera"));
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

            // The cell sits at the map's edge (x = 19.05256), so its world centre is beyond the panning
            // range and the camera stops at the range's edge instead of reaching it.
            Assert.That(controller.Center.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));
            Assert.That(controller.Center.x, Is.LessThan(19.05256f));

            Assert.That(controller.FocusOn(new HexCoord(-11, 0), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MinOffset.x).Within(0.00001f));

            Assert.That(controller.FocusOn(new HexCoord(0, 11), out error), Is.True, error);
            Assert.That(controller.Center.y, Is.EqualTo(controller.MaxOffset.y).Within(0.00001f));
        }

        [Test]
        public void TheWidestLevelKeepsTheHorizontalAimAndDropsTheVerticalOne()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;
            Assert.That(controller.FocusOn(new HexCoord(11, 0), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.GreaterThan(0f));

            // The widest level shows the whole depth, so the vertical range collapses and the clamp takes
            // the vertical component to the middle by itself. The horizontal component is untouched: the
            // player asked to look at the right edge and the widest level still shows the right edge.
            controller.Zoom = 1f;
            Assert.That(controller.Center.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(
                controller.Center.x,
                Is.EqualTo(controller.MaxOffset.x).Within(0.00001f),
                "the widest level must not throw the horizontal aim away");

            // The way back in keeps the centre it finds rather than re-aiming anything. Note it keeps the
            // widest level's value (10.17), not the value the zoom 2 aim asked for (15.05): the clamp at
            // zoom 1 already moved it, and a zoom change never restores an older centre.
            var atWidest = controller.Center.x;
            controller.Zoom = 2f;
            Assert.That(controller.Center.x, Is.EqualTo(atWidest).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(0f).Within(0.00001f));
        }

        [Test]
        public void PanningWorksAtTheWidestLevelBecauseTheHorizontalRangeSurvives()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            // This is the whole point of keeping the aim at zoom 1: a portrait screen shows roughly half
            // the map's width, so looking at the left half and the right half in turn is the only way to
            // see the whole map at the level that shows every row.
            Assert.That(controller.Zoom, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(controller.MaxOffset.x, Is.GreaterThan(0f));

            Assert.That(controller.TrySetOffset(new Vector2(-controller.MaxOffset.x, 0f), out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MinOffset.x).Within(0.00001f));
            Assert.That(controller.Center.y, Is.EqualTo(0f).Within(0.00001f));
        }

        [Test]
        public void AZoomChangeKeepsTheCenterItFinds()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            controller.Zoom = 2f;
            Assert.That(controller.TrySetOffset(new Vector2(12f, 5f), out error), Is.True, error);
            Assert.That(controller.Center, Is.EqualTo(new Vector2(12f, 5f)));

            controller.Zoom = 3f;
            Assert.That(controller.Center, Is.EqualTo(new Vector2(12f, 5f)), "a zoom change keeps the dragged centre");

            // Aiming afterwards moves it, and the next zoom change keeps that instead of the old value.
            Assert.That(controller.FocusOn(new HexCoord(0, 0), out error), Is.True, error);
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero));
            controller.Zoom = 4f;
            Assert.That(controller.Center, Is.EqualTo(Vector2.zero), "zooming again keeps the aimed centre");
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
        public void ZoomingToAPointClampsAgainstTheNewZoomRatherThanTheOldOne()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            // The widest level leaves a narrow range: aiming here and zooming afterwards would clamp the
            // aim to 10.17 and leave the edge cell outside the frame at zoom 3.
            controller.Zoom = 1f;
            var widestRangeX = controller.MaxOffset.x;

            var edgeCellCentre = new Vector3(19.05256f, 0f, 0f);
            Assert.That(controller.TryZoomToPoint(3f, edgeCellCentre, out error), Is.True, error);

            Assert.That(controller.Zoom, Is.EqualTo(3f).Within(0.00001f));
            Assert.That(controller.Center.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));
            Assert.That(
                controller.Center.x,
                Is.GreaterThan(widestRangeX),
                "the aim was clamped against the range the new zoom leaves, not the one it left");

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);
            Assert.That(
                controller.Center.x + (framing.VisibleWidth * 0.5f),
                Is.GreaterThanOrEqualTo(edgeCellCentre.x),
                "the aimed cell is inside the frame");

            // The widest level is not special-cased: the same aim keeps its horizontal component and
            // loses only the vertical one, which the clamp removes because the frame covers the depth.
            Assert.That(controller.TryZoomToPoint(1f, edgeCellCentre, out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MaxOffset.x).Within(0.00001f));
            Assert.That(controller.Center.x, Is.GreaterThan(0f));
            Assert.That(controller.Center.y, Is.EqualTo(0f).Within(0.00001f));

            // The mirror aim must land on the mirror end of the range rather than on the middle.
            var westEdgeCentre = new Vector3(-19.05256f, 0f, 0f);
            Assert.That(controller.TryZoomToPoint(1f, westEdgeCentre, out error), Is.True, error);
            Assert.That(controller.Center.x, Is.EqualTo(controller.MinOffset.x).Within(0.00001f));
            Assert.That(controller.Center.x, Is.LessThan(0f));

            // A point that cannot be aimed at is refused rather than clamped.
            Assert.That(controller.TryZoomToPoint(3f, new Vector3(float.NaN, 0f, 0f), out error), Is.False);
            Assert.That(error, Does.Contain("finite"));
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
            controller.LayerSettings = settings;

            string error;
            var cellLayer = mapView.CellLayer;

            // The default mask is everything, which must cover the map.
            Assert.That(settings.TryValidate(cellLayer, out error), Is.True, error);

            // A mask that omits the cell layer must stop the refresh, not silently blank the map.
            var blindLayer = cellLayer == 0 ? 1 : 0;
            settings.CullingMask = 1 << blindLayer;
            Assert.That(settings.TryValidate(cellLayer, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(controller.TryRefresh(out error), Is.False);
            Assert.That(error, Does.Contain("cell layer"));

            // A mask that covers the cell layer is accepted and copied onto the camera.
            settings.CullingMask = ~0;
            Assert.That(settings.TryValidate(cellLayer, out error), Is.True, error);
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            Assert.That(camera.cullingMask, Is.EqualTo(~0));

            var narrowMask = (1 << cellLayer) | (1 << blindLayer);
            settings.CullingMask = narrowMask;
            Assert.That(settings.TryValidate(cellLayer, out error), Is.True, error);
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            Assert.That(camera.cullingMask, Is.EqualTo(narrowMask));
        }

        [Test]
        public void LayerSettingsReportAMaskThatExcludesTheMapLayer()
        {
            var mapView = CreateMap();

            m_SettingsObject = new GameObject("Map Layer Settings");
            var settings = m_SettingsObject.AddComponent<OrthographicMapLayerSettings>();

            var otherLayer = mapView.CellLayer == 0 ? 1 : 0;
            settings.CullingMask = 1 << otherLayer;

            string error;
            Assert.That(settings.TryValidate(mapView.CellLayer, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void LayerSettingsReportALayerOutsideTheUsableRange()
        {
            m_SettingsObject = new GameObject("Map Layer Settings");
            var settings = m_SettingsObject.AddComponent<OrthographicMapLayerSettings>();

            string error;
            Assert.That(settings.TryValidate(32, out error), Is.False);
            Assert.That(error, Does.Contain("0..31"));
        }

        [Test]
        public void LayerSettingsReportAMissingViewThroughTheCamera()
        {
            // The settings hold only the mask now, so the missing view is the camera's refusal to report.
            // It has to be checked before the mask is validated: the mask is checked against the layer the
            // missing view would have named, so the other order would dereference it.
            m_SettingsObject = new GameObject("Map Layer Settings");
            var settings = m_SettingsObject.AddComponent<OrthographicMapLayerSettings>();

            var camera = CreateCamera(PortraitAspect);
            var controller = m_CameraObject.AddComponent<OrthographicMapCamera>();
            controller.Camera = camera;
            controller.LayerSettings = settings;

            string error;
            Assert.That(controller.TryRefresh(out error), Is.False);
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

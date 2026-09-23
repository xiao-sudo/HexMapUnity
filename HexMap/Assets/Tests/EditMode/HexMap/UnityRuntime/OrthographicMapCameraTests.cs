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

            Assert.That(framing.VisibleHeight, Is.GreaterThanOrEqualTo(framing.MapDepth));
            Assert.That(framing.ShowsEveryColumn, Is.False);
            Assert.That(controller.IsLockedToCenter, Is.False);
            Assert.That(framing.MaxOffset, Is.EqualTo(10.1733f).Within(0.001f));

            AssertEveryRowIsInsideTheFrustum(camera, framing);
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
            Assert.That(framing.MinOffset, Is.EqualTo(0f));
            Assert.That(framing.MaxOffset, Is.EqualTo(0f));

            var before = camera.transform.position;
            Assert.That(controller.TrySetOffset(5f, out error), Is.True, error);
            Assert.That(camera.transform.position, Is.EqualTo(before));
            Assert.That(controller.Offset, Is.EqualTo(0f));
        }

        [Test]
        public void OffsetMovesTheCameraAlongTheMapXAxisAndClamps()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);

            Assert.That(controller.TrySetOffset(3f, out error), Is.True, error);
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(3f, 30f, 0f)));

            Assert.That(controller.TrySetOffset(1000f, out error), Is.True, error);
            Assert.That(controller.Offset, Is.EqualTo(framing.MaxOffset).Within(0.00001f));
            Assert.That(camera.transform.position.x, Is.EqualTo(framing.MaxOffset).Within(0.00001f));

            Assert.That(controller.TrySetOffset(-1000f, out error), Is.True, error);
            Assert.That(camera.transform.position.x, Is.EqualTo(framing.MinOffset).Within(0.00001f));

            Assert.That(controller.TrySetOffset(float.NaN, out error), Is.False);
            Assert.That(error, Is.Not.Empty);

            Assert.That(controller.TrySetOffset(1f, out error), Is.True, error);
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(1f, 30f, 0f)));
            Assert.That(Vector3.Dot(camera.transform.forward, DownwardForward), Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void WritingTheOffsetPropertyAlsoClampsAndApplies()
        {
            var mapView = CreateMap(radius: 11, secondaryScale: 0.9f);
            var camera = CreateCamera(PortraitAspect);
            var controller = CreateController(mapView, camera);

            string error;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming framing;
            Assert.That(controller.TryGetFraming(out framing), Is.True);

            controller.Offset = 500f;
            Assert.That(controller.Offset, Is.EqualTo(framing.MaxOffset).Within(0.00001f));
            Assert.That(camera.transform.position.x, Is.EqualTo(framing.MaxOffset).Within(0.00001f));

            controller.Offset = -500f;
            Assert.That(controller.Offset, Is.EqualTo(framing.MinOffset).Within(0.00001f));
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
            AssertEveryRowIsInsideTheFrustum(flatCamera, flatFraming);

            var pointy = CreateMap(radius: 11, secondaryScale: 0.9f, orientation: HexOrientation.Pointy);
            var pointyCamera = CreateCamera(PortraitAspect);
            var pointyController = CreateController(pointy, pointyCamera);
            Assert.That(pointyController.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming pointyFraming;
            Assert.That(pointyController.TryGetFraming(out pointyFraming), Is.True);

            // The envelopes are not a clean swap of one another.
            Assert.That(flatFraming.OrthographicSize, Is.GreaterThan(pointyFraming.OrthographicSize));
            Assert.That(flatFraming.MaxOffset, Is.Not.EqualTo(pointyFraming.MaxOffset).Within(0.5f));
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
            Assert.That(controller.TrySetOffset(portrait.MaxOffset, out error), Is.True, error);
            Assert.That(controller.Offset, Is.EqualTo(portrait.MaxOffset).Within(0.00001f));

            // A landscape viewport swallows the map, so the offset is clamped back to the center.
            camera.aspect = LandscapeAspect;
            Assert.That(controller.TryRefresh(out error), Is.True, error);

            OrthographicMapFraming landscape;
            Assert.That(controller.TryGetFraming(out landscape), Is.True);
            Assert.That(camera.orthographicSize, Is.EqualTo(sizeInPortrait).Within(0.00001f));
            Assert.That(controller.Offset, Is.EqualTo(0f));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0f, 30f, 0f)));
            AssertEveryRowIsInsideTheFrustum(camera, landscape);
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

            Assert.That(controller.TrySetOffset(2f, out error), Is.True, error);
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
            Assert.That(settings.TryValidate(out error), Is.True, error);

            settings.CullingMask = 1 << 9;
            Assert.That(settings.TryValidate(out error), Is.True, error);
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            Assert.That(camera.cullingMask, Is.EqualTo(1 << 9));

            // The HexMapView's cell layer is 0 by default, so a mask of layer 9 must be rejected.
            settings.CullingMask = 1 << mapView.CellLayer;
            Assert.That(settings.TryValidate(out error), Is.True, error);
            Assert.That(controller.TryRefresh(out error), Is.True, error);
            Assert.That(camera.cullingMask, Is.EqualTo(1 << mapView.CellLayer));
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

        private static void AssertEveryRowIsInsideTheFrustum(Camera camera, OrthographicMapFraming framing)
        {
            var position = camera.transform.position;
            var forward = camera.transform.forward;

            foreach (var depth in new[] { -framing.MapHalfDepth, framing.MapHalfDepth })
            {
                var point = position + ScreenUp * depth;
                var distance = Vector3.Dot(point - position, forward);

                Assert.That(
                    distance,
                    Is.InRange(camera.nearClipPlane, camera.farClipPlane),
                    string.Format("map depth edge {0} must lie inside the clip range.", depth));
            }
        }
    }
}

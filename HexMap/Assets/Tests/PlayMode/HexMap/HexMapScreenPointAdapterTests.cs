using HexMap.Core;
using HexMap.Sample;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.UnityRuntime.Tests
{
    [TestFixture]
    public sealed class HexMapScreenPointAdapterTests
    {
        private GameObject m_ViewObject;
        private GameObject m_CameraObject;

        [TearDown]
        public void TearDown()
        {
            if (m_ViewObject != null)
            {
                Object.DestroyImmediate(m_ViewObject);
            }

            if (m_CameraObject != null)
            {
                Object.DestroyImmediate(m_CameraObject);
            }
        }

        [Test]
        public void DirectScreenPointOperationConvertsThroughTheMapPlane()
        {
            m_ViewObject = new GameObject("Hex Map View");
            var view = m_ViewObject.AddComponent<HexMapView>();
            view.Plane = HexPlane.XZ;
            view.Build();
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;

            m_CameraObject = new GameObject("Picker Camera");
            var camera = m_CameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.transform.position = new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var adapter = m_ViewObject.AddComponent<HexMapScreenPointAdapter>();
            adapter.MapView = view;
            adapter.Picker = picker;
            adapter.TargetCamera = camera;
            adapter.MaxDistance = 100f;
            adapter.HandleLeftMouseClick = false;

            var worldPoint = m_ViewObject.transform.TransformPoint(
                view.Layout.HexToWorld(new HexCoord(0, 0)));
            var result = adapter.PickScreenPoint(camera.WorldToScreenPoint(worldPoint));

            Assert.That(result.Status, Is.EqualTo(HexMapScreenPointStatus.Found));
            Assert.That(result.MapResult.Value.Status, Is.EqualTo(HexPickStatus.Found));
            Assert.That(result.MapResult.Value.Cell.Coordinate, Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(Vector3.Distance(result.WorldPosition, worldPoint), Is.LessThan(0.0001f));
        }

        [Test]
        public void DirectScreenPointOperationSupportsTheXYMapPlane()
        {
            m_ViewObject = new GameObject("XY Hex Map View");
            var view = m_ViewObject.AddComponent<HexMapView>();
            view.Plane = HexPlane.XY;
            view.Origin = new Vector3(0.25f, -0.5f, 2f);
            view.Build();
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;

            m_CameraObject = new GameObject("XY Picker Camera");
            var camera = m_CameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;

            var adapter = m_ViewObject.AddComponent<HexMapScreenPointAdapter>();
            adapter.MapView = view;
            adapter.Picker = picker;
            adapter.TargetCamera = camera;
            adapter.MaxDistance = 100f;
            adapter.HandleLeftMouseClick = false;

            var worldPoint = m_ViewObject.transform.TransformPoint(
                view.Layout.HexToWorld(new HexCoord(0, 0)));
            var result = adapter.PickScreenPoint(camera.WorldToScreenPoint(worldPoint));

            Assert.That(result.Status, Is.EqualTo(HexMapScreenPointStatus.Found));
            Assert.That(result.MapResult.Value.Cell.Coordinate, Is.EqualTo(new HexCoord(0, 0)));
        }

        [Test]
        public void ParallelCameraRayIsAnAdapterFailure()
        {
            m_ViewObject = new GameObject("Hex Map View");
            var view = m_ViewObject.AddComponent<HexMapView>();
            view.Build();
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;

            m_CameraObject = new GameObject("Picker Camera");
            var camera = m_CameraObject.AddComponent<Camera>();
            camera.transform.rotation = Quaternion.identity;

            var adapter = m_ViewObject.AddComponent<HexMapScreenPointAdapter>();
            adapter.MapView = view;
            adapter.Picker = picker;
            adapter.TargetCamera = camera;
            adapter.HandleLeftMouseClick = false;

            var result = adapter.PickScreenPoint(Vector2.zero);

            Assert.That(result.Status, Is.EqualTo(HexMapScreenPointStatus.NoPlaneIntersection));
            Assert.That(result.MapResult.HasValue, Is.False);
        }

        [Test]
        public void MissingCameraIsReportedByTheAdapter()
        {
            m_ViewObject = new GameObject("Hex Map View");
            var view = m_ViewObject.AddComponent<HexMapView>();
            view.Build();
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;
            var adapter = m_ViewObject.AddComponent<HexMapScreenPointAdapter>();
            adapter.MapView = view;
            adapter.Picker = picker;
            adapter.TargetCamera = null;
            adapter.UseMainCamera = false;
            adapter.HandleLeftMouseClick = false;

            var result = adapter.PickScreenPoint(Vector2.zero);

            Assert.That(result.Status, Is.EqualTo(HexMapScreenPointStatus.NoCamera));
            Assert.That(result.MapResult.HasValue, Is.False);
        }
    }
}
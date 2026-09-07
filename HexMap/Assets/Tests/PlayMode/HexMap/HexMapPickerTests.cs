using NUnit.Framework;
using UnityEngine;
using HexMap.Core;
using HexMap.Runtime;

namespace HexMap.UnityRuntime.Tests
{
    [TestFixture]
    public sealed class HexMapPickerTests
    {
        private GameObject viewObject;
        private GameObject cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (viewObject != null)
            {
                Object.DestroyImmediate(viewObject);
            }

            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void PickReturnsTheCellUnderTheScreenPoint()
        {
            viewObject = new GameObject("Hex Map View");
            var view = viewObject.AddComponent<HexMapView>();
            view.Build();

            cameraObject = new GameObject("Picker Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.transform.position = new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var picker = viewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;
            picker.TargetCamera = camera;
            picker.MapLayer = ~0;
            picker.MaxDistance = 100f;

            Physics.SyncTransforms();
            var screenPoint = camera.WorldToScreenPoint(
                view.transform.TransformPoint(view.Layout.HexToWorld(new HexCoord(0, 0))));
            var result = picker.Pick(screenPoint);

            Assert.That(result.Status, Is.EqualTo(HexPickStatus.Found));
            Assert.That(result.Cell.Coordinate, Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(picker.LastResult.Status, Is.EqualTo(HexPickStatus.Found));
        }

        [Test]
        public void PickReturnsNoHitWhenTheRayMissesTheGeneratedMap()
        {
            viewObject = new GameObject("Hex Map View");
            var view = viewObject.AddComponent<HexMapView>();
            view.Build();

            cameraObject = new GameObject("Picker Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.transform.position = new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var picker = viewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;
            picker.TargetCamera = camera;
            picker.MapLayer = ~0;
            picker.MaxDistance = 100f;

            Physics.SyncTransforms();
            var result = picker.Pick(new Vector2(-1000f, -1000f));

            Assert.That(result.Status, Is.EqualTo(HexPickStatus.NoHit));
        }
    }
}
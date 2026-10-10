using System.Collections.Generic;
using HexMap.Core;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.Sample.Tests
{
    [TestFixture]
    public sealed class MapInputHandlerCameraControlTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();

        private OrthographicMapCamera m_MapCamera;
        private MapInputDispatcher m_Dispatcher;
        private GameplayMapInputHandler m_Gameplay;
        private TopDownMapInputHandler m_TopDown;

        [TearDown]
        public void TearDown()
        {
            for (var index = m_Objects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(m_Objects[index]);
            }

            m_Objects.Clear();
        }

        [Test]
        public void BothModesConsumeTheSameCameraDistanceIntentThroughTheDispatcher()
        {
            CreateReadyInputHandlers();
            var context = new MapCameraDistanceContext(Vector2.zero, 0.25f);

            m_Dispatcher.SetActiveChannel(SampleMapInputChannels.TopDown);
            var topDownBefore = m_MapCamera.Zoom;
            Assert.That(m_Dispatcher.OnMapCameraDistanceChanged(context), Is.True);
            var topDownAfter = m_MapCamera.Zoom;

            m_MapCamera.Zoom = topDownBefore;
            m_Dispatcher.SetActiveChannel(SampleMapInputChannels.Gameplay);
            Assert.That(m_Dispatcher.OnMapCameraDistanceChanged(context), Is.True);

            Assert.That(topDownAfter, Is.GreaterThan(topDownBefore));
            Assert.That(m_MapCamera.Zoom, Is.EqualTo(topDownAfter).Within(0.00001f));
        }

        [Test]
        public void BothModesConsumeTheSameScreenDragThroughTheDispatcher()
        {
            CreateReadyInputHandlers();
            m_MapCamera.Zoom = 3f;
            var delta = new Vector2(100f, -40f);

            m_Dispatcher.SetActiveChannel(SampleMapInputChannels.TopDown);
            Assert.That(m_Dispatcher.OnMapDragged(delta), Is.True);
            var topDownCenter = m_MapCamera.Center;

            string error;
            Assert.That(m_MapCamera.TrySetOffset(Vector2.zero, out error), Is.True, error);
            m_Dispatcher.SetActiveChannel(SampleMapInputChannels.Gameplay);
            Assert.That(m_Dispatcher.OnMapDragged(delta), Is.True);

            Assert.That(topDownCenter, Is.Not.EqualTo(Vector2.zero));
            Assert.That(m_MapCamera.Center, Is.EqualTo(topDownCenter));
        }

        [Test]
        public void CameraDistanceAtTheZoomLimitIsStillConsumed()
        {
            CreateReadyInputHandlers();
            m_MapCamera.Zoom = m_MapCamera.MaxZoom;
            m_Dispatcher.SetActiveChannel(SampleMapInputChannels.TopDown);

            Assert.That(
                m_Dispatcher.OnMapCameraDistanceChanged(new MapCameraDistanceContext(Vector2.zero, 1f)),
                Is.True);
            Assert.That(m_MapCamera.Zoom, Is.EqualTo(m_MapCamera.MaxZoom));
        }

        private void CreateReadyInputHandlers()
        {
            var mapView = CreateObject("Hex Map View").AddComponent<HexMapView>();
            mapView.Radius = 9;
            mapView.Orientation = HexOrientation.Pointy;
            mapView.Build();

            var camera = CreateObject("Map Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 9f / 16f;
            m_MapCamera = camera.gameObject.AddComponent<OrthographicMapCamera>();
            m_MapCamera.HexMapView = mapView;
            m_MapCamera.ViewportAspect = camera.aspect;
            string error;
            Assert.That(m_MapCamera.TryRefresh(out error), Is.True, error);

            var cameraControl = CreateObject("Map Camera Input Control")
                .AddComponent<OrthographicMapCameraInputController>();
            cameraControl.MapCamera = m_MapCamera;

            m_Dispatcher = CreateObject("Map Input Dispatcher").AddComponent<MapInputDispatcher>();
            m_Gameplay = CreateHandler<GameplayMapInputHandler>("Gameplay Input Handler", cameraControl);
            m_TopDown = CreateHandler<TopDownMapInputHandler>("Top Down Input Handler", cameraControl);
        }

        private T CreateHandler<T>(string name, OrthographicMapCameraInputController cameraControl)
            where T : MonoBehaviour, IMapInputHandler
        {
            var target = CreateObject(name);
            target.SetActive(false);
            var handler = target.AddComponent<T>();
            if (handler is GameplayMapInputHandler gameplay)
            {
                gameplay.Dispatcher = m_Dispatcher;
                gameplay.CameraInputController = cameraControl;
            }
            else if (handler is TopDownMapInputHandler topDown)
            {
                topDown.Dispatcher = m_Dispatcher;
                topDown.CameraInputController = cameraControl;
            }

            target.SetActive(true);
            return handler;
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            m_Objects.Add(target);
            return target;
        }
    }
}

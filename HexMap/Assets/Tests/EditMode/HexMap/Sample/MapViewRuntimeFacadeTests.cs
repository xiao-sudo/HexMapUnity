using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexMap.Sample.Tests
{
    [TestFixture]
    public sealed class MapViewRuntimeFacadeTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();

        private Camera m_GameplayCamera;
        private MapInputDispatcher m_Dispatcher;
        private GameObject m_GameplayUiRoot;
        private GameObject m_TopDownUiRoot;
        private MapViewModeWiring m_Wiring;

        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < m_Objects.Count; index++)
            {
                if (m_Objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_Objects[index]);
                }
            }

            m_Objects.Clear();
        }

        [Test]
        public void InitializeDoesNotEnterTheInitialModeUntilStart()
        {
            CreateWiring();
            var runtime = new MapViewRuntimeFacade();

            var initialized = runtime.Initialize(MapViewMode.Gameplay, m_Wiring);

            Assert.That(initialized, Is.True);
            Assert.That(runtime.IsInitialized, Is.True);
            Assert.That(runtime.IsRunning, Is.False);
            Assert.That(runtime.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
            Assert.That(m_Dispatcher.ActiveCamera, Is.Null);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(MapInputChannels.None));
        }

        [Test]
        public void StartStopAndRepeatedStopOwnTheInitialRoute()
        {
            CreateWiring();
            var runtime = InitializeSuccessfully(MapViewMode.Gameplay);
            var modes = new List<MapViewMode>();
            var stopCount = 0;
            runtime.ModeChanged += modes.Add;
            runtime.RuntimeStopped += () => stopCount++;

            runtime.Start();

            Assert.That(runtime.IsRunning, Is.True);
            Assert.That(modes, Is.EqualTo(new[] { MapViewMode.Gameplay }));
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_GameplayCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapInputChannels.Gameplay));

            runtime.Stop();
            runtime.Stop();

            Assert.That(runtime.IsRunning, Is.False);
            Assert.That(stopCount, Is.EqualTo(1));
            Assert.That(m_Dispatcher.ActiveCamera, Is.Null);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(MapInputChannels.None));
        }

        [Test]
        public void SetViewModePublishesOnlyRealTransitionsAndRestartsAtInitialMode()
        {
            CreateWiring();
            var runtime = InitializeSuccessfully(MapViewMode.Gameplay);
            var modes = new List<MapViewMode>();
            runtime.ModeChanged += modes.Add;

            runtime.Start();
            runtime.SetViewMode(MapViewMode.TopDown);
            var revisionAfterTopDown = m_Dispatcher.InputRouteRevision;
            runtime.SetViewMode(MapViewMode.TopDown);
            runtime.Stop();
            runtime.Start();

            Assert.That(modes, Is.EqualTo(new[]
            {
                MapViewMode.Gameplay,
                MapViewMode.TopDown,
                MapViewMode.Gameplay,
            }));
            Assert.That(m_Dispatcher.InputRouteRevision, Is.GreaterThan(revisionAfterTopDown));
            Assert.That(runtime.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
        }

        [Test]
        public void TopDownModeUsesTheBaseCameraAndShowsOnlyTheTopDownUi()
        {
            CreateWiring();
            var runtime = InitializeSuccessfully(MapViewMode.Gameplay);

            runtime.Start();
            runtime.SetViewMode(MapViewMode.TopDown);

            Assert.That(m_GameplayCamera.enabled, Is.True);
            Assert.That(m_GameplayCamera.orthographic, Is.True);
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_GameplayCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapInputChannels.TopDown));
            Assert.That(m_GameplayUiRoot.activeSelf, Is.False);
            Assert.That(m_TopDownUiRoot.activeSelf, Is.True);
        }

        [Test]
        public void LeavingTopDownRestoresTheGameplayCameraView()
        {
            CreateWiring();
            var runtime = InitializeSuccessfully(MapViewMode.Gameplay);
            var position = new Vector3(4f, 5f, 6f);
            var rotation = Quaternion.Euler(11f, 22f, 33f);
            m_GameplayCamera.transform.SetPositionAndRotation(position, rotation);
            m_GameplayCamera.orthographic = false;
            m_GameplayCamera.fieldOfView = 55f;
            m_GameplayCamera.orthographicSize = 7f;
            m_GameplayCamera.nearClipPlane = 0.5f;
            m_GameplayCamera.farClipPlane = 444f;
            m_GameplayCamera.cullingMask = 1 << 9;

            runtime.Start();
            runtime.SetViewMode(MapViewMode.TopDown);
            runtime.SetViewMode(MapViewMode.Gameplay);

            Assert.That(m_GameplayCamera.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(m_GameplayCamera.transform.rotation, rotation), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(m_GameplayCamera.orthographic, Is.False);
            Assert.That(m_GameplayCamera.fieldOfView, Is.EqualTo(55f).Within(0.0001f));
            Assert.That(m_GameplayCamera.orthographicSize, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(m_GameplayCamera.nearClipPlane, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(m_GameplayCamera.farClipPlane, Is.EqualTo(444f).Within(0.0001f));
            Assert.That(m_GameplayCamera.cullingMask, Is.EqualTo(1 << 9));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapInputChannels.Gameplay));
        }

        [Test]
        public void InvalidWiringReturnsDiagnosticsAndCannotStart()
        {
            CreateWiring();
            var invalidWiring = new MapViewModeWiring(
                m_Wiring.GameplayCamera,
                m_Wiring.MapCamera,
                m_Wiring.GameplayUiRoot,
                m_Wiring.TopDownUiRoot,
                null);
            var runtime = new MapViewRuntimeFacade();

            LogAssert.Expect(
                LogType.Error,
                "Map view runtime initialization failed: initial mode must be Gameplay or TopDown.");
            LogAssert.Expect(
                LogType.Error,
                "Map view runtime initialization failed: Map input dispatcher is required.");

            Assert.That(runtime.Initialize((MapViewMode)99, invalidWiring), Is.False);
            Assert.That(runtime.IsInitialized, Is.False);
            Assert.That(runtime.IsRunning, Is.False);
            Assert.That(() => runtime.Start(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void DisposeStopsOnceAndRejectsFurtherUse()
        {
            CreateWiring();
            var runtime = InitializeSuccessfully(MapViewMode.Gameplay);
            var stopCount = 0;
            runtime.RuntimeStopped += () => stopCount++;
            runtime.Start();

            runtime.Dispose();
            runtime.Dispose();

            Assert.That(stopCount, Is.EqualTo(1));
            Assert.That(m_Dispatcher.ActiveCamera, Is.Null);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(MapInputChannels.None));
            Assert.That(() => runtime.Start(), Throws.TypeOf<ObjectDisposedException>());
        }

        private MapViewRuntimeFacade InitializeSuccessfully(MapViewMode initialMode)
        {
            var runtime = new MapViewRuntimeFacade();
            var initialized = runtime.Initialize(initialMode, m_Wiring);
            Assert.That(initialized, Is.True);
            return runtime;
        }

        private void CreateWiring()
        {
            m_GameplayCamera = CreateObject("Gameplay Camera").AddComponent<Camera>();
            var mapCamera = m_GameplayCamera.gameObject.AddComponent<OrthographicMapCamera>();
            var mapView = CreateObject("Hex Map View").AddComponent<HexMapView>();
            mapView.Radius = 3;
            mapView.Orientation = HexOrientation.Pointy;
            mapView.Build();
            mapCamera.HexMapView = mapView;
            m_Dispatcher = CreateObject("Map Input Dispatcher").AddComponent<MapInputDispatcher>();
            m_GameplayUiRoot = CreateObject("Gameplay UI");
            m_TopDownUiRoot = CreateObject("Top Down UI");
            m_Wiring = new MapViewModeWiring(
                m_GameplayCamera,
                mapCamera,
                m_GameplayUiRoot,
                m_TopDownUiRoot,
                m_Dispatcher);
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            m_Objects.Add(target);
            return target;
        }
    }
}

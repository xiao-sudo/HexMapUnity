using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.Sample.Tests
{
    [TestFixture]
    public sealed class MapViewRuntimeFacadeTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();

        private Camera m_GameplayCamera;
        private MapInputDispatcher m_Dispatcher;
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

            var result = runtime.Initialize(MapViewMode.Gameplay, m_Wiring);

            Assert.That(result.Succeeded, Is.True);
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

            var result = runtime.Initialize((MapViewMode)99, invalidWiring);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Is.EqualTo(new[]
            {
                "Initial mode must be Gameplay or TopDown.",
                "Map input dispatcher is required.",
            }));
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
            var result = runtime.Initialize(initialMode, m_Wiring);
            Assert.That(result.Succeeded, Is.True);
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
            var gameplayUiRoot = CreateObject("Gameplay UI");
            var topDownUiRoot = CreateObject("Top Down UI");
            m_Wiring = new MapViewModeWiring(
                m_GameplayCamera,
                mapCamera,
                gameplayUiRoot,
                topDownUiRoot,
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

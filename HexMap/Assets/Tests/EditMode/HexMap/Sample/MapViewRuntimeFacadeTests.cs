using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
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

        private GameObject m_FacadeObject;
        private MapViewRuntimeFacade m_Facade;
        private MapViewModeSwitcher m_Switcher;
        private Camera m_GameplayCamera;
        private MapInputDispatcher m_Dispatcher;

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
        public void EnableDisableAndReenableOwnTheInitialModeAndInputRoute()
        {
            CreateInactiveFacade(MapViewMode.Gameplay);
            var modes = new List<MapViewMode>();
            var stopCount = 0;
            m_Facade.ModeChanged += modes.Add;
            m_Facade.RuntimeStopped += () => stopCount++;

            m_FacadeObject.SetActive(true);

            Assert.That(m_Facade.IsConfigurationValid, Is.True);
            Assert.That(m_Facade.IsRunning, Is.True);
            Assert.That(m_Facade.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
            Assert.That(modes, Is.EqualTo(new[] { MapViewMode.Gameplay }));
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_GameplayCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapInputChannels.Gameplay));

            m_FacadeObject.SetActive(false);

            Assert.That(m_Facade.IsRunning, Is.False);
            Assert.That(stopCount, Is.EqualTo(1));
            Assert.That(m_Dispatcher.ActiveCamera, Is.Null);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(MapInputChannels.None));

            m_FacadeObject.SetActive(true);

            Assert.That(m_Facade.IsRunning, Is.True);
            Assert.That(modes, Is.EqualTo(new[] { MapViewMode.Gameplay, MapViewMode.Gameplay }));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapInputChannels.Gameplay));
        }

        [Test]
        public void DestroyingARunningFacadeStopsExactlyOnce()
        {
            CreateInactiveFacade(MapViewMode.Gameplay);
            var stopCount = 0;
            m_Facade.RuntimeStopped += () => stopCount++;
            m_FacadeObject.SetActive(true);

            UnityEngine.Object.DestroyImmediate(m_FacadeObject);
            m_Objects.Remove(m_FacadeObject);

            Assert.That(stopCount, Is.EqualTo(1));
            Assert.That(m_Dispatcher.ActiveCamera, Is.Null);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(MapInputChannels.None));
        }

        [Test]
        public void DestroyingAFacadeAfterDisableDoesNotPublishASecondStop()
        {
            CreateInactiveFacade(MapViewMode.Gameplay);
            var stopCount = 0;
            m_Facade.RuntimeStopped += () => stopCount++;
            m_FacadeObject.SetActive(true);
            m_FacadeObject.SetActive(false);

            UnityEngine.Object.DestroyImmediate(m_FacadeObject);
            m_Objects.Remove(m_FacadeObject);

            Assert.That(stopCount, Is.EqualTo(1));
            Assert.That(m_Dispatcher.ActiveCamera, Is.Null);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(MapInputChannels.None));
        }

        [Test]
        public void SetViewModeChangesModeOnceAndIgnoresTheAlreadyActiveMode()
        {
            CreateInactiveFacade(MapViewMode.Gameplay);
            var modes = new List<MapViewMode>();
            m_Facade.ModeChanged += modes.Add;
            m_FacadeObject.SetActive(true);

            m_Facade.SetViewMode(MapViewMode.TopDown);
            var revisionAfterChange = m_Dispatcher.InputRouteRevision;
            m_Facade.SetViewMode(MapViewMode.TopDown);

            Assert.That(m_Facade.CurrentMode, Is.EqualTo(MapViewMode.TopDown));
            Assert.That(modes, Is.EqualTo(new[] { MapViewMode.Gameplay, MapViewMode.TopDown }));
            Assert.That(m_Dispatcher.InputRouteRevision, Is.EqualTo(revisionAfterChange));
        }

        [Test]
        public void MissingRequiredConfigurationPreventsStartupAndExplainsTheFailure()
        {
            CreateInactiveFacade(MapViewMode.Gameplay);
            SetPrivateField("m_Dispatcher", null);
            LogAssert.Expect(LogType.Error, new Regex("MapViewRuntimeFacade configuration error: Map input dispatcher is required."));

            m_FacadeObject.SetActive(true);

            Assert.That(m_Facade.IsConfigurationValid, Is.False);
            Assert.That(m_Facade.IsRunning, Is.False);
            Assert.That(m_Facade.ValidationErrors.Count, Is.EqualTo(1));
            Assert.That(m_Facade.ValidationErrors[0], Is.EqualTo("Map input dispatcher is required."));
        }

        [Test]
        public void InvalidInitialModePreventsStartupAndPublishesDiagnostics()
        {
            CreateInactiveFacade((MapViewMode)99);
            LogAssert.Expect(LogType.Error, new Regex("MapViewRuntimeFacade configuration error: Initial mode must be Gameplay or TopDown."));

            m_FacadeObject.SetActive(true);

            Assert.That(m_Facade.IsConfigurationValid, Is.False);
            Assert.That(m_Facade.IsRunning, Is.False);
            Assert.That(m_Facade.ValidationErrors.Count, Is.EqualTo(1));
            Assert.That(m_Facade.ValidationErrors[0], Is.EqualTo("Initial mode must be Gameplay or TopDown."));
        }

        [Test]
        public void AnAlreadyActiveSwitcherBecomesAnInspectableStartupFailure()
        {
            CreateInactiveFacade(MapViewMode.Gameplay);
            m_Switcher.Configure(new MapViewModeWiring(
                m_GameplayCamera,
                m_GameplayCamera.GetComponent<OrthographicMapCamera>(),
                FindObject("Gameplay UI"),
                FindObject("Top Down UI"),
                m_Dispatcher));
            m_Switcher.SetMode(MapViewMode.Gameplay);
            LogAssert.Expect(LogType.Error, new Regex("MapViewRuntimeFacade configuration error: MapViewModeSwitcher cannot accept"));

            m_FacadeObject.SetActive(true);

            Assert.That(m_Facade.IsConfigurationValid, Is.False);
            Assert.That(m_Facade.IsRunning, Is.False);
            Assert.That(m_Facade.ValidationErrors.Count, Is.EqualTo(1));
            Assert.That(m_Facade.ValidationErrors[0], Does.Contain("MapViewModeSwitcher cannot accept"));
        }

        private void CreateInactiveFacade(MapViewMode initialMode)
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
            m_Switcher = CreateObject("Map View Mode Switcher").AddComponent<MapViewModeSwitcher>();

            m_FacadeObject = CreateObject("Map View Runtime Facade");
            m_FacadeObject.SetActive(false);
            m_Facade = m_FacadeObject.AddComponent<MapViewRuntimeFacade>();
            SetPrivateField("m_Switcher", m_Switcher);
            SetPrivateField("m_GameplayCamera", m_GameplayCamera);
            SetPrivateField("m_MapCamera", mapCamera);
            SetPrivateField("m_GameplayUiRoot", gameplayUiRoot);
            SetPrivateField("m_TopDownUiRoot", topDownUiRoot);
            SetPrivateField("m_Dispatcher", m_Dispatcher);
            SetPrivateField("m_InitialMode", initialMode);
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            m_Objects.Add(target);
            return target;
        }

        private GameObject FindObject(string name)
        {
            for (var index = 0; index < m_Objects.Count; index++)
            {
                if (m_Objects[index].name == name)
                {
                    return m_Objects[index];
                }
            }

            Assert.Fail("Expected test object '" + name + "' to exist.");
            return null;
        }

        private void SetPrivateField(string name, object value)
        {
            var field = typeof(MapViewRuntimeFacade).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Expected serialized field '" + name + "'.");
            field.SetValue(m_Facade, value);
        }
    }
}

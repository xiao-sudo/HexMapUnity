using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.Sample.Tests
{
    [TestFixture]
    public sealed class MapViewModeSwitcherTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();

        private MapViewModeSwitcher m_Switcher;
        private Camera m_GameplayCamera;
        private MapInputDispatcher m_Dispatcher;
        private GameObject m_GameplayUiRoot;
        private GameObject m_TopDownUiRoot;

        [TearDown]
        public void TearDown()
        {
            foreach (var target in m_Objects)
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }

            m_Objects.Clear();
        }

        [Test]
        public void EnteringTopDownUsesTheOneBaseCameraAndShowsTheMapUi()
        {
            CreateRig();

            m_Switcher.ApplySerializedMode();
            m_Switcher.Toggle();

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.TopDown));
            Assert.That(m_GameplayCamera.enabled, Is.True);
            Assert.That(m_GameplayCamera.orthographic, Is.True);
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_GameplayCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapInputChannels.TopDown));
            Assert.That(m_GameplayUiRoot.activeSelf, Is.False);
            Assert.That(m_TopDownUiRoot.activeSelf, Is.True);
        }

        [Test]
        public void LeavingTopDownRestoresTheCompleteGameplayView()
        {
            CreateRig();
            m_Switcher.ApplySerializedMode();

            var position = new Vector3(4f, 5f, 6f);
            var rotation = Quaternion.Euler(11f, 22f, 33f);
            m_GameplayCamera.transform.SetPositionAndRotation(position, rotation);
            m_GameplayCamera.orthographic = false;
            m_GameplayCamera.fieldOfView = 55f;
            m_GameplayCamera.orthographicSize = 7f;
            m_GameplayCamera.nearClipPlane = 0.5f;
            m_GameplayCamera.farClipPlane = 444f;
            m_GameplayCamera.cullingMask = 1 << 9;

            m_Switcher.Toggle();
            m_Switcher.Toggle();

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
            Assert.That(m_GameplayCamera.enabled, Is.True);
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
        public void StartingInTopDownStillRestoresTheAuthoredGameplayView()
        {
            CreateRig(MapViewMode.TopDown);
            var authoredPosition = m_GameplayCamera.transform.position;
            var authoredRotation = m_GameplayCamera.transform.rotation;

            m_Switcher.ApplySerializedMode();
            m_Switcher.Toggle();

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
            Assert.That(m_GameplayCamera.orthographic, Is.False);
            Assert.That(m_GameplayCamera.transform.position, Is.EqualTo(authoredPosition));
            Assert.That(Quaternion.Angle(m_GameplayCamera.transform.rotation, authoredRotation), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void RepeatedSwitchesLeaveTheBaseCameraEnabledAndTheGameplayViewUndrifted()
        {
            CreateRig();
            m_Switcher.ApplySerializedMode();
            var position = m_GameplayCamera.transform.position;
            var rotation = m_GameplayCamera.transform.rotation;

            for (var i = 0; i < 5; i++)
            {
                m_Switcher.Toggle();
                m_Switcher.Toggle();
            }

            Assert.That(m_GameplayCamera.enabled, Is.True);
            Assert.That(m_GameplayCamera.orthographic, Is.False);
            Assert.That(m_GameplayCamera.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(m_GameplayCamera.transform.rotation, rotation), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void AHalfWiredSwitcherDoesNotThrow()
        {
            m_Switcher = CreateObject("Map View Mode Switcher").AddComponent<MapViewModeSwitcher>();

            Assert.DoesNotThrow(() => m_Switcher.ApplySerializedMode());
            Assert.DoesNotThrow(() => m_Switcher.Toggle());
        }

        [Test]
        public void SetModeCanEnterTopDownBeforeSerializedModeIsApplied()
        {
            CreateRig();

            m_Switcher.SetMode(MapViewMode.TopDown);

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.TopDown));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapInputChannels.TopDown));
        }

        [Test]
        public void SetModeDoesNotRepeatAnAlreadyActiveTransition()
        {
            CreateRig();
            m_Switcher.SetMode(MapViewMode.TopDown);
            var routeRevision = m_Dispatcher.InputRouteRevision;

            m_Switcher.SetMode(MapViewMode.TopDown);

            Assert.That(m_Dispatcher.InputRouteRevision, Is.EqualTo(routeRevision));
        }

        [Test]
        public void SetModeRejectsAnUndefinedMode()
        {
            CreateRig();

            Assert.That(
                () => m_Switcher.SetMode((MapViewMode)99),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private void CreateRig(MapViewMode startMode = MapViewMode.Gameplay)
        {
            m_GameplayCamera = CreateObject("Gameplay Camera").AddComponent<Camera>();
            m_GameplayCamera.orthographic = false;
            m_GameplayCamera.transform.SetPositionAndRotation(new Vector3(2f, 3f, 4f), Quaternion.Euler(15f, 30f, 0f));

            var mapView = CreateObject("Hex Map View").AddComponent<HexMapView>();
            mapView.Radius = 3;
            mapView.Orientation = HexOrientation.Pointy;
            mapView.Build();

            var mapCamera = m_GameplayCamera.gameObject.AddComponent<OrthographicMapCamera>();
            mapCamera.HexMapView = mapView;
            mapCamera.ViewportAspect = m_GameplayCamera.aspect;

            m_Dispatcher = CreateObject("Map Input Dispatcher").AddComponent<MapInputDispatcher>();
            m_GameplayUiRoot = CreateObject("Gameplay UI");
            m_TopDownUiRoot = CreateObject("Top Down UI");
            m_Switcher = CreateObject("Map View Mode Switcher").AddComponent<MapViewModeSwitcher>();
            m_Switcher.Configure(new MapViewModeWiring(
                m_GameplayCamera,
                mapCamera,
                m_GameplayUiRoot,
                m_TopDownUiRoot,
                m_Dispatcher,
                startMode));
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            m_Objects.Add(target);
            return target;
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HexMap.Core;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace HexMap.Sample.Tests
{
    /// <remarks>
    /// <para>
    /// <b>The switcher is driven through four members and nothing else</b>: <c>Configure</c>,
    /// <c>ApplySerializedMode</c>, <c>Toggle</c> and <c>CurrentMode</c>. The mode-specific verbs are gone on
    /// purpose — the point of the refactor is that there is one way in and the modes are states behind it —
    /// so a test that wants the map view up presses the same button the scene's UnityEvent presses.
    /// </para>
    /// <para>
    /// <b>Stack membership is asserted together with the render type.</b> A camera can sit in a base
    /// camera's stack list and still never render: URP skips a stack member whose render type is not
    /// <see cref="CameraRenderType.Overlay"/>, warning every frame, and a fresh
    /// <c>UniversalAdditionalCameraData</c> is a base camera. Membership alone would go green while the UI
    /// stayed invisible, so both are pinned.
    /// </para>
    /// <para>
    /// <b>The start mode is written through <see cref="SerializedObject"/> using the field name as a
    /// string</b>, the way an inspector or a saved scene writes it. Everything else about the rig goes
    /// through <see cref="MapViewModeSwitcher.Configure"/>, which is the one place the references are
    /// accepted.
    /// </para>
    /// <para>
    /// <b><c>Start</c> cannot run in edit mode</b> (there are no start callbacks), so the tests drive
    /// <c>ApplySerializedMode</c>, the seam it calls, or simply press the button first — both are entry
    /// paths the runtime uses.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class MapViewModeSwitcherTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();

        private MapViewModeSwitcher m_Switcher;
        private Camera m_GameplayCamera;
        private Camera m_TopDownCamera;
        private Camera m_UiCamera;
        private MapClickDispatcher m_Dispatcher;
        private GameObject m_GameplayUiRoot;
        private GameObject m_TopDownUiRoot;

        [TearDown]
        public void TearDown()
        {
            foreach (var target in m_Objects)
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }

            m_Objects.Clear();
            m_Switcher = null;
            m_GameplayCamera = null;
            m_TopDownCamera = null;
            m_UiCamera = null;
            m_Dispatcher = null;
            m_GameplayUiRoot = null;
            m_TopDownUiRoot = null;
        }

        /// <summary>
        /// A fully wired switcher: two base cameras, an overlay UI camera, a dispatcher and both UI roots.
        /// </summary>
        private void CreateRig(MapViewMode startMode = MapViewMode.Gameplay)
        {
            m_GameplayCamera = CreateObject("Gameplay Camera").AddComponent<Camera>();
            m_TopDownCamera = CreateObject("Top Down Camera").AddComponent<Camera>();
            m_TopDownCamera.orthographic = true;
            m_TopDownCamera.aspect = 9f / 16f;
            m_UiCamera = CreateObject("UI Camera").AddComponent<Camera>();
            m_Dispatcher = CreateObject("Map Click Dispatcher").AddComponent<MapClickDispatcher>();
            m_GameplayUiRoot = CreateObject("Gameplay UI");
            m_TopDownUiRoot = CreateObject("Top Down UI");

            m_Switcher = CreateObject("Map View Mode Switcher").AddComponent<MapViewModeSwitcher>();
            m_Switcher.Configure(new MapViewModeWiring(
                m_GameplayCamera,
                m_TopDownCamera,
                m_UiCamera,
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

        /// <summary>
        /// Whether a camera is a member of a base camera's stack. Only base cameras are ever asked: asking
        /// an overlay for its stack logs a warning and answers with null.
        /// </summary>
        private static bool IsInStackOf(Camera baseCamera, Camera overlay)
        {
            return baseCamera.GetUniversalAdditionalCameraData().cameraStack.Contains(overlay);
        }

        /// <summary>
        /// Writes the serialized start mode the way a scene does. The field is private and the runtime mode
        /// is deliberately read-only, so the fixture must go through the serializer rather than a setter.
        /// </summary>
        private static void SetSerializedStartMode(MapViewModeSwitcher switcher, MapViewMode mode)
        {
            var serialized = new SerializedObject(switcher);
            var property = serialized.FindProperty("m_StartMode");
            Assert.That(
                property,
                Is.Not.Null,
                "MapViewModeSwitcher must keep a serialized field named 'm_StartMode'; the fixture writes it");
            property.enumValueIndex = (int)mode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string SharedCameraMessage()
        {
            return "MapViewModeSwitcher has the same camera wired as the UI camera and as a base camera; "
                + "the camera stack was left alone. Wire a separate overlay camera for the UI.";
        }

        /// <summary>Presses the map button, which is the only thing the scene itself ever does.</summary>
        private void PressTheMapButton()
        {
            m_Switcher.Toggle();
        }

        [Test]
        public void PressingTheMapButtonPutsTheWholeMapViewUp()
        {
            CreateRig();

            PressTheMapButton();

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.TopDown));
            Assert.That(m_GameplayCamera.enabled, Is.False);
            Assert.That(m_TopDownCamera.enabled, Is.True);
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_TopDownCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.TopDown));
            Assert.That(m_TopDownUiRoot.activeSelf, Is.True);
            Assert.That(m_GameplayUiRoot.activeSelf, Is.False);
        }

        [Test]
        public void PressingItAgainPutsGameplayBack()
        {
            CreateRig();
            PressTheMapButton();

            PressTheMapButton();

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
            Assert.That(m_GameplayCamera.enabled, Is.True);
            Assert.That(m_TopDownCamera.enabled, Is.False);
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_GameplayCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.Gameplay));
            Assert.That(m_GameplayUiRoot.activeSelf, Is.True);
            Assert.That(m_TopDownUiRoot.activeSelf, Is.False);
        }

        [Test]
        public void TheUiCameraIsAnOverlayInExactlyTheStackThatRenders()
        {
            CreateRig();

            PressTheMapButton();

            Assert.That(
                m_UiCamera.GetUniversalAdditionalCameraData().renderType,
                Is.EqualTo(CameraRenderType.Overlay),
                "a stack member that is not an overlay is skipped by URP and renders nothing");
            Assert.That(IsInStackOf(m_TopDownCamera, m_UiCamera), Is.True);
            Assert.That(IsInStackOf(m_GameplayCamera, m_UiCamera), Is.False);

            PressTheMapButton();

            Assert.That(IsInStackOf(m_GameplayCamera, m_UiCamera), Is.True);
            Assert.That(
                IsInStackOf(m_TopDownCamera, m_UiCamera),
                Is.False,
                "an overlay may belong to one stack only, and the disabled camera's stack would blank the UI");
        }

        [Test]
        public void TheUiCameraIsNeverMovedDisabledOrReparented()
        {
            CreateRig();
            var position = m_UiCamera.transform.position;
            var rotation = m_UiCamera.transform.rotation;
            var parent = m_UiCamera.transform.parent;

            PressTheMapButton();
            PressTheMapButton();

            Assert.That(m_UiCamera.enabled, Is.True);
            Assert.That(m_UiCamera.transform.position, Is.EqualTo(position));
            Assert.That(m_UiCamera.transform.rotation, Is.EqualTo(rotation));
            Assert.That(m_UiCamera.transform.parent, Is.EqualTo(parent));
        }

        [Test]
        public void ComingBackFromTheMapViewRestoresTheGameplayCameraExactly()
        {
            // The camera the map view has to give back is the one gameplay was using when it was taken away,
            // which means gameplay has to have been entered at least once. A scene whose start mode is the map
            // view has nothing to give back, and that case has its own test below.
            CreateRig();
            m_Switcher.ApplySerializedMode();

            var position = new Vector3(4f, 5f, 6f);
            var rotation = Quaternion.Euler(11f, 22f, 33f);
            m_GameplayCamera.transform.position = position;
            m_GameplayCamera.transform.rotation = rotation;
            m_GameplayCamera.fieldOfView = 55f;
            m_GameplayCamera.orthographicSize = 7f;

            // Two presses: gameplay → map view (which is where the pose is remembered) → gameplay.
            PressTheMapButton();
            PressTheMapButton();

            Assert.That(m_GameplayCamera.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(m_GameplayCamera.transform.rotation, rotation), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(m_GameplayCamera.fieldOfView, Is.EqualTo(55f).Within(0.0001f));
            Assert.That(m_GameplayCamera.orthographicSize, Is.EqualTo(7f).Within(0.0001f));
        }

        [Test]
        public void PressingTheMapButtonTwiceKeepsTheOriginalPose()
        {
            // Start the map view from gameplay, so the pose gameplay was using gets remembered.
            CreateRig();
            m_Switcher.ApplySerializedMode();
            var captured = m_GameplayCamera.transform.position;

            m_Switcher.Toggle();

            // The camera moves while the map view is up. Gameplay code can do that — it knows nothing about
            // view modes — and it is not the camera the map view is looking through.
            var moved = new Vector3(9f, 9f, 9f);
            m_GameplayCamera.transform.position = moved;

            // A repeated press while already in the map view is not a mode change, so it must not remember the
            // moved camera in place of the pose the player is owed.
            m_Switcher.Toggle();
            m_Switcher.Toggle();
            m_Switcher.Toggle();

            Assert.That(
                m_GameplayCamera.transform.position,
                Is.EqualTo(captured),
                "the pose gameplay was using must survive a press that does not change the mode");
        }

        [Test]
        public void PressingTheButtonBeforeAnythingElseEntersTheStartMode()
        {
            // No ApplySerializedMode first: the button is the first thing that happens, which is what a
            // player does. The start mode decides which way the first press goes.
            CreateRig(MapViewMode.TopDown);

            PressTheMapButton();

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
            Assert.That(m_GameplayCamera.enabled, Is.True);
            Assert.That(m_TopDownCamera.enabled, Is.False);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.Gameplay));
        }

        [Test]
        public void PressingTheButtonTwiceIsNotAModeChange()
        {
            // Start the map view from gameplay, then press it once more while it is already up.
            CreateRig();
            m_Switcher.ApplySerializedMode();
            m_Switcher.Toggle();

            var position = m_TopDownCamera.transform.position;

            // The same request twice. Nothing about the presentation may move, and the click channel in
            // particular must not be re-pushed.
            m_Switcher.Toggle();
            m_Switcher.Toggle();

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.TopDown));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.TopDown));
            Assert.That(m_TopDownUiRoot.activeSelf, Is.True);
            Assert.That(m_GameplayUiRoot.activeSelf, Is.False);
            Assert.That(m_TopDownCamera.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void ASceneThatStartsInTopDownModeIsAppliedWithoutSwitching()
        {
            CreateRig();
            SetSerializedStartMode(m_Switcher, MapViewMode.TopDown);

            m_Switcher.ApplySerializedMode();

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.TopDown));
            Assert.That(m_TopDownCamera.enabled, Is.True);
            Assert.That(m_GameplayCamera.enabled, Is.False);
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_TopDownCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.TopDown));
            Assert.That(IsInStackOf(m_TopDownCamera, m_UiCamera), Is.True);
            Assert.That(m_TopDownUiRoot.activeSelf, Is.True);
            Assert.That(m_GameplayUiRoot.activeSelf, Is.False);
        }

        [Test]
        public void LeavingAMapViewThatWasNeverEnteredLeavesTheGameplayCameraWhereItIs()
        {
            // The scene is authored to start in the map view, so nothing ever captured a gameplay pose.
            // Returning to gameplay has nothing to restore and must not invent one.
            CreateRig(MapViewMode.TopDown);
            m_Switcher.ApplySerializedMode();
            var position = new Vector3(7f, 8f, 9f);
            m_GameplayCamera.transform.position = position;

            PressTheMapButton();

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
            Assert.That(m_GameplayCamera.enabled, Is.True);
            Assert.That(m_TopDownCamera.enabled, Is.False);
            Assert.That(
                m_GameplayCamera.transform.position,
                Is.EqualTo(position),
                "a scene that started toggled on has no capture, so there is nothing to restore");
        }

        [Test]
        public void RepeatedSwitchesLeaveNoDriftBehind()
        {
            CreateRig();
            var position = m_GameplayCamera.transform.position;
            var rotation = m_GameplayCamera.transform.rotation;

            for (var i = 0; i < 5; i++)
            {
                PressTheMapButton();
                PressTheMapButton();
            }

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
            Assert.That(m_GameplayCamera.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(m_GameplayCamera.transform.rotation, rotation), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(IsInStackOf(m_GameplayCamera, m_UiCamera), Is.True);
            Assert.That(IsInStackOf(m_TopDownCamera, m_UiCamera), Is.False);
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_GameplayCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.Gameplay));
        }

        [Test]
        public void TheMapCameraAndTheGesturesFollowTheModeWithoutTheSwitcherOwningThem()
        {
            // The switcher no longer knows the gestures exist: the map camera's own enabled flag is the only
            // thing that decides whether a gesture can act (the gesture components check it themselves).
            // What is pinned here is that the mode still decides when that camera is alive.
            CreateRig();
            var mapCamera = CreateMapCamera();

            m_Switcher.ApplySerializedMode();

            Assert.That(mapCamera.Camera.enabled, Is.False, "gameplay puts the map camera away");

            PressTheMapButton();

            Assert.That(mapCamera.Camera.enabled, Is.True, "the map view brings it back");
        }

        [Test]
        public void AHalfWiredSwitcherDoesNotThrow()
        {
            m_Switcher = CreateObject("Map View Mode Switcher").AddComponent<MapViewModeSwitcher>();

            Assert.DoesNotThrow(() => m_Switcher.Toggle());
            Assert.DoesNotThrow(() => m_Switcher.Toggle());
            Assert.DoesNotThrow(() => m_Switcher.ApplySerializedMode());

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
        }

        [Test]
        public void WiringTheUiCameraAsABaseCameraTooIsReportedOnceAndLeavesTheStackAlone()
        {
            CreateRig();
            m_Switcher.Configure(new MapViewModeWiring(
                m_GameplayCamera,
                m_TopDownCamera,
                m_TopDownCamera,
                m_GameplayUiRoot,
                m_TopDownUiRoot,
                m_Dispatcher,
                MapViewMode.Gameplay));

            // One report for two switches: the mistake is in the scene, not in the switch, so repeating it
            // on every toggle would only bury the console. An extra report would fail this test.
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(SharedCameraMessage())));

            Assert.DoesNotThrow(() => PressTheMapButton());
            Assert.DoesNotThrow(() => PressTheMapButton());

            Assert.That(m_Switcher.CurrentMode, Is.EqualTo(MapViewMode.Gameplay));
            Assert.That(
                m_TopDownCamera.GetUniversalAdditionalCameraData().renderType,
                Is.EqualTo(CameraRenderType.Base),
                "a camera wired into two roles must not be talked into being an overlay");
        }

        /// <summary>
        /// A built map, which the map camera needs before it can frame anything. Only the camera's own
        /// enabled flag is of interest here; the switcher no longer takes a map camera at all.
        /// </summary>
        private OrthographicMapCamera CreateMapCamera()
        {
            var mapView = CreateObject("Hex Map View").AddComponent<HexMapView>();
            mapView.Radius = 3;
            mapView.Orientation = HexOrientation.Pointy;
            mapView.Build();

            var mapCamera = m_TopDownCamera.gameObject.AddComponent<OrthographicMapCamera>();
            mapCamera.HexMapView = mapView;
            mapCamera.Camera = m_TopDownCamera;

            string error;
            Assert.That(mapCamera.TryRefresh(out error), Is.True, error);
            return mapCamera;
        }
    }
}

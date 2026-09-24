using System.Collections.Generic;
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
    /// <b>Stack membership is asserted together with the render type.</b> A camera can sit in a base
    /// camera's stack list and still never render: URP skips a stack member whose render type is not
    /// <see cref="CameraRenderType.Overlay"/>, warning every frame, and a fresh
    /// <c>UniversalAdditionalCameraData</c> is a base camera. Membership alone would go green while the UI
    /// stayed invisible, so both are pinned.
    /// </para>
    /// <para>
    /// <b>The mode and the focus zoom are written through <see cref="SerializedObject"/> using the field
    /// names as strings</b>, the way an inspector or a saved scene writes them, because
    /// <see cref="MapViewModeSwitcher.IsTopDown"/> is deliberately read-only: the switcher is the only
    /// thing allowed to change the mode.
    /// </para>
    /// <para>
    /// <b><c>Start</c> cannot run in edit mode</b> (there are no start callbacks), so the tests drive the
    /// seam it calls. That is the same work the first frame would do.
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
                    Object.DestroyImmediate(target);
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
        private void CreateRig()
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
            m_Switcher.GameplayCamera = m_GameplayCamera;
            m_Switcher.TopDownCamera = m_TopDownCamera;
            m_Switcher.UiCamera = m_UiCamera;
            m_Switcher.Dispatcher = m_Dispatcher;
            m_Switcher.GameplayUiRoot = m_GameplayUiRoot;
            m_Switcher.TopDownUiRoot = m_TopDownUiRoot;
        }

        /// <summary>
        /// A built map, which the map camera needs before it can focus at all. Without framing the focus
        /// request fails, which is a different test.
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

        private static void SetSerializedTopDown(MapViewModeSwitcher switcher, bool isTopDown)
        {
            var serialized = new SerializedObject(switcher);
            RequireProperty(serialized, "m_IsTopDown").boolValue = isTopDown;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedFocusZoom(MapViewModeSwitcher switcher, float zoom)
        {
            var serialized = new SerializedObject(switcher);
            RequireProperty(serialized, "m_FocusZoom").floatValue = zoom;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string fieldName)
        {
            var property = serialized.FindProperty(fieldName);
            Assert.That(
                property,
                Is.Not.Null,
                "MapViewModeSwitcher must keep a serialized field named '" + fieldName + "'; the fixture writes it");
            return property;
        }

        /// <summary>
        /// Mirrors the messages the switcher logs, so an expectation stays in step with the source instead
        /// of drifting into a substring that happens to still match.
        /// </summary>
        private static string FocusFailureMessage()
        {
            return "MapViewModeSwitcher could not focus the map view: Refresh the camera before focusing.";
        }

        private static string SharedCameraMessage()
        {
            return "MapViewModeSwitcher has the same camera wired as the UI camera and as a base camera; "
                + "the camera stack was left alone. Wire a separate overlay camera for the UI.";
        }

        [Test]
        public void EnteringTopDownSwapsTheCamerasTheChannelAndTheUi()
        {
            CreateRig();

            m_Switcher.EnterTopDown();

            Assert.That(m_Switcher.IsTopDown, Is.True);
            Assert.That(m_GameplayCamera.enabled, Is.False);
            Assert.That(m_TopDownCamera.enabled, Is.True);
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_TopDownCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.TopDown));
            Assert.That(m_TopDownUiRoot.activeSelf, Is.True);
            Assert.That(m_GameplayUiRoot.activeSelf, Is.False);
        }

        [Test]
        public void ExitingTopDownPushesTheGameplayCameraAndChannelBack()
        {
            CreateRig();
            m_Switcher.EnterTopDown();

            m_Switcher.ExitTopDown();

            Assert.That(m_Switcher.IsTopDown, Is.False);
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

            m_Switcher.EnterTopDown();

            Assert.That(
                m_UiCamera.GetUniversalAdditionalCameraData().renderType,
                Is.EqualTo(CameraRenderType.Overlay),
                "a stack member that is not an overlay is skipped by URP and renders nothing");
            Assert.That(IsInStackOf(m_TopDownCamera, m_UiCamera), Is.True);
            Assert.That(IsInStackOf(m_GameplayCamera, m_UiCamera), Is.False);

            m_Switcher.ExitTopDown();

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

            m_Switcher.EnterTopDown();
            m_Switcher.ExitTopDown();

            Assert.That(m_UiCamera.enabled, Is.True);
            Assert.That(m_UiCamera.transform.position, Is.EqualTo(position));
            Assert.That(m_UiCamera.transform.rotation, Is.EqualTo(rotation));
            Assert.That(m_UiCamera.transform.parent, Is.EqualTo(parent));
        }

        [Test]
        public void ExitingTopDownRestoresTheGameplayCameraExactly()
        {
            CreateRig();
            var position = new Vector3(4f, 5f, 6f);
            var rotation = Quaternion.Euler(11f, 22f, 33f);
            m_GameplayCamera.transform.position = position;
            m_GameplayCamera.transform.rotation = rotation;
            m_GameplayCamera.fieldOfView = 55f;
            m_GameplayCamera.orthographicSize = 7f;

            m_Switcher.EnterTopDown();

            // Anything that moves the camera while the map view is up must not survive the return: the
            // snapshot taken on the way in is what "exactly where it was" means.
            m_GameplayCamera.transform.position = new Vector3(-1f, -2f, -3f);
            m_GameplayCamera.transform.rotation = Quaternion.identity;
            m_GameplayCamera.fieldOfView = 20f;
            m_GameplayCamera.orthographicSize = 1f;

            m_Switcher.ExitTopDown();

            Assert.That(m_GameplayCamera.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(m_GameplayCamera.transform.rotation, rotation), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(m_GameplayCamera.fieldOfView, Is.EqualTo(55f).Within(0.0001f));
            Assert.That(m_GameplayCamera.orthographicSize, Is.EqualTo(7f).Within(0.0001f));
        }

        [Test]
        public void EnteringTopDownTwiceKeepsTheOriginalSnapshot()
        {
            CreateRig();
            var position = m_GameplayCamera.transform.position;

            m_Switcher.EnterTopDown();
            m_GameplayCamera.transform.position = new Vector3(9f, 9f, 9f);
            m_Switcher.EnterTopDown();
            m_Switcher.ExitTopDown();

            Assert.That(
                m_GameplayCamera.transform.position,
                Is.EqualTo(position),
                "a second press of the map button must not re-snapshot the moved camera");
        }

        [Test]
        public void ToggleFlipsTheModeBothWays()
        {
            CreateRig();

            m_Switcher.Toggle();

            Assert.That(m_Switcher.IsTopDown, Is.True);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.TopDown));

            m_Switcher.Toggle();

            Assert.That(m_Switcher.IsTopDown, Is.False);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.Gameplay));
        }

        [Test]
        public void RepeatedSwitchesLeaveNoDriftBehind()
        {
            CreateRig();
            var position = m_GameplayCamera.transform.position;
            var rotation = m_GameplayCamera.transform.rotation;

            for (var i = 0; i < 5; i++)
            {
                m_Switcher.Toggle();
                m_Switcher.Toggle();
            }

            Assert.That(m_Switcher.IsTopDown, Is.False);
            Assert.That(m_GameplayCamera.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(m_GameplayCamera.transform.rotation, rotation), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(IsInStackOf(m_GameplayCamera, m_UiCamera), Is.True);
            Assert.That(IsInStackOf(m_TopDownCamera, m_UiCamera), Is.False);
            Assert.That(m_Dispatcher.ActiveCamera, Is.EqualTo(m_GameplayCamera));
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.Gameplay));
        }

        [Test]
        public void ASceneThatStartsInTopDownModeIsAppliedWithoutSwitching()
        {
            CreateRig();
            SetSerializedTopDown(m_Switcher, true);

            m_Switcher.ApplySerializedMode();

            Assert.That(m_Switcher.IsTopDown, Is.True);
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
            CreateRig();
            SetSerializedTopDown(m_Switcher, true);
            m_Switcher.ApplySerializedMode();
            var position = new Vector3(7f, 8f, 9f);
            m_GameplayCamera.transform.position = position;

            m_Switcher.ExitTopDown();

            Assert.That(m_Switcher.IsTopDown, Is.False);
            Assert.That(m_GameplayCamera.enabled, Is.True);
            Assert.That(m_TopDownCamera.enabled, Is.False);
            Assert.That(
                m_GameplayCamera.transform.position,
                Is.EqualTo(position),
                "a scene that started toggled on has no snapshot, so there is nothing to restore");
        }

        [Test]
        public void EnteringTopDownAimsTheMapCameraAtTheFocusTargetAndZoomsIn()
        {
            CreateRig();
            var mapCamera = CreateMapCamera();
            SetSerializedFocusZoom(m_Switcher, 4f);
            var target = CreateObject("Focus Target");
            target.transform.position = new Vector3(2f, 0f, 1f);
            m_Switcher.MapCamera = mapCamera;
            m_Switcher.FocusTarget = target.transform;

            m_Switcher.EnterTopDown();

            Assert.That(mapCamera.HasFocus, Is.True);
            Assert.That(mapCamera.Focus, Is.EqualTo(new Vector2(2f, 1f)), "the XZ plane reads x and z");
            Assert.That(mapCamera.Zoom, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void WithoutAFocusTargetTheMapViewOpensTheWayThePlayerLeftIt()
        {
            CreateRig();
            var mapCamera = CreateMapCamera();
            mapCamera.SetZoomImmediate(2f);
            m_Switcher.MapCamera = mapCamera;

            m_Switcher.EnterTopDown();

            Assert.That(mapCamera.HasFocus, Is.False);
            Assert.That(mapCamera.Zoom, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void AFocusTargetThatCannotBeFocusedWarnsAndStillSwitches()
        {
            CreateRig();

            // A map camera that was never refreshed cannot focus. This is the "the map view is up but the
            // camera never moved" case, which has to be reported instead of passing as a normal switch.
            var mapCamera = m_TopDownCamera.gameObject.AddComponent<OrthographicMapCamera>();
            var target = CreateObject("Focus Target");
            m_Switcher.MapCamera = mapCamera;
            m_Switcher.FocusTarget = target.transform;

            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(FocusFailureMessage())));

            m_Switcher.EnterTopDown();

            Assert.That(m_Switcher.IsTopDown, Is.True, "a failed focus must not leave the mode half switched");
            Assert.That(m_TopDownCamera.enabled, Is.True);
            Assert.That(m_Dispatcher.ActiveChannel, Is.EqualTo(SampleMapClickChannels.TopDown));
            Assert.That(m_TopDownUiRoot.activeSelf, Is.True);
        }

        [Test]
        public void TheMapGesturesAreOnlyLiveInTopDownMode()
        {
            CreateRig();
            var drag = CreateObject("Map Drag Input").AddComponent<OrthographicMapDragInput>();
            var zoom = CreateObject("Map Zoom Input").AddComponent<OrthographicMapZoomInput>();
            m_Switcher.MapDragInput = drag;
            m_Switcher.MapZoomInput = zoom;

            m_Switcher.ApplySerializedMode();

            // The gestures drive the map camera, which gameplay has just disabled: left live, a gameplay
            // drag would move the view the player is given back on the way out.
            Assert.That(drag.IsEnabled, Is.False);
            Assert.That(zoom.IsEnabled, Is.False);

            m_Switcher.EnterTopDown();

            Assert.That(drag.IsEnabled, Is.True);
            Assert.That(zoom.IsEnabled, Is.True);

            m_Switcher.ExitTopDown();

            Assert.That(drag.IsEnabled, Is.False);
            Assert.That(zoom.IsEnabled, Is.False);
        }

        [Test]
        public void AHalfWiredSwitcherDoesNotThrow()
        {
            m_Switcher = CreateObject("Map View Mode Switcher").AddComponent<MapViewModeSwitcher>();

            Assert.DoesNotThrow(() => m_Switcher.Toggle());
            Assert.DoesNotThrow(() => m_Switcher.Toggle());
            Assert.DoesNotThrow(() => m_Switcher.ApplySerializedMode());

            Assert.That(m_Switcher.IsTopDown, Is.False);
        }

        [Test]
        public void WiringTheUiCameraAsABaseCameraTooIsReportedOnceAndLeavesTheStackAlone()
        {
            CreateRig();
            m_Switcher.UiCamera = m_TopDownCamera;

            // One report for two switches: the mistake is in the scene, not in the switch, so repeating it
            // on every toggle would only bury the console. An extra report would fail this test.
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(SharedCameraMessage())));

            Assert.DoesNotThrow(() => m_Switcher.Toggle());
            Assert.DoesNotThrow(() => m_Switcher.Toggle());

            Assert.That(m_Switcher.IsTopDown, Is.False);
            Assert.That(
                m_TopDownCamera.GetUniversalAdditionalCameraData().renderType,
                Is.EqualTo(CameraRenderType.Base),
                "a camera wired into two roles must not be talked into being an overlay");
        }
    }
}

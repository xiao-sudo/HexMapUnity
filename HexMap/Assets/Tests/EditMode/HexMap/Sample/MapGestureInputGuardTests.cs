using System.Collections.Generic;
using System.Reflection;
using HexMap.Core;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.Sample.Tests
{
    /// <remarks>
    /// <para>
    /// <b>These tests do not prove the guard's camera term, and cannot.</b> Neither component can be handed
    /// input: <c>Input.GetMouseButton</c> is false, <c>Input.touchCount</c> is zero and
    /// <c>Input.mouseScrollDelta</c> is zero in edit mode, and the frame callback is private, so it is
    /// invoked directly. Under those conditions "the camera is off" and "the camera is on but the player is
    /// not touching anything" take different branches and arrive at the same observable state: camera
    /// unmoved, gesture flags false. An earlier version of this fixture claimed to tell them apart; it was
    /// wrong, and the assertion that tried failed. Treat every test here as "the component does not throw
    /// and does not keep a gesture in flight", never as "the guard did it".
    /// </para>
    /// <para>
    /// <b>The guard's real payoff is asserted by hand on the ticket:</b> a wheel notch or a drag during
    /// gameplay no longer moves a camera nobody can see, and the view the player gets back on the way out is
    /// the view they left.
    /// </para>
    /// <para>
    /// <b>The gesture state is injected through reflection</b>, because there is no input event that could
    /// create it in edit mode. This matches the existing fixtures in this repository, which read and write
    /// private fields the same way (<c>DecorationViewEditModeTests</c>, <c>DecorationRenderingTests</c>).
    /// The assertions themselves are about observable state: the gesture flags and the camera's
    /// <c>Center</c> / <c>Zoom</c>.
    /// </para>
    /// <para>
    /// <b>A note on the captured pinch values.</b> Ending a gesture only clears its flag; the captured
    /// anchor, start distance and start zoom are left as they were and are harmless, because they are only
    /// read while the flag is set and are overwritten when the next gesture starts. Asserting that they
    /// were cleared would be asserting something the component never promised.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class MapGestureInputGuardTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();

        private Camera m_Camera;
        private OrthographicMapCamera m_MapCamera;
        private OrthographicMapDragInput m_Drag;
        private OrthographicMapZoomInput m_Zoom;

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
            m_Camera = null;
            m_MapCamera = null;
            m_Drag = null;
            m_Zoom = null;
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            m_Objects.Add(target);
            return target;
        }

        /// <summary>
        /// A camera with a built map behind it, which is what <see cref="OrthographicMapCamera"/> needs
        /// before it will accept any framing or zoom request at all. The camera component starts enabled,
        /// so a guard that never looks at it looks exactly like a working gesture.
        /// </summary>
        private void CreateMapCamera()
        {
            var mapView = CreateObject("Hex Map View").AddComponent<HexMapView>();
            mapView.Radius = 3;
            mapView.Orientation = HexOrientation.Pointy;
            mapView.Build();

            m_Camera = CreateObject("Top Down Camera").AddComponent<Camera>();
            m_Camera.orthographic = true;
            m_Camera.aspect = 9f / 16f;

            m_MapCamera = m_Camera.gameObject.AddComponent<OrthographicMapCamera>();
            m_MapCamera.HexMapView = mapView;
            m_MapCamera.Camera = m_Camera;

            string error;
            Assert.That(m_MapCamera.TryRefresh(out error), Is.True, error);
        }

        private void CreateDragInput()
        {
            m_Drag = CreateObject("Map Drag Input").AddComponent<OrthographicMapDragInput>();
            m_Drag.MapCamera = m_MapCamera;
        }

        private void CreateZoomInput()
        {
            m_Zoom = CreateObject("Map Zoom Input").AddComponent<OrthographicMapZoomInput>();
            m_Zoom.MapCamera = m_MapCamera;
        }

        /// <summary>
        /// Runs the component's frame callback. There is no frame loop in edit mode, and the callback is
        /// private, so it is called directly rather than by enabling the component and waiting.
        /// </summary>
        private static void RunFrame(MonoBehaviour component)
        {
            var update = component.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null, "the components must keep a private Update method");
            update.Invoke(component, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "the components must keep a private field named '" + fieldName + "'");
            field.SetValue(target, value);
        }

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "the components must keep a private field named '" + fieldName + "'");
            return (T)field.GetValue(target);
        }

        /// <summary>
        /// Puts the drag component in the middle of a gesture: already dragging, with a remembered pointer
        /// position to measure the next delta against. This is the state the component is in on every frame
        /// after the first one of a drag.
        /// </summary>
        private void BeginDragGesture()
        {
            SetPrivateField(m_Drag, "m_IsDragging", true);
            SetPrivateField(m_Drag, "m_PreviousPointerPosition", new Vector3(10f, 10f, 0f));
        }

        /// <summary>
        /// Puts the zoom component in the middle of a pinch: an active gesture with a captured anchor, a
        /// captured start zoom and a captured start distance, which is the state it is in on every frame of
        /// a pinch after the first one.
        /// </summary>
        private void BeginPinchGesture(float startZoom, float startDistance)
        {
            SetPrivateField(m_Zoom, "m_IsGestureActive", true);
            SetPrivateField(m_Zoom, "m_AnchorViewport", Vector2.zero);
            SetPrivateField(m_Zoom, "m_PinchStartZoom", startZoom);
            SetPrivateField(m_Zoom, "m_PinchStartDistance", startDistance);
        }

        [Test]
        public void ADragThatWasInFlightIsNotHeldOntoWhileTheCameraIsOff()
        {
            CreateMapCamera();
            CreateDragInput();
            BeginDragGesture();

            // The camera goes away mid-gesture, which is what the mode switch does. Everything asserted
            // below is also true one frame later with the camera live, because in edit mode no button is
            // ever held — so this pins the state a switch must not leave behind, not which branch ran.
            m_Camera.enabled = false;
            RunFrame(m_Drag);

            Assert.That(ReadPrivateField<bool>(m_Drag, "m_IsDragging"), Is.False);
            Assert.That(m_MapCamera.Center, Is.EqualTo(Vector2.zero), "a dead camera must not be panned");

            // And the camera coming back must find a component that is not still mid-drag: a remembered
            // pointer position that outlives the switch is the thing that would make the first frame back
            // jump the view.
            m_Camera.enabled = true;
            RunFrame(m_Drag);

            Assert.That(ReadPrivateField<bool>(m_Drag, "m_IsDragging"), Is.False);
            Assert.That(m_MapCamera.Center, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void APinchOnACameraThatIsOffDoesNotZoomAndGivesUpItsGesture()
        {
            CreateMapCamera();
            CreateZoomInput();
            m_MapCamera.Zoom = 5f;

            // A pinch captured its anchor, its start zoom and its start distance while the camera was still
            // live — the state a gesture is in when the player opens the map view mid-pinch.
            BeginPinchGesture(5f, 100f);
            m_Camera.enabled = false;

            RunFrame(m_Zoom);

            Assert.That(
                ReadPrivateField<bool>(m_Zoom, "m_IsGestureActive"),
                Is.False,
                "a pinch that started before the switch must not resume against its captured start zoom");
            Assert.That(m_MapCamera.Zoom, Is.EqualTo(5f).Within(0.0001f), "a dead camera must not be zoomed");
        }

        [Test]
        public void SwitchingTheCameraOffAndOnDoesNotResumeTheOldGestureOnTheNextFrame()
        {
            CreateMapCamera();
            CreateDragInput();
            CreateZoomInput();
            BeginDragGesture();
            BeginPinchGesture(5f, 100f);

            // The real sequence: a gesture is in flight, the player opens the whole map view, then comes
            // back. What has to be true afterwards is that neither component is still holding the gesture
            // it had — otherwise the first frame after the camera returns measures against a pointer
            // position and a start zoom the player has long forgotten. The camera itself is not asserted
            // here: it never moves in edit mode either way, and the two tests above already pin that.
            m_Camera.enabled = false;
            RunFrame(m_Drag);
            RunFrame(m_Zoom);

            m_Camera.enabled = true;
            RunFrame(m_Drag);
            RunFrame(m_Zoom);

            Assert.That(ReadPrivateField<bool>(m_Drag, "m_IsDragging"), Is.False);
            Assert.That(ReadPrivateField<bool>(m_Zoom, "m_IsGestureActive"), Is.False);
        }

        [Test]
        public void AFrameOnALiveCameraStillRunsWithoutThrowing()
        {
            CreateMapCamera();
            CreateDragInput();
            CreateZoomInput();

            // The odd assertion out: with no input to inject, a live frame has nothing to do, so all this
            // can prove is that the guard did not become a blanket refusal. The other half of the guard —
            // that real input still moves a live camera — is the manual check on the ticket.
            Assert.DoesNotThrow(() => RunFrame(m_Drag));
            Assert.DoesNotThrow(() => RunFrame(m_Zoom));
        }

        [Test]
        public void AHalfWiredGestureComponentDoesNotThrow()
        {
            // No camera assigned at all: the null half of the guard, which used to be the whole guard.
            m_Drag = CreateObject("Map Drag Input").AddComponent<OrthographicMapDragInput>();
            m_Zoom = CreateObject("Map Zoom Input").AddComponent<OrthographicMapZoomInput>();

            BeginDragGesture();
            BeginPinchGesture(5f, 100f);

            Assert.DoesNotThrow(() => RunFrame(m_Drag));
            Assert.DoesNotThrow(() => RunFrame(m_Zoom));

            Assert.That(ReadPrivateField<bool>(m_Drag, "m_IsDragging"), Is.False);
            Assert.That(ReadPrivateField<bool>(m_Zoom, "m_IsGestureActive"), Is.False);
        }

        [Test]
        public void AMapCameraWithoutItsOwnCameraComponentIsNotLive()
        {
            // The map camera is assigned but has no Camera of its own: the middle term of the guard. It has
            // to be a refusal rather than a null reference, because a half-wired scene must not throw.
            m_MapCamera = CreateObject("Map Camera Without A Camera").AddComponent<OrthographicMapCamera>();
            CreateDragInput();

            BeginDragGesture();

            Assert.DoesNotThrow(() => RunFrame(m_Drag));
            Assert.That(ReadPrivateField<bool>(m_Drag, "m_IsDragging"), Is.False);
        }
    }
}

using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Turns a two finger pinch or a mouse wheel into map zoom on an <see cref="OrthographicMapCamera"/>.
    /// <para>
    /// The anchor is the midpoint between the fingers, or the mouse position for the wheel, so the place
    /// being pinched stays put instead of sliding out from under the fingers. The anchor is captured once
    /// when the gesture starts and held for the whole gesture, because the midpoint drifts as the fingers
    /// move together.
    /// </para>
    /// <para>
    /// The zoom is applied as a ratio of the pinch distance and as an exponential of the wheel delta, so
    /// the gesture feels the same at every zoom level. The camera clamps the result, so this component
    /// never needs to know the zoom limits.
    /// </para>
    /// </summary>
    public sealed class OrthographicMapZoomInput : MonoBehaviour
    {
        /// <summary>
        /// The zoom factor one wheel notch applies. Above 1 the content grows when the wheel goes up.
        /// </summary>
        private const float WheelZoomPerNotch = 1.15f;

        [SerializeField]
        private OrthographicMapCamera m_MapCamera;

        [SerializeField]
        [Tooltip("Whether the mouse wheel also zooms. Touch pinch always works.")]
        private bool m_UseMouseWheel = true;

        [SerializeField]
        private bool m_IsEnabled = true;

        private bool m_IsGestureActive;
        private Vector2 m_AnchorViewport;
        private float m_PinchStartDistance;
        private float m_PinchStartZoom = 1f;

        public OrthographicMapCamera MapCamera
        {
            get { return m_MapCamera; }
            set { m_MapCamera = value; }
        }

        public bool UseMouseWheel
        {
            get { return m_UseMouseWheel; }
            set { m_UseMouseWheel = value; }
        }

        public bool IsEnabled
        {
            get { return m_IsEnabled; }
            set { m_IsEnabled = value; }
        }

        public bool IsGestureActive
        {
            get { return m_IsGestureActive; }
        }

        /// <summary>
        /// Converts a pointer position in screen pixels into the anchor the camera expects: each component
        /// runs -1 to 1 from the viewport center.
        /// </summary>
        public static Vector2 ScreenToViewportAnchor(Vector2 screenPosition, float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
            {
                return Vector2.zero;
            }

            return new Vector2(
                screenPosition.x / screenWidth * 2f - 1f,
                screenPosition.y / screenHeight * 2f - 1f);
        }

        /// <summary>
        /// Converts a pinch distance ratio into a zoom level. A ratio of 1 leaves the zoom alone, and the
        /// ratio is measured against the distance the gesture started at rather than the previous frame,
        /// so no drift accumulates.
        /// </summary>
        public static float PinchRatioToZoom(float startZoom, float startDistance, float currentDistance)
        {
            if (startDistance <= 0f || currentDistance <= 0f || startZoom <= 0f)
            {
                return startZoom;
            }

            return startZoom * (currentDistance / startDistance);
        }

        private void Update()
        {
            if (!m_IsEnabled || m_MapCamera == null)
            {
                EndGestureIfActive();
                return;
            }

            if (Input.touchCount >= 2)
            {
                UpdatePinch();
                return;
            }

            EndGestureIfActive();

            if (m_UseMouseWheel)
            {
                UpdateWheel();
            }
        }

        private void UpdatePinch()
        {
            var first = Input.GetTouch(0);
            var second = Input.GetTouch(1);
            var midpoint = (first.position + second.position) * 0.5f;
            var distance = (first.position - second.position).magnitude;

            if (distance <= 0f)
            {
                return;
            }

            if (!m_IsGestureActive)
            {
                m_IsGestureActive = true;
                m_AnchorViewport = ScreenToViewportAnchor(midpoint, UnityEngine.Screen.width, UnityEngine.Screen.height);
                m_PinchStartZoom = m_MapCamera.Zoom;
                m_PinchStartDistance = distance;
                m_MapCamera.BeginGesture();
                return;
            }

            // The anchor stays where the gesture captured it, and the ratio is always measured against the
            // distance the gesture started at, so the zoom cannot accumulate error across frames.
            ApplyZoom(PinchRatioToZoom(m_PinchStartZoom, m_PinchStartDistance, distance));
        }

        private void UpdateWheel()
        {
            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            // Each notch is one wheel gesture in its own right, anchored where the pointer is.
            m_AnchorViewport = ScreenToViewportAnchor(
                Input.mousePosition,
                UnityEngine.Screen.width,
                UnityEngine.Screen.height);
            ApplyZoom(m_MapCamera.Zoom * Mathf.Pow(WheelZoomPerNotch, scroll));
        }

        private void ApplyZoom(float zoom)
        {
            string error;
            if (!m_MapCamera.TryZoomTo(zoom, m_AnchorViewport, out error))
            {
                // A failed zoom is a scene wiring problem, not something to throw at the player.
                Debug.LogWarning("OrthographicMapZoomInput could not zoom the map: " + error, this);
            }
        }

        private void EndGestureIfActive()
        {
            if (!m_IsGestureActive)
            {
                return;
            }

            m_IsGestureActive = false;
            m_MapCamera.EndGesture();
        }
    }
}

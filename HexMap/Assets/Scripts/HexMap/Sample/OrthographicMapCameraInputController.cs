using HexMap.Core;
using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Applies Map input intents to one orthographic Map camera. Map-mode handlers share this component so
    /// they expose identical pan and camera-distance behaviour while the dispatcher remains their only
    /// input route.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrthographicMapCameraInputController : MonoBehaviour
    {
        private const float DragDirection = -1f;

        [SerializeField]
        private OrthographicMapCamera m_MapCamera;

        [SerializeField]
        [Tooltip("Multiplier applied to normalized camera-distance input before exponential zooming.")]
        private float m_PinchZoomSensitivity = 1f;

        public OrthographicMapCamera MapCamera
        {
            get { return m_MapCamera; }
            set { m_MapCamera = value; }
        }

        public float PinchZoomSensitivity
        {
            get { return m_PinchZoomSensitivity; }
            set { m_PinchZoomSensitivity = value; }
        }

        /// <summary>
        /// Applies a screen-space Map drag while preserving the current frame's visible-size mapping.
        /// </summary>
        public bool HandleMapDragged(Vector2 screenDelta)
        {
            if (!TryRefreshFraming())
            {
                return false;
            }

            OrthographicMapFraming framing;
            if (!m_MapCamera.TryGetFraming(out framing))
            {
                return false;
            }

            var offsetDelta = ScreenDeltaToOffsetDelta(
                screenDelta,
                framing.VisibleWidth,
                framing.VisibleHeight,
                Screen.width,
                Screen.height);
            if (Mathf.Approximately(offsetDelta.x, 0f) && Mathf.Approximately(offsetDelta.y, 0f))
            {
                return true;
            }

            string error;
            if (m_MapCamera.TrySetOffset(m_MapCamera.Center + offsetDelta, out error))
            {
                return true;
            }

            Debug.LogWarning("OrthographicMapCameraInputController could not pan the map: " + error, this);
            return false;
        }

        /// <summary>
        /// Applies a device-independent camera-distance change around its screen-pixel anchor.
        /// </summary>
        public bool HandleCameraDistanceChanged(in MapCameraDistanceContext context)
        {
            if (!TryRefreshFraming())
            {
                return false;
            }

            var zoomMultiplier = Mathf.Exp(context.NormalizedDistanceDelta * m_PinchZoomSensitivity);
            var targetZoom = m_MapCamera.Zoom * zoomMultiplier;
            var viewportAnchor = ScreenToViewportAnchor(context.ScreenAnchor, Screen.width, Screen.height);

            string error;
            if (m_MapCamera.TryZoomTo(targetZoom, viewportAnchor, out error))
            {
                return true;
            }

            Debug.LogWarning("OrthographicMapCameraInputController could not zoom the map: " + error, this);
            return false;
        }

        /// <summary>
        /// Converts a screen-pixel anchor to the coordinates expected by <see cref="OrthographicMapCamera"/>.
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
        /// Converts a screen delta into the matching local Map-plane center delta.
        /// </summary>
        public static Vector2 ScreenDeltaToOffsetDelta(
            Vector2 screenDelta,
            float visibleWidth,
            float visibleHeight,
            float screenWidth,
            float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || visibleWidth <= 0f || visibleHeight <= 0f)
            {
                return Vector2.zero;
            }

            return new Vector2(
                screenDelta.x / screenWidth * visibleWidth * DragDirection,
                screenDelta.y / screenHeight * visibleHeight * DragDirection);
        }

        private bool TryRefreshFraming()
        {
            if (m_MapCamera == null)
            {
                return false;
            }

            string error;
            if (m_MapCamera.TryRefreshIfStale(out error))
            {
                return true;
            }

            Debug.LogWarning("OrthographicMapCameraInputController could not refresh the map framing: " + error, this);
            return false;
        }
    }
}

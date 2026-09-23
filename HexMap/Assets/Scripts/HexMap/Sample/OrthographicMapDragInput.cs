using HexMap.Core;
using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Turns a horizontal mouse or touch drag into map panning on an <see cref="OrthographicMapCamera"/>.
    /// <para>
    /// The content follows the finger: dragging left moves the map left, which moves the camera right.
    /// No damping, no inertia, no sensitivity curve: one screen pixel is worth the same fraction of the
    /// visible map width everywhere, so the feel does not change between devices.
    /// </para>
    /// </summary>
    public sealed class OrthographicMapDragInput : MonoBehaviour
    {
        /// <summary>
        /// Positive means the camera moves right when the pointer moves right, which drags the content
        /// left and makes the map feel grabbed. Flipping this sign flips the whole gesture.
        /// </summary>
        private const float DragDirection = 1f;

        [SerializeField]
        private OrthographicMapCamera m_MapCamera;

        [SerializeField]
        private bool m_IsEnabled = true;

        private Vector3 m_PreviousPointerPosition;
        private bool m_IsDragging;

        public OrthographicMapCamera MapCamera
        {
            get { return m_MapCamera; }
            set { m_MapCamera = value; }
        }

        public bool IsEnabled
        {
            get { return m_IsEnabled; }
            set { m_IsEnabled = value; }
        }

        /// <summary>
        /// Converts a pointer delta in screen pixels into a change of the camera offset along the map's
        /// local +X axis. Pure so the mapping can be reasoned about without a device.
        /// </summary>
        public static float ScreenDeltaToOffsetDelta(float screenDeltaX, float visibleWidth, float screenWidth)
        {
            if (screenWidth <= 0f || visibleWidth <= 0f)
            {
                return 0f;
            }

            return screenDeltaX / screenWidth * visibleWidth * DragDirection;
        }

        private void Update()
        {
            if (!m_IsEnabled || m_MapCamera == null)
            {
                m_IsDragging = false;
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                m_IsDragging = false;
                return;
            }

            var pointerPosition = Input.mousePosition;
            if (!m_IsDragging)
            {
                m_IsDragging = true;
                m_PreviousPointerPosition = pointerPosition;
                return;
            }

            var screenDeltaX = pointerPosition.x - m_PreviousPointerPosition.x;
            m_PreviousPointerPosition = pointerPosition;

            if (Mathf.Approximately(screenDeltaX, 0f))
            {
                return;
            }

            OrthographicMapFraming framing;
            if (!m_MapCamera.TryGetFraming(out framing))
            {
                return;
            }

            var offsetDelta = ScreenDeltaToOffsetDelta(screenDeltaX, framing.VisibleWidth, UnityEngine.Screen.width);
            if (Mathf.Approximately(offsetDelta, 0f))
            {
                return;
            }

            string error;
            if (!m_MapCamera.TrySetOffset(m_MapCamera.Offset + offsetDelta, out error))
            {
                // A failed pan is a scene wiring problem, not something to throw at the player.
                Debug.LogWarning("OrthographicMapDragInput could not pan the map: " + error, this);
            }
        }
    }
}

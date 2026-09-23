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
        private const float DragDirection = -1f;

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
        /// Converts a pointer delta in screen pixels into a change of the camera centre along the map's
        /// local plane axes. Pure so the mapping can be reasoned about without a device.
        /// </summary>
        /// <param name="screenDelta">Pointer movement in screen pixels.</param>
        /// <param name="visibleWidth">The world width the frame currently covers.</param>
        /// <param name="visibleHeight">The world height the frame currently covers.</param>
        /// <param name="screenWidth">The viewport width in pixels.</param>
        /// <param name="screenHeight">The viewport height in pixels.</param>
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

            // Screen X runs along the map's local X and screen Y along its local plane axis, so a
            // full-width drag is worth exactly one visible width on either axis.
            return new Vector2(
                screenDelta.x / screenWidth * visibleWidth * DragDirection,
                screenDelta.y / screenHeight * visibleHeight * DragDirection);
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

            var screenDelta = new Vector2(
                pointerPosition.x - m_PreviousPointerPosition.x,
                pointerPosition.y - m_PreviousPointerPosition.y);
            m_PreviousPointerPosition = pointerPosition;

            if (Mathf.Approximately(screenDelta.x, 0f) && Mathf.Approximately(screenDelta.y, 0f))
            {
                return;
            }

            OrthographicMapFraming framing;
            if (!m_MapCamera.TryGetFraming(out framing))
            {
                return;
            }

            var offsetDelta = ScreenDeltaToOffsetDelta(
                screenDelta,
                framing.VisibleWidth,
                framing.VisibleHeight,
                UnityEngine.Screen.width,
                UnityEngine.Screen.height);
            if (Mathf.Approximately(offsetDelta.x, 0f) && Mathf.Approximately(offsetDelta.y, 0f))
            {
                return;
            }

            string error;
            if (!m_MapCamera.TrySetOffset(m_MapCamera.Center + offsetDelta, out error))
            {
                // A failed pan is a scene wiring problem, not something to throw at the player.
                Debug.LogWarning("OrthographicMapDragInput could not pan the map: " + error, this);
            }
        }
    }
}

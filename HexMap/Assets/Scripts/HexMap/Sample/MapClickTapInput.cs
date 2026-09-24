using HexMap.UnityRuntime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HexMap.Sample
{
    /// <summary>
    /// The pointer-to-click adapter: it decides whether a press was a tap on the map and, if so, hands the
    /// screen position to the <see cref="MapClickDispatcher"/>.
    /// <para>
    /// This is deliberately the only component that knows about pointers. Telling a tap from a drag,
    /// staying out of the UI's way and reading whichever input backend the project uses all belong here,
    /// so nothing downstream has to know which camera is live or which mode is up. A project that swaps
    /// this for the new Input System or for EasyTouch only has to keep calling
    /// <see cref="MapClickDispatcher.OnMapClicked"/> with a screen position.
    /// </para>
    /// <para>
    /// The mouse path is also the touch path: Unity maps the primary touch to mouse button 0, so one
    /// implementation covers the editor and the device.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapClickTapInput : MonoBehaviour
    {
        /// <summary>
        /// How far the pointer may travel, in screen pixels, and still count as a tap. A drag that pans the
        /// map must not also click a plot when the finger comes up.
        /// </summary>
        [SerializeField]
        [Tooltip("Maximum pointer travel, in screen pixels, that still counts as a tap.")]
        private float m_TapSlop = 20f;

        [SerializeField]
        private MapClickDispatcher m_Dispatcher;

        private Vector2 m_PressPosition;
        private bool m_IsPressed;
        private bool m_HasReportedMissingDispatcher;

        public MapClickDispatcher Dispatcher
        {
            get { return m_Dispatcher; }
            set { m_Dispatcher = value; }
        }

        public float TapSlop
        {
            get { return m_TapSlop; }
            set { m_TapSlop = value; }
        }

        private void Update()
        {
            if (m_Dispatcher == null)
            {
                ReportMissingDispatcher();
                return;
            }

            // Two fingers is a pinch, which is the zoom gesture rather than a tap. It also cancels a press
            // that already started, so putting a second finger down cannot end as a click.
            if (Input.touchCount > 1)
            {
                m_IsPressed = false;
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                // The press decides who owns the pointer: one that starts on UI belongs to the UI even if
                // the finger ends up over the map, and one that starts on the map is a map click even if
                // the finger ends up over a panel.
                m_IsPressed = !IsPointerOverUi();
                m_PressPosition = Input.mousePosition;
                return;
            }

            if (!Input.GetMouseButtonUp(0))
            {
                return;
            }

            var wasPressedOnMap = m_IsPressed;
            m_IsPressed = false;

            if (!wasPressedOnMap)
            {
                return;
            }

            var releasePosition = (Vector2)Input.mousePosition;
            if ((releasePosition - m_PressPosition).sqrMagnitude > m_TapSlop * m_TapSlop)
            {
                // Too far to be a tap, so this was a pan and the map has already moved under it.
                return;
            }

            // The press position, not the release one: it is the place the pointer was aimed at.
            m_Dispatcher.OnMapClicked(m_PressPosition);
        }

        private static bool IsPointerOverUi()
        {
            // No event system means no UI can be hit, which is the state an early scene is in.
            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        /// <summary>
        /// Reports a missing dispatcher once. This runs on every frame, so repeating it would bury the
        /// console: a reference that was never assigned is a wiring mistake, not a per-frame condition.
        /// </summary>
        private void ReportMissingDispatcher()
        {
            if (m_HasReportedMissingDispatcher)
            {
                return;
            }

            m_HasReportedMissingDispatcher = true;
            Debug.LogError(
                "MapClickTapInput has no MapClickDispatcher reference, so taps reach nothing.",
                this);
        }
    }
}

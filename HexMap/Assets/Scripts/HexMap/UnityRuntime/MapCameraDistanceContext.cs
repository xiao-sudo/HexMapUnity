using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// A device-independent request to move the map camera nearer or farther from its current view.
    /// The input layer converts a physical gesture into this context without exposing its input backend
    /// to map handlers.
    /// </summary>
    public readonly struct MapCameraDistanceContext
    {
        private readonly Vector2 m_ScreenAnchor;
        private readonly float m_NormalizedDistanceDelta;
        private readonly float m_ViewportAspect;

        /// <summary>
        /// The screen-pixel anchor that the camera should preserve while changing distance.
        /// </summary>
        public Vector2 ScreenAnchor
        {
            get { return m_ScreenAnchor; }
        }

        /// <summary>
        /// A signed distance change normalized by the shorter screen dimension. Positive values move
        /// the camera nearer; negative values move it farther away.
        /// </summary>
        public float NormalizedDistanceDelta
        {
            get { return m_NormalizedDistanceDelta; }
        }

        /// <summary>The viewport aspect selected by the active input route.</summary>
        public float ViewportAspect
        {
            get { return m_ViewportAspect; }
        }

        public MapCameraDistanceContext(Vector2 screenAnchor, float normalizedDistanceDelta)
            : this(screenAnchor, normalizedDistanceDelta, 0f)
        {
        }

        public MapCameraDistanceContext(
            Vector2 screenAnchor,
            float normalizedDistanceDelta,
            float viewportAspect)
        {
            m_ScreenAnchor = screenAnchor;
            m_NormalizedDistanceDelta = normalizedDistanceDelta;
            m_ViewportAspect = viewportAspect;
        }
    }
}

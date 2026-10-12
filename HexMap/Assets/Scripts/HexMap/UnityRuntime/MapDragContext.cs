using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// A map drag expressed in screen pixels together with the viewport information selected by the
    /// active input route. Handlers consume this intent without knowing which camera owns the route.
    /// </summary>
    public readonly struct MapDragContext
    {
        public MapDragContext(Vector2 screenDelta, float viewportAspect)
        {
            ScreenDelta = screenDelta;
            ViewportAspect = viewportAspect;
        }

        public Vector2 ScreenDelta { get; }

        public float ViewportAspect { get; }
    }
}

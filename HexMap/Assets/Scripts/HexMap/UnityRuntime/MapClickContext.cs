using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// What one map click resolved to, handed to whichever <see cref="IMapClickHandler"/> owns the
    /// active channel.
    /// <para>
    /// A click that hit nothing is still delivered, with <see cref="HasPlot"/> false. Handlers need that
    /// case: clicking empty space to dismiss a panel is a normal request, and a handler that never hears
    /// about it cannot close anything.
    /// </para>
    /// <para>
    /// <see cref="PickStatus"/> is carried so "the click did nothing" can be traced instead of guessed:
    /// it separates "no camera was set", "the map is not ready", "the click was outside the map" and
    /// "that cell belongs to no plot".
    /// </para>
    /// </summary>
    public readonly struct MapClickContext
    {
        /// <summary>
        /// The screen position the caller passed in, unchanged. Carried through so a handler can use it
        /// for feedback that is not the plot itself.
        /// </summary>
        public Vector2 ScreenPosition { get; }

        /// <summary>
        /// True when the click resolved to a plot on the map.
        /// </summary>
        public bool HasPlot { get; }

        /// <summary>
        /// The clicked plot, or -1 when <see cref="HasPlot"/> is false.
        /// </summary>
        public int PlotId { get; }

        /// <summary>
        /// Why the click resolved the way it did. Always meaningful, including on success.
        /// </summary>
        public PlotScreenPickStatus PickStatus { get; }

        private MapClickContext(Vector2 screenPosition, bool hasPlot, int plotId, PlotScreenPickStatus pickStatus)
        {
            ScreenPosition = screenPosition;
            HasPlot = hasPlot;
            PlotId = plotId;
            PickStatus = pickStatus;
        }

        /// <summary>
        /// A click that resolved to a plot.
        /// </summary>
        internal static MapClickContext Hit(Vector2 screenPosition, int plotId)
        {
            return new MapClickContext(screenPosition, true, plotId, PlotScreenPickStatus.Found);
        }

        /// <summary>
        /// A click that resolved to nothing, with the reason it did not.
        /// </summary>
        internal static MapClickContext Miss(Vector2 screenPosition, PlotScreenPickStatus status)
        {
            return new MapClickContext(screenPosition, false, -1, status);
        }
    }
}

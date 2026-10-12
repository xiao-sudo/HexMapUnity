using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Responds to map input for one channel. Register an implementation with a
    /// <see cref="MapInputDispatcher"/> under the channel that should receive the input.
    /// </summary>
    public interface IMapInputHandler
    {
        /// <summary>
        /// Handles one map click.
        /// </summary>
        /// <param name="context">
        /// What the click resolved to. <see cref="MapClickContext.HasPlot"/> is false when the click hit
        /// nothing, which is still delivered so a handler can dismiss what it opened.
        /// </param>
        /// <returns>True when the handler consumed the click.</returns>
        bool OnMapClicked(in MapClickContext context);

        /// <summary>
        /// Handles one map drag in screen pixels. Drag routing deliberately does not perform map picking;
        /// the context contains viewport information selected by the active route.
        /// </summary>
        /// <returns>True when the handler consumed the drag.</returns>
        bool OnMapDragged(in MapDragContext context);

        /// <summary>
        /// Handles one device-independent request to move the map camera nearer or farther away.
        /// </summary>
        /// <returns>True when the handler consumed the camera-distance intent.</returns>
        bool OnMapCameraDistanceChanged(in MapCameraDistanceContext context);
    }
}

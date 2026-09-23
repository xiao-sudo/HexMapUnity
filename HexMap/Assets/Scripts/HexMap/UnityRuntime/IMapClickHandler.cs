namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Responds to map clicks for one channel. Register an implementation with a
    /// <see cref="MapClickDispatcher"/> under the channel that should receive the clicks.
    /// </summary>
    public interface IMapClickHandler
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
    }
}

using System;

namespace HexMap.Sample
{
    /// <summary>
    /// Something that announces which plot a delivered map click resolved to.
    /// <para>
    /// Both mode handlers publish the same event, and a panel should not have to care which mode it is
    /// looking at: it subscribes to whichever handler owns its mode and reacts to the plot id. This
    /// interface is what lets one panel type serve both modes instead of a near-copy per mode.
    /// </para>
    /// <para>
    /// A plot id of -1 means the click hit nothing, which is the panel's cue to close.
    /// </para>
    /// </summary>
    public interface IMapPlotClickSource
    {
        /// <summary>
        /// Raised for every click the owning handler is given, including the ones that hit no plot.
        /// </summary>
        event Action<int> PlotClicked;
    }
}

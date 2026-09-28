namespace HexMap.Sample
{
    /// <summary>
    /// The click channels this application declares, one per view mode. The runtime library only knows
    /// <c>MapClickChannels.None</c>; the mode vocabulary lives here, next to the code that owns the modes,
    /// so the library stays reusable.
    /// <para>
    /// The numbers are derived from <see cref="MapViewMode"/> rather than repeated, so a mode's channel
    /// cannot drift away from the mode it belongs to. They are deliberately still a separate vocabulary: a
    /// channel is what <c>MapClickDispatcher</c> routes by, and a mode is what the player sees. They happen
    /// to correspond one to one today, but merging them would mean no mode could ever exist without a
    /// channel, and no channel without a mode.
    /// </para>
    /// </summary>
    public static class SampleMapClickChannels
    {
        /// <summary>Normal gameplay: following a unit around the perspective view.</summary>
        public const int Gameplay = (int)MapViewMode.Gameplay + ChannelBase;

        /// <summary>The whole map seen from straight above.</summary>
        public const int TopDown = (int)MapViewMode.TopDown + ChannelBase;

        /// <summary>
        /// Keeps every mode's channel away from <c>MapClickChannels.None</c>, which is zero. The modes are
        /// numbered from zero, so without this the first mode would own the channel that means "no mode
        /// reacts", and a click arriving between two modes would be routed to gameplay.
        /// </summary>
        private const int ChannelBase = 1;

        /// <summary>The channel a mode's clicks arrive on.</summary>
        public static int For(MapViewMode mode)
        {
            return (int)mode + ChannelBase;
        }
    }
}

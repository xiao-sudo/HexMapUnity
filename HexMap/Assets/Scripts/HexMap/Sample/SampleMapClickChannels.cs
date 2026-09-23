namespace HexMap.Sample
{
    /// <summary>
    /// The click channels this application declares. The runtime library only knows
    /// <c>MapClickChannels.None</c>; the mode vocabulary lives here, next to the code that owns the modes,
    /// so the library stays reusable.
    /// </summary>
    public static class SampleMapClickChannels
    {
        /// <summary>Normal gameplay: following a unit around the perspective view.</summary>
        public const int Gameplay = 1;

        /// <summary>The whole map seen from straight above.</summary>
        public const int TopDown = 2;
    }
}

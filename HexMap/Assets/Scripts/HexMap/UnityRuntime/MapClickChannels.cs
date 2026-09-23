namespace HexMap.UnityRuntime
{
    /// <summary>
    /// The channel identifiers a <see cref="MapClickDispatcher"/> understands.
    /// <para>
    /// Concrete channels belong to the application: this assembly never learns what a "top down" or a
    /// "gameplay" channel means. Declare them next to the code that owns the mode, for example
    /// <c>public const int Gameplay = 1;</c> and <c>public const int TopDown = 2;</c>.
    /// </para>
    /// </summary>
    public static class MapClickChannels
    {
        /// <summary>
        /// No channel. While active, map clicks are accepted and dropped, which is how a mode says
        /// "this mode does not react to clicks" without unregistering anything.
        /// </summary>
        public const int None = 0;
    }
}

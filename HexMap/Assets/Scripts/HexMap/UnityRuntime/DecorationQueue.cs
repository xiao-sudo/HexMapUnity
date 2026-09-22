namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Owns the render queue numbers that order the decoration, HexMap and overlay layers.
    /// </summary>
    /// <remarks>
    /// This is the only place these numbers are defined. Layering is expressed purely through
    /// render queue sub-ranges inside the transparent band, never through depth offsets or
    /// geometry height: every layer blends with alpha and disables depth writes, so the depth
    /// buffer cannot order them.
    /// </remarks>
    public static class DecorationQueue
    {
        /// <summary>Decorations, drawn before the HexMap so the map covers them.</summary>
        public const int Decoration = 2800;

        /// <summary>
        /// The HexMap layer. This number is owned by the <c>Queue</c> tag of the hex cell shader,
        /// not by this constant; a test asserts the shader still reports it.
        /// </summary>
        public const int HexMap = 3000;

        /// <summary>Overlays, drawn after the HexMap so they sit on top of it.</summary>
        public const int Overlay = 3005;
    }
}

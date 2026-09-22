namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Owns the numbers that place the decoration, HexMap, overlay and effect bands in draw order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ordering is expressed by <see cref="DecorationSortingOrder"/>, <see cref="HexMapSortingOrder"/>
    /// and <see cref="OverlaySortingOrder"/>, not by the queue numbers.</b> The measured sort key is
    /// <c>sortingLayer → sortingOrder → renderQueue</c>: <c>sortingOrder</c> outranks the render
    /// queue, so the queue numbers no longer decide which band covers which. They remain here as the
    /// material identity values, and staying in the transparent band is still required because every
    /// layer alpha-blends with depth writes off, so nothing may move to an opaque queue.
    /// </para>
    /// <para>
    /// <b>Smaller sorting order draws first.</b> The HexMap sits at
    /// <see cref="HexMapSortingOrder"/> and cannot move: the instanced draw path has no sorting knob
    /// at all, so an instanced HexMap is pinned to zero. Everything below that baseline must
    /// therefore use a negative sorting order.
    /// </para>
    /// </remarks>
    public static class DecorationQueue
    {
        /// <summary>Decorations are drawn before the HexMap, so the map covers them.</summary>
        public const int Decoration = 2800;

        /// <summary>
        /// The HexMap band's queue. This number is owned by the <c>Queue</c> tag of the hex cell
        /// shader, not by this constant; a test asserts the shader still reports it.
        /// </summary>
        public const int HexMap = 3000;

        /// <summary>Overlays are drawn after the HexMap, so they sit on top of it.</summary>
        public const int Overlay = 3005;

        /// <summary>
        /// Sorting order for the decoration band. Negative on purpose: see
        /// <see cref="HexMapSortingOrder"/>.
        /// </summary>
        public const int DecorationSortingOrder = -100;

        /// <summary>
        /// The sorting order the HexMap is drawn at, and the baseline every other band is placed
        /// against. Smaller values draw first, so a band below the HexMap needs a negative order.
        /// </summary>
        /// <remarks>
        /// <b>This value is not configurable, and on the instanced draw path it is not even
        /// settable.</b> <c>Graphics.DrawMeshInstanced</c> takes no sorting order and passes through
        /// no <c>Renderer</c>, so a HexMap built that way is pinned to 0 whether or not anyone says
        /// so. This constant records the baseline so the other bands can be reasoned about; the
        /// MeshRenderer strategy is what actually writes it onto its cells.
        /// </remarks>
        public const int HexMapSortingOrder = 0;

        /// <summary>Sorting order for the overlay band, above the HexMap baseline.</summary>
        public const int OverlaySortingOrder = 100;
    }
}

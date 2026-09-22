using System;
using HexMap.UnityRuntime;

namespace HexMap.Editor
{
    /// <summary>
    /// The render band a decoration prefab is authored into: below the HexMap, or above it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The member name is also the token the asset name carries.</b>
    /// <see cref="DecorationPrefabBuilder.BuildAssetName"/> writes this value into the file name, so
    /// the label on disk and the value in code are the same token and cannot be renamed apart. Same
    /// arrangement as <see cref="HexMap.Core.HexPlane"/> for the plane.
    /// </para>
    /// <para>
    /// This enum exists instead of a pair of loose ints because a queue of 2800 next to a sorting
    /// order of 100 must not be expressible. Its symptom would be a decoration that quietly covers
    /// the map, which is the trap the band contract is written around; see the trap list in
    /// <c>docs/implementation/decoration-rendering.md</c>.
    /// </para>
    /// </remarks>
    public enum DecorationBand
    {
        /// <summary>Drawn before the HexMap, so the map covers it.</summary>
        Decoration = 0,

        /// <summary>Drawn after the HexMap, so it sits on top of it.</summary>
        Overlay = 1
    }

    /// <summary>
    /// Resolves a <see cref="DecorationBand"/> to the two numbers a prefab has to carry for it.
    /// </summary>
    /// <remarks>
    /// <b>This is the tooling's only copy of the band table, and it reads the numbers from
    /// <see cref="DecorationQueue"/> rather than restating them.</b> Writing 2800 or 3005 here would
    /// put a second owner on values whose whole point is that they have one, and the two would drift
    /// the first time a band moved.
    /// </remarks>
    public static class DecorationBands
    {
        /// <summary>The render queue the band's materials belong in.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="band"/> is not one of the declared <see cref="DecorationBand"/> values.
        /// </exception>
        public static int Queue(DecorationBand band)
        {
            switch (band)
            {
                case DecorationBand.Decoration:
                    return DecorationQueue.Decoration;
                case DecorationBand.Overlay:
                    return DecorationQueue.Overlay;
                default:
                    throw new ArgumentOutOfRangeException(nameof(band), band, null);
            }
        }

        /// <summary>
        /// The sorting order that puts the band where it belongs relative to
        /// <see cref="DecorationQueue.HexMapSortingOrder"/>. Smaller draws first.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="band"/> is not one of the declared <see cref="DecorationBand"/> values.
        /// </exception>
        public static int SortingOrder(DecorationBand band)
        {
            switch (band)
            {
                case DecorationBand.Decoration:
                    return DecorationQueue.DecorationSortingOrder;
                case DecorationBand.Overlay:
                    return DecorationQueue.OverlaySortingOrder;
                default:
                    throw new ArgumentOutOfRangeException(nameof(band), band, null);
            }
        }
    }
}

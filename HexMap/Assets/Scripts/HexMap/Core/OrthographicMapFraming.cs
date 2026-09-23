using System;
using UnityEngine;

namespace HexMap.Core
{
    /// <summary>
    /// Immutable framing snapshot for an orthographic camera that looks straight down
    /// at a hex map's center line.
    /// <para>
    /// The camera can only pan along the map's local +X axis, so "up" on screen is the
    /// map's local +Z axis. Both the plane (<see cref="HexPlane"/>) and the orientation
    /// (<see cref="HexOrientation"/>) are inputs. The plane only decides which world axis
    /// the depth lies on; the orientation decides which of the two hexagon extents is the
    /// depth. This type performs no plane or orientation branching beyond that swap.
    /// </para>
    /// <para>
    /// The envelope is the map's outer hexagon <em>including the outermost cells' vertices</em>:
    /// the center lattice spread plus one cell half profile. Deriving it from the center lattice
    /// alone would clip the outermost cells' tips.
    /// </para>
    /// <para>
    /// The visible height is always at least the whole map depth, so every row is always
    /// inside the frame. The visible width is derived from the viewport aspect ratio and
    /// is not independently configurable, so some columns may fall outside the frame and
    /// that is what makes panning meaningful.
    /// </para>
    /// </summary>
    public readonly struct OrthographicMapFraming
    {
        /// <summary>
        /// The smallest view margin that still keeps every row inside the frame.
        /// </summary>
        public const float MinimumViewMargin = 1f;

        /// <summary>
        /// The zoom level at which the framing is created: the map's whole depth fits the frame.
        /// </summary>
        public const float DefaultZoom = 1f;

        /// <summary>
        /// The map's outer envelope half extent along the local X axis: the axis the camera pans on.
        /// </summary>
        public float MapHalfWidth { get; }

        /// <summary>
        /// The map's outer envelope half extent along the screen's vertical axis.
        /// </summary>
        public float MapHalfDepth { get; }

        /// <summary>
        /// How much of the map the frame shows. 1 is the whole depth leaving the view margin free,
        /// and larger values zoom in. Zoom is the only input besides the aspect ratio that changes
        /// the visible size; the map envelope never depends on it.
        /// </summary>
        public float Zoom { get; }

        /// <summary>
        /// The empty space this framing leaves around the map's depth. Kept so a zoom change can
        /// recompute the size without the caller repeating its inputs.
        /// </summary>
        public float ViewMargin { get; }

        /// <summary>
        /// The viewport aspect ratio this framing was built for. Kept so a zoom change can recompute
        /// the visible width. The aspect ratio is never configurable through this type.
        /// </summary>
        public float Aspect { get; }

        /// <summary>
        /// The visible half height at zoom 1, which is the map half depth times the view margin.
        /// Every zoom is measured against this value rather than against the current one, so
        /// applying a zoom repeatedly cannot accumulate.
        /// </summary>
        public float BaseOrthographicSize { get; }

        /// <summary>
        /// The map's outer envelope center in layout local space (the center lattice is centered
        /// on the layout origin). This is where the camera sits when the offset is zero.
        /// </summary>
        public Vector3 Origin { get; }

        /// <summary>
        /// The <c>Camera.orthographicSize</c> that keeps every row inside the frame.
        /// </summary>
        public float OrthographicSize { get; }

        /// <summary>
        /// The world height covered by the frame. Always at least <c>2 * MapHalfDepth</c>.
        /// </summary>
        public float VisibleHeight { get; }

        /// <summary>
        /// The world width covered by the frame. Not independently configurable.
        /// </summary>
        public float VisibleWidth { get; }

        /// <summary>
        /// The map's full width along the pan axis.
        /// </summary>
        public float MapWidth
        {
            get { return MapHalfWidth * 2f; }
        }

        /// <summary>
        /// The map's full depth along the screen's vertical axis.
        /// </summary>
        public float MapDepth
        {
            get { return MapHalfDepth * 2f; }
        }

        /// <summary>
        /// How far the offset may travel from the center in either direction.
        /// Zero when the frame is at least as wide as the map, in which case the camera is locked to the center.
        /// </summary>
        public float MovableHalfRange
        {
            get
            {
                var range = MapHalfWidth - VisibleWidth * 0.5f;
                return range > 0f ? range : 0f;
            }
        }

        /// <summary>
        /// The smallest allowed offset along the map's local +X axis.
        /// </summary>
        public float MinOffset
        {
            get { return -MovableHalfRange; }
        }

        /// <summary>
        /// The largest allowed offset along the map's local +X axis.
        /// </summary>
        public float MaxOffset
        {
            get { return MovableHalfRange; }
        }

        /// <summary>
        /// True when the frame is at least as wide as the map, so no panning is possible.
        /// </summary>
        public bool IsLockedToCenter
        {
            get { return MovableHalfRange <= 0f; }
        }

        /// <summary>
        /// True when the frame covers every column of the map. A wide viewport makes this happen;
        /// it is geometry, not a defect.
        /// </summary>
        public bool ShowsEveryColumn
        {
            get { return VisibleWidth >= MapWidth; }
        }

        private OrthographicMapFraming(
            float mapHalfWidth,
            float mapHalfDepth,
            Vector3 origin,
            float zoom,
            float viewMargin,
            float aspect,
            float baseOrthographicSize)
        {
            MapHalfWidth = mapHalfWidth;
            MapHalfDepth = mapHalfDepth;
            Origin = origin;
            Zoom = zoom;
            ViewMargin = viewMargin;
            Aspect = aspect;
            BaseOrthographicSize = baseOrthographicSize;
            OrthographicSize = baseOrthographicSize / zoom;
            VisibleHeight = OrthographicSize * 2f;
            VisibleWidth = VisibleHeight * aspect;
        }

        /// <summary>
        /// Returns the same map seen at another zoom level. The envelope, origin, view margin and
        /// aspect ratio are untouched; only the visible size and everything derived from it change.
        /// Zoom 1 reproduces this framing exactly, and zoom is absolute rather than a factor, so
        /// applying it twice cannot accumulate.
        /// </summary>
        public OrthographicMapFraming WithZoom(float zoom)
        {
            if (!IsFinitePositive(zoom))
            {
                throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "Zoom must be positive and finite.");
            }

            return new OrthographicMapFraming(
                MapHalfWidth,
                MapHalfDepth,
                Origin,
                zoom,
                ViewMargin,
                Aspect,
                BaseOrthographicSize);
        }

        /// <summary>
        /// Returns the same map seen at another zoom level, reporting an invalid zoom instead of throwing.
        /// </summary>
        public bool TryWithZoom(float zoom, out OrthographicMapFraming framing, out string error)
        {
            framing = this;

            if (!IsFinitePositive(zoom))
            {
                error = "Zoom must be positive and finite.";
                return false;
            }

            framing = WithZoom(zoom);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Creates a framing snapshot, throwing when the inputs cannot describe a frame.
        /// </summary>
        public static OrthographicMapFraming Create(
            HexLayout layout,
            int mapRadius,
            float viewMargin,
            float aspect,
            float zoom = DefaultZoom)
        {
            OrthographicMapFraming framing;
            string error;
            if (!TryCreate(layout, mapRadius, viewMargin, aspect, zoom, out framing, out error))
            {
                throw new ArgumentException(error, nameof(layout));
            }

            return framing;
        }

        /// <summary>
        /// Creates a framing snapshot at zoom 1, which is the level where the whole map depth fits.
        /// </summary>
        public static bool TryCreate(
            HexLayout layout,
            int mapRadius,
            float viewMargin,
            float aspect,
            out OrthographicMapFraming framing,
            out string error)
        {
            return TryCreate(layout, mapRadius, viewMargin, aspect, DefaultZoom, out framing, out error);
        }

        /// <summary>
        /// Creates a framing snapshot at a zoom level, reporting invalid inputs instead of throwing.
        /// </summary>
        public static bool TryCreate(
            HexLayout layout,
            int mapRadius,
            float viewMargin,
            float aspect,
            float zoom,
            out OrthographicMapFraming framing,
            out string error)
        {
            framing = default(OrthographicMapFraming);

            if (mapRadius <= 0)
            {
                error = "Map radius must be positive.";
                return false;
            }

            if (!IsFinitePositive(layout.OuterRadius))
            {
                error = "Layout outer radius must be positive and finite.";
                return false;
            }

            if (!IsFinitePositive(layout.SecondaryScale))
            {
                error = "Layout secondary scale must be positive and finite.";
                return false;
            }

            if (!IsFinite(layout.Origin))
            {
                error = "Layout origin must be finite.";
                return false;
            }

            if (!IsFinitePositive(aspect))
            {
                error = "Viewport aspect must be positive and finite.";
                return false;
            }

            if (!IsFinite(viewMargin) || viewMargin < MinimumViewMargin)
            {
                error = "View margin must be finite and at least " + MinimumViewMargin
                    + " so that every row stays inside the frame.";
                return false;
            }

            if (!IsFinitePositive(zoom))
            {
                error = "Zoom must be positive and finite.";
                return false;
            }

            var outerRadius = layout.OuterRadius;
            var secondaryScale = layout.SecondaryScale;
            var radius = mapRadius;

            // Cell profile: the half extents of one hexagon's convex hull. The mesh applies the
            // secondary scale to the in-plane component, so the two extents are not equal.
            float cellAxisExtent;
            float cellSecondaryExtent;
            if (layout.Orientation == HexOrientation.Pointy)
            {
                // Vertices sit at 30 + 60k degrees: flat sides face +-X, points face +-plane.
                cellAxisExtent = outerRadius * Mathf.Sqrt(3f) * 0.5f;
                cellSecondaryExtent = outerRadius * secondaryScale;
            }
            else
            {
                // Vertices sit at 60k degrees: points face +-X, flat sides face +-plane.
                cellAxisExtent = outerRadius;
                cellSecondaryExtent = outerRadius * Mathf.Sqrt(3f) * 0.5f * secondaryScale;
            }

            // Center lattice spread: from the origin hex to the outermost ring's extreme hex.
            float centerAxisExtent;
            float centerSecondaryExtent;
            if (layout.Orientation == HexOrientation.Pointy)
            {
                centerAxisExtent = Mathf.Sqrt(3f) * radius * outerRadius;
                centerSecondaryExtent = 1.5f * radius * outerRadius * secondaryScale;
            }
            else
            {
                centerAxisExtent = 1.5f * radius * outerRadius * secondaryScale;
                centerSecondaryExtent = Mathf.Sqrt(3f) * radius * outerRadius * secondaryScale;
            }

            var mapHalfWidth = centerAxisExtent + cellAxisExtent;
            var mapHalfDepth = centerSecondaryExtent + cellSecondaryExtent;

            // Camera.orthographicSize is the half height, so "every row inside the frame" means
            // size >= mapHalfDepth at zoom 1. The size is derived from this base and the zoom in the
            // constructor, so a zoom change never has to know about the margin again.
            var baseOrthographicSize = mapHalfDepth * viewMargin;

            framing = new OrthographicMapFraming(
                mapHalfWidth,
                mapHalfDepth,
                layout.Origin,
                zoom,
                viewMargin,
                aspect,
                baseOrthographicSize);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Clamps an offset into <see cref="MinOffset"/>..<see cref="MaxOffset"/>.
        /// A non-finite offset maps to zero.
        /// </summary>
        public float ClampOffset(float offset)
        {
            if (!IsFinite(offset))
            {
                return 0f;
            }

            if (offset < MinOffset)
            {
                return MinOffset;
            }

            if (offset > MaxOffset)
            {
                return MaxOffset;
            }

            return offset;
        }

        /// <summary>
        /// Clamps an offset, reporting a non-finite input instead of silently replacing it.
        /// </summary>
        public bool TrySetOffset(float offset, out float clampedOffset, out string error)
        {
            if (!IsFinite(offset))
            {
                clampedOffset = 0f;
                error = "Offset must be finite.";
                return false;
            }

            clampedOffset = ClampOffset(offset);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Maps an offset to 0..1 across the movable range. Returns 0.5 when the camera is locked to the center.
        /// </summary>
        public float NormalizeOffset(float offset)
        {
            var range = MovableHalfRange;
            if (range <= 0f)
            {
                return 0.5f;
            }

            return (ClampOffset(offset) + range) / (range * 2f);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && IsFinite(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}

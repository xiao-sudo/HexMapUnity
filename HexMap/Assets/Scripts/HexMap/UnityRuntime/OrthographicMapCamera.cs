using System;
using HexMap.Core;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Where the camera takes the map plane from.
    /// </summary>
    public enum MapPlaneMode
    {
        /// <summary>
        /// Use the plane configured on the referenced <see cref="HexMapView"/>. This is the normal path.
        /// </summary>
        FollowMapView = 0,

        /// <summary>
        /// Look at the map's XY plane regardless of what the view says. Fails when the view disagrees.
        /// </summary>
        ForceXY = 1,

        /// <summary>
        /// Look at the map's XZ plane regardless of what the view says. Fails when the view disagrees.
        /// </summary>
        ForceXZ = 2
    }

    /// <summary>
    /// Drives a scene <see cref="Camera"/> into a straight-down orthographic view of a
    /// <see cref="HexMapView"/> map, pans it over the plane, and zooms it between a widest level that
    /// shows every row and a closest level of <see cref="MaxZoom"/>.
    /// <para>
    /// The framing comes from <see cref="OrthographicMapFraming"/>. Zoom 1 is the only level that keeps
    /// the whole map depth inside the frame; zooming in is allowed to drop rows, which is what opens up
    /// vertical panning.
    /// </para>
    /// <para>
    /// The camera aims at a center point expressed in the map's two plane coordinates, and aiming is
    /// clamped into the panning range, so an aim at the map's edge ends up beside the viewport center
    /// rather than outside the map.
    /// </para>
    /// <para>
    /// The camera keeps no memory of where it was asked to look. Aiming takes effect immediately and a
    /// zoom change keeps whatever center the camera has, so there is nothing to re-aim later. A caller
    /// that wants both at once asks for both at once with <see cref="TryZoomToPoint"/>, which is the only
    /// order that cannot clamp an aim against the range of the zoom it is about to leave.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrthographicMapCamera : MonoBehaviour
    {
        /// <summary>
        /// Aspect ratios within this distance of <see cref="TargetAspect"/> are treated as equal, so the
        /// serialized target is kept instead of being replaced by a negligible difference.
        /// </summary>
        public const float AspectTolerance = 0.001f;

        /// <summary>
        /// The zoom level that keeps every row inside the frame.
        /// </summary>
        public const float MinZoom = 1f;

        /// <summary>
        /// The origin cell of a radius based map, which is what <see cref="FocusOn"/> measures against.
        /// </summary>
        private static readonly HexCoord HexCoordOrigin = new HexCoord(0, 0);

        /// <summary>
        /// The closest zoom level a component starts with. A zoom level divides the frame that fits the
        /// whole map depth, so this number means the same thing whatever the map's radius, its cell size
        /// or the screen: at this level the visible height is <c>mapDepth * ViewMargin / MaxZoom</c>.
        /// <para>
        /// About 12 keeps a twelfth of the map depth in frame, which is what the aspect-derived limit it
        /// replaced gave on the portrait screen this game targets.
        /// </para>
        /// </summary>
        public const float DefaultMaxZoom = 12f;

        [SerializeField]
        private HexMapView m_HexMapView;

        [SerializeField]
        private Camera m_Camera;

        [SerializeField]
        [Tooltip("Optional. When set, its culling mask is copied onto the camera during refresh.")]
        private OrthographicMapLayerSettings m_LayerSettings;

        [SerializeField]
        [Tooltip("Distance from the map plane along its normal, in map local units.")]
        private float m_Height = 30f;

        [SerializeField]
        [Tooltip("Near clip distance from the camera, in map local units.")]
        private float m_Near = 28f;

        [SerializeField]
        [Tooltip("Far clip distance from the camera, in map local units.")]
        private float m_Far = 32f;

        [SerializeField]
        [Tooltip("How much empty space to leave around the map's depth. Must be at least 1.")]
        private float m_ViewMargin = 1.1f;

        [SerializeField]
        [Tooltip("Portrait 9:16. Used only when the real camera aspect is within the tolerance.")]
        private float m_TargetAspect = 9f / 16f;

        [SerializeField]
        private MapPlaneMode m_PlaneMode = MapPlaneMode.FollowMapView;

        [SerializeField]
        [Tooltip("Closest zoom level allowed. Larger means the player can zoom in further.")]
        private float m_MaxZoom = DefaultMaxZoom;

        [SerializeField]
        [Tooltip("Zoom levels per second while easing towards the target zoom.")]
        private float m_ZoomSpeed = 6f;

        [SerializeField]
        [HideInInspector]
        private Vector2 m_DesiredCenter;

        [SerializeField]
        [HideInInspector]
        private bool m_HasCenter;

        [SerializeField]
        [HideInInspector]
        private float m_Zoom = MinZoom;

        [SerializeField]
        [HideInInspector]
        private float m_TargetZoom = MinZoom;

        // Assigned by scene serialization only, so the compiler cannot see a write. Nothing in code sets
        // these, and the inspector is the intended author, so the warning is suppressed rather than faked
        // with a self-assignment.
#pragma warning disable 0649
        [SerializeField]
        [Tooltip("Optional starting focus, in map plane coordinates. Applied on the first refresh.")]
        private Vector2 m_InitialFocus;

        [SerializeField]
        private bool m_HasInitialFocus;
#pragma warning restore 0649

        private OrthographicMapFraming m_BaseFraming;
        private OrthographicMapFraming m_Framing;
        private HexLayout m_AppliedLayout;
        private Transform m_AppliedTransform;
        private float m_AppliedScale = 1f;
        private int m_AppliedRadius;
        private float m_AppliedViewMargin;
        private float m_AppliedAspect;
        private int m_AppliedCullingMask = -1;
        private bool m_HasFraming;

        public HexMapView HexMapView
        {
            get { return m_HexMapView; }
            set { m_HexMapView = value; }
        }

        public Camera Camera
        {
            get { return m_Camera; }
            set { m_Camera = value; }
        }

        public OrthographicMapLayerSettings LayerSettings
        {
            get { return m_LayerSettings; }
            set { m_LayerSettings = value; }
        }

        public MapPlaneMode PlaneMode
        {
            get { return m_PlaneMode; }
            set { m_PlaneMode = value; }
        }

        public float ViewMargin
        {
            get { return m_ViewMargin; }
            set { m_ViewMargin = value; }
        }

        public float TargetAspect
        {
            get { return m_TargetAspect; }
            set { m_TargetAspect = value; }
        }

        public float Height
        {
            get { return m_Height; }
            set { m_Height = value; }
        }

        /// <summary>
        /// The closest zoom level allowed. Kept as a plain number rather than derived from the framing:
        /// a zoom level is relative to the frame that fits the whole map, so it already means the same
        /// thing on a different map, radius or screen shape, and deriving it only made the limit depend
        /// on the viewport aspect in a way no single sentence could describe.
        /// </summary>
        public float MaxZoom
        {
            get { return m_MaxZoom; }
            set { m_MaxZoom = value; }
        }

        public float ZoomSpeed
        {
            get { return m_ZoomSpeed; }
            set { m_ZoomSpeed = value; }
        }

        public bool HasFraming
        {
            get { return m_HasFraming; }
        }

        /// <summary>
        /// The zoom level in use. Zoom 1 is the widest and keeps every row inside the frame.
        /// </summary>
        public float Zoom
        {
            get { return m_Zoom; }
            set
            {
                // The ceiling is a plain number now, so it applies before the first refresh too.
                m_Zoom = ClampZoom(value);
                m_TargetZoom = m_Zoom;
                if (m_HasFraming)
                {
                    ApplyZoom();
                }
            }
        }

        /// <summary>
        /// The zoom level the camera is easing towards. Writing it clamps into the allowed range.
        /// </summary>
        public float TargetZoom
        {
            get { return m_TargetZoom; }
            set { m_TargetZoom = ClampZoom(value); }
        }

        /// <summary>
        /// Restores a zoom level immediately, without easing towards it. Use this to put the camera back
        /// the way a caller found it; <see cref="TargetZoom"/> would animate across the difference instead.
        /// </summary>
        public void SetZoomImmediate(float zoom)
        {
            m_Zoom = ClampZoom(zoom);
            m_TargetZoom = m_Zoom;

            if (m_HasFraming)
            {
                ApplyZoom();
            }
        }

        /// <summary>
        /// The center the camera is aiming at, in the map's local plane coordinates.
        /// </summary>
        public Vector2 Center
        {
            get { return m_DesiredCenter; }
        }

        /// <summary>
        /// The current framing with the camera zoom applied. Returns false before the first refresh.
        /// </summary>
        public bool TryGetFraming(out OrthographicMapFraming framing)
        {
            framing = m_Framing;
            return m_HasFraming;
        }

        /// <summary>
        /// The panning limits along the map's local plane axes. Zero on an axis the frame covers entirely.
        /// </summary>
        public Vector2 MinOffset
        {
            get
            {
                if (!m_HasFraming)
                {
                    return Vector2.zero;
                }

                return new Vector2(-m_Framing.MovableHalfRange, -VerticalHalfRange);
            }
        }

        /// <summary>
        /// The panning limits along the map's local plane axes. Zero on an axis the frame covers entirely.
        /// </summary>
        public Vector2 MaxOffset
        {
            get
            {
                if (!m_HasFraming)
                {
                    return Vector2.zero;
                }

                return new Vector2(m_Framing.MovableHalfRange, VerticalHalfRange);
            }
        }

        /// <summary>
        /// The center mapped to 0..1 per axis, where the range is empty on an axis the frame covers.
        /// </summary>
        public Vector2 NormalizedOffset
        {
            get
            {
                var min = MinOffset;
                var max = MaxOffset;
                return new Vector2(
                    NormalizeAxis(m_DesiredCenter.x, min.x, max.x),
                    NormalizeAxis(m_DesiredCenter.y, min.y, max.y));
            }
        }

        /// <summary>
        /// True when the frame covers the map on both axes, so panning cannot move anything.
        /// </summary>
        public bool IsLockedToCenter
        {
            get
            {
                if (!m_HasFraming)
                {
                    return true;
                }

                return m_Framing.MovableHalfRange <= 0f && VerticalHalfRange <= 0f;
            }
        }

        /// <summary>
        /// Recomputes the framing from the view, the camera and the viewport, then writes the camera.
        /// Reports what is missing instead of throwing.
        /// </summary>
        public bool TryRefresh(out string error)
        {
            // The layer mask is validated and adopted on every path. It has to be: the cheap path below
            // exists for panning and zooming, and skipping the mask there would leave a freshly assigned
            // LayerSettings silently unapplied, which is the exact failure this feature is meant to catch.
            var cullingMask = -1;
            if (m_LayerSettings != null)
            {
                if (!m_LayerSettings.TryValidate(out error))
                {
                    return false;
                }

                cullingMask = m_LayerSettings.ResolvedCullingMask;
            }

            // Panning and zooming call through here, and the base framing only changes when the map or the
            // viewport configuration changes. When it cannot have changed, keep the snapshot and just
            // re-apply, which avoids rebuilding layouts and envelope maths per frame.
            if (m_HasFraming && IsCameraConfigurationUnchanged())
            {
                m_AppliedCullingMask = cullingMask;
                RefreshFramingFromZoom();
                ApplyToCamera();
                error = string.Empty;
                return true;
            }

            // The scalar inputs that feed the base framing are validated here rather than further down,
            // because the cheap path below must not be able to accept a configuration the long path would
            // reject. A view margin below one, for instance, silently clips rows, and reporting that only
            // on a full rebuild would make the refusal depend on whether the map happened to change.
            if (!IsFinite(m_ViewMargin) || m_ViewMargin < OrthographicMapFraming.MinimumViewMargin)
            {
                error = "View margin must be finite and at least " + OrthographicMapFraming.MinimumViewMargin
                    + " so that every row stays inside the frame.";
                return false;
            }

            if (m_HexMapView == null)
            {
                error = "A HexMapView reference is required.";
                return false;
            }

            if (m_Camera == null)
            {
                error = "A Camera reference is required.";
                return false;
            }

            if (!m_HexMapView.HasMap)
            {
                // Without this the layout is default and the envelope maths fails with a message about the
                // outer radius, which points at the wrong problem entirely.
                error = "HexMapView must be built before the camera can frame it.";
                return false;
            }

            if (m_HexMapView.Radius <= 0)
            {
                error = "HexMapView radius must be positive.";
                return false;
            }

            var layout = m_HexMapView.Layout;
            var aspect = ResolveAspect();

            if (!m_Camera.orthographic)
            {
                error = "The camera must be orthographic. Orthographic is a scene setting, not a runtime one; tick it in the inspector.";
                return false;
            }

            if (!IsFinitePositive(m_Near) || !IsFinitePositive(m_Far) || m_Far <= m_Near)
            {
                error = "Near and far clip distances must be positive and finite, and far must exceed near.";
                return false;
            }

            if (!IsFinite(m_Height))
            {
                error = "Camera height must be finite.";
                return false;
            }

            // MaxZoom == MinZoom is allowed: that is a camera which pans but never zooms, which is a
            // sane scene. Anything below the floor is not, and it would otherwise turn into a size of
            // zero rather than a complaint.
            if (!IsFinitePositive(m_MaxZoom) || m_MaxZoom < MinZoom)
            {
                error = "Max zoom must be finite and at least " + MinZoom + ".";
                return false;
            }

            if (!TryResolvePlaneAndOrientation(layout, out error))
            {
                return false;
            }

            var scale = m_HexMapView.transform.lossyScale.x;
            if (!IsFinitePositive(scale))
            {
                error = "The HexMapView transform must have a non-zero scale.";
                return false;
            }

            OrthographicMapFraming baseFraming;
            if (!OrthographicMapFraming.TryCreate(
                layout,
                m_HexMapView.Radius,
                m_ViewMargin,
                aspect,
                out baseFraming,
                out error))
            {
                return false;
            }

            m_BaseFraming = baseFraming;
            m_AppliedLayout = layout;
            m_AppliedTransform = m_HexMapView.transform;
            m_AppliedScale = scale;
            m_AppliedRadius = m_HexMapView.Radius;
            m_AppliedViewMargin = m_ViewMargin;
            m_AppliedAspect = aspect;
            m_AppliedCullingMask = cullingMask;
            m_HasFraming = true;

            if (!IsFinitePositive(m_Zoom))
            {
                // A freshly added component serializes zero, which would mean an infinite magnification.
                m_Zoom = MinZoom;
            }

            if (!IsFinitePositive(m_TargetZoom))
            {
                m_TargetZoom = m_Zoom;
            }

            m_Zoom = ClampZoom(m_Zoom);
            m_TargetZoom = ClampZoom(m_TargetZoom);

            ApplyInitialFocus();

            if (!HasUserCenter)
            {
                m_DesiredCenter = Vector2.zero;
            }

            m_Framing = m_BaseFraming.WithZoom(m_Zoom);
            ClampCenter();
            ApplyToCamera();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Creates the framing snapshot for the current configuration without touching the camera.
        /// </summary>
        public bool TryCreateFraming(out OrthographicMapFraming framing, out string error)
        {
            framing = default(OrthographicMapFraming);

            if (m_HexMapView == null)
            {
                error = "A HexMapView reference is required.";
                return false;
            }

            if (!m_HexMapView.HasMap)
            {
                error = "HexMapView must be built before the camera can frame it.";
                return false;
            }

            if (m_HexMapView.Radius <= 0)
            {
                error = "HexMapView radius must be positive.";
                return false;
            }

            if (!IsFinite(m_ViewMargin) || m_ViewMargin < OrthographicMapFraming.MinimumViewMargin)
            {
                error = "View margin must be finite and at least " + OrthographicMapFraming.MinimumViewMargin
                    + " so that every row stays inside the frame.";
                return false;
            }

            var layout = m_HexMapView.Layout;

            if (!TryResolvePlaneAndOrientation(layout, out error))
            {
                return false;
            }

            return OrthographicMapFraming.TryCreate(
                layout,
                m_HexMapView.Radius,
                m_ViewMargin,
                ResolveAspect(),
                m_Zoom,
                out framing,
                out error);
        }

        /// <summary>
        /// Moves the camera center along the map's local plane axes, clamped per axis.
        /// </summary>
        public bool TrySetOffset(Vector2 offset, out string error)
        {
            if (!m_HasFraming)
            {
                error = "Refresh the camera before setting an offset.";
                return false;
            }
            if (!IsFinite(offset.x))
            {
                error = "Offset X must be finite.";
                return false;
            }

            if (!IsFinite(offset.y))
            {
                error = "Offset Y must be finite.";
                return false;
            }

            m_DesiredCenter = ClampToRange(offset);
            m_HasCenter = true;

            // Dragging only moves the camera. Nothing else is recorded, so a later zoom keeps this
            // center instead of pulling the view back to where it used to be aimed.
            ApplyToCamera();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Sets the zoom and keeps the point under the viewport anchor where it is.
        /// </summary>
        /// <param name="targetZoom">The requested zoom level, clamped into range.</param>
        /// <param name="viewportAnchor">
        /// The anchor in viewport coordinates: each component runs -1 to 1 from the center. X maps to the
        /// map's local X axis and Y to its local plane axis. Zero is the viewport center and degenerates
        /// to plain centered zooming.
        /// </param>
        public bool TryZoomTo(float targetZoom, Vector2 viewportAnchor, out string error)
        {
            if (!m_HasFraming)
            {
                error = "Refresh the camera before zooming.";
                return false;
            }

            if (!IsFinite(targetZoom))
            {
                error = "Target zoom must be finite.";
                return false;
            }

            if (!IsFinite(viewportAnchor.x) || !IsFinite(viewportAnchor.y))
            {
                error = "Viewport anchor must be finite.";
                return false;
            }

            var clamped = ClampZoom(targetZoom);
            var oldSize = m_Framing.OrthographicSize;
            var newSize = m_BaseFraming.OrthographicSize / clamped;

            var anchorOffset = new Vector2(
                viewportAnchor.x * m_Framing.VisibleWidth * 0.5f,
                viewportAnchor.y * m_Framing.VisibleHeight * 0.5f);
            var growth = 1f - newSize / oldSize;

            m_DesiredCenter = m_DesiredCenter + anchorOffset * growth;
            m_DesiredCenter = ClampToRange(m_DesiredCenter);
            m_HasCenter = true;
            m_Zoom = clamped;
            m_TargetZoom = clamped;

            ApplyZoom();

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Sets the zoom and aims at a world point, in that order.
        /// <para>
        /// Immediate rather than eased: aiming at a point while the frame is still changing would be
        /// aiming at a moving target, and re-aiming every frame of an ease is exactly the state this
        /// class no longer keeps. Set the frame, then aim inside it.
        /// </para>
        /// </summary>
        public bool TryZoomToPoint(float zoom, Vector3 worldPoint, out string error)
        {
            if (!m_HasFraming)
            {
                error = "Refresh the camera before zooming to a point.";
                return false;
            }

            if (!IsFinite(worldPoint))
            {
                error = "World point must be finite.";
                return false;
            }

            var clamped = ClampZoom(zoom);
            m_Zoom = clamped;
            m_TargetZoom = clamped;

            if (clamped <= MinZoom)
            {
                // Reaching the widest level re-centers, so an aim asking for it is ignored on purpose.
                m_DesiredCenter = Vector2.zero;
                m_HasCenter = false;
            }
            else
            {
                // The frame for the new zoom has to exist before the aim is clamped. Clamping first
                // measures the aim against the range of the zoom being left, which is the exact mistake
                // this method exists to prevent: at the widest level the vertical range is zero, so an
                // edge aim collapses onto the horizontal axis.
                m_Framing = m_BaseFraming.WithZoom(m_Zoom);
                m_DesiredCenter = ClampToRange(ToPlaneCoordinates(worldPoint));
                m_HasCenter = true;
            }

            ApplyZoom();

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Aims the camera at a cell. The center ends up clamped, so an edge cell sits beside the
        /// viewport center rather than outside the map. Like <see cref="FocusOnWorld"/> this is a
        /// one-shot request rather than a setting that later zoom changes return to.
        /// </summary>
        public bool FocusOn(HexCoord coordinate, out string error)
        {
            if (!m_HasFraming)
            {
                error = "Refresh the camera before focusing.";
                return false;
            }

            // A radius-R hex map is exactly the set of cells within R steps of the origin, so the
            // distance test is the membership test and needs nothing from the renderer.
            if (HexCoord.Distance(HexCoordOrigin, coordinate) > m_HexMapView.Radius)
            {
                error = "The coordinate is outside the map.";
                return false;
            }

            var world = m_AppliedTransform.TransformPoint(m_AppliedLayout.HexToWorld(coordinate));
            return FocusOnWorld(world, out error);
        }

        /// <summary>
        /// Aims the camera at a world point. The center ends up clamped, so a point outside the
        /// panning range sits at the edge of the frame instead. Nothing is remembered: a later zoom
        /// keeps the center this produced rather than coming back here.
        /// </summary>
        public bool FocusOnWorld(Vector3 worldPoint, out string error)
        {
            if (!m_HasFraming)
            {
                error = "Refresh the camera before focusing.";
                return false;
            }

            if (!IsFinite(worldPoint))
            {
                error = "World point must be finite.";
                return false;
            }

            m_DesiredCenter = ClampToRange(ToPlaneCoordinates(worldPoint));
            m_HasCenter = true;
            ApplyToCamera();
            error = string.Empty;
            return true;
        }

        private bool HasUserCenter
        {
            get { return m_HasCenter; }
        }

        private float VerticalHalfRange
        {
            get
            {
                if (!m_HasFraming)
                {
                    return 0f;
                }

                var range = m_Framing.MapHalfDepth - m_Framing.VisibleHeight * 0.5f;
                return range > 0f ? range : 0f;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances the zoom easing by one step. Exposed so a caller or a test can drive the easing
        /// without owning the frame loop; <see cref="Update"/> calls it with the frame delta.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!m_HasFraming)
            {
                return;
            }

            if (Mathf.Approximately(m_Zoom, m_TargetZoom))
            {
                return;
            }

            m_Zoom = m_ZoomSpeed > 0f && deltaTime > 0f
                ? Mathf.MoveTowards(m_Zoom, m_TargetZoom, m_ZoomSpeed * deltaTime)
                : m_TargetZoom;

            ApplyZoom();
        }

        /// <summary>
        /// Rebuilds the framing for the current zoom, keeps the center by re-clamping it, and writes the
        /// camera. This is the single place a zoom change flows through.
        /// </summary>
        /// <summary>
        /// True when nothing the base framing depends on has changed since the last refresh, so the base
        /// framing can be reused. Deliberately lenient: a false positive only costs one extra rebuild, but a
        /// false negative silently ignores a configuration change, so every input that feeds the base
        /// framing is compared here.
        /// </summary>
        private bool IsCameraConfigurationUnchanged()
        {
            if (m_AppliedTransform != m_HexMapView.transform)
            {
                return false;
            }

            if (!m_HexMapView.HasMap || m_HexMapView.Radius <= 0)
            {
                return false;
            }

            if (m_Camera == null || !m_Camera.orthographic)
            {
                return false;
            }

            // Scalars matter as much as the layout: the view margin and the aspect ratio both change the
            // base framing, and skipping them let a bad margin through on a repeat refresh.
            return m_AppliedRadius == m_HexMapView.Radius
                && m_AppliedViewMargin == m_ViewMargin
                && m_AppliedAspect == ResolveAspect()
                && LayoutEquals(m_AppliedLayout, m_HexMapView.Layout);
        }

        private static bool LayoutEquals(HexLayout left, HexLayout right)
        {
            return left.Orientation == right.Orientation
                && left.Plane == right.Plane
                && left.OuterRadius == right.OuterRadius
                && left.SecondaryScale == right.SecondaryScale
                && left.Origin == right.Origin;
        }

        /// <summary>
        /// Rebuilds the zoomed framing from the retained base framing and keeps the center, re-clamped
        /// against the range the new frame leaves.
        /// </summary>
        private void RefreshFramingFromZoom()
        {
            m_Zoom = ClampZoom(m_Zoom);
            m_Framing = m_BaseFraming.WithZoom(m_Zoom);
            ClampCenter();
        }

        private void ApplyZoom()
        {
            if (!m_HasFraming)
            {
                return;
            }

            m_Zoom = ClampZoom(m_Zoom);
            m_Framing = m_BaseFraming.WithZoom(m_Zoom);

            if (m_Zoom <= MinZoom)
            {
                // The widest level is the "see everything" state, so it is always centered.
                m_DesiredCenter = Vector2.zero;
                m_HasCenter = false;
            }

            // A zoom change keeps the center it finds: the clamp below is all that moves it, and it
            // only pulls the center back when a narrower frame no longer contains it.
            ClampCenter();
            ApplyToCamera();
        }

        /// <summary>
        /// Applies the serialized starting center, but only while nothing else has claimed the center.
        /// A drag, an aim request or a zoom all mean the player or the game has taken over, and a later
        /// refresh must not undo that.
        /// </summary>
        private void ApplyInitialFocus()
        {
            if (!m_HasInitialFocus || HasUserCenter)
            {
                return;
            }

            // The initial focus is already in map plane coordinates, so it only needs the plane's
            // perpendicular axis filled in to become a local point. The clamp happens after the framing
            // for the current zoom exists, which is why this only records where to look.
            var local = m_AppliedLayout.Plane == HexPlane.XY
                ? new Vector3(m_InitialFocus.x, m_InitialFocus.y, m_AppliedLayout.Origin.z)
                : new Vector3(m_InitialFocus.x, m_AppliedLayout.Origin.y, m_InitialFocus.y);

            m_DesiredCenter = ToPlaneCoordinates(m_AppliedTransform.TransformPoint(local));
            m_HasCenter = true;
        }

        private Vector2 ClampToRange(Vector2 offset)
        {
            var min = MinOffset;
            var max = MaxOffset;
            return new Vector2(
                offset.x < min.x ? min.x : (offset.x > max.x ? max.x : offset.x),
                offset.y < min.y ? min.y : (offset.y > max.y ? max.y : offset.y));
        }

        private void ClampCenter()
        {
            m_DesiredCenter = ClampToRange(m_DesiredCenter);
        }

        private float ClampZoom(float zoom)
        {
            if (!IsFinitePositive(zoom))
            {
                return MinZoom;
            }

            // A ceiling that cannot be a zoom level must not become the ceiling: the floor is the safest
            // answer here, and TryRefresh refuses to frame the camera when the serialized value is bad,
            // so the mistake is reported rather than quietly turning into a zero size.
            var max = MaxZoom > MinZoom ? MaxZoom : MinZoom;
            return zoom < MinZoom ? MinZoom : (zoom > max ? max : zoom);
        }

        private Vector2 ToPlaneCoordinates(Vector3 worldPoint)
        {
            var local = m_AppliedTransform.InverseTransformPoint(worldPoint);

            // The plane axes are the layout's plane coordinate plus X, so XY uses Y and XZ uses Z.
            return m_AppliedLayout.Plane == HexPlane.XY
                ? new Vector2(local.x, local.y)
                : new Vector2(local.x, local.z);
        }

        private static float NormalizeAxis(float value, float min, float max)
        {
            var range = max - min;
            if (range <= 0f)
            {
                return 0.5f;
            }

            if (value < min)
            {
                value = min;
            }
            else if (value > max)
            {
                value = max;
            }

            return (value - min) / range;
        }

        private bool TryResolvePlaneAndOrientation(HexLayout layout, out string error)
        {
            switch (m_PlaneMode)
            {
                case MapPlaneMode.ForceXY:
                    if (layout.Plane != HexPlane.XY)
                    {
                        error = "PlaneMode forces XY but the HexMapView plane is " + layout.Plane + ".";
                        return false;
                    }

                    break;

                case MapPlaneMode.ForceXZ:
                    if (layout.Plane != HexPlane.XZ)
                    {
                        error = "PlaneMode forces XZ but the HexMapView plane is " + layout.Plane + ".";
                        return false;
                    }

                    break;

                case MapPlaneMode.FollowMapView:
                    break;

                default:
                    error = "PlaneMode is not a defined MapPlaneMode value.";
                    return false;
            }

            error = string.Empty;
            return true;
        }

        private float ResolveAspect()
        {
            var cameraAspect = m_Camera.aspect;
            if (IsFinitePositive(cameraAspect) &&
                IsFinitePositive(m_TargetAspect) &&
                Mathf.Abs(cameraAspect - m_TargetAspect) <= AspectTolerance)
            {
                return m_TargetAspect;
            }

            return cameraAspect;
        }

        private void ApplyToCamera()
        {
            if (!m_HasFraming || m_AppliedTransform == null || m_Camera == null)
            {
                return;
            }

            // Screen right is the map's local +X because the camera's up comes from the plane's local +Z.
            var right = m_AppliedTransform.TransformDirection(Vector3.right).normalized;
            var upLocal = m_AppliedLayout.Plane == HexPlane.XY ? Vector3.up : Vector3.forward;
            var up = m_AppliedTransform.TransformDirection(upLocal).normalized;
            var forward = Vector3.Cross(right, up);

            // forward points from the plane towards the camera's look direction, so the camera has to
            // retreat along -forward to sit above the map.
            var position = m_AppliedTransform.TransformPoint(m_Framing.Origin)
                + right * (m_DesiredCenter.x * m_AppliedScale)
                + up * (m_DesiredCenter.y * m_AppliedScale)
                - forward * (m_Height * m_AppliedScale);

            m_Camera.orthographicSize = m_Framing.OrthographicSize * m_AppliedScale;
            m_Camera.nearClipPlane = m_Near * m_AppliedScale;
            m_Camera.farClipPlane = m_Far * m_AppliedScale;
            if (m_AppliedCullingMask >= 0)
            {
                m_Camera.cullingMask = m_AppliedCullingMask;
            }

            m_Camera.transform.position = position;
            m_Camera.transform.rotation = Quaternion.LookRotation(forward, up);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private void Start()
        {
            // Frame once on load so the scene shows the intended view without any caller.
            // Start runs after every Awake, which matters because HexMapView builds its map in Awake.
            // Viewport changes still need an explicit TryRefresh from the caller.
            string error;
            if (!TryRefresh(out error))
            {
                Debug.LogError("OrthographicMapCamera could not frame the map: " + error, this);
            }
        }
    }
}

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
    /// <para>
    /// Zoom is applied immediately and never eased here. An ease would have to re-apply the gesture's
    /// anchor on every step, so the animation belongs with whoever knows the anchor; this component
    /// offers one absolute level at a time and lets the caller ramp it across frames.
    /// </para>
    /// <para>
    /// The framing is a snapshot of the view, the camera and the viewport, and the component does not
    /// watch for changes to those. It rebuilds on demand: <see cref="TryRefresh"/> for a caller that
    /// knows something changed, and <see cref="TryRefreshIfStale"/> for one that only knows its input is
    /// about to start doing something, which is what keeps a viewport change from going unnoticed
    /// without any per-frame work here.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrthographicMapCamera : MonoBehaviour
    {
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
        private MapPlaneMode m_PlaneMode = MapPlaneMode.FollowMapView;

        [SerializeField]
        [Tooltip("Closest zoom level allowed. Larger means the player can zoom in further.")]
        private float m_MaxZoom = DefaultMaxZoom;

        [SerializeField]
        [HideInInspector]
        private Vector2 m_DesiredCenter;

        /// <summary>
        /// Whether anything has claimed the center. It exists because "the player dragged the map back to
        /// the middle" and "nobody has ever aimed this camera" are both <c>(0, 0)</c> and cannot be told
        /// apart by the value alone. A full rebuild keeps a claimed center and clears an unclaimed one.
        /// </summary>
        [SerializeField]
        [HideInInspector]
        private bool m_HasCenter;

        [SerializeField]
        [HideInInspector]
        private float m_Zoom = MinZoom;

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

        public bool HasFraming
        {
            get { return m_HasFraming; }
        }

        /// <summary>
        /// The zoom level in use. Zoom 1 is the widest and keeps every row inside the frame.
        /// <para>
        /// Writing it applies the new frame immediately. There is no target to ease towards: a caller
        /// that wants an animated zoom ramps this value itself, because only the caller knows whether
        /// the change belongs to an anchored gesture.
        /// </para>
        /// </summary>
        public float Zoom
        {
            get { return m_Zoom; }
            set
            {
                // The ceiling is a plain number now, so it applies before the first refresh too.
                m_Zoom = ClampZoom(value);
                if (m_HasFraming)
                {
                    ApplyZoom();
                }
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
                // The view reference is checked here rather than inside the settings: the settings no
                // longer holds one, and this check has to happen before the mask is validated, because
                // validating it needs the layer the map's cells are on.
                if (m_HexMapView == null)
                {
                    error = "A HexMapView reference is required.";
                    return false;
                }

                if (!m_LayerSettings.TryValidate(m_HexMapView.CellLayer, out error))
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
            var aspect = m_Camera.aspect;

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

            m_Zoom = ClampZoom(m_Zoom);

            if (!HasUserCenter)
            {
                // Nothing has aimed this camera, so a rebuild has no center worth keeping. A center that
                // was aimed at is kept instead, which is why the flag exists rather than the value.
                m_DesiredCenter = Vector2.zero;
            }

            m_Framing = m_BaseFraming.WithZoom(m_Zoom);
            ClampCenter();
            ApplyToCamera();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Re-frames the camera only when the snapshot behind <see cref="TryRefresh"/> no longer holds.
        /// <para>
        /// This is the entry point for a caller that cannot know whether anything changed but does know
        /// that its input is about to start doing something, so it must not be called per frame. The
        /// check compares the camera's own reported configuration against what this component snapshotted,
        /// which is knowledge that belongs here rather than in every caller: a viewport change arrives
        /// through <see cref="Camera.aspect"/> with no callback and no setter of ours behind it.
        /// </para>
        /// <para>
        /// When nothing changed this does not touch the camera at all, and it does not re-validate the
        /// layer settings either, so a mistyped mask reports its one loud failure from the refresh that
        /// found it instead of once per gesture. An empty <paramref name="error"/> with a true result
        /// means "the framing in hand is current".
        /// </para>
        /// </summary>
        public bool TryRefreshIfStale(out string error)
        {
            if (m_Camera == null)
            {
                error = "A Camera reference is required.";
                return false;
            }

            if (m_HexMapView == null)
            {
                error = "A HexMapView reference is required.";
                return false;
            }

            // Nothing changed, so the framing in hand is current and the camera is left alone entirely.
            if (m_HasFraming && IsCameraConfigurationUnchanged())
            {
                error = string.Empty;
                return true;
            }

            // Only a view that has no map at all is refused up front, because an attempt there can only
            // reproduce the failure the refresh that built nothing already reported. Every other refusal
            // is left to the full path: a refresh that failed before the map was built must be allowed to
            // retry once it is, since nothing else calls back into this component.
            if (!m_HexMapView.HasMap)
            {
                error = "HexMapView must be built before the camera can frame it.";
                return false;
            }

            return TryRefresh(out error);
        }

        /// <summary>
        /// Creates the framing snapshot for the current configuration and the current zoom, without
        /// touching the camera. Reports what is missing instead of throwing, in the same order
        /// <see cref="TryRefresh"/> uses so that both refuse the same configuration for the same reason.
        /// </summary>
        public bool TryCreateFraming(out OrthographicMapFraming framing, out string error)
        {
            framing = default(OrthographicMapFraming);

            if (m_HexMapView == null)
            {
                error = "A HexMapView reference is required.";
                return false;
            }

            // The camera is only read for its aspect, but it is required all the same: reading through a
            // missing reference would throw where every other refusal in this class reports.
            if (m_Camera == null)
            {
                error = "A Camera reference is required.";
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
                m_Camera.aspect,
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

            ApplyZoom();

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Sets the zoom and aims at a world point, in that order.
        /// <para>
        /// One call rather than two because the aim has to be clamped against the frame it is about to
        /// live in. Aiming first would clamp against the range of the zoom being left, and an edge aim
        /// can never recover from that. Set the frame, then aim inside it.
        /// </para>
        /// <para>
        /// No zoom level is special-cased, the widest included: the aim is clamped there like anywhere
        /// else, so it keeps its horizontal component and loses only the vertical one, which the clamp
        /// removes on its own because the widest frame covers the whole depth.
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

            // The frame for the new zoom has to exist before the aim is clamped. Clamping first measures
            // the aim against the range of the zoom being left, which is the exact mistake this method
            // exists to prevent: at the widest level the vertical range is zero, so an edge aim collapses
            // onto the horizontal axis.
            m_Framing = m_BaseFraming.WithZoom(m_Zoom);

            // Every level is treated the same, the widest included. It used to re-center and drop the aim
            // on the grounds that it is the see-everything state; that threw the horizontal half of the
            // aim away for no reason, because the vertical half is what the clamp removes anyway (the
            // widest frame covers the whole depth, so its vertical range is zero).
            m_DesiredCenter = ClampToRange(ToPlaneCoordinates(worldPoint));
            m_HasCenter = true;

            ApplyZoom();

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Aims the camera at a cell, keeping the current zoom. The center ends up clamped, so an edge
        /// cell sits beside the viewport center rather than outside the map. Like
        /// <see cref="FocusOnWorld"/> this is a one-shot request rather than a setting that later zoom
        /// changes return to. Use <see cref="TryZoomToCell"/> to pick the zoom in the same call.
        /// </summary>
        public bool FocusOn(HexCoord coordinate, out string error)
        {
            Vector3 world;
            if (!TryGetCellWorldCenter(coordinate, out world, out error))
            {
                return false;
            }

            return FocusOnWorld(world, out error);
        }

        /// <summary>
        /// Aims the camera at a cell and sets the zoom, in that order, in one call.
        /// <para>
        /// The one call is the point of it: the aim has to be clamped against the frame it is about to
        /// live in, so a caller that aims first and changes the zoom second clamps against the range of
        /// the zoom it is leaving, and an edge cell can never recover from that. This is
        /// <see cref="TryZoomToPoint"/> with the cell-to-world conversion done here, because every caller
        /// that starts from a cell would otherwise write that conversion itself, and the wrong order is
        /// exactly the mistake that is easy to make while doing it.
        /// </para>
        /// </summary>
        public bool TryZoomToCell(HexCoord coordinate, float zoom, out string error)
        {
            if (!IsFinite(zoom))
            {
                error = "Target zoom must be finite.";
                return false;
            }

            Vector3 world;
            if (!TryGetCellWorldCenter(coordinate, out world, out error))
            {
                return false;
            }

            return TryZoomToPoint(zoom, world, out error);
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

        /// <summary>
        /// Resolves a cell to its world center, refusing anything that cannot be resolved.
        /// <para>
        /// A radius-R hex map is exactly the set of cells within R steps of the origin, so the distance
        /// test is the membership test and needs nothing from the renderer.
        /// </para>
        /// </summary>
        private bool TryGetCellWorldCenter(HexCoord coordinate, out Vector3 worldPoint, out string error)
        {
            worldPoint = Vector3.zero;

            if (!m_HasFraming)
            {
                // The applied layout and transform are the snapshots a refresh leaves behind, so they are
                // the reason this has to be asked after one rather than before.
                error = "Refresh the camera before focusing.";
                return false;
            }

            if (HexCoord.Distance(HexCoordOrigin, coordinate) > m_HexMapView.Radius)
            {
                error = "The coordinate is outside the map.";
                return false;
            }

            worldPoint = m_AppliedTransform.TransformPoint(m_AppliedLayout.HexToWorld(coordinate));
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

        /// <summary>
        /// True when nothing the base framing depends on has changed since the last refresh, so the base
        /// framing can be reused. Deliberately lenient: a false positive only costs one extra rebuild, but a
        /// false negative silently ignores a configuration change, so every input that feeds the base
        /// framing is compared here.
        /// <para>
        /// The layout is compared as a snapshot (<see cref="HexLayout.Equals(HexLayout)"/> identifies the
        /// build it came from), so a rebuilt map is detected without comparing its float fields. The scalars
        /// use <see cref="Mathf.Approximately"/> so that a difference in the last bits alone counts as
        /// unchanged; genuine edits are far larger than that tolerance.
        /// </para>
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
            // Approximately rather than == so that a value which only differs in its last bits counts as
            // unchanged, which is the cheaper and visually identical choice for a rebuild this small.
            return Mathf.Approximately(m_AppliedRadius, m_HexMapView.Radius)
                && Mathf.Approximately(m_AppliedViewMargin, m_ViewMargin)
                && Mathf.Approximately(m_AppliedAspect, m_Camera.aspect)
                && m_AppliedLayout.Equals(m_HexMapView.Layout);
        }

        /// <summary>
        /// Rebuilds the zoomed framing from the retained base framing and keeps the center, re-clamped
        /// against the range the new frame leaves.
        /// <para>
        /// The base framing is kept as it is, including the aspect it was built from, so a viewport that
        /// changed since the last full rebuild stays stale for exactly as long as this path is taken. That
        /// is deliberate: the comparison in <see cref="IsCameraConfigurationUnchanged"/> reads
        /// <c>m_AppliedAspect</c> rather than the framing, so the next <see cref="TryRefresh"/> sees the
        /// same difference and takes the full path instead.
        /// </para>
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

            // A zoom change keeps the center it finds, at every level including the widest. The clamp
            // below is the only thing that moves it, and it moves one axis at a time: at the widest level
            // the frame covers the whole depth, so the vertical range is zero and the vertical component
            // collapses to the middle on its own, while the horizontal component survives. That is what
            // lets the player look at the left half of the map and then zoom in on it — re-centering here
            // would throw that aim away at the exact moment it is most useful.
            ClampCenter();
            ApplyToCamera();
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
            // A viewport change afterwards is picked up by whoever calls TryRefreshIfStale, and by a
            // caller that knows something changed through TryRefresh; nothing here watches for either.
            string error;
            if (!TryRefresh(out error))
            {
                Debug.LogError("OrthographicMapCamera could not frame the map: " + error, this);
            }
        }
    }
}

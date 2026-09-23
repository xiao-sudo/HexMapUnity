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
    /// shows every row and a closest level limited by <see cref="MinVisibleWidthRatio"/>.
    /// <para>
    /// The framing comes from <see cref="OrthographicMapFraming"/>. Zoom 1 is the only level that keeps
    /// the whole map depth inside the frame; zooming in is allowed to drop rows, which is what opens up
    /// vertical panning.
    /// </para>
    /// <para>
    /// The camera always aims at a center point expressed in the map's two plane coordinates. A focus
    /// is a request, not a position: the center is the focus clamped into the current panning range, so
    /// a focus at the map's edge ends up beside the viewport center rather than outside the map.
    /// </para>
    /// <para>
    /// The focus is applied when it is set and when a zoom change happens outside a gesture. Dragging
    /// moves the center without clearing the focus, so the next non-gesture zoom change re-aims at it.
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
        /// The smallest visible fraction of the map width the zoom clamps to, which sets the closest
        /// zoom level. Expressed as a fraction so a different map does not need a different number.
        /// </summary>
        public const float DefaultMinVisibleWidthRatio = 0.15f;

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
        [Tooltip("Smallest visible fraction of the map width. Smaller means a closer maximum zoom.")]
        private float m_MinVisibleWidthRatio = DefaultMinVisibleWidthRatio;

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

        [SerializeField]
        [Tooltip("Optional starting focus, in map plane coordinates. Applied on the first refresh.")]
        private Vector2 m_InitialFocus;

        [SerializeField]
        private bool m_HasInitialFocus;

        private OrthographicMapFraming m_BaseFraming;
        private OrthographicMapFraming m_Framing;
        private HexLayout m_AppliedLayout;
        private Transform m_AppliedTransform;
        private float m_AppliedScale = 1f;
        private int m_AppliedCullingMask = -1;
        private bool m_HasFraming;
        private Vector2 m_Focus;
        private bool m_HasFocus;
        private bool m_IsGestureActive;

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

        public float MinVisibleWidthRatio
        {
            get { return m_MinVisibleWidthRatio; }
            set { m_MinVisibleWidthRatio = value; }
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
                m_Zoom = m_HasFraming ? ClampZoom(value) : NormalizeZoom(value);
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
            set { m_TargetZoom = m_HasFraming ? ClampZoom(value) : NormalizeZoom(value); }
        }

        /// <summary>
        /// The highest zoom level allowed, derived from <see cref="MinVisibleWidthRatio"/>.
        /// </summary>
        public float MaxZoom
        {
            get
            {
                if (!m_HasFraming || m_MinVisibleWidthRatio <= 0f)
                {
                    return MinZoom;
                }

                var limit = 1f / (m_MinVisibleWidthRatio * m_BaseFraming.Aspect);
                return limit > MinZoom ? limit : MinZoom;
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
        /// The focus last requested, in the map's local plane coordinates. Reading it does not imply
        /// the camera is aiming at it; <see cref="Center"/> is where the camera actually is.
        /// </summary>
        public Vector2 Focus
        {
            get { return m_Focus; }
        }

        public bool HasFocus
        {
            get { return m_HasFocus; }
        }

        /// <summary>
        /// True when a gesture owns the pan and zoom. While true, a zoom change does not re-aim at the focus.
        /// </summary>
        public bool IsGestureActive
        {
            get { return m_IsGestureActive; }
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
            // Panning and zooming call through here, and the base framing only changes when the map or the
            // viewport configuration changes. When it cannot have changed, keep the snapshot and just
            // re-apply, which avoids rebuilding layouts and envelope maths per frame.
            if (m_HasFraming && IsCameraConfigurationUnchanged())
            {
                RefreshFramingFromZoom();
                ApplyToCamera();
                error = string.Empty;
                return true;
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

            HexLayout layout;
            if (!TryGetLayout(out layout, out error))
            {
                return false;
            }

            if (!TryResolvePlaneAndOrientation(layout, out error))
            {
                return false;
            }

            var cullingMask = -1;
            if (m_LayerSettings != null)
            {
                if (!m_LayerSettings.TryValidate(out error))
                {
                    return false;
                }

                cullingMask = m_LayerSettings.ResolvedCullingMask;
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
                ResolveAspect(),
                out baseFraming,
                out error))
            {
                return false;
            }

            m_BaseFraming = baseFraming;
            m_AppliedLayout = layout;
            m_AppliedTransform = m_HexMapView.transform;
            m_AppliedScale = scale;
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

            if (!m_HasFocus && !HasUserCenter)
            {
                m_DesiredCenter = Vector2.zero;
            }

            m_Framing = m_BaseFraming.WithZoom(m_Zoom);
            AlignCenterToFocus();
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

            HexLayout layout;
            if (!TryGetLayout(out layout, out error))
            {
                return false;
            }

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

            // Dragging moves the camera without forgetting the focus: the next non-gesture zoom change
            // re-aims at it. Clearing the focus here would make zoom stop tracking it silently.
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

            // An anchored zoom is itself a gesture, so it outranks the focus. Without this the
            // re-aim rule would discard the anchored center and snap the camera back to the focus,
            // which is exactly the case the anchor exists to serve.
            var wasGestureActive = m_IsGestureActive;
            m_IsGestureActive = true;
            ApplyZoom();
            m_IsGestureActive = wasGestureActive;

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Aims the camera at a cell. The center ends up clamped, so an edge cell sits beside the
        /// viewport center rather than outside the map.
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
        /// panning range sits at the edge of the frame instead.
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

            m_Focus = ToPlaneCoordinates(worldPoint);
            m_HasFocus = true;
            m_DesiredCenter = ClampToRange(m_Focus);
            m_HasCenter = true;
            ApplyToCamera();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Clears the focus so later zoom changes stop re-aiming at it.
        /// </summary>
        public void ClearFocus()
        {
            m_HasFocus = false;
        }

        /// <summary>
        /// Declares that a gesture owns the pan and zoom. Zoom changes while a gesture is active do not
        /// re-aim at the focus.
        /// </summary>
        public void BeginGesture()
        {
            m_IsGestureActive = true;
        }

        /// <summary>
        /// Ends the gesture. The focus is kept, so the next non-gesture zoom change re-aims at it.
        /// </summary>
        public void EndGesture()
        {
            m_IsGestureActive = false;
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
        /// Rebuilds the framing for the current zoom, re-aims at the focus when it should, and writes
        /// the camera. This is the single place a zoom change flows through.
        /// </summary>
        /// <summary>
        /// True when nothing the base framing depends on has changed since the last refresh, so the base
        /// framing can be reused. Deliberately lenient: a false positive only costs one extra rebuild.
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

            return LayoutEquals(m_AppliedLayout, m_HexMapView.Layout);
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
        /// Rebuilds the zoomed framing from the retained base framing and re-applies the centering rules.
        /// </summary>
        private void RefreshFramingFromZoom()
        {
            m_Zoom = ClampZoom(m_Zoom);
            m_Framing = m_BaseFraming.WithZoom(m_Zoom);
            AlignCenterToFocus();
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
            else if (m_HasFocus && !m_IsGestureActive)
            {
                m_DesiredCenter = ClampToRange(m_Focus);
                m_HasCenter = true;
            }

            ClampCenter();
            ApplyToCamera();
        }

        private void AlignCenterToFocus()
        {
            if (m_HasFocus)
            {
                m_DesiredCenter = ClampToRange(m_Focus);
                m_HasCenter = true;
            }
        }

        private void ApplyInitialFocus()
        {
            if (!m_HasInitialFocus || m_HasFocus)
            {
                return;
            }

            // The initial focus is already in map plane coordinates, so it only needs the plane's
            // perpendicular axis filled in to become a local point.
            var local = m_AppliedLayout.Plane == HexPlane.XY
                ? new Vector3(m_InitialFocus.x, m_InitialFocus.y, m_AppliedLayout.Origin.z)
                : new Vector3(m_InitialFocus.x, m_AppliedLayout.Origin.y, m_InitialFocus.y);

            m_Focus = ToPlaneCoordinates(m_AppliedTransform.TransformPoint(local));
            m_HasFocus = true;
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

            var max = MaxZoom;
            return zoom < MinZoom ? MinZoom : (zoom > max ? max : zoom);
        }

        /// <summary>
        /// Guards a zoom written before the framing exists. The upper limit is unknown until then, so
        /// only the floor and non-finite values are handled here and the ceiling waits for the refresh.
        /// </summary>
        private static float NormalizeZoom(float zoom)
        {
            return IsFinitePositive(zoom) ? zoom : MinZoom;
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

        private bool TryGetLayout(out HexLayout layout, out string error)
        {
            layout = default(HexLayout);

            if (!m_HexMapView.HasMap)
            {
                error = "HexMapView must be built before the camera can frame it.";
                return false;
            }

            layout = m_HexMapView.Layout;

            if (m_HexMapView.Radius <= 0)
            {
                error = "HexMapView radius must be positive.";
                return false;
            }

            error = string.Empty;
            return true;
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

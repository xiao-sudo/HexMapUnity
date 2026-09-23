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
    /// <see cref="HexMapView"/> map, and pans it left and right along the map's local X axis.
    /// <para>
    /// The framing comes from <see cref="OrthographicMapFraming"/>, so every row of the map is always
    /// inside the frame and the visible width follows from the viewport aspect ratio. With a portrait
    /// viewport (the shipped target) only part of the map's width fits, which is what makes panning
    /// meaningful; with a landscape viewport the frame can swallow the whole map and the camera then
    /// locks to the center with a zero panning range. That lock is geometry, not a defect.
    /// </para>
    /// <para>
    /// Nothing here runs per frame. Call <see cref="TryRefresh"/> when the view or the viewport changes.
    /// <see cref="TrySetOffset"/> afterwards only re-applies the camera, so panning never re-derives the
    /// framing and never needs the renderer to have been built.
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
        [HideInInspector]
        private float m_Offset;

        private OrthographicMapFraming m_Framing;
        private HexLayout m_AppliedLayout;
        private Transform m_AppliedTransform;
        private float m_AppliedScale = 1f;
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

        public bool HasFraming
        {
            get { return m_HasFraming; }
        }

        /// <summary>
        /// The offset along the map's local +X axis. Writing it clamps into the movable range.
        /// </summary>
        public float Offset
        {
            get { return m_Offset; }
            set
            {
                if (!m_HasFraming)
                {
                    m_Offset = value;
                    return;
                }

                m_Offset = m_Framing.ClampOffset(value);
                ApplyToCamera();
            }
        }

        /// <summary>
        /// True when the frame is at least as wide as the map, so panning cannot move anything.
        /// </summary>
        public bool IsLockedToCenter
        {
            get { return !m_HasFraming || m_Framing.IsLockedToCenter; }
        }

        /// <summary>
        /// Copies the current framing snapshot. Returns false before the first successful refresh.
        /// </summary>
        public bool TryGetFraming(out OrthographicMapFraming framing)
        {
            framing = m_Framing;
            return m_HasFraming;
        }

        /// <summary>
        /// Recomputes the framing from the view, the camera and the viewport, then writes the camera.
        /// Reports what is missing instead of throwing.
        /// </summary>
        public bool TryRefresh(out string error)
        {
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

            OrthographicMapFraming framing;
            if (!OrthographicMapFraming.TryCreate(
                layout,
                m_HexMapView.Radius,
                m_ViewMargin,
                ResolveAspect(),
                out framing,
                out error))
            {
                return false;
            }

            m_Framing = framing;
            m_AppliedLayout = layout;
            m_AppliedTransform = m_HexMapView.transform;
            m_AppliedScale = scale;
            m_AppliedCullingMask = cullingMask;
            m_HasFraming = true;
            m_Offset = framing.ClampOffset(m_Offset);

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
                out framing,
                out error);
        }

        /// <summary>
        /// Moves the camera to the requested offset along the map's local +X axis, clamped into range.
        /// </summary>
        public bool TrySetOffset(float offset, out string error)
        {
            if (!m_HasFraming)
            {
                error = "Refresh the camera before setting an offset.";
                return false;
            }

            float clamped;
            if (!m_Framing.TrySetOffset(offset, out clamped, out error))
            {
                return false;
            }

            m_Offset = clamped;
            ApplyToCamera();
            error = string.Empty;
            return true;
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
                + right * (m_Offset * m_AppliedScale)
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

using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public sealed class HexMapView : MonoBehaviour
    {
        [SerializeField] private int m_Radius = 3;
        [SerializeField] private HexOrientation m_Orientation = HexOrientation.Pointy;
        [SerializeField] private HexPlane m_Plane = HexPlane.XZ;
        [SerializeField] private float m_OuterRadius = 1f;
        [SerializeField] private float m_SecondaryScale = 1f;
        [SerializeField] private Vector3 m_Origin;
        [SerializeField] private Material m_CellMaterial;
        [SerializeField] private int m_CellLayer;
        [SerializeField] private HexMapRenderStrategy m_RenderStrategy = HexMapRenderStrategy.MeshRenderer;

        [SerializeField]
        private Color m_BaseAppearanceColor;

#if UNITY_EDITOR
        [SerializeField] private HexMap.Gvg.Authoring.GvgMapAuthoringAsset m_GvgMapAuthoringAsset;
#endif

        private RuntimeHexMap m_Map;
        private HexLayout m_Layout;
        private HexMapRenderer m_Renderer;

        public int Radius
        {
            get { return m_Radius; }
            set { m_Radius = value; }
        }

        public HexOrientation Orientation
        {
            get { return m_Orientation; }
            set { m_Orientation = value; }
        }

        public HexPlane Plane
        {
            get { return m_Plane; }
            set { m_Plane = value; }
        }

        public float OuterRadius
        {
            get { return m_OuterRadius; }
            set { m_OuterRadius = value; }
        }

        public float SecondaryScale
        {
            get { return m_SecondaryScale; }
            set { m_SecondaryScale = value; }
        }

        public Vector3 Origin
        {
            get { return m_Origin; }
            set { m_Origin = value; }
        }

#if UNITY_EDITOR
        public HexMap.Gvg.Authoring.GvgMapAuthoringAsset GvgMapAuthoringAssetEditorOnly
        {
            get { return m_GvgMapAuthoringAsset; }
            set { m_GvgMapAuthoringAsset = value; }
        }
#endif

        public HexMapRenderStrategy RenderStrategy
        {
            get { return m_RenderStrategy; }
            set { m_RenderStrategy = value; }
        }

        public RuntimeHexMap Map
        {
            get { return m_Map; }
        }

        public bool HasMap
        {
            get { return m_Map != null; }
        }

        public HexLayout Layout
        {
            get { return m_Layout; }
        }

        /// <summary>
        /// Creates a validated map snapshot from this scene view's topology.
        /// The snapshot does not depend on the renderer having been built.
        /// </summary>
        public RuntimeHexMap CreateMapSnapshot()
        {
            return new RuntimeHexMap(new HexMapDefinition(m_Radius));
        }

        /// <summary>
        /// Creates a validated layout snapshot from this scene view's display configuration.
        /// </summary>
        public HexLayout CreateLayoutSnapshot()
        {
            return new HexLayout(
                m_Orientation,
                m_Plane,
                m_OuterRadius,
                m_Origin,
                m_SecondaryScale);
        }

        /// <summary>
        /// Creates the map and layout used by editor and runtime consumers without
        /// requiring a renderer or Unity lifecycle callback.
        /// </summary>
        public bool TryCreateSnapshots(
            out RuntimeHexMap map,
            out HexLayout layout,
            out string error)
        {
            try
            {
                map = CreateMapSnapshot();
                layout = CreateLayoutSnapshot();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                map = null;
                layout = default(HexLayout);
                error = exception.Message;
                return false;
            }
        }

        public void Build()
        {
            ValidateTransformScale();
            DisposeRenderer();

            m_Map = CreateMapSnapshot();
            m_Layout = CreateLayoutSnapshot();
            var renderConfig = new HexMapRenderConfig(
                transform,
                m_CellMaterial,
                m_CellLayer,
                m_BaseAppearanceColor,
                m_RenderStrategy);
            m_Renderer = new HexMapRenderer(m_Map, m_Layout, renderConfig);
        }

        public bool TryGetHexView(HexCoord coordinate, out HexView view)
        {
            view = null;
            if (m_Map == null || m_Renderer == null)
            {
                return false;
            }

            if (!m_Map.Query(coordinate).HasCell)
            {
                return false;
            }

            return m_Renderer.TryGetHexView(coordinate, out view);
        }

        public HexCoord WorldToHex(Vector3 worldPoint)
        {
            var localPoint = WorldToMapLocal(worldPoint);
            return m_Layout.WorldToHex(localPoint);
        }

        public bool TryGetHexViewAtWorldPoint(Vector3 worldPoint, out HexView view)
        {
            // This convenience query intentionally ignores the perpendicular coordinate;
            // use HexMapPicker when the point must lie on the active map plane.
            var coordinate = WorldToHex(worldPoint);
            return TryGetHexView(coordinate, out view);
        }

        internal Vector3 GetPlotWorldCenter(IReadOnlyList<HexCell> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (cells.Count == 0) throw new ArgumentException("A Plot must contain at least one Cell.", nameof(cells));
            ValidateTransformScale();
            var localCenter = Vector3.zero;
            for (var index = 0; index < cells.Count; index++) localCenter += m_Layout.HexToWorld(cells[index].Coordinate);
            return transform.TransformPoint(localCenter / cells.Count);
        }

        /// <summary>
        /// Converts a world point into the map view's local space. This only applies
        /// scene Transform geometry; it does not check the map plane or map bounds.
        /// </summary>
        public Vector3 WorldToMapLocal(Vector3 worldPoint)
        {
            ValidateTransformScale();
            ValidateFinite(worldPoint, nameof(worldPoint));
            return transform.InverseTransformPoint(worldPoint);
        }

        /// <summary>
        /// Gets the world-space plane containing Layout.Origin.
        /// </summary>
        public Plane WorldPlane
        {
            get
            {
                ValidateTransformScale();

                var layout = CreateLayoutSnapshot();
                var localNormal = layout.Plane == HexPlane.XY
                    ? Vector3.forward
                    : Vector3.up;
                var worldPoint = transform.TransformPoint(layout.Origin);
                var worldNormal = transform.TransformDirection(localNormal).normalized;
                return new Plane(worldNormal, worldPoint);
            }
        }

        private void ValidateTransformScale()
        {
            var scale = transform.lossyScale;
            var x = Mathf.Abs(scale.x);
            var y = Mathf.Abs(scale.y);
            var z = Mathf.Abs(scale.z);
            if (float.IsNaN(x) || float.IsInfinity(x) ||
                float.IsNaN(y) || float.IsInfinity(y) ||
                float.IsNaN(z) || float.IsInfinity(z) ||
                x <= Mathf.Epsilon || y <= Mathf.Epsilon || z <= Mathf.Epsilon ||
                Mathf.Abs(x - y) > 0.0001f || Mathf.Abs(x - z) > 0.0001f)
            {
                throw new InvalidOperationException("HexMapView requires a uniform, non-zero transform scale.");
            }
        }

        private static void ValidateFinite(Vector3 value, string parameterName)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y) ||
                float.IsNaN(value.z) || float.IsInfinity(value.z))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "World point must be finite.");
            }
        }

        private void DisposeRenderer()
        {
            if (m_Renderer == null)
            {
                return;
            }

            m_Renderer.Dispose();
            m_Renderer = null;
        }

        private void OnDestroy()
        {
            DisposeRenderer();
        }
    }
}

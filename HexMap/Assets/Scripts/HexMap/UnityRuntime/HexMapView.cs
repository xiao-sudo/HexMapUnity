using System;
using HexMap.Core;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public sealed class HexMapView : MonoBehaviour
    {
        [SerializeField] private HexMapConfigAsset m_Config;
        [SerializeField] private HexOrientation m_Orientation = HexOrientation.Pointy;
        [SerializeField] private HexPlane m_Plane = HexPlane.XZ;
        [SerializeField] private float m_OuterRadius = 1f;
        [SerializeField] private Vector3 m_Origin;
        [SerializeField] private Material m_CellMaterial;
        [SerializeField] private int m_CellLayer;
#if UNITY_EDITOR
        [SerializeField] private bool m_ShowDebugLabels = true;
        [SerializeField] private bool m_ShowDebugCoordinates;
        [SerializeField] private bool m_ShowDebugBounds = true;
#endif

        private RuntimeHexMap m_Map;
        private HexLayout m_Layout;
        private HexMapRenderer m_Renderer;

        public HexMapConfigAsset Config
        {
            get { return m_Config; }
            set { m_Config = value; }
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

        public Vector3 Origin
        {
            get { return m_Origin; }
            set { m_Origin = value; }
        }

#if UNITY_EDITOR
        public bool ShowDebugLabels
        {
            get { return m_ShowDebugLabels; }
            set { m_ShowDebugLabels = value; }
        }

        public bool ShowDebugCoordinates
        {
            get { return m_ShowDebugCoordinates; }
            set { m_ShowDebugCoordinates = value; }
        }

        public bool ShowDebugBounds
        {
            get { return m_ShowDebugBounds; }
            set { m_ShowDebugBounds = value; }
        }
#endif

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

        public void Awake()
        {
            Build();
        }

        public void Build()
        {
            ValidateTransformScale();
            DisposeRenderer();

            var definition = m_Config == null
                ? new HexMapDefinition(3, new HexCoord[0])
                : m_Config.CreateDefinition();

            m_Map = new RuntimeHexMap(definition);
            m_Layout = new HexLayout(m_Orientation, m_Plane, m_OuterRadius, m_Origin);
            var renderConfig = new HexMapRenderConfig(transform, m_CellMaterial, m_CellLayer);
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

                var localNormal = m_Layout.Plane == HexPlane.XY
                    ? Vector3.forward
                    : Vector3.up;
                var worldPoint = transform.TransformPoint(m_Layout.Origin);
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

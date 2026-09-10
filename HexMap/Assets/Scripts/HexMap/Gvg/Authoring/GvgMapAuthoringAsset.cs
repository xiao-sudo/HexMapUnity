using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using UnityEngine;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.Gvg.Authoring
{
    [CreateAssetMenu(fileName = "GvgMapAuthoring", menuName = "Hex Map/GVG Map Authoring")]
    public sealed class GvgMapAuthoringAsset : ScriptableObject
    {
        [SerializeField] private string m_MapId = string.Empty;
        [SerializeField] private int m_Radius = 1;
        [SerializeField] private HexOrientation m_Orientation = HexOrientation.Pointy;
        [SerializeField] private HexPlane m_Plane = HexPlane.XZ;
        [SerializeField] private float m_OuterRadius = 1f;
        [SerializeField] private List<GvgPlotAuthoringData> m_Plots = new List<GvgPlotAuthoringData>();

        public string MapId
        {
            get { return string.IsNullOrEmpty(m_MapId) ? name : m_MapId; }
            set { m_MapId = value ?? string.Empty; }
        }

        public int Radius
        {
            get { return m_Radius; }
            set { m_Radius = new HexMapRadius(value).Radius; }
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
            set
            {
                if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Outer radius must be positive and finite.");
                m_OuterRadius = value;
            }
        }

        public IReadOnlyList<GvgPlotAuthoringData> Plots
        {
            get { return m_Plots; }
        }

        public RuntimeHexMap CreateRuntimeMap()
        {
            return new RuntimeHexMap(new HexMapDefinition(m_Radius));
        }

        public void ReplacePlots(IEnumerable<GvgPlotAuthoringData> plots)
        {
            if (plots == null) throw new ArgumentNullException(nameof(plots));
            m_Plots = new List<GvgPlotAuthoringData>();
            foreach (var plot in plots)
            {
                if (plot == null) throw new ArgumentException("Plot list cannot contain null entries.", nameof(plots));
                m_Plots.Add(plot.Clone());
            }
        }

        internal List<GvgPlotAuthoringData> MutablePlots
        {
            get { return m_Plots; }
        }

        private void OnValidate()
        {
            if (m_Radius < 0) m_Radius = 0;
            if (m_Radius > HexMapRadius.MaxSupportedRadius) m_Radius = HexMapRadius.MaxSupportedRadius;
            if (m_OuterRadius <= 0f || float.IsNaN(m_OuterRadius) || float.IsInfinity(m_OuterRadius))
            {
                m_OuterRadius = 1f;
            }

            if (m_Plots == null)
            {
                m_Plots = new List<GvgPlotAuthoringData>();
            }
        }
    }
}
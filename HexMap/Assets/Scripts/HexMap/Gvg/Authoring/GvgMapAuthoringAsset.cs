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

        [SerializeField] private List<GvgPlotAuthoringData> m_Plots = new List<GvgPlotAuthoringData>();

        public string MapId
        {
            get { return string.IsNullOrEmpty(m_MapId) ? name : m_MapId; }
            set { m_MapId = value ?? string.Empty; }
        }


        public IReadOnlyList<GvgPlotAuthoringData> Plots
        {
            get { return m_Plots; }
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

            if (m_Plots == null)
            {
                m_Plots = new List<GvgPlotAuthoringData>();
            }
        }
    }
}
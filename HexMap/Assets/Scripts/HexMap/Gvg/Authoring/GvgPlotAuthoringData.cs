using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexMap.Gvg.Authoring
{
    [Serializable]
    public sealed class GvgPlotAuthoringData
    {
        [SerializeField] private int m_PlotId;
        [SerializeField] private List<int> m_HexIds = new List<int>();
        [SerializeField] private PlotType m_PlotType = PlotType.Normal;
        [SerializeField] private PlotGenerationType m_GenerationType = PlotGenerationType.Initial;

        public GvgPlotAuthoringData() { }

        public GvgPlotAuthoringData(int plotId, IEnumerable<int> hexIds, PlotType plotType)
            : this(plotId, hexIds, plotType, PlotGenerationType.Initial)
        {
        }

        public GvgPlotAuthoringData(
            int plotId,
            IEnumerable<int> hexIds,
            PlotType plotType,
            PlotGenerationType generationType)
        {
            if (hexIds == null) throw new ArgumentNullException(nameof(hexIds));
            m_PlotId = plotId;
            m_HexIds = new List<int>(hexIds);
            m_PlotType = plotType;
            m_GenerationType = generationType;
        }

        public int PlotId
        {
            get { return m_PlotId; }
            set { m_PlotId = value; }
        }

        public List<int> HexIds
        {
            get { return m_HexIds; }
        }

        public PlotType PlotType
        {
            get { return m_PlotType; }
            set { m_PlotType = value; }
        }

        public PlotGenerationType GenerationType
        {
            get { return m_GenerationType; }
            set { m_GenerationType = value; }
        }

        public GvgPlotAuthoringData Clone()
        {
            return new GvgPlotAuthoringData(m_PlotId, m_HexIds, m_PlotType, m_GenerationType);
        }
    }
}
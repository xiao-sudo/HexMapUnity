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
        [SerializeField] private int m_Start;
        [SerializeField] private int m_End = -1;

        public GvgPlotAuthoringData() { }

        public GvgPlotAuthoringData(int plotId, IEnumerable<int> hexIds, PlotType plotType)
            : this(plotId, hexIds, plotType, 0, -1)
        {
        }

        public GvgPlotAuthoringData(
            int plotId,
            IEnumerable<int> hexIds,
            PlotType plotType,
            int start,
            int end)
        {
            if (hexIds == null) throw new ArgumentNullException(nameof(hexIds));
            m_PlotId = plotId;
            m_HexIds = new List<int>(hexIds);
            m_PlotType = plotType;
            m_Start = start;
            m_End = end;
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

        public int Start
        {
            get { return m_Start; }
            set { m_Start = value; }
        }

        public int End
        {
            get { return m_End; }
            set { m_End = value; }
        }

        public bool IsMultiCell
        {
            get { return m_HexIds != null && m_HexIds.Count > 1; }
        }

        public GvgPlotAuthoringData Clone()
        {
            return new GvgPlotAuthoringData(m_PlotId, m_HexIds, m_PlotType, m_Start, m_End);
        }
    }
}
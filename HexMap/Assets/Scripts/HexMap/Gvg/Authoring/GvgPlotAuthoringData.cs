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
        [SerializeField] private int m_AffiliatedCampId = Plot.NoAffiliatedCampId;
        [SerializeField] private int m_OwnerFactionId = Plot.NoFactionId;

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
            : this(plotId, hexIds, plotType, start, end, Plot.NoAffiliatedCampId)
        {
        }

        public GvgPlotAuthoringData(
            int plotId,
            IEnumerable<int> hexIds,
            PlotType plotType,
            int start,
            int end,
            int affiliatedCampId,
            int ownerFactionId = Plot.NoFactionId)
        {
            if (hexIds == null) throw new ArgumentNullException(nameof(hexIds));
            if (affiliatedCampId < Plot.NoAffiliatedCampId)
                throw new ArgumentOutOfRangeException(nameof(affiliatedCampId));
            if (ownerFactionId < Plot.NoFactionId)
                throw new ArgumentOutOfRangeException(nameof(ownerFactionId));
            m_PlotId = plotId;
            m_HexIds = new List<int>(hexIds);
            m_PlotType = plotType;
            m_Start = start;
            m_End = end;
            m_AffiliatedCampId = affiliatedCampId;
            m_OwnerFactionId = ownerFactionId;
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

        public int AffiliatedCampId
        {
            get { return m_AffiliatedCampId; }
            set
            {
                if (value < Plot.NoAffiliatedCampId)
                    throw new ArgumentOutOfRangeException(nameof(value));
                m_AffiliatedCampId = value;
            }
        }

        public int OwnerFactionId
        {
            get { return m_OwnerFactionId; }
            set
            {
                if (value < Plot.NoFactionId)
                    throw new ArgumentOutOfRangeException(nameof(value));
                m_OwnerFactionId = value;
            }
        }

        public bool IsMultiCell
        {
            get { return m_HexIds != null && m_HexIds.Count > 1; }
        }

        public GvgPlotAuthoringData Clone()
        {
            return new GvgPlotAuthoringData(
                m_PlotId,
                m_HexIds,
                m_PlotType,
                m_Start,
                m_End,
                m_AffiliatedCampId,
                m_OwnerFactionId);
        }
    }
}

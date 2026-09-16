using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HexMap.Gvg
{
    /// <summary>
    /// Runtime representation of one logical Plot row, mirroring the exported logical
    /// table columns (ID, Coordinates, Type, GridType, Safe, Start, End).
    /// Instances are constructed by data loaders; the runtime composition pipeline
    /// only consumes them. This DTO is not Unity-serialized.
    /// </summary>
    public sealed class GvgPlotRuntimeData
    {
        private readonly int m_PlotId;
        private readonly IReadOnlyList<int> m_HexIds;
        private readonly PlotType m_PlotType;
        private readonly int m_GenerationType;
        private readonly int m_Start;
        private readonly int m_End;
        private readonly int m_AffiliatedCampId;
        private readonly int m_OwnerFactionId;

        public GvgPlotRuntimeData(
            int plotId,
            IEnumerable<int> hexIds,
            PlotType plotType,
            int generationType,
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
            m_HexIds = new ReadOnlyCollection<int>(new List<int>(hexIds));
            m_PlotType = plotType;
            m_GenerationType = generationType;
            m_Start = start;
            m_End = end;
            m_AffiliatedCampId = affiliatedCampId;
            m_OwnerFactionId = ownerFactionId;
        }

        public GvgPlotRuntimeData(int plotId, IEnumerable<int> hexIds, PlotType plotType)
            : this(plotId, hexIds, plotType, 0, 0, -1, Plot.NoAffiliatedCampId)
        {
        }

        public GvgPlotRuntimeData(
            int plotId,
            IEnumerable<int> hexIds,
            PlotType plotType,
            int start,
            int end,
            int affiliatedCampId)
            : this(plotId, hexIds, plotType, start == 0 ? 0 : 1, start, end, affiliatedCampId)
        {
        }

        public int PlotId
        {
            get { return m_PlotId; }
        }

        public IReadOnlyList<int> HexIds
        {
            get { return m_HexIds; }
        }

        public PlotType PlotType
        {
            get { return m_PlotType; }
        }

        public int GenerationType
        {
            get { return m_GenerationType; }
        }

        public int Start
        {
            get { return m_Start; }
        }

        public int End
        {
            get { return m_End; }
        }

        public int AffiliatedCampId
        {
            get { return m_AffiliatedCampId; }
        }

        public int OwnerFactionId
        {
            get { return m_OwnerFactionId; }
        }
    }
}

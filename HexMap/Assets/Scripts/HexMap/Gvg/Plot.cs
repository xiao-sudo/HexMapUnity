using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HexMap.Runtime;

namespace HexMap.Gvg
{
    public enum PlotType
    {
        Camp = 1,
        Normal = 2,
        Grass = 3,
        SmallCity = 4,
        BigCity = 5,
        Capital = 6,
        Obstacle = 7
    }

    public enum PlotState
    {
        NotOpen = 0,
        Open = 1
    }


    public enum BlockingState
    {
        Passable = 0,
        Blocked = 1
    }

    public enum FactionId
    {
        Neutral = 0,
        Red = 1,
        Blue = 2
    }

    public sealed class Plot
    {
        public const int NoAffiliatedCampId = -1;

        private readonly int m_PlotId;
        private readonly IReadOnlyList<HexCell> m_Cells;
        private readonly PlotType m_PlotType;
        private PlotState m_PlotState;
        private readonly FactionId m_OwnerFaction;
        private readonly BlockingState m_BlockingState;
        private readonly int m_AffiliatedCampId;

        public Plot(
            int plotId,
            IReadOnlyList<HexCell> cells,
            PlotType plotType,
            PlotState plotState,
            FactionId ownerFaction,
            BlockingState blockingState,
            int affiliatedCampId = NoAffiliatedCampId)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (cells.Count == 0) throw new ArgumentException("A Plot must contain at least one cell.", nameof(cells));
            if (!Enum.IsDefined(typeof(PlotType), plotType))
                throw new ArgumentOutOfRangeException(nameof(plotType), plotType, "PlotType is not defined.");
            if (!Enum.IsDefined(typeof(PlotState), plotState))
                throw new ArgumentOutOfRangeException(nameof(plotState), plotState, "PlotState is not defined.");
            if (affiliatedCampId < NoAffiliatedCampId)
                throw new ArgumentOutOfRangeException(nameof(affiliatedCampId), affiliatedCampId, "Affiliated CampId must be -1 or non-negative.");

            var copiedCells = new List<HexCell>(cells.Count);
            var cellIds = new HashSet<int>();
            var coordinates = new HashSet<HexMap.Core.HexCoord>();

            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                if (!cellIds.Add(cell.Id) || !coordinates.Add(cell.Coordinate))
                {
                    throw new ArgumentException("A Plot cannot contain duplicate cells.", nameof(cells));
                }

                copiedCells.Add(cell);
            }

            if (plotType == PlotType.Obstacle && blockingState != BlockingState.Blocked)
            {
                throw new ArgumentException("Obstacle Plots must be blocked.", nameof(blockingState));
            }

            m_PlotId = plotId;
            m_Cells = new ReadOnlyCollection<HexCell>(copiedCells);
            m_PlotType = plotType;
            m_PlotState = plotState;
            m_OwnerFaction = ownerFaction;
            m_BlockingState = blockingState;
            m_AffiliatedCampId = affiliatedCampId;
        }

        public int PlotId { get { return m_PlotId; } }
        public IReadOnlyList<HexCell> Cells { get { return m_Cells; } }
        public PlotType PlotType { get { return m_PlotType; } }
        public PlotState PlotState { get { return m_PlotState; } }
        public FactionId OwnerFaction { get { return m_OwnerFaction; } }
        public BlockingState BlockingState { get { return m_BlockingState; } }
        public int AffiliatedCampId { get { return m_AffiliatedCampId; } }

        public bool IsOpenForPathfinding
        {
            get { return m_PlotState == PlotState.Open; }
        }

        public bool Open()
        {
            if (m_PlotState == PlotState.Open) return false;
            m_PlotState = PlotState.Open;
            return true;
        }

        public bool Close()
        {
            if (m_PlotState == PlotState.NotOpen) return false;
            m_PlotState = PlotState.NotOpen;
            return true;
        }
    }
}
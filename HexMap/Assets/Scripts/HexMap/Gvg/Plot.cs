using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HexMap.Runtime;

namespace HexMap.Gvg
{
    public enum PlotType
    {
        Camp = 0,
        Normal = 1,
        Grass = 2,
        SmallCity = 3,
        BigCity = 4,
        Capital = 5,
        Obstacle = 6
    }

    public enum PlotState
    {
        NotGenerated = 0,
        NotOpened = 1,
        Open = 2,
        Battle = 3
    }

    public enum OwnershipMode
    {
        Capturable = 0,
        Fixed = 1
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
        private readonly int m_PlotId;
        private readonly IReadOnlyList<HexCell> m_Cells;
        private readonly PlotType m_PlotType;
        private readonly PlotState m_PlotState;
        private readonly FactionId m_OwnerFaction;
        private readonly OwnershipMode m_OwnershipMode;
        private readonly BlockingState m_BlockingState;

        public Plot(
            int plotId,
            IReadOnlyList<HexCell> cells,
            PlotType plotType,
            PlotState plotState,
            FactionId ownerFaction,
            OwnershipMode ownershipMode,
            BlockingState blockingState)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (cells.Count == 0) throw new ArgumentException("A Plot must contain at least one cell.", nameof(cells));

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
            m_OwnershipMode = ownershipMode;
            m_BlockingState = blockingState;
        }

        public int PlotId { get { return m_PlotId; } }
        public IReadOnlyList<HexCell> Cells { get { return m_Cells; } }
        public PlotType PlotType { get { return m_PlotType; } }
        public PlotState PlotState { get { return m_PlotState; } }
        public FactionId OwnerFaction { get { return m_OwnerFaction; } }
        public OwnershipMode OwnershipMode { get { return m_OwnershipMode; } }
        public BlockingState BlockingState { get { return m_BlockingState; } }

        public bool IsOpenForPathfinding
        {
            get { return m_PlotState == PlotState.Open || m_PlotState == PlotState.Battle; }
        }
    }
}

using System;
using System.Collections.Generic;
using HexMap.Runtime;

namespace HexMap.Gvg
{
    public sealed class PlotPathService
    {
        private readonly PlotRegistry m_Registry;
        private readonly HexPathfinder m_Pathfinder;
        private readonly ReusablePathRequest m_Request;
        private readonly List<HexCell> m_StartCells;
        private readonly List<HexCell> m_TargetCells;

        public PlotPathService(PlotRegistry registry)
        {
            m_Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            var map = registry.Map;
            m_Pathfinder = new HexPathfinder(map, new PathSearchWorkspace(map));
            m_StartCells = new List<HexCell>(map.Count);
            m_TargetCells = new List<HexCell>(map.Count);
            m_Request = new ReusablePathRequest(
                m_StartCells,
                m_TargetCells,
                new PlotPathPolicy(registry, FactionId.Neutral));
        }

        public PlotRegistry Registry { get { return m_Registry; } }

        public PathResult FindPath(
            int startPlotId,
            int targetPlotId,
            FactionId movingFaction,
            PathResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var startPlot = m_Registry.GetPlot(startPlotId);
            var targetPlot = m_Registry.GetPlot(targetPlotId);

            m_Request.ClearStarts();
            m_Request.ClearTargets();
            for (var index = 0; index < startPlot.Cells.Count; index++)
            {
                if (!m_Request.TryAddStart(startPlot.Cells[index]))
                {
                    throw new InvalidOperationException("The start cell buffer capacity is insufficient.");
                }
            }

            for (var index = 0; index < targetPlot.Cells.Count; index++)
            {
                if (!m_Request.TryAddTarget(targetPlot.Cells[index]))
                {
                    throw new InvalidOperationException("The target cell buffer capacity is insufficient.");
                }
            }

            m_Request.Policy = new PlotPathPolicy(m_Registry, movingFaction);
            return m_Pathfinder.FindPath(m_Request, result);
        }
    }
}

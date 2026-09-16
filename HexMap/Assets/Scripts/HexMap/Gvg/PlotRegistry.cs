using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.Gvg
{
    public sealed class PlotRegistry
    {
        private readonly RuntimeHexMap m_Map;
        private readonly Dictionary<int, Plot> m_PlotsById;
        // Reverse index from a Cell to every Plot that contains it. A Plot may
        // cover multiple Cells, and the same Cell may appear in multiple Plot
        // records when the runtime data contains temporal/layered Plot entries.
        // Pathfinding still requires at most one open Plot for a Cell; see
        // TryGetOpenPlot.
        private readonly Dictionary<int, List<Plot>> m_PlotsByCellId;
        private IReadOnlyList<Plot> m_Plots;

        public PlotRegistry(RuntimeHexMap map, IReadOnlyList<Plot> plots)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (plots == null) throw new ArgumentNullException(nameof(plots));

            m_Map = map;
            m_PlotsById = new Dictionary<int, Plot>(plots.Count);
            m_PlotsByCellId = new Dictionary<int, List<Plot>>(map.Count);
            var copiedPlots = new List<Plot>(plots.Count);
            for (var index = 0; index < plots.Count; index++)
            {
                RegisterCore(plots[index], copiedPlots);
            }

            m_Plots = new ReadOnlyCollection<Plot>(copiedPlots);
        }

        public RuntimeHexMap Map { get { return m_Map; } }
        public int Count { get { return m_Plots.Count; } }
        public IReadOnlyList<Plot> Plots { get { return m_Plots; } }

        public bool TryGetPlot(int plotId, out Plot plot)
        {
            return m_PlotsById.TryGetValue(plotId, out plot);
        }

        public Plot GetPlot(int plotId)
        {
            Plot plot;
            if (!TryGetPlot(plotId, out plot))
                throw new KeyNotFoundException("The Plot ID is not registered: " + plotId);
            return plot;
        }

        public bool TryGetPlot(HexCell cell, out Plot plot)
        {
            HexCell mapCell;
            if (!m_Map.TryGetCell(cell.Coordinate, out mapCell) || mapCell.Id != cell.Id)
            {
                plot = null;
                return false;
            }

            return TryGetOpenPlot(cell.Id, out plot);
        }

        public bool TryGetPlotForCell(int cellId, out Plot plot)
        {
            return TryGetOpenPlot(cellId, out plot);
        }

        public Plot GetPlotForCell(int cellId)
        {
            Plot plot;
            if (!TryGetPlotForCell(cellId, out plot))
                throw new KeyNotFoundException("The cell has no unique open Plot: " + cellId);
            return plot;
        }

        public Plot GetPlot(HexCell cell)
        {
            Plot plot;
            if (!TryGetPlot(cell, out plot))
                throw new KeyNotFoundException("The cell has no unique open Plot: " + cell.Id);
            return plot;
        }

        public bool TryGetPlotsForCell(int cellId, out IReadOnlyList<Plot> plots)
        {
            List<Plot> cellPlots;
            if (!m_PlotsByCellId.TryGetValue(cellId, out cellPlots))
            {
                plots = null;
                return false;
            }

            plots = cellPlots;
            return true;
        }

        private bool TryGetOpenPlot(int cellId, out Plot plot)
        {
            List<Plot> cellPlots;
            if (!m_PlotsByCellId.TryGetValue(cellId, out cellPlots))
            {
                plot = null;
                return false;
            }

            plot = null;
            for (var index = 0; index < cellPlots.Count; index++)
            {
                if (!cellPlots[index].IsOpenForPathfinding) continue;

                // Multiple Plot records for a Cell are valid for temporal or
                // layered data, but there must be exactly one open Plot for
                // the Cell to have an unambiguous pathfinding meaning.
                if (plot != null)
                {
                    plot = null;
                    return false;
                }

                plot = cellPlots[index];
            }

            return plot != null;
        }

        private void RegisterCore(Plot plot, List<Plot> plots)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (m_PlotsById.ContainsKey(plot.PlotId))
                throw new ArgumentException("The Plot ID is already registered: " + plot.PlotId, nameof(plot));

            for (var cellIndex = 0; cellIndex < plot.Cells.Count; cellIndex++)
            {
                var cell = plot.Cells[cellIndex];
                HexCell mapCell;
                if (!m_Map.TryGetCell(cell.Coordinate, out mapCell) || mapCell.Id != cell.Id)
                    throw new ArgumentException("A Plot contains a cell outside this map.", nameof(plot));
            }

            m_PlotsById.Add(plot.PlotId, plot);
            for (var cellIndex = 0; cellIndex < plot.Cells.Count; cellIndex++)
            {
                var cellId = plot.Cells[cellIndex].Id;
                List<Plot> cellPlots;
                if (!m_PlotsByCellId.TryGetValue(cellId, out cellPlots))
                {
                    cellPlots = new List<Plot>();
                    m_PlotsByCellId.Add(cellId, cellPlots);
                }

                cellPlots.Add(plot);
            }

            plots.Add(plot);
        }
    }
}
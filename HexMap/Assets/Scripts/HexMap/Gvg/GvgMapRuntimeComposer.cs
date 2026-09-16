using System;
using System.Collections.Generic;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.Gvg
{
    /// <summary>
    /// Builds the runtime Plot graph from a logical plot table.
    /// Pure and stateless: it only consumes GvgPlotRuntimeData rows and a map, and is
    /// agnostic to where the rows came from (mock adapter today, real table loader later).
    /// </summary>
    public static class GvgMapRuntimeComposer
    {
        public static bool TryCompose(
            RuntimeHexMap map,
            IReadOnlyList<GvgPlotRuntimeData> rows,
            out PlotRegistry registry,
            out string error)
        {
            registry = null;
            error = string.Empty;

            if (map == null)
            {
                error = "Cannot compose plots for a null map.";
                return false;
            }

            if (rows == null)
            {
                error = "Cannot compose plots from a null row list.";
                return false;
            }

            var plots = new List<Plot>(rows.Count);
            var plotIds = new HashSet<int>(rows.Count);
            var openRowsByCellId = new Dictionary<int, GvgPlotRuntimeData>(map.Count);

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row == null)
                {
                    error = "The plot row list contains a null entry at index " + index + ".";
                    return false;
                }

                if (!plotIds.Add(row.PlotId))
                {
                    error = "Duplicate PlotId " + row.PlotId + " in the runtime plot table.";
                    return false;
                }

                if (row.HexIds == null || row.HexIds.Count == 0)
                {
                    error = "Plot " + row.PlotId + " has no HexIds.";
                    return false;
                }

                var cells = new List<HexCell>(row.HexIds.Count);
                var cellIds = new HashSet<int>(row.HexIds.Count);
                for (var hexIndex = 0; hexIndex < row.HexIds.Count; hexIndex++)
                {
                    var hexId = row.HexIds[hexIndex];
                    if (!cellIds.Add(hexId))
                    {
                        error = "Plot " + row.PlotId + " contains duplicate HexId " + hexId + ".";
                        return false;
                    }

                    var query = map.Query(hexId);
                    if (!query.HasCell)
                    {
                        error = "Plot " + row.PlotId + " references HexId " + hexId +
                            " which is outside the map topology (Radius " + map.Radius.Radius + ").";
                        return false;
                    }

                    cells.Add(query.Cell);
                }

                if (row.Start == 0)
                {
                    for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                    {
                        var cellId = cells[cellIndex].Id;
                        GvgPlotRuntimeData existingOpenRow;
                        if (openRowsByCellId.TryGetValue(cellId, out existingOpenRow))
                        {
                            error = "HexId " + cellId + " has more than one open plot row (Start == 0): Plot " +
                                existingOpenRow.PlotId + " and Plot " + row.PlotId + ".";
                            return false;
                        }

                        openRowsByCellId.Add(cellId, row);
                    }
                }

                try
                {
                    plots.Add(CreatePlot(row, cells));
                }
                catch (Exception exception)
                {
                    error = "Plot " + row.PlotId + " is invalid: " + exception.Message;
                    return false;
                }
            }

            try
            {
                registry = new PlotRegistry(map, plots);
            }
            catch (Exception exception)
            {
                registry = null;
                error = "Cannot build the PlotRegistry: " + exception.Message;
                return false;
            }

            return true;
        }

        private static Plot CreatePlot(GvgPlotRuntimeData row, IReadOnlyList<HexCell> cells)
        {
            var blockingState = row.PlotType == PlotType.Obstacle
                ? BlockingState.Blocked
                : BlockingState.Passable;
            var plotState = row.Start == 0
                ? PlotState.Open
                : PlotState.NotOpen;
            return new Plot(
                row.PlotId,
                cells,
                row.PlotType,
                plotState,
                blockingState,
                row.OwnerFactionId,
                row.AffiliatedCampId);
        }
    }
}

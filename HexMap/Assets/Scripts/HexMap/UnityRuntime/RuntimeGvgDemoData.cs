using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Gvg;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Mock runtime plot-table data source used by the editor runtime test environment
    /// (issue 05). Generates a deterministic plot table for an arbitrary map radius:
    /// one open Normal plot per cell, plus a small obstacle arc, one NotOpen cell and one
    /// multi-cell plot so the runtime composer and pathfinding exercise real cases.
    /// This is the temporary simulated data source; replace it with the real runtime
    /// table loader when that data source lands (the permanent seam stays
    /// GvgPlotRuntimeData -> GvgMapRuntimeComposer -> PlotRegistry).
    /// </summary>
    public static class RuntimeGvgDemoData
    {
        /// <summary>PlotId offset for generated multi-cell plots so they never collide with cell ids.</summary>
        public const int MultiCellPlotIdBase = 9000000;

        private static readonly HexCoord[] ObstacleArc =
        {
            new HexCoord(1, 0),
            new HexCoord(0, 1),
            new HexCoord(-1, 1),
            new HexCoord(-1, 0),
            new HexCoord(0, -1)
        };

        private static readonly HexCoord NotOpenCell = new HexCoord(2, -2);
        private static readonly HexCoord[] MultiCellCoordinates =
        {
            new HexCoord(0, 2),
            new HexCoord(1, 1)
        };

        public static List<GvgPlotRuntimeData> CreateDefaultTable(RuntimeHexMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var specials = new Dictionary<HexCoord, GvgPlotRuntimeData>();
            AddObstacleArc(map, specials);
            AddNotOpenCell(map, specials);
            AddMultiCellPlot(map, specials);

            var rows = new List<GvgPlotRuntimeData>(map.Count);
            var emittedPlotIds = new HashSet<int>();
            foreach (var cell in map.Cells)
            {
                GvgPlotRuntimeData specialRow;
                if (specials.TryGetValue(cell.Coordinate, out specialRow))
                {
                    if (emittedPlotIds.Add(specialRow.PlotId))
                    {
                        rows.Add(specialRow);
                    }

                    continue;
                }

                rows.Add(new GvgPlotRuntimeData(cell.Id, new[] { cell.Id }, PlotType.Normal));
            }

            return rows;
        }

        private static void AddObstacleArc(
            RuntimeHexMap map,
            Dictionary<HexCoord, GvgPlotRuntimeData> specials)
        {
            for (var index = 0; index < ObstacleArc.Length; index++)
            {
                AddSingleCellSpecial(
                    map,
                    specials,
                    ObstacleArc[index],
                    PlotType.Obstacle,
                    0,
                    -1);
            }
        }

        private static void AddNotOpenCell(
            RuntimeHexMap map,
            Dictionary<HexCoord, GvgPlotRuntimeData> specials)
        {
            // A NotOpen plot (Start != 0) stays closed for pathfinding until it opens later.
            AddSingleCellSpecial(
                map,
                specials,
                NotOpenCell,
                PlotType.Normal,
                1,
                1);
        }

        private static void AddMultiCellPlot(
            RuntimeHexMap map,
            Dictionary<HexCoord, GvgPlotRuntimeData> specials)
        {
            var firstQuery = map.Query(MultiCellCoordinates[0]);
            var secondQuery = map.Query(MultiCellCoordinates[1]);
            if (!firstQuery.HasCell || !secondQuery.HasCell)
            {
                return;
            }

            var firstId = firstQuery.Cell.Id;
            var secondId = secondQuery.Cell.Id;
            var plot = new GvgPlotRuntimeData(
                MultiCellPlotIdBase + 1,
                new[] { firstId, secondId },
                PlotType.Normal);
            specials.Add(MultiCellCoordinates[0], plot);
            specials.Add(MultiCellCoordinates[1], plot);
        }

        private static void AddSingleCellSpecial(
            RuntimeHexMap map,
            Dictionary<HexCoord, GvgPlotRuntimeData> specials,
            HexCoord coordinate,
            PlotType plotType,
            int start,
            int end)
        {
            var query = map.Query(coordinate);
            if (!query.HasCell)
            {
                return;
            }

            var cellId = query.Cell.Id;
            specials.Add(
                coordinate,
                new GvgPlotRuntimeData(
                    cellId,
                    new[] { cellId },
                    plotType,
                    start,
                    end,
                    Plot.NoAffiliatedCampId));
        }
    }
}

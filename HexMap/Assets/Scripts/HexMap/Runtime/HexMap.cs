using System;
using System.Collections.Generic;
using HexMap.Core;

namespace HexMap.Runtime
{
    public sealed class HexMap
    {
        private readonly HexMapBounds bounds;
        private readonly Dictionary<HexCoord, HexCell> cellsByCoordinate;
        private readonly IReadOnlyList<HexCell> cells;

        public HexMap(HexMapDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            bounds = definition.Bounds;
            cellsByCoordinate = new Dictionary<HexCoord, HexCell>();
            var generatedCells = new List<HexCell>();

            for (var q = (long)bounds.MinQ; q <= bounds.MaxQ; q++)
            {
                for (var r = (long)bounds.MinR; r <= bounds.MaxR; r++)
                {
                    var coordinate = new HexCoord((int)q, (int)r);
                    if (definition.IsExcluded(coordinate))
                    {
                        continue;
                    }

                    var cell = new HexCell(coordinate);
                    cellsByCoordinate.Add(coordinate, cell);
                    generatedCells.Add(cell);
                }
            }

            cells = generatedCells.AsReadOnly();
        }

        public HexMapBounds Bounds
        {
            get { return bounds; }
        }

        public int Count
        {
            get { return cells.Count; }
        }

        public IReadOnlyList<HexCell> Cells
        {
            get { return cells; }
        }

        public HexCellQuery Query(HexCoord coordinate)
        {
            if (!bounds.Contains(coordinate))
            {
                return HexCellQuery.OutsideMap();
            }

            HexCell cell;
            return cellsByCoordinate.TryGetValue(coordinate, out cell)
                ? HexCellQuery.Found(cell)
                : HexCellQuery.Missing();
        }

        public bool TryGetCell(HexCoord coordinate, out HexCell cell)
        {
            var query = Query(coordinate);
            cell = query.Cell;
            return query.HasCell;
        }
    }
}
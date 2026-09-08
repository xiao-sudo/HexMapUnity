using System;
using System.Collections.Generic;
using HexMap.Core;

namespace HexMap.Runtime
{
    public sealed class HexMap
    {
        private readonly HexMapRadius m_Radius;
        private readonly Dictionary<HexCoord, HexCell> m_CellsByCoordinate;
        private readonly IReadOnlyList<HexCell> m_Cells;

        public HexMap(HexMapDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            m_Radius = definition.Radius;
            var generatedCellCount = m_Radius.CellCount - definition.ExcludedCoordinates.Count;
            m_CellsByCoordinate = new Dictionary<HexCoord, HexCell>(generatedCellCount);
            var generatedCells = new List<HexCell>(generatedCellCount);

            for (var q = -m_Radius.Radius; q <= m_Radius.Radius; q++)
            {
                var minR = Math.Max(-m_Radius.Radius, -q - m_Radius.Radius);
                var maxR = Math.Min(m_Radius.Radius, -q + m_Radius.Radius);

                for (var r = minR; r <= maxR; r++)
                {
                    var coordinate = new HexCoord(q, r);
                    if (definition.IsExcluded(coordinate))
                    {
                        continue;
                    }

                    var cell = new HexCell(coordinate);
                    m_CellsByCoordinate.Add(coordinate, cell);
                    generatedCells.Add(cell);
                }
            }

            m_Cells = generatedCells.AsReadOnly();
        }

        public HexMapRadius Radius
        {
            get { return m_Radius; }
        }

        public int Count
        {
            get { return m_Cells.Count; }
        }

        public IReadOnlyList<HexCell> Cells
        {
            get { return m_Cells; }
        }

        public HexCellQuery Query(HexCoord coordinate)
        {
            if (!m_Radius.Contains(coordinate))
            {
                return HexCellQuery.OutsideMap();
            }

            HexCell cell;
            return m_CellsByCoordinate.TryGetValue(coordinate, out cell)
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
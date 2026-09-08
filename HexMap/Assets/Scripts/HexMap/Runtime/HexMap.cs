using System;
using System.Collections.Generic;
using HexMap.Core;

namespace HexMap.Runtime
{
    public sealed class HexMap
    {
        private readonly HexMapRadius m_Radius;
        private readonly Dictionary<HexCoord, HexCell> m_CellsByCoordinate;
        private readonly Dictionary<int, HexCell> m_CellsById;
        private readonly IReadOnlyList<HexCell> m_Cells;

        public HexMap(HexMapDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            m_Radius = definition.Radius;
            var generatedCellCount = m_Radius.CellCount;
            m_CellsByCoordinate = new Dictionary<HexCoord, HexCell>(generatedCellCount);
            m_CellsById = new Dictionary<int, HexCell>(generatedCellCount);
            var generatedCells = new List<HexCell>(generatedCellCount);
            var coordinatesById = CreateCoordinatesById();

            foreach (var coordinate in EnumerateCoordinates())
            {
                var cell = new HexCell(coordinatesById[coordinate], coordinate);
                m_CellsByCoordinate.Add(coordinate, cell);
                m_CellsById.Add(cell.Id, cell);
                generatedCells.Add(cell);
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

        public HexCellQuery Query(int cellId)
        {
            HexCell cell;
            return m_CellsById.TryGetValue(cellId, out cell)
                ? HexCellQuery.Found(cell)
                : HexCellQuery.Missing();
        }

        public bool TryGetCell(int cellId, out HexCell cell)
        {
            var query = Query(cellId);
            cell = query.Cell;
            return query.HasCell;
        }

        private Dictionary<HexCoord, int> CreateCoordinatesById()
        {
            var rings = new List<HexCoord>[m_Radius.Radius + 1];
            var center = new HexCoord(0, 0);

            foreach (var coordinate in EnumerateCoordinates())
            {
                var distance = HexCoord.Distance(center, coordinate);
                if (rings[distance] == null)
                {
                    rings[distance] = new List<HexCoord>();
                }

                rings[distance].Add(coordinate);
            }

            var coordinatesById = new Dictionary<HexCoord, int>(m_Radius.CellCount);
            var id = 0;
            foreach (var ring in rings)
            {
                foreach (var coordinate in ring)
                {
                    coordinatesById.Add(coordinate, id);
                    id++;
                }
            }

            return coordinatesById;
        }

        private IEnumerable<HexCoord> EnumerateCoordinates()
        {
            for (var q = -m_Radius.Radius; q <= m_Radius.Radius; q++)
            {
                var minR = Math.Max(-m_Radius.Radius, -q - m_Radius.Radius);
                var maxR = Math.Min(m_Radius.Radius, -q + m_Radius.Radius);

                for (var r = minR; r <= maxR; r++)
                {
                    yield return new HexCoord(q, r);
                }
            }
        }
    }
}

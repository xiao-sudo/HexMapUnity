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
            
            // Q + R + S = 0, => S = -(Q + R)
            // |Q| <= radius, |R| <= radius, |S| <= radius
            // |S| = |Q + R| <= radius => -radius - Q <= R <= radius - Q
            // (-radius - Q <= R <= radius - Q)  AND   (-radius <= R <= radius)
            // Max(-radius - Q, -radius) <= R <= Min(radius - Q, radius)
            for (var q = -m_Radius.Radius; q <= m_Radius.Radius; q++)
            {
                var minR = Math.Max(-m_Radius.Radius, -q - m_Radius.Radius);
                var maxR = Math.Min(m_Radius.Radius, -q + m_Radius.Radius);

                for (var r = minR; r <= maxR; r++)
                {
                    var coordinate = new HexCoord(q, r);
                    var cell = CreateCell(coordinate);
                    m_CellsByCoordinate.Add(coordinate, cell);
                    m_CellsById.Add(cell.Id, cell);
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

        /// <summary>
        /// Returns the stable CellId for <paramref name="coordinate"/>.
        ///
        /// <para>
        /// CellIds are allocated ring by ring starting from the center:
        /// <list type="bullet">
        /// <item>ring 0 contains 1 cell with Id = 0</item>
        /// <item>ring d contains 6d cells</item>
        /// </list>
        /// </para>
        ///
        /// <para>
        /// Within each ring the order follows the same q/r traversal used when
        /// constructing the map: iterate Q from -d to d, and for each Q iterate
        /// the two (or more) R values that fall on the ring boundary.
        /// </para>
        ///
        /// <para>
        /// The formula is therefore:
        /// <code>
        /// CellId
        ///     = first id of the current ring
        ///     + offset of this coordinate inside the ring
        /// </code>
        /// </para>
        /// </summary>
        public static int GetCellId(HexCoord coordinate)
        {
            var center = new HexCoord(0, 0);
            var distance = HexCoord.Distance(center, coordinate);

            // The center cell always has Id = 0.
            if (distance == 0)
            {
                return 0;
            }

            // Sum of all cells in rings 0 .. d-1:
            //     1 + 6*(1 + 2 + ... + (d - 1))
            //   = 1 + 3d(d - 1)
            var firstIdInRing = 1 + 3 * distance * (distance - 1);
            return firstIdInRing + GetRingOffset(coordinate, distance);
        }

        private static HexCell CreateCell(HexCoord coordinate)
        {
            return new HexCell(GetCellId(coordinate), coordinate);
        }

        /// <summary>
        /// Computes the 0-based offset of <paramref name="coordinate"/> inside
        /// the ring at distance <paramref name="distance"/>.
        ///
        /// <para>
        /// The ring is traversed column by column, Q from -distance to distance.
        /// For most columns there are exactly two ring cells:
        /// one with R &lt; 0 and one with R &gt; 0.
        /// Only the leftmost (Q = -d) and rightmost (Q = d) columns
        /// contain d + 1 consecutive cells.
        /// </para>
        ///
        /// <para>
        /// Visual layout for ring d, read left to right, top to bottom:
        /// <code>
        /// Q = -d :  R = 0 .. d          (d + 1 cells)
        /// -d &lt; Q &lt; 0 :  R = -q - d  (R &lt; 0)
        ///                 R = d        (R &gt; 0)
        /// 0 &lt;= Q &lt; d :  R = -d       (R &lt; 0)
        ///                 R = d - q    (R &gt; 0)
        /// Q = d :   R = -d .. 0         (d + 1 cells)
        /// </code>
        /// </para>
        /// </summary>
        private static int GetRingOffset(HexCoord coordinate, int distance)
        {
            // Leftmost column: R runs from 0 to d, offset == R.
            if (coordinate.Q == -distance)
            {
                return coordinate.R;
            }

            // Rightmost column: R runs from -d to 0.
            // Offset starts after the 4d - 1 cells in the columns before it,
            // then advances by (R + d) inside this column.
            if (coordinate.Q == distance)
            {
                return 5 * distance - 1 + coordinate.R + distance;
            }

            // Middle column: there are 2 cells per Q.
            // Base offset jumps past the leftmost column and all earlier
            // middle columns; the R > 0 cell is the second in this column.
            var offset = distance + 1 + 2 * (coordinate.Q + distance - 1);
            if (coordinate.R > 0)
            {
                offset++;
            }

            return offset;
        }
    }
}

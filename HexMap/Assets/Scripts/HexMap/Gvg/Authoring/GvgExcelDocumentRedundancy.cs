using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexMap.Gvg.Authoring
{
    /// <summary>
    /// Type of an Excel column. Int maps to an integer, IntArray maps to a
    /// bracketed integer list such as [1,2,3], and String covers everything else.
    /// </summary>
    public enum GvgExcelColumnKind
    {
        Int = 0,
        IntArray = 1,
        String = 2
    }

    /// <summary>
    /// Metadata for one logical or redundant Excel column: its header name and type.
    /// The order of <see cref="GvgExcelDocumentRedundancy.Columns"/> is the column
    /// order used by both import and export.
    /// </summary>
    [Serializable]
    public sealed class GvgExcelColumnInfo
    {
        [SerializeField] private string m_Name = string.Empty;
        [SerializeField] private GvgExcelColumnKind m_Kind = GvgExcelColumnKind.String;

        public GvgExcelColumnInfo() { }

        public GvgExcelColumnInfo(string name, GvgExcelColumnKind kind)
        {
            m_Name = name ?? string.Empty;
            m_Kind = kind;
        }

        public string Name
        {
            get { return m_Name; }
            set { m_Name = value ?? string.Empty; }
        }

        public GvgExcelColumnKind Kind
        {
            get { return m_Kind; }
            set { m_Kind = value; }
        }
    }

    /// <summary>
    /// One cell inside the 5-row header block. Text is the displayed value; fill
    /// and font colors are ARGB hex strings (for example FF037EAA) or empty when
    /// the cell has no explicit color; comment is the cell note text or empty.
    /// </summary>
    [Serializable]
    public sealed class GvgExcelHeaderCell
    {
        [SerializeField] private string m_ColumnName = string.Empty;
        [SerializeField] private string m_Text = string.Empty;
        [SerializeField] private string m_FillColor = string.Empty;
        [SerializeField] private string m_FontColor = string.Empty;
        [SerializeField] private string m_Comment = string.Empty;

        public GvgExcelHeaderCell() { }

        public GvgExcelHeaderCell(string columnName, string text, string fillColor, string fontColor, string comment)
        {
            m_ColumnName = columnName ?? string.Empty;
            m_Text = text ?? string.Empty;
            m_FillColor = fillColor ?? string.Empty;
            m_FontColor = fontColor ?? string.Empty;
            m_Comment = comment ?? string.Empty;
        }

        public string ColumnName
        {
            get { return m_ColumnName; }
            set { m_ColumnName = value ?? string.Empty; }
        }

        public string Text
        {
            get { return m_Text; }
            set { m_Text = value ?? string.Empty; }
        }

        public string FillColor
        {
            get { return m_FillColor; }
            set { m_FillColor = value ?? string.Empty; }
        }

        public string FontColor
        {
            get { return m_FontColor; }
            set { m_FontColor = value ?? string.Empty; }
        }

        public string Comment
        {
            get { return m_Comment; }
            set { m_Comment = value ?? string.Empty; }
        }

        public GvgExcelHeaderCell Clone()
        {
            return new GvgExcelHeaderCell(m_ColumnName, m_Text, m_FillColor, m_FontColor, m_Comment);
        }
    }

    /// <summary>
    /// One row of the header block. The cell order matches the column order of the
    /// redundancy document so the export can rebuild the exact first-5-rows block.
    /// </summary>
    [Serializable]
    public sealed class GvgExcelHeaderRow
    {
        [SerializeField] private List<GvgExcelHeaderCell> m_Cells = new List<GvgExcelHeaderCell>();

        public IReadOnlyList<GvgExcelHeaderCell> Cells
        {
            get { return m_Cells; }
        }

        public List<GvgExcelHeaderCell> MutableCells
        {
            get { return m_Cells; }
        }

        public void ReplaceCells(IEnumerable<GvgExcelHeaderCell> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            m_Cells = new List<GvgExcelHeaderCell>();
            foreach (var cell in cells)
            {
                if (cell == null) throw new ArgumentException("Header cell list cannot contain null entries.", nameof(cells));
                m_Cells.Add(cell.Clone());
            }
        }

        public GvgExcelHeaderRow Clone()
        {
            var clone = new GvgExcelHeaderRow();
            clone.ReplaceCells(m_Cells);
            return clone;
        }
    }

    /// <summary>
    /// Content of one cell in a data row, keyed by column name. Both logical and
    /// redundant columns are recorded; on export logical columns are recomputed and
    /// redundant columns are written back verbatim.
    /// </summary>
    [Serializable]
    public sealed class GvgExcelColumnContent
    {
        [SerializeField] private string m_ColumnName = string.Empty;
        [SerializeField] private string m_Content = string.Empty;

        public GvgExcelColumnContent() { }

        public GvgExcelColumnContent(string columnName, string content)
        {
            m_ColumnName = columnName ?? string.Empty;
            m_Content = content ?? string.Empty;
        }

        public string ColumnName
        {
            get { return m_ColumnName; }
            set { m_ColumnName = value ?? string.Empty; }
        }

        public string Content
        {
            get { return m_Content; }
            set { m_Content = value ?? string.Empty; }
        }
    }

    /// <summary>
    /// One imported data row, keyed by PlotId. Content is recorded for every column
    /// so redundant values can be written back unchanged on the next export.
    /// </summary>
    [Serializable]
    public sealed class GvgExcelRowData
    {
        [SerializeField] private int m_PlotId;
        [SerializeField] private List<GvgExcelColumnContent> m_Columns = new List<GvgExcelColumnContent>();

        public int PlotId
        {
            get { return m_PlotId; }
            set { m_PlotId = value; }
        }

        public IReadOnlyList<GvgExcelColumnContent> Columns
        {
            get { return m_Columns; }
        }

        public List<GvgExcelColumnContent> MutableColumns
        {
            get { return m_Columns; }
        }

        public void ReplaceColumns(IEnumerable<GvgExcelColumnContent> columns)
        {
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            m_Columns = new List<GvgExcelColumnContent>();
            foreach (var column in columns)
            {
                if (column == null) throw new ArgumentException("Column content list cannot contain null entries.", nameof(columns));
                m_Columns.Add(new GvgExcelColumnContent(column.ColumnName, column.Content));
            }
        }

        public GvgExcelRowData Clone()
        {
            var clone = new GvgExcelRowData();
            clone.m_PlotId = m_PlotId;
            clone.ReplaceColumns(m_Columns);
            return clone;
        }
    }

    /// <summary>
    /// Complete set of Excel round-trip redundancy recorded on a
    /// <see cref="GvgMapAuthoringAsset"/>: the 5-row header block (text, fill/font
    /// colors, comments), the ordered column list with per-column types, and the
    /// per-data-row content of every column. Each import replaces it entirely.
    /// </summary>
    [Serializable]
    public sealed class GvgExcelDocumentRedundancy
    {
        [SerializeField] private List<GvgExcelHeaderRow> m_HeaderRows = new List<GvgExcelHeaderRow>();
        [SerializeField] private List<GvgExcelColumnInfo> m_Columns = new List<GvgExcelColumnInfo>();
        [SerializeField] private List<GvgExcelRowData> m_Rows = new List<GvgExcelRowData>();

        public IReadOnlyList<GvgExcelHeaderRow> HeaderRows
        {
            get { return m_HeaderRows; }
        }

        public IReadOnlyList<GvgExcelColumnInfo> Columns
        {
            get { return m_Columns; }
        }

        public IReadOnlyList<GvgExcelRowData> Rows
        {
            get { return m_Rows; }
        }

        public void ReplaceHeaderRows(IEnumerable<GvgExcelHeaderRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            m_HeaderRows = new List<GvgExcelHeaderRow>();
            foreach (var row in rows)
            {
                if (row == null) throw new ArgumentException("Header row list cannot contain null entries.", nameof(rows));
                m_HeaderRows.Add(row.Clone());
            }
        }

        public void ReplaceColumns(IEnumerable<GvgExcelColumnInfo> columns)
        {
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            m_Columns = new List<GvgExcelColumnInfo>();
            foreach (var column in columns)
            {
                if (column == null) throw new ArgumentException("Column list cannot contain null entries.", nameof(columns));
                m_Columns.Add(new GvgExcelColumnInfo(column.Name, column.Kind));
            }
        }

        public void ReplaceRows(IEnumerable<GvgExcelRowData> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            m_Rows = new List<GvgExcelRowData>();
            foreach (var row in rows)
            {
                if (row == null) throw new ArgumentException("Row list cannot contain null entries.", nameof(rows));
                m_Rows.Add(row.Clone());
            }
        }

        public bool ContainsRow(int plotId)
        {
            return FindRow(plotId) != null;
        }

        public GvgExcelRowData FindRow(int plotId)
        {
            for (var index = 0; index < m_Rows.Count; index++)
            {
                if (m_Rows[index].PlotId == plotId) return m_Rows[index];
            }

            return null;
        }

        public void AddRow(GvgExcelRowData row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (ContainsRow(row.PlotId)) return;
            m_Rows.Add(row.Clone());
        }

        public void RemoveRow(int plotId)
        {
            for (var index = m_Rows.Count - 1; index >= 0; index--)
            {
                if (m_Rows[index].PlotId == plotId) m_Rows.RemoveAt(index);
            }
        }

        public GvgExcelColumnKind GetColumnKind(string columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return GvgExcelColumnKind.String;
            for (var index = 0; index < m_Columns.Count; index++)
            {
                if (string.Equals(m_Columns[index].Name, columnName, StringComparison.Ordinal))
                {
                    return m_Columns[index].Kind;
                }
            }

            return GvgExcelColumnKind.String;
        }

        public string GetColumnContent(int plotId, string columnName)
        {
            var row = FindRow(plotId);
            if (row == null) return string.Empty;
            for (var index = 0; index < row.Columns.Count; index++)
            {
                if (string.Equals(row.Columns[index].ColumnName, columnName, StringComparison.Ordinal))
                {
                    return row.Columns[index].Content;
                }
            }

            return string.Empty;
        }

        public void SetColumnContent(int plotId, string columnName, string content)
        {
            var row = FindRow(plotId);
            if (row == null)
            {
                row = new GvgExcelRowData();
                row.PlotId = plotId;
                AddRow(row);
                row = FindRow(plotId);
            }

            for (var index = 0; index < row.MutableColumns.Count; index++)
            {
                if (string.Equals(row.MutableColumns[index].ColumnName, columnName, StringComparison.Ordinal))
                {
                    row.MutableColumns[index].Content = content ?? string.Empty;
                    return;
                }
            }

            row.MutableColumns.Add(new GvgExcelColumnContent(columnName, content ?? string.Empty));
        }

        /// <summary>
        /// Re-keys every row whose PlotId changed. Rows whose PlotId collides after
        /// re-keying keep the first occurrence. Callers should then prune rows that no
        /// longer correspond to a live Plot.
        /// </summary>
        public void ReKey(IReadOnlyDictionary<int, int> mapping)
        {
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            for (var index = 0; index < m_Rows.Count; index++)
            {
                var row = m_Rows[index];
                int newId;
                if (mapping.TryGetValue(row.PlotId, out newId) && newId != row.PlotId)
                {
                    row.PlotId = newId;
                }
            }

            var seen = new HashSet<int>();
            for (var index = m_Rows.Count - 1; index >= 0; index--)
            {
                if (!seen.Add(m_Rows[index].PlotId))
                {
                    m_Rows.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Removes every row whose PlotId is not present in <paramref name="livePlotIds"/>.
        /// </summary>
        public void RetainOnly(IEnumerable<int> livePlotIds)
        {
            if (livePlotIds == null) throw new ArgumentNullException(nameof(livePlotIds));
            var live = new HashSet<int>(livePlotIds);
            for (var index = m_Rows.Count - 1; index >= 0; index--)
            {
                if (!live.Contains(m_Rows[index].PlotId))
                {
                    m_Rows.RemoveAt(index);
                }
            }
        }

        public string DefaultContent(GvgExcelColumnKind kind)
        {
            switch (kind)
            {
                case GvgExcelColumnKind.Int:
                    return "0";
                case GvgExcelColumnKind.IntArray:
                    return "[]";
                default:
                    return string.Empty;
            }
        }

        public GvgExcelDocumentRedundancy Clone()
        {
            var clone = new GvgExcelDocumentRedundancy();
            clone.ReplaceHeaderRows(m_HeaderRows);
            clone.ReplaceColumns(m_Columns);
            clone.ReplaceRows(m_Rows);
            return clone;
        }
    }
}

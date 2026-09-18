using System;
using HexMap.Core;
using HexMap.Runtime;

namespace HexMap.UnityRuntime
{
    public sealed class HexView
    {
        private readonly HexCoord m_Coordinate;
        private readonly HexCell m_Cell;
        private readonly HexRenderHandle m_RenderHandle;

        internal HexView(HexCell cell, HexRenderHandle renderHandle)
        {
            m_Coordinate = cell.Coordinate;
            m_Cell = cell;
            m_RenderHandle = renderHandle;
        }

        public HexCoord Coordinate
        {
            get { return m_Coordinate; }
        }

        public HexCell Cell
        {
            get { return m_Cell; }
        }

        public bool IsValid
        {
            get { return m_RenderHandle != null && m_RenderHandle.IsValid; }
        }

        public bool IsSelected
        {
            get { return m_RenderHandle != null && m_RenderHandle.IsSelected; }
        }

        public void SetAppearance(HexAppearance appearance)
        {
            EnsureValid();
            m_RenderHandle.SetAppearance(appearance);
        }

        public void Select(HexSelectionAppearance appearance)
        {
            EnsureValid();
            m_RenderHandle.Select(appearance);
        }

        public void Deselect()
        {
            EnsureValid();
            m_RenderHandle.Deselect();
        }

        internal int Generation
        {
            get { return m_RenderHandle.Generation; }
        }

        internal void Invalidate()
        {
            m_RenderHandle.Invalidate();
        }

        private void EnsureValid()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("The HexView is no longer valid.");
            }
        }
    }
}
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

        public void SetAppearance(HexAppearance appearance)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("The HexView is no longer valid.");
            }

            m_RenderHandle.SetAppearance(appearance);
        }

        internal int Generation
        {
            get { return m_RenderHandle.Generation; }
        }

        internal void Invalidate()
        {
            m_RenderHandle.Invalidate();
        }
    }
}
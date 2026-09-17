using System;
using System.Collections.Generic;
using HexMap.Core;
using UnityEngine;

namespace HexMap.Runtime
{
    public enum PathResultStatus { Success = 0, InvalidInput = 1, NoPath = 2 }

    public enum PathFailureReason
    {
        None = 0,
        StartMissing = 1,
        NoValidTargets = 2,
        NoReachableTarget = 3,
        ResultCapacityExceeded = 4,
        NoValidStarts = 5,
        MapNotInitialized = 6,
        PlotNotFound = 7
    }

    public sealed class PathResult
    {
        private readonly List<HexCell> m_Cells;
        private PathResultStatus m_Status;
        private PathFailureReason m_Reason;
        private HexCell m_ReachedTarget;
        private int m_Cost;

        public PathResult(List<HexCell> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            m_Cells = cells;
            SetFailure(PathResultStatus.InvalidInput, PathFailureReason.None);
        }

        public PathResultStatus Status { get { return m_Status; } }
        public IReadOnlyList<HexCell> Cells { get { return m_Cells; } }
        public int Count { get { return m_Cells.Count; } }
        public HexCell ReachedTarget { get { return m_ReachedTarget; } }
        public int Cost { get { return m_Cost; } }
        public PathFailureReason Reason { get { return m_Reason; } }
        public bool IsSuccess { get { return m_Status == PathResultStatus.Success; } }

        public bool CopyWorldCentersTo(HexLayout layout, List<Vector3> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            if (output.Capacity < m_Cells.Count) return false;
            for (var index = 0; index < m_Cells.Count; index++)
            {
                output.Add(layout.HexToWorld(m_Cells[index].Coordinate));
            }
            return true;
        }

        internal void BeginSearch()
        {
            m_Cells.Clear();
            m_Status = PathResultStatus.InvalidInput;
            m_Reason = PathFailureReason.None;
            m_ReachedTarget = default(HexCell);
            m_Cost = 0;
        }

        internal bool TryAddCell(HexCell cell)
        {
            if (m_Cells.Count >= m_Cells.Capacity) return false;
            m_Cells.Add(cell);
            return true;
        }

        internal void ReverseCells() { m_Cells.Reverse(); }

        internal void SetSuccess(HexCell reachedTarget)
        {
            m_Status = PathResultStatus.Success;
            m_Reason = PathFailureReason.None;
            m_ReachedTarget = reachedTarget;
            m_Cost = m_Cells.Count - 1;
        }

        public void SetFailure(PathResultStatus status, PathFailureReason reason)
        {
            m_Cells.Clear();
            m_Status = status;
            m_Reason = reason;
            m_ReachedTarget = default(HexCell);
            m_Cost = 0;
        }
    }
}

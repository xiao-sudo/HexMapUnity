using System.Collections.Generic;
using System.Collections.ObjectModel;
using HexMap.Core;
using UnityEngine;

namespace HexMap.Runtime
{
    public enum PathResultStatus
    {
        Success = 0,
        InvalidInput = 1,
        NoPath = 2
    }

    public enum PathFailureReason
    {
        None = 0,
        StartMissing = 1,
        NoValidTargets = 2,
        NoReachableTarget = 3
    }

    public sealed class PathResult
    {
        private PathResult(
            PathResultStatus status,
            PathFailureReason reason,
            IReadOnlyList<HexCell> cells,
            HexCell reachedTarget,
            int cost)
        {
            Status = status;
            Reason = reason;
            Cells = cells;
            ReachedTarget = reachedTarget;
            Cost = cost;
        }

        public PathResultStatus Status { get; }
        public IReadOnlyList<HexCell> Cells { get; }
        public HexCell ReachedTarget { get; }
        public int Cost { get; }
        public PathFailureReason Reason { get; }
        public bool IsSuccess
        {
            get { return Status == PathResultStatus.Success; }
        }

        public IReadOnlyList<Vector3> ToWorldCenters(HexLayout layout)
        {
            var worldCenters = new List<Vector3>(Cells.Count);
            foreach (var cell in Cells)
            {
                worldCenters.Add(layout.HexToWorld(cell.Coordinate));
            }

            return new ReadOnlyCollection<Vector3>(worldCenters);
        }

        internal static PathResult CreateSuccess(IReadOnlyList<HexCell> cells, HexCell reachedTarget)
        {
            return new PathResult(
                PathResultStatus.Success,
                PathFailureReason.None,
                cells,
                reachedTarget,
                cells.Count - 1);
        }

        internal static PathResult CreateFailure(PathResultStatus status, PathFailureReason reason)
        {
            return new PathResult(
                status,
                reason,
                new ReadOnlyCollection<HexCell>(new List<HexCell>()),
                default(HexCell),
                0);
        }
    }
}
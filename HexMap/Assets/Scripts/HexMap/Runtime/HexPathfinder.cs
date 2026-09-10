using System;
using System.Collections.Generic;
using HexMap.Core;

namespace HexMap.Runtime
{
    public sealed class HexPathfinder
    {
        private readonly HexMap m_Map;
        private readonly PathSearchWorkspace m_Workspace;

        public HexPathfinder(HexMap map, PathSearchWorkspace workspace)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (!workspace.IsFor(map)) throw new ArgumentException("The workspace belongs to a different map.", nameof(workspace));
            m_Map = map;
            m_Workspace = workspace;
        }

        public PathResult FindPath(PathRequest request, PathResult result)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return FindPathCore(request.Starts, request.Targets, request.Policy, result);
        }

        public PathResult FindPath(ReusablePathRequest request, PathResult result)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return FindPathCore(request.Starts, request.Targets, request.Policy, result);
        }

        private PathResult FindPathCore(
            IReadOnlyList<HexCell> starts,
            IReadOnlyList<HexCell> targets,
            IHexPathPolicy policy,
            PathResult result)
        {
            if (starts == null) throw new ArgumentNullException(nameof(starts));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (result == null) throw new ArgumentNullException(nameof(result));

            result.BeginSearch();
            var targetCoordinates = m_Workspace.TargetCoordinates;
            var parents = m_Workspace.Parents;
            var distances = m_Workspace.Distances;
            var pending = m_Workspace.Pending;
            var evaluatedTargets = m_Workspace.EvaluatedTargets;
            targetCoordinates.Clear();
            parents.Clear();
            distances.Clear();
            pending.Clear();
            evaluatedTargets.Clear();

            var validStartCount = 0;
            for (var startIndex = 0; startIndex < starts.Count; startIndex++)
            {
                HexCell mapStart;
                if (!IsCellInMap(starts[startIndex], out mapStart))
                {
                    continue;
                }

                if (!distances.ContainsKey(mapStart.Coordinate))
                {
                    distances.Add(mapStart.Coordinate, 0);
                    pending.Enqueue(mapStart.Coordinate);
                    validStartCount++;
                }
            }

            if (validStartCount == 0)
            {
                result.SetFailure(PathResultStatus.InvalidInput, starts.Count == 1 ? PathFailureReason.StartMissing : PathFailureReason.NoValidStarts);
                return result;
            }

            for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                HexCell mapTarget;
                if (IsCellInMap(targets[targetIndex], out mapTarget))
                {
                    targetCoordinates.Add(mapTarget.Coordinate);
                }
            }

            if (targetCoordinates.Count == 0)
            {
                result.SetFailure(PathResultStatus.InvalidInput, PathFailureReason.NoValidTargets);
                return result;
            }

            for (var startIndex = 0; startIndex < starts.Count; startIndex++)
            {
                HexCell mapStart;
                if (IsCellInMap(starts[startIndex], out mapStart) &&
                    targetCoordinates.Contains(mapStart.Coordinate))
                {
                    if (!result.TryAddCell(mapStart))
                    {
                        result.SetFailure(PathResultStatus.InvalidInput, PathFailureReason.ResultCapacityExceeded);
                        return result;
                    }

                    result.SetSuccess(mapStart);
                    return result;
                }
            }

            var bestTarget = default(HexCoord);
            var bestDistance = int.MaxValue;
            var hasBestTarget = false;

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                var currentDistance = distances[current];
                if (currentDistance >= bestDistance) continue;

                for (var directionIndex = 0; directionIndex < 6; directionIndex++)
                {
                    var neighbor = current.GetNeighbor((HexDirection)directionIndex);
                    HexCell neighborCell;
                    if (!m_Map.TryGetCell(neighbor, out neighborCell)) continue;

                    if (targetCoordinates.Contains(neighbor))
                    {
                        if (evaluatedTargets.Add(neighbor) && policy.CanEnter(neighborCell))
                        {
                            var candidateDistance = currentDistance + 1;
                            if (!hasBestTarget ||
                                candidateDistance < bestDistance ||
                                (candidateDistance == bestDistance && IsBefore(neighbor, bestTarget)))
                            {
                                bestTarget = neighbor;
                                bestDistance = candidateDistance;
                                hasBestTarget = true;
                                parents[neighbor] = current;
                            }
                        }

                        continue;
                    }

                    if (distances.ContainsKey(neighbor) || !policy.CanPass(neighborCell)) continue;
                    distances.Add(neighbor, currentDistance + 1);
                    parents.Add(neighbor, current);
                    pending.Enqueue(neighbor);
                }
            }

            if (!hasBestTarget)
            {
                result.SetFailure(PathResultStatus.NoPath, PathFailureReason.NoReachableTarget);
                return result;
            }

            HexCell chosenStart;
            if (!FindRootFor(bestTarget, starts, out chosenStart))
            {
                result.SetFailure(PathResultStatus.NoPath, PathFailureReason.NoReachableTarget);
                return result;
            }

            if (!BuildPath(chosenStart, bestTarget, result))
            {
                result.SetFailure(PathResultStatus.InvalidInput, PathFailureReason.ResultCapacityExceeded);
                return result;
            }

            result.SetSuccess(m_Map.Query(bestTarget).Cell);
            return result;
        }

        private bool IsCellInMap(HexCell cell, out HexCell mapCell)
        {
            if (!m_Map.TryGetCell(cell.Coordinate, out mapCell)) return false;
            return mapCell.Id == cell.Id;
        }

        private bool FindRootFor(
            HexCoord target,
            IReadOnlyList<HexCell> starts,
            out HexCell root)
        {
            var current = target;
            while (m_Workspace.Parents.TryGetValue(current, out var parent))
            {
                current = parent;
            }

            root = default(HexCell);
            for (var index = 0; index < starts.Count; index++)
            {
                HexCell mapStart;
                if (IsCellInMap(starts[index], out mapStart) && mapStart.Coordinate == current)
                {
                    root = mapStart;
                    return true;
                }
            }

            return false;
        }

        private static bool IsBefore(HexCoord first, HexCoord second)
        {
            return first.Q < second.Q || (first.Q == second.Q && first.R < second.R);
        }

        private bool BuildPath(HexCell start, HexCoord target, PathResult result)
        {
            var current = target;
            while (true)
            {
                HexCell cell;
                if (!m_Map.TryGetCell(current, out cell) || !result.TryAddCell(cell)) return false;
                if (current == start.Coordinate) break;
                current = m_Workspace.Parents[current];
            }

            result.ReverseCells();
            return true;
        }
    }
}

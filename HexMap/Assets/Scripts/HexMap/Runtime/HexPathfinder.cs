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
            // This is a multi-source, multi-target BFS. Every hex-to-hex move costs one,
            // so the first distance at which a target is discovered is its shortest path
            // distance. Ordinary cells use CanPass; target cells use CanEnter and are
            // deliberately not enqueued as intermediate nodes.
            if (starts == null) throw new ArgumentNullException(nameof(starts));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (result == null) throw new ArgumentNullException(nameof(result));

            // The workspace is caller-owned and reused between searches. Clear its
            // contents without replacing the dictionaries, sets, or queue.
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

            // All valid starts are BFS roots at distance zero. A start bypasses
            // CanPass because it is already occupied by the caller's moving entity.
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

            // Keep only map cells whose coordinate and stable Cell Id both match the
            // request. TargetCoordinates is a set, so duplicate targets are harmless.
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

            // Reaching a target that is already a valid start costs zero and does not
            // require either CanPass or CanEnter.
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
                // Once a target at distance d is known, expanding a node at distance d
                // can only produce targets at distance d + 1 or more.
                if (currentDistance >= bestDistance) continue;

                for (var directionIndex = 0; directionIndex < 6; directionIndex++)
                {
                    var neighbor = current.GetNeighbor((HexDirection)directionIndex);
                    HexCell neighborCell;
                    if (!m_Map.TryGetCell(neighbor, out neighborCell)) continue;

                    if (targetCoordinates.Contains(neighbor))
                    {
                        // A target is an endpoint, not a transit cell: evaluate it with
                        // CanEnter, remember its parent, and never enqueue it. A target
                        // is evaluated once because CanEnter depends on the Cell only.
                        if (evaluatedTargets.Add(neighbor) && policy.CanEnter(neighborCell))
                        {
                            var candidateDistance = currentDistance + 1;
                            // Prefer the shortest target. Equal distances are resolved
                            // by (Q, R) so the result is deterministic.
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

                    // Non-target cells must be passable and are visited at most once.
                    // Their parent and distance are recorded before they enter the BFS.
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
            // The chosen target is not stored in Distances; its parent link is enough
            // to walk back to the root selected from the supplied starts.
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
            // Parents point from child to predecessor, so collect target-to-start and
            // reverse once to expose the path in movement order.
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HexMap.Core;

namespace HexMap.Runtime
{
    internal static class HexPathfinder
    {
        public static PathResult FindPath(HexMap map, PathRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            HexCell mapStart;
            if (!IsCellInMap(map, request.Start, out mapStart))
            {
                return PathResult.CreateFailure(
                    PathResultStatus.InvalidInput,
                    PathFailureReason.StartMissing);
            }

            var targetCoordinates = new HashSet<HexCoord>();
            foreach (var target in request.Targets)
            {
                HexCell mapTarget;
                if (IsCellInMap(map, target, out mapTarget))
                {
                    targetCoordinates.Add(mapTarget.Coordinate);
                }
            }

            if (targetCoordinates.Count == 0)
            {
                return PathResult.CreateFailure(
                    PathResultStatus.InvalidInput,
                    PathFailureReason.NoValidTargets);
            }

            if (targetCoordinates.Contains(mapStart.Coordinate))
            {
                return PathResult.CreateSuccess(
                    new ReadOnlyCollection<HexCell>(new[] { mapStart }),
                    mapStart);
            }

            var parents = new Dictionary<HexCoord, HexCoord>();
            var distances = new Dictionary<HexCoord, int>
            {
                { mapStart.Coordinate, 0 }
            };
            var pending = new Queue<HexCoord>();
            var evaluatedTargets = new HashSet<HexCoord>();
            pending.Enqueue(mapStart.Coordinate);

            var bestTarget = default(HexCoord);
            var bestDistance = int.MaxValue;
            var hasBestTarget = false;

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                var currentDistance = distances[current];
                if (currentDistance >= bestDistance)
                {
                    continue;
                }

                for (var directionIndex = 0; directionIndex < 6; directionIndex++)
                {
                    var neighbor = current.GetNeighbor((HexDirection)directionIndex);
                    HexCell neighborCell;
                    if (!map.TryGetCell(neighbor, out neighborCell))
                    {
                        continue;
                    }

                    if (targetCoordinates.Contains(neighbor))
                    {
                        if (evaluatedTargets.Add(neighbor) && request.CanEnter(neighborCell, request.Context))
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

                    if (distances.ContainsKey(neighbor) || !request.CanPass(neighborCell, request.Context))
                    {
                        continue;
                    }

                    distances.Add(neighbor, currentDistance + 1);
                    parents.Add(neighbor, current);
                    pending.Enqueue(neighbor);
                }
            }

            if (!hasBestTarget)
            {
                return PathResult.CreateFailure(
                    PathResultStatus.NoPath,
                    PathFailureReason.NoReachableTarget);
            }

            return PathResult.CreateSuccess(
                BuildPath(map, mapStart, bestTarget, parents),
                map.Query(bestTarget).Cell);
        }

        private static bool IsCellInMap(HexMap map, HexCell cell, out HexCell mapCell)
        {
            if (!map.TryGetCell(cell.Coordinate, out mapCell))
            {
                return false;
            }

            return mapCell.Id == cell.Id;
        }

        private static bool IsBefore(HexCoord first, HexCoord second)
        {
            return first.Q < second.Q || (first.Q == second.Q && first.R < second.R);
        }

        private static IReadOnlyList<HexCell> BuildPath(
            HexMap map,
            HexCell start,
            HexCoord target,
            IReadOnlyDictionary<HexCoord, HexCoord> parents)
        {
            var coordinates = new List<HexCoord>();
            var current = target;
            coordinates.Add(current);
            while (current != start.Coordinate)
            {
                current = parents[current];
                coordinates.Add(current);
            }

            coordinates.Reverse();
            var cells = new List<HexCell>(coordinates.Count);
            foreach (var coordinate in coordinates)
            {
                cells.Add(map.Query(coordinate).Cell);
            }

            return new ReadOnlyCollection<HexCell>(cells);
        }
    }
}
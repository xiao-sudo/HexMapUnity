using System;
using System.Collections.Generic;
using NUnit.Framework;
using HexMap.Core;
using HexMap.Runtime;
using UnityEngine;

namespace HexMap.Runtime.Tests
{
    [TestFixture]
    public sealed class HexPathfindingTests
    {
        [Test]
        public void FindsAUnitCostPathFromStartToTarget()
        {
            var map = new HexMap(new HexMapDefinition(2));
            var start = CellAt(map, 0, 0);
            var target = CellAt(map, 2, 0);
            var context = new object();
            var request = CreateRequest(
                start,
                new[] { target },
                context,
                (cell, callbackContext) => ReferenceEquals(callbackContext, context),
                (cell, callbackContext) => ReferenceEquals(callbackContext, context));

            var result = FindPath(map, request);

            AssertSuccess(result, target, 2);
            AssertCoordinates(result, map, new[]
            {
                new HexCoord(0, 0),
                new HexCoord(1, 0),
                new HexCoord(2, 0)
            });
        }

        [Test]
        public void StartingOnATargetSucceedsWithoutCheckingEitherRule()
        {
            var map = new HexMap(new HexMapDefinition(1));
            var start = CellAt(map, 0, 0);
            var canPassCalls = 0;
            var canEnterCalls = 0;
            var request = CreateRequest(
                start,
                new[] { start },
                new object(),
                (cell, context) =>
                {
                    canPassCalls++;
                    return false;
                },
                (cell, context) =>
                {
                    canEnterCalls++;
                    return false;
                });

            var result = FindPath(map, request);

            AssertSuccess(result, start, 0);
            Assert.That(canPassCalls, Is.EqualTo(0));
            Assert.That(canEnterCalls, Is.EqualTo(0));
        }

        [Test]
        public void StartDoesNotNeedToPassAndCanLeaveEvenWhenItWouldBeBlocked()
        {
            var map = new HexMap(new HexMapDefinition(1));
            var start = CellAt(map, 0, 0);
            var target = CellAt(map, 1, 0);
            var request = CreateRequest(
                start,
                new[] { target },
                new object(),
                (cell, context) => false,
                (cell, context) => true);

            var result = FindPath(map, request);

            AssertSuccess(result, target, 1);
        }

        [Test]
        public void CanEnterAllowsABlockedTargetButCanPassIsNotUsedForThatFinalStep()
        {
            var map = new HexMap(new HexMapDefinition(1));
            var start = CellAt(map, 0, 0);
            var target = CellAt(map, 1, 0);
            var canPassSawTarget = false;
            var canEnterSawTarget = false;
            var request = CreateRequest(
                start,
                new[] { target },
                new object(),
                (cell, context) =>
                {
                    canPassSawTarget |= cell.Coordinate == target.Coordinate;
                    return true;
                },
                (cell, context) =>
                {
                    canEnterSawTarget |= cell.Coordinate == target.Coordinate;
                    return true;
                });

            var result = FindPath(map, request);

            AssertSuccess(result, target, 1);
            Assert.That(canPassSawTarget, Is.False);
            Assert.That(canEnterSawTarget, Is.True);
        }

        [Test]
        public void ARejectedTargetCannotBeUsedAsAnIntermediateCell()
        {
            var map = new HexMap(new HexMapDefinition(3));
            var start = CellAt(map, 0, 0);
            var rejectedTarget = CellAt(map, 1, 0);
            var reachableTarget = CellAt(map, 2, 0);
            var request = CreateRequest(
                start,
                new[] { rejectedTarget, reachableTarget },
                new object(),
                (cell, context) => true,
                (cell, context) => cell.Coordinate != rejectedTarget.Coordinate);

            var result = FindPath(map, request);

            AssertSuccess(result, reachableTarget, 3);
            var query = map.Query(result.Cells[1]);
            Assert.That(query.Cell.Coordinate, Is.EqualTo(new HexCoord(1, -1)));
            Assert.That(result.Cells, Has.None.EqualTo(rejectedTarget));
        }

        [Test]
        public void ChoosesTheNearestReachableCellFromMultipleTargets()
        {
            var map = new HexMap(new HexMapDefinition(3));
            var start = CellAt(map, 0, 0);
            var nearTarget = CellAt(map, 1, 0);
            var farTarget = CellAt(map, 2, 0);
            var request = CreateRequest(
                start,
                new[] { farTarget, nearTarget, nearTarget },
                new object(),
                (cell, context) => true,
                (cell, context) => true);

            var result = FindPath(map, request);

            AssertSuccess(result, nearTarget, 1);
        }

        [Test]
        public void EqualCostTargetsUseCoordinateOrderInsteadOfTargetListOrDiscoveryOrder()
        {
            var map = new HexMap(new HexMapDefinition(1));
            var start = CellAt(map, 0, 0);
            var qZeroTarget = CellAt(map, 0, 1);
            var qOneTarget = CellAt(map, 1, 0);
            var request = CreateRequest(
                start,
                new[] { qOneTarget, qZeroTarget, qOneTarget },
                new object(),
                (cell, context) => true,
                (cell, context) => true);

            var result = FindPath(map, request);

            AssertSuccess(result, qZeroTarget, 1);
        }

        [Test]
        public void EqualLengthPathsUseTheCanonicalNeighborDirectionOrder()
        {
            var map = new HexMap(new HexMapDefinition(2));
            var start = CellAt(map, 0, 0);
            var target = CellAt(map, 1, 1);
            var request = CreateRequest(
                start,
                new[] { target },
                new object(),
                (cell, context) => true,
                (cell, context) => true);

            var result = FindPath(map, request);

            AssertSuccess(result, target, 2);
            var query = map.Query(result.Cells[1]);
            Assert.That(query.Cell.Coordinate, Is.EqualTo(new HexCoord(1, 0)));
        }

        [Test]
        public void InvalidStartReturnsAStartMissingInputFailure()
        {
            var map = new HexMap(new HexMapDefinition(1));
            var invalidStart = new HexCell(99, new HexCoord(0, 0));
            var request = CreateRequest(
                invalidStart,
                new[] { CellAt(map, 1, 0) },
                new object(),
                (cell, context) => true,
                (cell, context) => true);

            var result = FindPath(map, request);

            AssertFailure(result, PathResultStatus.InvalidInput, PathFailureReason.StartMissing);
        }

        [Test]
        public void InvalidTargetsAreFilteredWhenAValidTargetRemains()
        {
            var map = new HexMap(new HexMapDefinition(1));
            var start = CellAt(map, 0, 0);
            var target = CellAt(map, 1, 0);
            var invalidTarget = new HexCell(99, new HexCoord(0, 0));
            var request = CreateRequest(
                start,
                new[] { invalidTarget, target },
                new object(),
                (cell, context) => true,
                (cell, context) => true);

            var result = FindPath(map, request);

            AssertSuccess(result, target, 1);
        }

        [Test]
        public void EmptyOrInvalidTargetsReturnNoValidTargetsInputFailure()
        {
            var map = new HexMap(new HexMapDefinition(1));
            var start = CellAt(map, 0, 0);
            var invalidTarget = new HexCell(99, new HexCoord(0, 0));

            var emptyResult = FindPath(map, CreateRequest(
                start,
                new HexCell[0],
                new object(),
                (cell, context) => true,
                (cell, context) => true));
            var invalidResult = FindPath(map, CreateRequest(
                start,
                new[] { invalidTarget },
                new object(),
                (cell, context) => true,
                (cell, context) => true));

            AssertFailure(emptyResult, PathResultStatus.InvalidInput, PathFailureReason.NoValidTargets);
            AssertFailure(invalidResult, PathResultStatus.InvalidInput, PathFailureReason.NoValidTargets);
        }

        [Test]
        public void UnreachableTargetsReturnNoPathWithoutAnExecutablePartialPath()
        {
            var map = new HexMap(new HexMapDefinition(2));
            var start = CellAt(map, 0, 0);
            var target = CellAt(map, 2, 0);
            var request = CreateRequest(
                start,
                new[] { target },
                new object(),
                (cell, context) => false,
                (cell, context) => true);

            var result = FindPath(map, request);

            AssertFailure(result, PathResultStatus.NoPath, PathFailureReason.NoReachableTarget);
            Assert.That(result.Cells, Is.Empty);
            Assert.That(result.Cost, Is.EqualTo(0));
            Assert.That(result.ReachedTarget, Is.EqualTo(default(HexCell)));
        }

        [Test]
        public void CanPassAndCanEnterReceiveTheSameOpaqueContext()
        {
            var map = new HexMap(new HexMapDefinition(1));
            var start = CellAt(map, 0, 0);
            var target = CellAt(map, 1, 0);
            var context = new object();
            var canPassContext = new object();
            var canEnterContext = new object();
            var request = CreateRequest(
                start,
                new[] { target },
                context,
                (cell, callbackContext) =>
                {
                    canPassContext = callbackContext;
                    return true;
                },
                (cell, callbackContext) =>
                {
                    canEnterContext = callbackContext;
                    return true;
                });

            FindPath(map, request);

            Assert.That(canPassContext, Is.SameAs(context));
            Assert.That(canEnterContext, Is.SameAs(context));
        }

        [Test]
        public void ReusableRequestAndResultCanBeUsedForMultipleSearches()
        {
            var map = new HexMap(new HexMapDefinition(2));
            var start = CellAt(map, 0, 0);
            var firstTarget = CellAt(map, 1, 0);
            var secondTarget = CellAt(map, 2, 0);
            var request = new ReusablePathRequest(
                start,
                1,
                new DelegatePathPolicy(
                    (cell, context) => true,
                    (cell, context) => true,
                    null));
            Assert.That(request.TryAddTarget(firstTarget), Is.True);

            var pathfinder = new HexPathfinder(map, new PathSearchWorkspace(map));
            var result = new PathResult(new List<int>(map.Count));

            pathfinder.FindPath(request, result);
            AssertSuccess(result, firstTarget, 1);

            request.ClearTargets();
            Assert.That(request.TryAddTarget(secondTarget), Is.True);
            pathfinder.FindPath(request, result);
            AssertSuccess(result, secondTarget, 2);
        }

        [Test]
        public void ResultCapacityFailureClearsTheOutputPath()
        {
            var map = new HexMap(new HexMapDefinition(1));
            var start = CellAt(map, 0, 0);
            var target = CellAt(map, 1, 0);
            var request = new ReusablePathRequest(
                start,
                1,
                new DelegatePathPolicy(
                    (cell, context) => true,
                    (cell, context) => true,
                    null));
            Assert.That(request.TryAddTarget(target), Is.True);

            var pathfinder = new HexPathfinder(map, new PathSearchWorkspace(map));
            var result = new PathResult(new List<int>(0));

            pathfinder.FindPath(request, result);

            AssertFailure(
                result,
                PathResultStatus.InvalidInput,
                PathFailureReason.ResultCapacityExceeded);
            Assert.That(result.Cells, Is.Empty);
            Assert.That(result.Cost, Is.EqualTo(0));
            Assert.That(result.ReachedTarget, Is.EqualTo(default(HexCell)));
        }

        private static PathRequest CreateRequest(
            HexCell start,
            HexCell[] targets,
            object context,
            Func<HexCell, object, bool> canPass,
            Func<HexCell, object, bool> canEnter)
        {
            return new PathRequest(
                start,
                targets,
                new DelegatePathPolicy(canPass, canEnter, context));
        }

        private static PathResult FindPath(HexMap map, PathRequest request)
        {
            var pathfinder = new HexPathfinder(map, new PathSearchWorkspace(map));
            var result = new PathResult(new List<int>(map.Count));
            return pathfinder.FindPath(request, result);
        }

        private static HexCell CellAt(HexMap map, int q, int r)
        {
            return map.Query(new HexCoord(q, r)).Cell;
        }

        private static void AssertSuccess(PathResult result, HexCell target, int cost)
        {
            Assert.That(result.Status, Is.EqualTo(PathResultStatus.Success));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ReachedTarget, Is.EqualTo(target));
            Assert.That(result.Cost, Is.EqualTo(cost));
            Assert.That(result.Cells.Count, Is.EqualTo(cost + 1));
            Assert.That(result.Reason, Is.EqualTo(PathFailureReason.None));
        }

        private static void AssertFailure(
            PathResult result,
            PathResultStatus status,
            PathFailureReason reason)
        {
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Reason, Is.EqualTo(reason));
        }

        private static void AssertCoordinates(PathResult result, HexMap map, HexCoord[] expected)
        {
            Assert.That(result.Cells.Count, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                var query = map.Query(result.Cells[index]);
                Assert.That(query.Cell.Coordinate, Is.EqualTo(expected[index]));
            }
        }

        private sealed class DelegatePathPolicy : IHexPathPolicy
        {
            private readonly Func<HexCell, object, bool> m_CanPass;
            private readonly Func<HexCell, object, bool> m_CanEnter;
            private readonly object m_Context;

            public DelegatePathPolicy(
                Func<HexCell, object, bool> canPass,
                Func<HexCell, object, bool> canEnter,
                object context)
            {
                m_CanPass = canPass;
                m_CanEnter = canEnter;
                m_Context = context;
            }

            public bool CanPass(HexCell cell)
            {
                return m_CanPass(cell, m_Context);
            }

            public bool CanEnter(HexCell cell)
            {
                return m_CanEnter(cell, m_Context);
            }
        }
    }
}

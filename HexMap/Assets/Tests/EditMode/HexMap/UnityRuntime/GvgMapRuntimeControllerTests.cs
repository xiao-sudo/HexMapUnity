using System.Collections.Generic;
using System.Text.RegularExpressions;
using HexMap.Gvg;
using HexMap.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexMap.UnityRuntime.Tests
{
    [TestFixture]
    public sealed class GvgMapRuntimeControllerTests
    {
        private GameObject m_MapObject;

        [TearDown]
        public void TearDown()
        {
            if (m_MapObject != null)
            {
                Object.DestroyImmediate(m_MapObject);
            }
        }

        [Test]
        public void InitializesQueriesUpdatesOwnershipAndFindsPathsThroughItsPublicApi()
        {
            var controller = CreateController();
            var rows = CreateRows(controller.HexMapView.Map, 10);

            Assert.That(controller.TryInitialize(rows), Is.True);
            Assert.That(controller.IsInitialized, Is.True);

            var startId = controller.HexMapView.Map.Query(0).Cell.Id;
            var targetId = controller.HexMapView.Map.Query(1).Cell.Id;
            Plot plot;
            Assert.That(controller.TryGetPlot(startId, out plot), Is.True);
            Assert.That(plot.OwnerFactionId, Is.EqualTo(10));
            Assert.That(controller.TrySetPlotOwnerFactionId(startId, -100), Is.True);
            Assert.That(plot.OwnerFactionId, Is.EqualTo(-100));

            var result = new PathResult(new List<HexCell>(controller.HexMapView.Map.Count));
            Assert.That(controller.TryFindPlotPath(startId, targetId, 10, result), Is.True);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Count, Is.EqualTo(2));
        }

        [Test]
        public void FailedReinitializationPreservesThePreviousRuntimeState()
        {
            var controller = CreateController();
            var rows = CreateRows(controller.HexMapView.Map, 10);
            Assert.That(controller.TryInitialize(rows), Is.True);

            var plotId = controller.HexMapView.Map.Query(0).Cell.Id;
            Assert.That(controller.TrySetPlotOwnerFactionId(plotId, 20), Is.True);
            LogAssert.Expect(LogType.Error, new Regex("failed to initialize Plot snapshot"));
            Assert.That(controller.TryInitialize(new[]
            {
                new GvgPlotRuntimeData(999, new[] { 999999 }, PlotType.Normal)
            }), Is.False);

            Plot plot;
            Assert.That(controller.TryGetPlot(plotId, out plot), Is.True);
            Assert.That(plot.OwnerFactionId, Is.EqualTo(20));
        }

        [Test]
        public void PathFailuresAreReportedInTheCallerProvidedResultWithoutLogging()
        {
            var controller = CreateController();
            var result = new PathResult(new List<HexCell>(1));

            Assert.That(controller.TryFindPlotPath(1, 2, 10, result), Is.False);
            Assert.That(result.Reason, Is.EqualTo(PathFailureReason.MapNotInitialized));

            Assert.That(controller.TryInitialize(CreateRows(controller.HexMapView.Map, 10)), Is.True);
            Assert.That(controller.TryFindPlotPath(999, 1000, 10, result), Is.False);
            Assert.That(result.Reason, Is.EqualTo(PathFailureReason.PlotNotFound));
        }

        [Test]
        public void MissingHexMapViewLogsAnErrorAndCannotInitialize()
        {
            m_MapObject = new GameObject("GVG Map Runtime Controller");
            var controller = m_MapObject.AddComponent<GvgMapRuntimeController>();

            LogAssert.Expect(LogType.Error, new Regex("requires a HexMapView reference"));
            controller.HexMapView = null;

            Assert.That(controller.IsInitialized, Is.False);
            Assert.That(controller.TryInitialize(new List<GvgPlotRuntimeData>()), Is.False);
        }

        private GvgMapRuntimeController CreateController()
        {
            m_MapObject = new GameObject("GVG Map Runtime Controller");
            m_MapObject.SetActive(false);
            var mapView = m_MapObject.AddComponent<HexMapView>();
            mapView.Radius = 1;
            var controller = m_MapObject.AddComponent<GvgMapRuntimeController>();
            controller.HexMapView = mapView;
            m_MapObject.SetActive(true);
            return controller;
        }

        private static List<GvgPlotRuntimeData> CreateRows(Runtime.HexMap map, int ownerFactionId)
        {
            var rows = new List<GvgPlotRuntimeData>(map.Count);
            for (var index = 0; index < map.Cells.Count; index++)
            {
                var cell = map.Cells[index];
                rows.Add(new GvgPlotRuntimeData(
                    cell.Id,
                    new[] { cell.Id },
                    PlotType.Normal,
                    0,
                    0,
                    -1,
                    -1,
                    ownerFactionId));
            }

            return rows;
        }
    }
}
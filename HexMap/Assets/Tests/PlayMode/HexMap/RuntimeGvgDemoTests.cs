using HexMap.Core;
using HexMap.Gvg;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.UnityRuntime.Tests
{
    /// <summary>
    /// PlayMode coverage for the RuntimeGvgDemo editor runtime test environment
    /// (issue 05): map generation, plot composition and GVG pathfinding end to end.
    /// </summary>
    [TestFixture]
    public sealed class RuntimeGvgDemoTests
    {
        private GameObject m_Root;

        [TearDown]
        public void TearDown()
        {
            if (m_Root != null)
            {
                Object.DestroyImmediate(m_Root);
                m_Root = null;
            }
        }

        [Test]
        public void DemoBuildsMapComposesTableAndFindsAPath()
        {
            var demo = CreateDemo();

            Assert.That(demo.IsReady, Is.True, demo.Error);
            Assert.That(demo.Registry, Is.Not.Null);
            Assert.That(demo.Registry.Count, Is.GreaterThan(0));
            Assert.That(demo.StartPlotId, Is.GreaterThanOrEqualTo(0));
            Assert.That(demo.TargetPlotId, Is.GreaterThanOrEqualTo(0));

            demo.RunPathfinding();

            Assert.That(demo.LastPath, Is.Not.Null);
            Assert.That(demo.LastPath.IsSuccess, Is.True, "Path failed: " + demo.LastPath.Reason);
            Assert.That(demo.LastPath.Cost, Is.GreaterThan(0));
        }

        [Test]
        public void DemoPathAvoidsBlockedPlots()
        {
            var demo = CreateDemo();
            demo.RunPathfinding();

            Assert.That(demo.LastPath.IsSuccess, Is.True, "Path failed: " + demo.LastPath.Reason);

            var cells = demo.LastPath.Cells;
            for (var index = 0; index < cells.Count; index++)
            {
                Plot plot;
                Assert.That(
                    demo.Registry.TryGetPlotForCell(cells[index].Id, out plot),
                    Is.True,
                    "Path cell " + cells[index].Id + " has no open plot.");
                Assert.That(plot.BlockingState, Is.EqualTo(BlockingState.Passable));
                Assert.That(plot.IsOpenForPathfinding, Is.True);
            }
        }

        [Test]
        public void DemoGeneratedTableExposesBlockedAndNotOpenPlots()
        {
            var demo = CreateDemo();

            var hasBlocked = false;
            var hasNotOpen = false;
            var plots = demo.Registry.Plots;
            for (var index = 0; index < plots.Count; index++)
            {
                if (plots[index].BlockingState == BlockingState.Blocked)
                {
                    hasBlocked = true;
                }

                if (!plots[index].IsOpenForPathfinding)
                {
                    hasNotOpen = true;
                }
            }

            Assert.That(hasBlocked, Is.True, "The generated demo table should contain blocked plots.");
            Assert.That(hasNotOpen, Is.True, "The generated demo table should contain NotOpen plots.");
        }

        [Test]
        public void DemoCanSwitchStartAndTargetCandidates()
        {
            var demo = CreateDemo();
            var originalStart = demo.StartPlotId;

            demo.CycleStart(1);
            Assert.That(demo.StartPlotId, Is.Not.EqualTo(originalStart));
            demo.CycleStart(-1);
            Assert.That(demo.StartPlotId, Is.EqualTo(originalStart));

            demo.RunPathfinding();
            Assert.That(demo.LastPath.IsSuccess, Is.True);
        }

        private RuntimeGvgDemo CreateDemo()
        {
            m_Root = new GameObject("Runtime GVG Demo Test");
            var view = m_Root.AddComponent<HexMapView>();
            view.Radius = 3;
            view.Plane = HexPlane.XZ;
            view.Build();

            var demo = m_Root.AddComponent<RuntimeGvgDemo>();
            demo.MapView = view;
            demo.UseAuthoringAssetData = false;
            demo.BuildDemo();
            return demo;
        }
    }
}
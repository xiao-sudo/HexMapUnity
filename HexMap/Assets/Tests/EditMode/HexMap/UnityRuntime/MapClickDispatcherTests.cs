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
    public sealed class MapClickDispatcherTests
    {
        /// <summary>
        /// The channel the application would declare for its gameplay mode. The dispatcher itself has no
        /// opinion about it, which is the point.
        /// </summary>
        private const int GameplayChannel = 1;

        private const int TopDownChannel = 2;

        private GameObject m_MapObject;
        private GameObject m_CameraObject;
        private GameObject m_DispatcherObject;
        private GameObject m_HandlerObject;

        [TearDown]
        public void TearDown()
        {
            Destroy(ref m_HandlerObject);
            Destroy(ref m_DispatcherObject);
            Destroy(ref m_CameraObject);
            Destroy(ref m_MapObject);
        }

        private void Destroy(ref GameObject target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }

            target = null;
        }

        /// <summary>
        /// A camera looking straight down at the middle of the map. Its position is fixed on purpose:
        /// a click arrives in screen space and is unprojected back to the plane, so the two round-trip.
        /// Moving this camera would not move where a click lands, only which part of the map the screen
        /// covers. Tests that need a click to land somewhere specific aim through
        /// <see cref="ScreenPointOf"/> instead.
        /// </summary>
        private Camera CreateCamera()
        {
            m_CameraObject = new GameObject("Map Click Camera");
            var camera = m_CameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 9f / 16f;
            camera.transform.position = new Vector3(0f, 30f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return camera;
        }

        private GvgMapRuntimeController CreateController(int radius)
        {
            m_MapObject = new GameObject("GVG Map Runtime Controller");
            m_MapObject.SetActive(false);
            var mapView = m_MapObject.AddComponent<HexMapView>();
            mapView.Radius = radius;
            var controller = m_MapObject.AddComponent<GvgMapRuntimeController>();
            controller.HexMapView = mapView;
            m_MapObject.SetActive(true);
            return controller;
        }

        private MapClickDispatcher CreateDispatcher(GvgMapRuntimeController controller, Camera camera, int channel)
        {
            m_DispatcherObject = new GameObject("Map Click Dispatcher");
            m_DispatcherObject.SetActive(false);
            var dispatcher = m_DispatcherObject.AddComponent<MapClickDispatcher>();
            dispatcher.Controller = controller;
            m_DispatcherObject.SetActive(true);
            dispatcher.SetActiveCamera(camera);
            dispatcher.SetActiveChannel(channel);
            return dispatcher;
        }

        /// <summary>
        /// One plot per cell, so any click inside the map resolves to a plot.
        /// </summary>
        private static List<GvgPlotRuntimeData> CreateOnePlotPerCell(Runtime.HexMap map)
        {
            var rows = new List<GvgPlotRuntimeData>(map.Count);
            foreach (var cell in map.Cells)
            {
                rows.Add(new GvgPlotRuntimeData(cell.Id, new[] { cell.Id }, PlotType.Normal, 0, 0, -1, -1));
            }

            return rows;
        }

        private MapClickDispatcher CreateReadyDispatcher(
            out GvgMapRuntimeController controller,
            out Camera camera,
            out RecordingHandler handler)
        {
            controller = CreateController(9);
            Assert.That(
                controller.TryInitialize(CreateOnePlotPerCell(controller.HexMapView.Map)),
                Is.True);
            camera = CreateCamera();
            var dispatcher = CreateDispatcher(controller, camera, GameplayChannel);
            handler = new RecordingHandler();
            Assert.That(dispatcher.Register(GameplayChannel, handler), Is.True);
            return dispatcher;
        }

        [Test]
        public void TheRegisteredHandlerForTheActiveChannelReceivesTheClick()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);

            var consumed = dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero));

            Assert.That(consumed, Is.True);
            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(handler.LastContext.HasPlot, Is.True);
            Assert.That(handler.LastContext.PlotId, Is.GreaterThanOrEqualTo(0));
            Assert.That(handler.LastContext.PickStatus, Is.EqualTo(PlotScreenPickStatus.Found));
        }

        [Test]
        public void SwitchingTheChannelSwitchesWhichHandlerReacts()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler gameplay;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out gameplay);
            var topDown = new RecordingHandler();
            Assert.That(dispatcher.Register(TopDownChannel, topDown), Is.True);

            dispatcher.SetActiveChannel(TopDownChannel);
            dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero));

            Assert.That(gameplay.CallCount, Is.EqualTo(0));
            Assert.That(topDown.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void AClickOnAnUnregisteredChannelIsReportedOnceAndDropped()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler gameplay;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out gameplay);
            dispatcher.SetActiveChannel(TopDownChannel);

            // The drop is deliberately logged, so the framework has to be told to expect it. Repeated
            // clicks report once, which is the second half of what this test pins.
            LogAssert.Expect(
                LogType.Error,
                new Regex(Regex.Escape(NoHandlerMessage(TopDownChannel))));

            Assert.That(dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)), Is.False);
            Assert.That(dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)), Is.False);
            Assert.That(gameplay.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void NoChannelActiveDropsTheClickSilently()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);
            dispatcher.SetActiveChannel(MapClickChannels.None);

            Assert.That(dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)), Is.False);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void WithoutACameraTheClickIsDropped()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);
            dispatcher.SetActiveCamera(null);

            // A missing camera is reported rather than silently dropping the click.
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(NoCameraMessage())));

            Assert.That(dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)), Is.False);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void WithoutAControllerTheClickIsDropped()
        {
            var camera = CreateCamera();
            var dispatcher = CreateDispatcher(null, camera, GameplayChannel);
            var handler = new RecordingHandler();
            Assert.That(dispatcher.Register(GameplayChannel, handler), Is.True);

            // A dispatcher with nothing to pick against is reported, not silently dropped. This is
            // missing scene wiring, so it has to be visible in the console.
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(NoControllerMessage())));

            Assert.That(dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)), Is.False);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void AClickThatLandsOutsideTheMapIsStillDeliveredWithNoPlot()
        {
            // Zoom out until the map no longer fills the viewport, so a click can land on screen past
            // its edge. The ray still meets the plane, and the pick gets far enough to see that no cell
            // is out there: that is the OutsideMap reason, as opposed to a ray that never reaches the
            // plane at all. Clicking off screen would test the same path on a shakier assumption.
            var camera = CreateCamera();
            camera.orthographicSize = 25f;
            var controller = CreateController(9);
            Assert.That(
                controller.TryInitialize(CreateOnePlotPerCell(controller.HexMapView.Map)),
                Is.True);
            var dispatcher = CreateDispatcher(controller, camera, GameplayChannel);
            var handler = new RecordingHandler();
            Assert.That(dispatcher.Register(GameplayChannel, handler), Is.True);

            // Past the map's edge in depth: the radius-9 map reaches about 14.5 world units, and the
            // zoomed-out viewport reaches 25, so 20 is outside the map and well inside the screen.
            var consumed = dispatcher.OnMapClicked(ScreenPointOf(camera, new Vector3(0f, 0f, 20f)));

            Assert.That(consumed, Is.True, "clicking empty space must reach the handler so it can dismiss");
            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(handler.LastContext.HasPlot, Is.False);
            Assert.That(handler.LastContext.PlotId, Is.EqualTo(-1));
            Assert.That(handler.LastContext.PickStatus, Is.EqualTo(PlotScreenPickStatus.OutsideMap));
        }

        [Test]
        public void AClickThatMissesTheMapPlaneIsDeliveredWithItsOwnReason()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);

            // The camera looks up and away from the plane, so the ray never meets it.
            camera.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var consumed = dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero));

            Assert.That(consumed, Is.True);
            Assert.That(handler.LastContext.HasPlot, Is.False);
            Assert.That(handler.LastContext.PickStatus, Is.EqualTo(PlotScreenPickStatus.NoPlaneIntersection));
        }

        [Test]
        public void TheOriginalScreenPositionIsCarriedIntoTheContext()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);
            var position = ScreenPointOf(camera, Vector3.zero);

            dispatcher.OnMapClicked(position);

            Assert.That(handler.LastContext.ScreenPosition, Is.EqualTo(position));
        }

        [Test]
        public void TheHandlerCanRefuseTheClick()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);
            handler.Consume = false;

            Assert.That(dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)), Is.False);
            Assert.That(handler.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void AReturnedFalseFromOneHandlerStillLeavesTheChannelIntact()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);
            handler.Consume = false;

            dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero));
            dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero));

            Assert.That(handler.CallCount, Is.EqualTo(2), "a refusal must not unregister the handler");
        }

        [Test]
        public void RegisteringASecondHandlerForTheSameChannelIsRefused()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler first;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out first);
            var second = new RecordingHandler();

            // Two handlers on one channel is a wiring mistake: the refusal is reported rather than
            // quietly ignored, which is what makes it findable when it happens at runtime.
            LogAssert.Expect(
                LogType.Error,
                new Regex(Regex.Escape(DuplicateHandlerMessage(GameplayChannel))));

            Assert.That(dispatcher.Register(GameplayChannel, second), Is.False);
            Assert.That(dispatcher.HandlerCount, Is.EqualTo(1));

            dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero));
            Assert.That(first.CallCount, Is.EqualTo(1), "the original handler keeps the channel");
            Assert.That(second.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void RegisteringOnTheNoneChannelOrWithANullHandlerIsRefused()
        {
            var camera = CreateCamera();
            var dispatcher = CreateDispatcher(null, camera, MapClickChannels.None);

            // Both refusals are reported, in the order the two calls make them. The declaration order
            // here has to follow the call order, because each message is a distinct report.
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(NoneChannelMessage())));
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(NullHandlerMessage())));

            Assert.That(dispatcher.Register(MapClickChannels.None, new RecordingHandler()), Is.False);
            Assert.That(dispatcher.Register(GameplayChannel, null), Is.False);
            Assert.That(dispatcher.HandlerCount, Is.EqualTo(0));
        }

        [Test]
        public void UnregisteringIsIdempotentAndIgnoresForeignHandlers()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);
            var stranger = new RecordingHandler();

            Assert.That(dispatcher.Unregister(GameplayChannel, stranger), Is.False, "a different handler must not free the channel");
            Assert.That(dispatcher.Unregister(TopDownChannel, handler), Is.False, "an unregistered channel changes nothing");
            Assert.That(dispatcher.Unregister(GameplayChannel, null), Is.False);
            Assert.That(dispatcher.HandlerCount, Is.EqualTo(1));

            Assert.That(dispatcher.Unregister(GameplayChannel, handler), Is.True);
            Assert.That(dispatcher.Unregister(GameplayChannel, handler), Is.False, "a second unregister is a no-op");
            Assert.That(dispatcher.HandlerCount, Is.EqualTo(0));

            Assert.That(dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)), Is.False);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void ARegisteredChannelCanBeReusedAfterUnregistering()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler first;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out first);
            Assert.That(dispatcher.Unregister(GameplayChannel, first), Is.True);

            var replacement = new RecordingHandler();
            Assert.That(dispatcher.Register(GameplayChannel, replacement), Is.True);

            dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero));
            Assert.That(replacement.CallCount, Is.EqualTo(1));
            Assert.That(first.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void ADestroyedHandlerIsSkippedInsteadOfThrowing()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler registered;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out registered);

            // A handler destroyed without unregistering is the case a forgotten OnDisable leaves behind.
            m_HandlerObject = new GameObject("Destroyed Handler");
            var doomed = m_HandlerObject.AddComponent<UnityHandler>();
            Assert.That(dispatcher.Register(TopDownChannel, doomed), Is.True);
            Object.DestroyImmediate(m_HandlerObject);
            m_HandlerObject = null;

            dispatcher.SetActiveChannel(TopDownChannel);

            // One report is expected for the two clicks below, not two: the first reports the dead
            // handler and the second is deduplicated, because repeating the same complaint on every
            // click would bury the console. Both clicks still fail, and neither throws.
            LogAssert.Expect(
                LogType.Error,
                new Regex(Regex.Escape(DeadHandlerMessage(TopDownChannel))));

            Assert.DoesNotThrow(() => dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)));
            Assert.That(dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)), Is.False);
        }

        [Test]
        public void TheLastContextExplainsWhatTheClickResolvedTo()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);

            MapClickContext context;
            Assert.That(dispatcher.TryGetLastContext(out context), Is.False, "nothing has been clicked yet");

            dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero));
            Assert.That(dispatcher.TryGetLastContext(out context), Is.True);
            Assert.That(context.HasPlot, Is.True);

            dispatcher.SetActiveChannel(MapClickChannels.None);
            dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero));
            Assert.That(
                dispatcher.TryGetLastContext(out context),
                Is.False,
                "a click dropped before picking must not leave a stale context behind");
        }

        [Test]
        public void AMapThatIsNotInitialisedReportsThatReasonWithoutThrowing()
        {
            // The controller builds the runtime map in Awake but the plot registry waits for
            // TryInitialize, so clicks arrive before plots exist.
            var controller = CreateController(9);
            var camera = CreateCamera();
            var dispatcher = CreateDispatcher(controller, camera, GameplayChannel);
            var handler = new RecordingHandler();
            Assert.That(dispatcher.Register(GameplayChannel, handler), Is.True);

            Assert.That(dispatcher.OnMapClicked(ScreenPointOf(camera, Vector3.zero)), Is.True, "the click is still delivered");
            Assert.That(handler.LastContext.HasPlot, Is.False);
            Assert.That(handler.LastContext.PickStatus, Is.EqualTo(PlotScreenPickStatus.MapNotInitialized));
        }

        /// <summary>
        /// Where a world point appears on screen, which is also the screen position a click has to use
        /// to land on that world point. Aiming through this keeps a test stating the map location it
        /// means rather than a screen coordinate that only happens to name it.
        /// </summary>
        private static Vector2 ScreenPointOf(Camera camera, Vector3 worldPoint)
        {
            return camera.WorldToScreenPoint(worldPoint);
        }

        /// <summary>
        /// Mirrors the messages the dispatcher logs, so an expectation stays in step with the source
        /// instead of drifting into a substring that happens to still match.
        /// </summary>
        private static string NoHandlerMessage(int channel)
        {
            return "MapClickDispatcher has no handler registered for channel " + channel
                + "; the click was dropped.";
        }

        private static string DeadHandlerMessage(int channel)
        {
            return "MapClickDispatcher still has a destroyed handler for channel " + channel
                + "; unregister it in OnDisable.";
        }

        private static string NoCameraMessage()
        {
            return "MapClickDispatcher has no active camera; the mode owner must call SetActiveCamera before clicks arrive.";
        }

        private static string NoControllerMessage()
        {
            return "MapClickDispatcher has no GvgMapRuntimeController, so clicks cannot be resolved.";
        }

        private static string DuplicateHandlerMessage(int channel)
        {
            return "MapClickDispatcher already has a handler for channel " + channel
                + "; unregister the previous one first. The new handler was ignored.";
        }

        private static string NoneChannelMessage()
        {
            return "MapClickDispatcher.Register was given the None channel; pick an application channel.";
        }

        private static string NullHandlerMessage()
        {
            return "MapClickDispatcher.Register was given a null handler.";
        }

        private sealed class RecordingHandler : IMapClickHandler
        {
            public int CallCount { get; private set; }

            public MapClickContext LastContext { get; private set; }

            public bool Consume { get; set; } = true;

            public bool OnMapClicked(in MapClickContext context)
            {
                CallCount++;
                LastContext = context;
                return Consume;
            }
        }

        /// <summary>
        /// A handler that is also a Unity object, so destroying it is observable the way a real
        /// MonoBehaviour handler would be.
        /// </summary>
        private sealed class UnityHandler : MonoBehaviour, IMapClickHandler
        {
            public bool OnMapClicked(in MapClickContext context)
            {
                return true;
            }
        }
    }
}

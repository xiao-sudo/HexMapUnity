using System.Collections.Generic;
using HexMap.Gvg;
using HexMap.Runtime;
using NUnit.Framework;
using UnityEngine;

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

        private static void Destroy(ref GameObject target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }

            target = null;
        }

        private static Camera CreateCamera(Quaternion? rotation = null)
        {
            m_CameraObject = new GameObject("Map Click Camera");
            var camera = m_CameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 9f / 16f;
            camera.transform.position = new Vector3(0f, 30f, 0f);
            camera.transform.rotation = rotation ?? Quaternion.Euler(90f, 0f, 0f);
            return camera;
        }

        private static GvgMapRuntimeController CreateController(int radius)
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

        private static MapClickDispatcher CreateDispatcher(GvgMapRuntimeController controller, Camera camera, int channel)
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

        private static MapClickDispatcher CreateReadyDispatcher(
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

            var consumed = dispatcher.OnMapClicked(ScreenCentre(camera));

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
            dispatcher.OnMapClicked(ScreenCentre(camera));

            Assert.That(gameplay.CallCount, Is.EqualTo(0));
            Assert.That(topDown.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void NoChannelActiveDropsTheClickSilently()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);
            dispatcher.SetActiveChannel(MapClickChannels.None);

            Assert.That(dispatcher.OnMapClicked(ScreenCentre(camera)), Is.False);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void AChannelWithoutAHandlerDropsTheClick()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);
            dispatcher.SetActiveChannel(TopDownChannel);

            Assert.That(dispatcher.OnMapClicked(ScreenCentre(camera)), Is.False);
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

            Assert.That(dispatcher.OnMapClicked(ScreenCentre(camera)), Is.False);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void WithoutAControllerTheClickIsDropped()
        {
            var camera = CreateCamera();
            var dispatcher = CreateDispatcher(null, camera, GameplayChannel);
            var handler = new RecordingHandler();
            Assert.That(dispatcher.Register(GameplayChannel, handler), Is.True);

            Assert.That(dispatcher.OnMapClicked(ScreenCentre(camera)), Is.False);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void AClickThatLandsOutsideTheMapIsStillDeliveredWithNoPlot()
        {
            // Point the camera back to front so the viewport centre lands on the plane far outside the
            // radius-9 map. The ray still hits the plane, so this exercises the OutsideMap path rather
            // than a ray that misses everything.
            var camera = CreateCamera(Quaternion.Euler(-90f, 0f, 0f));
            var controller = CreateController(9);
            Assert.That(
                controller.TryInitialize(CreateOnePlotPerCell(controller.HexMapView.Map)),
                Is.True);
            var dispatcher = CreateDispatcher(controller, camera, GameplayChannel);
            var handler = new RecordingHandler();
            Assert.That(dispatcher.Register(GameplayChannel, handler), Is.True);

            var consumed = dispatcher.OnMapClicked(ScreenCentre(camera));

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

            // Far outside the viewport, so the ray does not reach the map plane at all.
            var consumed = dispatcher.OnMapClicked(new Vector2(-10000f, -10000f));

            Assert.That(consumed, Is.True);
            Assert.That(handler.LastContext.HasPlot, Is.False);
            Assert.That(handler.LastContext.PickStatus, Is.Not.EqualTo(PlotScreenPickStatus.Found));
        }

        [Test]
        public void TheOriginalScreenPositionIsCarriedIntoTheContext()
        {
            GvgMapRuntimeController controller;
            Camera camera;
            RecordingHandler handler;
            var dispatcher = CreateReadyDispatcher(out controller, out camera, out handler);
            var position = ScreenCentre(camera);

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

            Assert.That(dispatcher.OnMapClicked(ScreenCentre(camera)), Is.False);
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

            dispatcher.OnMapClicked(ScreenCentre(camera));
            dispatcher.OnMapClicked(ScreenCentre(camera));

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

            Assert.That(dispatcher.Register(GameplayChannel, second), Is.False);
            Assert.That(dispatcher.HandlerCount, Is.EqualTo(1));

            dispatcher.OnMapClicked(ScreenCentre(camera));
            Assert.That(first.CallCount, Is.EqualTo(1), "the original handler keeps the channel");
            Assert.That(second.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void RegisteringOnTheNoneChannelOrWithANullHandlerIsRefused()
        {
            var camera = CreateCamera();
            var dispatcher = CreateDispatcher(null, camera, MapClickChannels.None);

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

            Assert.That(dispatcher.OnMapClicked(ScreenCentre(camera)), Is.False);
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

            dispatcher.OnMapClicked(ScreenCentre(camera));
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
            Assert.DoesNotThrow(() => dispatcher.OnMapClicked(ScreenCentre(camera)));
            Assert.That(dispatcher.OnMapClicked(ScreenCentre(camera)), Is.False);
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

            dispatcher.OnMapClicked(ScreenCentre(camera));
            Assert.That(dispatcher.TryGetLastContext(out context), Is.True);
            Assert.That(context.HasPlot, Is.True);

            dispatcher.SetActiveChannel(MapClickChannels.None);
            dispatcher.OnMapClicked(ScreenCentre(camera));
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

            Assert.That(dispatcher.OnMapClicked(ScreenCentre(camera)), Is.True, "the click is still delivered");
            Assert.That(handler.LastContext.HasPlot, Is.False);
            Assert.That(handler.LastContext.PickStatus, Is.EqualTo(PlotScreenPickStatus.MapNotInitialized));
        }

        private static Vector2 ScreenCentre(Camera camera)
        {
            return camera.WorldToScreenPoint(Vector3.zero);
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

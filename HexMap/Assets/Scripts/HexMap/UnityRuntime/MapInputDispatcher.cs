using System.Collections.Generic;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// The single entry point for map input, and the single place that maps the current mode to the
    /// code that reacts to it.
    /// <para>
    /// The outer input layer owns everything that is about the pointer: telling a tap from a drag,
    /// ignoring clicks that land on UI, and reading whichever input backend the project uses. It calls
    /// <see cref="OnMapClicked"/> for clicks, <see cref="OnMapDragged"/> for drags, and
    /// <see cref="OnMapCameraDistanceChanged"/> for camera-distance changes. Picking and context building
    /// happen only for clicks, so no caller has to know which camera is active or what a miss means.
    /// </para>
    /// <para>
    /// Handlers register against a channel id. This assembly has no idea what those channels mean: the
    /// application declares them and a mode owner switches them with <see cref="SetActiveChannel"/>. That
    /// keeps the mode vocabulary, and the mode state itself, out of the runtime library.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapInputDispatcher : MonoBehaviour
    {
        [SerializeField]
        private GvgMapRuntimeController m_Controller;

        private IMapPlotPicker m_PlotPicker;

        private readonly Dictionary<int, IMapInputHandler> m_Handlers = new Dictionary<int, IMapInputHandler>();
        private readonly HashSet<string> m_ReportedProblems = new HashSet<string>();

        private Camera m_ActiveCamera;
        private int m_ActiveChannel = MapInputChannels.None;
        private MapClickContext m_LastContext;
        private bool m_HasLastContext;

        public GvgMapRuntimeController Controller
        {
            get { return m_Controller; }
            set
            {
                m_Controller = value;
                m_PlotPicker = value;
            }
        }

        /// <summary>
        /// Sets the picker used to resolve clicks. The runtime facade owns this wiring; the serialized
        /// controller remains as a scene-migration fallback for existing scenes.
        /// </summary>
        public void SetPlotPicker(IMapPlotPicker picker)
        {
            m_PlotPicker = picker;
        }

        private void Awake()
        {
            if (m_PlotPicker == null)
            {
                m_PlotPicker = m_Controller;
            }
        }

        /// <summary>
        /// The channel map input is currently routed to. <see cref="MapInputChannels.None"/> while a mode
        /// does not react to input.
        /// </summary>
        public int ActiveChannel
        {
            get { return m_ActiveChannel; }
        }

        /// <summary>
        /// Changes whenever the active Map input route changes. An outer input adapter captures this when a
        /// physical gesture starts, so a gesture that spans a mode or camera switch cannot act on the new
        /// route.
        /// </summary>
        public int InputRouteRevision { get; private set; }

        /// <summary>
        /// The camera used to turn a screen position into a map position. Pushed by whoever owns the mode,
        /// so it always matches the camera that is currently rendering.
        /// </summary>
        public Camera ActiveCamera
        {
            get { return m_ActiveCamera; }
        }

        /// <summary>
        /// The number of registered handlers.
        /// </summary>
        public int HandlerCount
        {
            get { return m_Handlers.Count; }
        }

        /// <summary>
        /// The context built by the most recent call to <see cref="OnMapClicked"/>, whatever the outcome.
        /// Reading it explains what the last click resolved to without turning on logging.
        /// </summary>
        public bool TryGetLastContext(out MapClickContext context)
        {
            context = m_LastContext;
            return m_HasLastContext;
        }

        /// <summary>
        /// Routes clicks to a channel. A mode owner calls this in the same step that switches the camera
        /// and the mode UI, so no frame can route a click through the previous mode's handler.
        /// </summary>
        public void SetActiveChannel(int channelId)
        {
            if (m_ActiveChannel == channelId)
            {
                return;
            }

            m_ActiveChannel = channelId;
            InputRouteRevision++;
        }

        /// <summary>
        /// Sets the camera used for picking. Pushed by whoever owns the mode rather than passed in by each
        /// caller, so "which camera is active" has one owner.
        /// </summary>
        public void SetActiveCamera(Camera camera)
        {
            if (ReferenceEquals(m_ActiveCamera, camera))
            {
                return;
            }

            m_ActiveCamera = camera;
            InputRouteRevision++;
        }

        /// <summary>
        /// Registers the handler for a channel. Fails when the channel already has a handler, because two
        /// handlers for one channel would silently steal clicks from each other.
        /// </summary>
        public bool Register(int channelId, IMapInputHandler handler)
        {
            if (handler == null)
            {
                ReportProblem("register-null", "MapInputDispatcher.Register was given a null handler.");
                return false;
            }

            if (channelId == MapInputChannels.None)
            {
                ReportProblem("register-none", "MapInputDispatcher.Register was given the None channel; pick an application channel.");
                return false;
            }

            IMapInputHandler existing;
            if (m_Handlers.TryGetValue(channelId, out existing))
            {
                ReportProblem(
                    "register-duplicate-" + channelId,
                    "MapInputDispatcher already has a handler for channel " + channelId
                        + "; unregister the previous one first. The new handler was ignored.");
                return false;
            }

            m_Handlers.Add(channelId, handler);
            return true;
        }

        /// <summary>
        /// Removes the handler for a channel. Unregistering a channel that holds a different handler, or
        /// none at all, changes nothing and reports nothing, so it is safe from OnDisable.
        /// </summary>
        public bool Unregister(int channelId, IMapInputHandler handler)
        {
            if (handler == null)
            {
                return false;
            }

            IMapInputHandler existing;
            if (!m_Handlers.TryGetValue(channelId, out existing))
            {
                return false;
            }

            if (!ReferenceEquals(existing, handler))
            {
                return false;
            }

            m_Handlers.Remove(channelId);
            return true;
        }

        /// <summary>
        /// Resolves a screen position into a context and hands it to the handler for the active channel.
        /// </summary>
        /// <returns>True when a handler consumed the click.</returns>
        public bool OnMapClicked(Vector2 screenPosition)
        {
            m_LastContext = default(MapClickContext);
            m_HasLastContext = false;

            if (m_ActiveChannel == MapInputChannels.None)
            {
                return false;
            }

            IMapInputHandler handler;
            if (!m_Handlers.TryGetValue(m_ActiveChannel, out handler))
            {
                return false;
            }

            var picker = m_PlotPicker ?? m_Controller;
            if (picker == null)
            {
                ReportProblem("no-picker", "MapInputDispatcher has no map Plot picker, so clicks cannot be resolved.");
                return false;
            }

            if (m_ActiveCamera == null)
            {
                ReportProblem("no-camera", "MapInputDispatcher has no active camera; the mode owner must call SetActiveCamera before clicks arrive.");
                return false;
            }

            var pick = picker.PickPlotAtScreenPosition(screenPosition, m_ActiveCamera);
            m_LastContext = pick.HasPlot
                ? MapClickContext.Hit(screenPosition, pick.PlotId)
                : MapClickContext.Miss(screenPosition, pick.Status);
            m_HasLastContext = true;

            if (!IsAlive(handler))
            {
                ReportProblem(
                    "dead-handler-" + m_ActiveChannel,
                    "MapInputDispatcher still has a destroyed handler for channel " + m_ActiveChannel
                        + "; the Map view runtime must release it during disposal.");
                return false;
            }

            return InvokeHandler(() => handler.OnMapClicked(m_LastContext), "click");
        }

        /// <summary>
        /// Routes a screen-space drag delta and active viewport aspect to the active channel. Drags carry no
        /// map-picking semantics: the handler decides what a delta means for its mode, such as panning a camera.
        /// </summary>
        /// <returns>True when the active handler consumed the drag.</returns>
        public bool OnMapDragged(Vector2 screenDelta)
        {
            if (m_ActiveChannel == MapInputChannels.None)
            {
                return false;
            }

            IMapInputHandler handler;
            if (!m_Handlers.TryGetValue(m_ActiveChannel, out handler))
            {
                return false;
            }

            if (m_ActiveCamera == null)
            {
                ReportProblem("no-camera-drag", "MapInputDispatcher has no active camera, so drags cannot be routed.");
                return false;
            }

            if (!IsAlive(handler))
            {
                ReportProblem(
                    "dead-handler-" + m_ActiveChannel,
                    "MapInputDispatcher still has a destroyed handler for channel " + m_ActiveChannel
                        + "; the Map view runtime must release it during disposal.");
                return false;
            }

            var context = new MapDragContext(screenDelta, m_ActiveCamera.aspect);
            return InvokeHandler(() => handler.OnMapDragged(context), "drag");
        }

        /// <summary>
        /// Routes a device-independent camera-distance intent and active viewport aspect to the active channel.
        /// Like drags, camera-distance changes do not need map picking dependencies: the active handler owns
        /// their camera meaning and reports whether it consumed them.
        /// </summary>
        /// <returns>True when the active handler consumed the camera-distance intent.</returns>
        public bool OnMapCameraDistanceChanged(in MapCameraDistanceContext context)
        {
            if (m_ActiveChannel == MapInputChannels.None)
            {
                return false;
            }

            IMapInputHandler handler;
            if (!m_Handlers.TryGetValue(m_ActiveChannel, out handler))
            {
                return false;
            }

            if (m_ActiveCamera == null)
            {
                ReportProblem(
                    "no-camera-distance",
                    "MapInputDispatcher has no active camera, so camera-distance changes cannot be routed.");
                return false;
            }

            if (!IsAlive(handler))
            {
                ReportProblem(
                    "dead-handler-" + m_ActiveChannel,
                    "MapInputDispatcher still has a destroyed handler for channel " + m_ActiveChannel
                        + "; the Map view runtime must release it during disposal.");
                return false;
            }

            var handledContext = new MapCameraDistanceContext(
                context.ScreenAnchor,
                context.NormalizedDistanceDelta,
                m_ActiveCamera.aspect);
            return InvokeHandler(
                () => handler.OnMapCameraDistanceChanged(handledContext),
                "camera-distance change");
        }

        private bool InvokeHandler(System.Func<bool> invoke, string inputName)
        {
            try
            {
                return invoke();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(
                    new System.InvalidOperationException(
                        "MapInputDispatcher handler threw while processing a " + inputName + ".",
                        exception),
                    this);
                return false;
            }
        }


        private static bool IsAlive(IMapInputHandler handler)
        {
            // The interface itself can be a genuine null.
            if (handler == null)
            {
                return false;
            }

            // A handler that is not a Unity object cannot have been destroyed, so it is alive. Only a
            // destroyed Unity object needs the second test, and that test must go through Unity's
            // overloaded == because a destroyed object still has a non-null reference. Never merge these
            // two checks into one expression: Unity's bool conversion would make the result always true.
            var unityObject = handler as Object;
            if (ReferenceEquals(unityObject, null))
            {
                return true;
            }

            return unityObject != null;
        }

        private void ReportProblem(string key, string message)
        {
            // One report per distinct problem: this runs on every click, so repeating it would bury the
            // console and slow the game down.
            if (!m_ReportedProblems.Add(key))
            {
                return;
            }

            Debug.LogError(message, this);
        }
    }
}

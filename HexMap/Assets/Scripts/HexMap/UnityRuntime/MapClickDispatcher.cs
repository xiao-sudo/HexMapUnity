using System.Collections.Generic;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// The single entry point for map clicks, and the single place that maps the current mode to the
    /// code that reacts to it.
    /// <para>
    /// The outer input layer owns everything that is about the pointer: telling a tap from a drag,
    /// ignoring clicks that land on UI, and reading whichever input backend the project uses. It calls
    /// <see cref="OnMapClicked"/> with a screen position and nothing else. Picking, context building and
    /// dispatch all happen here, so no caller has to know which camera is active or what a miss means.
    /// </para>
    /// <para>
    /// Handlers register against a channel id. This assembly has no idea what those channels mean: the
    /// application declares them and a mode owner switches them with <see cref="SetActiveChannel"/>. That
    /// keeps the mode vocabulary, and the mode state itself, out of the runtime library.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapClickDispatcher : MonoBehaviour
    {
        [SerializeField]
        private GvgMapRuntimeController m_Controller;

        private readonly Dictionary<int, IMapClickHandler> m_Handlers = new Dictionary<int, IMapClickHandler>();
        private readonly HashSet<string> m_ReportedProblems = new HashSet<string>();

        private Camera m_ActiveCamera;
        private int m_ActiveChannel = MapClickChannels.None;
        private MapClickContext m_LastContext;
        private bool m_HasLastContext;

        public GvgMapRuntimeController Controller
        {
            get { return m_Controller; }
            set { m_Controller = value; }
        }

        /// <summary>
        /// The channel clicks are currently routed to. <see cref="MapClickChannels.None"/> while a mode
        /// does not react to clicks.
        /// </summary>
        public int ActiveChannel
        {
            get { return m_ActiveChannel; }
        }

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
            m_ActiveChannel = channelId;
        }

        /// <summary>
        /// Sets the camera used for picking. Pushed by whoever owns the mode rather than passed in by each
        /// caller, so "which camera is active" has one owner.
        /// </summary>
        public void SetActiveCamera(Camera camera)
        {
            m_ActiveCamera = camera;
        }

        /// <summary>
        /// Registers the handler for a channel. Fails when the channel already has a handler, because two
        /// handlers for one channel would silently steal clicks from each other.
        /// </summary>
        public bool Register(int channelId, IMapClickHandler handler)
        {
            if (handler == null)
            {
                ReportProblem("register-null", "MapClickDispatcher.Register was given a null handler.");
                return false;
            }

            if (channelId == MapClickChannels.None)
            {
                ReportProblem("register-none", "MapClickDispatcher.Register was given the None channel; pick an application channel.");
                return false;
            }

            IMapClickHandler existing;
            if (m_Handlers.TryGetValue(channelId, out existing))
            {
                ReportProblem(
                    "register-duplicate-" + channelId,
                    "MapClickDispatcher already has a handler for channel " + channelId
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
        public bool Unregister(int channelId, IMapClickHandler handler)
        {
            if (handler == null)
            {
                return false;
            }

            IMapClickHandler existing;
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

            if (m_Controller == null)
            {
                ReportProblem("no-controller", "MapClickDispatcher has no GvgMapRuntimeController, so clicks cannot be resolved.");
                return false;
            }

            if (m_ActiveCamera == null)
            {
                ReportProblem("no-camera", "MapClickDispatcher has no active camera; the mode owner must call SetActiveCamera before clicks arrive.");
                return false;
            }

            if (m_ActiveChannel == MapClickChannels.None)
            {
                return false;
            }

            IMapClickHandler handler;
            if (!m_Handlers.TryGetValue(m_ActiveChannel, out handler))
            {
                ReportProblem(
                    "no-handler-" + m_ActiveChannel,
                    "MapClickDispatcher has no handler registered for channel " + m_ActiveChannel
                        + "; the click was dropped.");
                return false;
            }

            var pick = m_Controller.PickPlotAtScreenPosition(screenPosition, m_ActiveCamera);
            m_LastContext = pick.HasPlot
                ? MapClickContext.Hit(screenPosition, pick.PlotId)
                : MapClickContext.Miss(screenPosition, pick.Status);
            m_HasLastContext = true;

            if (!IsAlive(handler))
            {
                ReportProblem(
                    "dead-handler-" + m_ActiveChannel,
                    "MapClickDispatcher still has a destroyed handler for channel " + m_ActiveChannel
                        + "; unregister it in OnDisable.");
                return false;
            }

            return handler.OnMapClicked(m_LastContext);
        }

        private static bool IsAlive(IMapClickHandler handler)
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

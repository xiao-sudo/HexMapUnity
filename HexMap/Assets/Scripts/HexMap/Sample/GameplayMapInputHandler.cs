using System;
using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Reacts to map clicks while the player is in normal gameplay: it selects the clicked plot and
    /// announces which panel should open.
    /// <para>
    /// The panel itself is not this component's business. It publishes an event, and the gameplay UI
    /// subscribes while it is visible. That keeps the click path free of any UI type, which is what lets
    /// the other mode answer the same click with entirely different UI.
    /// </para>
    /// </summary>
    public sealed class GameplayMapInputHandler : MonoBehaviour, IMapInputHandler, IMapPlotClickSource
    {
        [SerializeField]
        private MapInputDispatcher m_Dispatcher;

        [SerializeField]
        private GvgMapRuntimeController m_Controller;

        /// <summary>
        /// Raised for every delivered click. PlotId is -1 when the click hit nothing, which is how the
        /// UI learns to close.
        /// </summary>
        public event Action<int> PlotClicked;

        public MapInputDispatcher Dispatcher
        {
            get { return m_Dispatcher; }
            set { m_Dispatcher = value; }
        }

        public GvgMapRuntimeController Controller
        {
            get { return m_Controller; }
            set { m_Controller = value; }
        }

        private void OnEnable()
        {
            RegisterIfPossible();
        }

        private void OnDisable()
        {
            // Unregistering is idempotent on the dispatcher, so this is safe even if OnEnable never ran.
            if (m_Dispatcher != null)
            {
                m_Dispatcher.Unregister(SampleMapInputChannels.Gameplay, this);
            }
        }

        public bool OnMapClicked(in MapClickContext context)
        {
            if (!context.HasPlot)
            {
                // A click on empty space is the dismiss gesture; it is still handed on so the UI can close.
                PlotClicked?.Invoke(-1);
                return true;
            }

            // Gameplay is where selection lives; the preview mode deliberately does not touch it.
            if (m_Controller != null)
            {
                m_Controller.Select(context.PlotId);
            }

            PlotClicked?.Invoke(context.PlotId);
            return true;
        }

        public bool OnMapDragged(Vector2 screenDelta)
        {
            return false;
        }

        public bool OnMapCameraDistanceChanged(in MapCameraDistanceContext context)
        {
            return false;
        }

        private void RegisterIfPossible()
        {
            if (m_Dispatcher == null)
            {
                Debug.LogError("GameplayMapInputHandler has no MapInputDispatcher reference.", this);
                return;
            }

            if (!m_Dispatcher.Register(SampleMapInputChannels.Gameplay, this))
            {
                // The dispatcher already logged why; nothing useful to add here.
                return;
            }
        }
    }
}

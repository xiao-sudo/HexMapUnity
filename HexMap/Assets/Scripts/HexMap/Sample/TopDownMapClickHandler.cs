using System;
using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Reacts to map clicks while the player is looking at the whole map from straight above.
    /// <para>
    /// It answers the same click as <see cref="GameplayMapClickHandler"/> with entirely different UI: it
    /// announces the plot and lets the preview UI decide what to show. It deliberately does not touch the
    /// gameplay selection, because previewing the map is not playing it.
    /// </para>
    /// </summary>
    public sealed class TopDownMapClickHandler : MonoBehaviour, IMapClickHandler, IMapPlotClickSource
    {
        [SerializeField]
        private MapClickDispatcher m_Dispatcher;

        /// <summary>
        /// Raised for every delivered click. PlotId is -1 when the click hit nothing.
        /// </summary>
        public event Action<int> PlotClicked;

        public MapClickDispatcher Dispatcher
        {
            get { return m_Dispatcher; }
            set { m_Dispatcher = value; }
        }

        private void OnEnable()
        {
            if (m_Dispatcher == null)
            {
                Debug.LogError("TopDownMapClickHandler has no MapClickDispatcher reference.", this);
                return;
            }

            m_Dispatcher.Register(SampleMapClickChannels.TopDown, this);
        }

        private void OnDisable()
        {
            if (m_Dispatcher != null)
            {
                m_Dispatcher.Unregister(SampleMapClickChannels.TopDown, this);
            }
        }

        public bool OnMapClicked(in MapClickContext context)
        {
            PlotClicked?.Invoke(context.HasPlot ? context.PlotId : -1);
            return true;
        }
    }
}

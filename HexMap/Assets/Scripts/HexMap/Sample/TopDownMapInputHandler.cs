using System;
using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Reacts to map clicks while the player is looking at the whole map from straight above.
    /// <para>
    /// It answers the same click as <see cref="GameplayMapInputHandler"/> with entirely different UI: it
    /// announces the plot and lets the preview UI decide what to show. It deliberately does not touch the
    /// gameplay selection, because previewing the map is not playing it.
    /// </para>
    /// </summary>
    public sealed class TopDownMapInputHandler : MonoBehaviour, IMapInputHandler, IMapPlotClickSource
    {
        [SerializeField]
        private OrthographicMapCameraInputController m_CameraInputController;

        /// <summary>
        /// Raised for every delivered click. PlotId is -1 when the click hit nothing.
        /// </summary>
        public event Action<int> PlotClicked;

        public OrthographicMapCameraInputController CameraInputController
        {
            get { return m_CameraInputController; }
            set { m_CameraInputController = value; }
        }

        public bool OnMapClicked(in MapClickContext context)
        {
            PlotClicked?.Invoke(context.HasPlot ? context.PlotId : -1);
            return true;
        }

        public bool OnMapDragged(in MapDragContext context)
        {
            return m_CameraInputController != null
                && m_CameraInputController.HandleMapDragged(context.ScreenDelta, context.ViewportAspect);
        }

        public bool OnMapCameraDistanceChanged(in MapCameraDistanceContext context)
        {
            return m_CameraInputController != null
                && m_CameraInputController.HandleCameraDistanceChanged(context, context.ViewportAspect);
        }
    }
}

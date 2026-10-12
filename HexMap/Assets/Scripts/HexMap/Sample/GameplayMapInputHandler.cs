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
    public sealed class GameplayMapInputHandler : MonoBehaviour, IMapInputHandler
    {
        /// <summary>
        /// Raised for every delivered click. PlotId is -1 when the click hit nothing, which is how the
        /// UI learns to close.
        /// </summary>
        public event Action<int> PlotClicked;


        public bool OnMapClicked(in MapClickContext context)
        {
            if (!context.HasPlot)
            {
                // A click on empty space is the dismiss gesture; it is still handed on so the UI can close.
                PlotClicked?.Invoke(-1);
                return true;
            }

            PlotClicked?.Invoke(context.PlotId);
            return true;
        }

        public bool OnMapDragged(in MapDragContext context)
        {
            return false;
        }

        public bool OnMapCameraDistanceChanged(in MapCameraDistanceContext context)
        {
            return false;
        }

    }
}

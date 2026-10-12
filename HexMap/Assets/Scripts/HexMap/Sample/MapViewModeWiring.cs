using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>How a Map view runtime is wired to its scene, in one readable value.</summary>
    public readonly struct MapViewModeWiring
    {
        public MapViewModeWiring(
            Camera gameplayCamera,
            OrthographicMapCamera mapCamera,
            MapInputDispatcher dispatcher,
            GvgMapRuntimeController mapController = null)
        {
            GameplayCamera = gameplayCamera;
            MapCamera = mapCamera;
            Dispatcher = dispatcher;
            MapController = mapController;
        }

        /// <summary>The single base camera used by both view modes.</summary>
        public Camera GameplayCamera { get; }

        /// <summary>Frames the map while the gameplay camera is temporarily orthographic.</summary>
        public OrthographicMapCamera MapCamera { get; }

        /// <summary>Receives the active camera and input channel whenever the mode changes.</summary>
        public MapInputDispatcher Dispatcher { get; }

        /// <summary>Scene-backed map implementation privately coordinated by the runtime facade.</summary>
        public GvgMapRuntimeController MapController { get; }

    }
}

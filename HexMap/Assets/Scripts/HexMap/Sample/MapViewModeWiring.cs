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
            GameObject gameplayUiRoot,
            GameObject topDownUiRoot,
            MapInputDispatcher dispatcher)
        {
            GameplayCamera = gameplayCamera;
            MapCamera = mapCamera;
            GameplayUiRoot = gameplayUiRoot;
            TopDownUiRoot = topDownUiRoot;
            Dispatcher = dispatcher;
        }

        /// <summary>The single base camera used by both view modes.</summary>
        public Camera GameplayCamera { get; }

        /// <summary>Frames the map while the gameplay camera is temporarily orthographic.</summary>
        public OrthographicMapCamera MapCamera { get; }

        /// <summary>Shown during normal gameplay. Goes inactive while the map view is up.</summary>
        public GameObject GameplayUiRoot { get; }

        /// <summary>Shown while the map view is up. Goes inactive during normal gameplay.</summary>
        public GameObject TopDownUiRoot { get; }

        /// <summary>Receives the active camera and input channel whenever the mode changes.</summary>
        public MapInputDispatcher Dispatcher { get; }

    }
}

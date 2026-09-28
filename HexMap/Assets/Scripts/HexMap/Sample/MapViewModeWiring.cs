using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// How a <see cref="MapViewModeSwitcher"/> is wired to its scene, in one readable value.
    /// <para>
    /// This exists so the wiring has a single shape. The switcher used to expose a public setter per
    /// reference, which meant "what does this switcher need" was only answerable by reading every setter,
    /// and the tests had to reach into private fields by name to set the serialized ones. Passing one value
    /// keeps the call site self-describing and keeps the field list from growing a parameter with every new
    /// slot.
    /// </para>
    /// <para>
    /// It is public because the test assembly calls it: this repository has no <c>InternalsVisibleTo</c>,
    /// and the existing precedent for that situation is to publish the helper the tests must call.
    /// </para>
    /// </summary>
    public readonly struct MapViewModeWiring
    {
        public MapViewModeWiring(
            Camera gameplayCamera,
            Camera topDownCamera,
            Camera uiCamera,
            GameObject gameplayUiRoot,
            GameObject topDownUiRoot,
            MapClickDispatcher dispatcher,
            MapViewMode startMode)
        {
            GameplayCamera = gameplayCamera;
            TopDownCamera = topDownCamera;
            UiCamera = uiCamera;
            GameplayUiRoot = gameplayUiRoot;
            TopDownUiRoot = topDownUiRoot;
            Dispatcher = dispatcher;
            StartMode = startMode;
        }

        /// <summary>The camera used during normal gameplay. It keeps the AudioListener and the MainCamera tag.</summary>
        public Camera GameplayCamera { get; }

        /// <summary>The orthographic camera used for the whole map view. It must not carry the MainCamera tag.</summary>
        public Camera TopDownCamera { get; }

        /// <summary>The UI camera. It is an overlay that is moved between the two base cameras' stacks.</summary>
        public Camera UiCamera { get; }

        /// <summary>Shown during normal gameplay. Goes inactive while the map view is up.</summary>
        public GameObject GameplayUiRoot { get; }

        /// <summary>Shown while the map view is up. Goes inactive during normal gameplay.</summary>
        public GameObject TopDownUiRoot { get; }

        /// <summary>Receives the active camera and click channel whenever the mode changes.</summary>
        public MapClickDispatcher Dispatcher { get; }

        /// <summary>The mode the scene starts in.</summary>
        public MapViewMode StartMode { get; }
    }
}

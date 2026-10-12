using System;
using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Switches the one gameplay camera between its gameplay view and its orthographic whole-map view.
    /// The UI camera is intentionally absent: it is fixed scene infrastructure in the gameplay camera's
    /// URP stack and does not participate in a mode transition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapViewModeSwitcher : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The single base camera. It keeps the AudioListener and the MainCamera tag in both modes.")]
        private Camera m_GameplayCamera;

        [SerializeField]
        [Tooltip("Frames the whole map after the gameplay camera becomes orthographic.")]
        private OrthographicMapCamera m_MapCamera;

        [SerializeField]
        [Tooltip("Shown during normal gameplay. Goes inactive while the map view is up.")]
        private GameObject m_GameplayUiRoot;

        [SerializeField]
        [Tooltip("Shown while the map view is up. Goes inactive during normal gameplay.")]
        private GameObject m_TopDownUiRoot;

        [SerializeField]
        [Tooltip("Receives the active camera and input channel whenever the mode changes.")]
        private MapInputDispatcher m_Dispatcher;

        private IMapViewModeState m_GameplayState;
        private IMapViewModeState m_TopDownState;
        private IMapViewModeState m_CurrentState;
        private bool m_IsConfigured;

        public MapViewMode CurrentMode
        {
            get
            {
                if (m_CurrentState == null)
                {
                    throw new InvalidOperationException("MapViewModeSwitcher has no active mode. Configure it and call SetMode first.");
                }

                return m_CurrentState.Mode;
            }
        }

        public float ActiveCameraAspect
        {
            get { return m_GameplayCamera != null ? m_GameplayCamera.aspect : 1f; }
        }
        public MapViewModeWiring Wiring
        {
            get
            {
                return new MapViewModeWiring(
                    m_GameplayCamera,
                    m_MapCamera,
                    m_GameplayUiRoot,
                    m_TopDownUiRoot,
                    m_Dispatcher);
            }
        }

        public void Configure(in MapViewModeWiring wiring)
        {
            if (m_CurrentState != null)
            {
                throw new InvalidOperationException("MapViewModeSwitcher cannot replace its wiring after a mode is active.");
            }

            m_GameplayCamera = wiring.GameplayCamera;
            m_MapCamera = wiring.MapCamera;
            m_GameplayUiRoot = wiring.GameplayUiRoot;
            m_TopDownUiRoot = wiring.TopDownUiRoot;
            m_Dispatcher = wiring.Dispatcher;
            m_IsConfigured = true;
        }

        public void Toggle()
        {
            SetMode(CurrentMode == MapViewMode.Gameplay ? MapViewMode.TopDown : MapViewMode.Gameplay);
        }

        /// <summary>
        /// Switches to the requested view mode after business code has supplied its wiring through
        /// <see cref="Configure"/>. It is a no-op when the requested mode is already active.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="target"/> is not a declared mode.</exception>
        public void SetMode(MapViewMode target)
        {
            if (target != MapViewMode.Gameplay && target != MapViewMode.TopDown)
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "The requested map view mode is not defined.");
            }

            if (!m_IsConfigured)
            {
                throw new InvalidOperationException("MapViewModeSwitcher must be configured before SetMode is called.");
            }

            EnsureStates();
            if (m_CurrentState != null && m_CurrentState.Mode == target)
            {
                return;
            }

            if (m_CurrentState != null)
            {
                m_CurrentState.Exit();
            }
            else
            {
                InitializeUiRoots();
            }

            ClearActiveInput();
            m_CurrentState = target == MapViewMode.TopDown ? m_TopDownState : m_GameplayState;
            m_CurrentState.Enter(this);
        }

        /// <summary>
        /// Leaves the active mode and removes its presentation and input route. The switcher remains
        /// configured, so a later <see cref="SetMode"/> starts a new mode lifetime.
        /// </summary>
        internal void Stop()
        {
            if (m_CurrentState != null)
            {
                m_CurrentState.Exit();
                m_CurrentState = null;
            }

            ClearActiveInput();
        }

        private void EnsureStates()
        {
            if (m_CurrentState != null)
            {
                return;
            }

            m_GameplayState = new MapGameplayViewState(
                m_GameplayCamera,
                m_GameplayUiRoot,
                SampleMapInputChannels.For(MapViewMode.Gameplay));
            m_TopDownState = new MapTopDownViewState(
                m_GameplayCamera,
                m_MapCamera,
                m_TopDownUiRoot,
                SampleMapInputChannels.For(MapViewMode.TopDown));
        }

        private void InitializeUiRoots()
        {
            MapViewModeUi.SetRootActive(m_GameplayUiRoot, false);
            MapViewModeUi.SetRootActive(m_TopDownUiRoot, false);
        }

        private void ClearActiveInput()
        {
            if (m_Dispatcher != null)
            {
                m_Dispatcher.SetActiveCamera(null);
                m_Dispatcher.SetActiveChannel(MapInputChannels.None);
            }
        }

        internal void EnterMode(Camera camera, int channel)
        {
            if (camera != null)
            {
                camera.enabled = true;
            }

            if (m_Dispatcher != null)
            {
                m_Dispatcher.SetActiveCamera(camera);
                m_Dispatcher.SetActiveChannel(channel);
            }
        }

    }
}

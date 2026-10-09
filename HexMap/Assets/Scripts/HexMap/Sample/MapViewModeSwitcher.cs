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
        [Tooltip("Receives the active camera and click channel whenever the mode changes.")]
        private MapClickDispatcher m_Dispatcher;

        [SerializeField]
        [Tooltip("Which mode the scene starts in.")]
        private MapViewMode m_StartMode;

        private IMapViewModeState m_GameplayState;
        private IMapViewModeState m_TopDownState;
        private IMapViewModeState m_CurrentState;

        public MapViewMode CurrentMode
        {
            get { return m_CurrentState != null ? m_CurrentState.Mode : m_StartMode; }
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
                    m_Dispatcher,
                    m_StartMode);
            }
        }

        private void Start()
        {
            ApplySerializedMode();
        }

        public void Configure(in MapViewModeWiring wiring)
        {
            m_GameplayCamera = wiring.GameplayCamera;
            m_MapCamera = wiring.MapCamera;
            m_GameplayUiRoot = wiring.GameplayUiRoot;
            m_TopDownUiRoot = wiring.TopDownUiRoot;
            m_Dispatcher = wiring.Dispatcher;
            m_StartMode = wiring.StartMode;
        }

        public void ApplySerializedMode()
        {
            EnsureStates();
            SetMode(m_StartMode);
        }

        public void Toggle()
        {
            EnsureStates();
            SetMode(CurrentMode == MapViewMode.Gameplay ? MapViewMode.TopDown : MapViewMode.Gameplay);
        }

        private void SetMode(MapViewMode target)
        {
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

        private void EnsureStates()
        {
            if (m_CurrentState != null)
            {
                return;
            }

            m_GameplayState = new MapGameplayViewState(
                m_GameplayCamera,
                m_GameplayUiRoot,
                SampleMapClickChannels.For(MapViewMode.Gameplay));
            m_TopDownState = new MapTopDownViewState(
                m_GameplayCamera,
                m_MapCamera,
                m_TopDownUiRoot,
                SampleMapClickChannels.For(MapViewMode.TopDown));
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
                m_Dispatcher.SetActiveChannel(MapClickChannels.None);
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

using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Unity composition host for one <see cref="MapViewRuntimeFacade"/>. It adapts Unity lifecycle and
    /// scene references only; mode behavior, input routing, and camera presentation stay inside the facade.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SampleMapViewRuntimeHost : MonoBehaviour
    {
        [SerializeField] private Camera m_GameplayCamera;
        [SerializeField] private OrthographicMapCamera m_MapCamera;
        [SerializeField] private MapInputDispatcher m_Dispatcher;
        [SerializeField] private GvgMapRuntimeController m_MapController;
        [SerializeField] private GameplayMapInputHandler m_GameplayHandler;
        [SerializeField] private TopDownMapInputHandler m_TopDownHandler;
        [SerializeField] private MapViewMode m_InitialMode = MapViewMode.Gameplay;
        [SerializeField] private float m_PinchDeadZoneNormalized = 0.003f;

        private MapViewRuntimeFacade m_Runtime;
        private bool m_IsInitialized;

        public MapViewRuntimeFacade Runtime { get { return m_Runtime; } }
        public Camera GameplayCamera { get { return m_GameplayCamera; } }
        public OrthographicMapCamera MapCamera { get { return m_MapCamera; } }
        public MapInputDispatcher Dispatcher { get { return m_Dispatcher; } }
        public GvgMapRuntimeController MapController { get { return m_MapController; } }
        public GameplayMapInputHandler GameplayHandler { get { return m_GameplayHandler; } }
        public TopDownMapInputHandler TopDownHandler { get { return m_TopDownHandler; } }
        public MapViewMode InitialMode { get { return m_InitialMode; } }
        public float PinchDeadZoneNormalized { get { return m_PinchDeadZoneNormalized; } }

        /// <summary>Sets all scene composition dependencies in one operation for editor migration tools.</summary>
        public void Configure(
            Camera gameplayCamera,
            OrthographicMapCamera mapCamera,
            MapInputDispatcher dispatcher,
            GvgMapRuntimeController mapController,
            GameplayMapInputHandler gameplayHandler,
            TopDownMapInputHandler topDownHandler,
            MapViewMode initialMode,
            float pinchDeadZoneNormalized)
        {
            m_GameplayCamera = gameplayCamera;
            m_MapCamera = mapCamera;
            m_Dispatcher = dispatcher;
            m_MapController = mapController;
            m_GameplayHandler = gameplayHandler;
            m_TopDownHandler = topDownHandler;
            m_InitialMode = initialMode;
            m_PinchDeadZoneNormalized = pinchDeadZoneNormalized;
        }

        public void SetViewMode(MapViewMode mode)
        {
            if (m_Runtime == null)
            {
                Debug.LogError("Map view mode cannot change before the runtime host has initialized.", this);
                return;
            }

            m_Runtime.SetViewMode(mode);
        }

        private void Awake()
        {
            m_Runtime = new MapViewRuntimeFacade();

            m_IsInitialized = m_Runtime.Initialize(
                m_InitialMode,
                new MapViewModeWiring(
                    m_GameplayCamera,
                    m_MapCamera,
                    m_Dispatcher,
                    m_MapController),
                m_GameplayHandler,
                m_TopDownHandler,
                m_PinchDeadZoneNormalized);

            if (!m_IsInitialized)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (m_IsInitialized)
            {
                m_Runtime.Start();
            }
        }

        private void OnDisable()
        {
            if (m_IsInitialized)
            {
                m_Runtime.Stop();
            }
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (m_IsInitialized && m_Runtime != null)
            {
                m_Runtime.Tick();
            }
        }
#endif

        private void OnDestroy()
        {
            if (m_Runtime != null)
            {
                m_Runtime.Dispose();
                m_Runtime = null;
            }

            m_IsInitialized = false;
        }
    }
}

using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// 场景中的 Map 视图组合根：在运行时将场景引用交给 <see cref="MapViewRuntimeFacade"/>，
    /// 并明确请求初始模式。
    /// <para>
    /// 本类不拥有模式切换逻辑、相机数学、Map 数据或 UI 行为；这些职责分别保留在运行时门面、
    /// 相机控制器、运行时 Map 控制器和各模式 Handler 中。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SampleMapViewBootstrapper : MonoBehaviour
    {
        [SerializeField]
        private Camera m_GameplayCamera;

        [SerializeField]
        private OrthographicMapCamera m_MapCamera;

        [SerializeField]
        private GameObject m_GameplayUiRoot;

        [SerializeField]
        private GameObject m_TopDownUiRoot;

        [SerializeField]
        private MapInputDispatcher m_Dispatcher;

        [SerializeField]
        private GameplayMapInputHandler m_GameplayHandler;

        [SerializeField]
        private TopDownMapInputHandler m_TopDownHandler;

        [SerializeField]
        private MapViewMode m_InitialMode = MapViewMode.Gameplay;

        private MapViewRuntimeFacade m_Runtime;

        public MapViewRuntimeFacade Runtime
        {
            get { return m_Runtime; }
        }

        public Camera GameplayCamera
        {
            get { return m_GameplayCamera; }
            set { m_GameplayCamera = value; }
        }

        public OrthographicMapCamera MapCamera
        {
            get { return m_MapCamera; }
            set { m_MapCamera = value; }
        }

        public GameObject GameplayUiRoot
        {
            get { return m_GameplayUiRoot; }
            set { m_GameplayUiRoot = value; }
        }

        public GameObject TopDownUiRoot
        {
            get { return m_TopDownUiRoot; }
            set { m_TopDownUiRoot = value; }
        }

        public MapInputDispatcher Dispatcher
        {
            get { return m_Dispatcher; }
            set { m_Dispatcher = value; }
        }

        public GameplayMapInputHandler GameplayHandler
        {
            get { return m_GameplayHandler; }
            set { m_GameplayHandler = value; }
        }

        public TopDownMapInputHandler TopDownHandler
        {
            get { return m_TopDownHandler; }
            set { m_TopDownHandler = value; }
        }

        public MapViewMode InitialMode
        {
            get { return m_InitialMode; }
            set { m_InitialMode = value; }
        }

        private void Awake()
        {
            m_Runtime = new MapViewRuntimeFacade();
            if (m_Runtime.Initialize(m_InitialMode, new MapViewModeWiring(
                m_GameplayCamera,
                m_MapCamera,
                m_GameplayUiRoot,
                m_TopDownUiRoot,
                m_Dispatcher),
                m_GameplayHandler,
                m_TopDownHandler))
            {
                return;
            }

            enabled = false;
        }

        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            m_Runtime.Start();
        }

        private void OnDestroy()
        {
            if (m_Runtime != null)
            {
                m_Runtime.Dispose();
                m_Runtime = null;
            }
        }
    }
}

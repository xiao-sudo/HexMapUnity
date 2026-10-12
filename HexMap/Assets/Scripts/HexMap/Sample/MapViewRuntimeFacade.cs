using System;
using HexMap.UnityRuntime;

namespace HexMap.Sample
{
    /// <summary>
    /// Explicit runtime lifecycle for the supported Map view modes. A composition root supplies scene
    /// dependencies through <see cref="Initialize"/> before the runtime can start.
    /// </summary>
    public sealed class MapViewRuntimeFacade : IDisposable
    {
        private MapViewRuntimeModeController m_ModeController;
        private EasyTouchMapInputSource m_InputSource;
        private MapViewMode m_CurrentMode;
        private MapViewMode m_InitialMode;
        private bool m_IsInitialized;
        private bool m_IsRunning;
        private bool m_IsDisposed;
        private readonly RuntimeObserverSet<MapViewMode> m_ModeChangedObservers = new RuntimeObserverSet<MapViewMode>();
        private readonly RuntimeObserverSet<bool> m_RuntimeStoppedObservers = new RuntimeObserverSet<bool>();

        public MapViewMode CurrentMode
        {
            get
            {
                EnsureInitialized();
                return m_CurrentMode;
            }
        }

        public bool IsInitialized
        {
            get { return m_IsInitialized; }
        }

        public bool IsRunning
        {
            get { return m_IsRunning; }
        }

        /// <summary>
        /// Validates and retains the runtime wiring without entering a view mode.
        /// </summary>
        public bool Initialize(
            MapViewMode initialMode,
            in MapViewModeWiring wiring,
            IMapInputHandler gameplayHandler,
            IMapInputHandler topDownHandler,
            float pinchDeadZoneNormalized = 0.003f)
        {
            if (m_IsDisposed)
            {
                UnityEngine.Debug.LogError("Map view runtime initialization failed: a disposed MapViewRuntimeFacade cannot be initialized.");
                return false;
            }

            if (m_IsInitialized)
            {
                UnityEngine.Debug.LogError("Map view runtime initialization failed: MapViewRuntimeFacade can only be initialized once.");
                return false;
            }

            if (!Validate(initialMode, wiring, gameplayHandler, topDownHandler))
            {
                return false;
            }

            var modeController = new MapViewRuntimeModeController(wiring, gameplayHandler, topDownHandler);
            if (!modeController.TryRegisterHandlers())
            {
                modeController.Dispose();
                return false;
            }

            m_ModeController = modeController;
            m_InputSource = new EasyTouchMapInputSource(wiring.Dispatcher, pinchDeadZoneNormalized);
            m_InitialMode = initialMode;
            m_CurrentMode = initialMode;
            m_IsInitialized = true;
            return true;
        }

        public void Start()
        {
            EnsureNotDisposed();
            EnsureInitialized();
            if (m_IsRunning)
            {
                return;
            }

            m_ModeController.SetMode(m_InitialMode);
            m_InputSource.Start();
            m_CurrentMode = m_InitialMode;
            m_IsRunning = true;
            m_ModeChangedObservers.Publish(m_CurrentMode);
        }

        public void SetViewMode(MapViewMode target)
        {
            EnsureNotDisposed();
            EnsureInitialized();
            if (!m_IsRunning)
            {
                throw new InvalidOperationException("MapViewRuntimeFacade must be running before its view mode can change.");
            }

            if (!IsDeclaredMode(target))
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "The requested map view mode is not defined.");
            }

            if (m_CurrentMode == target)
            {
                return;
            }

            m_ModeController.SetMode(target);
            m_CurrentMode = target;
            m_ModeChangedObservers.Publish(m_CurrentMode);
        }

        public void Stop()
        {
            if (!m_IsRunning)
            {
                return;
            }

            m_InputSource.Stop();
            m_ModeController.Stop();
            m_IsRunning = false;
            m_RuntimeStoppedObservers.Publish(true);
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            Stop();
            if (m_InputSource != null)
            {
                m_InputSource.Dispose();
                m_InputSource = null;
            }

            if (m_ModeController != null)
            {
                m_ModeController.Dispose();
                m_ModeController = null;
            }

            m_ModeChangedObservers.Clear();
            m_RuntimeStoppedObservers.Clear();
            m_IsDisposed = true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Drives editor-only input polling for the private physical input source.
        /// </summary>
        internal void Tick()
        {
            EnsureNotDisposed();
            if (m_IsRunning)
            {
                m_InputSource.Tick();
            }
        }
#endif

        public IDisposable SubscribeModeChanged(Action<MapViewMode> observer)
        {
            EnsureNotDisposed();
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            return m_ModeChangedObservers.Subscribe(observer, observer.Target);
        }

        public IDisposable SubscribeRuntimeStopped(Action observer)
        {
            EnsureNotDisposed();
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            return m_RuntimeStoppedObservers.Subscribe(_ => observer(), observer.Target);
        }

        private static bool Validate(
            MapViewMode initialMode,
            in MapViewModeWiring wiring,
            IMapInputHandler gameplayHandler,
            IMapInputHandler topDownHandler)
        {
            var isValid = true;
            if (!IsDeclaredMode(initialMode))
            {
                UnityEngine.Debug.LogError(
                    "Map view runtime initialization failed: initial mode must be Gameplay or TopDown.");
                isValid = false;
            }

            isValid &= Require(wiring.GameplayCamera, "Gameplay camera");
            isValid &= Require(wiring.MapCamera, "Orthographic map camera");
            isValid &= Require(wiring.GameplayUiRoot, "Gameplay UI root");
            isValid &= Require(wiring.TopDownUiRoot, "Top-down UI root");
            isValid &= Require(wiring.Dispatcher, "Map input dispatcher");
            isValid &= RequireHandler(gameplayHandler, "Gameplay");
            isValid &= RequireHandler(topDownHandler, "TopDown");
            return isValid;
        }

        private static bool Require(UnityEngine.Object value, string name)
        {
            if (value != null)
            {
                return true;
            }

            UnityEngine.Debug.LogError("Map view runtime initialization failed: " + name + " is required.");
            return false;
        }

        private static bool RequireHandler(IMapInputHandler handler, string modeName)
        {
            if (handler != null)
            {
                return true;
            }

            UnityEngine.Debug.LogError(
                "Map view runtime initialization failed: " + modeName + " mode requires a primary input handler.");
            return false;
        }

        private static bool IsDeclaredMode(MapViewMode mode)
        {
            return mode == MapViewMode.Gameplay || mode == MapViewMode.TopDown;
        }

        private void EnsureInitialized()
        {
            if (!m_IsInitialized)
            {
                throw new InvalidOperationException("MapViewRuntimeFacade must be initialized before use.");
            }
        }

        private void EnsureNotDisposed()
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(MapViewRuntimeFacade));
            }
        }
    }

}

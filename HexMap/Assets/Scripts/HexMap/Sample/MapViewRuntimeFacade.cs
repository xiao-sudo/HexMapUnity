using System;

namespace HexMap.Sample
{
    /// <summary>
    /// Explicit runtime lifecycle for the supported Map view modes. A composition root supplies scene
    /// dependencies through <see cref="Initialize"/> before the runtime can start.
    /// </summary>
    public sealed class MapViewRuntimeFacade : IDisposable
    {
        private MapViewRuntimeModeController m_ModeController;
        private MapViewMode m_CurrentMode;
        private MapViewMode m_InitialMode;
        private bool m_IsInitialized;
        private bool m_IsRunning;
        private bool m_IsDisposed;

        public event Action<MapViewMode> ModeChanged;

        public event Action RuntimeStopped;

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
        public bool Initialize(MapViewMode initialMode, in MapViewModeWiring wiring)
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

            if (!Validate(initialMode, wiring))
            {
                return false;
            }

            m_ModeController = new MapViewRuntimeModeController(wiring);
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
            m_CurrentMode = m_InitialMode;
            m_IsRunning = true;
            ModeChanged?.Invoke(m_CurrentMode);
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
            ModeChanged?.Invoke(m_CurrentMode);
        }

        public void Stop()
        {
            if (!m_IsRunning)
            {
                return;
            }

            m_ModeController.Stop();
            m_IsRunning = false;
            RuntimeStopped?.Invoke();
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            Stop();
            m_ModeController = null;
            m_IsDisposed = true;
        }

        private static bool Validate(MapViewMode initialMode, in MapViewModeWiring wiring)
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

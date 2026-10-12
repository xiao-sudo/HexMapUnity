using System;
using System.Collections.Generic;
using HexMap.Gvg;
using HexMap.Runtime;
using HexMap.UnityRuntime;
using UnityEngine;

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
        private MapInputDispatcher m_Dispatcher;
        private GvgMapRuntimeController m_MapController;
        private MapViewMode m_CurrentMode;
        private MapViewMode m_InitialMode;
        private bool m_IsInitialized;
        private bool m_IsRunning;
        private bool m_IsDisposed;
        private readonly RuntimeObserverSet<MapViewMode> m_ModeChangedObservers = new RuntimeObserverSet<MapViewMode>();
        private readonly RuntimeObserverSet<bool> m_RuntimeStoppedObservers = new RuntimeObserverSet<bool>();
        private readonly PathResult m_MapNotInitializedPathResult = new PathResult(new List<int>());

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

        /// <summary>Whether the scene-backed GVG map has accepted a Plot snapshot.</summary>
        public bool IsMapInitialized
        {
            get { return m_MapController != null && m_MapController.IsInitialized; }
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
            m_Dispatcher = wiring.Dispatcher;
            m_MapController = wiring.MapController;
            if (m_MapController != null)
            {
                m_Dispatcher.SetPlotPicker(m_MapController);
            }

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

        /// <summary>
        /// Applies a GVG Plot snapshot to the configured map. The facade owns the externally visible
        /// map lifetime; callers never need to address the scene controller directly.
        /// </summary>
        public bool TryInitializeMap(IReadOnlyList<GvgPlotRuntimeData> plots)
        {
            EnsureNotDisposed();
            EnsureInitialized();
            return m_MapController != null && m_MapController.TryInitialize(plots);
        }

        public PlotScreenPickResult PickPlotAtScreenPosition(Vector2 screenPosition)
        {
            EnsureNotDisposed();
            EnsureInitialized();
            if (m_MapController == null)
            {
                return PlotScreenPickResult.MapNotInitialized();
            }

            return m_MapController.PickPlotAtScreenPosition(screenPosition, m_Dispatcher.ActiveCamera);
        }

        public bool SelectPlot(int plotId)
        {
            EnsureNotDisposed();
            EnsureInitialized();
            return m_MapController != null && m_MapController.Select(plotId);
        }

        public bool DeselectPlot(int plotId)
        {
            EnsureNotDisposed();
            EnsureInitialized();
            return m_MapController != null && m_MapController.Deselect(plotId);
        }

        public bool DeselectAllPlots()
        {
            EnsureNotDisposed();
            EnsureInitialized();
            return m_MapController != null && m_MapController.DeselectAll();
        }

        public bool TrySetPlotOwnerFactionId(int plotId, int factionId)
        {
            EnsureNotDisposed();
            EnsureInitialized();
            return m_MapController != null && m_MapController.TrySetPlotOwnerFactionId(plotId, factionId);
        }

        public bool TryGetPlotWorldAnchor(int plotId, out Vector3 worldAnchor)
        {
            EnsureNotDisposed();
            EnsureInitialized();
            if (m_MapController == null)
            {
                worldAnchor = Vector3.zero;
                return false;
            }

            return m_MapController.TryGetPlotWorldAnchor(plotId, out worldAnchor);
        }

        public PathResult TryFindPlotPath(int startPlotId, int targetPlotId, int movingFactionId)
        {
            EnsureNotDisposed();
            EnsureInitialized();
            if (m_MapController != null)
            {
                return m_MapController.TryFindPlotPath(startPlotId, targetPlotId, movingFactionId);
            }

            m_MapNotInitializedPathResult.SetFailure(PathResultStatus.InvalidInput, PathFailureReason.MapNotInitialized);
            return m_MapNotInitializedPathResult;
        }

        /// <summary>
        /// Copies the scene map's cell IDs for callers that need to construct a one-cell-per-Plot sample
        /// snapshot without exposing the scene controller.
        /// </summary>
        public bool TryCopyMapCellIds(List<int> cellIds)
        {
            EnsureNotDisposed();
            EnsureInitialized();
            if (cellIds == null)
            {
                throw new ArgumentNullException(nameof(cellIds));
            }

            if (m_MapController == null || m_MapController.HexMapView == null || m_MapController.HexMapView.Map == null)
            {
                return false;
            }

            cellIds.Clear();
            foreach (var cell in m_MapController.HexMapView.Map.Cells)
            {
                cellIds.Add(cell.Id);
            }

            return true;
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

            if (m_Dispatcher != null)
            {
                m_Dispatcher.SetPlotPicker(null);
                m_Dispatcher = null;
            }

            m_MapController = null;

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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
        public MapViewRuntimeInitializationResult Initialize(MapViewMode initialMode, in MapViewModeWiring wiring)
        {
            if (m_IsDisposed)
            {
                return MapViewRuntimeInitializationResult.Failure("A disposed MapViewRuntimeFacade cannot be initialized.");
            }

            if (m_IsInitialized)
            {
                return MapViewRuntimeInitializationResult.Failure("MapViewRuntimeFacade can only be initialized once.");
            }

            var errors = Validate(initialMode, wiring);
            if (errors.Count != 0)
            {
                return new MapViewRuntimeInitializationResult(errors);
            }

            m_ModeController = new MapViewRuntimeModeController(wiring);
            m_InitialMode = initialMode;
            m_CurrentMode = initialMode;
            m_IsInitialized = true;
            return MapViewRuntimeInitializationResult.Success();
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

        private static List<string> Validate(MapViewMode initialMode, in MapViewModeWiring wiring)
        {
            var errors = new List<string>();
            if (!IsDeclaredMode(initialMode))
            {
                errors.Add("Initial mode must be Gameplay or TopDown.");
            }

            AddMissingReferenceError(errors, wiring.GameplayCamera, "Gameplay camera");
            AddMissingReferenceError(errors, wiring.MapCamera, "Orthographic map camera");
            AddMissingReferenceError(errors, wiring.GameplayUiRoot, "Gameplay UI root");
            AddMissingReferenceError(errors, wiring.TopDownUiRoot, "Top-down UI root");
            AddMissingReferenceError(errors, wiring.Dispatcher, "Map input dispatcher");
            return errors;
        }

        private static void AddMissingReferenceError(List<string> errors, UnityEngine.Object value, string name)
        {
            if (value == null)
            {
                errors.Add(name + " is required.");
            }
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

    /// <summary>The inspectable outcome of explicit Map view runtime initialization.</summary>
    public sealed class MapViewRuntimeInitializationResult
    {
        private readonly ReadOnlyCollection<string> m_Errors;

        internal MapViewRuntimeInitializationResult(IEnumerable<string> errors)
        {
            m_Errors = new List<string>(errors).AsReadOnly();
        }

        public bool Succeeded
        {
            get { return m_Errors.Count == 0; }
        }

        public IReadOnlyList<string> Errors
        {
            get { return m_Errors; }
        }

        internal static MapViewRuntimeInitializationResult Success()
        {
            return new MapViewRuntimeInitializationResult(new string[0]);
        }

        internal static MapViewRuntimeInitializationResult Failure(string error)
        {
            return new MapViewRuntimeInitializationResult(new[] { error });
        }
    }
}

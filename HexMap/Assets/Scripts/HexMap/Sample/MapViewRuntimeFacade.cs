using System;
using System.Collections.Generic;
using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// The scene-level lifecycle owner for the two supported Map view modes.
    /// It keeps scene wiring and the concrete switcher private so gameplay code only depends on this
    /// runtime boundary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapViewRuntimeFacade : MonoBehaviour
    {
        [SerializeField]
        private MapViewModeSwitcher m_Switcher;

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
        private MapViewMode m_InitialMode = MapViewMode.Gameplay;

        private readonly List<string> m_ValidationErrors = new List<string>();

        private MapViewMode m_CurrentMode;
        private bool m_IsConfigured;
        private bool m_IsRunning;

        /// <summary>Raised after a mode has become the active presentation and input route.</summary>
        public event Action<MapViewMode> ModeChanged;

        /// <summary>Raised after the active mode has been stopped and its input route cleared.</summary>
        public event Action RuntimeStopped;

        /// <summary>The most recently entered mode, or the configured initial mode before first start.</summary>
        public MapViewMode CurrentMode
        {
            get { return m_CurrentMode; }
        }

        /// <summary>Whether the facade currently owns an active view mode and input route.</summary>
        public bool IsRunning
        {
            get { return m_IsRunning; }
        }

        /// <summary>Whether required scene configuration was valid during <see cref="Awake"/>.</summary>
        public bool IsConfigurationValid
        {
            get { return m_IsConfigured; }
        }

        /// <summary>Read-only diagnostics for configuration that prevented startup.</summary>
        public IReadOnlyList<string> ValidationErrors
        {
            get { return m_ValidationErrors; }
        }

        private void Awake()
        {
            ValidateConfiguration();
            m_CurrentMode = m_InitialMode;
            if (!m_IsConfigured)
            {
                ReportValidationErrors();
                enabled = false;
                return;
            }

            try
            {
                m_Switcher.Configure(new MapViewModeWiring(
                    m_GameplayCamera,
                    m_MapCamera,
                    m_GameplayUiRoot,
                    m_TopDownUiRoot,
                    m_Dispatcher));
            }
            catch (InvalidOperationException exception)
            {
                m_ValidationErrors.Add("MapViewModeSwitcher cannot accept this facade's scene wiring: " + exception.Message);
                m_IsConfigured = false;
                ReportValidationErrors();
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!m_IsConfigured)
            {
                return;
            }

            StartRuntime();
        }

        private void OnDisable()
        {
            StopRuntime();
        }

        private void OnDestroy()
        {
            StopRuntime();
        }

        /// <summary>Switches the currently running Map presentation to a declared mode.</summary>
        public void SetViewMode(MapViewMode target)
        {
            if (!IsDeclaredMode(target))
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "The requested map view mode is not defined.");
            }

            if (!m_IsRunning)
            {
                throw new InvalidOperationException("MapViewRuntimeFacade must be running before its view mode can change.");
            }

            if (m_CurrentMode == target)
            {
                return;
            }

            m_Switcher.SetMode(target);
            m_CurrentMode = target;
            ModeChanged?.Invoke(m_CurrentMode);
        }

        private void StartRuntime()
        {
            if (m_IsRunning)
            {
                return;
            }

            m_Switcher.SetMode(m_InitialMode);
            m_CurrentMode = m_InitialMode;
            m_IsRunning = true;
            ModeChanged?.Invoke(m_CurrentMode);
        }

        private void StopRuntime()
        {
            if (!m_IsRunning)
            {
                return;
            }

            m_Switcher.Stop();
            m_IsRunning = false;
            RuntimeStopped?.Invoke();
        }

        private void ValidateConfiguration()
        {
            m_ValidationErrors.Clear();
            AddMissingReferenceError(m_Switcher, "MapViewModeSwitcher");
            AddMissingReferenceError(m_GameplayCamera, "Gameplay camera");
            AddMissingReferenceError(m_MapCamera, "Orthographic map camera");
            AddMissingReferenceError(m_GameplayUiRoot, "Gameplay UI root");
            AddMissingReferenceError(m_TopDownUiRoot, "Top-down UI root");
            AddMissingReferenceError(m_Dispatcher, "Map input dispatcher");

            if (!IsDeclaredMode(m_InitialMode))
            {
                m_ValidationErrors.Add("Initial mode must be Gameplay or TopDown.");
            }

            m_IsConfigured = m_ValidationErrors.Count == 0;
        }

        private void AddMissingReferenceError(UnityEngine.Object value, string name)
        {
            if (value == null)
            {
                m_ValidationErrors.Add(name + " is required.");
            }
        }

        private void ReportValidationErrors()
        {
            for (var index = 0; index < m_ValidationErrors.Count; index++)
            {
                Debug.LogError("MapViewRuntimeFacade configuration error: " + m_ValidationErrors[index], this);
            }
        }

        private static bool IsDeclaredMode(MapViewMode mode)
        {
            return mode == MapViewMode.Gameplay || mode == MapViewMode.TopDown;
        }
    }
}

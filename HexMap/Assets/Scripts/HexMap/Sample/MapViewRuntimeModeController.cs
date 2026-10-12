using System;
using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>Owns the implementation details of a configured Map view-mode lifetime.</summary>
    internal sealed class MapViewRuntimeModeController : IMapViewModeHost, System.IDisposable
    {
        private readonly Camera m_GameplayCamera;
        private readonly OrthographicMapCamera m_MapCamera;
        private readonly GameObject m_GameplayUiRoot;
        private readonly GameObject m_TopDownUiRoot;
        private readonly MapInputDispatcher m_Dispatcher;
        private readonly IMapInputHandler m_GameplayHandler;
        private readonly IMapInputHandler m_TopDownHandler;

        private IMapViewModeState m_GameplayState;
        private IMapViewModeState m_TopDownState;
        private IMapViewModeState m_CurrentState;
        private bool m_AreHandlersRegistered;

        public MapViewRuntimeModeController(
            in MapViewModeWiring wiring,
            IMapInputHandler gameplayHandler,
            IMapInputHandler topDownHandler)
        {
            m_GameplayCamera = wiring.GameplayCamera;
            m_MapCamera = wiring.MapCamera;
            m_GameplayUiRoot = wiring.GameplayUiRoot;
            m_TopDownUiRoot = wiring.TopDownUiRoot;
            m_Dispatcher = wiring.Dispatcher;
            m_GameplayHandler = gameplayHandler;
            m_TopDownHandler = topDownHandler;
        }

        public bool TryRegisterHandlers()
        {
            if (!m_Dispatcher.Register(SampleMapInputChannels.Gameplay, m_GameplayHandler))
            {
                return false;
            }

            if (!m_Dispatcher.Register(SampleMapInputChannels.TopDown, m_TopDownHandler))
            {
                m_Dispatcher.Unregister(SampleMapInputChannels.Gameplay, m_GameplayHandler);
                return false;
            }

            m_AreHandlersRegistered = true;
            return true;
        }

        public void SetMode(MapViewMode target)
        {
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
                MapViewModeUi.SetRootActive(m_GameplayUiRoot, false);
                MapViewModeUi.SetRootActive(m_TopDownUiRoot, false);
            }

            ClearActiveInput();
            m_CurrentState = target == MapViewMode.TopDown ? m_TopDownState : m_GameplayState;
            m_CurrentState.Enter(this);
        }

        public void Stop()
        {
            if (m_CurrentState != null)
            {
                m_CurrentState.Exit();
                m_CurrentState = null;
            }

            ClearActiveInput();
        }

        public void Dispose()
        {
            Stop();
            if (!m_AreHandlersRegistered)
            {
                return;
            }

            m_Dispatcher.Unregister(SampleMapInputChannels.TopDown, m_TopDownHandler);
            m_Dispatcher.Unregister(SampleMapInputChannels.Gameplay, m_GameplayHandler);
            m_AreHandlersRegistered = false;
        }

        void IMapViewModeHost.EnterMode(Camera camera, int channel)
        {
            if (camera != null)
            {
                camera.enabled = true;
            }

            m_Dispatcher.SetActiveCamera(camera);
            m_Dispatcher.SetActiveChannel(channel);
        }

        private void EnsureStates()
        {
            if (m_GameplayState != null)
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

        private void ClearActiveInput()
        {
            m_Dispatcher.SetActiveCamera(null);
            m_Dispatcher.SetActiveChannel(MapInputChannels.None);
        }

    }
}

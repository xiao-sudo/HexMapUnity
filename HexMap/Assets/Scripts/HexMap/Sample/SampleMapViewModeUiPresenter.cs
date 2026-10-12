using System;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Sample-level presentation policy for Map view modes. This is deliberately separate from the runtime
    /// host and facade: an outer application coordinator explicitly binds it to a running facade.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SampleMapViewModeUiPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject m_GameplayUiRoot;
        [SerializeField] private GameObject m_TopDownUiRoot;

        private MapViewRuntimeFacade m_Runtime;
        private IDisposable m_ModeChangedSubscription;
        private IDisposable m_RuntimeStoppedSubscription;

        public GameObject GameplayUiRoot { get { return m_GameplayUiRoot; } }
        public GameObject TopDownUiRoot { get { return m_TopDownUiRoot; } }

        public void Configure(GameObject gameplayUiRoot, GameObject topDownUiRoot)
        {
            m_GameplayUiRoot = gameplayUiRoot;
            m_TopDownUiRoot = topDownUiRoot;
        }

        public void Bind(MapViewRuntimeFacade runtime)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (ReferenceEquals(m_Runtime, runtime))
            {
                Synchronize();
                return;
            }

            Unbind();
            m_Runtime = runtime;
            m_ModeChangedSubscription = runtime.SubscribeModeChanged(ShowMode);
            m_RuntimeStoppedSubscription = runtime.SubscribeRuntimeStopped(HideAll);
            Synchronize();
        }

        public void Unbind()
        {
            if (m_ModeChangedSubscription != null)
            {
                m_ModeChangedSubscription.Dispose();
                m_ModeChangedSubscription = null;
            }

            if (m_RuntimeStoppedSubscription != null)
            {
                m_RuntimeStoppedSubscription.Dispose();
                m_RuntimeStoppedSubscription = null;
            }

            m_Runtime = null;
            HideAll();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Synchronize()
        {
            if (m_Runtime != null && m_Runtime.IsRunning)
            {
                ShowMode(m_Runtime.CurrentMode);
                return;
            }

            HideAll();
        }

        private void ShowMode(MapViewMode mode)
        {
            SetRootActive(m_GameplayUiRoot, mode == MapViewMode.Gameplay);
            SetRootActive(m_TopDownUiRoot, mode == MapViewMode.TopDown);
        }

        private void HideAll()
        {
            SetRootActive(m_GameplayUiRoot, false);
            SetRootActive(m_TopDownUiRoot, false);
        }

        private static void SetRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }
    }
}

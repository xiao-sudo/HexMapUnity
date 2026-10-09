using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>A presentation owned by a mode. A single-base-camera mode only presents UI roots.</summary>
    internal readonly struct MapViewPresentation
    {
        private readonly GameObject m_UiRoot;

        private MapViewPresentation(GameObject uiRoot)
        {
            m_UiRoot = uiRoot;
        }

        public static MapViewPresentation OfUiRoot(GameObject uiRoot)
        {
            return new MapViewPresentation(uiRoot);
        }

        public void Hide()
        {
            if (m_UiRoot != null && m_UiRoot.activeSelf)
            {
                m_UiRoot.SetActive(false);
            }
        }
    }
}
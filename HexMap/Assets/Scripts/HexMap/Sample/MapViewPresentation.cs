using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// One thing a mode can turn on. Stored so the driver can turn everything off between modes without
    /// knowing what a mode is, or which camera or root it woke up.
    /// <para>
    /// The UI root is switched off by deactivating the object alone. Deactivating a parent hides its
    /// children as well, so nothing has to be walked; disabling an extra component on the way would be work
    /// nobody asked for.
    /// </para>
    /// </summary>
    internal readonly struct MapViewPresentation
    {
        private readonly Camera m_Camera;
        private readonly GameObject m_UiRoot;

        private MapViewPresentation(Camera camera, GameObject uiRoot)
        {
            m_Camera = camera;
            m_UiRoot = uiRoot;
        }

        /// <summary>A camera this mode may switch on. Switched off by disabling the component.</summary>
        public static MapViewPresentation OfCamera(Camera camera)
        {
            return new MapViewPresentation(camera, null);
        }

        /// <summary>A UI root this mode may switch on. Switched off by deactivating the object.</summary>
        public static MapViewPresentation OfUiRoot(GameObject uiRoot)
        {
            return new MapViewPresentation(null, uiRoot);
        }

        /// <summary>
        /// Turns this presentation off. This is the only direction the driver needs: each mode turns its own
        /// things on through the context, and the driver turns everything off before the next mode starts.
        /// A null slot, which is what a half-wired scene leaves behind, is simply left alone.
        /// </summary>
        public void Hide()
        {
            if (m_Camera != null)
            {
                m_Camera.enabled = false;
            }

            if (m_UiRoot != null && m_UiRoot.activeSelf)
            {
                m_UiRoot.SetActive(false);
            }
        }
    }
}

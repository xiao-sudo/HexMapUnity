using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// What a <see cref="MapViewPresentation"/> stands for. The tag exists so that "how do I switch this
    /// off" is answered in exactly one place: a camera goes off through <c>enabled</c>, a UI root through
    /// <c>SetActive</c>, and the driver that clears the stage must not have to know which it is holding.
    /// </summary>
    internal enum MapViewPresentationKind
    {
        /// <summary>A camera, switched off by disabling the component.</summary>
        Camera,

        /// <summary>A UI root, switched off by deactivating the object.</summary>
        UiRoot,
    }

    /// <summary>
    /// One thing a mode can turn on. Stored so the driver can turn everything off between modes without
    /// knowing what a mode is, or which camera or root it woke up.
    /// <para>
    /// The kind and the target are kept as two fields rather than one field per kind, so that adding a kind
    /// is one enum member, one factory and one branch in <see cref="Hide"/> — instead of a new field that
    /// every existing branch has to be re-checked against. The failure that avoids is a quiet one: a
    /// presentation whose kind has no branch would simply never be switched off, and that only shows up much
    /// later as two modes presenting at once.
    /// </para>
    /// <para>
    /// The UI root is switched off by deactivating the object alone. Deactivating a parent hides its
    /// children as well, so nothing has to be walked; disabling an extra component on the way would be work
    /// nobody asked for.
    /// </para>
    /// </summary>
    internal readonly struct MapViewPresentation
    {
        private readonly MapViewPresentationKind m_Kind;
        private readonly Object m_Target;

        private MapViewPresentation(MapViewPresentationKind kind, Object target)
        {
            m_Kind = kind;
            m_Target = target;
        }

        /// <summary>A camera this mode may switch on. Switched off by disabling the component.</summary>
        public static MapViewPresentation OfCamera(Camera camera)
        {
            return new MapViewPresentation(MapViewPresentationKind.Camera, camera);
        }

        /// <summary>A UI root this mode may switch on. Switched off by deactivating the object.</summary>
        public static MapViewPresentation OfUiRoot(GameObject uiRoot)
        {
            return new MapViewPresentation(MapViewPresentationKind.UiRoot, uiRoot);
        }

        /// <summary>
        /// Turns this presentation off. This is the only direction the driver needs: each mode turns its own
        /// things on through the context, and the driver turns everything off before the next mode starts.
        /// A slot that was never filled, which is what a half-wired scene leaves behind, is simply left
        /// alone.
        /// </summary>
        public void Hide()
        {
            switch (m_Kind)
            {
                case MapViewPresentationKind.Camera:
                    var camera = m_Target as Camera;
                    if (camera != null)
                    {
                        camera.enabled = false;
                    }

                    return;

                case MapViewPresentationKind.UiRoot:
                    var root = m_Target as GameObject;
                    if (root != null && root.activeSelf)
                    {
                        root.SetActive(false);
                    }

                    return;

                default:
                    // A kind with no branch here would leave whatever it stands for switched on, silently.
                    // Doing nothing keeps a half-wired scene from throwing; the missing branch is the defect.
                    return;
            }
        }
    }
}

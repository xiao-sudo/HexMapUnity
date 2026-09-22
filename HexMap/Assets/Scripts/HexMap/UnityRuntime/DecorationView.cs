using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Renders one decoration (or one overlay) from a Sprite, as a prefab the artist drops in the scene.
    /// </summary>
    /// <remarks>
    /// The prefab is the authoring surface: a root GameObject carrying this component and a child
    /// GameObject carrying the <see cref="MeshFilter"/> and <see cref="MeshRenderer"/>. All geometry
    /// and material work is delegated to <see cref="DecorationMeshFactory"/> and
    /// <see cref="DecorationMaterialCache"/>, so this component only owns component semantics:
    /// reading its serialized fields, applying them, and reacting to changes.
    ///
    /// The layer is expressed as a render queue. Decorations use <see cref="DecorationQueue.Decoration"/>
    /// so the HexMap covers them; an overlay is the same prefab with the queue set to
    /// <see cref="DecorationQueue.Overlay"/>, which needs no separate shader or component.
    ///
    /// <see cref="ExecuteAlways"/> keeps the decoration visible in the scene view without entering
    /// play mode. That is why the generated mesh carries <see cref="HideFlags.DontSave"/>: Unity
    /// would otherwise persist it into the scene or prefab file as an unexplained sub-asset.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DecorationView : MonoBehaviour
    {
        private static readonly string s_ShaderName = "HexMap/Decoration";

        [SerializeField] private Sprite m_Sprite;
        [SerializeField] private int m_Queue = DecorationQueue.Decoration;
        [SerializeField] private int m_SortingOrder;
        [SerializeField] private bool m_Visible = true;

        [Tooltip("The child that carries the MeshFilter and MeshRenderer. Found automatically when left empty.")]
        [SerializeField] private MeshFilter m_MeshFilter;

        [Tooltip("Optional. Leave empty to look the decoration shader up by name; assign it to survive shader stripping in a build.")]
        [SerializeField] private Shader m_Shader;

        private MeshRenderer m_Renderer;
        private bool m_ReportedMissingFilter;

        /// <summary>The Sprite this decoration renders. Setting it rebuilds the mesh.</summary>
        public Sprite Sprite
        {
            get { return m_Sprite; }
            set
            {
                if (m_Sprite == value)
                {
                    return;
                }

                m_Sprite = value;
                Apply();
            }
        }

        /// <summary>
        /// The render queue this decoration is drawn in, set on the prefab. Decorations use
        /// <see cref="DecorationQueue.Decoration"/>; an overlay is the same prefab with this set to
        /// <see cref="DecorationQueue.Overlay"/>.
        /// </summary>
        /// <remarks>
        /// Read-only on purpose. A Material carries exactly one queue and cached Materials are
        /// shared by every decoration using the same texture, so changing this derives a new
        /// Material that then stays resident in <see cref="DecorationMaterialCache"/> for the rest
        /// of the session. Exposing a setter would invite a per-frame change that leaks one
        /// Material per value. To retarget a decoration, edit the field on the prefab instead.
        /// </remarks>
        public int Queue
        {
            get { return m_Queue; }
        }

        /// <summary>Ordering inside the queue. Only nudges placement between decorations.</summary>
        public int SortingOrder
        {
            get { return m_SortingOrder; }
            set
            {
                if (m_SortingOrder == value)
                {
                    return;
                }

                m_SortingOrder = value;
                Apply();
            }
        }

        /// <summary>
        /// Whether this decoration is drawn. Toggling it only switches the renderer, so a hidden
        /// decoration keeps its geometry, material and ordering and re-showing it rebuilds nothing.
        /// </summary>
        public bool Visible
        {
            get { return m_Visible; }
            set
            {
                if (m_Visible == value)
                {
                    return;
                }

                m_Visible = value;
                Apply();
            }
        }

        /// <summary>True once the decoration has geometry and a material to draw with.</summary>
        public bool IsReady
        {
            get { return m_Renderer != null && m_Renderer.sharedMaterial != null; }
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        /// <summary>
        /// Rebuilds geometry and material from the current fields, then applies the render settings.
        /// Safe to call when nothing changed; Unity calls it on enable and on inspector edits.
        /// </summary>
        public void Apply()
        {
            var filter = ResolveMeshFilter();
            var renderer = ResolveRenderer(filter);
            if (filter == null || renderer == null)
            {
                // Reported once, not on every inspector edit.
                if (!m_ReportedMissingFilter)
                {
                    m_ReportedMissingFilter = true;
                    Debug.LogError(
                        "A DecorationView needs a MeshRenderer. Give the prefab a child GameObject " +
                        "holding a MeshFilter and MeshRenderer, and assign it, or leave the field " +
                        "empty when that child is the only MeshFilter below this object.",
                        this);
                }

                return;
            }

            m_ReportedMissingFilter = false;

            var mesh = DecorationMeshFactory.GetOrCreateMesh(m_Sprite);
            if (mesh == null)
            {
                // No Sprite yet is a legitimate state for a freshly created prefab, not an error:
                // report nothing and leave the renderer off.
                renderer.enabled = false;
                return;
            }

            if (filter.sharedMesh != mesh)
            {
                filter.sharedMesh = mesh;
            }

            var texture = ResolveTexture(m_Sprite);
            var material = DecorationMaterialCache.GetOrCreateMaterial(texture, ResolveShader(), m_Queue);
            if (material == null)
            {
                // Reported by the cache. Disable rather than throw, so one misconfigured decoration
                // cannot take the scene down.
                renderer.enabled = false;
                return;
            }

            if (renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;
            }

            DecorationRenderSettings.Apply(renderer);
            renderer.sortingOrder = m_SortingOrder;
            renderer.enabled = m_Visible;
        }

        private MeshFilter ResolveMeshFilter()
        {
            if (m_MeshFilter == null)
            {
                m_MeshFilter = GetComponent<MeshFilter>();
            }

            if (m_MeshFilter == null)
            {
                m_MeshFilter = GetComponentInChildren<MeshFilter>(true);
            }

            return m_MeshFilter;
        }

        private MeshRenderer ResolveRenderer(MeshFilter filter)
        {
            if (m_Renderer == null && filter != null)
            {
                m_Renderer = filter.GetComponent<MeshRenderer>();
            }

            return m_Renderer;
        }

        private Shader ResolveShader()
        {
            if (m_Shader == null)
            {
                m_Shader = Shader.Find(s_ShaderName);
            }

            return m_Shader;
        }

        private static Texture2D ResolveTexture(Sprite sprite)
        {
            return sprite == null ? null : sprite.texture;
        }
    }
}

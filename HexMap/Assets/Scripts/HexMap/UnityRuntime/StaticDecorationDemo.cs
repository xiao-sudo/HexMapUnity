using HexMap.Core;
using HexMap.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.UnityRuntime
{
    /// <summary>A reproducible scene fixture, not the production map composition root.</summary>
    public sealed class StaticDecorationDemo : MonoBehaviour
    {
        [SerializeField] private Shader m_DecorationShader;
        [SerializeField] private Shader m_HexShader;
        [SerializeField] private Texture2D m_Image;
        [SerializeField, Min(0)] private int m_MapRadius = 1;
        [SerializeField] private bool m_DecorationsVisible = true;
        [SerializeField] private StaticDecorationPlacement[] m_Placements =
        {
            new StaticDecorationPlacement(new Vector3(-2.5f, 0f, 0.2f), Quaternion.identity, new Vector3(2.2f, 5f, 1f)),
            new StaticDecorationPlacement(new Vector3(0f, 0f, 0.2f), Quaternion.identity, new Vector3(2.2f, 5f, 1f)),
            new StaticDecorationPlacement(new Vector3(2.5f, 0f, 0.2f), Quaternion.identity, new Vector3(2.2f, 5f, 1f))
        };

        private StaticDecorationRenderer m_Decorations;
        private HexMapRenderer m_Map;
        private Material m_DecorationMaterial;
        private Material m_HexMaterial;
        private Texture2D m_GeneratedImage;

        public void ConfigureShaders(Shader decorationShader, Shader hexShader)
        {
            m_DecorationShader = decorationShader;
            m_HexShader = hexShader;
        }

        private void Start()
        {
            if (m_DecorationShader == null || m_HexShader == null)
            {
                Debug.LogError("Assign both shaders, or create the scene using HexMap/Demos/Create Static Decoration Scene.", this);
                enabled = false;
                return;
            }
            if (!GraphicsSettings.useScriptableRenderPipelineBatching)
                Debug.LogWarning("Enable SRP Batcher in the active URP asset before measuring the decoration demo.", this);

            m_DecorationMaterial = new Material(m_DecorationShader) { name = "Demo Shared Decoration" };
            if (m_Image == null) m_GeneratedImage = CreateFeatheredImage();
            m_DecorationMaterial.SetTexture("_BaseMap", m_Image != null ? m_Image : m_GeneratedImage);
            m_Decorations = new StaticDecorationRenderer(transform, m_DecorationMaterial, m_Placements, gameObject.layer);
            m_Decorations.Visible = m_DecorationsVisible;

            m_HexMaterial = new Material(m_HexShader) { name = "Demo Instanced Hex" };
            m_HexMaterial.SetFloat("_InteriorAlpha", 1f);
            m_Map = new HexMapRenderer(new RuntimeHexMap(new HexMapDefinition(m_MapRadius)),
                new HexLayout(HexOrientation.Pointy, HexPlane.XY, 1f, Vector3.zero),
                new HexMapRenderConfig(transform, m_HexMaterial, gameObject.layer, Color.red,
                    HexMapRenderStrategy.DrawMeshInstanced));
            HexView center;
            if (m_Map.TryGetHexView(new HexCoord(0, 0), out center))
                center.SetAppearance(new HexAppearance(true, new Color(1f, 0f, 0f, 0.5f), false));
        }

        private void LateUpdate()
        {
            if (m_Decorations != null && m_Decorations.Visible != m_DecorationsVisible)
                m_Decorations.Visible = m_DecorationsVisible;
            if (m_Map != null) m_Map.Render();
        }

        private void OnEnable()
        {
            if (m_Decorations != null) m_Decorations.Visible = m_DecorationsVisible;
        }

        private void OnDisable()
        {
            if (m_Decorations != null) m_Decorations.Visible = false;
        }

        private static Texture2D CreateFeatheredImage()
        {
            const int size = 64;
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Demo Feathered Green Image",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    pixels[y * size + x] = new Color(0f, 1f, 0f, Mathf.SmoothStep(0f, 1f, edge / 8f));
                }
            }
            image.SetPixels(pixels);
            image.Apply(false, true);
            return image;
        }

        private void OnDestroy()
        {
            if (m_Map != null) m_Map.Dispose();
            if (m_Decorations != null) m_Decorations.Dispose();
            Destroy(m_DecorationMaterial);
            Destroy(m_HexMaterial);
            Destroy(m_GeneratedImage);
        }
    }
}

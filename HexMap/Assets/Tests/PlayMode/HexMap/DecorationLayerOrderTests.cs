using System.Collections;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.UnityRuntime.Tests
{
    /// <summary>
    /// Pins the cross-layer ordering: decorations draw before the HexMap and overlays after it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is measured, not assumed. The project's render-order reference once derived the sort key
    /// as <c>sortingLayer → renderQueue → sortingOrder</c> and labelled that derivation as inference.
    /// A probe falsified it: <c>sortingOrder</c> outranks <c>renderQueue</c>, so a decoration at
    /// queue 2800 given <c>sortingOrder = 1</c> drew over a HexMap at queue 3000. The key is really
    /// <c>sortingLayer → sortingOrder → renderQueue</c>.
    /// </para>
    /// <para>
    /// The decoration component therefore writes no <c>sortingOrder</c> at all, and these tests are
    /// what notices if that changes. Each case draws one opaque rectangle against the opaque HexMap
    /// and reads the centre pixel, so the answer comes from the screen rather than from reasoning
    /// about sort flags.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class DecorationLayerOrderTests
    {
        private const int Layer = 31;
        private const int Resolution = 256;

        private readonly List<Object> m_Created = new List<Object>();

        private GameObject m_Root;
        private Camera m_Camera;
        private RenderTexture m_Target;
        private Texture2D m_Readback;
        private Material m_HexMaterial;
        private HexMapRenderer m_Map;

        [SetUp]
        public void SetUp()
        {
            DecorationMeshFactory.Clear();
            DecorationMaterialCache.Clear();

            m_Root = new GameObject("Decoration layer order");

            var cameraObject = new GameObject("Test camera");
            cameraObject.transform.SetParent(m_Root.transform, false);
            m_Camera = cameraObject.AddComponent<Camera>();
            m_Camera.transform.position = new Vector3(0f, 0f, 10f);
            m_Camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            m_Camera.orthographic = true;
            m_Camera.orthographicSize = 3f;
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = Color.black;
            m_Camera.allowHDR = false;
            m_Camera.allowMSAA = false;
            m_Camera.cullingMask = 1 << Layer;

            m_Target = new RenderTexture(
                Resolution, Resolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            m_Target.Create();
            m_Camera.targetTexture = m_Target;

            m_Readback = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Camera != null)
            {
                m_Camera.targetTexture = null;
            }

            if (m_Map != null)
            {
                m_Map.Dispose();
                m_Map = null;
            }

            if (m_Target != null)
            {
                m_Target.Release();
            }

            for (var index = 0; index < m_Created.Count; index++)
            {
                if (m_Created[index] != null)
                {
                    Object.DestroyImmediate(m_Created[index]);
                }
            }

            m_Created.Clear();

            if (m_HexMaterial != null)
            {
                Object.DestroyImmediate(m_HexMaterial);
                m_HexMaterial = null;
            }

            if (m_Root != null)
            {
                Object.DestroyImmediate(m_Root);
                m_Root = null;
            }

            if (m_Target != null)
            {
                Object.DestroyImmediate(m_Target);
                m_Target = null;
            }

            if (m_Readback != null)
            {
                Object.DestroyImmediate(m_Readback);
                m_Readback = null;
            }

            DecorationMeshFactory.Clear();
            DecorationMaterialCache.Clear();
        }

        [UnityTest]
        public IEnumerator DecorationStaysBelowTheHexMapAndOverlayStaysAbove()
        {
            Assert.That(
                m_HexMaterial == null,
                Is.True,
                "the map is built below so the material queue can be asserted first");

            var hexShader = Shader.Find("HexMap/InstancedHexCell");
            Assert.That(hexShader, Is.Not.Null);
            m_HexMaterial = new Material(hexShader);
            m_HexMaterial.SetFloat("_InteriorAlpha", 1f);

            Assert.That(
                m_HexMaterial.renderQueue,
                Is.EqualTo(DecorationQueue.HexMap),
                "the layering contract names the queue the hex shader must own");

            var decoration = CreateRectangle(DecorationQueue.Decoration, Color.blue, new Vector3(-1.5f, 0f, 0f));
            var overlay = CreateRectangle(DecorationQueue.Overlay, Color.green, new Vector3(1.5f, 0f, 0f));

            // The component must not write sortingOrder: it outranks renderQueue, so any non-zero
            // value would let a decoration cross the HexMap's layer.
            Assert.That(
                decoration.GetComponentInChildren<MeshRenderer>(true).sortingOrder,
                Is.EqualTo(0),
                "a decoration must leave sortingOrder at its default");
            Assert.That(
                overlay.GetComponentInChildren<MeshRenderer>(true).sortingOrder,
                Is.EqualTo(0),
                "an overlay must leave sortingOrder at its default");

            BuildHexMap(Color.red);

            yield return null;
            m_Map.Render();
            yield return new WaitForEndOfFrame();
            ReadFrame();

            // The Hex is opaque and covers the decoration's half; the overlay is opaque and covers
            // the Hex on its own half.
            AssertPixel(new Vector3(-1.5f, 0f, 0f), Color.red, "the HexMap must cover the decoration");
            AssertPixel(new Vector3(1.5f, 0f, 0f), Color.green, "the overlay must cover the HexMap");
        }

        private DecorationView CreateRectangle(int queue, Color colour, Vector3 position)
        {
            var texture = new Texture2D(4, 1, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(new[] { colour, colour, colour, colour });
            texture.Apply();
            m_Created.Add(texture);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 4f, 1f),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect);
            m_Created.Add(sprite);

            var root = new GameObject("Rectangle " + queue);
            root.transform.SetParent(m_Root.transform, false);
            root.transform.position = new Vector3(position.x, position.y, -0.2f);
            root.layer = Layer;

            var child = new GameObject("Renderer");
            child.transform.SetParent(root.transform, false);
            child.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            child.layer = Layer;
            child.AddComponent<MeshFilter>();
            child.AddComponent<MeshRenderer>();

            var view = root.AddComponent<DecorationView>();
            view.Sprite = sprite;
            SetQueue(view, queue);
            view.Apply();

            Assert.That(view.IsReady, Is.True);
            return view;
        }

        private void BuildHexMap(Color appearance)
        {
            m_Map = new HexMapRenderer(
                new RuntimeHexMap(new HexMapDefinition(0)),
                new HexLayout(HexOrientation.Pointy, HexPlane.XY, 1f, Vector3.zero),
                new HexMapRenderConfig(
                    m_Root.transform,
                    m_HexMaterial,
                    Layer,
                    appearance,
                    HexMapRenderStrategy.DrawMeshInstanced));
        }

        /// <summary>
        /// Writes the queue the way the prefab asset would. The component exposes no queue setter
        /// because changing it derives a Material that stays resident for the session.
        /// </summary>
        private static void SetQueue(DecorationView view, int queue)
        {
            var field = typeof(DecorationView).GetField(
                "m_Queue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "DecorationView must keep a serialized queue field");
            field.SetValue(view, queue);
        }

        private void ReadFrame()
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = m_Target;
                m_Readback.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                m_Readback.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private void AssertPixel(Vector3 world, Color expected, string message)
        {
            var viewport = m_Camera.WorldToViewportPoint(world);
            var actual = m_Readback.GetPixel(
                Mathf.FloorToInt(viewport.x * Resolution),
                Mathf.FloorToInt(viewport.y * Resolution));

            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.06f), message + " (red)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.06f), message + " (green)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.06f), message + " (blue)");
        }
    }
}

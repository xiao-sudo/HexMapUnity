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
    /// Because <c>sortingOrder</c> outranks <c>renderQueue</c>, it is the only knob that can order
    /// these bands, and it is the one the decoration component writes. That also makes it the one
    /// field that can silently break the layering: a decoration ordered at or above the HexMap
    /// baseline draws over the map. These tests check both the assigned band values and the result
    /// on screen, so a changed constant or a changed renderer setting shows up as a failing pixel
    /// rather than as a decoration that quietly covers the map.
    /// </para>
    /// <para>
    /// Each case draws one opaque rectangle against the opaque HexMap and reads the centre pixel, so
    /// the answer comes from the screen rather than from reasoning about sort flags.
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

            var decoration = CreateRectangle(
                DecorationQueue.Decoration,
                DecorationQueue.DecorationSortingOrder,
                Color.blue,
                new Vector3(-1.5f, 0f, 0f));
            var overlay = CreateRectangle(
                DecorationQueue.Overlay,
                DecorationQueue.OverlaySortingOrder,
                Color.green,
                new Vector3(1.5f, 0f, 0f));

            // The bands are separated by sorting order. It outranks the render queue, so a
            // decoration at or above the HexMap baseline would draw over the map.
            var decorationRenderer = decoration.GetComponentInChildren<MeshRenderer>(true);
            var overlayRenderer = overlay.GetComponentInChildren<MeshRenderer>(true);

            Assert.That(
                decorationRenderer.sortingOrder,
                Is.EqualTo(DecorationQueue.DecorationSortingOrder),
                "a decoration must take the decoration band");
            Assert.That(
                decorationRenderer.sortingOrder,
                Is.LessThan(DecorationQueue.HexMapSortingOrder),
                "a decoration must sit below the HexMap baseline");
            Assert.That(
                overlayRenderer.sortingOrder,
                Is.EqualTo(DecorationQueue.OverlaySortingOrder),
                "an overlay must take the overlay band");
            Assert.That(
                overlayRenderer.sortingOrder,
                Is.GreaterThan(DecorationQueue.HexMapSortingOrder),
                "an overlay must sit above the HexMap baseline");

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

        private DecorationView CreateRectangle(
            int queue,
            int sortingOrder,
            Color colour,
            Vector3 position)
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
            SetSerializedField(view, "m_Queue", queue);
            SetSerializedField(view, "m_SortingOrder", sortingOrder);
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
        /// Writes a serialized field the way the prefab asset would. The component exposes no setters
        /// for the queue or the sorting order: both decide which Material is derived and which band
        /// the decoration joins, so they are placement settings rather than runtime switches.
        /// </summary>
        private static void SetSerializedField(DecorationView view, string name, object value)
        {
            var field = typeof(DecorationView).GetField(
                name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "DecorationView must keep a serialized field named " + name);
            field.SetValue(view, value);
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

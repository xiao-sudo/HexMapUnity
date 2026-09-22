using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HexMap.Core;
using HexMap.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.UnityRuntime.Tests
{
    /// <summary>
    /// Renders decorations and a HexMap into a RenderTexture and reads pixels back, so the layering
    /// is asserted from what reaches the screen rather than from what the code says it does.
    /// </summary>
    /// <remarks>
    /// Decoration layer: 31 only, so nothing else in the scene can contribute a pixel. The camera is
    /// orthographic and faces the XY plane, which is the plane the shared hex mesh is generated for.
    /// </remarks>
    [TestFixture]
    public sealed class DecorationRenderingTests
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

            m_Root = new GameObject("Decoration rendering test");

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
        public IEnumerator HexCoversTheDecorationAndCameraMovementDoesNotBreakIt()
        {
            Assert.That(SystemInfo.supportsInstancing, Is.True, "Use a graphics-enabled Unity editor.");

            // Blue at half alpha on the left half, opaque blue on the right half.
            var sprite = CreateSprite(
                4, 1,
                new[] { Color.clear, new Color(0f, 0f, 1f, 0.5f), Color.blue, Color.blue });

            CreateDrawableDecoration(sprite, DecorationQueue.Decoration, new Vector3(2f, 4f, 1f));
            BuildHexMap(new Color(1f, 0f, 0f, 0.5f));

            yield return null;
            m_Map.Render();
            yield return new WaitForEndOfFrame();
            ReadFrame();

            AssertRgb(new Vector3(0.25f, 0f, 0f), new Color(0.5f, 0f, 0.5f), "half-transparent Hex over decoration");
            AssertRgb(new Vector3(-1.5f, 0f, 0f), new Color(0f, 0f, 0.5f), "partial texture alpha outside Hex");

            HexView cell;
            Assert.That(m_Map.TryGetHexView(new HexCoord(0, 0), out cell), Is.True);
            cell.SetAppearance(new HexAppearance(true, Color.red, false));
            m_Camera.transform.position += new Vector3(0.15f, 0.1f, 0f);
            m_Camera.orthographicSize = 2.7f;
            yield return null;
            m_Map.Render();
            yield return new WaitForEndOfFrame();
            ReadFrame();

            AssertRgb(new Vector3(0.25f, 0f, 0f), Color.red, "opaque Hex covers decoration after pan and zoom");
            AssertRgb(new Vector3(-1.5f, 0f, 0f), new Color(0f, 0f, 0.5f), "decoration stays fixed when the camera moves");
        }

        [UnityTest]
        public IEnumerator HiddenDecorationDrawsNothingAndComesBackUnchanged()
        {
            var sprite = CreateSprite(
                4, 1,
                new[] { Color.blue, Color.blue, Color.blue, Color.blue });
            var view = CreateDrawableDecoration(sprite, DecorationQueue.Decoration, new Vector3(2f, 4f, 1f));

            yield return null;
            yield return new WaitForEndOfFrame();
            ReadFrame();
            AssertRgb(Vector3.zero, Color.blue, "visible decoration draws");

            view.Visible = false;
            yield return null;
            yield return new WaitForEndOfFrame();
            ReadFrame();
            AssertRgb(Vector3.zero, Color.black, "hidden decoration draws nothing");

            view.Visible = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            ReadFrame();
            AssertRgb(Vector3.zero, Color.blue, "re-shown decoration draws again");
            Assert.That(view.IsReady, Is.True, "toggling visibility must not tear down geometry or material");
        }

        [UnityTest]
        public IEnumerator AnOverlayIsOpaqueAndCoversTheHexMap()
        {
            var sprite = CreateSprite(
                4, 1,
                new[] { Color.green, Color.green, Color.green, Color.green });

            // Same prefab, same shader, only the queue differs: this is what makes it an overlay.
            var overlay = CreateDrawableDecoration(sprite, DecorationQueue.Overlay, new Vector3(2f, 4f, 1f));
            Assert.That(overlay.Queue, Is.EqualTo(DecorationQueue.Overlay));

            BuildHexMap(new Color(1f, 0f, 0f, 1f));

            yield return null;
            m_Map.Render();
            yield return new WaitForEndOfFrame();
            ReadFrame();

            AssertRgb(Vector3.zero, Color.green, "an opaque overlay draws over an opaque Hex");
        }

        private DecorationView CreateDrawableDecoration(Sprite sprite, int queue, Vector3 localScale)
        {
            var root = new GameObject("Decoration " + queue);
            root.transform.SetParent(m_Root.transform, false);
            root.transform.position = new Vector3(0f, 0f, -0.2f);
            root.layer = Layer;

            var child = new GameObject("Renderer");
            child.transform.SetParent(root.transform, false);
            child.transform.localScale = localScale;
            child.layer = Layer;
            var filter = child.AddComponent<MeshFilter>();
            child.AddComponent<MeshRenderer>();

            var view = root.AddComponent<DecorationView>();
            view.Sprite = sprite;
            SetQueue(view, queue);
            view.Apply();

            Assert.That(view.IsReady, Is.True, "the decoration component must resolve its child MeshRenderer");
            Assert.That(filter.sharedMesh, Is.Not.Null, "the decoration component must assign a mesh");
            return view;
        }

        /// <summary>
        /// Writes the queue the way the prefab asset would, by setting the field rather than a
        /// setter. The component exposes no queue setter on purpose: changing the queue derives
        /// another Material that stays resident for the session, so it is a prefab setting, not
        /// something to drive at runtime.
        /// </summary>
        private static void SetQueue(DecorationView view, int queue)
        {
            var field = typeof(DecorationView).GetField(
                "m_Queue", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "DecorationView must keep a serialized queue field");
            field.SetValue(view, queue);
        }

        private void BuildHexMap(Color appearance)
        {
            var hexShader = Shader.Find("HexMap/InstancedHexCell");
            Assert.That(hexShader, Is.Not.Null);
            m_HexMaterial = new Material(hexShader);
            m_HexMaterial.SetFloat("_InteriorAlpha", 1f);

            m_Map = new HexMapRenderer(
                new RuntimeHexMap(new HexMapDefinition(0)),
                new HexLayout(HexOrientation.Pointy, HexPlane.XY, 1f, Vector3.zero),
                new HexMapRenderConfig(
                    m_Root.transform, m_HexMaterial, Layer, appearance, HexMapRenderStrategy.DrawMeshInstanced));
        }

        private Sprite CreateSprite(int width, int height, Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply();
            m_Created.Add(texture);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect);
            m_Created.Add(sprite);
            return sprite;
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

        private void AssertRgb(Vector3 world, Color expected, string message)
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

using System.Collections;
using HexMap.Core;
using HexMap.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.UnityRuntime.Tests
{
    public sealed class StaticDecorationRenderingTests
    {
        private GameObject m_Root;
        private Camera m_Camera;
        private RenderTexture m_Target;
        private Texture2D m_Readback;
        private Texture2D m_Image;
        private Material m_DecorationMaterial;
        private Material m_HexMaterial;
        private StaticDecorationRenderer m_Decorations;
        private HexMapRenderer m_Map;

        [UnityTest]
        public IEnumerator TransparentEdgesAndHexCoverageSurviveCameraMovement()
        {
            var decorationShader = Shader.Find("HexMap/StaticDecoration");
            var hexShader = Shader.Find("HexMap/InstancedHexCell");
            Assert.That(decorationShader, Is.Not.Null);
            Assert.That(hexShader, Is.Not.Null);
            Assert.That(SystemInfo.supportsInstancing, Is.True, "Use a graphics-enabled Unity editor.");

            m_Root = new GameObject("Decoration rendering test");
            var cameraObject = new GameObject("Test camera");
            cameraObject.transform.SetParent(m_Root.transform, false);
            m_Camera = cameraObject.AddComponent<Camera>();
            m_Camera.transform.position = new Vector3(0f, 0f, -10f);
            m_Camera.orthographic = true;
            m_Camera.orthographicSize = 3f;
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = Color.black;
            m_Camera.allowHDR = false;
            m_Camera.allowMSAA = false;
            m_Camera.cullingMask = 1 << 31;
            m_Target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            m_Target.Create();
            m_Camera.targetTexture = m_Target;
            m_Readback = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
            m_Image = new Texture2D(4, 1, TextureFormat.RGBA32, false, true);
            m_Image.filterMode = FilterMode.Point;
            m_Image.wrapMode = TextureWrapMode.Clamp;
            m_Image.SetPixels(new[] { Color.clear, new Color(0f, 0f, 1f, 0.5f), Color.blue, Color.blue });
            m_Image.Apply();
            m_DecorationMaterial = new Material(decorationShader);
            m_DecorationMaterial.SetTexture("_BaseMap", m_Image);
            m_HexMaterial = new Material(hexShader);
            m_HexMaterial.SetFloat("_InteriorAlpha", 1f);

            // Deliberately nearer the camera than the Hex: queue ordering must still win.
            m_Decorations = new StaticDecorationRenderer(m_Root.transform, m_DecorationMaterial,
                new[] { new StaticDecorationPlacement(new Vector3(0f, 0f, -0.2f), Quaternion.identity, new Vector3(8f, 4f, 1f)) }, 31);
            m_Map = new HexMapRenderer(new RuntimeHexMap(new HexMapDefinition(0)),
                new HexLayout(HexOrientation.Pointy, HexPlane.XY, 1f, Vector3.zero),
                new HexMapRenderConfig(m_Root.transform, m_HexMaterial, 31,
                    new Color(1f, 0f, 0f, 0.5f), HexMapRenderStrategy.DrawMeshInstanced));

            yield return null;
            m_Map.Render();
            yield return new WaitForEndOfFrame();
            ReadFrame();
            AssertRgb(new Vector3(0.25f, 0f, 0f), new Color(0.5f, 0f, 0.5f), "Half-transparent Hex over decoration");
            AssertRgb(new Vector3(-1.5f, 0f, 0f), new Color(0f, 0f, 0.5f), "Partial texture alpha outside Hex");

            HexView cell;
            Assert.That(m_Map.TryGetHexView(new HexCoord(0, 0), out cell), Is.True);
            cell.SetAppearance(new HexAppearance(true, Color.red, false));
            m_Camera.transform.position += new Vector3(0.15f, 0.1f, 0f);
            m_Camera.orthographicSize = 2.7f;
            yield return null;
            m_Map.Render();
            yield return new WaitForEndOfFrame();
            ReadFrame();
            AssertRgb(new Vector3(0.25f, 0f, 0f), Color.red, "Opaque Hex covers decoration after pan and zoom");
            AssertRgb(new Vector3(-1.5f, 0f, 0f), new Color(0f, 0f, 0.5f), "Decoration remains fixed after camera movement");
        }

        private void ReadFrame()
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = m_Target;
                m_Readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
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
            var actual = m_Readback.GetPixel(Mathf.FloorToInt(viewport.x * 256), Mathf.FloorToInt(viewport.y * 256));
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.06f), message + " (red)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.06f), message + " (green)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.06f), message + " (blue)");
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Camera != null) m_Camera.targetTexture = null;
            if (m_Map != null) m_Map.Dispose();
            if (m_Decorations != null) m_Decorations.Dispose();
            if (m_Target != null) m_Target.Release();
            Object.DestroyImmediate(m_Root);
            Object.DestroyImmediate(m_Target);
            Object.DestroyImmediate(m_Readback);
            Object.DestroyImmediate(m_Image);
            Object.DestroyImmediate(m_DecorationMaterial);
            Object.DestroyImmediate(m_HexMaterial);
        }
    }
}

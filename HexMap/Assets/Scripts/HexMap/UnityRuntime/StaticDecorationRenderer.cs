using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Owns a static group of independent renderers sharing one quad and material.
    /// The caller owns the material and texture, and disposes this group before them.
    /// </summary>
    public sealed class StaticDecorationRenderer : IDisposable
    {
        private GameObject m_Root;
        private Mesh m_Quad;

        public StaticDecorationRenderer(Transform parent, Material material,
            IReadOnlyList<StaticDecorationPlacement> placements, int layer = 0)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            if (layer < 0 || layer > 31) throw new ArgumentOutOfRangeException(nameof(layer));
            if (material.shader.name != "HexMap/StaticDecoration" || material.renderQueue != 2900)
                throw new ArgumentException("Use HexMap/StaticDecoration at queue 2900, before HexMap's queue 3000.", nameof(material));

            try
            {
                m_Root = new GameObject("Static Decorations");
                m_Root.SetActive(false);
                m_Root.transform.SetParent(parent, false);
                m_Quad = new Mesh
                {
                    name = "Shared Static Decoration Quad",
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                        new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
                    },
                    uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                    triangles = new[] { 0, 2, 1, 0, 3, 2 }
                };
                m_Quad.RecalculateBounds();
                for (var index = 0; index < placements.Count; index++)
                {
                    var placement = placements[index];
                    var child = new GameObject("Decoration " + index);
                    child.layer = layer;
                    child.transform.SetParent(m_Root.transform, false);
                    child.transform.localPosition = placement.Position;
                    child.transform.localRotation = placement.Rotation;
                    child.transform.localScale = placement.Scale;
                    child.AddComponent<MeshFilter>().sharedMesh = m_Quad;
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                }
                m_Root.SetActive(true);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool Visible
        {
            get { return m_Root != null && m_Root.activeSelf; }
            set
            {
                if (m_Root == null) throw new ObjectDisposedException(nameof(StaticDecorationRenderer));
                m_Root.SetActive(value);
            }
        }

        public void Dispose()
        {
            if (m_Root != null)
            {
                m_Root.SetActive(false);
                DestroyOwnedObject(m_Root);
                m_Root = null;
            }
            if (m_Quad != null)
            {
                DestroyOwnedObject(m_Quad);
                m_Quad = null;
            }
        }

        private static void DestroyOwnedObject(UnityEngine.Object target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Derives and caches the Material that renders one texture at one render queue.
    /// </summary>
    /// <remarks>
    /// Materials are derived, never authored: the queue numbers come from <see cref="DecorationQueue"/>
    /// and the texture from the Sprite, so a derived Material has no artistic knobs and does not
    /// belong in version control. The cache key must include the queue, because a Material carries
    /// exactly one render queue: caching by texture alone would let a decoration Material and an
    /// overlay Material overwrite each other's queue.
    /// </remarks>
    public static class DecorationMaterialCache
    {
        private static readonly int s_BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");

        private static readonly Dictionary<MaterialKey, Material> s_Materials =
            new Dictionary<MaterialKey, Material>();

        /// <summary>
        /// Returns the cached Material for this texture and queue, creating it on first use.
        /// Returns null when the inputs are unusable.
        /// </summary>
        public static Material GetOrCreateMaterial(Texture2D texture, Shader shader, int queue)
        {
            if (texture == null)
            {
                Debug.LogError("A decoration needs a Sprite backed by a Texture2D.");
                return null;
            }

            if (shader == null)
            {
                Debug.LogError(
                    "The decoration shader is unavailable. It is probably stripped from the build: " +
                    "assign it on the DecorationView component so the build keeps a reference to it.");
                return null;
            }

            var key = new MaterialKey(texture, queue);
            Material cached;
            if (s_Materials.TryGetValue(key, out cached))
            {
                return cached;
            }

            var material = new Material(shader)
            {
                name = "Decoration - " + texture.name + " @" + queue,
                hideFlags = HideFlags.DontSave,
                renderQueue = queue
            };
            material.SetTexture(s_BaseMapId, texture);
            material.SetColor(s_BaseColorId, Color.white);

            s_Materials.Add(key, material);
            return material;
        }

        /// <summary>
        /// Destroys every cached Material. Call it on play-mode exit and from test teardown; the
        /// cache outlives any single component, so no component may dispose it.
        /// </summary>
        public static void Clear()
        {
            foreach (var material in s_Materials.Values)
            {
                DestroyObject(material);
            }

            s_Materials.Clear();
        }

        private readonly struct MaterialKey : IEquatable<MaterialKey>
        {
            private readonly int m_TextureId;
            private readonly int m_Queue;

            public MaterialKey(Texture2D texture, int queue)
            {
                m_TextureId = texture.GetInstanceID();
                m_Queue = queue;
            }

            public bool Equals(MaterialKey other)
            {
                return m_TextureId == other.m_TextureId && m_Queue == other.m_Queue;
            }

            public override bool Equals(object obj)
            {
                return obj is MaterialKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (m_TextureId * 397) ^ m_Queue;
                }
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}

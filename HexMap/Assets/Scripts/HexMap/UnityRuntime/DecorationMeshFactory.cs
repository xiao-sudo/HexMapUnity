using System.Collections.Generic;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Builds and caches the mesh that renders one Sprite.
    /// </summary>
    /// <remarks>
    /// UVs are taken straight from <see cref="Sprite.uv"/> and never synthesised. That passthrough
    /// is what makes a Sprite Atlas work: when a Sprite is packed, Unity rewrites its UVs to point
    /// into the atlas page, so a texture that samples a generic 0..1 quad would sample the whole
    /// atlas instead of this Sprite's region. The same passthrough also covers trimmed Sprites and
    /// custom pivots, so no branch is needed for either.
    ///
    /// Sprite geometry arrives as <c>Vector2</c> positions in Sprite-local space and <c>ushort</c>
    /// indices, so both are converted for the Mesh. Whatever topology the Sprite reports is kept:
    /// a Full Rect import yields one quad, a Tight import yields the outline mesh Unity generated
    /// for the opaque region. Tight meshes cost fewer fragments and pack into an atlas more
    /// compactly, and nothing here depends on the vertex count. The mesh is centred on
    /// <see cref="Sprite.bounds"/> so its origin matches the Sprite pivot in both cases.
    /// </remarks>
    public static class DecorationMeshFactory
    {
        private static readonly Dictionary<int, Mesh> s_Meshes = new Dictionary<int, Mesh>();

        /// <summary>
        /// Returns the shared mesh for <paramref name="sprite"/>, building it on first use.
        /// Returns null when the Sprite is null or reports no usable geometry.
        /// </summary>
        public static Mesh GetOrCreateMesh(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }

            Mesh cached;
            if (s_Meshes.TryGetValue(sprite.GetInstanceID(), out cached))
            {
                return cached;
            }

            var spriteVertices = sprite.vertices;
            var spriteTriangles = sprite.triangles;
            var spriteUvs = sprite.uv;
            if (!ValidateMesh(sprite, spriteVertices, spriteTriangles, spriteUvs))
            {
                return null;
            }

            var centre = sprite.bounds.center;
            var vertices = new Vector3[spriteVertices.Length];
            for (var index = 0; index < spriteVertices.Length; index++)
            {
                vertices[index] = new Vector3(
                    spriteVertices[index].x - centre.x,
                    spriteVertices[index].y - centre.y,
                    -centre.z);
            }

            var triangles = new int[spriteTriangles.Length];
            for (var index = 0; index < spriteTriangles.Length; index++)
            {
                triangles[index] = spriteTriangles[index];
            }

            var mesh = new Mesh
            {
                name = "Decoration - " + sprite.name,
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = vertices;
            mesh.uv = spriteUvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            s_Meshes.Add(sprite.GetInstanceID(), mesh);
            return mesh;
        }

        /// <summary>
        /// Destroys every cached mesh. Call it on play-mode exit and from test teardown; the cache
        /// outlives any single component, so no component may dispose it.
        /// </summary>
        public static void Clear()
        {
            foreach (var mesh in s_Meshes.Values)
            {
                DestroyObject(mesh);
            }

            s_Meshes.Clear();
        }

        /// <summary>
        /// Accepts whatever topology the Sprite reports, and rejects only geometry that cannot be
        /// turned into a mesh at all.
        /// </summary>
        /// <remarks>
        /// The vertex count is deliberately not checked. Full Rect imports report four vertices and
        /// Tight imports report the outline Unity generated, and both are valid here because the
        /// mesh is built from these arrays rather than from an assumed quad. The remaining checks
        /// guard against a Sprite with no geometry and against indices that would make
        /// <c>Mesh.triangles</c> throw.
        /// </remarks>
        private static bool ValidateMesh(
            Sprite sprite,
            Vector2[] vertices,
            ushort[] triangles,
            Vector2[] uvs)
        {
            if (vertices == null || triangles == null || uvs == null)
            {
                Debug.LogError(
                    "Decoration sprite '" + sprite.name + "' reports no geometry. " +
                    "Check its import settings: a Sprite needs a non-empty mesh.");
                return false;
            }

            if (vertices.Length == 0 || uvs.Length != vertices.Length || triangles.Length < 3)
            {
                Debug.LogError(
                    "Decoration sprite '" + sprite.name + "' reports inconsistent geometry: " +
                    vertices.Length + " vertices, " + triangles.Length + " indices and " +
                    uvs.Length + " uvs. Every vertex needs a UV and every triangle three indices.");
                return false;
            }

            if (triangles.Length % 3 != 0)
            {
                Debug.LogError(
                    "Decoration sprite '" + sprite.name + "' reports " + triangles.Length +
                    " indices, which is not a whole number of triangles. Check its import settings.");
                return false;
            }

            for (var index = 0; index < triangles.Length; index++)
            {
                if (triangles[index] >= vertices.Length)
                {
                    Debug.LogError(
                        "Decoration sprite '" + sprite.name + "' has an out-of-range triangle index.");
                    return false;
                }
            }

            return true;
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}

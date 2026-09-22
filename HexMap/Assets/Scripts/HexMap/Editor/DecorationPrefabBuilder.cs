using System;
using HexMap.Core;
using HexMap.UnityRuntime;
using UnityEditor;
using UnityEngine;

namespace HexMap.Editor
{
    /// <summary>
    /// Builds the decoration prefab shape: a root carrying <see cref="DecorationView"/> and one child
    /// carrying the <see cref="MeshFilter"/> and <see cref="MeshRenderer"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The child's Transform is the only place the render plane lives.</b> The mesh that
    /// <see cref="DecorationMeshFactory"/> builds is laid out in local XY on both planes, so the
    /// plane is not a mesh decision and must never become one: a mesh per plane would double the
    /// mesh cache and break the contract that one Sprite has exactly one mesh. Rotating this single
    /// node is therefore the whole implementation of both planes.
    /// </para>
    /// <para>
    /// <b>XZ is <c>+90°</c> about X, which maps the Sprite's local up onto world <c>+Z</c>.</b> That
    /// is the direction the production camera in <c>map.unity</c> has at the top of its frame, so an
    /// image appears upright in the scene view instead of rotated by 180°. The same rotation turns
    /// the mesh's front face from <c>+Z</c> to <c>-Y</c>; <c>Decoration.shader</c> culls nothing, so
    /// both planes stay visible from both sides.
    /// </para>
    /// <para>
    /// Every entry in <see cref="DecorationPrefabMenu"/> goes through this class. There is exactly
    /// one place that knows what a decoration prefab looks like, so the skeleton entries and the
    /// entries that fill in a Sprite cannot drift apart.
    /// </para>
    /// </remarks>
    public static class DecorationPrefabBuilder
    {
        /// <summary>
        /// The name of the child that carries the geometry. It stays "Renderer" on every plane: the
        /// plane is a rotation rather than a rename, and <see cref="DecorationView"/> looks the child
        /// up by component instead of by name.
        /// </summary>
        public const string RendererObjectName = "Renderer";

        /// <summary>
        /// The child's local rotation that renders the Sprite parallel to <paramref name="plane"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="plane"/> is not one of the declared <see cref="HexPlane"/> values.
        /// </exception>
        public static Quaternion RotationFor(HexPlane plane)
        {
            if (!Enum.IsDefined(typeof(HexPlane), plane))
            {
                throw new ArgumentOutOfRangeException(nameof(plane), plane, null);
            }

            return plane == HexPlane.XZ
                ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.identity;
        }

        /// <summary>
        /// The asset file name, without folder or extension, for a decoration made from
        /// <paramref name="sourceName"/> on <paramref name="plane"/>.
        /// </summary>
        /// <remarks>
        /// The plane is written out as the enum member name on purpose: the label on disk and the
        /// value in code are then the same token, so they cannot be renamed apart. Names have to be
        /// distinct per plane because both planes of one Sprite are legitimate decorations.
        /// </remarks>
        public static string BuildAssetName(string sourceName, HexPlane plane)
        {
            return sourceName + "_Decoration_" + plane;
        }

        /// <summary>
        /// Creates the prefab hierarchy in memory. The caller owns the returned object and must
        /// destroy it; use <see cref="CreateAsset"/> when it is meant to become an asset.
        /// </summary>
        /// <remarks>
        /// <paramref name="sprite"/> may be null, which is how the skeleton entries ask for the
        /// required shape and nothing else. A null Sprite is a legitimate state for a decoration
        /// being authored, so the renderer is left off rather than reported as an error.
        /// </remarks>
        public static GameObject CreateHierarchy(HexPlane plane, Sprite sprite)
        {
            var root = new GameObject("Decoration");
            try
            {
                var rendererObject = new GameObject(RendererObjectName);
                rendererObject.transform.SetParent(root.transform, false);
                rendererObject.transform.localRotation = RotationFor(plane);
                rendererObject.AddComponent<MeshFilter>();
                rendererObject.AddComponent<MeshRenderer>();

                var view = root.AddComponent<DecorationView>();
                if (sprite != null)
                {
                    // The setter applies as well; the explicit call below is what covers the
                    // skeleton case, where nothing was assigned and nothing would have run.
                    view.Sprite = sprite;
                }

                view.Apply();
            }
            catch
            {
                // Leaving the half-built object behind would leak it into the scene the menu was
                // invoked from, which is worse than the original failure.
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }

            return root;
        }

        /// <summary>
        /// Creates the hierarchy and saves it as the prefab asset at <paramref name="assetPath"/>,
        /// returning the saved asset.
        /// </summary>
        public static GameObject CreateAsset(string assetPath, HexPlane plane, Sprite sprite)
        {
            var root = CreateHierarchy(plane, sprite);
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}

#if UNITY_EDITOR
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
    /// <b>The band decides the queue and the sorting order, and is written onto the component before
    /// the component applies anything.</b> <see cref="DecorationView"/> reads the queue to derive its
    /// material and the sorting order to write onto the renderer, so a band written afterwards would
    /// leave both wrong -- and the failure would be invisible, because the prefab would still look
    /// like a decoration. <see cref="DecorationBand"/> is the only table of what each band means.
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

        /// <summary>The serialized field <see cref="DecorationView"/> keeps its render queue in.</summary>
        private const string QueuePropertyName = "m_Queue";

        /// <summary>The serialized field <see cref="DecorationView"/> keeps its band's order in.</summary>
        private const string SortingOrderPropertyName = "m_SortingOrder";

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
        /// The asset file name, without folder or extension, for a prefab made from
        /// <paramref name="sourceName"/> in <paramref name="band"/> on <paramref name="plane"/>.
        /// </summary>
        /// <remarks>
        /// Both dimensions are written out as their enum member names on purpose: the label on disk
        /// and the value in code are then the same token, so they cannot be renamed apart. All four
        /// combinations have to be distinct, because one Sprite can legitimately become four
        /// prefabs -- two bands times two planes.
        /// </remarks>
        public static string BuildAssetName(string sourceName, DecorationBand band, HexPlane plane)
        {
            return sourceName + "_" + band + "_" + plane;
        }

        /// <summary>
        /// Creates the prefab hierarchy in memory. The caller owns the returned object and must
        /// destroy it; use <see cref="CreateAsset"/> when it is meant to become an asset.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <paramref name="sprite"/> may be null, which is how the skeleton entries ask for the
        /// required shape and nothing else. A null Sprite is a legitimate state for a decoration
        /// being authored, so the renderer is left off rather than reported as an error.
        /// </para>
        /// <para>
        /// Adding the component already runs one <c>Apply</c> through <c>[ExecuteAlways]</c>, and that
        /// first pass sees whatever the component's own defaults are. Configuring the band before the
        /// final <c>Apply</c> is what makes the last pass the one that decides; the discarded first
        /// pass costs one cached material at editor time and nothing at runtime.
        /// </para>
        /// </remarks>
        public static GameObject CreateHierarchy(DecorationBand band, HexPlane plane, Sprite sprite)
        {
            var root = new GameObject(band.ToString());
            try
            {
                var rendererObject = new GameObject(RendererObjectName);
                rendererObject.transform.SetParent(root.transform, false);
                rendererObject.transform.localRotation = RotationFor(plane);
                rendererObject.AddComponent<MeshFilter>();
                rendererObject.AddComponent<MeshRenderer>();

                var view = root.AddComponent<DecorationView>();

                // Before Apply, never after: Apply reads the queue to derive the material and the
                // sorting order to write onto the renderer.
                ConfigureBand(root, band);

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
        public static GameObject CreateAsset(
            string assetPath,
            DecorationBand band,
            HexPlane plane,
            Sprite sprite)
        {
            var root = CreateHierarchy(band, plane, sprite);
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Writes the band's queue and sorting order into the component's serialized fields.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Through <see cref="SerializedObject"/>, which is the mechanism the Inspector itself
        /// uses.</b> <see cref="DecorationView"/> exposes both values as read-only properties on
        /// purpose -- the queue derives a Material that then stays cached, and the sorting order is a
        /// placement decision rather than runtime state -- so the sanctioned way to set them is to
        /// edit the fields on the prefab, which is exactly what this does before the prefab exists.
        /// </para>
        /// <para>
        /// The property names are strings because that is what the serialization system takes. A
        /// renamed field would make <see cref="SerializedObject.FindProperty"/> return null, so the
        /// lookup is checked rather than dereferenced: the guard tests assert the values that come
        /// back out of the saved prefab, which is what turns that rename into a failure.
        /// </para>
        /// </remarks>
        private static void ConfigureBand(GameObject root, DecorationBand band)
        {
            var serialized = new SerializedObject(root.GetComponent<DecorationView>());
            var queue = serialized.FindProperty(QueuePropertyName);
            var sortingOrder = serialized.FindProperty(SortingOrderPropertyName);

            if (queue == null || sortingOrder == null)
            {
                throw new InvalidOperationException(
                    "DecorationView must keep serialized fields named '" + QueuePropertyName +
                    "' and '" + SortingOrderPropertyName + "'; the decoration prefab builder writes " +
                    "the band through them.");
            }

            queue.intValue = DecorationBands.Queue(band);
            sortingOrder.intValue = DecorationBands.SortingOrder(band);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif

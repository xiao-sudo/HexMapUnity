using System.Collections.Generic;
using HexMap.Core;
using UnityEditor;
using UnityEngine;

namespace HexMap.Editor
{
    /// <summary>
    /// The editor entry points that create decoration prefabs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four entries, two questions each: what the prefab is built from (the selected Sprite, or
    /// nothing but the required shape) and which plane it renders on. All four go through
    /// <see cref="DecorationPrefabBuilder"/>, so none of them can disagree about the structure or
    /// the rotation.
    /// </para>
    /// <para>
    /// <b>The plane is picked by the menu entry rather than by a dialog or by a folder rule.</b> A
    /// decoration's plane is a per-asset decision — an upright tree and a flat road tile can live in
    /// the same folder — so it has to be stated where the prefab is created, and two entries state
    /// it in the one place that is always visible.
    /// </para>
    /// </remarks>
    internal static class DecorationPrefabMenu
    {
        private const string SpriteXyMenuPath = "Assets/Create/HexMap/Decoration (XY)";
        private const string SpriteXzMenuPath = "Assets/Create/HexMap/Decoration (XZ)";
        private const string SkeletonXyMenuPath = "HexMap/Create Decoration Prefab (XY)";
        private const string SkeletonXzMenuPath = "HexMap/Create Decoration Prefab (XZ)";

        [MenuItem(SpriteXyMenuPath, false, 1)]
        private static void CreateFromSelectedSpritesOnXy()
        {
            CreateFromSelectedSprites(HexPlane.XY);
        }

        [MenuItem(SpriteXyMenuPath, true, 1)]
        private static bool ValidateCreateFromSelectedSpritesOnXy()
        {
            return CollectSelectedSprites().Count > 0;
        }

        [MenuItem(SpriteXzMenuPath, false, 2)]
        private static void CreateFromSelectedSpritesOnXz()
        {
            CreateFromSelectedSprites(HexPlane.XZ);
        }

        [MenuItem(SpriteXzMenuPath, true, 2)]
        private static bool ValidateCreateFromSelectedSpritesOnXz()
        {
            return CollectSelectedSprites().Count > 0;
        }

        [MenuItem(SkeletonXyMenuPath)]
        private static void CreateSkeletonOnXy()
        {
            CreateSkeleton(HexPlane.XY);
        }

        [MenuItem(SkeletonXzMenuPath)]
        private static void CreateSkeletonOnXz()
        {
            CreateSkeleton(HexPlane.XZ);
        }

        /// <summary>
        /// Creates one prefab per selected Sprite, next to the texture it came from.
        /// </summary>
        /// <remarks>
        /// The prefab goes beside its Sprite rather than into a configured output folder: a
        /// decoration prefab is meaningless without the Sprite it references, so keeping the two
        /// together is what makes deleting a texture and finding its dependants a single operation.
        /// </remarks>
        private static void CreateFromSelectedSprites(HexPlane plane)
        {
            var sprites = CollectSelectedSprites();
            if (sprites.Count == 0)
            {
                return;
            }

            var created = new List<GameObject>(sprites.Count);
            for (var index = 0; index < sprites.Count; index++)
            {
                var sprite = sprites[index];
                var folder = GetFolder(AssetDatabase.GetAssetPath(sprite));
                if (string.IsNullOrEmpty(folder))
                {
                    Debug.LogError(
                        "Decoration prefab for sprite '" + sprite.name + "' was not created: the " +
                        "sprite reports no asset folder to put the prefab in.");
                    continue;
                }

                var fileName = DecorationPrefabBuilder.BuildAssetName(sprite.name, plane) + ".prefab";
                var assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName);
                created.Add(DecorationPrefabBuilder.CreateAsset(assetPath, plane, sprite));
            }

            if (created.Count == 0)
            {
                return;
            }

            EditorGUIUtility.PingObject(created[0]);
            Selection.objects = created.ToArray();
        }

        /// <summary>
        /// Creates the required structure with no Sprite, at a path the user picks.
        /// </summary>
        /// <remarks>
        /// This is the only entry that asks for a path. The Sprite entries cannot: they are invoked
        /// on an already-selected asset, and asking again where to put the result of "make a
        /// decoration out of this" is a question the selection has already answered.
        /// </remarks>
        private static void CreateSkeleton(HexPlane plane)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Decoration Prefab",
                "Decoration_" + plane,
                "prefab",
                "Choose where to save the decoration prefab.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var prefab = DecorationPrefabBuilder.CreateAsset(path, plane, null);
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;
        }

        /// <summary>
        /// The Sprites the current selection resolves to, or an empty list when any selected object
        /// is not a Sprite asset.
        /// </summary>
        /// <remarks>
        /// All or nothing on purpose: the validator calls this too, so a menu entry is enabled only
        /// when it would create a prefab for every selected object. Creating some and silently
        /// skipping the rest would make a partial failure look like a success.
        /// </remarks>
        private static List<Sprite> CollectSelectedSprites()
        {
            var sprites = new List<Sprite>();
            var selected = Selection.objects;

            for (var index = 0; index < selected.Length; index++)
            {
                var sprite = ResolveSprite(selected[index]);
                if (sprite == null)
                {
                    sprites.Clear();
                    return sprites;
                }

                sprites.Add(sprite);
            }

            return sprites;
        }

        /// <summary>
        /// Resolves one selected object to a Sprite, or null when it is not a Sprite asset.
        /// </summary>
        /// <remarks>
        /// The project window hands back the texture's main asset, which is a
        /// <see cref="Texture2D"/> even when the texture is imported as a Sprite; only an expanded
        /// sub-Sprite is handed back as a <see cref="Sprite"/>. Both are accepted, because "make a
        /// decoration out of the image I selected" must not depend on which of the two rows the user
        /// happened to click.
        ///
        /// A sheet's main asset has no Sprite of its own, so it is rejected rather than resolved to
        /// one of its sub-Sprites: picking one silently would create a prefab for an image the user
        /// did not point at.
        /// </remarks>
        private static Sprite ResolveSprite(Object selected)
        {
            if (selected == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite)
            {
                return null;
            }

            var sprite = selected as Sprite;
            if (sprite != null)
            {
                return sprite;
            }

            if (importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        /// <summary>
        /// The asset folder part of an asset path, found by the separator rather than through
        /// <c>Path.GetDirectoryName</c>, which would hand back Windows separators that
        /// <see cref="AssetDatabase"/> does not expect.
        /// </summary>
        private static string GetFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            var separator = assetPath.LastIndexOf('/');
            return separator <= 0 ? null : assetPath.Substring(0, separator);
        }
    }
}

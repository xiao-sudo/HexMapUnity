#if UNITY_EDITOR
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
    /// Eight entries, two questions each: what the prefab is built from (the selected Sprite, or
    /// nothing but the required shape) and which band and plane it renders in. Every one of them goes
    /// through <see cref="DecorationPrefabBuilder"/>, so none of them can disagree about the
    /// structure, the rotation, or the two numbers a band means.
    /// </para>
    /// <para>
    /// <b>Band and plane are picked by the menu entry rather than by a dialog or by a folder
    /// rule.</b> Both are per-asset decisions -- an upright tree, a flat road tile and a floating
    /// status icon can all live in the same folder -- so they have to be stated where the prefab is
    /// created, and menu entries state them in the one place that is always visible.
    /// </para>
    /// <para>
    /// The entries are flat rather than grouped into <c>Decoration/</c> and <c>Overlay/</c>
    /// submenus: with four items per band a submenu costs a click and buys nothing, and the paths
    /// that already exist stay where anyone who learned them expects to find them.
    /// </para>
    /// </remarks>
    internal static class DecorationPrefabMenu
    {
        private const string DecorationSpriteXyMenuPath = "Assets/Create/HexMap/Decoration (XY)";
        private const string DecorationSpriteXzMenuPath = "Assets/Create/HexMap/Decoration (XZ)";
        private const string OverlaySpriteXyMenuPath = "Assets/Create/HexMap/Overlay (XY)";
        private const string OverlaySpriteXzMenuPath = "Assets/Create/HexMap/Overlay (XZ)";
        private const string DecorationSkeletonXyMenuPath = "HexMap/Create Decoration Prefab (XY)";
        private const string DecorationSkeletonXzMenuPath = "HexMap/Create Decoration Prefab (XZ)";
        private const string OverlaySkeletonXyMenuPath = "HexMap/Create Overlay Prefab (XY)";
        private const string OverlaySkeletonXzMenuPath = "HexMap/Create Overlay Prefab (XZ)";

        [MenuItem(DecorationSpriteXyMenuPath, false, 1)]
        private static void CreateDecorationFromSpriteOnXy()
        {
            CreateFromSelectedSprites(DecorationBand.Decoration, HexPlane.XY);
        }

        [MenuItem(DecorationSpriteXyMenuPath, true, 1)]
        private static bool ValidateCreateDecorationFromSpriteOnXy()
        {
            return CanCreateFromSelectedSprites();
        }

        [MenuItem(DecorationSpriteXzMenuPath, false, 2)]
        private static void CreateDecorationFromSpriteOnXz()
        {
            CreateFromSelectedSprites(DecorationBand.Decoration, HexPlane.XZ);
        }

        [MenuItem(DecorationSpriteXzMenuPath, true, 2)]
        private static bool ValidateCreateDecorationFromSpriteOnXz()
        {
            return CanCreateFromSelectedSprites();
        }

        [MenuItem(OverlaySpriteXyMenuPath, false, 3)]
        private static void CreateOverlayFromSpriteOnXy()
        {
            CreateFromSelectedSprites(DecorationBand.Overlay, HexPlane.XY);
        }

        [MenuItem(OverlaySpriteXyMenuPath, true, 3)]
        private static bool ValidateCreateOverlayFromSpriteOnXy()
        {
            return CanCreateFromSelectedSprites();
        }

        [MenuItem(OverlaySpriteXzMenuPath, false, 4)]
        private static void CreateOverlayFromSpriteOnXz()
        {
            CreateFromSelectedSprites(DecorationBand.Overlay, HexPlane.XZ);
        }

        [MenuItem(OverlaySpriteXzMenuPath, true, 4)]
        private static bool ValidateCreateOverlayFromSpriteOnXz()
        {
            return CanCreateFromSelectedSprites();
        }

        [MenuItem(DecorationSkeletonXyMenuPath)]
        private static void CreateDecorationSkeletonOnXy()
        {
            CreateSkeleton(DecorationBand.Decoration, HexPlane.XY);
        }

        [MenuItem(DecorationSkeletonXzMenuPath)]
        private static void CreateDecorationSkeletonOnXz()
        {
            CreateSkeleton(DecorationBand.Decoration, HexPlane.XZ);
        }

        [MenuItem(OverlaySkeletonXyMenuPath)]
        private static void CreateOverlaySkeletonOnXy()
        {
            CreateSkeleton(DecorationBand.Overlay, HexPlane.XY);
        }

        [MenuItem(OverlaySkeletonXzMenuPath)]
        private static void CreateOverlaySkeletonOnXz()
        {
            CreateSkeleton(DecorationBand.Overlay, HexPlane.XZ);
        }

        /// <summary>
        /// Creates one prefab per selected Sprite, next to the texture it came from.
        /// </summary>
        /// <remarks>
        /// The prefab goes beside its Sprite rather than into a configured output folder: a
        /// decoration prefab is meaningless without the Sprite it references, so keeping the two
        /// together is what makes deleting a texture and finding its dependants a single operation.
        /// </remarks>
        private static void CreateFromSelectedSprites(DecorationBand band, HexPlane plane)
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

                var fileName = DecorationPrefabBuilder.BuildAssetName(sprite.name, band, plane)
                    + ".prefab";
                var assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName);
                created.Add(DecorationPrefabBuilder.CreateAsset(assetPath, band, plane, sprite));
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
        ///
        /// The skeleton carries its band's queue and sorting order even though it has nothing to
        /// draw yet, so filling in the Sprite later cannot land it in the wrong band.
        /// </remarks>
        private static void CreateSkeleton(DecorationBand band, HexPlane plane)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Decoration Prefab",
                band + "_" + plane,
                "prefab",
                "Choose where to save the decoration prefab.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var prefab = DecorationPrefabBuilder.CreateAsset(path, band, plane, null);
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;
        }

        private static bool CanCreateFromSelectedSprites()
        {
            return CollectSelectedSprites().Count > 0;
        }

        /// <summary>
        /// The Sprites the current selection resolves to, or an empty list when any selected object
        /// is not a Sprite asset.
        /// </summary>
        /// <remarks>
        /// All or nothing on purpose: the validators call this too, so a menu entry is enabled only
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
#endif

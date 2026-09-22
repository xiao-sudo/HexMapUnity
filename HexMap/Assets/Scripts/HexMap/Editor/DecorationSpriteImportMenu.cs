#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HexMap.Editor
{
    /// <summary>
    /// Brings textures that were imported before the config existed up to the configured settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This entry point is necessary because <c>OnPreprocessTexture</c> only ever runs on an
    /// import.</b> A folder that is added to the config today does not re-import the art that is
    /// already sitting in it, so without this the rule would appear to work for new art and silently
    /// not for old art — the worst possible split, because both look identical in the Inspector until
    /// someone reimports by hand.
    /// </para>
    /// <para>
    /// <b>Only textures that differ are touched.</b> Reimporting rewrites the meta file and re-packs
    /// whichever atlas the texture belongs to, so a blanket sweep of a decoration folder costs far
    /// more than the comparison that avoids it.
    /// </para>
    /// </remarks>
    internal static class DecorationSpriteImportMenu
    {
        private const string MenuPath = "HexMap/Reimport Decoration Sprite Folders";

        [MenuItem(MenuPath)]
        private static void ReimportDecorationSpriteFolders()
        {
            // The config may have been edited since the last batch; do not sweep with a stale plan.
            DecorationSpriteImportPolicy.Invalidate();
            var plan = DecorationSpriteImportPolicy.Plan;

            var reimported = 0;
            var alreadyCorrect = 0;
            var visited = new HashSet<string>();

            for (var folderIndex = 0; folderIndex < plan.Folders.Length; folderIndex++)
            {
                var folder = plan.Folders[folderIndex];
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                for (var guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

                    // Two configured folders can overlap — "Assets" and "Assets/HexMap/Res" both
                    // cover the same texture — and visiting it twice would reimport it twice.
                    if (!visited.Add(assetPath) || !DecorationSpriteImportPolicy.Covers(assetPath))
                    {
                        continue;
                    }

                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null)
                    {
                        continue;
                    }

                    if (plan.Settings.Matches(importer))
                    {
                        alreadyCorrect++;
                        continue;
                    }

                    plan.Settings.ApplyTo(importer);
                    importer.SaveAndReimport();
                    reimported++;
                }
            }

            Debug.Log(
                "Decoration sprite folders: " + reimported + " texture(s) reimported, " +
                alreadyCorrect + " already correct, across " + plan.Folders.Length + " folder(s).");
        }
    }
}
#endif

using UnityEditor;

namespace HexMap.Editor
{
    /// <summary>
    /// Forces the decoration import settings onto every texture under a configured folder.
    /// </summary>
    /// <remarks>
    /// A thin shell: <see cref="DecorationSpriteImportPolicy"/> decides which folders count and
    /// <see cref="DecorationSpriteImportSettings.ApplyTo"/> decides what to write, so this class only
    /// supplies the two things neither of them can reach — the in-flight asset path and the importer.
    /// Anything that needs a decision belongs on one of those two, where a test can reach it.
    /// </remarks>
    internal sealed class DecorationSpriteImporter : AssetPostprocessor
    {
        // Not an override: AssetPostprocessor declares no OnPreprocessTexture, Unity finds this by
        // name on the derived type. Same reason OnPostprocessAllAssets below is static.
        private void OnPreprocessTexture()
        {
            if (!DecorationSpriteImportPolicy.Covers(assetPath))
            {
                return;
            }

            var importer = assetImporter as TextureImporter;
            if (importer == null)
            {
                return;
            }

            DecorationSpriteImportPolicy.Plan.Settings.ApplyTo(importer);
        }

        /// <summary>
        /// Drops the cached plan after every batch, so a config asset that was just created, edited or
        /// deleted takes effect on the next import instead of on the next domain reload.
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            DecorationSpriteImportPolicy.Invalidate();
        }
    }
}

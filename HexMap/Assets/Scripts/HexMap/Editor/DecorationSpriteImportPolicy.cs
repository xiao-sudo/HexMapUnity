#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HexMap.Editor
{
    /// <summary>
    /// Decides which folders are decoration folders and with which settings, once per import batch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The config asset is optional and its absence is not a problem.</b> With no
    /// <see cref="DecorationImportConfig"/> in the project the policy falls back to
    /// <see cref="FallbackFolder"/> with <see cref="DecorationSpriteImportSettings.Default"/>, which
    /// is the state a fresh clone is in — the repository deliberately ships no config asset, because
    /// a <c>.asset</c> file carries the script's guid and only Unity can assign that. The fallback
    /// therefore logs nothing; only a config that exists and misbehaves is worth a warning.
    /// </para>
    /// <para>
    /// <b>The plan is cached, and the cache is dropped after every import batch.</b> Resolving per
    /// texture would run an asset-database search for each file of a folder drop; resolving once per
    /// session would ignore a folder added five minutes later. The postprocessor invalidates after
    /// each batch for exactly that reason, so the cache is never more than one batch stale.
    /// </para>
    /// <para>
    /// <b>Nothing here may throw.</b> It runs inside <c>OnPreprocessTexture</c>, where an asset
    /// database lookup can fail while an import is in flight, and a decoration folder that stops
    /// importing textures is a far worse outcome than one that imports them with default settings.
    /// </para>
    /// </remarks>
    public static class DecorationSpriteImportPolicy
    {
        /// <summary>
        /// The folder that is treated as a decoration folder when no config asset exists.
        /// </summary>
        /// <remarks>
        /// It is the folder the decoration prefabs and their Sprites already live in, so the fallback
        /// describes what the project already does rather than inventing a new location.
        /// </remarks>
        public const string FallbackFolder = "Assets/HexMap/Res";

        private const string ConfigFilter = "t:DecorationImportConfig";

        private static DecorationSpriteImportPlan? s_Plan;

        /// <summary>The folders and settings in force for the current import batch.</summary>
        public static DecorationSpriteImportPlan Plan
        {
            get
            {
                if (s_Plan == null)
                {
                    s_Plan = Resolve();
                }

                return s_Plan.Value;
            }
        }

        /// <summary>Whether a texture at <paramref name="assetPath"/> gets the decoration settings.</summary>
        public static bool Covers(string assetPath)
        {
            return DecorationSpriteImportRules.IsInAnyFolder(assetPath, Plan.Folders);
        }

        /// <summary>Drops the cached plan so the next import sees the config as it now stands.</summary>
        public static void Invalidate()
        {
            s_Plan = null;
        }

        /// <summary>
        /// The plan a given config asset describes, or the fallback when it is null.
        /// </summary>
        /// <remarks>
        /// Separated from the asset-database lookup so the decision itself can be asserted directly:
        /// a test can hand in null or a config it created and check the result, without depending on
        /// which config assets happen to exist in the project.
        /// </remarks>
        public static DecorationSpriteImportPlan PlanFor(DecorationImportConfig config)
        {
            if (config == null)
            {
                return Fallback;
            }

            return new DecorationSpriteImportPlan(config.GetFolderPaths(), config.GetSettings(), false);
        }

        /// <summary>What is used when no config asset can be read.</summary>
        public static DecorationSpriteImportPlan Fallback
        {
            get
            {
                return new DecorationSpriteImportPlan(
                    new[] { FallbackFolder },
                    DecorationSpriteImportSettings.Default,
                    true);
            }
        }

        private static DecorationSpriteImportPlan Resolve()
        {
            try
            {
                var guids = AssetDatabase.FindAssets(ConfigFilter);
                if (guids == null || guids.Length == 0)
                {
                    // The expected state rather than a problem: see the class remarks.
                    return Fallback;
                }

                var configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                if (guids.Length > 1)
                {
                    Debug.LogWarning(
                        "There are " + guids.Length + " DecorationImportConfig assets. Using '" +
                        configPath + "' and ignoring the rest; delete the extras so which one wins " +
                        "is not a matter of asset order.");
                }

                var config = AssetDatabase.LoadAssetAtPath<DecorationImportConfig>(configPath);
                if (config == null)
                {
                    Debug.LogWarning(
                        "The DecorationImportConfig asset at '" + configPath + "' could not be " +
                        "loaded, so decoration textures are falling back to '" + FallbackFolder + "'.");
                    return Fallback;
                }

                var plan = PlanFor(config);
                if (plan.Folders.Length == 0)
                {
                    Debug.LogWarning(
                        "'" + configPath + "' lists no folders, so no texture is imported as a " +
                        "decoration Sprite. Add a folder to it, or delete it to fall back to '" +
                        FallbackFolder + "'.");
                }

                return plan;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Decoration texture import fell back to '" + FallbackFolder + "' because " +
                    "resolving the DecorationImportConfig failed: " + exception.Message);
                return Fallback;
            }
        }
    }

    /// <summary>Which folders are decoration folders, and what their textures are set to.</summary>
    public readonly struct DecorationSpriteImportPlan
    {
        public DecorationSpriteImportPlan(
            string[] folders,
            DecorationSpriteImportSettings settings,
            bool isFallback)
        {
            Folders = folders;
            Settings = settings;
            IsFallback = isFallback;
        }

        public string[] Folders { get; }
        public DecorationSpriteImportSettings Settings { get; }

        /// <summary>True when this plan came from <see cref="DecorationSpriteImportPolicy.Fallback"/>.</summary>
        public bool IsFallback { get; }
    }
}
#endif

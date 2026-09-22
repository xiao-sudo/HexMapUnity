#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexMap.Editor
{
    /// <summary>
    /// Runs <see cref="DecorationNormalizer"/> when a prefab or a scene is about to be written to disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Writing time, not creation time.</b> Rearranging on every drop means racing Unity's creation
    /// event, its deferred callbacks and its undo groups; the undo part of that race made one Ctrl+Z
    /// put an already-adopted instance back at the scene root, which reads as "the tool did not work".
    /// A save is not an undo step and it sees the finished state, so none of that applies.
    /// </para>
    /// <para>
    /// <b>Both hooks fire before the file is written, and changes made here are included in it.</b>
    /// For prefabs that is <c>PrefabStage.prefabSaving</c>, which Unity calls immediately before
    /// <c>PrefabUtility.SaveAsPrefabAsset</c> (see <c>PrefabStage.SavePrefab</c>); for scenes it is
    /// <c>EditorSceneManager.sceneSaving</c>. <c>AssetModificationProcessor.OnWillSaveAssets</c> is
    /// deliberately not used: it does not fire reliably for prefab assets, and
    /// <c>PrefabUtility.prefabInstanceUpdated</c> fires after the asset was already written.
    /// </para>
    /// <para>
    /// <b>Nothing is deferred.</b> <c>EditorApplication.delayCall</c> would run after the asset is
    /// written, so the change would land one save late -- the opposite of the point.
    /// </para>
    /// <para>
    /// <b>Prefab Mode's Auto Save is on by default and fires the prefab hook after essentially every
    /// edit.</b> The pass is therefore idempotent (it moves nothing when the layout is already right)
    /// and guarded against re-entry, so it settles instead of looping. On layouts that do need fixing,
    /// the mutation dirties the stage again during the save, which can cost one extra auto-save; after
    /// that the pass finds nothing to do.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class DecorationContainerHooks
    {
        private static bool s_Normalizing;

        static DecorationContainerHooks()
        {
            // A static constructor so a domain reload rebuilds the subscription; no "already
            // subscribed" flag, which would survive the reload and suppress the rebuilt one.
            PrefabStage.prefabSaving += OnPrefabSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnPrefabSaving(GameObject prefabRoot)
        {
            Run("prefab", () => DecorationNormalizer.NormalizePrefab(prefabRoot));
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            Run("scene", () => DecorationNormalizer.NormalizeScene(scene));
        }

        /// <summary>
        /// Runs one pass and reports what it moved. Never lets anything escape: a throw here would break
        /// somebody's save.
        /// </summary>
        /// <returns>How many instances were moved.</returns>
        private static int Run(string what, Func<int> normalize)
        {
            if (s_Normalizing)
            {
                return 0;
            }

            s_Normalizing = true;
            try
            {
                var moved = normalize();
                if (moved > 0)
                {
                    Debug.Log(
                        "Moved " + moved + " decoration instance(s) under their containers before " +
                        "saving the " + what + ".");
                }

                return moved;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return 0;
            }
            finally
            {
                s_Normalizing = false;
            }
        }

        [MenuItem("HexMap/Normalize Decoration Containers in Scene")]
        private static void NormalizeActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (Run("scene", () => DecorationNormalizer.NormalizeScene(scene)) > 0)
            {
                // Only mark when something actually changed: an idle menu click must not make the
                // scene look edited.
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        [MenuItem("HexMap/Normalize Decoration Containers in Selected Prefabs")]
        private static void NormalizeSelectedPrefabs()
        {
            var paths = SelectedPrefabPaths();
            for (var index = 0; index < paths.Count; index++)
            {
                NormalizePrefabAsset(paths[index]);
            }
        }

        [MenuItem("HexMap/Normalize Decoration Containers in Selected Prefabs", true)]
        private static bool ValidateNormalizeSelectedPrefabs()
        {
            return SelectedPrefabPaths().Count > 0;
        }

        /// <summary>
        /// Normalizes one prefab asset by opening its contents, rearranging them and writing them back.
        /// </summary>
        /// <remarks>
        /// This exists because there is <b>no public pre-write hook for "apply instance overrides to a
        /// prefab asset"</b>: a prefab edited through an instance in a scene reaches the asset by a path
        /// no callback covers. Doing it from a menu keeps it deterministic and keeps the tool out of the
        /// editor's own save machinery.
        /// </remarks>
        private static void NormalizePrefabAsset(string assetPath)
        {
            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(assetPath);
                var moved = DecorationNormalizer.NormalizePrefab(contents);
                if (moved == 0)
                {
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                Debug.Log(
                    "Moved " + moved + " decoration instance(s) under their containers in '" +
                    assetPath + "'.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static List<string> SelectedPrefabPaths()
        {
            var paths = new List<string>();
            var selected = Selection.objects;
            for (var index = 0; index < selected.Length; index++)
            {
                var path = AssetDatabase.GetAssetPath(selected[index]);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }
    }
}
#endif

using HexMap.Core;
using HexMap.Gvg.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexMap.UnityRuntime.Editor
{
    /// <summary>
    /// Editor menu entries for the runtime GVG test environment (issue 05):
    /// create a dedicated demo scene, attach the demo to the current scene, or toggle
    /// the Play-mode auto-spawn behavior.
    /// </summary>
    public static class RuntimeGvgDemoMenu
    {
        public const string DemoScenePath = "Assets/Scenes/RuntimeGvgTest.unity";
        public const string AuthoringAssetPath = "Assets/GvgMapAuthoring.asset";

        [MenuItem("HexMap/Runtime Test/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var asset = AssetDatabase.LoadAssetAtPath<GvgMapAuthoringAsset>(AuthoringAssetPath);

            var viewObject = new GameObject("HexMap View");
            var view = viewObject.AddComponent<HexMapView>();
            view.Radius = 11;
            view.OuterRadius = 11;
            view.Plane = HexPlane.XZ;
            if (asset != null)
            {
                view.GvgMapAuthoringAssetEditorOnly = asset;
            }

            var demoObject = new GameObject("Runtime GVG Demo");
            var demo = demoObject.AddComponent<RuntimeGvgDemo>();
            demo.MapView = view;
            demo.UseAuthoringAssetData = asset != null;

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 45f, -32f);
                camera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene, DemoScenePath);
            if (saved)
            {
                Debug.Log("[RuntimeGvgDemo] Demo scene saved to " + DemoScenePath +
                    ". Enter Play mode to run map generation + pathfinding.");
            }
            else
            {
                Debug.LogWarning("[RuntimeGvgDemo] Failed to save the demo scene to " + DemoScenePath + ".");
            }
        }

        [MenuItem("HexMap/Runtime Test/Add Demo to Current Scene")]
        public static void AddDemoToCurrentScene()
        {
            var view = Object.FindObjectOfType<HexMapView>();
            if (view == null)
            {
                EditorUtility.DisplayDialog(
                    "Runtime GVG Demo",
                    "No HexMapView found in the current scene.\n\nOpen test.unity, create a demo scene, or add a HexMapView first.",
                    "OK");
                return;
            }

            if (view.GetComponent<RuntimeGvgDemo>() != null)
            {
                EditorUtility.DisplayDialog(
                    "Runtime GVG Demo",
                    "The scene already has a RuntimeGvgDemo attached to the HexMapView.",
                    "OK");
                return;
            }

            var demo = view.gameObject.AddComponent<RuntimeGvgDemo>();
            demo.MapView = view;
            demo.UseAuthoringAssetData = view.GvgMapAuthoringAssetEditorOnly != null;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.SetDirty(view);
            Debug.Log("[RuntimeGvgDemo] Added RuntimeGvgDemo to '" + view.gameObject.name +
                "'. Enter Play mode to run map generation + pathfinding.");
        }

        [MenuItem("HexMap/Runtime Test/Toggle Editor Auto-Spawn")]
        public static void ToggleAutoSpawn()
        {
            var enabled = EditorPrefs.GetBool(RuntimeGvgDemo.EditorAutoSpawnPrefKey, true);
            EditorPrefs.SetBool(RuntimeGvgDemo.EditorAutoSpawnPrefKey, !enabled);
            Debug.Log("[RuntimeGvgDemo] Editor auto-spawn " + (!enabled ? "enabled" : "disabled") + ".");
        }

        [MenuItem("HexMap/Runtime Test/Toggle Editor Auto-Spawn", true)]
        public static bool ToggleAutoSpawnValidate()
        {
            Menu.SetChecked(
                "HexMap/Runtime Test/Toggle Editor Auto-Spawn",
                EditorPrefs.GetBool(RuntimeGvgDemo.EditorAutoSpawnPrefKey, true));
            return true;
        }
    }
}
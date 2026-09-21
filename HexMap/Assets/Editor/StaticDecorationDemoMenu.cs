using HexMap.UnityRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HexMap.Editor
{
    public static class StaticDecorationDemoMenu
    {
        [MenuItem("HexMap/Demos/Create Static Decoration Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Exit Play Mode before creating the decoration demo.");
                return;
            }
            var decorationShader = Shader.Find("HexMap/StaticDecoration");
            var hexShader = Shader.Find("HexMap/InstancedHexCell");
            if (decorationShader == null || hexShader == null)
            {
                Debug.LogError("Wait for the HexMap shaders to finish importing.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            var demoObject = new GameObject("Static Decoration Demo");
            demoObject.AddComponent<StaticDecorationDemo>().ConfigureShaders(decorationShader, hexShader);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = demoObject;
        }
    }
}

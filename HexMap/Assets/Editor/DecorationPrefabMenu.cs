using HexMap.UnityRuntime;
using UnityEditor;
using UnityEngine;

namespace HexMap.Editor
{
    /// <summary>
    /// Creates the reference decoration prefab: the shape a decoration has to be.
    /// </summary>
    /// <remarks>
    /// This builds one fixed structure, not a generator. A decoration is meant to be authored by
    /// hand and duplicated, so there is nothing to batch; the menu exists only so the required
    /// hierarchy does not have to be remembered from documentation.
    /// </remarks>
    internal static class DecorationPrefabMenu
    {
        private const string MenuPath = "HexMap/Create Decoration Prefab";

        [MenuItem(MenuPath)]
        private static void CreateDecorationPrefab()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Decoration Prefab",
                "Decoration",
                "prefab",
                "Choose where to save the decoration prefab.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var root = new GameObject("Decoration");
            try
            {
                var rendererObject = new GameObject("Renderer");
                rendererObject.transform.SetParent(root.transform, false);
                rendererObject.AddComponent<MeshFilter>();
                rendererObject.AddComponent<MeshRenderer>();

                var view = root.AddComponent<DecorationView>();
                view.Apply();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;
        }
    }
}

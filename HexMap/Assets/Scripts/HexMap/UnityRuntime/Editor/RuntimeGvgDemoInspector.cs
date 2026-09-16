using UnityEditor;
using UnityEngine;

namespace HexMap.UnityRuntime.Editor
{
    /// <summary>
    /// Inspector for RuntimeGvgDemo: draws the default serialized fields plus actions to
    /// build the map/plot pipeline and run pathfinding directly from Edit mode so the
    /// generated map and path can be previewed in the Scene view without entering Play mode.
    /// </summary>
    [CustomEditor(typeof(RuntimeGvgDemo))]
    public sealed class RuntimeGvgDemoInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var demo = (RuntimeGvgDemo)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Pipeline", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build Demo"))
            {
                demo.BuildDemo();
            }

            GUI.enabled = demo.IsReady;
            if (GUILayout.Button("Find Path"))
            {
                demo.RunPathfinding();
            }

            if (GUILayout.Button("Reset Colors"))
            {
                demo.ResetVisuals();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mode: " + (string.IsNullOrEmpty(demo.DataMode) ? "-" : demo.DataMode));
            EditorGUILayout.LabelField(
                "Plots: " + (demo.Registry != null ? demo.Registry.Count.ToString() : "-") +
                "   Start: " + demo.StartPlotId +
                "   Target: " + demo.TargetPlotId);

            if (!string.IsNullOrEmpty(demo.Error))
            {
                EditorGUILayout.HelpBox(demo.Error, MessageType.Error);
            }

            var path = demo.LastPath;
            if (path != null)
            {
                var text = path.IsSuccess
                    ? string.Format("Path SUCCESS: cost {0}, cells {1}", path.Cost, path.Count)
                    : string.Format("Path {0}: {1}", path.Status, path.Reason);
                EditorGUILayout.HelpBox(text, path.IsSuccess ? MessageType.Info : MessageType.Warning);
            }
        }
    }
}
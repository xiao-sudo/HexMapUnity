#if UNITY_EDITOR
using HexMap.UnityRuntime;
using UnityEditor;
using UnityEngine;
using HexMap.Gvg.Authoring;

namespace HexMap.Gvg.Editor
{
    /// <summary>
    /// Custom inspector for HexMapView: exposes the Editor-only GVG binding.
    /// The binding field itself is wrapped in #if UNITY_EDITOR, so this UI never
    /// leaks into Player builds.
    /// </summary>
    [CustomEditor(typeof(HexMapView))]
    public sealed class HexMapViewInspector : UnityEditor.Editor
    {
        private const string BindingFieldName = "m_GvgMapAuthoringAsset";
        private SerializedProperty m_GvgBindingProperty;

        private void OnEnable()
        {
            m_GvgBindingProperty = serializedObject.FindProperty(BindingFieldName);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSerializedFieldsExcludingBinding();
            DrawBindingSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSerializedFieldsExcludingBinding()
        {
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == BindingFieldName)
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        private void DrawBindingSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("GVG Map Binding", EditorStyles.boldLabel);

            if (m_GvgBindingProperty == null)
            {
                EditorGUILayout.HelpBox("Binding field unavailable in this build.", MessageType.Info);
                return;
            }

            var view = (HexMapView)target;
            DrawBindingField(view);
            DrawBindingDiagnostic(view);
            DrawBindingActions(view);
        }

        private void DrawBindingField(HexMapView view)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_GvgBindingProperty, new GUIContent("Authoring Asset"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            if (PrefabUtility.GetPrefabInstanceStatus(view.gameObject) == PrefabInstanceStatus.Connected)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            }
        }

        private void DrawBindingDiagnostic(HexMapView view)
        {
            if (view == null)
            {
                return;
            }

            GvgMapAuthoringAsset asset;
            GvgMapBindingDiagnostic diagnostic;
            GvgMapBindingResolver.TryResolve(view, out asset, out diagnostic);

            var message = Describe(diagnostic);
            var messageType = diagnostic == GvgMapBindingDiagnostic.None
                ? MessageType.Info
                : MessageType.Warning;
            EditorGUILayout.HelpBox(message, messageType);
        }

        private void DrawBindingActions(HexMapView view)
        {
            EditorGUILayout.BeginHorizontal();
            var asset = view.GvgMapAuthoringAssetEditorOnly;
            GUI.enabled = asset != null;
            if (GUILayout.Button("Unbind"))
            {
                GvgMapBindingEditor.Unbind(view);
                EditorUtility.SetDirty(view);
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private static string Describe(GvgMapBindingDiagnostic diagnostic)
        {
            switch (diagnostic)
            {
                case GvgMapBindingDiagnostic.None:
                    return "Binding is healthy.";
                case GvgMapBindingDiagnostic.ViewMissing:
                    return "No HexMapView found. Select a map root or child object.";
                case GvgMapBindingDiagnostic.AssetMissing:
                    return "Authoring asset is missing or broken. Rebind to continue.";
                case GvgMapBindingDiagnostic.TopologyMismatch:
                    return "Asset topology (max HexId) does not match the view radius. Rebind or repair the asset.";
                case GvgMapBindingDiagnostic.NotBound:
                    return "Not bound. Assign an authoring asset to enable GVG scene editing.";
                default:
                    return string.Empty;
            }
        }
    }
}
#endif
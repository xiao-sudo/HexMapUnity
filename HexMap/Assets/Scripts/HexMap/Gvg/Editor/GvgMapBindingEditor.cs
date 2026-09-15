using HexMap.Gvg.Authoring;
using HexMap.UnityRuntime;
using UnityEditor;
using UnityEngine;

namespace HexMap.Gvg.Editor
{
    /// <summary>
    /// Performs binding mutations with full Undo/Redo support.
    /// Kept separate from <see cref="GvgMapBindingResolver"/> so the resolver stays read-only.
    /// </summary>
    public static class GvgMapBindingEditor
    {
        public static void Bind(HexMapView view, GvgMapAuthoringAsset asset)
        {
            if (view == null)
            {
                Debug.LogWarning("Cannot bind: HexMapView is null.");
                return;
            }

            if (asset == null)
            {
                Unbind(view);
                return;
            }

            Undo.RecordObject(view, "Bind GVG Map Authoring Asset");
            view.GvgMapAuthoringAssetEditorOnly = asset;
            MarkDirty(view);
        }

        public static void Unbind(HexMapView view)
        {
            if (view == null)
            {
                return;
            }

            Undo.RecordObject(view, "Unbind GVG Map Authoring Asset");
            view.GvgMapAuthoringAssetEditorOnly = null;
            MarkDirty(view);
        }

        private static void MarkDirty(HexMapView view)
        {
            EditorUtility.SetDirty(view);
            var root = view.gameObject;
            if (PrefabUtility.GetPrefabInstanceStatus(root) == PrefabInstanceStatus.Connected)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            }
        }
    }
}

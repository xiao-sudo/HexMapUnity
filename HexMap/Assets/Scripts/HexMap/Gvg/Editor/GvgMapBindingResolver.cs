using HexMap.Gvg.Authoring;
using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Gvg.Editor
{
    /// <summary>
    /// Health state of a GVG map binding. Read-only result produced by
    /// <see cref="GvgMapBindingResolver"/>; never mutated by the resolver.
    /// </summary>
    public enum GvgMapBindingDiagnostic
    {
        /// <summary>Binding is healthy: asset exists and topology matches the view.</summary>
        None = 0,

        /// <summary>The selected object's root has no HexMapView.</summary>
        ViewMissing,

        /// <summary>The binding field is empty or references a missing asset.</summary>
        AssetMissing,

        /// <summary>The asset's topology (max HexId) does not match the view's radius.</summary>
        TopologyMismatch,

        /// <summary>The root is a HexMapView but has never been bound.</summary>
        NotBound
    }

    /// <summary>
    /// Editor-only read-only entry point for GVG map bindings.
    /// 03 (SceneView editing) and 04 (import/export validation) consume this API.
    /// </summary>
    public static class GvgMapBindingResolver
    {
        /// <summary>
        /// Resolves the root HexMapView from any selected object (root or descendant).
        /// </summary>
        public static bool TryResolveView(GameObject selected, out HexMapView view)
        {
            view = null;
            if (selected == null)
            {
                return false;
            }

            view = selected.GetComponentInParent<HexMapView>();
            return view != null;
        }

        /// <summary>
        /// Resolves the bound asset from a HexMapView and reports the binding diagnostic.
        /// Never modifies the view or the asset.
        /// </summary>
        public static bool TryResolve(HexMapView view, out GvgMapAuthoringAsset asset, out GvgMapBindingDiagnostic diagnostic)
        {
            asset = null;
            diagnostic = GvgMapBindingDiagnostic.NotBound;
            if (view == null)
            {
                diagnostic = GvgMapBindingDiagnostic.ViewMissing;
                return false;
            }

            asset = view.GvgMapAuthoringAssetEditorOnly;
            if (asset == null)
            {
                diagnostic = GvgMapBindingDiagnostic.AssetMissing;
                return false;
            }

            var map = view.CreateMapSnapshot();
            if (map == null || !GvgMapAuthoringUtility.IsCompatibleWithMap(asset, map))
            {
                diagnostic = GvgMapBindingDiagnostic.TopologyMismatch;
                return false;
            }

            diagnostic = GvgMapBindingDiagnostic.None;
            return true;
        }

        /// <summary>
        /// Convenience overload: resolves view and binding in one call from any selected object.
        /// </summary>
        public static bool TryResolve(
            GameObject selected,
            out HexMapView view,
            out GvgMapAuthoringAsset asset,
            out GvgMapBindingDiagnostic diagnostic)
        {
            view = null;
            asset = null;
            if (!TryResolveView(selected, out view))
            {
                diagnostic = GvgMapBindingDiagnostic.ViewMissing;
                return false;
            }

            return TryResolve(view, out asset, out diagnostic);
        }
    }
}

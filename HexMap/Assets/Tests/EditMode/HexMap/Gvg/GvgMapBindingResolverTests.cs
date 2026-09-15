using System.Collections.Generic;
using HexMap.Gvg.Authoring;
using HexMap.Gvg.Editor;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexMap.Gvg.Tests
{
    [TestFixture]
    public sealed class GvgMapBindingResolverTests
    {
        [Test]
        public void ResolveFromChildFindsRootViewBinding()
        {
            var root = new GameObject("MapRoot");
            var child = new GameObject("CellChild");
            child.transform.SetParent(root.transform);
            var view = root.AddComponent<HexMapView>();
            var asset = CreateAssetWithOnePlot(0);
            view.GvgMapAuthoringAssetEditorOnly = asset;

            try
            {
                HexMapView resolvedView;
                GvgMapAuthoringAsset resolvedAsset;
                GvgMapBindingDiagnostic diagnostic;
                Assert.That(GvgMapBindingResolver.TryResolve(child, out resolvedView, out resolvedAsset, out diagnostic), Is.True);
                Assert.That(resolvedView, Is.SameAs(view));
                Assert.That(resolvedAsset, Is.SameAs(asset));
                Assert.That(diagnostic, Is.EqualTo(GvgMapBindingDiagnostic.None));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ResolveMissingViewReturnsViewMissing()
        {
            var root = new GameObject("NoView");

            try
            {
                HexMapView resolvedView;
                GvgMapAuthoringAsset resolvedAsset;
                GvgMapBindingDiagnostic diagnostic;
                Assert.That(GvgMapBindingResolver.TryResolve(root, out resolvedView, out resolvedAsset, out diagnostic), Is.False);
                Assert.That(diagnostic, Is.EqualTo(GvgMapBindingDiagnostic.ViewMissing));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResolveUnboundViewReturnsAssetMissing()
        {
            var root = new GameObject("MapRoot");
            var view = root.AddComponent<HexMapView>();

            try
            {
                GvgMapAuthoringAsset resolvedAsset;
                GvgMapBindingDiagnostic diagnostic;
                Assert.That(GvgMapBindingResolver.TryResolve(view, out resolvedAsset, out diagnostic), Is.False);
                Assert.That(resolvedAsset, Is.Null);
                Assert.That(diagnostic, Is.EqualTo(GvgMapBindingDiagnostic.AssetMissing));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResolveTopologyMismatchWhenAssetExceedsViewRadius()
        {
            var root = new GameObject("MapRoot");
            var view = root.AddComponent<HexMapView>();
            view.Radius = 0; // single cell, max HexId = 0
            var asset = CreateAssetWithOnePlot(1); // references HexId 1, out of range
            view.GvgMapAuthoringAssetEditorOnly = asset;

            try
            {
                GvgMapAuthoringAsset resolvedAsset;
                GvgMapBindingDiagnostic diagnostic;
                Assert.That(GvgMapBindingResolver.TryResolve(view, out resolvedAsset, out diagnostic), Is.False);
                Assert.That(diagnostic, Is.EqualTo(GvgMapBindingDiagnostic.TopologyMismatch));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void UnbindClearsBinding()
        {
            var root = new GameObject("MapRoot");
            var view = root.AddComponent<HexMapView>();
            var asset = CreateAssetWithOnePlot(0);
            view.GvgMapAuthoringAssetEditorOnly = asset;

            try
            {
                GvgMapBindingEditor.Unbind(view);
                Assert.That(view.GvgMapAuthoringAssetEditorOnly, Is.Null);

                GvgMapAuthoringAsset resolvedAsset;
                GvgMapBindingDiagnostic diagnostic;
                Assert.That(GvgMapBindingResolver.TryResolve(view, out resolvedAsset, out diagnostic), Is.False);
                Assert.That(diagnostic, Is.EqualTo(GvgMapBindingDiagnostic.AssetMissing));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void BindThenUnbindKeepsViewUsable()
        {
            var root = new GameObject("MapRoot");
            var view = root.AddComponent<HexMapView>();
            var asset = CreateAssetWithOnePlot(0);

            try
            {
                GvgMapBindingEditor.Bind(view, asset);
                Assert.That(view.GvgMapAuthoringAssetEditorOnly, Is.SameAs(asset));

                GvgMapBindingEditor.Unbind(view);
                Assert.That(view.GvgMapAuthoringAssetEditorOnly, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void BindSupportsUndoRedo()
        {
            var root = new GameObject("MapRoot");
            var view = root.AddComponent<HexMapView>();
            var asset = CreateAssetWithOnePlot(0);

            try
            {
                GvgMapBindingEditor.Bind(view, asset);
                Assert.That(view.GvgMapAuthoringAssetEditorOnly, Is.SameAs(asset));

                Undo.PerformUndo();
                Assert.That(view.GvgMapAuthoringAssetEditorOnly, Is.Null);

                Undo.PerformRedo();
                Assert.That(view.GvgMapAuthoringAssetEditorOnly, Is.SameAs(asset));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(asset);
            }
        }

        private static GvgMapAuthoringAsset CreateAssetWithOnePlot(int hexId)
        {
            var asset = ScriptableObject.CreateInstance<GvgMapAuthoringAsset>();
            asset.MapId = "SpikeMap";
            asset.ReplacePlots(new List<GvgPlotAuthoringData>
            {
                new GvgPlotAuthoringData(hexId, new[] { hexId }, PlotType.Normal)
            });
            return asset;
        }
    }
}

using System;
using HexMap.Core;
using HexMap.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.UnityRuntime.Tests
{
    [TestFixture]
    public sealed class HexMapViewTests
    {
        private GameObject m_ViewObject;

        [TearDown]
        public void TearDown()
        {
            if (m_ViewObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_ViewObject);
            }
        }
        [Test]
        public void AppearanceEqualityUsesVisibleColorAndGradientEnabled()
        {
            var first = new HexAppearance(true, Color.red, false);
            var same = new HexAppearance(true, Color.red, false);
            var different = new HexAppearance(false, Color.red);
            var differentGradientEnabled = new HexAppearance(true, Color.red, true);

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first, Is.Not.EqualTo(differentGradientEnabled));
            Assert.That(first.GradientEnabled, Is.False);
        }

        [Test]
        public void ExplicitAppearanceConstructorPublishesGradientEnabled()
        {
            var appearance = new HexAppearance(true, Color.cyan, true);

            Assert.That(appearance.Visible, Is.True);
            Assert.That(appearance.Color, Is.EqualTo(Color.cyan));
            Assert.That(appearance.GradientEnabled, Is.True);
        }
        [Test]
        public void BuildPublishesViewsOnlyForExistingCells()
        {
            m_ViewObject = new GameObject("Hex Map View");
            var mapView = m_ViewObject.AddComponent<HexMapView>();
            mapView.Build();

            HexView view;
            Assert.That(mapView.TryGetHexView(new HexCoord(0, 0), out view), Is.True);
            Assert.That(view.Coordinate, Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(view.Cell.Coordinate, Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(view.IsValid, Is.True);
            Assert.That(mapView.TryGetHexView(new HexCoord(100, 0), out view), Is.False);
        }

        [Test]
        public void BuildUsesSecondaryScaleForLayoutGeometry()
        {
            m_ViewObject = new GameObject("Hex Map View");
            var mapView = m_ViewObject.AddComponent<HexMapView>();
            mapView.SecondaryScale = 0.8f;
            mapView.Build();

            Assert.That(mapView.Layout.SecondaryScale, Is.EqualTo(0.8f));
            var world = mapView.Layout.HexToWorld(new HexCoord(0, 1));
            Assert.That(world.z, Is.EqualTo(1.2f).Within(0.00001f));
        }

        [Test]
        public void RebuildInvalidatesViewsFromThePreviousGeneration()
        {
            m_ViewObject = new GameObject("Hex Map View");
            var mapView = m_ViewObject.AddComponent<HexMapView>();
            mapView.Build();

            HexView oldView;
            Assert.That(mapView.TryGetHexView(new HexCoord(0, 0), out oldView), Is.True);
            mapView.Build();

            HexView currentView;
            Assert.That(mapView.TryGetHexView(new HexCoord(0, 0), out currentView), Is.True);
            Assert.That(oldView.IsValid, Is.False);
            Assert.That(currentView, Is.Not.SameAs(oldView));
            Assert.Throws<InvalidOperationException>(
                () => oldView.SetAppearance(new HexAppearance(true, Color.white)));
        }

        [Test]
        public void WorldToHexUsesTheViewTransformForTranslationRotationAndUniformScale()
        {
            m_ViewObject = new GameObject("Hex Map View");
            m_ViewObject.transform.position = new Vector3(4f, 2f, -3f);
            m_ViewObject.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
            m_ViewObject.transform.localScale = Vector3.one * 2f;
            var mapView = m_ViewObject.AddComponent<HexMapView>();
            mapView.Build();

            var localPoint = mapView.Layout.HexToWorld(new HexCoord(1, -1));
            var worldPoint = m_ViewObject.transform.TransformPoint(localPoint);

            Assert.That(mapView.WorldToHex(worldPoint), Is.EqualTo(new HexCoord(1, -1)));
        }

        [Test]
        public void SceneEditingDrawAndPickPathsRoundTripEveryCell()
        {
            m_ViewObject = new GameObject("Hex Map View");
            m_ViewObject.transform.position = new Vector3(4f, 2f, -3f);
            m_ViewObject.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
            m_ViewObject.transform.localScale = Vector3.one * 2f;
            var mapView = m_ViewObject.AddComponent<HexMapView>();
            mapView.Radius = 2;
            mapView.Build();

            foreach (var cell in mapView.Map.Cells)
            {
                // Draw path: layout.HexToWorld(coord) + view TransformPoint.
                var worldCenter = mapView.transform.TransformPoint(mapView.Layout.HexToWorld(cell.Coordinate));
                // Pick path: WorldToMapLocal (InverseTransformPoint) + layout.WorldToHex.
                Assert.That(mapView.WorldToHex(worldCenter), Is.EqualTo(cell.Coordinate));
            }
        }

        [Test]
        public void NonUniformScaleIsRejected()
        {
            m_ViewObject = new GameObject("Hex Map View");
            m_ViewObject.transform.localScale = new Vector3(1f, 2f, 1f);
            var mapView = m_ViewObject.AddComponent<HexMapView>();

            Assert.Throws<InvalidOperationException>(() => mapView.Build());
        }

        [Test]
        public void RendererAppliesSecondaryScaleToSharedMesh()
        {
            var map = new Runtime.HexMap(new HexMapDefinition(1));
            var parent = new GameObject("Renderer Parent");

            try
            {
                var pointyRenderer = new HexMapRenderer(
                    map,
                    new HexLayout(HexOrientation.Pointy, HexPlane.XZ, 1f, Vector3.zero, 0.8f),
                    new HexMapRenderConfig(parent.transform, null, 0, Color.black));
                try
                {
                    var pointyMesh = parent.GetComponentInChildren<MeshFilter>().sharedMesh;
                    Assert.That(pointyMesh.vertices[2].z, Is.EqualTo(0.8f).Within(0.00001f));
                }
                finally
                {
                    pointyRenderer.Dispose();
                }

                var flatRenderer = new HexMapRenderer(
                    map,
                    new HexLayout(HexOrientation.Flat, HexPlane.XZ, 1f, Vector3.zero, 0.8f),
                    new HexMapRenderConfig(parent.transform, null, 0, Color.black));
                try
                {
                    var flatMesh = parent.GetComponentInChildren<MeshFilter>().sharedMesh;
                    Assert.That(flatMesh.vertices[2].z, Is.EqualTo(Mathf.Sin(60f * Mathf.Deg2Rad) * 0.8f).Within(0.00001f));
                }
                finally
                {
                    flatRenderer.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void RendererPublishesNormalizedBorderDistanceWeightsForEveryLayoutVariant()
        {
            var map = new Runtime.HexMap(new HexMapDefinition(1));
            var parent = new GameObject("Renderer Parent");

            try
            {
                foreach (var orientation in new[] { HexOrientation.Pointy, HexOrientation.Flat })
                {
                    foreach (var plane in new[] { HexPlane.XY, HexPlane.XZ })
                    {
                        var renderer = new HexMapRenderer(
                            map,
                            new HexLayout(orientation, plane, 2f, Vector3.zero, 0.65f),
                            new HexMapRenderConfig(parent.transform, null, 0, Color.black));
                        try
                        {
                            var mesh = parent.GetComponentInChildren<MeshFilter>().sharedMesh;
                            var uv = mesh.uv;
                            var normalizedPositions = mesh.uv2;
                            Assert.That(normalizedPositions.Length, Is.EqualTo(7));
                            Assert.That(normalizedPositions[0], Is.EqualTo(Vector2.zero));
                            Assert.That(uv.Length, Is.EqualTo(7));
                            Assert.That(uv[0].x, Is.EqualTo(1f));
                            Assert.That(uv[0].y, Is.EqualTo(0f));
                            for (var index = 1; index < uv.Length; index++)
                            {
                                Assert.That(uv[index].x, Is.EqualTo(0f));
                                Assert.That(uv[index].y, Is.EqualTo(0f));
                                var angle = (index - 1) * 60f * Mathf.Deg2Rad;
                                Assert.That(normalizedPositions[index].x, Is.EqualTo(Mathf.Cos(angle)).Within(0.00001f));
                                Assert.That(normalizedPositions[index].y, Is.EqualTo(Mathf.Sin(angle)).Within(0.00001f));
                            }
                        }
                        finally
                        {
                            renderer.Dispose();
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
        [Test]
        public void InstancedColorShaderUsesTransparentBorderDefaults()
        {
            var shader = Shader.Find("HexMap/InstancedColor");
            Assert.That(shader, Is.Not.Null);
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(shader), Is.False);


            var material = new Material(shader);
            try
            {
                Assert.That(material.GetTag("RenderType", true, string.Empty), Is.EqualTo("Transparent"));
                Assert.That(material.renderQueue, Is.EqualTo(3000));
                Assert.That(material.HasProperty("_BorderWidth"), Is.True);
                Assert.That(material.HasProperty("_GradientEnabled"), Is.True);
                Assert.That(material.HasProperty("_GradientPower"), Is.True);
                Assert.That(material.HasProperty("_GradientStartAlpha"), Is.True);
                Assert.That(material.GetFloat("_GradientStartAlpha"), Is.EqualTo(0.5f));
                Assert.That(material.HasProperty("_InteriorAlpha"), Is.True);
                Assert.That(material.HasProperty("_InnerRadius"), Is.True);
                Assert.That(material.GetFloat("_InnerRadius"), Is.EqualTo(0.4275f).Within(0.00001f));
                Assert.That(material.HasProperty("_AntiAliasing"), Is.False);
                Assert.That(material.GetFloat("_BorderWidth"), Is.EqualTo(0.05f));
                Assert.That(material.GetFloat("_GradientEnabled"), Is.EqualTo(0f));
                Assert.That(material.GetFloat("_GradientPower"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_InteriorAlpha"), Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
        [Test]
        public void SetAppearancePublishesInstancedColorAndGradientEnabled()
        {
            var shader = Shader.Find("HexMap/InstancedColor");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            // A single cell ensures the inspected Renderer belongs to the target view.
            var map = new Runtime.HexMap(new HexMapDefinition(0));
            material.SetFloat("_BorderWidth", 0.2f);
            material.SetFloat("_GradientPower", 3f);
            var parent = new GameObject("Renderer Parent");

            try
            {
                var renderer = new HexMapRenderer(
                    map,
                    new HexLayout(HexOrientation.Pointy, HexPlane.XZ, 1f, Vector3.zero),
                    new HexMapRenderConfig(parent.transform, material, 0, Color.black));
                try
                {
                    HexView view;
                    Assert.That(renderer.TryGetHexView(new HexCoord(0, 0), out view), Is.True);
                    view.SetAppearance(new HexAppearance(true, Color.magenta, true));

                    var meshRenderer = parent.GetComponentInChildren<MeshRenderer>();
                    var propertyBlock = new MaterialPropertyBlock();
                    meshRenderer.GetPropertyBlock(propertyBlock);
                    Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.magenta));
                    Assert.That(propertyBlock.GetFloat("_GradientEnabled"), Is.EqualTo(1f));
                    Assert.That(meshRenderer.sharedMaterial.GetFloat("_BorderWidth"), Is.EqualTo(0.2f));
                    Assert.That(meshRenderer.sharedMaterial.GetFloat("_GradientPower"), Is.EqualTo(3f));
                    view.SetAppearance(new HexAppearance(false, Color.cyan, false));

                    meshRenderer.GetPropertyBlock(propertyBlock);
                    Assert.That(meshRenderer.enabled, Is.False);
                    Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.magenta));
                    Assert.That(propertyBlock.GetFloat("_GradientEnabled"), Is.EqualTo(1f));

                }
                finally
                {
                    renderer.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
        [Test]
        public void SelectionAppearanceOverridesBaseAppearanceUntilDeselected()
        {
            var shader = Shader.Find("HexMap/InstancedColor");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var map = new Runtime.HexMap(new HexMapDefinition(0));
            var parent = new GameObject("Renderer Parent");

            try
            {
                var renderer = new HexMapRenderer(
                    map,
                    new HexLayout(HexOrientation.Pointy, HexPlane.XZ, 1f, Vector3.zero),
                    new HexMapRenderConfig(parent.transform, material, 0, Color.black));
                try
                {
                    HexView view;
                    Assert.That(renderer.TryGetHexView(new HexCoord(0, 0), out view), Is.True);

                    view.Select(new HexSelectionAppearance(Color.red, true));
                    Assert.That(view.IsSelected, Is.True);
                    view.SetAppearance(new HexAppearance(true, Color.blue, false));

                    var meshRenderer = parent.GetComponentInChildren<MeshRenderer>();
                    var propertyBlock = new MaterialPropertyBlock();
                    meshRenderer.GetPropertyBlock(propertyBlock);
                    Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.red));
                    Assert.That(propertyBlock.GetFloat("_GradientEnabled"), Is.EqualTo(1f));

                    view.SetAppearance(new HexAppearance(true, Color.green, true));
                    meshRenderer.GetPropertyBlock(propertyBlock);
                    Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.red));
                    Assert.That(propertyBlock.GetFloat("_GradientEnabled"), Is.EqualTo(1f));

                    view.Select(new HexSelectionAppearance(Color.yellow, false));
                    meshRenderer.GetPropertyBlock(propertyBlock);
                    Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.yellow));
                    Assert.That(propertyBlock.GetFloat("_GradientEnabled"), Is.EqualTo(0f));

                    view.SetAppearance(new HexAppearance(false, Color.cyan, true));
                    Assert.That(meshRenderer.enabled, Is.False);

                    view.Deselect();
                    view.Deselect();
                    Assert.That(view.IsSelected, Is.False);
                    Assert.That(meshRenderer.enabled, Is.False);

                    view.SetAppearance(new HexAppearance(true, Color.cyan, true));
                    meshRenderer.GetPropertyBlock(propertyBlock);
                    Assert.That(meshRenderer.enabled, Is.True);
                    Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.cyan));
                    Assert.That(propertyBlock.GetFloat("_GradientEnabled"), Is.EqualTo(1f));
                }
                finally
                {
                    renderer.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
        [Test]
        public void RendererSharesMeshAndDoesNotCreateMeshColliders()
        {
            var map = new Runtime.HexMap(new HexMapDefinition(1));
            var parent = new GameObject("Renderer Parent");
            var config = new HexMapRenderConfig(parent.transform, null, 0, Color.black);
            var renderer = new HexMapRenderer(
                map,
                new HexLayout(HexOrientation.Pointy, HexPlane.XZ, 1f, Vector3.zero),
                config);

            try
            {
                var filters = parent.GetComponentsInChildren<MeshFilter>();
                Assert.That(filters.Length, Is.EqualTo(map.Count));
                Assert.That(filters[0].sharedMesh, Is.SameAs(filters[1].sharedMesh));
                Assert.That(parent.GetComponentsInChildren<MeshCollider>(), Is.Empty);
            }
            finally
            {
                renderer.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }        [Test]
        public void SnapshotsUseSerializedSceneConfigurationWithoutBuild()
        {
            m_ViewObject = new GameObject("Hex Map View");
            var mapView = m_ViewObject.AddComponent<HexMapView>();
            mapView.Radius = 2;
            mapView.Orientation = HexOrientation.Flat;
            mapView.Plane = HexPlane.XY;
            mapView.OuterRadius = 2f;
            mapView.SecondaryScale = 0.75f;
            mapView.Origin = new Vector3(0.5f, -0.25f, 1f);

            Runtime.HexMap map;
            HexLayout layout;
            string error;
            Assert.That(mapView.TryCreateSnapshots(out map, out layout, out error), Is.True);
            Assert.That(error, Is.Empty);
            Assert.That(map.Count, Is.EqualTo(19));
            Assert.That(layout.Orientation, Is.EqualTo(HexOrientation.Flat));
            Assert.That(layout.Plane, Is.EqualTo(HexPlane.XY));
            Assert.That(layout.OuterRadius, Is.EqualTo(2f));
            Assert.That(layout.SecondaryScale, Is.EqualTo(0.75f));
            Assert.That(layout.Origin, Is.EqualTo(new Vector3(0.5f, -0.25f, 1f)));
        }

        [Test]
        public void SnapshotsReportInvalidSceneLayoutConfiguration()
        {
            m_ViewObject = new GameObject("Hex Map View");
            var mapView = m_ViewObject.AddComponent<HexMapView>();
            mapView.OuterRadius = 0f;

            Runtime.HexMap map;
            HexLayout layout;
            string error;
            Assert.That(mapView.TryCreateSnapshots(out map, out layout, out error), Is.False);
            Assert.That(error, Does.Contain("Outer radius"));
        }

    }
}

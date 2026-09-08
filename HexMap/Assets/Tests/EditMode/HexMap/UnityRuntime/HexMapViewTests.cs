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
        public void DebugLabelsAreEnabledAndCoordinatesAreHiddenByDefault()
        {
            m_ViewObject = new GameObject("Hex Map View");
            var mapView = m_ViewObject.AddComponent<HexMapView>();

            Assert.That(mapView.Radius, Is.EqualTo(3));
            Assert.That(mapView.ShowDebugLabels, Is.True);
            Assert.That(mapView.ShowDebugCoordinates, Is.False);
            Assert.That(mapView.ShowDebugBounds, Is.True);

            mapView.ShowDebugCoordinates = true;
            Assert.That(mapView.ShowDebugCoordinates, Is.True);
        }

        [Test]
        public void AppearanceEqualityUsesVisibleAndColor()
        {
            var first = new HexAppearance(true, Color.red);
            var same = new HexAppearance(true, Color.red);
            var different = new HexAppearance(false, Color.red);

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(different));
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
        public void NonUniformScaleIsRejected()
        {
            m_ViewObject = new GameObject("Hex Map View");
            m_ViewObject.transform.localScale = new Vector3(1f, 2f, 1f);
            var mapView = m_ViewObject.AddComponent<HexMapView>();

            Assert.Throws<InvalidOperationException>(() => mapView.Build());
        }

        [Test]
        public void RendererSharesMeshAndDoesNotCreateMeshColliders()
        {
            var map = new Runtime.HexMap(new HexMapDefinition(1));
            var parent = new GameObject("Renderer Parent");
            var config = new HexMapRenderConfig(parent.transform, null, 0);
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
        }
    }
}

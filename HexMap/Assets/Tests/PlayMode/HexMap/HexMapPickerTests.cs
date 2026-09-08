using System;
using HexMap.Core;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.UnityRuntime.Tests
{
    [TestFixture]
    public sealed class HexMapPickerTests
    {
        private GameObject m_ViewObject;
        private HexMapConfigAsset m_Config;

        [TearDown]
        public void TearDown()
        {
            if (m_ViewObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_ViewObject);
            }

            if (m_Config != null)
            {
                UnityEngine.Object.DestroyImmediate(m_Config);
            }
        }

        [Test]
        public void PickWorldPositionReturnsTheViewAndItsCell()
        {
            var view = CreateView(HexPlane.XZ, Vector3.zero);
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;

            var worldPoint = m_ViewObject.transform.TransformPoint(
                view.Layout.HexToWorld(new HexCoord(0, 0)));
            var result = picker.PickWorldPosition(worldPoint);

            Assert.That(result.Status, Is.EqualTo(HexPickStatus.Found));
            Assert.That(result.View, Is.Not.Null);
            Assert.That(result.Cell.Coordinate, Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(result.View.Cell.Coordinate, Is.EqualTo(result.Cell.Coordinate));
        }

        [Test]
        public void PickWorldPositionReportsNoMapWithoutAView()
        {
            m_ViewObject = new GameObject("Hex Map Picker");
            var picker = m_ViewObject.AddComponent<HexMapPicker>();

            var result = picker.PickWorldPosition(Vector3.zero);

            Assert.That(result.Status, Is.EqualTo(HexPickStatus.NoMap));
            Assert.That(result.View, Is.Null);
            Assert.That(result.HasCell, Is.False);
        }

        [Test]
        public void PickWorldPositionValidatesTheMapPlaneWithTolerance()
        {
            var view = CreateView(HexPlane.XZ, Vector3.zero);
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;
            var center = m_ViewObject.transform.TransformPoint(view.Layout.Origin);

            Assert.That(
                picker.PickWorldPosition(center + Vector3.up * 0.00005f).Status,
                Is.EqualTo(HexPickStatus.Found));
            var result = picker.PickWorldPosition(center + Vector3.up * 0.001f);

            Assert.That(result.Status, Is.EqualTo(HexPickStatus.NotOnMapPlane));
            Assert.That(result.View, Is.Null);
            Assert.That(result.Cell, Is.EqualTo(default(HexMap.Runtime.HexCell)));
        }

        [Test]
        public void PickWorldPositionDistinguishesOutsideMapAndMissingCell()
        {
            var view = CreateView(HexPlane.XZ, Vector3.zero, new HexCoord(0, 0));
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;

            var outside = m_ViewObject.transform.TransformPoint(
                view.Layout.HexToWorld(new HexCoord(100, 0)));
            Assert.That(picker.PickWorldPosition(outside).Status,
                Is.EqualTo(HexPickStatus.OutsideMap));

            var missing = m_ViewObject.transform.TransformPoint(
                view.Layout.HexToWorld(new HexCoord(0, 0)));
            var result = picker.PickWorldPosition(missing);
            Assert.That(result.Status, Is.EqualTo(HexPickStatus.Missing));
            Assert.That(result.View, Is.Null);
            Assert.That(result.Cell, Is.EqualTo(default(HexMap.Runtime.HexCell)));
        }

        [Test]
        public void PickWorldPositionRejectsNonFiniteWorldPoints()
        {
            var view = CreateView(HexPlane.XZ, Vector3.zero);
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => picker.PickWorldPosition(new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => picker.PickWorldPosition(new Vector3(0f, float.PositiveInfinity, 0f)));
        }

        [Test]
        public void ConvenienceWorldQueryIgnoresThePerpendicularCoordinate()
        {
            var view = CreateView(HexPlane.XZ, Vector3.zero);
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;
            var center = m_ViewObject.transform.TransformPoint(view.Layout.Origin);

            HexView viewAtPoint;
            Assert.That(view.TryGetHexViewAtWorldPoint(center + Vector3.up * 10f, out viewAtPoint), Is.True);
            Assert.That(picker.PickWorldPosition(center + Vector3.up * 10f).Status,
                Is.EqualTo(HexPickStatus.NotOnMapPlane));
        }

        [Test]
        public void GeometrySupportsTransformedMapObjectsAndWorldPlane()
        {
            m_ViewObject = new GameObject("Hex Map View");
            m_ViewObject.transform.position = new Vector3(4f, 2f, -3f);
            m_ViewObject.transform.rotation = Quaternion.Euler(15f, 37f, 8f);
            m_ViewObject.transform.localScale = Vector3.one * 2f;
            var view = m_ViewObject.AddComponent<HexMapView>();
            view.Plane = HexPlane.XY;
            view.Origin = new Vector3(0.5f, -0.25f, 0.75f);
            view.Build();
            var picker = m_ViewObject.AddComponent<HexMapPicker>();
            picker.MapView = view;
            var worldPoint = m_ViewObject.transform.TransformPoint(view.Layout.HexToWorld(new HexCoord(1, -1)));

            Assert.That(picker.PickWorldPosition(worldPoint).Status, Is.EqualTo(HexPickStatus.Found));
            Assert.That(Mathf.Abs(view.WorldPlane.GetDistanceToPoint(worldPoint)), Is.LessThan(0.00001f));
        }

        private HexMapView CreateView(HexPlane plane, Vector3 origin, params HexCoord[] excluded)
        {
            m_ViewObject = new GameObject("Hex Map View");
            var view = m_ViewObject.AddComponent<HexMapView>();
            view.Plane = plane;
            view.Origin = origin;
            if (excluded.Length > 0)
            {
                m_Config = ScriptableObject.CreateInstance<HexMapConfigAsset>();
                m_Config.Radius = 1;
                m_Config.SetExcludedCoordinates(excluded);
                view.Config = m_Config;
            }

            view.Build();
            return view;
        }
    }
}
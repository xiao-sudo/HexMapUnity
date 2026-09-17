using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public sealed class HexMapRenderer : IDisposable
    {
        private readonly HexMapRenderConfig m_Config;
        private readonly Dictionary<HexCoord, HexView> m_Views = new Dictionary<HexCoord, HexView>();
        private RuntimeHexMap m_Map;
        private HexLayout m_Layout;
        private Transform m_GeneratedRoot;
        private Mesh m_SharedMesh;
        private int m_Generation;
        private bool m_IsDisposed;

        public HexMapRenderer(RuntimeHexMap map, HexLayout layout, HexMapRenderConfig config)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            m_Config = config;
            m_Map = map;
            m_Layout = layout;
            BuildCurrentMap();
        }

        public RuntimeHexMap Map
        {
            get { return m_Map; }
        }

        public HexLayout Layout
        {
            get { return m_Layout; }
        }

        public int Generation
        {
            get { return m_Generation; }
        }

        public bool TryGetHexView(HexCoord coordinate, out HexView view)
        {
            EnsureNotDisposed();
            return m_Views.TryGetValue(coordinate, out view);
        }

        public void Rebuild(RuntimeHexMap map, HexLayout layout)
        {
            EnsureNotDisposed();
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            InvalidateCurrentViews();
            ReleaseGeneratedResources();
            m_Map = map;
            m_Layout = layout;
            BuildCurrentMap();
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            InvalidateCurrentViews();
            ReleaseGeneratedResources();
            m_Map = null;
            m_IsDisposed = true;
        }

        private void BuildCurrentMap()
        {
            m_Generation++;
            m_GeneratedRoot = new GameObject("Generated Hex Map").transform;
            if (m_Config.Parent != null)
            {
                m_GeneratedRoot.SetParent(m_Config.Parent, false);
            }

            m_SharedMesh = CreateCellMesh();
            var material = m_Config.SharedMaterial;

            foreach (var cell in m_Map.Cells)
            {
                var cellObject = new GameObject(cell.Coordinate.ToString());
                cellObject.layer = m_Config.Layer;
                cellObject.transform.SetParent(m_GeneratedRoot, false);
                cellObject.transform.localPosition = m_Layout.HexToWorld(cell.Coordinate);

                var filter = cellObject.AddComponent<MeshFilter>();
                var renderer = cellObject.AddComponent<MeshRenderer>();
                filter.sharedMesh = m_SharedMesh;
                renderer.sharedMaterial = material;

                var renderHandle = new HexRenderHandle(renderer, m_Generation);
                m_Views.Add(cell.Coordinate, new HexView(cell, renderHandle));
            }
        }

        private Mesh CreateCellMesh()
        {
            var mesh = new Mesh { name = "Generated Hex Cell" };
            var vertices = new Vector3[7];
            var borderDistances = new Vector2[7];
            var triangles = new int[18];
            vertices[0] = Vector3.zero;
            borderDistances[0] = Vector2.right;

            for (var index = 0; index < 6; index++)
            {
                var angle = (m_Layout.Orientation == HexOrientation.Pointy ? 30f : 0f) + index * 60f;
                var radians = angle * Mathf.Deg2Rad;
                var x = Mathf.Cos(radians) * m_Layout.OuterRadius;
                var secondary = Mathf.Sin(radians) * m_Layout.OuterRadius * m_Layout.SecondaryScale;
                vertices[index + 1] = m_Layout.Plane == HexPlane.XY
                    ? new Vector3(x, secondary, 0f)
                    : new Vector3(x, 0f, secondary);
                borderDistances[index + 1] = Vector2.zero;

                var triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                if (m_Layout.Plane == HexPlane.XY)
                {
                    triangles[triangleIndex + 1] = index + 1;
                    triangles[triangleIndex + 2] = index == 5 ? 1 : index + 2;
                }
                else
                {
                    triangles[triangleIndex + 1] = index == 5 ? 1 : index + 2;
                    triangles[triangleIndex + 2] = index + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = borderDistances;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private void InvalidateCurrentViews()
        {
            foreach (var view in m_Views.Values)
            {
                view.Invalidate();
            }

            m_Views.Clear();
        }

        private void ReleaseGeneratedResources()
        {
            if (m_GeneratedRoot != null)
            {
                DestroyObject(m_GeneratedRoot.gameObject);
                m_GeneratedRoot = null;
            }

            if (m_SharedMesh != null)
            {
                DestroyObject(m_SharedMesh);
                m_SharedMesh = null;
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private void EnsureNotDisposed()
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(HexMapRenderer));
            }
        }
    }
}
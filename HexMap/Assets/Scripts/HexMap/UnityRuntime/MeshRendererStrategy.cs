using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexMap.UnityRuntime
{
    internal sealed class MeshRendererStrategy : IHexRenderStrategy
    {
        private readonly Dictionary<HexCoord, IHexRenderTarget> m_Targets =
            new Dictionary<HexCoord, IHexRenderTarget>();
        private Transform m_GeneratedRoot;
        private Mesh m_SharedMesh;
        private bool m_IsBuilt;
        private bool m_IsDisposed;

        public IReadOnlyDictionary<HexCoord, IHexRenderTarget> Build(
            RuntimeHexMap map,
            HexLayout layout,
            HexMapRenderConfig config,
            int generation)
        {
            EnsureNotDisposed();
            if (m_IsBuilt)
            {
                throw new InvalidOperationException("The MeshRenderer strategy has already been built.");
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                m_GeneratedRoot = new GameObject("Generated Hex Map").transform;
                if (config.Parent != null)
                {
                    m_GeneratedRoot.SetParent(config.Parent, false);
                }

                m_GeneratedRoot.gameObject.SetActive(false);
                m_SharedMesh = CreateCellMesh(layout);

                foreach (var cell in map.Cells)
                {
                    var cellObject = new GameObject(cell.Coordinate.ToString());
                    cellObject.layer = config.Layer;
                    cellObject.transform.SetParent(m_GeneratedRoot, false);
                    cellObject.transform.localPosition = layout.HexToWorld(cell.Coordinate);

                    var filter = cellObject.AddComponent<MeshFilter>();
                    var renderer = cellObject.AddComponent<MeshRenderer>();
                    filter.sharedMesh = m_SharedMesh;
                    renderer.sharedMaterial = config.SharedMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    renderer.allowOcclusionWhenDynamic = false;

                    m_Targets.Add(cell.Coordinate, new MeshRendererTarget(renderer));
                }

                m_IsBuilt = true;
                return m_Targets;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Activate()
        {
            EnsureNotDisposed();
            if (!m_IsBuilt)
            {
                throw new InvalidOperationException("The MeshRenderer strategy has not been built.");
            }

            m_GeneratedRoot.gameObject.SetActive(true);
        }

        public void Render()
        {
            EnsureNotDisposed();
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

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

            m_Targets.Clear();
            m_IsBuilt = false;
            m_IsDisposed = true;
        }

        private static Mesh CreateCellMesh(HexLayout layout)
        {
            var mesh = new Mesh { name = "Generated Hex Cell" };
            var vertices = new Vector3[7];
            var borderDistances = new Vector2[7];
            var normalizedPositions = new Vector2[7];
            var triangles = new int[18];
            vertices[0] = Vector3.zero;
            borderDistances[0] = Vector2.right;
            normalizedPositions[0] = Vector2.zero;

            for (var index = 0; index < 6; index++)
            {
                var angle = (layout.Orientation == HexOrientation.Pointy ? 30f : 0f) + index * 60f;
                var radians = angle * Mathf.Deg2Rad;
                var x = Mathf.Cos(radians) * layout.OuterRadius;
                var secondary = Mathf.Sin(radians) * layout.OuterRadius * layout.SecondaryScale;
                vertices[index + 1] = layout.Plane == HexPlane.XY
                    ? new Vector3(x, secondary, 0f)
                    : new Vector3(x, 0f, secondary);
                borderDistances[index + 1] = Vector2.zero;
                // Keep shader-space edge normals fixed for Pointy and Flat layouts.
                var normalizedRadians = index * 60f * Mathf.Deg2Rad;
                normalizedPositions[index + 1] =
                    new Vector2(Mathf.Cos(normalizedRadians), Mathf.Sin(normalizedRadians));

                var triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                if (layout.Plane == HexPlane.XY)
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
            mesh.uv2 = normalizedPositions;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
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
                throw new ObjectDisposedException(nameof(MeshRendererStrategy));
            }
        }
    }
}
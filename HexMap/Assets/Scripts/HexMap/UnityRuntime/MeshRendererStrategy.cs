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
                m_SharedMesh = HexCellMeshFactory.CreateCellMesh(layout);

                foreach (var cell in map.Cells)
                {
                    string cellName = null;
#if UNITY_EDITOR
                    cellName = $"Cell #{cell.Id} {cell.Coordinate}";
#endif
                    
                    var cellObject = new GameObject(cellName);
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
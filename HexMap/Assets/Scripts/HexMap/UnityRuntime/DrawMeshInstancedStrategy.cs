using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexMap.UnityRuntime
{
    internal interface IInstancedDrawAdapter
    {
        void Draw(
            Mesh mesh,
            Material material,
            Matrix4x4[] matrices,
            int count,
            MaterialPropertyBlock properties,
            int layer);
    }

    internal sealed class UnityInstancedDrawAdapter : IInstancedDrawAdapter
    {
        public void Draw(
            Mesh mesh,
            Material material,
            Matrix4x4[] matrices,
            int count,
            MaterialPropertyBlock properties,
            int layer)
        {
            Graphics.DrawMeshInstanced(
                mesh,
                0,
                material,
                matrices,
                count,
                properties,
                ShadowCastingMode.Off,
                false,
                layer,
                null,
                LightProbeUsage.Off,
                null);
        }
    }

    internal sealed class DrawMeshInstancedStrategy : IHexRenderStrategy
    {
        private const int MaxInstancesPerDraw = 1023;

        private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int s_GradientEnabledId = Shader.PropertyToID("_GradientEnabled");
        private static readonly int s_VisibleId = Shader.PropertyToID("_Visible");

        private readonly Dictionary<HexCoord, IHexRenderTarget> m_Targets =
            new Dictionary<HexCoord, IHexRenderTarget>();
        private readonly List<DrawMeshInstancedTarget> m_RenderTargets =
            new List<DrawMeshInstancedTarget>();
        private readonly IInstancedDrawAdapter m_DrawAdapter;
        private readonly Matrix4x4[] m_Matrices = new Matrix4x4[MaxInstancesPerDraw];
        private readonly Vector4[] m_Colors = new Vector4[MaxInstancesPerDraw];
        private readonly float[] m_GradientEnabled = new float[MaxInstancesPerDraw];
        private readonly float[] m_Visible = new float[MaxInstancesPerDraw];
        private readonly MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();

        private Mesh m_SharedMesh;
        private Material m_SharedMaterial;
        private Transform m_Parent;
        private int m_Layer;
        private bool m_IsBuilt;
        private bool m_IsDisposed;

        public DrawMeshInstancedStrategy()
            : this(new UnityInstancedDrawAdapter())
        {
        }

        internal DrawMeshInstancedStrategy(IInstancedDrawAdapter drawAdapter)
        {
            m_DrawAdapter = drawAdapter ?? throw new ArgumentNullException(nameof(drawAdapter));
        }

        public IReadOnlyDictionary<HexCoord, IHexRenderTarget> Build(
            RuntimeHexMap map,
            HexLayout layout,
            HexMapRenderConfig config,
            int generation)
        {
            EnsureNotDisposed();
            if (m_IsBuilt)
            {
                throw new InvalidOperationException("The DrawMeshInstanced strategy has already been built.");
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
                m_SharedMesh = HexCellMeshFactory.CreateCellMesh(layout);
                m_SharedMaterial = config.SharedMaterial;
                m_Parent = config.Parent;
                m_Layer = config.Layer;
                if (m_SharedMaterial != null)
                {
                    m_SharedMaterial.enableInstancing = true;
                }

                foreach (var cell in map.Cells)
                {
                    var localMatrix = Matrix4x4.TRS(
                        layout.HexToWorld(cell.Coordinate),
                        Quaternion.identity,
                        Vector3.one);
                    var target = new DrawMeshInstancedTarget(localMatrix);
                    m_Targets.Add(cell.Coordinate, target);
                    m_RenderTargets.Add(target);
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
                throw new InvalidOperationException("The DrawMeshInstanced strategy has not been built.");
            }
        }

        public void Render()
        {
            EnsureNotDisposed();
            if (!m_IsBuilt || m_SharedMaterial == null || m_RenderTargets.Count == 0)
            {
                return;
            }

            var parentMatrix = m_Parent == null ? Matrix4x4.identity : m_Parent.localToWorldMatrix;
            var targetIndex = 0;
            while (targetIndex < m_RenderTargets.Count)
            {
                var instanceCount = 0;
                while (targetIndex < m_RenderTargets.Count && instanceCount < MaxInstancesPerDraw)
                {
                    var target = m_RenderTargets[targetIndex++];
                    if (!target.IsValid)
                    {
                        continue;
                    }

                    target.WriteTo(
                        parentMatrix,
                        m_Matrices,
                        m_Colors,
                        m_GradientEnabled,
                        m_Visible,
                        instanceCount);
                    instanceCount++;
                }

                if (instanceCount == 0)
                {
                    continue;
                }

                m_PropertyBlock.Clear();
                m_PropertyBlock.SetVectorArray(s_BaseColorId, m_Colors);
                m_PropertyBlock.SetFloatArray(s_GradientEnabledId, m_GradientEnabled);
                m_PropertyBlock.SetFloatArray(s_VisibleId, m_Visible);
                m_DrawAdapter.Draw(
                    m_SharedMesh,
                    m_SharedMaterial,
                    m_Matrices,
                    instanceCount,
                    m_PropertyBlock,
                    m_Layer);
            }
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            foreach (var target in m_RenderTargets)
            {
                target.Invalidate();
            }

            if (m_SharedMesh != null)
            {
                DestroyObject(m_SharedMesh);
                m_SharedMesh = null;
            }

            m_Targets.Clear();
            m_RenderTargets.Clear();
            m_SharedMaterial = null;
            m_Parent = null;
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
                throw new ObjectDisposedException(nameof(DrawMeshInstancedStrategy));
            }
        }
    }
}

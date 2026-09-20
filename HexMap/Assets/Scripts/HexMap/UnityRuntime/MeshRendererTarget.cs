using System;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    internal sealed class MeshRendererTarget : IHexRenderTarget
    {
        private readonly MeshRenderer m_Renderer;
        private readonly MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();
        private bool m_IsInvalidated;

        private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int s_GradientEnabledId = Shader.PropertyToID("_GradientEnabled");

        public MeshRendererTarget(MeshRenderer renderer)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            m_Renderer = renderer;
        }

        public bool IsValid
        {
            get { return !m_IsInvalidated && m_Renderer != null; }
        }

        public void Apply(HexAppearance appearance)
        {
            EnsureValid();

            m_Renderer.enabled = appearance.Visible;
            if (!appearance.Visible)
            {
                return;
            }

            m_PropertyBlock.Clear();
            m_PropertyBlock.SetColor(s_BaseColorId, appearance.Color);
            m_PropertyBlock.SetFloat(s_GradientEnabledId, appearance.GradientEnabled ? 1f : 0f);
            m_Renderer.SetPropertyBlock(m_PropertyBlock);
        }

        public void Invalidate()
        {
            m_IsInvalidated = true;
        }

        private void EnsureValid()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("The HexView is no longer valid.");
            }
        }
    }
}
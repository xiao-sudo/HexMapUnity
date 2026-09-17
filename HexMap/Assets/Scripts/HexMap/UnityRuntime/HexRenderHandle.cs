using System;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    internal sealed class HexRenderHandle
    {
        private readonly int m_Generation;
        private readonly MeshRenderer m_Renderer;
        private readonly MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();
        private bool m_IsValid = true;
        private bool m_HasAppearance;
        private HexAppearance m_Appearance;

        private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int s_GradientEnabledId = Shader.PropertyToID("_GradientEnabled");

        public HexRenderHandle(MeshRenderer renderer, int generation)
        {
            m_Renderer = renderer;
            m_Generation = generation;
        }

        public bool IsValid
        {
            get { return m_IsValid && m_Renderer != null; }
        }

        public int Generation
        {
            get { return m_Generation; }
        }

        public void SetAppearance(HexAppearance appearance)
        {
            EnsureValid();
            if (m_HasAppearance && m_Appearance == appearance)
            {
                return;
            }

            m_Appearance = appearance;
            m_HasAppearance = true;
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
            m_IsValid = false;
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
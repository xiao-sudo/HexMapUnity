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
            m_PropertyBlock.Clear();

            var material = m_Renderer.sharedMaterial;
            if (material != null)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    m_PropertyBlock.SetColor("_BaseColor", appearance.Color);
                }

                if (material.HasProperty("_BorderWidth"))
                {
                    m_PropertyBlock.SetFloat("_BorderWidth", appearance.BorderWidth);
                }

                if (material.HasProperty("_GradientEnabled"))
                {
                    m_PropertyBlock.SetFloat("_GradientEnabled", appearance.GradientEnabled ? 1f : 0f);
                }

                if (material.HasProperty("_GradientPower"))
                {
                    m_PropertyBlock.SetFloat("_GradientPower", appearance.GradientPower);
                }

                if (material.HasProperty("_Color"))
                {
                    m_PropertyBlock.SetColor("_Color", appearance.Color);
                }
            }

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
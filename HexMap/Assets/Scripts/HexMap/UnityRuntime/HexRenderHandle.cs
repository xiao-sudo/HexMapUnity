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
        private bool m_HasBaseAppearance;
        private HexAppearance m_BaseAppearance;
        private bool m_IsSelected;
        private HexSelectionAppearance m_SelectionAppearance;

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

        public bool IsSelected
        {
            get { return m_IsSelected; }
        }

        public void SetAppearance(HexAppearance appearance)
        {
            EnsureValid();
            if (m_HasBaseAppearance && m_BaseAppearance == appearance)
            {
                return;
            }

            m_BaseAppearance = appearance;
            m_HasBaseAppearance = true;
            ApplyAppearance();
        }

        public void Select(HexSelectionAppearance appearance)
        {
            EnsureValid();
            if (m_IsSelected && m_SelectionAppearance == appearance)
            {
                return;
            }

            m_SelectionAppearance = appearance;
            m_IsSelected = true;
            ApplyAppearance();
        }

        public void Deselect()
        {
            EnsureValid();
            if (!m_IsSelected)
            {
                return;
            }

            m_IsSelected = false;
            ApplyAppearance();
        }

        public void Invalidate()
        {
            m_IsSelected = false;
            m_IsValid = false;
        }

        private void ApplyAppearance()
        {
            if (!m_HasBaseAppearance)
            {
                return;
            }

            m_Renderer.enabled = m_BaseAppearance.Visible;
            if (!m_BaseAppearance.Visible)
            {
                return;
            }

            var color = m_IsSelected ? m_SelectionAppearance.Color : m_BaseAppearance.Color;
            var gradientEnabled = m_IsSelected
                ? m_SelectionAppearance.GradientEnabled
                : m_BaseAppearance.GradientEnabled;
            
            m_PropertyBlock.Clear();
            m_PropertyBlock.SetColor(s_BaseColorId, color);
            m_PropertyBlock.SetFloat(s_GradientEnabledId, gradientEnabled ? 1f : 0f);
            m_Renderer.SetPropertyBlock(m_PropertyBlock);
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
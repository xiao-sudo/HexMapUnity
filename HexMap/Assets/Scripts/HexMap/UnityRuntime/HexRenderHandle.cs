using System;

namespace HexMap.UnityRuntime
{
    internal sealed class HexRenderHandle
    {
        private readonly int m_Generation;
        private readonly IHexRenderTarget m_Target;
        private bool m_IsValid = true;
        private bool m_HasBaseAppearance;
        private HexAppearance m_BaseAppearance;
        private bool m_IsSelected;
        private HexSelectionAppearance m_SelectionAppearance;

        internal HexRenderHandle(IHexRenderTarget target, int generation)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            m_Target = target;
            m_Generation = generation;
        }

        public bool IsValid
        {
            get { return m_IsValid && m_Target != null && m_Target.IsValid; }
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
            if (!m_IsValid)
            {
                return;
            }

            m_IsSelected = false;
            m_IsValid = false;
            m_Target.Invalidate();
        }

        private void ApplyAppearance()
        {
            if (!m_HasBaseAppearance)
            {
                return;
            }

            var color = m_IsSelected ? m_SelectionAppearance.Color : m_BaseAppearance.Color;
            var gradientEnabled = m_IsSelected
                ? m_SelectionAppearance.GradientEnabled
                : m_BaseAppearance.GradientEnabled;
            m_Target.Apply(new HexAppearance(m_BaseAppearance.Visible, color, gradientEnabled));
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
using System;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    internal sealed class DrawMeshInstancedTarget : IHexRenderTarget
    {
        private readonly Matrix4x4 m_LocalMatrix;
        private readonly bool m_UseLinearColorSpace;
        private HexAppearance m_Appearance;
        private bool m_IsInvalidated;

        public DrawMeshInstancedTarget(Matrix4x4 localMatrix, bool useLinearColorSpace)
        {
            m_LocalMatrix = localMatrix;
            m_UseLinearColorSpace = useLinearColorSpace;
            m_Appearance = new HexAppearance(false, Color.clear, false);
        }

        public bool IsValid
        {
            get { return !m_IsInvalidated; }
        }

        public void Apply(HexAppearance appearance)
        {
            EnsureValid();
            m_Appearance = appearance;
        }

        public void Invalidate()
        {
            m_IsInvalidated = true;
        }

        public void WriteTo(
            Matrix4x4 parentMatrix,
            Matrix4x4[] matrices,
            Vector4[] colors,
            float[] gradientEnabled,
            float[] visible,
            int index)
        {
            EnsureValid();
            matrices[index] = parentMatrix * m_LocalMatrix;
            var color = m_UseLinearColorSpace
                ? m_Appearance.Color.linear
                : m_Appearance.Color;
            colors[index] = new Vector4(
                color.r,
                color.g,
                color.b,
                color.a);
            gradientEnabled[index] = m_Appearance.GradientEnabled ? 1f : 0f;
            visible[index] = m_Appearance.Visible ? 1f : 0f;
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

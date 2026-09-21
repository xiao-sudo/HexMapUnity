using System;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    [Serializable]
    public struct StaticDecorationPlacement
    {
        [SerializeField] private Vector3 m_Position;
        [SerializeField] private Vector3 m_EulerAngles;
        [SerializeField] private Vector3 m_Scale;

        public Vector3 Position { get { return m_Position; } }
        public Quaternion Rotation { get { return Quaternion.Euler(m_EulerAngles); } }
        public Vector3 Scale { get { return m_Scale; } }

        public StaticDecorationPlacement(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            m_Position = position;
            m_EulerAngles = rotation.eulerAngles;
            m_Scale = scale;
        }
    }
}

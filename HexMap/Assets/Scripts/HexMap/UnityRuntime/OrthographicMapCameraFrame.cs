using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>The complete orthographic view calculated for a map, without a Unity Camera dependency.</summary>
    public readonly struct OrthographicMapCameraFrame
    {
        public OrthographicMapCameraFrame(
            Vector3 position,
            Quaternion rotation,
            float orthographicSize,
            float nearClipPlane,
            float farClipPlane,
            int cullingMask)
        {
            Position = position;
            Rotation = rotation;
            OrthographicSize = orthographicSize;
            NearClipPlane = nearClipPlane;
            FarClipPlane = farClipPlane;
            CullingMask = cullingMask;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float OrthographicSize { get; }
        public float NearClipPlane { get; }
        public float FarClipPlane { get; }
        public int CullingMask { get; }
        public bool HasCullingMask { get { return CullingMask >= 0; } }
    }
}
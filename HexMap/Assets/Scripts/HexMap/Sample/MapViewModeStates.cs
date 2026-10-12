using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    internal interface IMapViewModeHost
    {
        void EnterMode(Camera camera, int channel);
    }

    internal interface IMapViewModeState
    {
        MapViewMode Mode { get; }

        void Enter(IMapViewModeHost host);

        void Exit();
    }

    /// <summary>Normal gameplay through the single base camera.</summary>
    internal sealed class MapGameplayViewState : IMapViewModeState
    {
        private readonly Camera m_Camera;
        private readonly int m_Channel;

        public MapGameplayViewState(Camera camera, int channel)
        {
            m_Camera = camera;
            m_Channel = channel;
        }

        public MapViewMode Mode
        {
            get { return MapViewMode.Gameplay; }
        }

        public void Enter(IMapViewModeHost host)
        {
            host.EnterMode(m_Camera, m_Channel);
        }

        public void Exit()
        {
        }
    }

    /// <summary>
    /// The whole map through the gameplay camera. This state owns every temporary change it makes to that
    /// camera, so leaving the mode always restores exactly the view that was active on entry.
    /// </summary>
    internal sealed class MapTopDownViewState : IMapViewModeState
    {
        private readonly Camera m_Camera;
        private readonly OrthographicMapCamera m_MapCamera;
        private readonly int m_Channel;

        private CameraViewState m_RememberedCameraState;
        private bool m_HasRememberedCameraState;

        public MapTopDownViewState(
            Camera camera,
            OrthographicMapCamera mapCamera,
            int channel)
        {
            m_Camera = camera;
            m_MapCamera = mapCamera;
            m_Channel = channel;
        }

        public MapViewMode Mode
        {
            get { return MapViewMode.TopDown; }
        }

        public void Enter(IMapViewModeHost host)
        {
            m_RememberedCameraState = CameraViewState.Capture(m_Camera);
            m_HasRememberedCameraState = true;

            if (m_Camera != null)
            {
                m_Camera.orthographic = true;
            }

            if (m_MapCamera != null)
            {
                m_MapCamera.FrameChanged += ApplyFrame;
                if (m_Camera != null)
                {
                    m_MapCamera.ViewportAspect = m_Camera.aspect;
                }

                string error;
                if (!m_MapCamera.TryRefresh(out error))
                {
                    Debug.LogWarning("MapTopDownViewState could not frame the map: " + error);
                }
            }

            host.EnterMode(m_Camera, m_Channel);
        }

        public void Exit()
        {
            if (m_MapCamera != null)
            {
                m_MapCamera.FrameChanged -= ApplyFrame;
            }

            if (!m_HasRememberedCameraState)
            {
                return;
            }

            m_RememberedCameraState.Restore(m_Camera);
            m_HasRememberedCameraState = false;
        }

        private void ApplyFrame(OrthographicMapCameraFrame frame)
        {
            if (m_Camera == null)
            {
                return;
            }

            m_Camera.orthographic = true;
            m_Camera.transform.SetPositionAndRotation(frame.Position, frame.Rotation);
            m_Camera.orthographicSize = frame.OrthographicSize;
            m_Camera.nearClipPlane = frame.NearClipPlane;
            m_Camera.farClipPlane = frame.FarClipPlane;
            if (frame.HasCullingMask)
            {
                m_Camera.cullingMask = frame.CullingMask;
            }
        }
    }

    /// <summary>A camera view that can be restored without leaking map-view details to gameplay.</summary>
    internal readonly struct CameraViewState
    {
        private CameraViewState(
            Vector3 position,
            Quaternion rotation,
            bool orthographic,
            float fieldOfView,
            float orthographicSize,
            float nearClipPlane,
            float farClipPlane,
            int cullingMask)
        {
            Position = position;
            Rotation = rotation;
            Orthographic = orthographic;
            FieldOfView = fieldOfView;
            OrthographicSize = orthographicSize;
            NearClipPlane = nearClipPlane;
            FarClipPlane = farClipPlane;
            CullingMask = cullingMask;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public bool Orthographic { get; }

        public float FieldOfView { get; }

        public float OrthographicSize { get; }

        public float NearClipPlane { get; }

        public float FarClipPlane { get; }

        public int CullingMask { get; }

        public static CameraViewState Capture(Camera camera)
        {
            if (camera == null)
            {
                return default(CameraViewState);
            }

            var transform = camera.transform;
            return new CameraViewState(
                transform.position,
                transform.rotation,
                camera.orthographic,
                camera.fieldOfView,
                camera.orthographicSize,
                camera.nearClipPlane,
                camera.farClipPlane,
                camera.cullingMask);
        }

        public void Restore(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            var transform = camera.transform;
            transform.position = Position;
            transform.rotation = Rotation;
            camera.orthographic = Orthographic;
            camera.fieldOfView = FieldOfView;
            camera.orthographicSize = OrthographicSize;
            camera.nearClipPlane = NearClipPlane;
            camera.farClipPlane = FarClipPlane;
            camera.cullingMask = CullingMask;
        }
    }
}

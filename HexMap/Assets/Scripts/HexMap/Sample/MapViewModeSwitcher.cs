using HexMap.UnityRuntime;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HexMap.Sample
{
    /// <summary>
    /// Owns which way the map is being looked at, and nothing else.
    /// <para>
    /// It is the single owner of the mode, the single place that moves a camera between URP stacks, and
    /// the single place that tells the click dispatcher which camera and channel are live. Keeping those
    /// three in one step is what makes a switch atomic: no frame can render the new mode while still
    /// resolving clicks through the old one.
    /// </para>
    /// <para>
    /// Both cameras stay in the scene and enabled states are swapped. The UI camera is never rebuilt and
    /// never moved; only the stack it belongs to changes, because a URP overlay camera is only rendered
    /// while its base camera is rendered.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapViewModeSwitcher : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The camera used during normal gameplay. It keeps the AudioListener and the MainCamera tag.")]
        private Camera m_GameplayCamera;

        [SerializeField]
        [Tooltip("The orthographic camera used for the whole map view. It must not carry the MainCamera tag.")]
        private Camera m_TopDownCamera;

        [SerializeField]
        [Tooltip("The UI camera. It is an overlay that is moved between the two base cameras' stacks.")]
        private Camera m_UiCamera;

        [SerializeField]
        private OrthographicMapCamera m_MapCamera;

        [SerializeField]
        private MapClickDispatcher m_Dispatcher;

        [SerializeField]
        [Tooltip("Shown during normal gameplay. Goes inactive while the map view is up.")]
        private GameObject m_GameplayUiRoot;

        [SerializeField]
        [Tooltip("Shown while the map view is up. Goes inactive during normal gameplay.")]
        private GameObject m_TopDownUiRoot;

        [SerializeField]
        [Tooltip("Optional. When set, opening the map view aims the camera at it and zooms in.")]
        private Transform m_FocusTarget;

        [SerializeField]
        [Tooltip("Zoom to use when opening the map view with a focus target assigned.")]
        private float m_FocusZoom = 3f;

        [SerializeField]
        private bool m_IsTopDown;

        private CameraState m_GameplayCameraState;
        private bool m_HasGameplayCameraState;

        /// <summary>
        /// True while the whole map view is up.
        /// </summary>
        public bool IsTopDown
        {
            get { return m_IsTopDown; }
        }

        public Camera GameplayCamera
        {
            get { return m_GameplayCamera; }
            set { m_GameplayCamera = value; }
        }

        public Camera TopDownCamera
        {
            get { return m_TopDownCamera; }
            set { m_TopDownCamera = value; }
        }

        public Camera UiCamera
        {
            get { return m_UiCamera; }
            set { m_UiCamera = value; }
        }

        public OrthographicMapCamera MapCamera
        {
            get { return m_MapCamera; }
            set { m_MapCamera = value; }
        }

        public MapClickDispatcher Dispatcher
        {
            get { return m_Dispatcher; }
            set { m_Dispatcher = value; }
        }

        public GameObject GameplayUiRoot
        {
            get { return m_GameplayUiRoot; }
            set { m_GameplayUiRoot = value; }
        }

        public GameObject TopDownUiRoot
        {
            get { return m_TopDownUiRoot; }
            set { m_TopDownUiRoot = value; }
        }

        public Transform FocusTarget
        {
            get { return m_FocusTarget; }
            set { m_FocusTarget = value; }
        }

        private void Start()
        {
            // Put the scene into a known state without switching anything: the first click must already
            // find the right camera and channel, even if the mode was left toggled on in the inspector.
            if (m_IsTopDown)
            {
                ApplyTopDown();
            }
            else
            {
                ApplyGameplay();
            }
        }

        /// <summary>
        /// Switches to the other mode. Bind this to the map button.
        /// </summary>
        public void Toggle()
        {
            if (m_IsTopDown)
            {
                ExitTopDown();
            }
            else
            {
                EnterTopDown();
            }
        }

        /// <summary>
        /// Shows the whole map view.
        /// </summary>
        public void EnterTopDown()
        {
            if (m_IsTopDown)
            {
                return;
            }

            SnapshotGameplayCamera();
            m_IsTopDown = true;
            ApplyTopDown();
        }

        /// <summary>
        /// Returns to normal gameplay, putting the gameplay camera back exactly where it was.
        /// </summary>
        public void ExitTopDown()
        {
            if (!m_IsTopDown)
            {
                return;
            }

            m_IsTopDown = false;
            ApplyGameplay();
        }

        private void ApplyTopDown()
        {
            MoveUiCameraIntoStack(m_TopDownCamera);

            if (m_GameplayCamera != null)
            {
                m_GameplayCamera.enabled = false;
            }

            if (m_TopDownCamera != null)
            {
                m_TopDownCamera.enabled = true;
            }

            // Both pushes happen in the same step as the camera swap so no frame can resolve a click with
            // the previous mode's camera.
            if (m_Dispatcher != null)
            {
                m_Dispatcher.SetActiveCamera(m_TopDownCamera);
                m_Dispatcher.SetActiveChannel(SampleMapClickChannels.TopDown);
            }

            SetUiRootActive(m_TopDownUiRoot, true);
            SetUiRootActive(m_GameplayUiRoot, false);

            FocusIfRequested();
        }

        private void ApplyGameplay()
        {
            MoveUiCameraIntoStack(m_GameplayCamera);

            if (m_TopDownCamera != null)
            {
                m_TopDownCamera.enabled = false;
            }

            if (m_GameplayCamera != null)
            {
                m_GameplayCamera.enabled = true;
            }

            RestoreGameplayCamera();

            if (m_Dispatcher != null)
            {
                m_Dispatcher.SetActiveCamera(m_GameplayCamera);
                m_Dispatcher.SetActiveChannel(SampleMapClickChannels.Gameplay);
            }

            SetUiRootActive(m_GameplayUiRoot, true);
            SetUiRootActive(m_TopDownUiRoot, false);
        }

        private void FocusIfRequested()
        {
            if (m_FocusTarget == null || m_MapCamera == null)
            {
                // No focus target means the map view opens the way the player left it.
                return;
            }

            string error;
            if (!m_MapCamera.FocusOnWorld(m_FocusTarget.position, out error))
            {
                Debug.LogWarning("MapViewModeSwitcher could not focus the map view: " + error, this);
                return;
            }

            m_MapCamera.SetZoomImmediate(m_FocusZoom);
        }

        private void SnapshotGameplayCamera()
        {
            if (m_GameplayCamera == null)
            {
                m_HasGameplayCameraState = false;
                return;
            }

            var transform = m_GameplayCamera.transform;
            m_GameplayCameraState = new CameraState(
                transform.position,
                transform.rotation,
                m_GameplayCamera.fieldOfView,
                m_GameplayCamera.orthographicSize);
            m_HasGameplayCameraState = true;
        }

        private void RestoreGameplayCamera()
        {
            if (!m_HasGameplayCameraState || m_GameplayCamera == null)
            {
                return;
            }

            var transform = m_GameplayCamera.transform;
            transform.position = m_GameplayCameraState.Position;
            transform.rotation = m_GameplayCameraState.Rotation;
            m_GameplayCamera.fieldOfView = m_GameplayCameraState.FieldOfView;
            m_GameplayCamera.orthographicSize = m_GameplayCameraState.OrthographicSize;
        }

        /// <summary>
        /// Moves the UI camera into a base camera's stack. A URP overlay camera is only rendered while its
        /// base camera is rendered, so leaving it in the stack of a disabled camera blanks the UI.
        /// </summary>
        private void MoveUiCameraIntoStack(Camera baseCamera)
        {
            if (m_UiCamera == null || baseCamera == null)
            {
                return;
            }

            // Remove it from wherever it currently is first: an overlay may belong to one stack only, and
            // adding it twice makes URP reject the stack.
            if (m_GameplayCamera != null && m_GameplayCamera != baseCamera)
            {
                RemoveUiCameraFrom(m_GameplayCamera);
            }

            if (m_TopDownCamera != null && m_TopDownCamera != baseCamera)
            {
                RemoveUiCameraFrom(m_TopDownCamera);
            }

            var baseData = baseCamera.GetUniversalAdditionalCameraData();
            if (!baseData.cameraStack.Contains(m_UiCamera))
            {
                baseData.cameraStack.Add(m_UiCamera);
            }
        }

        private void RemoveUiCameraFrom(Camera baseCamera)
        {
            // A base camera is the only kind that has a stack; asking an overlay for one returns null.
            var data = baseCamera.GetUniversalAdditionalCameraData();
            if (data.renderType != CameraRenderType.Base)
            {
                return;
            }

            data.cameraStack.Remove(m_UiCamera);
        }

        private static void SetUiRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        private readonly struct CameraState
        {
            public CameraState(Vector3 position, Quaternion rotation, float fieldOfView, float orthographicSize)
            {
                Position = position;
                Rotation = rotation;
                FieldOfView = fieldOfView;
                OrthographicSize = orthographicSize;
            }

            public Vector3 Position { get; }

            public Quaternion Rotation { get; }

            public float FieldOfView { get; }

            public float OrthographicSize { get; }
        }
    }
}

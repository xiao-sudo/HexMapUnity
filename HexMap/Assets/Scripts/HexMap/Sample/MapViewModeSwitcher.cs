using HexMap.UnityRuntime;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HexMap.Sample
{
    /// <summary>
    /// Owns which way the map is being looked at, and nothing else.
    /// <para>
    /// The modes themselves are states (<see cref="MapGameplayViewState"/>, <see cref="MapTopDownViewState"/>),
    /// each of which knows how to put its own view up, and each of which keeps whatever it needs to undo that.
    /// This component is the driver around them: it decides when a mode changes, refuses a change that is not
    /// one, tells the incoming mode what it is replacing, and keeps the one thing that belongs to no single
    /// mode — where the UI camera's stack membership goes.
    /// </para>
    /// <para>
    /// A switch is one synchronous step: exit, reset, enter. That is what makes it atomic, so no frame can
    /// render the new mode while still resolving clicks through the old one, and it is why the driver is the
    /// only place that ever needs to know the ordering.
    /// </para>
    /// <para>
    /// Both cameras stay in the scene and enabled states are swapped. The UI camera is never rebuilt and
    /// never moved; only the stack it belongs to changes, because a URP overlay camera is only rendered
    /// while its base camera is rendered. Moving it also means owning its render type: URP skips a stack
    /// member that is still a base camera, so moving the UI camera into a stack also marks it an overlay.
    /// </para>
    /// <para>
    /// Adding a mode means adding a state and registering it in <see cref="CreateStates"/>. Nothing else
    /// here has to learn its name: the driver resets whatever the states declare, not what it knows.
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
        [Tooltip("Shown during normal gameplay. Goes inactive while the map view is up.")]
        private GameObject m_GameplayUiRoot;

        [SerializeField]
        [Tooltip("Shown while the map view is up. Goes inactive during normal gameplay.")]
        private GameObject m_TopDownUiRoot;

        [SerializeField]
        [Tooltip("Receives the active camera and click channel whenever the mode changes.")]
        private MapClickDispatcher m_Dispatcher;

        [SerializeField]
        [Tooltip("Which mode the scene starts in. Set to gameplay so the first switch has a camera pose to restore.")]
        private MapViewMode m_StartMode;

        private MapViewModeContext m_Context;
        private IMapViewModeState m_GameplayState;
        private IMapViewModeState m_TopDownState;
        private IMapViewModeState m_CurrentState;
        private bool m_HasReportedSharedCameraWiring;

        /// <summary>
        /// The mode the app is in now. Derived from the current state rather than tracked beside it, so
        /// there is one answer to "which mode is this": before the first mode is entered it is the
        /// serialized start mode.
        /// </summary>
        public MapViewMode CurrentMode
        {
            get { return m_CurrentState != null ? m_CurrentState.Mode : m_StartMode; }
        }

        /// <summary>
        /// The wiring this switcher currently holds, read back as the same shape <see cref="Configure"/>
        /// takes. It exists for the editor wiring tool, which has to know which UI roots to hang buttons and
        /// panels under without being handed a second way to write them: the fields stay private, and the
        /// one writer is still <see cref="Configure"/>.
        /// </summary>
        public MapViewModeWiring Wiring
        {
            get
            {
                return new MapViewModeWiring(
                    m_GameplayCamera,
                    m_TopDownCamera,
                    m_UiCamera,
                    m_GameplayUiRoot,
                    m_TopDownUiRoot,
                    m_Dispatcher,
                    m_StartMode);
            }
        }

        private void Start()
        {
            ApplySerializedMode();
        }

        /// <summary>
        /// Replaces the scene wiring. Everything that is serialized can also be set from code, which is how
        /// the editor wiring tool and the tests build a rig; the state objects themselves are built later, on
        /// first use, so configuring a switcher never has to be paired with a particular call order.
        /// </summary>
        public void Configure(in MapViewModeWiring wiring)
        {
            m_GameplayCamera = wiring.GameplayCamera;
            m_TopDownCamera = wiring.TopDownCamera;
            m_UiCamera = wiring.UiCamera;
            m_GameplayUiRoot = wiring.GameplayUiRoot;
            m_TopDownUiRoot = wiring.TopDownUiRoot;
            m_Dispatcher = wiring.Dispatcher;
            m_StartMode = wiring.StartMode;
        }

        /// <summary>
        /// Puts the scene into the mode the serialized field says it is in. This is what <c>Start</c> runs,
        /// and it is reachable so a test can drive it, because start callbacks do not run in edit mode.
        /// <para>
        /// It does not leave a mode first — nothing has been presented yet — but it does clear the stage
        /// before entering, exactly like an ordinary switch. That is the one entry path there is: there is no
        /// separate "apply" variant that could drift out of step with the switch, and no reliance on how the
        /// scene happened to be left in the inspector.
        /// </para>
        /// </summary>
        public void ApplySerializedMode()
        {
            EnsureStates();
            SetMode(m_StartMode);
        }

        /// <summary>
        /// Switches to the other mode. Bind this to the map button.
        /// </summary>
        public void Toggle()
        {
            EnsureStates();

            // Before the first mode is entered there is no current state, and CurrentMode answers with the
            // start mode, so this reads as "go to the mode that is not the one we are in" in every case
            // including the very first press.
            SetMode(CurrentMode == MapViewMode.Gameplay ? MapViewMode.TopDown : MapViewMode.Gameplay);
        }

        private void SetMode(MapViewMode target)
        {
            if (m_CurrentState != null && m_CurrentState.Mode == target)
            {
                // Pressing the button twice is not a mode change, and must not re-run any of it: a second
                // capture would replace the pose the player is owed with wherever the camera is now.
                //
                // The comparison is against the state, never against CurrentMode: before the first entry
                // CurrentMode answers with the start mode, so asking it would call "start in the map view"
                // a repeat of the map view and skip the whole entry.
                return;
            }

            if (m_CurrentState != null)
            {
                // The first entry has no previous mode to leave, which is the only difference between
                // starting a scene in a mode and switching into one. Whatever has to be put back on a return
                // is remembered by the mode being left, inside its own Exit.
                m_CurrentState.Exit(m_Context);
            }

            // The reset runs on the first entry too, not only on a switch, because it is the only thing that
            // switches anything off: entering a mode turns that mode's own camera and root on, and no state
            // ever touches another mode's. Without it on the first entry a scene authored to start in the
            // map view keeps both cameras enabled.
            ResetPresentation();

            // The state is written before it is entered, so CurrentMode already answers with the new mode
            // while Enter runs. An Enter that throws therefore leaves the switcher believing it is in a mode
            // it only partly presented, which is deliberate: the alternative is a switcher that reports the
            // old mode while the new one is half up.
            m_CurrentState = GetState(target);
            m_CurrentState.Enter(m_Context);
        }

        private void EnsureStates()
        {
            if (m_CurrentState != null)
            {
                return;
            }

            m_Context = new MapViewModeContext(this);
            CreateStates();
        }

        private void CreateStates()
        {
            // Both states are handed the same UI camera: each has to move it into its own stack on the way
            // in, and neither owns it — which stacks it may belong to is still decided in one place.
            //
            // Nothing is handed across: the gameplay state keeps the pose it has to give back, because it is
            // the mode that knows when its camera stops and starts being used. The map view is given only
            // what it needs to show itself.
            m_GameplayState = new MapGameplayViewState(
                m_GameplayCamera,
                m_UiCamera,
                m_GameplayUiRoot,
                SampleMapClickChannels.For(MapViewMode.Gameplay));

            m_TopDownState = new MapTopDownViewState(
                m_TopDownCamera,
                m_UiCamera,
                m_TopDownUiRoot,
                SampleMapClickChannels.For(MapViewMode.TopDown));
        }

        private IMapViewModeState GetState(MapViewMode mode)
        {
            return mode == MapViewMode.TopDown ? m_TopDownState : m_GameplayState;
        }

        /// <summary>
        /// Clears the stage before a mode is entered: every camera and UI root any mode declared, plus the
        /// click channel, which is pointed at nothing. Runs on the first entry as well as on a switch, which
        /// is what makes "exactly one mode is presenting" true from the very first frame.
        /// <para>
        /// The channel is cleared first so that the window between two modes is "no mode reacts to clicks"
        /// rather than "the old mode's camera with the new mode's channel". The list is flat and belongs to
        /// the states, not to this component, which is why adding a mode needs no change here.
        /// </para>
        /// </summary>
        private void ResetPresentation()
        {
            if (m_Dispatcher != null)
            {
                m_Dispatcher.SetActiveCamera(null);
                m_Dispatcher.SetActiveChannel(MapClickChannels.None);
            }

            HideAll(m_GameplayState);
            HideAll(m_TopDownState);
        }

        private static void HideAll(IMapViewModeState state)
        {
            var presentations = state.Presentations;
            for (var i = 0; i < presentations.Length; i++)
            {
                presentations[i].Hide();
            }
        }

        /// <summary>
        /// Puts this mode's camera in charge of clicks and its channel in charge of handlers, in one step.
        /// Called by the context while a state is being entered.
        /// </summary>
        internal void EnterMode(Camera camera, int channel)
        {
            if (camera != null)
            {
                camera.enabled = true;
            }

            if (m_Dispatcher != null)
            {
                m_Dispatcher.SetActiveCamera(camera);
                m_Dispatcher.SetActiveChannel(channel);
            }
        }

        /// <summary>Shows or hides a UI root, leaving it alone when it is already in that state.</summary>
        internal void SetUiRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        /// <summary>
        /// Moves the UI camera into a base camera's stack. A URP overlay camera is only rendered while its
        /// base camera is rendered, so leaving it in the stack of a disabled camera blanks the UI.
        /// <para>
        /// Both arguments come from the calling state, which is why the same UI camera is passed by both:
        /// the states hold the reference, this method holds the rule. It used to read the UI camera off this
        /// component, which would have been a second place the same fact could be wrong.
        /// </para>
        /// </summary>
        internal void MoveUiCameraIntoStack(Camera baseCamera, Camera uiCamera)
        {
            if (uiCamera == null || baseCamera == null)
            {
                return;
            }

            if (uiCamera == m_GameplayCamera || uiCamera == m_TopDownCamera)
            {
                // The same camera cannot be a base camera and the overlay it renders through. Marking it
                // an overlay would also make its own cameraStack invalid, so a stack is left alone here.
                ReportSharedCameraWiring();
                return;
            }

            // The UI camera has to be an overlay before it joins a stack: URP skips a stack member that
            // is still a base camera, warning every frame while the UI quietly renders nothing, and a
            // fresh UniversalAdditionalCameraData is a base camera. This is the only owner of stack
            // membership, so it owns the render type too.
            uiCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;

            // Remove it from wherever it currently is first: an overlay may belong to one stack only, and
            // adding it twice makes URP reject the stack.
            if (m_GameplayCamera != null && m_GameplayCamera != baseCamera)
            {
                RemoveUiCameraFrom(m_GameplayCamera, uiCamera);
            }

            if (m_TopDownCamera != null && m_TopDownCamera != baseCamera)
            {
                RemoveUiCameraFrom(m_TopDownCamera, uiCamera);
            }

            var baseData = baseCamera.GetUniversalAdditionalCameraData();
            if (!baseData.cameraStack.Contains(uiCamera))
            {
                baseData.cameraStack.Add(uiCamera);
            }
        }

        /// <summary>
        /// Reports a camera wired into two roles. It repeats on every switch, so it is reported once per
        /// switcher: this is a scene wiring mistake, not a per-frame condition.
        /// </summary>
        private void ReportSharedCameraWiring()
        {
            if (m_HasReportedSharedCameraWiring)
            {
                return;
            }

            m_HasReportedSharedCameraWiring = true;
            Debug.LogError(
                "MapViewModeSwitcher has the same camera wired as the UI camera and as a base camera; "
                    + "the camera stack was left alone. Wire a separate overlay camera for the UI.",
                this);
        }

        private void RemoveUiCameraFrom(Camera baseCamera, Camera uiCamera)
        {
            // A base camera is the only kind that has a stack; asking an overlay for one returns null.
            var data = baseCamera.GetUniversalAdditionalCameraData();
            if (data.renderType != CameraRenderType.Base)
            {
                return;
            }

            data.cameraStack.Remove(uiCamera);
        }
    }
}

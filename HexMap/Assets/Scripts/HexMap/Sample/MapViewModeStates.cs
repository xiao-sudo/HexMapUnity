using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// One way of looking at the map, described by what it turns on rather than by which branch of a
    /// switcher it is.
    /// <para>
    /// A state holds its own camera, its own UI root, the shared UI camera and its own click channel. It
    /// does not know that another mode exists, and nothing about it depends on which state ran before it:
    /// whatever a mode has to put back on its return is remembered by that mode itself, on the way out.
    /// </para>
    /// <para>
    /// Both states hold the same UI camera because each has to move it into its own stack on the way in.
    /// Holding a reference is not owning it: the rule for which stacks it may belong to lives in one place.
    /// </para>
    /// </summary>
    internal interface IMapViewModeState
    {
        /// <summary>Which mode this state presents.</summary>
        MapViewMode Mode { get; }

        /// <summary>
        /// The things this mode may switch on. The driver switches all of them off before the next mode is
        /// entered, which is what lets every <see cref="Enter"/> start from an empty stage and lets a third
        /// mode be added without touching this one.
        /// </summary>
        MapViewPresentation[] Presentations { get; }

        /// <summary>
        /// Puts the app into this mode: what it shows, which camera picks clicks, and which channel hears
        /// them, all in one step. Also where a mode puts back whatever it set aside in <see cref="Exit"/>.
        /// <para>
        /// Repeating an entry is not required to be safe. Whether the mode is actually changing is known
        /// only to the driver, and the driver refuses a switch to the mode already in effect.
        /// </para>
        /// </summary>
        void Enter(MapViewModeContext context);

        /// <summary>
        /// Leaves this mode. This runs before the driver hides the presentation, so it is the place for the
        /// work that belongs to leaving rather than to being gone — remembering a camera, for instance.
        /// Turning its own things off is not required here: the driver does that for every mode at once.
        /// </summary>
        void Exit(MapViewModeContext context);
    }

    /// <summary>
    /// Normal gameplay: the perspective camera that follows the action, with the gameplay UI up and the map
    /// gestures uninteresting because the map camera is off.
    /// <para>
    /// This mode does not aim the gameplay camera — it is the player's camera, moved by gameplay code that
    /// knows nothing about view modes. But it is the only mode that knows when that camera stops and starts
    /// being used, so it is the mode that remembers the pose on the way out and puts it back on the way in.
    /// The map view knows nothing about it.
    /// </para>
    /// </summary>
    internal sealed class MapGameplayViewState : IMapViewModeState
    {
        private readonly Camera m_Camera;
        private readonly Camera m_UiCamera;
        private readonly GameObject m_UiRoot;
        private readonly int m_Channel;
        private readonly MapViewPresentation[] m_Presentations;

        private GameplayCameraState m_RememberedCameraState;
        private bool m_HasRememberedCameraState;

        public MapGameplayViewState(Camera camera, Camera uiCamera, GameObject uiRoot, int channel)
        {
            m_Camera = camera;
            m_UiCamera = uiCamera;
            m_UiRoot = uiRoot;
            m_Channel = channel;
            m_Presentations = new[]
            {
                MapViewPresentation.OfCamera(m_Camera),
                MapViewPresentation.OfUiRoot(m_UiRoot),
            };
        }

        public MapViewMode Mode
        {
            get { return MapViewMode.Gameplay; }
        }

        public MapViewPresentation[] Presentations
        {
            get { return m_Presentations; }
        }

        public void Enter(MapViewModeContext context)
        {
            // Put the player's camera back where this mode left it. On the very first entry there is nothing
            // to put back — a scene whose start mode is the map view has never been in gameplay — and the
            // camera is left where the scene put it, which is the only sensible answer in that case.
            if (m_HasRememberedCameraState)
            {
                m_RememberedCameraState.Restore(m_Camera);
            }

            // The camera and the channel move together: a click arriving while only one of them had changed
            // would be picked with this camera and routed to the other mode's handler.
            context.Enter(m_Camera, m_Channel);
            context.ShowOwnView(m_UiRoot);

            // The UI camera follows the mode. An overlay camera renders only while its base camera does, so
            // leaving it in the map view's stack would blank the UI the moment the map view went down.
            context.EnterOwnUiStack(m_Camera, m_UiCamera);
        }

        public void Exit(MapViewModeContext context)
        {
            // This is the last moment the camera is still the one the player was using, so this is when its
            // pose is worth keeping.
            //
            // Every departure records, replacing whatever was recorded before, because that is what "the view
            // the player left" means: entering the map view from wherever gameplay has since been put should
            // come back to that place, not to wherever the camera was the first time this mode was left.
            m_RememberedCameraState = GameplayCameraState.Capture(m_Camera);
            m_HasRememberedCameraState = true;
        }
    }

    /// <summary>
    /// The whole map seen from straight above, with the map UI up and the gameplay camera put away.
    /// <para>
    /// This mode owns nothing but its own view. The gameplay camera it replaces is remembered by that mode,
    /// not by this one: taking it out of service and putting it back are the same mode's business, and this
    /// mode needs to know nothing about either.
    /// </para>
    /// </summary>
    internal sealed class MapTopDownViewState : IMapViewModeState
    {
        private readonly Camera m_Camera;
        private readonly Camera m_UiCamera;
        private readonly GameObject m_UiRoot;
        private readonly int m_Channel;
        private readonly MapViewPresentation[] m_Presentations;

        public MapTopDownViewState(Camera camera, Camera uiCamera, GameObject uiRoot, int channel)
        {
            m_Camera = camera;
            m_UiCamera = uiCamera;
            m_UiRoot = uiRoot;
            m_Channel = channel;
            m_Presentations = new[]
            {
                MapViewPresentation.OfCamera(m_Camera),
                MapViewPresentation.OfUiRoot(m_UiRoot),
            };
        }

        public MapViewMode Mode
        {
            get { return MapViewMode.TopDown; }
        }

        public MapViewPresentation[] Presentations
        {
            get { return m_Presentations; }
        }

        public void Enter(MapViewModeContext context)
        {
            context.Enter(m_Camera, m_Channel);
            context.ShowOwnView(m_UiRoot);
            context.EnterOwnUiStack(m_Camera, m_UiCamera);
        }

        public void Exit(MapViewModeContext context)
        {
            // Nothing to hand over. What the player is owed is remembered by the mode that is owed it.
        }
    }

    /// <summary>
    /// A camera's pose and lens, kept so a mode can put that camera back exactly as it found it. Restoring
    /// only the position would leave a camera aimed elsewhere with a different field of view, which is the
    /// difference between "the view the player left" and "roughly where they were".
    /// </summary>
    internal readonly struct GameplayCameraState
    {
        private GameplayCameraState(Vector3 position, Quaternion rotation, float fieldOfView, float orthographicSize)
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

        /// <summary>Reads a camera's pose now. A null camera produces a state that restores nothing.</summary>
        public static GameplayCameraState Capture(Camera camera)
        {
            if (camera == null)
            {
                return default(GameplayCameraState);
            }

            var transform = camera.transform;
            return new GameplayCameraState(
                transform.position,
                transform.rotation,
                camera.fieldOfView,
                camera.orthographicSize);
        }

        /// <summary>Puts a camera back where this state was captured.</summary>
        public void Restore(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            var transform = camera.transform;
            transform.position = Position;
            transform.rotation = Rotation;
            camera.fieldOfView = FieldOfView;
            camera.orthographicSize = OrthographicSize;
        }
    }
}

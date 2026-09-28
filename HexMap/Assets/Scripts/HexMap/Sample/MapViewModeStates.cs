using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// One way of looking at the map, described by what it turns on rather than by which branch of a
    /// switcher it is.
    /// <para>
    /// A state holds its own camera, its own UI root, the shared UI camera and its own click channel. It
    /// does not know that another mode exists, which is what leaves the driver free to keep the machinery
    /// that belongs to no single mode — where the UI camera's stack membership goes, and where the gameplay
    /// camera was — without either mode owning it.
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
        /// them, all in one step.
        /// <para>
        /// <paramref name="previousMode"/> is null on the very first entry, and otherwise names the mode
        /// being left. It is passed in rather than looked up because the states deliberately do not know
        /// about each other, and because on the first entry there is no earlier state to ask: a mode that
        /// has to distinguish "the player was just in gameplay" from "the scene opened here" cannot read
        /// that off the driver's current state without the cold start breaking it.
        /// </para>
        /// <para>
        /// Repeating an entry is not required to be safe. Whether the mode is actually changing is known
        /// only to the driver, and the driver refuses a switch to the mode already in effect.
        /// </para>
        /// </summary>
        void Enter(MapViewMode? previousMode, MapViewModeContext context);

        /// <summary>
        /// Leaves this mode. This runs before the driver hides the presentation, so it is the place for the
        /// work that belongs to leaving rather than to being gone — restoring a camera, for instance.
        /// Turning its own things off is not required here: the driver does that for every mode at once.
        /// </summary>
        void Exit(MapViewModeContext context);
    }

    /// <summary>
    /// Normal gameplay: the perspective camera that follows the action, with the gameplay UI up and the map
    /// gestures uninteresting because the map camera is off.
    /// <para>
    /// This mode does not aim the gameplay camera. It is the player's camera, moved by gameplay code that
    /// knows nothing about view modes, so getting out of the way is all this mode has to do. Where that
    /// camera was is the driver's business, not this state's: see <see cref="MapViewModeSwitcher"/>.
    /// </para>
    /// </summary>
    internal sealed class MapGameplayViewState : IMapViewModeState
    {
        private readonly Camera m_Camera;
        private readonly Camera m_UiCamera;
        private readonly GameObject m_UiRoot;
        private readonly int m_Channel;
        private readonly MapViewPresentation[] m_Presentations;

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

        public void Enter(MapViewMode? previousMode, MapViewModeContext context)
        {
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
            // Nothing to undo: this mode's camera is the player's camera and this mode never aims it. The map
            // view remembers that pose when it takes the camera out of service, and puts it back on its way
            // out, so there is nothing for this mode to hand over or take back.
        }
    }

    /// <summary>
    /// The whole map seen from straight above, with the map UI up and the gameplay camera put away.
    /// <para>
    /// This is the mode that owns the gameplay camera's pose: entering it takes that camera out of service,
    /// so entering it is when the pose is remembered, and leaving it is when the pose is put back — position,
    /// rotation and lens parameters, not just position.
    /// </para>
    /// </summary>
    internal sealed class MapTopDownViewState : IMapViewModeState
    {
        private readonly Camera m_Camera;
        private readonly Camera m_UiCamera;
        private readonly Camera m_GameplayCamera;
        private readonly GameObject m_UiRoot;
        private readonly int m_Channel;
        private readonly MapViewPresentation[] m_Presentations;

        private GameplayCameraState m_GameplayCameraState;
        private bool m_HasGameplayCameraState;

        public MapTopDownViewState(Camera camera, Camera uiCamera, Camera gameplayCamera, GameObject uiRoot, int channel)
        {
            m_Camera = camera;
            m_UiCamera = uiCamera;
            m_GameplayCamera = gameplayCamera;
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

        public void Enter(MapViewMode? previousMode, MapViewModeContext context)
        {
            // Remember where the gameplay camera is, but only when gameplay is what this entry is leaving.
            // Two conditions have to hold together, and each rules out a different mistake:
            //
            // 1. previousMode == Gameplay: entering the map view from gameplay is the moment that camera
            //    stops being the one in use, so that is when its pose has to be remembered. A scene whose
            //    start mode is the map view never leaves gameplay, so it must not remember anything — there
            //    is no pose the player was ever given, and "restoring" one would drop the camera at the
            //    scene's default position on the first way out.
            // 2. !m_HasGameplayCameraState: remember once. A later entry must not overwrite the pose with
            //    wherever the map view has since moved the camera, or "the view the player left" would drift
            //    with every switch.
            if (previousMode == MapViewMode.Gameplay && !m_HasGameplayCameraState)
            {
                m_GameplayCameraState = GameplayCameraState.Capture(m_GameplayCamera);
                m_HasGameplayCameraState = true;
            }

            context.Enter(m_Camera, m_Channel);
            context.ShowOwnView(m_UiRoot);
            context.EnterOwnUiStack(m_Camera, m_UiCamera);
        }

        public void Exit(MapViewModeContext context)
        {
            // Give the gameplay camera back exactly where this mode found it. The pose is this mode's own
            // field, so remember-and-restore are one object's business and there is no shared flag that can
            // get out of step. Restore itself tolerates a missing camera.
            if (m_HasGameplayCameraState)
            {
                m_GameplayCameraState.Restore(m_GameplayCamera);
            }
        }
    }

    /// <summary>
    /// A camera's pose and lens, kept so the map view can put the gameplay camera back exactly as it found
    /// it. Restoring only the position would leave a camera aimed elsewhere with a different field of view,
    /// which is the difference between "the view the player left" and "roughly where they were".
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

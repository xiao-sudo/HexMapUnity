using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// Everything a state is allowed to do to the app. The state classes hold the scene references they
    /// were built with; this is the only seam through which they change the world, and it exists so the
    /// switcher keeps the two things that must not be duplicated: which camera stack the UI camera belongs
    /// to, and the gameplay camera's remembered pose.
    /// <para>
    /// The context holds no state of its own, so one instance per switcher is built once and handed to both
    /// states. It is a channel, not a container.
    /// </para>
    /// </summary>
    internal sealed class MapViewModeContext
    {
        private readonly MapViewModeSwitcher m_Switcher;

        internal MapViewModeContext(MapViewModeSwitcher switcher)
        {
            m_Switcher = switcher;
        }

        /// <summary>
        /// Puts this mode in charge of clicks. The active camera and the channel move in one step, because a
        /// frame that picked with the new mode's camera but routed through the old mode's handler would
        /// deliver a click to the wrong place.
        /// </summary>
        public void Enter(Camera camera, int channel)
        {
            m_Switcher.EnterMode(camera, channel);
        }

        /// <summary>
        /// Moves the UI camera into this mode's camera stack. Both modes call it with their own base camera,
        /// because an overlay camera only renders while its base camera does. The UI camera itself is handed
        /// in rather than looked up: it is the same camera for both modes, so no state owns it.
        /// </summary>
        public void EnterOwnUiStack(Camera baseCamera, Camera uiCamera)
        {
            m_Switcher.MoveUiCameraIntoStack(baseCamera, uiCamera);
        }

        /// <summary>
        /// Shows the UI root this mode owns. Only its own: the roots of modes that are not current were
        /// turned off by the driver before this mode was entered.
        /// </summary>
        public void ShowOwnView(GameObject root)
        {
            m_Switcher.SetUiRootActive(root, true);
        }

        /// <summary>Hides the UI root this mode owns.</summary>
        public void HideOwnView(GameObject root)
        {
            m_Switcher.SetUiRootActive(root, false);
        }
    }
}

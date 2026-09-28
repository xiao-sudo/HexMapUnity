namespace HexMap.Sample
{
    /// <summary>
    /// The ways the map can be looked at. The vocabulary belongs here, next to the states that implement
    /// the modes, so the runtime library never learns what a "top down" view is.
    /// <para>
    /// The names are the serialized values of a scene's start mode, so they are written out in scene files
    /// and driven by the editor wiring tool by name. Renaming a member is a scene migration, not a
    /// refactor.
    /// </para>
    /// </summary>
    public enum MapViewMode
    {
        /// <summary>Normal gameplay: the perspective camera that follows the action.</summary>
        Gameplay = 0,

        /// <summary>The whole map seen from straight above.</summary>
        TopDown = 1,
    }
}

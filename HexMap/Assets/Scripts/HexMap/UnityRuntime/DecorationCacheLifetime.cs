using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Releases the decoration geometry and material caches on the one lifecycle event nothing else owns.
    /// </summary>
    /// <remarks>
    /// The caches are static and outlive every component, so no component may dispose them: a
    /// decoration destroyed on its own must not free a mesh or material its neighbours still draw
    /// with.
    ///
    /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> is the only correct hook here,
    /// because it runs before any scene object exists. Entering play mode therefore drops whatever
    /// the editor built, and the scene's own decorations rebuild what they need in their own
    /// OnEnable. Running the same clear at <see cref="RuntimeInitializeLoadType.AfterSceneLoad"/>
    /// instead would destroy the meshes and materials those OnEnable calls had just assigned,
    /// leaving every decoration in the scene with a dead reference and drawing nothing. Tests clear
    /// the same two caches directly.
    /// </remarks>
    internal static class DecorationCacheLifetime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearBeforeFirstSceneLoads()
        {
            DecorationMeshFactory.Clear();
            DecorationMaterialCache.Clear();
        }
    }
}

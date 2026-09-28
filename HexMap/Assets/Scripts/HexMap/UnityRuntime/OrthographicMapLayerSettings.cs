using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Single owner for the render layers an orthographic map camera should see.
    /// <para>
    /// It holds the mask and nothing else. It deliberately keeps no <see cref="HexMapView"/> reference:
    /// the only consumer is the camera, which already has one, so a second reference here could only
    /// disagree with the camera's without adding any check the camera could not make itself. The layer
    /// to check the mask against is passed in by that consumer.
    /// </para>
    /// <para>
    /// This component never writes <see cref="HexMapView"/>'s own layer field; <see cref="TryValidate"/>
    /// only reports when the mask and the map's cell layer disagree, because "configured but not
    /// effective" is this feature's main failure mode.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrthographicMapLayerSettings : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Layers the map camera renders. Must include the HexMapView's cell layer.")]
        private LayerMask m_CullingMask = ~0;

        public LayerMask CullingMask
        {
            get { return m_CullingMask; }
            set { m_CullingMask = value; }
        }

        /// <summary>
        /// The mask value to hand to a camera.
        /// </summary>
        public int ResolvedCullingMask
        {
            get { return m_CullingMask.value; }
        }

        /// <summary>
        /// Reports whether the mask actually covers the map and whether the layer index is usable.
        /// </summary>
        /// <param name="cellLayer">The layer the map's cell meshes are assigned to, from the map view
        /// the camera is framing.</param>
        public bool TryValidate(int cellLayer, out string error)
        {
            if (cellLayer < 0 || cellLayer > 31)
            {
                error = "HexMapView cell layer " + cellLayer + " is outside 0..31.";
                return false;
            }

            if ((m_CullingMask.value & (1 << cellLayer)) == 0)
            {
                error = "Culling mask does not include the HexMapView cell layer " + cellLayer + " ("
                    + LayerMask.LayerToName(cellLayer) + "); the map would not render.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}

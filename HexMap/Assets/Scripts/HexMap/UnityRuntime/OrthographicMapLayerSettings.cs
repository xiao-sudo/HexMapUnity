using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Single owner for the render layers an orthographic map camera should see, kept independent of
    /// <see cref="HexMapView"/> so the camera can be pointed at a map without owning it.
    /// <para>
    /// This component never writes <see cref="HexMapView"/>'s own layer field; it only reports when the
    /// two disagree, because "configured but not effective" is this feature's main failure mode.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrthographicMapLayerSettings : MonoBehaviour
    {
        [SerializeField]
        private HexMapView m_HexMapView;

        [SerializeField]
        [Tooltip("Layers the map camera renders. Should include the HexMapView's cell layer.")]
        private LayerMask m_CullingMask = ~0;

        public HexMapView HexMapView
        {
            get { return m_HexMapView; }
            set { m_HexMapView = value; }
        }

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
        /// Reports whether the mask actually covers the map and whether the layer indices are usable.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (m_HexMapView == null)
            {
                error = "A HexMapView reference is required.";
                return false;
            }

            var cellLayer = m_HexMapView.CellLayer;
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

using UnityEngine;

namespace HexMap.UnityRuntime
{
    public sealed class HexMapRenderConfig
    {
        public HexMapRenderConfig(Transform parent, Material sharedMaterial, int layer, Color baseAppearanceColor)
        {
            if (layer < 0 || layer > 31)
            {
                throw new System.ArgumentOutOfRangeException(nameof(layer), layer, "Layer must be between 0 and 31.");
            }

            Parent = parent;
            SharedMaterial = sharedMaterial;
            Layer = layer;
            BaseAppearanceColor = baseAppearanceColor;
        }

        public Transform Parent { get; }
        public Material SharedMaterial { get; }
        public int Layer { get; }
        public Color BaseAppearanceColor { get; }
    }
}
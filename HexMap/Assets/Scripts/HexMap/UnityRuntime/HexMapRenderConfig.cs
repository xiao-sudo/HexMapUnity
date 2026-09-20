using UnityEngine;

namespace HexMap.UnityRuntime
{
    public sealed class HexMapRenderConfig
    {
        public HexMapRenderConfig(Transform parent, Material sharedMaterial, int layer, Color baseAppearanceColor)
            : this(parent, sharedMaterial, layer, baseAppearanceColor, HexMapRenderStrategy.MeshRenderer)
        {
        }

        public HexMapRenderConfig(
            Transform parent,
            Material sharedMaterial,
            int layer,
            Color baseAppearanceColor,
            HexMapRenderStrategy strategy)
        {
            if (layer < 0 || layer > 31)
            {
                throw new System.ArgumentOutOfRangeException(nameof(layer), layer, "Layer must be between 0 and 31.");
            }

            if (!System.Enum.IsDefined(typeof(HexMapRenderStrategy), strategy))
            {
                throw new System.ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown HexMap render strategy.");
            }

            Parent = parent;
            SharedMaterial = sharedMaterial;
            Layer = layer;
            BaseAppearanceColor = baseAppearanceColor;
            Strategy = strategy;
        }

        public Transform Parent { get; }
        public Material SharedMaterial { get; }
        public int Layer { get; }
        public Color BaseAppearanceColor { get; }
        public HexMapRenderStrategy Strategy { get; }
    }
}
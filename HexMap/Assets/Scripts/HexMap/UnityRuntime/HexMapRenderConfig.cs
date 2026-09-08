using UnityEngine;

namespace HexMap.UnityRuntime
{
    public sealed class HexMapRenderConfig
    {
        public HexMapRenderConfig(Transform parent, Material sharedMaterial, int layer)
        {
            if (layer < 0 || layer > 31)
            {
                throw new System.ArgumentOutOfRangeException(nameof(layer), layer, "Layer must be between 0 and 31.");
            }

            Parent = parent;
            SharedMaterial = sharedMaterial;
            Layer = layer;
        }

        public Transform Parent { get; }
        public Material SharedMaterial { get; }
        public int Layer { get; }
    }
}
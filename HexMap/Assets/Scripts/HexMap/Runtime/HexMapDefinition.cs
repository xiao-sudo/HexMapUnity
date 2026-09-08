using HexMap.Core;

namespace HexMap.Runtime
{
    public sealed class HexMapDefinition
    {
        public HexMapDefinition(int radius)
        {
            Radius = new HexMapRadius(radius);
        }

        public HexMapRadius Radius { get; }
    }
}

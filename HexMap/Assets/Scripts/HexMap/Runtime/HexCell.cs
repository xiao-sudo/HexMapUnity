using HexMap.Core;

namespace HexMap.Runtime
{
    public readonly struct HexCell
    {
        public HexCell(HexCoord coordinate)
        {
            Coordinate = coordinate;
        }

        public HexCoord Coordinate { get; }
    }
}
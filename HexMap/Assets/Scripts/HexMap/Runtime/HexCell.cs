using HexMap.Core;

namespace HexMap.Runtime
{
    public readonly struct HexCell
    {
        public HexCell(int id, HexCoord coordinate)
        {
            if (id < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(id), id, "Cell ID must be non-negative.");
            }

            Id = id;
            Coordinate = coordinate;
        }

        public int Id { get; }
        public HexCoord Coordinate { get; }
    }
}

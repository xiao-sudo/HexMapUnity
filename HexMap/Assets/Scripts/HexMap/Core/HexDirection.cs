using System;

namespace HexMap.Core
{
    public enum HexDirection
    {
        East = 0,
        SouthEast = 1,
        SouthWest = 2,
        West = 3,
        NorthWest = 4,
        NorthEast = 5
    }

    public static class HexDirectionExtensions
    {
        public static HexDirection Opposite(this HexDirection direction)
        {
            switch (direction)
            {
                case HexDirection.East:
                    return HexDirection.West;
                case HexDirection.SouthEast:
                    return HexDirection.NorthWest;
                case HexDirection.SouthWest:
                    return HexDirection.NorthEast;
                case HexDirection.West:
                    return HexDirection.East;
                case HexDirection.NorthWest:
                    return HexDirection.SouthEast;
                case HexDirection.NorthEast:
                    return HexDirection.SouthWest;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        internal static HexCoord Delta(this HexDirection direction)
        {
            switch (direction)
            {
                case HexDirection.East:
                    return new HexCoord(1, 0);
                case HexDirection.SouthEast:
                    return new HexCoord(1, -1);
                case HexDirection.SouthWest:
                    return new HexCoord(0, -1);
                case HexDirection.West:
                    return new HexCoord(-1, 0);
                case HexDirection.NorthWest:
                    return new HexCoord(-1, 1);
                case HexDirection.NorthEast:
                    return new HexCoord(0, 1);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}

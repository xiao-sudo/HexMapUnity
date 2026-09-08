using System;
using HexMap.Core;

namespace HexMap.Runtime
{
    public readonly struct HexMapRadius
    {
        public const int MaxSupportedRadius = 26754;

        public HexMapRadius(int radius)
        {
            if (radius < 0 || radius > MaxSupportedRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    radius,
                    string.Format("Radius must be between 0 and {0}.", MaxSupportedRadius));
            }

            Radius = radius;
        }

        public int Radius { get; }

        public int CellCount
        {
            get { return checked(1 + 3 * Radius * (Radius + 1)); }
        }

        public bool Contains(HexCoord coordinate)
        {
            return IsWithinRadius(coordinate.Q) &&
                   IsWithinRadius(coordinate.R) &&
                   IsWithinRadius(coordinate.S);
        }

        private bool IsWithinRadius(int value)
        {
            return Math.Abs((long)value) <= Radius;
        }
    }
}
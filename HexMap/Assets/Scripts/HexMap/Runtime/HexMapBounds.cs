using System;
using HexMap.Core;

namespace HexMap.Runtime
{
    public readonly struct HexMapBounds
    {
        public HexMapBounds(int minQ, int maxQ, int minR, int maxR)
        {
            if (minQ > maxQ)
            {
                throw new ArgumentException("Minimum q must not exceed maximum q.", nameof(minQ));
            }

            if (minR > maxR)
            {
                throw new ArgumentException("Minimum r must not exceed maximum r.", nameof(minR));
            }

            MinQ = minQ;
            MaxQ = maxQ;
            MinR = minR;
            MaxR = maxR;
        }

        public int MinQ { get; }
        public int MaxQ { get; }
        public int MinR { get; }
        public int MaxR { get; }

        public bool Contains(HexCoord coordinate)
        {
            return coordinate.Q >= MinQ && coordinate.Q <= MaxQ &&
                   coordinate.R >= MinR && coordinate.R <= MaxR;
        }
    }
}
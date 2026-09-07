using System;

namespace HexMap.Core
{
    public readonly struct HexCubeCoord : IEquatable<HexCubeCoord>
    {
        public HexCubeCoord(int q, int r, int s)
        {
            if ((long)q + r + s != 0)
            {
                throw new ArgumentException("Cube coordinates must satisfy q + r + s = 0.");
            }

            Q = q;
            R = r;
            S = s;
        }

        public int Q { get; }
        public int R { get; }
        public int S { get; }

        public HexCoord ToAxial()
        {
            return new HexCoord(Q, R);
        }

        public bool Equals(HexCubeCoord other)
        {
            return Q == other.Q && R == other.R && S == other.S;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCubeCoord && Equals((HexCubeCoord)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Q;
                hash = (hash * 397) ^ R;
                return (hash * 397) ^ S;
            }
        }

        public static bool operator ==(HexCubeCoord left, HexCubeCoord right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexCubeCoord left, HexCubeCoord right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return string.Format("Cube({0}, {1}, {2})", Q, R, S);
        }
    }
}
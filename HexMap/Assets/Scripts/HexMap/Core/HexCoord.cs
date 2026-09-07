using System;

namespace HexMap.Core
{
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public HexCoord(int q, int r)
        {
            var s = -(long)q - r;
            if (s < int.MinValue || s > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(r), r, "The derived cube coordinate s must fit in Int32.");
            }

            Q = q;
            R = r;
        }

        public int Q { get; }
        public int R { get; }
        public int S
        {
            get
            {
                return checked((int)(-(long)Q - R));
            }
        }

        public HexCubeCoord Cube
        {
            get
            {
                return new HexCubeCoord(Q, R, S);
            }
        }

        public HexCoord GetNeighbor(HexDirection direction)
        {
            var delta = direction.Delta();
            var nextQ = (long)Q + delta.Q;
            var nextR = (long)R + delta.R;

            if (nextQ < int.MinValue || nextQ > int.MaxValue ||
                nextR < int.MinValue || nextR > int.MaxValue)
            {
                throw new OverflowException("The neighboring coordinate does not fit in Int32.");
            }

            return new HexCoord((int)nextQ, (int)nextR);
        }

        public static int Distance(HexCoord first, HexCoord second)
        {
            var dq = (long)first.Q - second.Q;
            var dr = (long)first.R - second.R;
            var ds = (long)first.S - second.S;
            var distance = (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;

            return checked((int)distance);
        }

        public bool Equals(HexCoord other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoord && Equals((HexCoord)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Q * 397) ^ R;
            }
        }

        public static bool operator ==(HexCoord left, HexCoord right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexCoord left, HexCoord right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return string.Format("Hex({0}, {1})", Q, R);
        }
    }
}
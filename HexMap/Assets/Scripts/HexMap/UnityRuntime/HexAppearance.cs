using System;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public readonly struct HexAppearance : IEquatable<HexAppearance>
    {
        public HexAppearance(bool visible, Color color)
        {
            Visible = visible;
            Color = color;
        }

        public bool Visible { get; }
        public Color Color { get; }

        public bool Equals(HexAppearance other)
        {
            return Visible == other.Visible && Color.Equals(other.Color);
        }

        public override bool Equals(object obj)
        {
            return obj is HexAppearance && Equals((HexAppearance)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Visible ? 1 : 0) * 397 ^ Color.GetHashCode();
            }
        }

        public static bool operator ==(HexAppearance left, HexAppearance right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexAppearance left, HexAppearance right)
        {
            return !left.Equals(right);
        }
    }
}
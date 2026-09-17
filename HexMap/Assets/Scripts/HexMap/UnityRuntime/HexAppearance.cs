using System;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public readonly struct HexAppearance : IEquatable<HexAppearance>
    {
        public HexAppearance(bool visible, Color color)
            : this(visible, color, true)
        {
        }

        public HexAppearance(
            bool visible,
            Color color,
            bool gradientEnabled)
        {
            Visible = visible;
            Color = color;
            GradientEnabled = gradientEnabled;
        }

        public bool Visible { get; }
        public Color Color { get; }
        public bool GradientEnabled { get; }

        public bool Equals(HexAppearance other)
        {
            return Visible == other.Visible
                && Color.Equals(other.Color)
                && GradientEnabled == other.GradientEnabled;
        }

        public override bool Equals(object obj)
        {
            return obj is HexAppearance && Equals((HexAppearance)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (Visible ? 1 : 0) * 397 ^ Color.GetHashCode();
                return hash * 397 ^ (GradientEnabled ? 1 : 0);
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
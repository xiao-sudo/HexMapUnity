using System;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public readonly struct HexSelectionAppearance : IEquatable<HexSelectionAppearance>
    {
        public HexSelectionAppearance(Color color, bool gradientEnabled)
        {
            Color = color;
            GradientEnabled = gradientEnabled;
        }

        public Color Color { get; }
        public bool GradientEnabled { get; }

        public bool Equals(HexSelectionAppearance other)
        {
            return Color.Equals(other.Color)
                && GradientEnabled == other.GradientEnabled;
        }

        public override bool Equals(object obj)
        {
            return obj is HexSelectionAppearance && Equals((HexSelectionAppearance)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Color.GetHashCode() * 397 ^ (GradientEnabled ? 1 : 0);
            }
        }

        public static bool operator ==(HexSelectionAppearance left, HexSelectionAppearance right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexSelectionAppearance left, HexSelectionAppearance right)
        {
            return !left.Equals(right);
        }
    }
}
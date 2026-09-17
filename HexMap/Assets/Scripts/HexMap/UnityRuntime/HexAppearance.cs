using System;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public readonly struct HexAppearance : IEquatable<HexAppearance>
    {
        public HexAppearance(bool visible, Color color)
            : this(visible, color, 0.05f, false, 1f)
        {
        }

        public HexAppearance(
            bool visible,
            Color color,
            float borderWidth,
            bool gradientEnabled,
            float gradientPower)
        {
            Visible = visible;
            Color = color;
            BorderWidth = borderWidth;
            GradientEnabled = gradientEnabled;
            GradientPower = gradientPower;
        }

        public bool Visible { get; }
        public Color Color { get; }
        public float BorderWidth { get; }
        public bool GradientEnabled { get; }
        public float GradientPower { get; }

        public bool Equals(HexAppearance other)
        {
            return Visible == other.Visible
                && Color.Equals(other.Color)
                && BorderWidth.Equals(other.BorderWidth)
                && GradientEnabled == other.GradientEnabled
                && GradientPower.Equals(other.GradientPower);
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
                hash = hash * 397 ^ BorderWidth.GetHashCode();
                hash = hash * 397 ^ (GradientEnabled ? 1 : 0);
                return hash * 397 ^ GradientPower.GetHashCode();
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
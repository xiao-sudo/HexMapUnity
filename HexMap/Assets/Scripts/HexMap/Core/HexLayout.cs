using System;
using UnityEngine;

namespace HexMap.Core
{
    public enum HexOrientation
    {
        Pointy = 0,
        Flat = 1
    }

    public enum HexPlane
    {
        XY = 0,
        XZ = 1
    }

    /// <summary>
    /// The coordinate maths for a hex map: how a <see cref="HexCoord"/> maps to map local space and back.
    /// <para>
    /// This is an immutable snapshot. <see cref="Equals(HexLayout)"/> identifies <em>the same snapshot</em>
    /// rather than the same values: every construction takes the next value of a static counter, so a copy
    /// of a layout equals its original, while a separately constructed layout with identical numbers does
    /// not. Callers that need to detect "the layout changed" should therefore compare snapshots instead of
    /// fields, which also avoids depending on float bit patterns.
    /// </para>
    /// </summary>
    public readonly struct HexLayout : IEquatable<HexLayout>
    {
        private static int s_NextVersion = 1;

        /// <summary>
        /// The snapshot identity, assigned on construction. Zero means the layout was never constructed,
        /// which is what <c>default(HexLayout)</c> carries.
        /// </summary>
        private readonly int m_Version;

        public HexLayout(
            HexOrientation orientation,
            HexPlane plane,
            float outerRadius,
            Vector3 origin,
            float secondaryScale = 1)
        {
            if (!Enum.IsDefined(typeof(HexOrientation), orientation))
            {
                throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null);
            }

            if (!Enum.IsDefined(typeof(HexPlane), plane))
            {
                throw new ArgumentOutOfRangeException(nameof(plane), plane, null);
            }

            if (!IsFinitePositive(outerRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(outerRadius), outerRadius, "Outer radius must be positive and finite.");
            }

            if (!IsFinitePositive(secondaryScale))
            {
                throw new ArgumentOutOfRangeException(nameof(secondaryScale), secondaryScale, "Secondary scale must be positive and finite.");
            }

            if (!IsFinite(origin))
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin, "Origin must be finite.");
            }

            Orientation = orientation;
            Plane = plane;
            OuterRadius = outerRadius;
            SecondaryScale = secondaryScale;
            Origin = origin;

            // A plain increment: layouts are only built on the main thread, and the counter starts at one
            // so that no constructed layout can be mistaken for default(HexLayout).
            m_Version = s_NextVersion++;
        }

        public HexOrientation Orientation { get; }
        public HexPlane Plane { get; }
        public float OuterRadius { get; }
        public float SecondaryScale { get; }
        public Vector3 Origin { get; }

        /// <summary>
        /// True when both sides are the same snapshot, which is what a caller wants when it asks whether a
        /// layout it retained is still the one the map is using.
        /// </summary>
        public bool Equals(HexLayout other)
        {
            return m_Version == other.m_Version;
        }

        public override bool Equals(object obj)
        {
            return obj is HexLayout other && Equals(other);
        }

        public override int GetHashCode()
        {
            return m_Version;
        }

        public static bool operator ==(HexLayout left, HexLayout right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexLayout left, HexLayout right)
        {
            return !left.Equals(right);
        }

        public Vector3 HexToWorld(HexCoord coordinate)
        {
            ValidateState();

            var x = 0f;
            var planeCoordinate = 0f;

            if (Orientation == HexOrientation.Pointy)
            {
                x = OuterRadius * Mathf.Sqrt(3f) * (coordinate.Q + coordinate.R * 0.5f);
                planeCoordinate = OuterRadius * 1.5f * coordinate.R;
            }
            else
            {
                x = OuterRadius * 1.5f * coordinate.Q;
                planeCoordinate = OuterRadius * Mathf.Sqrt(3f) * (coordinate.R + coordinate.Q * 0.5f);
            }

            planeCoordinate *= SecondaryScale;

            if (Plane == HexPlane.XY)
            {
                return new Vector3(Origin.x + x, Origin.y + planeCoordinate, Origin.z);
            }

            return new Vector3(Origin.x + x, Origin.y, Origin.z + planeCoordinate);
        }

        public HexCoord WorldToHex(Vector3 worldPoint)
        {
            ValidateState();

            if (!IsFinite(worldPoint))
            {
                throw new ArgumentOutOfRangeException(nameof(worldPoint), worldPoint, "World point must be finite.");
            }

            var delta = worldPoint - Origin;
            var x = delta.x;
            var planeCoordinate = Plane == HexPlane.XY ? delta.y : delta.z;
            planeCoordinate /= SecondaryScale;

            float q;
            float r;

            if (Orientation == HexOrientation.Pointy)
            {
                q = (Mathf.Sqrt(3f) / 3f * x - 1f / 3f * planeCoordinate) / OuterRadius;
                r = 2f / 3f * planeCoordinate / OuterRadius;
            }
            else
            {
                q = 2f / 3f * x / OuterRadius;
                r = (-1f / 3f * x + Mathf.Sqrt(3f) / 3f * planeCoordinate) / OuterRadius;
            }

            return RoundCube(q, r, -q - r);
        }

        private static HexCoord RoundCube(float q, float r, float s)
        {
            var roundedQ = RoundHalfAwayFromZero(q);
            var roundedR = RoundHalfAwayFromZero(r);
            var roundedS = RoundHalfAwayFromZero(s);

            var qDifference = Mathf.Abs(roundedQ - q);
            var rDifference = Mathf.Abs(roundedR - r);
            var sDifference = Mathf.Abs(roundedS - s);

            if (qDifference >= rDifference && qDifference >= sDifference)
            {
                roundedQ = -roundedR - roundedS;
            }
            else if (rDifference >= sDifference)
            {
                roundedR = -roundedQ - roundedS;
            }
            else
            {
                roundedS = -roundedQ - roundedR;
            }

            return new HexCoord(roundedQ, roundedR);
        }

        private static int RoundHalfAwayFromZero(float value)
        {
            return value >= 0f
                ? Mathf.FloorToInt(value + 0.5f)
                : Mathf.CeilToInt(value - 0.5f);
        }

        private void ValidateState()
        {
            if (!IsFinitePositive(OuterRadius))
            {
                throw new InvalidOperationException("HexLayout was not initialized with a valid outer radius.");
            }

            if (!IsFinitePositive(SecondaryScale))
            {
                throw new InvalidOperationException("HexLayout was not initialized with a valid secondary scale.");
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
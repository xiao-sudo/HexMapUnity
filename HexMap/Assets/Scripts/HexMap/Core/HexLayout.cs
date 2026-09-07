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

    public readonly struct HexLayout
    {
        public HexLayout(
            HexOrientation orientation,
            HexPlane plane,
            float outerRadius,
            Vector3 origin)
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

            if (!IsFinite(origin))
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin, "Origin must be finite.");
            }

            Orientation = orientation;
            Plane = plane;
            OuterRadius = outerRadius;
            Origin = origin;
        }

        public HexOrientation Orientation { get; }
        public HexPlane Plane { get; }
        public float OuterRadius { get; }
        public Vector3 Origin { get; }

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
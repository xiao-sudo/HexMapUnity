using System;
using System.Collections.Generic;
using HexMap.Core;

namespace HexMap.Runtime
{
    public sealed class HexMapDefinition
    {
        private readonly IReadOnlyList<HexCoord> m_ExcludedCoordinates;
        private readonly HashSet<HexCoord> m_ExcludedLookup;

        public HexMapDefinition(int radius, IEnumerable<HexCoord> excludedCoordinates)
        {
            if (excludedCoordinates == null)
            {
                throw new ArgumentNullException(nameof(excludedCoordinates));
            }

            Radius = new HexMapRadius(radius);
            var coordinates = new List<HexCoord>();
            m_ExcludedLookup = new HashSet<HexCoord>();

            foreach (var coordinate in excludedCoordinates)
            {
                if (!Radius.Contains(coordinate))
                {
                    throw new ArgumentException(
                        "An excluded coordinate must be inside the map radius.",
                        nameof(excludedCoordinates));
                }

                if (!m_ExcludedLookup.Add(coordinate))
                {
                    throw new ArgumentException(
                        "The map definition contains a duplicate excluded coordinate.",
                        nameof(excludedCoordinates));
                }

                coordinates.Add(coordinate);
            }

            this.m_ExcludedCoordinates = coordinates.AsReadOnly();
        }

        public HexMapRadius Radius { get; }
        public IReadOnlyList<HexCoord> ExcludedCoordinates
        {
            get { return m_ExcludedCoordinates; }
        }

        internal bool IsExcluded(HexCoord coordinate)
        {
            return m_ExcludedLookup.Contains(coordinate);
        }
    }
}
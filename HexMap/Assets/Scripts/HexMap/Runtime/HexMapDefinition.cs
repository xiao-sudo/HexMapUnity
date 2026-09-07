using System;
using System.Collections.Generic;
using HexMap.Core;

namespace HexMap.Runtime
{
    public sealed class HexMapDefinition
    {
        private readonly IReadOnlyList<HexCoord> excludedCoordinates;
        private readonly HashSet<HexCoord> excludedLookup;

        public HexMapDefinition(HexMapBounds bounds, IEnumerable<HexCoord> excludedCoordinates)
        {
            if (excludedCoordinates == null)
            {
                throw new ArgumentNullException(nameof(excludedCoordinates));
            }

            Bounds = bounds;
            var coordinates = new List<HexCoord>();
            excludedLookup = new HashSet<HexCoord>();

            foreach (var coordinate in excludedCoordinates)
            {
                if (!bounds.Contains(coordinate))
                {
                    throw new ArgumentException(
                        "An excluded coordinate must be inside the map bounds.",
                        nameof(excludedCoordinates));
                }

                if (!excludedLookup.Add(coordinate))
                {
                    throw new ArgumentException(
                        "The map definition contains a duplicate excluded coordinate.",
                        nameof(excludedCoordinates));
                }

                coordinates.Add(coordinate);
            }

            this.excludedCoordinates = coordinates.AsReadOnly();
        }

        public HexMapBounds Bounds { get; }
        public IReadOnlyList<HexCoord> ExcludedCoordinates
        {
            get { return excludedCoordinates; }
        }

        internal bool IsExcluded(HexCoord coordinate)
        {
            return excludedLookup.Contains(coordinate);
        }
    }
}
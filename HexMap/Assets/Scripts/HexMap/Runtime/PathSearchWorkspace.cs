using System;
using System.Collections.Generic;
using HexMap.Core;

namespace HexMap.Runtime
{
    public sealed class PathSearchWorkspace
    {
        private readonly HexMap m_Map;
        private readonly HashSet<HexCoord> m_TargetCoordinates;
        private readonly Dictionary<HexCoord, HexCoord> m_Parents;
        private readonly Dictionary<HexCoord, int> m_Distances;
        private readonly Queue<HexCoord> m_Pending;
        private readonly HashSet<HexCoord> m_EvaluatedTargets;

        public PathSearchWorkspace(HexMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            m_Map = map;
            var capacity = map.Count;
            m_TargetCoordinates = new HashSet<HexCoord>(capacity);
            m_Parents = new Dictionary<HexCoord, HexCoord>(capacity);
            m_Distances = new Dictionary<HexCoord, int>(capacity);
            m_Pending = new Queue<HexCoord>(capacity);
            m_EvaluatedTargets = new HashSet<HexCoord>(capacity);
        }

        internal bool IsFor(HexMap map)
        {
            return ReferenceEquals(m_Map, map);
        }

        internal HashSet<HexCoord> TargetCoordinates
        {
            get { return m_TargetCoordinates; }
        }

        internal Dictionary<HexCoord, HexCoord> Parents
        {
            get { return m_Parents; }
        }

        internal Dictionary<HexCoord, int> Distances
        {
            get { return m_Distances; }
        }

        internal Queue<HexCoord> Pending
        {
            get { return m_Pending; }
        }

        internal HashSet<HexCoord> EvaluatedTargets
        {
            get { return m_EvaluatedTargets; }
        }
    }
}

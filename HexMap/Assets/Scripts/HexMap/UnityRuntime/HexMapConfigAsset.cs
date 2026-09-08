using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace HexMap.UnityRuntime
{
    [CreateAssetMenu(menuName = "Hex Map/Map Config")]
    public sealed class HexMapConfigAsset : ScriptableObject
    {
        [FormerlySerializedAs("radius")]
        [SerializeField] private int m_Radius = 3;
        [FormerlySerializedAs("excludedCoordinates")]
        [SerializeField] private List<HexCoordEntry> m_ExcludedCoordinates = new List<HexCoordEntry>();

        public int Radius
        {
            get { return m_Radius; }
            set { m_Radius = value; }
        }

        public HexMapDefinition CreateDefinition()
        {
            var excluded = new List<HexCoord>();
            foreach (var entry in m_ExcludedCoordinates)
            {
                excluded.Add(new HexCoord(entry.Q, entry.R));
            }

            return new HexMapDefinition(m_Radius, excluded);
        }

        public void SetExcludedCoordinates(IEnumerable<HexCoord> coordinates)
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(nameof(coordinates));
            }

            m_ExcludedCoordinates.Clear();
            foreach (var coordinate in coordinates)
            {
                m_ExcludedCoordinates.Add(new HexCoordEntry(coordinate.Q, coordinate.R));
            }
        }

        [Serializable]
        private struct HexCoordEntry
        {
            [FormerlySerializedAs("q")]
            [SerializeField] private int m_Q;
            [FormerlySerializedAs("r")]
            [SerializeField] private int m_R;

            public HexCoordEntry(int q, int r)
            {
                m_Q = q;
                m_R = r;
            }

            public int Q
            {
                get { return m_Q; }
            }

            public int R
            {
                get { return m_R; }
            }
        }
    }
}
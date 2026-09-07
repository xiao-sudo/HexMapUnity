using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    [CreateAssetMenu(menuName = "Hex Map/Map Config")]
    public sealed class HexMapConfigAsset : ScriptableObject
    {
        [SerializeField] private int minQ;
        [SerializeField] private int maxQ;
        [SerializeField] private int minR;
        [SerializeField] private int maxR;
        [SerializeField] private List<HexCoordEntry> excludedCoordinates = new List<HexCoordEntry>();

        public HexMapDefinition CreateDefinition()
        {
            var excluded = new List<HexCoord>();
            foreach (var entry in excludedCoordinates)
            {
                excluded.Add(new HexCoord(entry.Q, entry.R));
            }

            return new HexMapDefinition(
                new HexMapBounds(minQ, maxQ, minR, maxR),
                excluded);
        }

        [Serializable]
        private struct HexCoordEntry
        {
            [SerializeField] private int q;
            [SerializeField] private int r;

            public int Q
            {
                get { return q; }
            }

            public int R
            {
                get { return r; }
            }
        }
    }
}
using System;
using HexMap.Core;
using HexMap.Runtime;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Queries the active map from a world-space position.
    /// Screen coordinates, cameras and input policy belong to an adapter layer.
    /// </summary>
    public sealed class HexMapPicker : MonoBehaviour
    {
        private const float PlaneTolerance = 0.0001f;

        [SerializeField] private HexMapView m_MapView;

        public HexMapView MapView
        {
            get { return m_MapView; }
            set { m_MapView = value; }
        }

        public HexPickResult PickWorldPosition(Vector3 worldPosition)
        {
            ValidateWorldPosition(worldPosition);

            if (m_MapView == null || m_MapView.Map == null)
            {
                return HexPickResult.Create(HexPickStatus.NoMap);
            }

            var localPosition = m_MapView.WorldToMapLocal(worldPosition);
            var planeAxis = m_MapView.Layout.Plane == HexPlane.XY
                ? localPosition.z - m_MapView.Layout.Origin.z
                : localPosition.y - m_MapView.Layout.Origin.y;

            if (Mathf.Abs(planeAxis) > PlaneTolerance)
            {
                return HexPickResult.Create(HexPickStatus.NotOnMapPlane);
            }

            var coordinate = m_MapView.Layout.WorldToHex(localPosition);
            var query = m_MapView.Map.Query(coordinate);
            if (query.Status == HexCellQueryStatus.OutsideMap)
            {
                return HexPickResult.Create(HexPickStatus.OutsideMap);
            }

            HexView view;
            if (query.Status == HexCellQueryStatus.Found &&
                m_MapView.TryGetHexView(coordinate, out view))
            {
                return HexPickResult.Found(view);
            }

            return HexPickResult.Create(HexPickStatus.Missing);
        }

        public bool TryPickWorldPosition(Vector3 worldPosition, out HexView view)
        {
            var result = PickWorldPosition(worldPosition);
            view = result.View;
            return result.HasCell;
        }

        private static void ValidateWorldPosition(Vector3 worldPosition)
        {
            if (float.IsNaN(worldPosition.x) || float.IsInfinity(worldPosition.x) ||
                float.IsNaN(worldPosition.y) || float.IsInfinity(worldPosition.y) ||
                float.IsNaN(worldPosition.z) || float.IsInfinity(worldPosition.z))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldPosition),
                    worldPosition,
                    "World position must be finite.");
            }
        }
    }
}
using System;
using HexMap.Runtime;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public sealed class HexMapPicker : MonoBehaviour
    {
        [SerializeField] private HexMapView mapView;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask mapLayer = -1;
        [SerializeField] private float maxDistance = 1000f;

        public HexMapView MapView
        {
            get { return mapView; }
            set { mapView = value; }
        }

        public Camera TargetCamera
        {
            get { return targetCamera; }
            set { targetCamera = value; }
        }

        public LayerMask MapLayer
        {
            get { return mapLayer; }
            set { mapLayer = value; }
        }

        public float MaxDistance
        {
            get { return maxDistance; }
            set { maxDistance = value; }
        }

        public HexPickResult LastResult { get; private set; }
        public event Action<HexPickResult> Picked;

        public void Awake()
        {
            if (mapView == null)
            {
                mapView = GetComponent<HexMapView>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        public void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            Pick(Input.mousePosition);
            if (LastResult.HasCell)
            {
                Debug.Log(string.Format("Picked {0}.", LastResult.Cell.Coordinate));
            }
            else
            {
                Debug.Log(string.Format("Hex pick result: {0}.", LastResult.Status));
            }

            var handler = Picked;
            if (handler != null)
            {
                handler(LastResult);
            }
        }

        public HexPickResult Pick(Vector2 screenPoint)
        {
            if (mapView == null || mapView.Map == null)
            {
                return Store(HexPickResult.Create(HexPickStatus.NoMap));
            }

            if (targetCamera == null)
            {
                return Store(HexPickResult.Create(HexPickStatus.NoCamera));
            }

            var ray = targetCamera.ScreenPointToRay(screenPoint);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, maxDistance, mapLayer))
            {
                return Store(HexPickResult.Create(HexPickStatus.NoHit));
            }

            if (!mapView.OwnsCollider(hit.collider))
            {
                return Store(HexPickResult.Create(HexPickStatus.NonMapHit));
            }

            var localPoint = mapView.transform.InverseTransformPoint(hit.point);
            var coordinate = mapView.Layout.WorldToHex(localPoint);
            var query = mapView.Map.Query(coordinate);

            switch (query.Status)
            {
                case HexCellQueryStatus.Found:
                    return Store(HexPickResult.Found(query.Cell));
                case HexCellQueryStatus.OutsideMap:
                    return Store(HexPickResult.Create(HexPickStatus.OutsideMap));
                case HexCellQueryStatus.Missing:
                    return Store(HexPickResult.Create(HexPickStatus.Missing));
                default:
                    return Store(HexPickResult.Create(HexPickStatus.NoHit));
            }
        }

        public bool TryPickCell(Vector2 screenPoint, out HexCell cell)
        {
            var result = Pick(screenPoint);
            cell = result.Cell;
            return result.HasCell;
        }

        private HexPickResult Store(HexPickResult result)
        {
            LastResult = result;
            return result;
        }
    }
}
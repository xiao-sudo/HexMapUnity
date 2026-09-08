using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    public enum HexMapScreenPointStatus
    {
        Found = 0,
        NoCamera = 1,
        NoPlaneIntersection = 2,
        MapQueryFailed = 3
    }

    public readonly struct HexMapScreenPointResult
    {
        private HexMapScreenPointResult(
            HexMapScreenPointStatus status,
            HexPickResult? mapResult,
            Vector3 worldPosition)
        {
            Status = status;
            MapResult = mapResult;
            WorldPosition = worldPosition;
        }

        public HexMapScreenPointStatus Status { get; }
        public HexPickResult? MapResult { get; }
        public Vector3 WorldPosition { get; }

        public bool HasCell
        {
            get { return Status == HexMapScreenPointStatus.Found && MapResult.HasValue && MapResult.Value.HasCell; }
        }

        internal static HexMapScreenPointResult Found(HexPickResult mapResult, Vector3 worldPosition)
        {
            return new HexMapScreenPointResult(
                HexMapScreenPointStatus.Found,
                mapResult,
                worldPosition);
        }

        internal static HexMapScreenPointResult AdapterFailure(HexMapScreenPointStatus status)
        {
            return new HexMapScreenPointResult(
                status,
                null,
                default(Vector3));
        }

        internal static HexMapScreenPointResult MapFailure(
            HexPickResult mapResult,
            Vector3 worldPosition)
        {
            return new HexMapScreenPointResult(
                HexMapScreenPointStatus.MapQueryFailed,
                mapResult,
                worldPosition);
        }
    }

    /// <summary>
    /// Sample-only screen-to-world adapter. It owns camera, input and presentation policy;
    /// the map query itself remains in HexMapPicker.
    /// </summary>
    public sealed class HexMapScreenPointAdapter : MonoBehaviour
    {
        [SerializeField] private HexMapView m_MapView;
        [SerializeField] private HexMapPicker m_Picker;
        [SerializeField] private Camera m_TargetCamera;
        [SerializeField] private float m_MaxDistance = 1000f;
        [SerializeField] private bool m_HandleLeftMouseClick = true;
        [SerializeField] private bool m_UseMainCamera = true;

        public HexMapView MapView
        {
            get { return m_MapView; }
            set { m_MapView = value; }
        }

        public HexMapPicker Picker
        {
            get { return m_Picker; }
            set { m_Picker = value; }
        }

        public Camera TargetCamera
        {
            get { return m_TargetCamera; }
            set { m_TargetCamera = value; }
        }

        public float MaxDistance
        {
            get { return m_MaxDistance; }
            set { m_MaxDistance = value; }
        }

        public bool HandleLeftMouseClick
        {
            get { return m_HandleLeftMouseClick; }
            set { m_HandleLeftMouseClick = value; }
        }

        public bool UseMainCamera
        {
            get { return m_UseMainCamera; }
            set { m_UseMainCamera = value; }
        }

        public HexMapScreenPointResult LastResult { get; private set; }
        public event System.Action<HexMapScreenPointResult> Picked;

        public void Awake()
        {
            ResolveReferences();
        }

        public void Update()
        {
            if (!m_HandleLeftMouseClick || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            Present(PickScreenPoint(Input.mousePosition));
        }

        public HexMapScreenPointResult PickScreenPoint(Vector2 screenPoint)
        {
            ResolveReferences();
            if (m_TargetCamera == null)
            {
                return HexMapScreenPointResult.AdapterFailure(HexMapScreenPointStatus.NoCamera);
            }

            if (m_MapView == null || !m_MapView.HasMap || m_Picker == null)
            {
                return HexMapScreenPointResult.MapFailure(
                    HexPickResult.Create(HexPickStatus.NoMap),
                    default(Vector3));
            }

            var ray = m_TargetCamera.ScreenPointToRay(screenPoint);
            var plane = m_MapView.WorldPlane;
            float distance;
            if (!plane.Raycast(ray, out distance) ||
                !IsFinite(distance) ||
                distance < 0f ||
                m_MaxDistance < 0f ||
                distance > m_MaxDistance)
            {
                return HexMapScreenPointResult.AdapterFailure(
                    HexMapScreenPointStatus.NoPlaneIntersection);
            }

            var worldPosition = ray.GetPoint(distance);
            var mapResult = m_Picker.PickWorldPosition(worldPosition);
            return mapResult.HasCell
                ? HexMapScreenPointResult.Found(mapResult, worldPosition)
                : HexMapScreenPointResult.MapFailure(mapResult, worldPosition);
        }

        public bool TryPickScreenPoint(
            Vector2 screenPoint,
            out HexMapScreenPointResult result)
        {
            result = PickScreenPoint(screenPoint);
            return result.HasCell;
        }

        public void Present(HexMapScreenPointResult result)
        {
            LastResult = result;
            if (result.HasCell)
            {
                Debug.Log("Picked a HexView.");
            }
            else
            {
                Debug.Log(string.Format("Hex screen pick result: {0}.", result.Status));
            }

            var handler = Picked;
            if (handler != null)
            {
                handler(result);
            }
        }

        private void ResolveReferences()
        {
            if (m_MapView == null)
            {
                m_MapView = GetComponent<HexMapView>();
            }

            if (m_Picker == null)
            {
                m_Picker = GetComponent<HexMapPicker>();
            }

            if (m_TargetCamera == null && m_UseMainCamera)
            {
                m_TargetCamera = Camera.main;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
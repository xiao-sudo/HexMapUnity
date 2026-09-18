using System;
using System.Collections.Generic;
using HexMap.Gvg;
using HexMap.Runtime;
using UnityEngine;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.UnityRuntime
{
    public enum PlotScreenPickStatus
    {
        Found = 0,
        MapNotInitialized = 1,
        NoCamera = 2,
        NoPlaneIntersection = 3,
        OutsideMap = 4,
        NoSelectablePlot = 5
    }

    public readonly struct PlotScreenPickResult
    {
        private PlotScreenPickResult(PlotScreenPickStatus status, int plotId)
        {
            Status = status;
            PlotId = plotId;
        }

        public PlotScreenPickStatus Status { get; }
        public int PlotId { get; }

        public bool HasPlot
        {
            get { return Status == PlotScreenPickStatus.Found; }
        }

        internal static PlotScreenPickResult Found(int plotId)
        {
            return new PlotScreenPickResult(PlotScreenPickStatus.Found, plotId);
        }

        internal static PlotScreenPickResult Failure(PlotScreenPickStatus status)
        {
            return new PlotScreenPickResult(status, -1);
        }
    }
    [Serializable]
    public struct IdToColor
    {
        public int Id;
        public Color Color;
    }

    /// <summary>
    /// Composes scene topology with external GVG Plot snapshots into the runtime map API.
    /// </summary>
    public sealed class GvgMapRuntimeController : MonoBehaviour
    {
        [SerializeField]
        private HexMapView m_HexMapView;

        [SerializeField]
        private List<IdToColor> m_FactionToColor;

        [SerializeField]
        private Color m_SelectedColor = Color.white;

        [SerializeField]
        [Range(0.1f, 10)]
        private float m_PlotWorldAnchorHeightOffset = 1;

        private RuntimeHexMap m_Map;
        private PlotRegistry m_PlotRegistry;
        private PlotPathService m_PlotPathService;
        private Dictionary<int, Vector3> m_PlotWorldCenters;
        private Dictionary<int, Color> m_FactionToColorDict;
        private PathResult m_PathResult;

        [NonSerialized]
        private int m_SelectedPlotId = -1;

        public HexMapView HexMapView
        {
            get { return m_HexMapView; }
            set
            {
                m_HexMapView = value;
                BuildMapFromView();
            }
        }

        public bool IsInitialized
        {
            get { return m_Map != null && m_PlotRegistry != null && m_PlotPathService != null; }
        }

        public int SelectedPlotId
        {
            get { return m_SelectedPlotId; }
        }

        /// <summary>
        /// Queries the selectable Plot at a Unity screen-space position without changing selection state.
        /// A null camera falls back to Camera.main.
        /// </summary>
        public PlotScreenPickResult PickPlotAtScreenPosition(
            Vector2 screenPosition,
            Camera cam = null)
        {
            if (!IsInitialized || m_HexMapView == null)
            {
                return PlotScreenPickResult.Failure(PlotScreenPickStatus.MapNotInitialized);
            }

            var targetCamera = cam != null ? cam : Camera.main;
            if (targetCamera == null)
            {
                return PlotScreenPickResult.Failure(PlotScreenPickStatus.NoCamera);
            }

            var ray = targetCamera.ScreenPointToRay(screenPosition);
            float distance;
            if (!m_HexMapView.WorldPlane.Raycast(ray, out distance) ||
                float.IsNaN(distance) ||
                float.IsInfinity(distance) ||
                distance < 0f)
            {
                return PlotScreenPickResult.Failure(PlotScreenPickStatus.NoPlaneIntersection);
            }

            var query = m_Map.Query(m_HexMapView.WorldToHex(ray.GetPoint(distance)));
            if (query.Status != HexCellQueryStatus.Found)
            {
                return PlotScreenPickResult.Failure(PlotScreenPickStatus.OutsideMap);
            }

            Plot plot;
            if (!m_PlotRegistry.TryGetPlotForCell(query.Cell.Id, out plot))
            {
                return PlotScreenPickResult.Failure(PlotScreenPickStatus.NoSelectablePlot);
            }

            return PlotScreenPickResult.Found(plot.PlotId);
        }
        /// <summary>
        /// Gets or sets the world-space distance between a Plot center and its render anchor.
        /// </summary>
        public float PlotWorldAnchorHeightOffset
        {
            get { return m_PlotWorldAnchorHeightOffset; }
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Height offset must be finite.");

                m_PlotWorldAnchorHeightOffset = value;
            }
        }

        private void Awake()
        {
            if (m_HexMapView == null)
            {
                m_HexMapView = GetComponent<HexMapView>();
            }

            BuildMapFromView();

            m_FactionToColorDict = new Dictionary<int, Color>(m_FactionToColor.Count);
            foreach (var idToColor in m_FactionToColor)
                m_FactionToColorDict.Add(idToColor.Id, idToColor.Color);

        }

        private void BuildMapFromView()
        {
            if (m_Map != null)
            {
                return;
            }

            if (m_HexMapView == null)
            {
                Debug.LogError("GvgMapRuntimeController requires a HexMapView reference.", this);
                return;
            }

            try
            {
                m_HexMapView.Build();
                m_Map = m_HexMapView.Map;
                if (m_Map == null)
                {
                    Debug.LogError("GvgMapRuntimeController could not build a HexMap from its HexMapView.", this);
                }
                else
                {
                    m_PathResult = new PathResult(new List<int>(m_HexMapView.Map.Count));
                }
            }
            catch (Exception exception)
            {
                m_Map = null;
                Debug.LogError("GvgMapRuntimeController failed to build its HexMap: " + exception.Message, this);
            }
        }

        public bool TryInitialize(IReadOnlyList<GvgPlotRuntimeData> plots)
        {
            if (m_Map == null)
            {
                return false;
            }

            if (plots == null || plots.Count == 0)
            {
                Debug.LogError("GvgMapRuntimeController requires a non-empty Plot snapshot.", this);
                return false;
            }

            PlotRegistry registry;
            string error;
            if (!GvgMapRuntimeComposer.TryCompose(m_Map, plots, out registry, out error))
            {
                Debug.LogError("GvgMapRuntimeController failed to initialize Plot snapshot: " + error, this);
                return false;
            }

            PlotPathService pathService;
            try
            {
                pathService = new PlotPathService(registry);
            }
            catch (Exception exception)
            {
                Debug.LogError("GvgMapRuntimeController failed to initialize Plot path service: " + exception.Message,
                    this);
                return false;
            }

            Dictionary<int, Vector3> plotWorldCenters;
            try
            {
                plotWorldCenters = BuildPlotWorldCenters(registry, plots);
            }
            catch (Exception exception)
            {
                Debug.LogError("GvgMapRuntimeController failed to calculate Plot world centers: " + exception.Message, this);
                return false;
            }

            DeselectAll();
            m_PlotRegistry = registry;
            m_PlotPathService = pathService;
            m_PlotWorldCenters = plotWorldCenters;
            return true;
        }

        public bool Select(int plotId)
        {
            Plot plot;
            if (!TryGetPlot(plotId, out plot) || m_SelectedPlotId == plotId)
            {
                return false;
            }

            DeselectAll();
            SetPlotSelection(plot, true);
            m_SelectedPlotId = plotId;
            return true;
        }

        public bool Deselect(int plotId)
        {
            if (m_SelectedPlotId != plotId)
            {
                return false;
            }

            Plot plot;
            if (TryGetPlot(plotId, out plot))
            {
                SetPlotSelection(plot, false);
            }

            m_SelectedPlotId = -1;
            return true;
        }

        public bool DeselectAll()
        {
            return m_SelectedPlotId != -1 && Deselect(m_SelectedPlotId);
        }
        public bool TryGetPlot(int plotId, out Plot plot)
        {
            if (m_PlotRegistry == null)
            {
                plot = null;
                return false;
            }

            return m_PlotRegistry.TryGetPlot(plotId, out plot);
        }

        public bool TryGetPlotWorldCenter(int plotId, out Vector3 worldCenter)
        {
            if (m_PlotWorldCenters == null || !m_PlotWorldCenters.TryGetValue(plotId, out worldCenter))
            {
                worldCenter = Vector3.zero;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets a render anchor offset from the cached Plot center along the map plane's world normal.
        /// </summary>
        public bool TryGetPlotWorldAnchor(int plotId, out Vector3 worldAnchor)
        {
            Vector3 worldCenter;
            if (!TryGetPlotWorldCenter(plotId, out worldCenter) || m_HexMapView == null)
            {
                worldAnchor = Vector3.zero;
                return false;
            }

            worldAnchor = worldCenter + m_HexMapView.WorldPlane.normal * m_PlotWorldAnchorHeightOffset;
            return true;
        }

        public bool TrySetPlotOwnerFactionId(int plotId, int ownerFactionId)
        {
            Plot plot;
            if (!TryGetPlot(plotId, out plot))
            {
                return false;
            }

            var r = m_PlotRegistry.TrySetOwnerFactionId(plotId, ownerFactionId);

            if (r)
            {
                if(null != m_FactionToColorDict && m_FactionToColorDict.TryGetValue(ownerFactionId, out var color))
                {
                    SetPlotColor(plot, color);
                }
            }

            return r;
        }

        private void SetPlotColor(Plot plot, Color color)
        {
            foreach (var cell in plot.Cells)
            {
                if (m_HexMapView.TryGetHexView(cell.Coordinate, out var view))
                    view.SetAppearance(new HexAppearance(true, color, true));
            }
        }

        private void SetPlotSelection(Plot plot, bool selected)
        {
            if (m_HexMapView == null)
            {
                return;
            }

            foreach (var cell in plot.Cells)
            {
                if (!m_HexMapView.TryGetHexView(cell.Coordinate, out var view))
                {
                    continue;
                }

                if (selected)
                {
                    view.Select(new HexSelectionAppearance(m_SelectedColor, true));
                }
                else
                {
                    view.Deselect();
                }
            }
        }
        public PathResult TryFindPlotPath(
            int startPlotId,
            int targetPlotId,
            int movingFactionId)
        {

            if (m_PlotPathService == null)
            {
                m_PathResult.SetFailure(PathResultStatus.InvalidInput, PathFailureReason.MapNotInitialized);
                return m_PathResult;
            }

            if (!m_PlotRegistry.TryGetPlot(startPlotId, out _) ||
                !m_PlotRegistry.TryGetPlot(targetPlotId, out _))
            {
                m_PathResult.SetFailure(PathResultStatus.InvalidInput, PathFailureReason.PlotNotFound);
                return m_PathResult;
            }

            return m_PlotPathService.FindPath(startPlotId, targetPlotId, movingFactionId, m_PathResult);
        }

        private Dictionary<int, Vector3> BuildPlotWorldCenters(
            PlotRegistry registry,
            IReadOnlyList<GvgPlotRuntimeData> plotData)
        {
            if (m_HexMapView == null)
            {
                throw new InvalidOperationException("A HexMapView is required to calculate Plot world centers.");
            }

            var centers = new Dictionary<int, Vector3>(plotData.Count);
            for (var index = 0; index < plotData.Count; index++)
            {
                var plotId = plotData[index].PlotId;
                Plot plot;
                if (!registry.TryGetPlot(plotId, out plot))
                {
                    throw new InvalidOperationException("The composed PlotRegistry is missing Plot " + plotId + ".");
                }

                centers.Add(plotId, m_HexMapView.GetPlotWorldCenter(plot.Cells));
            }

            return centers;
        }
    }
}

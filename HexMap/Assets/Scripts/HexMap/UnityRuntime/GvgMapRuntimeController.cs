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
        /// <summary>
        /// 使用指定的拾取状态和地块 ID 创建不可变的屏幕拾取结果。仅由本结构的成功与失败工厂方法调用。
        /// </summary>
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

        /// <summary>
        /// 创建表示屏幕坐标成功命中地块的拾取结果。
        /// </summary>
        internal static PlotScreenPickResult Found(int plotId)
        {
            return new PlotScreenPickResult(PlotScreenPickStatus.Found, plotId);
        }

        /// <summary>
        /// 创建表示屏幕坐标拾取失败的结果；失败结果的地块 ID 固定为 -1。
        /// </summary>
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
        /// 根据屏幕坐标查询可选中的地块，但不会改变当前的选中状态。
        /// </summary>
        /// <param name="screenPosition">Unity 屏幕坐标，原点由 Unity 的屏幕坐标系定义。</param>
        /// <param name="cam">用于发射射线的相机；传入 <see langword="null"/> 时自动使用 <see cref="Camera.main"/>。</param>
        /// <returns>成功时可取得地块 ID；失败时可通过状态判断地图、相机、射线与地块映射中的具体问题。</returns>
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
        /// 获取或设置地块世界中心到渲染锚点的偏移距离。
        /// </summary>
        /// <remarks>锚点沿地图平面的世界法线方向计算，通常用于将图标或标签抬离地图表面。设置值必须是有限数值。</remarks>
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

        /// <summary>
        /// 在 Unity 初始化组件时获取缺失的地图视图引用、构建运行时地图，并将 Inspector 配置的阵营颜色列表转换为便于查询的字典。
        /// </summary>
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

        /// <summary>
        /// 从当前地图视图构建运行时六边形地图，并预分配寻路结果容器。地图已构建时不会重复构建；视图缺失或构建失败时会记录错误日志。
        /// </summary>
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

        /// <summary>
        /// 使用非空 GVG 地块快照初始化地块注册表、寻路服务和世界中心缓存。全部成功后才替换当前运行时数据，并会清除已有选中状态。
        /// </summary>
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

        /// <summary>
        /// 选中指定地块，并取消此前选中地块的视觉状态。地块不存在或已经选中时返回 false。
        /// </summary>
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

        /// <summary>
        /// 仅当指定地块正处于选中状态时取消其选中状态；指定 ID 不是当前选中项时返回 false。
        /// </summary>
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

        /// <summary>
        /// 取消当前选中的地块；控制器没有选中地块时不执行任何操作并返回 false。
        /// </summary>
        public bool DeselectAll()
        {
            return m_SelectedPlotId != -1 && Deselect(m_SelectedPlotId);
        }
        /// <summary>
        /// 按 ID 获取已初始化的运行时地块对象。注册表不存在或不包含该 ID 时返回 false，输出为 null。
        /// </summary>
        private bool TryGetPlot(int plotId, out Plot plot)
        {
            if (m_PlotRegistry == null)
            {
                plot = null;
                return false;
            }

            return m_PlotRegistry.TryGetPlot(plotId, out plot);
        }

        /// <summary>
        /// 获取初始化时缓存的指定地块世界中心。中心缓存不存在或不含该地块时返回 false，输出为 Vector3.zero。
        /// </summary>
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
        /// 获取指定地块的渲染锚点世界坐标。
        /// </summary>
        /// <param name="plotId">要查询的地块 ID。</param>
        /// <param name="worldAnchor">查询成功时为从地块中心沿地图平面法线偏移后的世界坐标；失败时为 <see cref="Vector3.zero"/>。</param>
        /// <returns>地块中心缓存和地图视图都可用时返回 <see langword="true"/>。</returns>
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

        /// <summary>
        /// 更新地块所属阵营；若 Inspector 中配置了该阵营颜色，则同步刷新地块内所有可用格子视图的颜色。
        /// </summary>
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

        /// <summary>
        /// 将地块中所有已创建视图的格子设置为指定颜色，同时保持其填充显示。
        /// </summary>
        private void SetPlotColor(Plot plot, Color color)
        {
            foreach (var cell in plot.Cells)
            {
                if (m_HexMapView.TryGetHexView(cell.Coordinate, out var view))
                    view.SetAppearance(new HexAppearance(true, color, true));
            }
        }

        /// <summary>
        /// 为地块中所有可取得的格子视图应用或移除选中外观。地图视图不存在时直接返回，缺少单个格子视图不会影响其他格子。
        /// </summary>
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
        /// <summary>
        /// 在已初始化的地块拓扑中查找从起始地块到目标地块的移动路径。地图未初始化或任一地块不存在时，在可复用结果对象中写入相应失败状态。
        /// </summary>
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

        /// <summary>
        /// 根据已组合的地块注册表及原始快照，计算并建立地块 ID 到世界中心坐标的映射。地图视图不存在或注册表缺少快照地块时会抛出 InvalidOperationException。
        /// </summary>
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

using System;
using System.Collections.Generic;
using HexMap.Gvg;
using HexMap.Runtime;
using UnityEngine;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Composes scene topology with external GVG Plot snapshots into the runtime map API.
    /// </summary>
    public sealed class GvgMapRuntimeController : MonoBehaviour
    {
        [SerializeField] private HexMapView m_HexMapView;

        private RuntimeHexMap m_Map;
        private PlotRegistry m_PlotRegistry;
        private PlotPathService m_PlotPathService;

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

        private void Awake()
        {
            if (m_HexMapView == null)
            {
                m_HexMapView = GetComponent<HexMapView>();
            }

            BuildMapFromView();
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
                Debug.LogError("GvgMapRuntimeController failed to initialize Plot path service: " + exception.Message, this);
                return false;
            }

            m_PlotRegistry = registry;
            m_PlotPathService = pathService;
            return true;
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

        public bool TrySetPlotOwnerFactionId(int plotId, int ownerFactionId)
        {
            Plot plot;
            if (!TryGetPlot(plotId, out plot))
            {
                return false;
            }

            return m_PlotRegistry.TrySetOwnerFactionId(plotId, ownerFactionId);
        }

        public bool TryFindPlotPath(
            int startPlotId,
            int targetPlotId,
            int movingFactionId,
            PathResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            if (m_PlotPathService == null)
            {
                result.SetFailure(PathResultStatus.InvalidInput, PathFailureReason.MapNotInitialized);
                return false;
            }

            Plot startPlot;
            Plot targetPlot;
            if (!m_PlotRegistry.TryGetPlot(startPlotId, out startPlot) ||
                !m_PlotRegistry.TryGetPlot(targetPlotId, out targetPlot))
            {
                result.SetFailure(PathResultStatus.InvalidInput, PathFailureReason.PlotNotFound);
                return false;
            }

            return m_PlotPathService.FindPath(startPlotId, targetPlotId, movingFactionId, result).IsSuccess;
        }
    }
}
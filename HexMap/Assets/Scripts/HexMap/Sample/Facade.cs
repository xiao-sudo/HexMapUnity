using System;
using System.Collections.Generic;
using HexMap.Gvg;
using HexMap.UnityRuntime;
#if UNITY_EDITOR
using HexMap.Gvg.Authoring;
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace HexMap.Sample
{
    public class Facade : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField]
        private GvgMapAuthoringAsset m_Asset;
#endif

        [SerializeField]
        private SampleMapViewRuntimeHost m_Host;

        [SerializeField]
        private MapClickPanel m_GameClickPanel;

        [SerializeField]
        private MapClickPanel m_TopDownClickPanel;

        [SerializeField]
        private Button m_ToMainBtn;

        [SerializeField]
        private Button m_ToTopBtn;

        [SerializeField]
        private int m_TargetPlotId;

        [SerializeField]
        private int m_CampId;

        [SerializeField]
        private int m_SelectPlotId;

        [SerializeField]
        private GameObject m_Go;

        [SerializeField]
        private int m_FromPlotId;

        [SerializeField]
        private int m_ToPlotId;

        [SerializeField]
        private int m_Faction;

        private static int[] s_CampIds = { 11000, 11001, 11002, 11003, 11004, 11005 };

        // private void Update()
        // {
        //     if (Input.GetMouseButtonDown(0))
        //     {
        //         SelectPlotAtMousePosition();
        //     }
        //
        //     if (Input.GetMouseButtonDown(1))
        //     {
        //         SetCampAtMousePosition();
        //     }
        // }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
#if UNITY_EDITOR
            var plots = new List<GvgPlotRuntimeData>(m_Asset.Plots.Count);
            foreach (var plot in m_Asset.Plots)
            {
                int factionId = 11000 + Random.Range(0, 6);
                var data = new GvgPlotRuntimeData(plot.PlotId, plot.HexIds, plot.PlotType, 0, plot.Start, plot.End, -1,
                    factionId);
                plots.Add(data);
            }
#else
            var cellIds = new List<int>();
            if (!Runtime.TryCopyMapCellIds(cellIds))
            {
                Debug.LogError("Sample Facade could not read the configured map cells.", this);
                return;
            }

            var plots = new List<GvgPlotRuntimeData>(cellIds.Count);
            foreach (var cellId in cellIds)
            {
                int factionId = 11000 + Random.Range(0, 6);
                var data = new GvgPlotRuntimeData(cellId, new List<int>() { cellId }, PlotType.Normal, 0, 0, -1,
                    -1, factionId);
                plots.Add(data);
            }
#endif
            InitImpl(plots);
            RandomCamp(plots);

            m_Host.GameplayHandler.PlotClicked += OnGamePlotClicked;
            m_Host.TopDownHandler.PlotClicked += OnTopDownPlotClicked;

            m_ToMainBtn.onClick.AddListener(() =>
            {
                m_Host.SetViewMode(MapViewMode.Gameplay);
            });

            m_ToTopBtn.onClick.AddListener(() =>
            {
                m_Host.SetViewMode(MapViewMode.TopDown);
            });
        }

        private void OnGamePlotClicked(int plotId)
        {
            var hasPlot = plotId >= 0;

            if (hasPlot)
            {
                m_Host.Runtime.SelectPlot(plotId);
                m_GameClickPanel.OnPlotClicked(plotId);
            }
        }

        private void OnTopDownPlotClicked(int plotId)
        {
            var hasPlot = plotId >= 0;

            if (hasPlot)
            {
                m_Host.Runtime.SelectPlot(plotId);
                m_TopDownClickPanel.OnPlotClicked(plotId);
            }
        }

        private void InitImpl(List<GvgPlotRuntimeData> plots)
        {
            Runtime.TryInitializeMap(plots);
        }

        private void RandomCamp(List<GvgPlotRuntimeData> plots)
        {
            foreach (var plot in plots)
            {
                var index = Random.Range(0, s_CampIds.Length);
                SetPlotCampId(plot.PlotId, s_CampIds[index]);
            }
        }

        public bool SetPlotCampId(int plotId, int campId)
        {
            return Runtime.TrySetPlotOwnerFactionId(plotId, campId);
        }

        private void SelectPlotAtMousePosition()
        {
            var result = Runtime.PickPlotAtScreenPosition(Input.mousePosition);
            if (result.HasPlot)
            {
                Runtime.SelectPlot(result.PlotId);
                if (Runtime.TryGetPlotWorldAnchor(result.PlotId, out var anchor))
                {
                    if(null != m_Go)
                        m_Go.transform.position = anchor;
                }
            }
        }

        private void SetCampAtMousePosition()
        {
            var result = Runtime.PickPlotAtScreenPosition(Input.mousePosition);
            if (result.HasPlot)
            {
                SetPlotCampId(result.PlotId, m_CampId);
            }
        }

        public void SetTargetCamp()
        {
            SetPlotCampId(m_TargetPlotId, m_CampId);
        }

        public void SelectPlot()
        {
            Runtime.SelectPlot(m_SelectPlotId);
        }

        public void DeselectAllPlot()
        {
            Runtime.DeselectAllPlots();
        }

        public void SetSelectPosition()
        {
            if (Runtime.TryGetPlotWorldAnchor(m_SelectPlotId, out var center))
            {
                m_Go.transform.position = center;
            }
        }

        public void FindPath()
        {
            var r = Runtime.TryFindPlotPath(m_FromPlotId, m_ToPlotId, m_Faction);

            if (r.IsSuccess)
            {
                Debug.Log(r.Cells);
            }
            else
            {
                Debug.LogError(r.Reason);
            }
        }

        private MapViewRuntimeFacade Runtime
        {
            get
            {
                if (m_Host == null || m_Host.Runtime == null)
                {
                    throw new InvalidOperationException("Facade requires an initialized SampleMapViewRuntimeHost.");
                }

                return m_Host.Runtime;
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(Facade))]
    public class FacadeEditor : Editor
    {
        private Facade m_Facade;

        private void OnEnable()
        {
            m_Facade = target as Facade;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Init"))
            {
                m_Facade.Initialize();
            }

            if (GUILayout.Button("Set Camp"))
            {
                m_Facade.SetTargetCamp();
            }

            if (GUILayout.Button("Select"))
            {
                m_Facade.SelectPlot();
            }

            if (GUILayout.Button("Deselect"))
            {
                m_Facade.DeselectAllPlot();
            }

            if (GUILayout.Button("SetSelectWorldPosition"))
            {
                m_Facade.SetSelectPosition();
            }

            if (GUILayout.Button("FindPath"))
            {
                m_Facade.FindPath();
            }
        }
    }
#endif
}

using System;
using System.Collections.Generic;
using HexMap.Gvg;
using HexMap.UnityRuntime;
#if UNITY_EDITOR
using HexMap.Gvg.Authoring;
using UnityEditor;
#endif
using UnityEngine;
using Random = UnityEngine.Random;

namespace HexMap.Sample
{
    public class Facade : MonoBehaviour
    {
        [SerializeField]
        private GvgMapRuntimeController m_Controller;

#if UNITY_EDITOR
        [SerializeField]
        private GvgMapAuthoringAsset m_Asset;
#endif

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

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                SelectPlotAtMousePosition();
            }

            if (Input.GetMouseButtonDown(1))
            {
                SetCampAtMousePosition();
            }
        }

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
            var mapView = m_Controller.HexMapView;
            var plots = new List<GvgPlotRuntimeData>(mapView.Map.Count);
            foreach (var hexCell in mapView.Map.Cells)
            {
                int factionId = 11000 + Random.Range(0, 6);
                var data = new GvgPlotRuntimeData(hexCell.Id, new List<int>() { hexCell.Id }, PlotType.Normal, 0, 0, -1,
                    -1, factionId);

                plots.Add(data);
            }
#endif
            InitImpl(plots);
            RandomCamp(plots);
        }

        private void InitImpl(List<GvgPlotRuntimeData> plots)
        {
            m_Controller.TryInitialize(plots);
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
            return m_Controller.TrySetPlotOwnerFactionId(plotId, campId);
        }

        private void SelectPlotAtMousePosition()
        {
            if (m_Controller == null)
            {
                return;
            }

            var result = m_Controller.PickPlotAtScreenPosition(Input.mousePosition);
            if (result.HasPlot)
            {
                m_Controller.Select(result.PlotId);
                if (m_Controller.TryGetPlotWorldAnchor(result.PlotId, out var anchor))
                {
                    if(null != m_Go)
                        m_Go.transform.position = anchor;
                }
            }
        }

        private void SetCampAtMousePosition()
        {
            if (m_Controller == null)
            {
                return;
            }

            var result = m_Controller.PickPlotAtScreenPosition(Input.mousePosition);
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
            m_Controller.Select(m_SelectPlotId);
        }

        public void DeselectAllPlot()
        {
            m_Controller.DeselectAll();
        }

        public void SetSelectPosition()
        {
            if (m_Controller.TryGetPlotWorldAnchor(m_SelectPlotId, out var center))
            {
                m_Go.transform.position = center;
            }
        }

        public void FindPath()
        {
            var r = m_Controller.TryFindPlotPath(m_FromPlotId, m_ToPlotId, m_Faction);

            if (r.IsSuccess)
            {
                Debug.Log(r.Cells);
            }
            else
            {
                Debug.LogError(r.Reason);
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
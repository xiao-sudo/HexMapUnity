// using System;
// using System.Collections.Generic;
// using System.Text;
// using HexMap.Core;
// using HexMap.Gvg;
// using HexMap.Runtime;
// using RuntimeHexMap = HexMap.Runtime.HexMap;
// using UnityEngine;
//
// namespace HexMap.UnityRuntime
// {
//     /// <summary>
//     /// Editor runtime test environment (issue 05): drives a HexMapView's topology through
//     /// the full runtime pipeline - map generation, logical plot table -> PlotRegistry
//     /// composition, GVG plot pathfinding - and visualizes the result on the generated hex
//     /// cells. Works in Play mode (OnGUI + mouse/keyboard) and, via the inspector, in
//     /// Edit mode (press "Build Demo" to preview the map and path in the Scene view).
//     /// </summary>
//     [DisallowMultipleComponent]
//     public sealed class RuntimeGvgDemo : MonoBehaviour
//     {
//         public const string EditorAutoSpawnPrefKey = "HexMap.RuntimeGvgDemo.EditorAutoSpawn";
//
//         [Header("References")]
//         [SerializeField] private HexMapView m_MapView;
//
//         [Header("Data Source")]
//         [Tooltip("Use the Editor-only GvgMapAuthoring asset as the plot table (requires a bound asset).")]
//         [SerializeField] private bool m_UseAuthoringAssetData;
//         [SerializeField] private int m_FallbackRadius = 3;
//
//         [Header("Behavior")]
//         [SerializeField] private bool m_AutoRunPathAtStart = true;
//         [SerializeField] private bool m_ClickToSelect = true;
//         [SerializeField] private bool m_ShowGui = true;
//
//         [Header("Controls")]
//         [SerializeField] private KeyCode m_NextStartKey = KeyCode.Alpha1;
//         [SerializeField] private KeyCode m_NextTargetKey = KeyCode.Alpha2;
//         [SerializeField] private KeyCode m_RunPathKey = KeyCode.Return;
//         [SerializeField] private KeyCode m_ResetKey = KeyCode.R;
//
//         [Header("Colors")]
//         [SerializeField] private Color m_BaseColor = new Color(0.24f, 0.62f, 0.90f, 1f);
//         [SerializeField] private Color m_ObstacleColor = new Color(0.55f, 0.12f, 0.12f, 1f);
//         [SerializeField] private Color m_NotOpenColor = new Color(0.38f, 0.38f, 0.38f, 1f);
//         [SerializeField] private Color m_CampColor = new Color(0.62f, 0.32f, 0.75f, 1f);
//         [SerializeField] private Color m_CityColor = new Color(0.85f, 0.65f, 0.35f, 1f);
//         [SerializeField] private Color m_PathColor = new Color(1f, 0.85f, 0.20f, 1f);
//         [SerializeField] private Color m_StartColor = new Color(0.25f, 0.90f, 0.35f, 1f);
//         [SerializeField] private Color m_TargetColor = new Color(0.95f, 0.30f, 0.22f, 1f);
//
//         private PlotRegistry m_Registry;
//         private PlotPathService m_PathService;
//         private PathResult m_LastPath;
//         private readonly List<int> m_StartCandidates = new List<int>();
//         private readonly List<int> m_TargetCandidates = new List<int>();
//         private int m_StartIndex;
//         private int m_TargetIndex;
//         private bool m_NextClickSetsStart = true;
//         private string m_Error = string.Empty;
//         private string m_DataMode = string.Empty;
//         private bool m_IsReady;
//
//         public HexMapView MapView
//         {
//             get { return m_MapView; }
//             set { m_MapView = value; }
//         }
//
//         public bool UseAuthoringAssetData
//         {
//             get { return m_UseAuthoringAssetData; }
//             set { m_UseAuthoringAssetData = value; }
//         }
//
//         public bool ShowGui
//         {
//             get { return m_ShowGui; }
//             set { m_ShowGui = value; }
//         }
//
//         public bool IsReady
//         {
//             get { return m_IsReady; }
//         }
//
//         public string Error
//         {
//             get { return m_Error; }
//         }
//
//         public string DataMode
//         {
//             get { return m_DataMode; }
//         }
//
//         public PlotRegistry Registry
//         {
//             get { return m_Registry; }
//         }
//
//         public PathResult LastPath
//         {
//             get { return m_LastPath; }
//         }
//
//         public int StartPlotId
//         {
//             get
//             {
//                 return m_StartCandidates.Count == 0 ? -1 : m_StartCandidates[m_StartIndex];
//             }
//         }
//
//         public int TargetPlotId
//         {
//             get
//             {
//                 return m_TargetCandidates.Count == 0 ? -1 : m_TargetCandidates[m_TargetIndex];
//             }
//         }
//
//         private void Start()
//         {
//             BuildDemo();
//         }
//
//         /// <summary>
//         /// Builds (or rebuilds) the whole runtime pipeline: map, plot table, registry,
//         /// pathfinding candidates, base coloring and the initial path.
//         /// </summary>
//         public void BuildDemo()
//         {
//             ResetRuntimeState();
//
//             var view = ResolveView();
//             if (view == null)
//             {
//                 m_Error = "No HexMapView is available. Assign one or add the demo to a scene with a HexMapView.";
//                 ReportFailure();
//                 return;
//             }
//
//             if (view.Map == null)
//             {
//                 view.Build();
//             }
//
//             var map = view.Map;
//             if (map == null)
//             {
//                 m_Error = "The HexMapView failed to build its map.";
//                 ReportFailure();
//                 return;
//             }
//
//             var rows = BuildRows(view);
//             if (rows == null)
//             {
//                 ReportFailure();
//                 return;
//             }
//
//             PlotRegistry registry;
//             string composeError;
//             if (!GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out composeError))
//             {
//                 m_Error = "Plot table composition failed: " + composeError;
//                 ReportFailure();
//                 return;
//             }
//
//             m_Registry = registry;
//             m_PathService = new PlotPathService(m_Registry);
//             BuildCandidates();
//
//             if (m_StartCandidates.Count == 0 || m_TargetCandidates.Count == 0)
//             {
//                 m_Error = "The composed plot table has no open, passable, camp-free candidate plots to path through.";
//                 ReportFailure();
//                 return;
//             }
//
//             m_IsReady = true;
//             ApplyPlotColors();
//             if (m_AutoRunPathAtStart)
//             {
//                 RunPathfinding();
//             }
//         }
//
//         /// <summary>
//         /// Re-runs pathfinding between the current start and target plots and recolors
//         /// the map so the path, start and target are visible.
//         /// </summary>
//         public void RunPathfinding()
//         {
//             if (!m_IsReady || m_PathService == null)
//             {
//                 Debug.LogWarning("[RuntimeGvgDemo] Cannot pathfind before BuildDemo succeeded.");
//                 return;
//             }
//
//             var startPlotId = StartPlotId;
//             var targetPlotId = TargetPlotId;
//             if (startPlotId < 0 || targetPlotId < 0)
//             {
//                 Debug.LogWarning("[RuntimeGvgDemo] No valid start/target candidates.");
//                 return;
//             }
//
//             if (m_LastPath == null)
//             {
//                 m_LastPath = new PathResult(new List<int>(m_Registry.Map.Count));
//             }
//
//             m_PathService.FindPath(startPlotId, targetPlotId, Plot.NoFactionId, m_LastPath);
//
//             ApplyPlotColors();
//             if (m_LastPath.IsSuccess)
//             {
//                 HighlightPath();
//             }
//
//             Debug.Log("[RuntimeGvgDemo] " + SummarizePath());
//         }
//
//         /// <summary>
//         /// Restores the base per-plot coloring without the path overlay.
//         /// </summary>
//         public void ResetVisuals()
//         {
//             ApplyPlotColors();
//         }
//
//         public void CycleStart(int direction)
//         {
//             if (m_StartCandidates.Count == 0)
//             {
//                 return;
//             }
//
//             m_StartIndex = Mod(m_StartIndex + direction, m_StartCandidates.Count);
//         }
//
//         public void CycleTarget(int direction)
//         {
//             if (m_TargetCandidates.Count == 0)
//             {
//                 return;
//             }
//
//             m_TargetIndex = Mod(m_TargetIndex + direction, m_TargetCandidates.Count);
//         }
//
//         public bool SetStartPlot(int plotId)
//         {
//             var index = m_StartCandidates.IndexOf(plotId);
//             if (index < 0)
//             {
//                 return false;
//             }
//
//             m_StartIndex = index;
//             return true;
//         }
//
//         public bool SetTargetPlot(int plotId)
//         {
//             var index = m_TargetCandidates.IndexOf(plotId);
//             if (index < 0)
//             {
//                 return false;
//             }
//
//             m_TargetIndex = index;
//             return true;
//         }
//
//         private void Update()
//         {
//             if (!m_IsReady)
//             {
//                 return;
//             }
//
//             if (Input.GetKeyDown(m_NextStartKey))
//             {
//                 CycleStart(1);
//                 RunPathfinding();
//             }
//
//             if (Input.GetKeyDown(m_NextTargetKey))
//             {
//                 CycleTarget(1);
//                 RunPathfinding();
//             }
//
//             if (Input.GetKeyDown(m_RunPathKey))
//             {
//                 RunPathfinding();
//             }
//
//             if (Input.GetKeyDown(m_ResetKey))
//             {
//                 ResetVisuals();
//             }
//
//             if (m_ClickToSelect)
//             {
//                 HandleMouseSelection();
//             }
//         }
//
//         private void OnGUI()
//         {
//             if (!m_ShowGui)
//             {
//                 return;
//             }
//
//             GUILayout.BeginArea(new Rect(12f, 12f, 440f, 360f), GUI.skin.box);
//             DrawHeader();
//             if (m_IsReady)
//             {
//                 DrawControls();
//             }
//
//             DrawStatus();
//             DrawHelp();
//             GUILayout.EndArea();
//         }
//
//         private void DrawHeader()
//         {
//             GUILayout.Label("Runtime GVG Test Environment", GUI.skin.GetStyle("boldLabel"));
//             if (m_MapView != null && m_MapView.Map != null)
//             {
//                 GUILayout.Label(string.Format(
//                     "Map: radius {0}, cells {1} | Mode: {2}",
//                     m_MapView.Map.Radius.Radius,
//                     m_MapView.Map.Count,
//                     m_DataMode));
//             }
//             else
//             {
//                 GUILayout.Label("Map: not built yet");
//             }
//
//             if (m_Registry != null)
//             {
//                 GUILayout.Label(string.Format("Plots: {0}", m_Registry.Count));
//             }
//
//             GUILayout.Space(4f);
//         }
//
//         private void DrawControls()
//         {
//             GUILayout.BeginHorizontal();
//             GUILayout.Label("Start:", GUILayout.Width(52f));
//             if (GUILayout.Button("<", GUILayout.Width(28f)))
//             {
//                 CycleStart(-1);
//                 RunPathfinding();
//             }
//
//             GUILayout.Label(DescribePlot(StartPlotId));
//             if (GUILayout.Button(">", GUILayout.Width(28f)))
//             {
//                 CycleStart(1);
//                 RunPathfinding();
//             }
//
//             GUILayout.EndHorizontal();
//
//             GUILayout.BeginHorizontal();
//             GUILayout.Label("Target:", GUILayout.Width(52f));
//             if (GUILayout.Button("<", GUILayout.Width(28f)))
//             {
//                 CycleTarget(-1);
//                 RunPathfinding();
//             }
//
//             GUILayout.Label(DescribePlot(TargetPlotId));
//             if (GUILayout.Button(">", GUILayout.Width(28f)))
//             {
//                 CycleTarget(1);
//                 RunPathfinding();
//             }
//
//             GUILayout.EndHorizontal();
//
//             GUILayout.BeginHorizontal();
//             if (GUILayout.Button("Find Path"))
//             {
//                 RunPathfinding();
//             }
//
//             if (GUILayout.Button("Reset Colors"))
//             {
//                 ResetVisuals();
//             }
//
//             GUILayout.EndHorizontal();
//
//             GUILayout.Space(4f);
//         }
//
//         private void DrawStatus()
//         {
//             if (!string.IsNullOrEmpty(m_Error))
//             {
//                 GUI.color = Color.red;
//                 GUILayout.Label("Error: " + m_Error, GUI.skin.GetStyle("wordWrappedLabel"));
//                 GUI.color = Color.white;
//                 return;
//             }
//
//             if (m_LastPath == null)
//             {
//                 return;
//             }
//
//             if (m_LastPath.IsSuccess)
//             {
//                 GUILayout.Label(string.Format(
//                     "Path: SUCCESS  cost {0}  cells {1}",
//                     m_LastPath.Cost,
//                     m_LastPath.Count));
//                 GUILayout.Label(PlotSequenceText(), GUI.skin.GetStyle("wordWrappedLabel"));
//             }
//             else
//             {
//                 GUI.color = new Color(1f, 0.75f, 0.2f);
//                 GUILayout.Label(string.Format(
//                     "Path: {0}  reason {1}",
//                     m_LastPath.Status,
//                     m_LastPath.Reason),
//                     GUI.skin.GetStyle("wordWrappedLabel"));
//                 GUI.color = Color.white;
//             }
//         }
//
//         private void DrawHelp()
//         {
//             GUILayout.Space(6f);
//             GUILayout.Label(
//                 "Keys: " + m_NextStartKey + " cycle start, " + m_NextTargetKey + " cycle target, " +
//                 m_RunPathKey + " find path, " + m_ResetKey + " reset colors." +
//                 (m_ClickToSelect
//                     ? " Click plots in pairs: first click = start, second click = target."
//                     : string.Empty),
//                 GUI.skin.GetStyle("wordWrappedLabel"));
//         }
//
//         private string DescribePlot(int plotId)
//         {
//             if (plotId < 0 || m_Registry == null)
//             {
//                 return "none";
//             }
//
//             Plot plot;
//             if (!m_Registry.TryGetPlot(plotId, out plot) || plot.Cells.Count == 0)
//             {
//                 return plotId.ToString();
//             }
//
//             return string.Format(
//                 "#{0} [{1}]",
//                 plotId,
//                 plot.Cells[0].Coordinate);
//         }
//
//         private string PlotSequenceText()
//         {
//             var builder = new StringBuilder();
//             builder.Append("Plots: ");
//             var seen = new HashSet<int>();
//             var appended = 0;
//             var cells = m_LastPath.Cells;
//             for (var index = 0; index < cells.Count; index++)
//             {
//                 Plot plot;
//                 if (!m_Registry.TryGetPlotForCell(cells[index].Id, out plot) ||
//                     !seen.Add(plot.PlotId))
//                 {
//                     continue;
//                 }
//
//                 if (appended > 0)
//                 {
//                     builder.Append(" -> ");
//                 }
//
//                 builder.Append(plot.PlotId);
//                 appended++;
//                 if (appended >= 24)
//                 {
//                     builder.Append(" ...");
//                     break;
//                 }
//             }
//
//             return builder.ToString();
//         }
//
//         private void HighlightPath()
//         {
//             var startCoordinate = GetPlotCoordinate(StartPlotId);
//             var targetCoordinate = GetPlotCoordinate(TargetPlotId);
//             var cells = m_LastPath.Cells;
//             for (var index = 0; index < cells.Count; index++)
//             {
//                 var coordinate = cells[index].Coordinate;
//                 var color = m_PathColor;
//                 if (coordinate == startCoordinate)
//                 {
//                     color = m_StartColor;
//                 }
//                 else if (coordinate == targetCoordinate)
//                 {
//                     color = m_TargetColor;
//                 }
//
//                 ApplyCellColor(coordinate, color);
//             }
//         }
//
//         private HexCoord? GetPlotCoordinate(int plotId)
//         {
//             if (plotId < 0 || m_Registry == null)
//             {
//                 return null;
//             }
//
//             Plot plot;
//             if (!m_Registry.TryGetPlot(plotId, out plot) || plot.Cells.Count == 0)
//             {
//                 return null;
//             }
//
//             return plot.Cells[0].Coordinate;
//         }
//
//         private string SummarizePath()
//         {
//             if (m_LastPath == null)
//             {
//                 return "No path result.";
//             }
//
//             if (!m_LastPath.IsSuccess)
//             {
//                 return string.Format(
//                     "Path {0} -> {1}: {2} (reason {3})",
//                     StartPlotId,
//                     TargetPlotId,
//                     m_LastPath.Status,
//                     m_LastPath.Reason);
//             }
//
//             return string.Format(
//                 "Path {0} -> {1}: success, cost {2}, cells {3} | {4}",
//                 StartPlotId,
//                 TargetPlotId,
//                 m_LastPath.Cost,
//                 m_LastPath.Count,
//                 PlotSequenceText());
//         }
//
//         private void HandleMouseSelection()
//         {
//             if (!Input.GetMouseButtonDown(0))
//             {
//                 return;
//             }
//
//             var camera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
//             if (camera == null)
//             {
//                 return;
//             }
//
//             var ray = camera.ScreenPointToRay(Input.mousePosition);
//             var plane = m_MapView.WorldPlane;
//             float distance;
//             if (!plane.Raycast(ray, out distance) || distance < 0f)
//             {
//                 return;
//             }
//
//             var worldPoint = ray.GetPoint(distance);
//             var coordinate = m_MapView.WorldToHex(worldPoint);
//             var query = m_MapView.Map.Query(coordinate);
//             if (!query.HasCell)
//             {
//                 return;
//             }
//
//             Plot plot;
//             if (!m_Registry.TryGetPlotForCell(query.Cell.Id, out plot))
//             {
//                 return;
//             }
//
//             if (m_NextClickSetsStart)
//             {
//                 if (!SetStartPlot(plot.PlotId))
//                 {
//                     return;
//                 }
//
//                 m_NextClickSetsStart = false;
//                 m_LastPath = null;
//                 ApplyPlotColors();
//                 return;
//             }
//
//             if (SetTargetPlot(plot.PlotId))
//             {
//                 m_NextClickSetsStart = true;
//                 RunPathfinding();
//             }
//         }
//
//         private void BuildCandidates()
//         {
//             for (var index = 0; index < m_Registry.Plots.Count; index++)
//             {
//                 var plot = m_Registry.Plots[index];
//                 if (!IsPathCandidate(plot))
//                 {
//                     continue;
//                 }
//
//                 m_StartCandidates.Add(plot.PlotId);
//                 m_TargetCandidates.Add(plot.PlotId);
//             }
//
//             m_StartCandidates.Sort();
//             m_TargetCandidates.Sort();
//
//             m_StartIndex = IndexOf(m_StartCandidates, DefaultStartPlotId());
//             if (m_StartIndex < 0)
//             {
//                 m_StartIndex = 0;
//             }
//
//             m_TargetIndex = FurthestCandidateIndex();
//             if (m_TargetIndex < 0)
//             {
//                 m_TargetIndex = 0;
//             }
//         }
//
//         private int DefaultStartPlotId()
//         {
//             Plot plot;
//             if (m_Registry.TryGetPlotForCell(0, out plot))
//             {
//                 return plot.PlotId;
//             }
//
//             return m_StartCandidates.Count == 0 ? -1 : m_StartCandidates[0];
//         }
//
//         private int FurthestCandidateIndex()
//         {
//             var center = new HexCoord(0, 0);
//             var bestIndex = -1;
//             var bestDistance = -1;
//             for (var index = 0; index < m_TargetCandidates.Count; index++)
//             {
//                 Plot plot;
//                 if (!m_Registry.TryGetPlot(m_TargetCandidates[index], out plot) || plot.Cells.Count == 0)
//                 {
//                     continue;
//                 }
//
//                 var distance = HexCoord.Distance(center, plot.Cells[0].Coordinate);
//                 if (distance > bestDistance)
//                 {
//                     bestDistance = distance;
//                     bestIndex = index;
//                 }
//             }
//
//             return bestIndex;
//         }
//
//         private static bool IsPathCandidate(Plot plot)
//         {
//             return plot.IsOpenForPathfinding &&
//                 plot.BlockingState == BlockingState.Passable &&
//                 plot.AffiliatedCampId == Plot.NoAffiliatedCampId;
//         }
//
//         private static int IndexOf(List<int> candidates, int plotId)
//         {
//             return plotId < 0 ? -1 : candidates.IndexOf(plotId);
//         }
//
//         private static int Mod(int value, int count)
//         {
//             var result = value % count;
//             return result < 0 ? result + count : result;
//         }
//
//         private List<GvgPlotRuntimeData> BuildRows(HexMapView view)
//         {
// #if UNITY_EDITOR
//             if (m_UseAuthoringAssetData)
//             {
//                 var asset = view.GvgMapAuthoringAssetEditorOnly;
//                 if (asset == null)
//                 {
//                     m_Error = "UseAuthoringAssetData is enabled but the view has no GvgMapAuthoring asset bound.";
//                     return null;
//                 }
//
//                 m_DataMode = "AuthoringAsset (" + asset.name + ")";
//                 var rows = new List<GvgPlotRuntimeData>(Gvg.Authoring.GvgMapAuthoringRuntimeAdapter.ToRuntimeData(asset));
//                 if (rows.Count == 0)
//                 {
//                     m_Error = "The authoring asset produced no plot rows.";
//                     return null;
//                 }
//
//                 return rows;
//             }
// #endif
//             m_DataMode = "GeneratedDemoTable";
//             return RuntimeGvgDemoData.CreateDefaultTable(view.Map);
//         }
//
//         private HexMapView ResolveView()
//         {
//             if (m_MapView != null)
//             {
//                 return m_MapView;
//             }
//
//             m_MapView = FindObjectOfType<HexMapView>();
//             if (m_MapView != null)
//             {
//                 return m_MapView;
//             }
//
//             if (!Application.isPlaying)
//             {
//                 return null;
//             }
//
//             var gameObject = new GameObject("Runtime GVG Map (auto)");
//             m_MapView = gameObject.AddComponent<HexMapView>();
//             m_MapView.Radius = Mathf.Max(1, m_FallbackRadius);
//             return m_MapView;
//         }
//
//         private void ResetRuntimeState()
//         {
//             m_IsReady = false;
//             m_LastPath = null;
//             m_Registry = null;
//             m_PathService = null;
//             m_Error = string.Empty;
//             m_DataMode = string.Empty;
//             m_StartIndex = 0;
//             m_TargetIndex = 0;
//             m_NextClickSetsStart = true;
//             m_StartCandidates.Clear();
//             m_TargetCandidates.Clear();
//         }
//
//         private void ReportFailure()
//         {
//             Debug.LogError("[RuntimeGvgDemo] " + m_Error);
//         }
//
//         private void ApplyPlotColors()
//         {
//             if (m_MapView == null || m_Registry == null)
//             {
//                 return;
//             }
//
//             var cells = m_Registry.Map.Cells;
//             for (var index = 0; index < cells.Count; index++)
//             {
//                 var cell = cells[index];
//                 var color = m_NotOpenColor;
//                 Plot plot;
//                 if (m_Registry.TryGetPlotForCell(cell.Id, out plot))
//                 {
//                     color = ColorForPlot(plot);
//                 }
//
//                 ApplyCellColor(cell.Coordinate, color);
//             }
//         }
//
//         private Color ColorForPlot(Plot plot)
//         {
//             switch (plot.PlotType)
//             {
//                 case PlotType.Obstacle:
//                     return m_ObstacleColor;
//                 case PlotType.Camp:
//                     return m_CampColor;
//                 case PlotType.SmallCity:
//                 case PlotType.BigCity:
//                 case PlotType.Capital:
//                     return m_CityColor;
//                 default:
//                     return m_BaseColor;
//             }
//         }
//
//         private void ApplyCellColor(HexCoord coordinate, Color color)
//         {
//             HexView view;
//             if (m_MapView.TryGetHexView(coordinate, out view))
//             {
//                 view.SetAppearance(new HexAppearance(true, color));
//             }
//         }
//
// #if UNITY_EDITOR
//         [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
//         private static void EditorAutoSpawn()
//         {
//             if (!UnityEditor.EditorPrefs.GetBool(EditorAutoSpawnPrefKey, true))
//             {
//                 return;
//             }
//
//             if (FindObjectOfType<RuntimeGvgDemo>() != null)
//             {
//                 return;
//             }
//
//             var view = FindObjectOfType<HexMapView>();
//             if (view == null)
//             {
//                 return;
//             }
//
//             var demo = view.gameObject.AddComponent<RuntimeGvgDemo>();
//             demo.MapView = view;
//             demo.UseAuthoringAssetData = view.GvgMapAuthoringAssetEditorOnly != null;
//         }
// #endif
//     }
// }

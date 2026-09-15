using System;
using System.Collections.Generic;
using System.Linq;
using HexMap.Core;
using HexMap.Gvg.Authoring;
using HexMap.Runtime;
using HexMap.UnityRuntime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEditor;
using UnityEngine;

namespace HexMap.Gvg.Editor
{
    public sealed class GvgMapAuthoringWindow : EditorWindow
    {
        private enum ToolMode
        {
            SelectPlot,
            PaintAdd,
            PaintRemove
        }

        private GUIStyle m_LabelStyle;
        private readonly List<int> m_SelectedHexIds = new List<int>();
        private GvgMapAuthoringAsset m_Asset;
        private HexMapView m_MapView;
        private string m_MapError = string.Empty;
        private bool m_SceneEditing = true;
        private GvgMapBindingDiagnostic m_BindingDiagnostic = GvgMapBindingDiagnostic.NotBound;
        private ToolMode m_Mode;
        private int m_SelectedPlotId;
        private bool m_HasSelectedPlot;
        private bool m_ShowHexIds;
        private bool m_ShowCoordinates;
        private bool m_ShowTypes;
        private string m_PastedHexIds = string.Empty;
        private Vector2 m_Scroll;

        [MenuItem("Tools/Hex Map/GVG Map Authoring")]
        public static void Open()
        {
            var window = GetWindow<GvgMapAuthoringWindow>("GVG Map Authoring");
            window.InitializeFromScene();
        }

        private void OnEnable()
        {
            m_LabelStyle = CreateLabelStyle();
            SceneView.duringSceneGui += OnSceneGui;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnUndoRedoPerformed()
        {
            SceneView.RepaintAll();
            Repaint();
        }

        private void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            DrawAssetControls();
            if (m_Asset != null)
            {
                EditorGUILayout.Space();
                DrawMapControls();
                EditorGUILayout.Space();
                DrawToolControls();
                EditorGUILayout.Space();
                DrawSelectedPlotControls();
                EditorGUILayout.Space();
                DrawValidationControls();
                EditorGUILayout.Space();
                DrawExportControls();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetControls()
        {
            EditorGUILayout.BeginHorizontal();
            m_Asset = (GvgMapAuthoringAsset)EditorGUILayout.ObjectField("Asset", m_Asset, typeof(GvgMapAuthoringAsset), false);
            if (GUILayout.Button("Create", GUILayout.Width(80f)))
            {
                CreateAsset();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMapControls()
        {
            EditorGUILayout.LabelField("Scene Map", EditorStyles.boldLabel);
            m_MapView = (HexMapView)EditorGUILayout.ObjectField(
                "HexMapView",
                ResolveMapView(),
                typeof(HexMapView),
                true);

            if (m_MapView == null)
            {
                EditorGUILayout.HelpBox("Assign or select a scene HexMapView before editing GVG data.", MessageType.Warning);
                return;
            }

            GvgMapAuthoringAsset boundAsset;
            if (TryResolveHealthyBinding(out boundAsset))
            {
                EditorGUILayout.LabelField("Bound Asset", boundAsset != null ? boundAsset.name : "(none)");
            }
            else
            {
                EditorGUILayout.HelpBox(DescribeBinding(m_BindingDiagnostic), MessageType.Warning);
            }

            RuntimeHexMap map;
            HexLayout layout;
            string error;
            if (!m_MapView.TryCreateSnapshots(out map, out layout, out error))
            {
                m_MapError = error;
                EditorGUILayout.HelpBox("Invalid scene map configuration: " + error, MessageType.Error);
                return;
            }

            m_MapError = string.Empty;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Radius", m_MapView.Radius);
            EditorGUILayout.EnumPopup("Orientation", m_MapView.Orientation);
            EditorGUILayout.EnumPopup("Plane", m_MapView.Plane);
            EditorGUILayout.FloatField("Outer Radius", m_MapView.OuterRadius);
            EditorGUILayout.FloatField("Secondary Scale", m_MapView.SecondaryScale);
            EditorGUILayout.Vector3Field("Origin", m_MapView.Origin);
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginChangeCheck();
            var mapId = EditorGUILayout.TextField("Map Id", m_Asset.MapId);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_Asset, "Edit GVG Map Id");
                m_Asset.MapId = mapId;
                EditorUtility.SetDirty(m_Asset);
            }

            if (GUILayout.Button("Reset Default Plots"))
            {
                Undo.RecordObject(m_Asset, "Reset GVG Map Plots");
                GvgMapAuthoringUtility.ResetToDefaultPlots(m_Asset, map);
                ClearSelection();
                EditorUtility.SetDirty(m_Asset);
                SceneView.RepaintAll();
            }
        }
        private void DrawToolControls()
        {
            EditorGUILayout.LabelField("Scene Tool", EditorStyles.boldLabel);
            m_SceneEditing = EditorGUILayout.Toggle("Scene Editing", m_SceneEditing);
            m_Mode = (ToolMode)GUILayout.Toolbar((int)m_Mode, new[] { "Select Plot", "Paint Add", "Paint Remove" });
            m_ShowHexIds = EditorGUILayout.Toggle("Show HexId", m_ShowHexIds);
            m_ShowCoordinates = EditorGUILayout.Toggle("Show Coordinates", m_ShowCoordinates);
            m_ShowTypes = EditorGUILayout.Toggle("Show Type", m_ShowTypes);
            DrawPlotTypeLegend();
            EditorGUILayout.LabelField("Selected Hexes", string.Join(",", m_SelectedHexIds.ConvertAll(value => value.ToString()).ToArray()));
        }

        private void DrawPlotTypeLegend()
        {
            EditorGUILayout.LabelField("Plot Type Colors", EditorStyles.miniBoldLabel);
            foreach (PlotType plotType in Enum.GetValues(typeof(PlotType)))
            {
                EditorGUILayout.BeginHorizontal();
                var swatch = GUILayoutUtility.GetRect(18f, EditorGUIUtility.singleLineHeight, GUILayout.Width(18f));
                EditorGUI.DrawRect(swatch, ColorFor(plotType));
                EditorGUILayout.LabelField(plotType.ToString());
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            var affiliatedSwatch = GUILayoutUtility.GetRect(18f, EditorGUIUtility.singleLineHeight, GUILayout.Width(18f));
            EditorGUI.DrawRect(affiliatedSwatch, m_AffiliatedPlotOutlineColor);
            var affiliatedInnerSwatch = new Rect(
                affiliatedSwatch.x + 4f,
                affiliatedSwatch.y + 4f,
                affiliatedSwatch.width - 8f,
                affiliatedSwatch.height - 8f);
            EditorGUI.DrawRect(affiliatedInnerSwatch, Color.gray);
            EditorGUILayout.LabelField("Affiliated Plot outline");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var notOpenSwatch = GUILayoutUtility.GetRect(18f, EditorGUIUtility.singleLineHeight, GUILayout.Width(18f));
            EditorGUI.DrawRect(notOpenSwatch, m_NotOpenOverlayColor);
            EditorGUILayout.LabelField("NotOpen overlay");
            EditorGUILayout.EndHorizontal();
        }
        private void DrawSelectedPlotControls()
        {
            RuntimeHexMap map;
            HexLayout selectedLayout;
            if (!TryCreatePreviewData(out map, out selectedLayout)) return;
            var plot = FindSelectedPlot();
            EditorGUILayout.LabelField("Selected Plot", EditorStyles.boldLabel);
            if (plot == null)
            {
                EditorGUILayout.HelpBox("Select a Plot in SceneView.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("PlotId", plot.PlotId.ToString());
            EditorGUILayout.LabelField("HexIds", string.Join(",", plot.HexIds.ConvertAll(value => value.ToString()).ToArray()));

            EditorGUI.BeginChangeCheck();
            var affiliatedCampId = EditorGUILayout.IntField("Affiliated Camp Id", plot.AffiliatedCampId);
            if (EditorGUI.EndChangeCheck())
            {
                if (affiliatedCampId >= Plot.NoAffiliatedCampId)
                {
                    Undo.RecordObject(m_Asset, "Edit GVG Plot Affiliated Camp Id");
                    plot.AffiliatedCampId = affiliatedCampId;
                    EditorUtility.SetDirty(m_Asset);
                    SceneView.RepaintAll();
                }
                else
                {
                    EditorGUILayout.HelpBox("Affiliated Camp Id must be -1 or non-negative.", MessageType.Error);
                }
            }
            if (plot.PlotType == PlotType.Camp && plot.AffiliatedCampId == Plot.NoAffiliatedCampId)
            {
                EditorGUILayout.HelpBox("Camp Plot with -1 is affiliated with itself by default at runtime.", MessageType.Info);
            }

            EditorGUI.BeginChangeCheck();
            var plotType = (PlotType)EditorGUILayout.EnumPopup("Plot Type", plot.PlotType);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_Asset, "Edit GVG Plot Type");
                plot.PlotType = plotType;
                if (plot.HexIds.Count == 1)
                {
                    var hexId = plot.HexIds[0];
                    for (var index = 0; index < m_Asset.Plots.Count; index++)
                    {
                        var layer = m_Asset.Plots[index];
                        if (layer != null && layer.HexIds.Count == 1 && layer.HexIds[0] == hexId)
                        {
                            layer.PlotType = plotType;
                        }
                    }
                }

                GvgMapAuthoringUtility.NormalizePlotIds(m_Asset, map);
                EditorUtility.SetDirty(m_Asset);
                SceneView.RepaintAll();
            }

            EditorGUI.BeginDisabledGroup(plot.IsMultiCell);
            EditorGUI.BeginChangeCheck();
            var start = EditorGUILayout.IntField("Start Seconds", plot.Start);
            var end = EditorGUILayout.IntField("End Seconds", plot.End);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_Asset, "Edit GVG Plot Time Layer");
                plot.Start = start;
                plot.End = end;
                GvgMapAuthoringUtility.NormalizePlotIds(m_Asset, map);
                EditorUtility.SetDirty(m_Asset);
                SceneView.RepaintAll();
            }
            EditorGUI.EndDisabledGroup();

            if (!plot.IsMultiCell)
            {
                DrawHexScheduleControls(plot.HexIds[0]);
            }
            var initialState = GetInitialPlotState(plot);
            var initialPassable = initialState == PlotState.Open && plot.PlotType != PlotType.Obstacle;
            EditorGUILayout.LabelField("Initial Runtime State", initialState.ToString());
            EditorGUILayout.LabelField("Initial Passable", initialPassable ? "Yes" : "No");
            EditorGUILayout.LabelField(
                "Initial Capturable",
                initialState == PlotState.Open && plot.PlotType != PlotType.Camp ? "Yes" : "No");

            if (plot.IsMultiCell)
            {
                EditorGUILayout.HelpBox("Multi-cell Plots are permanently open in this version.", MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Merge Selected"))
            {
                RecordAndApply("Merge GVG Plots", () => GvgMapAuthoringUtility.TryMergeToMultiPlot(m_Asset, map, plot.PlotId, m_SelectedHexIds));
            }
            if (GUILayout.Button("Delete Plot"))
            {
                RecordAndApply("Delete GVG Plot", () => GvgMapAuthoringUtility.TryDeletePlot(m_Asset, map, plot.PlotId));
                ClearSelection();
            }
            EditorGUILayout.EndHorizontal();

            m_PastedHexIds = EditorGUILayout.TextField("Paste HexIds", m_PastedHexIds);
            if (GUILayout.Button("Merge Pasted HexIds"))
            {
                RecordAndApply("Paste GVG HexIds", () => GvgMapAuthoringUtility.TryPasteHexIdsToPlot(m_Asset, map, plot.PlotId, m_PastedHexIds));
            }
        }
        private void DrawHexScheduleControls(int hexId)
        {
            RuntimeHexMap map;
            HexLayout scheduleLayout;
            if (!TryCreatePreviewData(out map, out scheduleLayout)) return;
            var layers = GetLayersForHex(hexId);
            EditorGUILayout.LabelField("Hex Open Time Layers", EditorStyles.boldLabel);

            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Plot " + layer.PlotId, GUILayout.Width(70f));
                EditorGUI.BeginChangeCheck();
                var start = EditorGUILayout.IntField("Start", layer.Start);
                var end = EditorGUILayout.IntField("End", layer.End);
                var changed = EditorGUI.EndChangeCheck();
                if (changed)
                {
                    Undo.RecordObject(m_Asset, "Edit GVG Hex Open Time Layer");
                    layer.Start = start;
                    layer.End = end;
                    GvgMapAuthoringUtility.NormalizePlotIds(m_Asset, map);
                    EditorUtility.SetDirty(m_Asset);
                    SceneView.RepaintAll();
                    Repaint();
                }

                var delete = layers.Count > 1 && GUILayout.Button("Delete", GUILayout.Width(55f));
                EditorGUILayout.EndHorizontal();
                if (delete)
                {
                    DeleteHexScheduleLayer(hexId, layer);
                    break;
                }
            }

            if (GUILayout.Button("Add Time Layer"))
            {
                AddHexScheduleLayer(hexId, layers);
            }
        }

        private List<GvgPlotAuthoringData> GetLayersForHex(int hexId)
        {
            var layers = new List<GvgPlotAuthoringData>();
            for (var index = 0; index < m_Asset.Plots.Count; index++)
            {
                var plot = m_Asset.Plots[index];
                if (plot != null && !plot.IsMultiCell && plot.HexIds.Contains(hexId))
                {
                    layers.Add(plot);
                }
            }

            layers.Sort((left, right) =>
            {
                var result = left.Start.CompareTo(right.Start);
                return result != 0 ? result : left.PlotId.CompareTo(right.PlotId);
            });
            return layers;
        }

        private void AddHexScheduleLayer(int hexId, List<GvgPlotAuthoringData> layers)
        {
            RuntimeHexMap map;
            HexLayout scheduleLayout;
            if (!TryCreatePreviewData(out map, out scheduleLayout)) return;
            if (layers.Count == 0) return;

            var last = layers[layers.Count - 1];
            var splitStart = last.End == -1 ? last.Start + 1 : last.End;
            if (splitStart <= last.Start || splitStart == int.MaxValue)
            {
                EditorUtility.DisplayDialog("Add Time Layer", "The final time layer has no available second to split.", "OK");
                return;
            }

            Undo.RecordObject(m_Asset, "Add GVG Hex Open Time Layer");
            last.End = splitStart;
            m_Asset.ReplacePlots(CreatePlotsWithAdditionalLayer(hexId, layers, splitStart));
            GvgMapAuthoringUtility.NormalizePlotIds(m_Asset, map);
            EditorUtility.SetDirty(m_Asset);
            SceneView.RepaintAll();
            Repaint();
        }

        private void DeleteHexScheduleLayer(int hexId, GvgPlotAuthoringData layerToDelete)
        {
            RuntimeHexMap map;
            HexLayout scheduleLayout;
            if (!TryCreatePreviewData(out map, out scheduleLayout)) return;
            var layers = GetLayersForHex(hexId);
            if (layers.Count <= 1) return;

            var remaining = new List<GvgPlotAuthoringData>();
            for (var index = 0; index < m_Asset.Plots.Count; index++)
            {
                var plot = m_Asset.Plots[index];
                if (plot != layerToDelete) remaining.Add(plot.Clone());
            }

            var deletedIndex = layers.IndexOf(layerToDelete);
            if (deletedIndex > 0)
            {
                var previous = layers[deletedIndex - 1];
                var replacement = remaining.Find(plot => plot.PlotId == previous.PlotId);
                if (replacement != null)
                {
                    replacement.End = deletedIndex == layers.Count - 1
                        ? -1
                        : layers[deletedIndex].End;
                }
            }

            Undo.RecordObject(m_Asset, "Delete GVG Hex Open Time Layer");
            m_Asset.ReplacePlots(remaining);
            GvgMapAuthoringUtility.NormalizePlotIds(m_Asset, map);
            EditorUtility.SetDirty(m_Asset);
            SceneView.RepaintAll();
            Repaint();
        }

        private IEnumerable<GvgPlotAuthoringData> CreatePlotsWithAdditionalLayer(
            int hexId,
            List<GvgPlotAuthoringData> layers,
            int splitStart)
        {
            var result = new List<GvgPlotAuthoringData>();
            for (var index = 0; index < m_Asset.Plots.Count; index++)
            {
                var plot = m_Asset.Plots[index];
                if (plot == null) continue;
                if (plot.IsMultiCell || !plot.HexIds.Contains(hexId))
                {
                    result.Add(plot.Clone());
                }
            }

            for (var index = 0; index < layers.Count; index++)
            {
                result.Add(layers[index].Clone());
            }

            result.Add(new GvgPlotAuthoringData(0, new[] { hexId }, layers[0].PlotType, splitStart, -1));
            return result;
        }
        private void DrawValidationControls()
        {
            RuntimeHexMap validationMap; HexLayout validationLayout; if (!TryCreatePreviewData(out validationMap, out validationLayout)) return;
            var validation = GvgMapAuthoringUtility.Validate(m_Asset, validationMap);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (validation.IsValid)
            {
                EditorGUILayout.HelpBox("Ready to export.", MessageType.Info);
                return;
            }

            for (var index = 0; index < validation.Issues.Count; index++)
            {
                EditorGUILayout.HelpBox(validation.Issues[index].Message, MessageType.Error);
            }
        }

        private void DrawExportControls()
        {
            RuntimeHexMap map;
            HexLayout exportLayout;
            if (!TryCreatePreviewData(out map, out exportLayout)) return;

            GvgMapAuthoringAsset boundAsset;
            if (!TryResolveHealthyBinding(out boundAsset))
            {
                EditorGUILayout.HelpBox("Bind the scene map to the authoring asset to enable scene-topology import/export.", MessageType.Warning);
                return;
            }

            if (GUILayout.Button("Import Excel"))
            {
                var path = EditorUtility.OpenFilePanel("Import GVG Map Excel", string.Empty, "xlsx");
                if (!string.IsNullOrEmpty(path))
                {
                    var result = GvgMapExcelImporter.Import(path, m_Asset, map);
                    if (result.Applied)
                    {
                        EditorUtility.SetDirty(m_Asset);
                        SceneView.RepaintAll();
                        Repaint();
                        EditorUtility.DisplayDialog("GVG Map Import", "Imported " + result.ImportedRowCount + " rows.", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("GVG Map Import Failed", string.Join(Environment.NewLine, result.Errors.ToArray()), "OK");
                    }
                }
            }

            if (GUILayout.Button("Export Excel (.xlsx)"))
            {
                try
                {
                    var path = EditorUtility.SaveFilePanel(
                        "Export GVG Map Excel",
                        string.Empty,
                        "GVGMap_" + m_Asset.MapId + ".xlsx",
                        "xlsx");
                    if (string.IsNullOrEmpty(path)) return;

                    var exportedPath = GvgMapExcelExporter.ExportExcelToFile(m_Asset, map, path);
                    EditorUtility.DisplayDialog("GVG Map Export", "Exported to " + exportedPath, "OK");
                }
                catch (Exception exception)
                {
                    EditorUtility.DisplayDialog("GVG Map Export Failed", exception.Message, "OK");
                }
            }

            if (GUILayout.Button("Export CSV"))
            {
                try
                {
                    var path = EditorUtility.SaveFilePanel(
                        "Export GVG Map CSV",
                        string.Empty,
                        "GVGMap_" + m_Asset.MapId + ".csv",
                        "csv");
                    if (string.IsNullOrEmpty(path)) return;

                    var exportedPath = GvgMapAuthoringExporter.ExportToFile(m_Asset, map, path);
                    EditorUtility.DisplayDialog("GVG Map Export", "Exported to " + exportedPath, "OK");
                }
                catch (Exception exception)
                {
                    EditorUtility.DisplayDialog("GVG Map Export Failed", exception.Message, "OK");
                }
            }
        }
        private void OnSceneGui(SceneView sceneView)
        {
            ResolveMapView();

            GvgMapAuthoringAsset boundAsset;
            if (!TryResolveHealthyBinding(out boundAsset))
            {
                return;
            }

            // SceneView editing always targets the bound asset.
            if (m_Asset != boundAsset)
            {
                m_Asset = boundAsset;
                ClearSelection();
            }

            if (!m_SceneEditing) return;

            RuntimeHexMap map;
            HexLayout layout;
            if (!TryCreatePreviewData(out map, out layout)) return;

            ClaimSceneViewInput(map, layout);
            var plotsByHexId = GvgMapAuthoringUtility.CreatePlotLookup(m_Asset, false);
            DrawCells(map, layout, plotsByHexId);
            HandleSceneInput(sceneView, map, layout);
        }

        private void ClaimSceneViewInput(RuntimeHexMap map, HexLayout layout)
        {
            var current = Event.current;
            if (current == null || current.alt || current.type != EventType.Layout)
            {
                return;
            }

            HexCell cell;
            if (!TryGetMouseCell(current, layout, map, out cell))
            {
                return;
            }

            HandleUtility.AddDefaultControl(
                GUIUtility.GetControlID(FocusType.Passive));
        }

        private void DrawCells(RuntimeHexMap map, HexLayout layout, Dictionary<int, GvgPlotAuthoringData> plotsByHexId)
        {
            foreach (var cell in map.Cells)
            {
                GvgPlotAuthoringData plot;
                var hasPlot = plotsByHexId.TryGetValue(cell.Id, out plot);
                var fill = hasPlot ? ColorFor(plot.PlotType) : new Color(1f, 0f, 1f, 0.55f);
                if (hasPlot && m_HasSelectedPlot && plot.PlotId == m_SelectedPlotId)
                {
                    fill = Color.Lerp(fill, Color.white, 0.35f);
                }
                if (m_SelectedHexIds.Contains(cell.Id))
                {
                    fill = Color.Lerp(fill, Color.yellow, 0.45f);
                }

                var isAffiliated = hasPlot && plot.AffiliatedCampId != Plot.NoAffiliatedCampId;
                DrawHex(
                    layout,
                    cell.Coordinate,
                    fill,
                    hasPlot && GetInitialPlotState(plot) == PlotState.NotOpen,
                    isAffiliated);
                Handles.Label(MapPointToWorld(layout.HexToWorld(cell.Coordinate)), FormatLabel(cell, plot, hasPlot), m_LabelStyle);
            }
        }

        private void HandleSceneInput(SceneView sceneView, RuntimeHexMap map, HexLayout layout)
        {
            var current = Event.current;
            if (current == null || current.alt || current.button != 0) return;
            if (current.type != EventType.MouseDown && current.type != EventType.MouseDrag) return;

            HexCell cell;
            if (!TryGetMouseCell(current, layout, map, out cell)) return;

            if (m_Mode == ToolMode.SelectPlot && current.type == EventType.MouseDown)
            {
                var plot = FindPlotContainingHex(cell.Id);
                if (plot != null)
                {
                    var additive = current.shift || current.control || current.command;
                    if (!additive || !m_HasSelectedPlot)
                    {
                        m_HasSelectedPlot = true;
                        m_SelectedPlotId = plot.PlotId;
                    }

                    if (!additive)
                    {
                        m_SelectedHexIds.Clear();
                        m_SelectedHexIds.Add(cell.Id);
                    }
                    else
                    {
                        ToggleSelectedHex(cell.Id);
                    }

                    Repaint();
                }
            }
            else if (m_HasSelectedPlot && m_Mode == ToolMode.PaintAdd)
            {
                RecordAndApply("Paint Add GVG Hex", () => GvgMapAuthoringUtility.TryPaintAdd(m_Asset, map, m_SelectedPlotId, cell.Id));
                SelectPlotContaining(cell.Id);
            }
            else if (m_HasSelectedPlot && m_Mode == ToolMode.PaintRemove)
            {
                RecordAndApply("Paint Remove GVG Hex", () => GvgMapAuthoringUtility.TryPaintRemove(m_Asset, map, m_SelectedPlotId, cell.Id));
                if (FindSelectedPlot() == null)
                {
                    SelectPlotContaining(cell.Id);
                }
            }

            current.Use();
            sceneView.Repaint();
        }

        private bool TryCreatePreviewData(out RuntimeHexMap map, out HexLayout layout)
        {
            if (m_MapView == null)
            {
                map = null;
                layout = default(HexLayout);
                m_MapError = "No scene HexMapView is assigned.";
                return false;
            }

            string error;
            if (!m_MapView.TryCreateSnapshots(out map, out layout, out error))
            {
                m_MapError = error;
                return false;
            }

            m_MapError = string.Empty;
            return true;
        }

        private bool TryGetMouseCell(Event current, HexLayout layout, RuntimeHexMap map, out HexCell cell)
        {
            if (m_MapView == null)
            {
                cell = default(HexCell);
                return false;
            }

            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            float distance;
            if (!m_MapView.WorldPlane.Raycast(ray, out distance))
            {
                cell = default(HexCell);
                return false;
            }

            var localPoint = m_MapView.WorldToMapLocal(ray.GetPoint(distance));
            var coordinate = layout.WorldToHex(localPoint);
            return map.TryGetCell(coordinate, out cell);
        }

        private void DrawHex(HexLayout layout, HexCoord coordinate, Color fill, bool isNotOpen, bool isAffiliated)
        {
            var center = layout.HexToWorld(coordinate);
            var corners = new Vector3[6];
            var outline = new Vector3[7];
            var angleOffset = layout.Orientation == HexOrientation.Pointy ? 30f : 0f;
            for (var index = 0; index < 6; index++)
            {
                var angle = (angleOffset + index * 60f) * Mathf.Deg2Rad;
                var x = Mathf.Cos(angle) * layout.OuterRadius;
                var secondary = Mathf.Sin(angle) * layout.OuterRadius * layout.SecondaryScale;
                var corner = layout.Plane == HexPlane.XY
                    ? center + new Vector3(x, secondary, 0f)
                    : center + new Vector3(x, 0f, secondary);
                corners[index] = MapPointToWorld(corner);
                outline[index] = MapPointToWorld(corner);
            }

            outline[6] = outline[0];
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(corners);
            Handles.color = Color.black;
            Handles.DrawAAPolyLine(1f, outline);
            if (isNotOpen)
            {
                DrawNotOpenOverlay(layout, center, outline);
            }
            if (isAffiliated)
            {
                Handles.color = m_AffiliatedPlotOutlineColor;
                Handles.DrawAAPolyLine(3.5f, outline);
            }
        }

        private void DrawNotOpenOverlay(HexLayout layout, Vector3 center, Vector3[] outline)
        {
            Handles.color = m_NotOpenOverlayColor;
            Handles.DrawAAPolyLine(2.5f, outline);

            var horizontalRadius = layout.OuterRadius * 0.42f;
            var verticalRadius = layout.OuterRadius * layout.SecondaryScale * 0.42f;
            var firstLine = new Vector3[2];
            var secondLine = new Vector3[2];
            if (layout.Plane == HexPlane.XY)
            {
                firstLine[0] = MapPointToWorld(center + new Vector3(-horizontalRadius, -verticalRadius, 0f));
                firstLine[1] = MapPointToWorld(center + new Vector3(horizontalRadius, verticalRadius, 0f));
                secondLine[0] = MapPointToWorld(center + new Vector3(-horizontalRadius, verticalRadius, 0f));
                secondLine[1] = MapPointToWorld(center + new Vector3(horizontalRadius, -verticalRadius, 0f));
            }
            else
            {
                firstLine[0] = MapPointToWorld(center + new Vector3(-horizontalRadius, 0f, -verticalRadius));
                firstLine[1] = MapPointToWorld(center + new Vector3(horizontalRadius, 0f, verticalRadius));
                secondLine[0] = MapPointToWorld(center + new Vector3(-horizontalRadius, 0f, verticalRadius));
                secondLine[1] = MapPointToWorld(center + new Vector3(horizontalRadius, 0f, -verticalRadius));
            }

            Handles.DrawAAPolyLine(2f, firstLine);
            Handles.DrawAAPolyLine(2f, secondLine);
        }
        private string FormatLabel(HexCell cell, GvgPlotAuthoringData plot, bool hasPlot)
        {
            if (!hasPlot) return "Unassigned" + Environment.NewLine + "#" + cell.Id;
            var label = plot.PlotId.ToString();
            if (m_ShowHexIds) label += Environment.NewLine + "#" + cell.Id;
            if (m_ShowCoordinates)
            {
                label += Environment.NewLine + "(" + cell.Coordinate.Q + "," + cell.Coordinate.R + ")";
            }

            if (m_ShowTypes)
            {
                label += Environment.NewLine + plot.PlotType;
                label += Environment.NewLine + "State: " + GetInitialPlotState(plot);
                label += Environment.NewLine + "[" + plot.Start + "," + plot.End + ")";
                label += Environment.NewLine +
                    (plot.AffiliatedCampId == Plot.NoAffiliatedCampId
                        ? "Affiliation: None"
                        : "Affiliation: Camp " + plot.AffiliatedCampId);
            }

            return label;
        }
        private static PlotState GetInitialPlotState(GvgPlotAuthoringData plot)
        {
            return plot.Start > 0 ? PlotState.NotOpen : PlotState.Open;
        }

        private static GUIStyle CreateLabelStyle()
        {
            var style = new GUIStyle();
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            return style;
        }

        private static Color ColorFor(PlotType plotType)
        {
            switch (plotType)
            {
                case PlotType.Obstacle: return new Color(0.15f, 0.15f, 0.15f, 0.65f);
                case PlotType.Camp: return new Color(0.1f, 0.45f, 1f, 0.55f);
                case PlotType.SmallCity: return new Color(0.95f, 0.65f, 0.15f, 0.55f);
                case PlotType.BigCity: return new Color(0.95f, 0.35f, 0.15f, 0.6f);
                case PlotType.Capital: return new Color(0.9f, 0.1f, 0.25f, 0.65f);
                case PlotType.Grass: return new Color(0.15f, 0.65f, 0.25f, 0.45f);
                default: return new Color(0.45f, 0.45f, 0.5f, 0.35f);
            }
        }

        private static readonly Color m_NotOpenOverlayColor = new Color(1f, 0.82f, 0.1f, 0.95f);
        private static readonly Color m_AffiliatedPlotOutlineColor = new Color(0.85f, 0.15f, 1f, 0.95f);

        private void CreateAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create GVG Map Authoring Asset",
                "GvgMapAuthoring",
                "asset",
                "Choose where to save the GVG map authoring asset.");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<GvgMapAuthoringAsset>();
            RuntimeHexMap map;
            HexLayout layout;
            if (!TryCreatePreviewData(out map, out layout))
            {
                UnityEngine.Object.DestroyImmediate(asset);
                EditorUtility.DisplayDialog("Create GVG Map Authoring Asset", "A valid scene HexMapView is required.", "OK");
                return;
            }
            GvgMapAuthoringUtility.ResetToDefaultPlots(asset, map);
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create GVG Map Authoring Asset");
            AssetDatabase.SaveAssets();
            m_Asset = asset;
            Selection.activeObject = asset;
        }

        private HexMapView ResolveMapView()
        {
            if (m_MapView != null) return m_MapView;

            m_MapView = ResolveSceneMapView();
            return m_MapView;
        }

        /// <summary>
        /// Resolves the scene's HexMapView without mutating the window: selection first,
        /// then a single scene view. Returns null when ambiguous or absent.
        /// </summary>
        private HexMapView ResolveSceneMapView()
        {
            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                var fromSelection = selected.GetComponentInParent<HexMapView>();
                if (fromSelection != null) return fromSelection;
            }

            var views = UnityEngine.Object.FindObjectsOfType<HexMapView>();
            if (views.Length == 1) return views[0];
            return null;
        }

        /// <summary>
        /// Initializes the window from the current scene's HexMapView when opening via
        /// the Tools menu: adopts the view's configuration and, when the view is bound,
        /// loads its authoring asset so the window is immediately usable.
        /// </summary>
        private void InitializeFromScene()
        {
            m_MapView = ResolveSceneMapView();
            if (m_MapView == null)
            {
                return;
            }

            var boundAsset = m_MapView.GvgMapAuthoringAssetEditorOnly;
            if (boundAsset != null && m_Asset != boundAsset)
            {
                m_Asset = boundAsset;
                ClearSelection();
            }
        }

        /// <summary>
        /// Resolves the bound authoring asset for the current scene map and refreshes
        /// m_BindingDiagnostic so the window can explain why scene editing is off.
        /// Read-only; never mutates the view or the asset.
        /// </summary>
        private bool TryResolveHealthyBinding(out GvgMapAuthoringAsset boundAsset)
        {
            boundAsset = null;
            if (m_MapView == null)
            {
                m_BindingDiagnostic = GvgMapBindingDiagnostic.ViewMissing;
                return false;
            }

            GvgMapBindingDiagnostic diagnostic;
            if (!GvgMapBindingResolver.TryResolve(m_MapView, out boundAsset, out diagnostic))
            {
                m_BindingDiagnostic = diagnostic;
                return false;
            }

            m_BindingDiagnostic = GvgMapBindingDiagnostic.None;
            return true;
        }

        private static string DescribeBinding(GvgMapBindingDiagnostic diagnostic)
        {
            switch (diagnostic)
            {
                case GvgMapBindingDiagnostic.None:
                    return "Binding is healthy. Scene editing is enabled.";
                case GvgMapBindingDiagnostic.ViewMissing:
                    return "No scene HexMapView found. Select a map root or child object.";
                case GvgMapBindingDiagnostic.AssetMissing:
                    return "Bound authoring asset is missing or broken. Rebind it in the HexMapView inspector.";
                case GvgMapBindingDiagnostic.TopologyMismatch:
                    return "Bound asset topology (max HexId) does not match the map radius.";
                case GvgMapBindingDiagnostic.NotBound:
                    return "Map is not bound. Bind the authoring asset in the HexMapView inspector to enable scene editing.";
                default:
                    return string.Empty;
            }
        }

        private Vector3 MapPointToWorld(Vector3 mapLocalPoint)
        {
            return m_MapView == null ? mapLocalPoint : m_MapView.transform.TransformPoint(mapLocalPoint);
        }
        private void RecordAndApply(string undoName, Func<bool> action)
        {
            Undo.RecordObject(m_Asset, undoName);
            if (action())
            {
                EditorUtility.SetDirty(m_Asset);
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void ClearSelection()
        {
            m_HasSelectedPlot = false;
            m_SelectedPlotId = 0;
            m_SelectedHexIds.Clear();
        }

        private GvgPlotAuthoringData FindSelectedPlot()
        {
            if (!m_HasSelectedPlot || m_Asset == null) return null;
            for (var index = 0; index < m_Asset.Plots.Count; index++)
            {
                var plot = m_Asset.Plots[index];
                if (plot != null && plot.PlotId == m_SelectedPlotId) return plot;
            }

            return null;
        }

        private void ToggleSelectedHex(int hexId)
        {
            if (m_SelectedHexIds.Contains(hexId))
            {
                m_SelectedHexIds.Remove(hexId);
            }
            else
            {
                m_SelectedHexIds.Add(hexId);
            }
        }

        private void SelectPlotContaining(int hexId)
        {
            var plot = FindPlotContainingHex(hexId);
            if (plot == null) return;
            m_HasSelectedPlot = true;
            m_SelectedPlotId = plot.PlotId;
        }

        private GvgPlotAuthoringData FindPlotContainingHex(int hexId)
        {
            if (m_Asset == null) return null;
            for (var index = 0; index < m_Asset.Plots.Count; index++)
            {
                var plot = m_Asset.Plots[index];
                if (plot != null && plot.HexIds != null && plot.HexIds.Contains(hexId)) return plot;
            }

            return null;
        }

    }
}

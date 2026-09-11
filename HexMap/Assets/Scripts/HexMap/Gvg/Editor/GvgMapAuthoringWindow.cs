using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Gvg.Authoring;
using HexMap.Runtime;
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
            GetWindow<GvgMapAuthoringWindow>("GVG Map Authoring");
        }

        private void OnEnable()
        {
            m_LabelStyle = CreateLabelStyle();
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
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
            EditorGUILayout.LabelField("Map", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var mapId = EditorGUILayout.TextField("Map Id", m_Asset.MapId);
            var radius = EditorGUILayout.IntField("Radius", m_Asset.Radius);
            var orientation = (HexOrientation)EditorGUILayout.EnumPopup("Orientation", m_Asset.Orientation);
            var plane = (HexPlane)EditorGUILayout.EnumPopup("Plane", m_Asset.Plane);
            var outerRadius = EditorGUILayout.FloatField("Outer Radius", m_Asset.OuterRadius);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_Asset, "Edit GVG Map Settings");
                m_Asset.MapId = mapId;
                if (radius != m_Asset.Radius && EditorUtility.DisplayDialog(
                    "Rebuild Radius",
                    "Changing radius rebuilds the map coverage. Hexes outside the new radius are removed and new hexes get default single-cell Plots.",
                    "Apply",
                    "Cancel"))
                {
                    GvgMapAuthoringUtility.RebuildRadius(m_Asset, radius);
                }

                m_Asset.Orientation = orientation;
                m_Asset.Plane = plane;
                if (outerRadius > 0f) m_Asset.OuterRadius = outerRadius;
                EditorUtility.SetDirty(m_Asset);
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Default Plots"))
            {
                Undo.RecordObject(m_Asset, "Reset GVG Map Plots");
                GvgMapAuthoringUtility.ResetToDefaultPlots(m_Asset);
                ClearSelection();
                EditorUtility.SetDirty(m_Asset);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Repair Coverage"))
            {
                Undo.RecordObject(m_Asset, "Repair GVG Map Coverage");
                GvgMapAuthoringUtility.RepairForCurrentRadius(m_Asset);
                EditorUtility.SetDirty(m_Asset);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolControls()
        {
            EditorGUILayout.LabelField("Scene Tool", EditorStyles.boldLabel);
            m_Mode = (ToolMode)GUILayout.Toolbar((int)m_Mode, new[] { "Select Plot", "Paint Add", "Paint Remove" });
            m_ShowHexIds = EditorGUILayout.Toggle("Show HexId", m_ShowHexIds);
            m_ShowCoordinates = EditorGUILayout.Toggle("Show Coordinates", m_ShowCoordinates);
            m_ShowTypes = EditorGUILayout.Toggle("Show Type", m_ShowTypes);
            EditorGUILayout.LabelField("Selected Hexes", string.Join(",", m_SelectedHexIds.ConvertAll(value => value.ToString()).ToArray()));
        }

        private void DrawSelectedPlotControls()
        {
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
            var plotType = (PlotType)EditorGUILayout.EnumPopup("Plot Type", plot.PlotType);
            if (EditorGUI.EndChangeCheck())
            {
                if (plotType == PlotType.Obstacle && plot.GenerationType == PlotGenerationType.TimedOpen)
                {
                    EditorUtility.DisplayDialog(
                        "Invalid Plot Configuration",
                        "Obstacle Plots cannot use TimedOpen generation.",
                        "OK");
                }
                else
                {
                    Undo.RecordObject(m_Asset, "Edit GVG Plot Type");
                    plot.PlotType = plotType;
                    EditorUtility.SetDirty(m_Asset);
                    SceneView.RepaintAll();
                }
            }

            EditorGUI.BeginChangeCheck();
            var generationType = (PlotGenerationType)EditorGUILayout.EnumPopup(
                "Generation Type",
                plot.GenerationType);
            if (EditorGUI.EndChangeCheck())
            {
                if (plotType == PlotType.Obstacle && generationType == PlotGenerationType.TimedOpen)
                {
                    EditorUtility.DisplayDialog(
                        "Invalid Plot Configuration",
                        "Obstacle Plots cannot use TimedOpen generation.",
                        "OK");
                }
                else
                {
                    Undo.RecordObject(m_Asset, "Edit GVG Plot Generation Type");
                    plot.GenerationType = generationType;
                    EditorUtility.SetDirty(m_Asset);
                    SceneView.RepaintAll();
                }
            }

            var initialState = generationType == PlotGenerationType.Initial
                ? PlotState.Open
                : PlotState.NotOpen;
            var initialPassable = initialState == PlotState.Open && plot.PlotType != PlotType.Obstacle;
            EditorGUILayout.LabelField("Initial Runtime State", initialState.ToString());
            EditorGUILayout.LabelField("Initial Passable", initialPassable ? "Yes" : "No");
            EditorGUILayout.LabelField(
                "Initial Capturable",
                initialState == PlotState.Open && plot.PlotType != PlotType.Camp ? "Yes" : "No");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Merge Selected"))
            {
                RecordAndApply("Merge GVG Plots", () => GvgMapAuthoringUtility.TryMergeToMultiPlot(m_Asset, plot.PlotId, m_SelectedHexIds));
            }
            if (GUILayout.Button("Delete Plot"))
            {
                RecordAndApply("Delete GVG Plot", () => GvgMapAuthoringUtility.TryDeletePlot(m_Asset, plot.PlotId));
                ClearSelection();
            }
            EditorGUILayout.EndHorizontal();

            m_PastedHexIds = EditorGUILayout.TextField("Paste HexIds", m_PastedHexIds);
            if (GUILayout.Button("Merge Pasted HexIds"))
            {
                RecordAndApply("Paste GVG HexIds", () => GvgMapAuthoringUtility.TryPasteHexIdsToPlot(m_Asset, plot.PlotId, m_PastedHexIds));
            }
        }

        private void DrawValidationControls()
        {
            var validation = GvgMapAuthoringUtility.Validate(m_Asset);
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
            if (!GUILayout.Button("Export CSV")) return;
            try
            {
                var path = GvgMapAuthoringExporter.Export(m_Asset);
                EditorUtility.DisplayDialog("GVG Map Export", "Exported to " + path, "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("GVG Map Export Failed", exception.Message, "OK");
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (m_Asset == null) return;

            RuntimeHexMap map;
            HexLayout layout;
            if (!TryCreatePreviewData(out map, out layout)) return;

            var plotsByHexId = GvgMapAuthoringUtility.CreatePlotLookup(m_Asset, false);
            DrawCells(map, layout, plotsByHexId);
            HandleSceneInput(sceneView, map, layout);
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

                DrawHex(layout, cell.Coordinate, fill);
                Handles.Label(layout.HexToWorld(cell.Coordinate), FormatLabel(cell, plot, hasPlot), m_LabelStyle);
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
                RecordAndApply("Paint Add GVG Hex", () => GvgMapAuthoringUtility.TryPaintAdd(m_Asset, m_SelectedPlotId, cell.Id));
                SelectPlotContaining(cell.Id);
            }
            else if (m_HasSelectedPlot && m_Mode == ToolMode.PaintRemove)
            {
                RecordAndApply("Paint Remove GVG Hex", () => GvgMapAuthoringUtility.TryPaintRemove(m_Asset, m_SelectedPlotId, cell.Id));
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
            try
            {
                map = m_Asset.CreateRuntimeMap();
                layout = new HexLayout(m_Asset.Orientation, m_Asset.Plane, m_Asset.OuterRadius, Vector3.zero);
                return true;
            }
            catch (Exception)
            {
                map = null;
                layout = default(HexLayout);
                return false;
            }
        }

        private bool TryGetMouseCell(Event current, HexLayout layout, RuntimeHexMap map, out HexCell cell)
        {
            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            var plane = m_Asset.Plane == HexPlane.XY
                ? new Plane(Vector3.forward, Vector3.zero)
                : new Plane(Vector3.up, Vector3.zero);
            float distance;
            if (!plane.Raycast(ray, out distance))
            {
                cell = default(HexCell);
                return false;
            }

            var coordinate = layout.WorldToHex(ray.GetPoint(distance));
            return map.TryGetCell(coordinate, out cell);
        }

        private void DrawHex(HexLayout layout, HexCoord coordinate, Color fill)
        {
            var center = layout.HexToWorld(coordinate);
            var corners = new Vector3[6];
            var outline = new Vector3[7];
            var angleOffset = layout.Orientation == HexOrientation.Pointy ? 30f : 0f;
            for (var index = 0; index < 6; index++)
            {
                var angle = (angleOffset + index * 60f) * Mathf.Deg2Rad;
                var x = Mathf.Cos(angle) * layout.OuterRadius;
                var secondary = Mathf.Sin(angle) * layout.OuterRadius;
                var corner = layout.Plane == HexPlane.XY
                    ? center + new Vector3(x, secondary, 0f)
                    : center + new Vector3(x, 0f, secondary);
                corners[index] = corner;
                outline[index] = corner;
            }

            outline[6] = outline[0];
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(corners);
            Handles.color = Color.black;
            Handles.DrawAAPolyLine(1f, outline);
        }

        private string FormatLabel(HexCell cell, GvgPlotAuthoringData plot, bool hasPlot)
        {
            if (!hasPlot) return "Unassigned\n#" + cell.Id;
            var label = plot.PlotId.ToString();
            if (m_ShowHexIds) label += "\n#" + cell.Id;
            if (m_ShowCoordinates) label += "\n(" + cell.Coordinate.Q + "," + cell.Coordinate.R + ")";
            if (m_ShowTypes)
            {
                label += "\n" + plot.PlotType;
                label += "\n" + plot.GenerationType;
                if (plot.GenerationType == PlotGenerationType.TimedOpen)
                {
                    label += "\n" + PlotState.NotOpen;
                }
            }
            return label;
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

        private void CreateAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create GVG Map Authoring Asset",
                "GvgMapAuthoring",
                "asset",
                "Choose where to save the GVG map authoring asset.");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<GvgMapAuthoringAsset>();
            GvgMapAuthoringUtility.ResetToDefaultPlots(asset);
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create GVG Map Authoring Asset");
            AssetDatabase.SaveAssets();
            m_Asset = asset;
            Selection.activeObject = asset;
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

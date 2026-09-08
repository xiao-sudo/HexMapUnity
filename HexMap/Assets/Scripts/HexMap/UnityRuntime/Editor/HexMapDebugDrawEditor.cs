#if UNITY_EDITOR

using System;
using HexMap.Core;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEditor;
using UnityEngine;

namespace HexMap.UnityRuntime.Editor
{
    internal static class HexMapDebugDrawEditor
    {
        private const GizmoType m_DrawTypes = GizmoType.NonSelected | GizmoType.Selected;
        private const float m_BoundsLineWidth = 1f;
        private static readonly Color m_BoundsColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        private static readonly GUIStyle m_LabelStyle = CreateLabelStyle();

        [DrawGizmo(m_DrawTypes)]
        private static void DrawDebug(HexMapView view, GizmoType gizmoType)
        {
            if (view == null)
            {
                return;
            }

            RuntimeHexMap map;
            HexLayout layout;
            if (!TryGetPreviewData(view, out map, out layout))
            {
                return;
            }

            if (view.ShowDebugBounds)
            {
                DrawBounds(view, map, layout);
            }

            if (!view.ShowDebugLabels)
            {
                return;
            }

            DrawLabels(view, map, layout);
        }

        private static void DrawBounds(HexMapView view, RuntimeHexMap map, HexLayout layout)
        {
            var previousColor = Handles.color;
            Handles.color = m_BoundsColor;

            foreach (var cell in map.Cells)
            {
                var corners = CreateWorldCorners(view, layout, cell.Coordinate);
                Handles.DrawAAPolyLine(m_BoundsLineWidth, corners);
            }

            Handles.color = previousColor;
        }

        private static void DrawLabels(HexMapView view, RuntimeHexMap map, HexLayout layout)
        {
            m_LabelStyle.normal.textColor = view.LabelColor;

            foreach (var cell in map.Cells)
            {
                var localCenter = layout.HexToWorld(cell.Coordinate);
                var worldCenter = view.transform.TransformPoint(localCenter);
                Handles.Label(worldCenter, FormatLabel(cell, view.ShowDebugCoordinates), m_LabelStyle);
            }
        }

        private static Vector3[] CreateWorldCorners(
            HexMapView view,
            HexLayout layout,
            HexCoord coordinate)
        {
            var center = layout.HexToWorld(coordinate);
            var corners = new Vector3[7];
            var angleOffset = layout.Orientation == HexOrientation.Pointy ? 30f : 0f;

            for (var index = 0; index < 6; index++)
            {
                var angle = (angleOffset + index * 60f) * Mathf.Deg2Rad;
                var x = Mathf.Cos(angle) * layout.OuterRadius;
                var secondary = Mathf.Sin(angle) * layout.OuterRadius;
                var localCorner = layout.Plane == HexPlane.XY
                    ? center + new Vector3(x, secondary, 0f)
                    : center + new Vector3(x, 0f, secondary);
                corners[index] = view.transform.TransformPoint(localCorner);
            }

            corners[6] = corners[0];
            return corners;
        }

        private static GUIStyle CreateLabelStyle()
        {
            var style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        private static string FormatLabel(HexCell cell, bool showCoordinates)
        {
            return showCoordinates
                ? string.Format("#{0}\n ({1}, {2})", cell.Id, cell.Coordinate.Q, cell.Coordinate.R)
                : string.Format("#{0}", cell.Id);
        }

        private static bool TryGetPreviewData(
            HexMapView view,
            out RuntimeHexMap map,
            out HexLayout layout)
        {
            if (Application.isPlaying && view.Map != null)
            {
                map = view.Map;
                layout = view.Layout;
                return true;
            }

            try
            {
                var definition = new HexMapDefinition(view.Radius);
                map = new RuntimeHexMap(definition);
                layout = new HexLayout(
                    view.Orientation,
                    view.Plane,
                    view.OuterRadius,
                    view.Origin);
                return true;
            }
            catch (ArgumentException)
            {
                map = null;
                layout = default(HexLayout);
                return false;
            }
            catch (InvalidOperationException)
            {
                map = null;
                layout = default(HexLayout);
                return false;
            }
        }
    }
}

#endif

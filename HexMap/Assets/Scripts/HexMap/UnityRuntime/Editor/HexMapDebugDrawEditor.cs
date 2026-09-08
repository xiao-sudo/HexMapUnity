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

        [DrawGizmo(m_DrawTypes)]
        private static void DrawLabels(HexMapView view, GizmoType gizmoType)
        {
            if (view == null || !view.ShowDebugLabels)
            {
                return;
            }

            RuntimeHexMap map;
            HexLayout layout;
            if (!TryGetPreviewData(view, out map, out layout))
            {
                return;
            }

            foreach (var cell in map.Cells)
            {
                var localCenter = layout.HexToWorld(cell.Coordinate);
                var worldCenter = view.transform.TransformPoint(localCenter);
                Handles.Label(
                    worldCenter,
                    string.Format("Cell #{0}\nAxial ({1}, {2})", cell.Id, cell.Coordinate.Q, cell.Coordinate.R));
            }
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
                var definition = view.Config == null
                    ? new HexMapDefinition(3, new HexCoord[0])
                    : view.Config.CreateDefinition();
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

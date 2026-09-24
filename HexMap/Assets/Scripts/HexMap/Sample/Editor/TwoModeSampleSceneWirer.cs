using System.Collections.Generic;
using System.Text;
using HexMap.UnityRuntime;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HexMap.Sample.Editor
{
    /// <summary>
    /// Fills in the parts of a two-mode sample scene that no runtime component can create for itself: the
    /// click entry point, one panel per mode, the map gestures, and the references that tie them together.
    /// <para>
    /// It works on whatever scene is open and finds its targets by component rather than by name, so it can
    /// be run again after the scene changes: anything already there is reused, and it reports what it
    /// changed instead of rewriting the scene silently. Every change goes through <see cref="Undo"/>, so a
    /// mistaken run is one Ctrl+Z away.
    /// </para>
    /// <para>
    /// It deliberately does not touch cameras, their stacks or their render types. Those are scene
    /// decisions, and the point of the report is to say which ones still need a human look.
    /// </para>
    /// </summary>
    public static class TwoModeSampleSceneWirer
    {
        private const string ChangeLabel = "Wire two-mode sample scene";

        [MenuItem("Tools/Hex Map/Wire Two-Mode Sample Scene")]
        public static void Wire()
        {
            var changes = new List<string>();
            var notes = new List<string>();

            var switcher = FindSingle<MapViewModeSwitcher>();
            if (switcher == null)
            {
                Debug.LogError(
                    "Wire Two-Mode Scene: the open scene has no MapViewModeSwitcher, so there is nothing to wire to.");
                return;
            }

            var dispatcher = switcher.Dispatcher != null
                ? switcher.Dispatcher
                : FindSingle<MapClickDispatcher>();
            if (dispatcher == null)
            {
                Debug.LogError("Wire Two-Mode Scene: the open scene has no MapClickDispatcher.");
                return;
            }

            var mapCamera = switcher.MapCamera != null
                ? switcher.MapCamera
                : FindSingle<OrthographicMapCamera>();

            // The click entry point: without one nothing calls OnMapClicked, so the whole chain is dead.
            var tap = EnsureComponent<MapClickTapInput>(dispatcher.gameObject, changes);
            if (tap.Dispatcher != dispatcher)
            {
                Undo.RecordObject(tap, ChangeLabel);
                tap.Dispatcher = dispatcher;
                EditorUtility.SetDirty(tap);
                changes.Add("pointed MapClickTapInput at '" + dispatcher.name + "'");
            }

            // The gestures belong on the camera they drive, which is the one the switcher switches off.
            OrthographicMapDragInput drag = null;
            OrthographicMapZoomInput zoom = null;
            if (mapCamera == null)
            {
                notes.Add("no OrthographicMapCamera in the scene, so panning and zooming were not wired");
            }
            else
            {
                drag = EnsureComponent<OrthographicMapDragInput>(mapCamera.gameObject, changes);
                zoom = EnsureComponent<OrthographicMapZoomInput>(mapCamera.gameObject, changes);

                if (drag.MapCamera != mapCamera)
                {
                    Undo.RecordObject(drag, ChangeLabel);
                    drag.MapCamera = mapCamera;
                    EditorUtility.SetDirty(drag);
                    changes.Add("pointed OrthographicMapDragInput at '" + mapCamera.gameObject.name + "'");
                }

                if (zoom.MapCamera != mapCamera)
                {
                    Undo.RecordObject(zoom, ChangeLabel);
                    zoom.MapCamera = mapCamera;
                    EditorUtility.SetDirty(zoom);
                    changes.Add("pointed OrthographicMapZoomInput at '" + mapCamera.gameObject.name + "'");
                }
            }

            WireSwitcher(switcher, dispatcher, mapCamera, drag, zoom, changes);
            StartInGameplayMode(switcher, changes);

            WireModeButton(switcher, switcher.GameplayUiRoot, "gameplay", changes, notes);
            WireModeButton(switcher, switcher.TopDownUiRoot, "top-down", changes, notes);

            WirePanel(
                switcher.GameplayUiRoot,
                FindSingle<GameplayMapClickHandler>(),
                "Gameplay",
                "gameplay",
                changes,
                notes);
            WirePanel(
                switcher.TopDownUiRoot,
                FindSingle<TopDownMapClickHandler>(),
                "Map view",
                "top-down",
                changes,
                notes);

            NoteUiCameraUsage(notes);

            if (changes.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            var summary = new StringBuilder("Wire Two-Mode Scene: ");
            summary.Append(
                changes.Count == 0
                    ? "nothing to change, the scene was already wired."
                    : string.Join("; ", changes) + ".");
            if (notes.Count > 0)
            {
                summary.Append(" Check by hand: ").Append(string.Join("; ", notes)).Append('.');
            }

            Debug.Log(summary.ToString());
        }

        private static void WireSwitcher(
            MapViewModeSwitcher switcher,
            MapClickDispatcher dispatcher,
            OrthographicMapCamera mapCamera,
            OrthographicMapDragInput drag,
            OrthographicMapZoomInput zoom,
            List<string> changes)
        {
            Undo.RecordObject(switcher, ChangeLabel);

            if (switcher.Dispatcher != dispatcher)
            {
                switcher.Dispatcher = dispatcher;
                changes.Add("pointed the switcher at '" + dispatcher.name + "'");
            }

            if (mapCamera != null && switcher.MapCamera != mapCamera)
            {
                switcher.MapCamera = mapCamera;
                changes.Add("pointed the switcher at the map camera on '" + mapCamera.gameObject.name + "'");
            }

            if (drag != null && switcher.MapDragInput != drag)
            {
                switcher.MapDragInput = drag;
                changes.Add("gave the switcher the drag gesture to switch on and off");
            }

            if (zoom != null && switcher.MapZoomInput != zoom)
            {
                switcher.MapZoomInput = zoom;
                changes.Add("gave the switcher the zoom gesture to switch on and off");
            }

            EditorUtility.SetDirty(switcher);
        }

        /// <summary>
        /// Leaves the scene starting in gameplay mode. Starting in the map view applies the mode without
        /// taking a snapshot, so the first return has nothing to restore and the camera stays wherever the
        /// scene left it, which looks like a broken restore the first time it is tried.
        /// </summary>
        private static void StartInGameplayMode(MapViewModeSwitcher switcher, List<string> changes)
        {
            var serialized = new SerializedObject(switcher);
            var property = serialized.FindProperty("m_IsTopDown");
            if (property == null)
            {
                changes.Add("could not find the starting mode field; check the mode flag by hand");
                return;
            }

            if (!property.boolValue)
            {
                return;
            }

            property.boolValue = false;
            serialized.ApplyModifiedProperties();
            changes.Add("start in gameplay mode, so the first switch has a snapshot to restore");
        }

        private static void WireModeButton(
            MapViewModeSwitcher switcher,
            GameObject root,
            string mode,
            List<string> changes,
            List<string> notes)
        {
            if (root == null)
            {
                notes.Add("the switcher has no " + mode + " UI root, so its button was not wired");
                return;
            }

            var button = root.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                notes.Add("'" + root.name + "' has no Button, so nothing can switch the mode back");
                return;
            }

            for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == switcher
                    && button.onClick.GetPersistentMethodName(i) == nameof(MapViewModeSwitcher.Toggle))
                {
                    return;
                }
            }

            Undo.RecordObject(button, ChangeLabel);
            UnityEventTools.AddPersistentListener(button.onClick, switcher.Toggle);
            EditorUtility.SetDirty(button);
            changes.Add("wired the button in '" + root.name + "' to Toggle()");
        }

        private static void WirePanel(
            GameObject root,
            MonoBehaviour source,
            string title,
            string mode,
            List<string> changes,
            List<string> notes)
        {
            if (root == null)
            {
                return;
            }

            if (source == null)
            {
                notes.Add("no " + mode + " handler in the scene, so that mode's panel was not created");
                return;
            }

            var panel = FindPanel(root, source);
            if (panel == null)
            {
                CreatePanel(root, source, title, changes);
                return;
            }

            // A panel from an earlier run only needs its references checked: it exists, so it is the one
            // the scene wants rather than a duplicate.
            if (panel.Source != source)
            {
                Undo.RecordObject(panel, ChangeLabel);
                panel.Source = source;
                EditorUtility.SetDirty(panel);
                changes.Add("pointed the " + mode + " panel at its handler");
            }
        }

        private static MapClickPanel FindPanel(GameObject root, MonoBehaviour source)
        {
            var panels = root.GetComponentsInChildren<MapClickPanel>(true);
            for (var i = 0; i < panels.Length; i++)
            {
                if (panels[i].Source == source)
                {
                    return panels[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Builds a panel that opens on a plot: an empty host carrying the component, a content child it
        /// shows and hides, and a label inside that. The content has to be a child rather than the host,
        /// because the host has to stay active to keep its subscription to the handler's event.
        /// </summary>
        private static void CreatePanel(GameObject root, MonoBehaviour source, string title, List<string> changes)
        {
            var panelObject = new GameObject(title + " Panel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panelObject, ChangeLabel);
            var panelRect = (RectTransform)panelObject.transform;
            panelRect.SetParent(root.transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 220f);
            panelRect.sizeDelta = new Vector2(600f, 320f);

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(contentObject, ChangeLabel);
            var contentRect = (RectTransform)contentObject.transform;
            contentRect.SetParent(panelRect, false);
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(labelObject, ChangeLabel);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(contentRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 16f);
            labelRect.offsetMax = new Vector2(-16f, -16f);

            var label = Undo.AddComponent<TextMeshProUGUI>(labelObject);
            label.text = title;
            label.fontSize = 64f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            var panel = Undo.AddComponent<MapClickPanel>(panelObject);
            panel.Source = source;
            panel.Content = contentObject;
            panel.Label = label;
            panel.Title = title;
            EditorUtility.SetDirty(panel);

            changes.Add("created the " + title + " panel under '" + root.name + "'");
        }

        /// <summary>
        /// Reports the one thing this tool cannot decide: a Screen Space - Overlay canvas renders without
        /// any camera, so the scene would never exercise the UI camera moving between stacks.
        /// </summary>
        private static void NoteUiCameraUsage(List<string> notes)
        {
            var canvas = FindSingle<Canvas>();
            if (canvas == null)
            {
                notes.Add("the scene has no Canvas, so there is no UI to show per mode");
                return;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                notes.Add(
                    "'" + canvas.name + "' is Screen Space - Overlay, so the UI does not depend on a camera stack at all; "
                        + "switch it to Screen Space - Camera with the UI camera as its Render Camera to exercise that");
            }
        }

        private static T EnsureComponent<T>(GameObject host, List<string> changes)
            where T : Component
        {
            var existing = host.GetComponent<T>();
            if (existing != null)
            {
                return existing;
            }

            var created = Undo.AddComponent<T>(host);
            changes.Add("added " + typeof(T).Name + " to '" + host.name + "'");
            return created;
        }

        private static T FindSingle<T>()
            where T : Component
        {
            var found = Object.FindObjectsOfType<T>(true);
            if (found.Length == 0)
            {
                return null;
            }

            if (found.Length > 1)
            {
                Debug.LogWarning(
                    "Wire Two-Mode Scene: the scene has " + found.Length + " " + typeof(T).Name
                        + " components; using '" + found[0].name + "'.");
            }

            return found[0];
        }
    }
}

using System.Collections.Generic;
using System.Text;
using HexMap.UnityRuntime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HexMap.Sample.Editor
{
    /// <summary>
    /// Fills in the parts of a two-mode sample scene that no runtime component can create for itself: the
    /// one panel per mode and the references that tie them together. Map gestures are created privately by
    /// <see cref="MapViewRuntimeFacade"/> rather than attached to a scene object.
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

            var host = FindSingle<SampleMapViewRuntimeHost>();
            if (host == null)
            {
                Debug.LogError(
                    "Wire Two-Mode Scene: the open scene has no SampleMapViewRuntimeHost, so there is nothing to wire.");
                return;
            }

            var dispatcher = host.Dispatcher != null
                ? host.Dispatcher
                : FindSingle<MapInputDispatcher>();
            if (dispatcher == null)
            {
                Debug.LogError("Wire Two-Mode Scene: the open scene has no MapInputDispatcher.");
                return;
            }

            var mapCamera = host.MapCamera != null ? host.MapCamera : FindSingle<OrthographicMapCamera>();
            var presenter = FindSingle<SampleMapViewModeUiPresenter>();
            if (presenter == null)
            {
                Debug.LogError("Wire Two-Mode Scene: the open scene has no SampleMapViewModeUiPresenter.");
                return;
            }

            // Camera input is routed by the dispatcher and consumed only by the active TopDown handler.
            // The scene still needs a camera for mode wiring, but no longer needs direct Unity Input
            // adapters on that camera.
            if (mapCamera == null)
            {
                notes.Add("no OrthographicMapCamera in the scene, so TopDown camera input cannot be consumed");
            }

            WireHost(host, mapCamera, dispatcher, changes);

            var gameplayHandler = FindSingle<GameplayMapInputHandler>();
            var topDownHandler = FindSingle<TopDownMapInputHandler>();
            WireInputHandlers(host, gameplayHandler, topDownHandler, changes, notes);

            WirePanel(
                presenter.GameplayUiRoot,
                gameplayHandler,
                "Gameplay",
                "gameplay",
                changes,
                notes);
            WirePanel(
                presenter.TopDownUiRoot,
                topDownHandler,
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

        private static void WireHost(
            SampleMapViewRuntimeHost host,
            OrthographicMapCamera mapCamera,
            MapInputDispatcher dispatcher,
            List<string> changes)
        {
            if (host.MapCamera == mapCamera
                && host.Dispatcher == dispatcher)
            {
                return;
            }

            Undo.RecordObject(host, ChangeLabel);
            host.Configure(
                host.GameplayCamera,
                mapCamera,
                dispatcher,
                host.GameplayHandler,
                host.TopDownHandler,
                host.InitialMode,
                host.PinchDeadZoneNormalized);
            EditorUtility.SetDirty(host);
            changes.Add("wired SampleMapViewRuntimeHost on '" + host.name + "'");
        }

        private static void WireInputHandlers(
            SampleMapViewRuntimeHost host,
            GameplayMapInputHandler gameplayHandler,
            TopDownMapInputHandler topDownHandler,
            List<string> changes,
            List<string> notes)
        {
            if (gameplayHandler == null || topDownHandler == null)
            {
                notes.Add("both GameplayMapInputHandler and TopDownMapInputHandler are required before the Map view runtime can start");
                return;
            }

            if (host.GameplayHandler == gameplayHandler && host.TopDownHandler == topDownHandler)
            {
                return;
            }

            Undo.RecordObject(host, ChangeLabel);
            host.Configure(
                host.GameplayCamera,
                host.MapCamera,
                host.Dispatcher,
                gameplayHandler,
                topDownHandler,
                host.InitialMode,
                host.PinchDeadZoneNormalized);
            EditorUtility.SetDirty(host);
            changes.Add("wired Map view primary input handlers on '" + host.name + "'");
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
                notes.Add("no " + mode + " UI root is configured on SampleMapViewModeUiPresenter");
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
                Debug.LogError(
                    "Wire Two-Mode Scene: the scene has " + found.Length + " " + typeof(T).Name
                        + " components. Resolve the duplicate configuration before wiring.");
                return null;
            }

            return found[0];
        }

    }
}

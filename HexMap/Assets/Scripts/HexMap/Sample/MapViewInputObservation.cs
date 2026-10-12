using HexMap.UnityRuntime;
using UnityEngine;

namespace HexMap.Sample
{
    public enum MapViewInputKind
    {
        Click,
        Drag,
        CameraDistance,
    }

    /// <summary>A mode-semantic, post-handler Map input result published by the runtime facade.</summary>
    public readonly struct MapViewInputObservation
    {
        internal MapViewInputObservation(MapViewMode mode, in MapInputDispatchResult result)
        {
            Mode = mode;
            Kind = ToViewInputKind(result.Kind);
            Consumed = result.Consumed;
            Click = result.Click;
            DragDelta = result.DragDelta;
            CameraDistance = result.CameraDistance;
        }

        public MapViewMode Mode { get; }

        public MapViewInputKind Kind { get; }

        public bool Consumed { get; }

        public MapClickContext Click { get; }

        public Vector2 DragDelta { get; }

        public MapCameraDistanceContext CameraDistance { get; }

        private static MapViewInputKind ToViewInputKind(MapInputDispatchKind kind)
        {
            return kind == MapInputDispatchKind.Click
                ? MapViewInputKind.Click
                : kind == MapInputDispatchKind.Drag
                    ? MapViewInputKind.Drag
                    : MapViewInputKind.CameraDistance;
        }
    }
}

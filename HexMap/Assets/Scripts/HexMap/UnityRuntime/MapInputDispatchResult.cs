using UnityEngine;

namespace HexMap.UnityRuntime
{
    public enum MapInputDispatchKind
    {
        Click,
        Drag,
        CameraDistance,
    }

    /// <summary>The outcome of a handler invocation made by <see cref="MapInputDispatcher"/>.</summary>
    public readonly struct MapInputDispatchResult
    {
        private MapInputDispatchResult(
            int channel,
            MapInputDispatchKind kind,
            bool consumed,
            MapClickContext click,
            Vector2 dragDelta,
            MapCameraDistanceContext cameraDistance)
        {
            Channel = channel;
            Kind = kind;
            Consumed = consumed;
            Click = click;
            DragDelta = dragDelta;
            CameraDistance = cameraDistance;
        }

        public int Channel { get; }

        public MapInputDispatchKind Kind { get; }

        public bool Consumed { get; }

        public MapClickContext Click { get; }

        public Vector2 DragDelta { get; }

        public MapCameraDistanceContext CameraDistance { get; }

        public static MapInputDispatchResult Clicked(int channel, in MapClickContext context, bool consumed)
        {
            return new MapInputDispatchResult(
                channel,
                MapInputDispatchKind.Click,
                consumed,
                context,
                default(Vector2),
                default(MapCameraDistanceContext));
        }

        public static MapInputDispatchResult Dragged(int channel, Vector2 screenDelta, bool consumed)
        {
            return new MapInputDispatchResult(
                channel,
                MapInputDispatchKind.Drag,
                consumed,
                default(MapClickContext),
                screenDelta,
                default(MapCameraDistanceContext));
        }

        public static MapInputDispatchResult CameraDistanceChanged(
            int channel,
            in MapCameraDistanceContext context,
            bool consumed)
        {
            return new MapInputDispatchResult(
                channel,
                MapInputDispatchKind.CameraDistance,
                consumed,
                default(MapClickContext),
                default(Vector2),
                context);
        }
    }
}

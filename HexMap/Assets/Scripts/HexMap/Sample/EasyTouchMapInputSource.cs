using System;
using HedgehogTeam.EasyTouch;
using HexMap.UnityRuntime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HexMap.Sample
{
    /// <summary>
    /// 将 EasyTouch 的设备手势转换为 Map 输入语义。
    /// 由 <see cref="MapViewRuntimeFacade"/> 私有拥有并显式管理生命周期。
    /// </summary>
    internal sealed class EasyTouchMapInputSource : IDisposable
    {
#if UNITY_EDITOR
        private const float WheelZoomStepMultiplier = 1.15f;
#endif

        private readonly MapInputDispatcher m_Dispatcher;
        private readonly float m_PinchDeadZoneNormalized;

        private int m_PrimaryFingerIndex = -1;
        private int m_RouteRevisionAtStart;
        private Vector2 m_StartPosition;
        private bool m_StartedOnUi;
        private bool m_HasBecomeMultiTouch;
        private bool m_HasRoutedSwipe;
        private bool m_IsPinchActive;
        private int m_PinchRouteRevisionAtStart;
        private Vector2 m_PinchScreenAnchor;
        private bool m_IsStarted;
        private bool m_IsDisposed;

        internal EasyTouchMapInputSource(MapInputDispatcher dispatcher, float pinchDeadZoneNormalized)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            m_Dispatcher = dispatcher;
            m_PinchDeadZoneNormalized = Mathf.Max(0f, pinchDeadZoneNormalized);
        }

        internal void Start()
        {
            ThrowIfDisposed();
            if (m_IsStarted)
            {
                return;
            }

            EasyTouch.On_TouchStart += OnTouchStart;
            EasyTouch.On_Swipe += OnSwipe;
            EasyTouch.On_SwipeEnd += OnSwipeEnd;
            EasyTouch.On_SimpleTap += OnSimpleTap;
            EasyTouch.On_TouchStart2Fingers += OnTouchStart2Fingers;
            EasyTouch.On_Pinch += OnPinch;
            EasyTouch.On_PinchEnd += OnPinchEnd;
            EasyTouch.On_Cancel += OnCancel;
            EasyTouch.On_Cancel2Fingers += OnCancel2Fingers;
            m_IsStarted = true;
        }

        internal void Stop()
        {
            ThrowIfDisposed();
            if (m_IsStarted)
            {
                EasyTouch.On_TouchStart -= OnTouchStart;
                EasyTouch.On_Swipe -= OnSwipe;
                EasyTouch.On_SwipeEnd -= OnSwipeEnd;
                EasyTouch.On_SimpleTap -= OnSimpleTap;
                EasyTouch.On_TouchStart2Fingers -= OnTouchStart2Fingers;
                EasyTouch.On_Pinch -= OnPinch;
                EasyTouch.On_PinchEnd -= OnPinchEnd;
                EasyTouch.On_Cancel -= OnCancel;
                EasyTouch.On_Cancel2Fingers -= OnCancel2Fingers;
                m_IsStarted = false;
            }

            ClearGesture();
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            Stop();
            m_IsDisposed = true;
        }

        internal void Tick()
        {
            ThrowIfDisposed();
#if UNITY_EDITOR
            if (m_IsStarted)
            {
                RouteMouseWheel();
            }
#endif
        }

        private void OnTouchStart(Gesture gesture)
        {
            if (gesture == null)
            {
                return;
            }

            if (gesture.touchCount != 1)
            {
                m_HasBecomeMultiTouch = true;
                return;
            }

            if (m_PrimaryFingerIndex >= 0)
            {
                return;
            }

            m_PrimaryFingerIndex = gesture.fingerIndex;
            m_RouteRevisionAtStart = m_Dispatcher.InputRouteRevision;
            m_StartPosition = gesture.startPosition;
            m_StartedOnUi = gesture.IsOverUIElement();
            m_HasBecomeMultiTouch = false;
            m_HasRoutedSwipe = false;
        }

        private void OnSwipe(Gesture gesture)
        {
            if (!IsEligiblePrimaryGesture(gesture))
            {
                return;
            }

            var screenDelta = m_HasRoutedSwipe
                ? gesture.deltaPosition
                : gesture.position - m_StartPosition;
            m_HasRoutedSwipe = true;
            m_Dispatcher.OnMapDragged(screenDelta);
        }

        private void OnSimpleTap(Gesture gesture)
        {
            if (gesture == null || gesture.fingerIndex != m_PrimaryFingerIndex)
            {
                return;
            }

            if (IsEligiblePrimaryGesture(gesture))
            {
                m_Dispatcher.OnMapClicked(m_StartPosition);
            }

            ClearGesture();
        }

        private void OnSwipeEnd(Gesture gesture)
        {
            if (gesture != null && gesture.fingerIndex == m_PrimaryFingerIndex)
            {
                ClearGesture();
            }
        }

        private void OnTouchStart2Fingers(Gesture gesture)
        {
            m_HasBecomeMultiTouch = true;
            ClearPinch();
            if (gesture == null || DidAnyPinchFingerStartOnUi(gesture))
            {
                return;
            }

            m_IsPinchActive = true;
            m_PinchRouteRevisionAtStart = m_Dispatcher.InputRouteRevision;
            m_PinchScreenAnchor = gesture.startPosition;
            m_Dispatcher.OnMapCameraDistanceChanged(new MapCameraDistanceContext(m_PinchScreenAnchor, 0f));
        }

        private void OnPinch(Gesture gesture)
        {
            if (!m_IsPinchActive || gesture == null)
            {
                return;
            }

            if (m_Dispatcher.InputRouteRevision != m_PinchRouteRevisionAtStart)
            {
                ClearPinch();
                return;
            }

            var shorterScreenDimension = Mathf.Min(Screen.width, Screen.height);
            if (shorterScreenDimension <= 0f)
            {
                return;
            }

            var normalizedDelta = gesture.deltaPinch / shorterScreenDimension;
            if (Mathf.Abs(normalizedDelta) < m_PinchDeadZoneNormalized)
            {
                return;
            }

            m_Dispatcher.OnMapCameraDistanceChanged(
                new MapCameraDistanceContext(m_PinchScreenAnchor, normalizedDelta));
        }

        private void OnPinchEnd(Gesture gesture)
        {
            ClearPinch();
        }

        private void OnCancel(Gesture gesture)
        {
            if (gesture == null || gesture.fingerIndex == m_PrimaryFingerIndex)
            {
                ClearGesture();
            }
        }

        private void OnCancel2Fingers(Gesture gesture)
        {
            ClearGesture();
        }

#if UNITY_EDITOR
        private void RouteMouseWheel()
        {
            var scrollSteps = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scrollSteps, 0f) || IsScreenPositionOverUi(Input.mousePosition))
            {
                return;
            }

            var screenAnchor = (Vector2)Input.mousePosition;
            m_Dispatcher.OnMapCameraDistanceChanged(new MapCameraDistanceContext(screenAnchor, 0f));
            m_Dispatcher.OnMapCameraDistanceChanged(
                new MapCameraDistanceContext(
                    screenAnchor,
                    scrollSteps * Mathf.Log(WheelZoomStepMultiplier)));
        }
#endif

        private static bool DidAnyPinchFingerStartOnUi(Gesture gesture)
        {
            var touches = Input.touches;
            if (touches.Length > 2)
            {
                return true;
            }

            if (touches.Length == 2)
            {
                return IsScreenPositionOverUi(touches[0].position)
                    || IsScreenPositionOverUi(touches[1].position);
            }

            return gesture.IsOverUIElement() || IsScreenPositionOverUi(gesture.startPosition);
        }

        private static bool IsScreenPositionOverUi(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var eventData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };
            var raycastResults = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(eventData, raycastResults);
            for (var index = 0; index < raycastResults.Count; index++)
            {
                if (raycastResults[index].module is GraphicRaycaster)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsEligiblePrimaryGesture(Gesture gesture)
        {
            if (gesture == null
                || gesture.touchCount != 1
                || gesture.fingerIndex != m_PrimaryFingerIndex
                || m_StartedOnUi
                || m_HasBecomeMultiTouch)
            {
                return false;
            }

            if (m_Dispatcher.InputRouteRevision != m_RouteRevisionAtStart)
            {
                ClearGesture();
                return false;
            }

            return true;
        }

        private void ClearGesture()
        {
            m_PrimaryFingerIndex = -1;
            m_RouteRevisionAtStart = 0;
            m_StartPosition = Vector2.zero;
            m_StartedOnUi = false;
            m_HasBecomeMultiTouch = false;
            m_HasRoutedSwipe = false;
            ClearPinch();
        }

        private void ClearPinch()
        {
            m_IsPinchActive = false;
            m_PinchRouteRevisionAtStart = 0;
            m_PinchScreenAnchor = Vector2.zero;
        }

        private void ThrowIfDisposed()
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(EasyTouchMapInputSource));
            }
        }
    }
}

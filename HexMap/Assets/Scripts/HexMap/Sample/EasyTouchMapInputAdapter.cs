using HedgehogTeam.EasyTouch;
using HexMap.UnityRuntime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HexMap.Sample
{
    /// <summary>
    /// 将 EasyTouch 的设备手势转换为 Map 输入语义。
    /// 手势归属只在第一根手指按下时判定一次：从 UI 开始、被取消或曾经变为多指的手势，
    /// 都不会进入 Map Dispatcher。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EasyTouchMapInputAdapter : MonoBehaviour
    {
#if UNITY_EDITOR
        // 鼠标滚轮仅用于 Editor 中验证地图缩放手感。
        private const float WheelZoomStepMultiplier = 1.15f;
#endif

        [SerializeField]
        private MapInputDispatcher m_Dispatcher;

        [SerializeField]
        [Tooltip("Pinch 起始标记之后，小于此归一化值的距离变化会被忽略。")]
        private float m_PinchDeadZoneNormalized = 0.003f;

        // 当前候选单指手势的主手指。-1 表示没有正在跟踪的手势。
        private int m_PrimaryFingerIndex = -1;

        // 按下时的输入路由版本；模式或活动相机切换后，旧手势必须失效。
        private int m_RouteRevisionAtStart;

        // 按下位置既是 Tap 的拾取位置，也用于补齐首次 Swipe 的阈值前位移。
        private Vector2 m_StartPosition;

        // UI 归属只在按下时采样，之后手指跨越 UI 边界也不改变归属。
        private bool m_StartedOnUi;

        // 第二根手指加入后永久为真，防止 Pinch 结束时误触发 Map Tap。
        private bool m_HasBecomeMultiTouch;

        // EasyTouch 越过静止容差才开始发出 Swipe；首次需要使用完整位移。
        private bool m_HasRoutedSwipe;

        // Pinch 的锚点在第二指落下时固定，后续双指中点移动不会让地图漂移。
        private bool m_IsPinchActive;
        private int m_PinchRouteRevisionAtStart;
        private Vector2 m_PinchScreenAnchor;

        // 缺少 Dispatcher 是接线错误，只报告一次，避免每次手势重复刷屏。
        private bool m_HasReportedMissingDispatcher;

        /// <summary>
        /// Map 输入的唯一路由入口。替换引用时取消正在进行的手势，避免旧状态继续生效。
        /// </summary>
        public MapInputDispatcher Dispatcher
        {
            get { return m_Dispatcher; }
            set
            {
                if (ReferenceEquals(m_Dispatcher, value))
                {
                    return;
                }

                m_Dispatcher = value;
                ClearGesture();
            }
        }

        /// <summary>
        /// Pinch 起始标记之后，能够进入 Map 输入路由的最小归一化距离变化。
        /// </summary>
        public float PinchDeadZoneNormalized
        {
            get { return m_PinchDeadZoneNormalized; }
            set { m_PinchDeadZoneNormalized = Mathf.Max(0f, value); }
        }

#if UNITY_EDITOR
        private void Update()
        {
            RouteMouseWheel();
        }
#endif

        private void OnEnable()
        {
            // EasyTouch 使用静态事件；启用和停用必须成对订阅与解除，避免失效对象被继续回调。
            EasyTouch.On_TouchStart += OnTouchStart;
            EasyTouch.On_Swipe += OnSwipe;
            EasyTouch.On_SwipeEnd += OnSwipeEnd;
            EasyTouch.On_SimpleTap += OnSimpleTap;
            EasyTouch.On_TouchStart2Fingers += OnTouchStart2Fingers;
            EasyTouch.On_Pinch += OnPinch;
            EasyTouch.On_PinchEnd += OnPinchEnd;
            EasyTouch.On_Cancel += OnCancel;
            EasyTouch.On_Cancel2Fingers += OnCancel2Fingers;
        }

        private void OnDisable()
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
            ClearGesture();
        }

        private void OnTouchStart(Gesture gesture)
        {
            if (gesture == null)
            {
                return;
            }

            // 第一根手指取得候选单指序列；之后的手指由下方双指开始事件处理，并永久取消本次
            // Map 输入资格。
            // EasyTouch 在同一帧识别双指时，仍会随后为每根手指补发 On_TouchStart。它们不是新的
            // 单指候选手势；若在这里重置多指标记，随后补发的单指 Swipe 就会错误地平移 Map。
            if (gesture.touchCount != 1)
            {
                m_HasBecomeMultiTouch = true;
                return;
            }

            if (m_PrimaryFingerIndex >= 0)
            {
                return;
            }

            if (!TryGetDispatcher(out var dispatcher))
            {
                return;
            }

            m_PrimaryFingerIndex = gesture.fingerIndex;
            m_RouteRevisionAtStart = dispatcher.InputRouteRevision;
            m_StartPosition = gesture.startPosition;
            m_StartedOnUi = gesture.IsOverUIElement();
            m_HasBecomeMultiTouch = false;
            m_HasRoutedSwipe = false;
        }

        private void OnSwipe(Gesture gesture)
        {
            if (!IsEligiblePrimaryGesture(gesture, out var dispatcher))
            {
                return;
            }

            // EasyTouch 仅在越过静止容差后才发出 Swipe。首次发送完整位移，避免相机落后手指
            // 一个阈值距离；之后使用事件自身提供的逐帧增量。
            var screenDelta = m_HasRoutedSwipe
                ? gesture.deltaPosition
                : gesture.position - m_StartPosition;
            m_HasRoutedSwipe = true;
            dispatcher.OnMapDragged(screenDelta);
        }

        private void OnSimpleTap(Gesture gesture)
        {
            if (gesture == null || gesture.fingerIndex != m_PrimaryFingerIndex)
            {
                return;
            }

            if (IsEligiblePrimaryGesture(gesture, out var dispatcher))
            {
                dispatcher.OnMapClicked(m_StartPosition);
            }

            // 被 UI 或多指规则拒绝的 Tap 同样是终态。若不释放状态，下一次物理触摸会被误认为
            // 第二根手指并被静默丢弃。
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
            // Simulator 和真机都可能在 adapter 收到第一根手指的 On_TouchStart 前先发双指开始。
            // 因此不能依赖 m_PrimaryFingerIndex 是否已建立；只要双指开始，这个物理序列就永远
            // 不能再作为 Map 单指 Swipe 或 Tap 处理。
            m_HasBecomeMultiTouch = true;

            // 第二指到来会永久取消先前单指，确保松开 Pinch 时不会选择 Map Cell。
            ClearPinch();
            if (gesture == null || DidAnyPinchFingerStartOnUi(gesture))
            {
                return;
            }

            MapInputDispatcher dispatcher;
            if (!TryGetDispatcher(out dispatcher))
            {
                return;
            }

            m_IsPinchActive = true;
            m_PinchRouteRevisionAtStart = dispatcher.InputRouteRevision;
            m_PinchScreenAnchor = gesture.startPosition;

            // 零增量标记就是完整的开始契约：Handler 若将来需要维护手势状态，可在这里重置，
            // 而不必通过 Map 输入暴露后端特有的开始/结束回调。
            dispatcher.OnMapCameraDistanceChanged(new MapCameraDistanceContext(m_PinchScreenAnchor, 0f));
        }

        private void OnPinch(Gesture gesture)
        {
            if (!m_IsPinchActive || gesture == null)
            {
                return;
            }

            MapInputDispatcher dispatcher;
            if (!TryGetDispatcher(out dispatcher) || dispatcher.InputRouteRevision != m_PinchRouteRevisionAtStart)
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

            dispatcher.OnMapCameraDistanceChanged(
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

            MapInputDispatcher dispatcher;
            if (!TryGetDispatcher(out dispatcher))
            {
                return;
            }

            var screenAnchor = (Vector2)Input.mousePosition;
            // 每次滚轮输入都是独立的距离手势，遵循与 Pinch 相同的零增量标记约定。
            dispatcher.OnMapCameraDistanceChanged(new MapCameraDistanceContext(screenAnchor, 0f));
            dispatcher.OnMapCameraDistanceChanged(
                new MapCameraDistanceContext(
                    screenAnchor,
                    scrollSteps * Mathf.Log(WheelZoomStepMultiplier)));
        }
#endif

        private static bool DidAnyPinchFingerStartOnUi(Gesture gesture)
        {
            // EasyTouch 的双指 UI 标记可能要求两指都命中同一个 UI。Map 的归属规则更严格：
            // 任一手指属于 UI 就拒绝整次 Pinch。原生触点能提供各自的起始位置；桌面端的
            // Second Finger Simulation 没有 Input.touches 条目，因此退回使用 EasyTouch 的 UI 结果。
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
                // Physics Raycaster 也可能注册到 EventSystem；它们命中的是 Map 世界对象而不是 UI，
                // 不能因此抑制相机输入。
                if (raycastResults[index].module is GraphicRaycaster)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsEligiblePrimaryGesture(Gesture gesture, out MapInputDispatcher dispatcher)
        {
            dispatcher = null;
            // 只有同一根、从 Map 开始且从未变为多指的手指，才可能继续向下游路由。
            if (gesture == null
                || gesture.touchCount != 1
                || gesture.fingerIndex != m_PrimaryFingerIndex
                || m_StartedOnUi
                || m_HasBecomeMultiTouch)
            {
                return false;
            }

            if (!TryGetDispatcher(out dispatcher))
            {
                ClearGesture();
                return false;
            }

            if (dispatcher.InputRouteRevision != m_RouteRevisionAtStart)
            {
                // 模式或活动相机已经切换；需要新的物理手势才能操作新的输入路由。
                ClearGesture();
                return false;
            }

            return true;
        }

        private bool TryGetDispatcher(out MapInputDispatcher dispatcher)
        {
            dispatcher = m_Dispatcher;
            if (dispatcher != null)
            {
                return true;
            }

            if (!m_HasReportedMissingDispatcher)
            {
                m_HasReportedMissingDispatcher = true;
                Debug.LogError(
                    "EasyTouchMapInputAdapter 未配置 MapInputDispatcher，Map 手势无法送达。",
                    this);
            }

            return false;
        }

        private void ClearGesture()
        {
            // 所有退出路径都回到同一初始状态，下一根手指可以开始独立的新手势。
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
    }
}

using HedgehogTeam.EasyTouch;
using HexMap.UnityRuntime;
using UnityEngine;

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
        [SerializeField]
        private MapInputDispatcher m_Dispatcher;

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

        private void OnEnable()
        {
            // EasyTouch 使用静态事件；启用和停用必须成对订阅与解除，避免失效对象被继续回调。
            EasyTouch.On_TouchStart += OnTouchStart;
            EasyTouch.On_Swipe += OnSwipe;
            EasyTouch.On_SwipeEnd += OnSwipeEnd;
            EasyTouch.On_SimpleTap += OnSimpleTap;
            EasyTouch.On_TouchStart2Fingers += OnTouchStart2Fingers;
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
            // 本票不路由 Pinch；但第二指到来必须永久取消先前单指，确保松开 Pinch 时不会选择
            // Map Cell。
            if (m_PrimaryFingerIndex >= 0)
            {
                m_HasBecomeMultiTouch = true;
            }
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

        private bool IsEligiblePrimaryGesture(Gesture gesture, out MapInputDispatcher dispatcher)
        {
            dispatcher = null;
            // 只有同一根、从 Map 开始且从未变为多指的手指，才可能继续向下游路由。
            if (gesture == null || gesture.fingerIndex != m_PrimaryFingerIndex || m_StartedOnUi || m_HasBecomeMultiTouch)
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
        }
    }
}

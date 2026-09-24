using System;
using UnityEngine;

namespace HexMap.Core
{
    /// <summary>
    /// 正交相机的取景快照（不可变）。该相机正对六边形地图的中心线向下俯视。
    /// <para>
    /// 相机只能沿地图的局部 +X 轴平移，因此屏幕上的“上方”对应地图的局部 +Z 轴。
    /// 平面（<see cref="HexPlane"/>）与朝向（<see cref="HexOrientation"/>）都是输入参数：
    /// 平面只决定深度落在哪个世界轴上；朝向则决定六边形两个尺寸中的哪一个作为深度。
    /// 除了这处互换之外，本类型不再对平面或朝向做任何分支判断。
    /// </para>
    /// <para>
    /// 包络是地图的外接六边形，<em>包含最外层格子的顶点</em>：即中心点阵的跨度加上
    /// 一个格子的半轮廓。若仅由中心点阵推导，最外层格子的尖端会被裁掉。
    /// </para>
    /// <para>
    /// 可见高度始终不小于整张地图的深度，因此每一行都始终位于取景框内。可见宽度由视口
    /// 宽高比推导而来，无法单独配置，因此部分列可能落在取景框之外——这正是平移存在的意义。
    /// </para>
    /// <para>
    /// 变量关系分横纵两个方向看。先看<em>横向</em>（相机能左右平移的那个方向：向右 = 地图局部 +X = 屏幕向右）：
    /// <c>MapHalfWidth</c> / <c>MapHalfDepth</c> 是地图外包络的半宽与半深，只由布局与地图半径决定，
    /// 与缩放、宽高比无关。相机可移动的范围由地图与取景框的宽度差决定：
    /// <c>MovableHalfRange = max(0, MapHalfWidth - VisibleWidth / 2)</c>，
    /// 于是 <c>MinOffset = -MovableHalfRange</c>、<c>MaxOffset = +MovableHalfRange</c>；
    /// 偏移 0 表示相机正对地图中心 <c>Origin</c>。
    /// </para>
    /// <para>
    /// 取景框是“屏幕能看到的范围”（宽 = <c>VisibleWidth</c>），只有横向会裁掉地图：
    /// 偏移为 <c>MaxOffset</c> 时取景框左边缘正好贴住地图左边缘，为 <c>MinOffset</c> 时右边缘贴住地图右边缘，
    /// 再往外推就会露出一段没有地图的空白，因此被 <see cref="ClampOffset"/> 夹回范围内。
    /// 如果 <c>VisibleWidth</c> ≥ <c>MapWidth</c>（例如 Zoom = 1 时），可移动范围为零，相机被锁在地图中心。
    /// </para>
    /// <para>
    /// 再看<em>纵向</em>（平面轴方向，<c>HexPlane.XZ</c> 时为地图局部 +Z，<c>HexPlane.XY</c> 时为 +Y）：
    /// <c>VisibleHeight = 2 × BaseOrthographicSize / Zoom</c>，而 <c>BaseOrthographicSize = MapHalfDepth × ViewMargin</c>，
    /// 所以在 Zoom = 1 时可见高度恰好是地图深度乘以 <c>ViewMargin</c>，地图上下边缘与取景框上下边缘之间的
    /// 空隙就是 <c>ViewMargin</c> 带来的留白。也正因为可见高度永远不小于地图深度，纵向不存在被裁掉的行，
    /// 不需要上下平移——只有横向的 <c>MinOffset</c> / <c>MaxOffset</c> 有意义。
    /// </para>
    /// <para>
    /// 各量的依赖关系可以按下面的顺序读：
    /// <c>MapWidth = 2 × MapHalfWidth</c>、<c>MapDepth = 2 × MapHalfDepth</c> 只取决于地图；
    /// <c>VisibleHeight = 2 × OrthographicSize</c>、<c>VisibleWidth = VisibleHeight × Aspect</c> 由视图尺寸推导；
    /// <c>OrthographicSize = BaseOrthographicSize / Zoom</c> 由缩放档位决定（<c>Zoom</c> 是绝对档位而非倍率，
    /// 因此重复应用不会累积）。也就是说：缩放变 → 可见尺寸变 → 可移动范围变，而地图外包络始终不变。
    /// </para>
    /// <para>
    /// 对照图见 <c>docs/implementation/orthographic-map-framing-diagram.md</c>（含横向平移与纵向 ViewMargin 两张示意图）。
    /// </para>
    /// <para>
    /// 由此得到几条可以直接背下来的推论：
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>MapHalfWidth</c> / <c>MapHalfDepth</c> 只来自地图外包络，与 <c>Zoom</c>、<c>Aspect</c> 无关，是“地图本身有多大”。</description></item>
    /// <item><description><c>VisibleWidth</c> = <c>VisibleHeight</c> × <c>Aspect</c>，不是独立参数；屏幕越宽，取景框越宽，越可能不需要平移。</description></item>
    /// <item><description><c>ViewMargin</c> 只作用在深度方向，使取景框纵向始终包住地图，因此永远不需要上下平移，只需左右平移。</description></item>
    /// <item><description><c>Zoom</c> 越大：取景框在世界上越小、屏幕上的格子越大、<c>VisibleWidth</c> 越窄，可移动范围越大；<c>Zoom</c> 是绝对档位而非倍率，1 表示“看全深度”（见 <see cref="DefaultZoom"/>）。</description></item>
    /// <item><description><c>MinOffset</c> / <c>MaxOffset</c> 是<em>相机中心</em>允许的偏移范围（基准是地图中心，偏移 0 即相机停在 <c>Origin</c>），不是地图上的坐标；两者关于 0 对称，端点对应“取景框边缘正好贴住地图边缘”。</description></item>
    /// <item><description>当 <c>VisibleWidth</c> ≥ <c>MapWidth</c> 时 <c>MovableHalfRange</c> = 0（见 <see cref="IsLockedToCenter"/>），相机被锁在地图中心，拖拽无效。</description></item>
    /// </list>
    /// </summary>
    public readonly struct OrthographicMapFraming
    {
        /// <summary>
        /// 仍能让每一行都留在取景框内的最小视图边距。
        /// </summary>
        public const float MinimumViewMargin = 1f;

        /// <summary>
        /// 创建取景时使用的缩放级别：地图的整个深度刚好放入取景框。
        /// </summary>
        public const float DefaultZoom = 1f;

        /// <summary>
        /// 地图外包络沿局部 X 轴的半宽：即相机平移所沿的轴。
        /// </summary>
        public float MapHalfWidth { get; }

        /// <summary>
        /// 地图外包络沿屏幕竖直方向的半深度。
        /// </summary>
        public float MapHalfDepth { get; }

        /// <summary>
        /// 取景框显示地图的比例。1 表示显示整个深度并留出视图边距，值越大则越放大。
        /// 除宽高比之外，缩放是唯一会改变可见尺寸的输入；地图包络从不依赖它。
        /// </summary>
        public float Zoom { get; }

        /// <summary>
        /// 本次取景在地图深度四周留出的空白。保留它是为了让缩放变化能在不要求调用方
        /// 重复提供输入的情况下重新计算尺寸。
        /// </summary>
        public float ViewMargin { get; }

        /// <summary>
        /// 构建本次取景所针对的视口宽高比。保留它是为了让缩放变化能重新计算可见宽度。
        /// 宽高比永远不能通过本类型配置。
        /// </summary>
        public float Aspect { get; }

        /// <summary>
        /// 缩放为 1 时的可见半高，等于地图半深度乘以视图边距。
        /// 所有缩放都以该值为基准而非以当前值换算，因此重复应用缩放不会产生累积误差。
        /// </summary>
        public float BaseOrthographicSize { get; }

        /// <summary>
        /// 地图外包络中心在布局局部空间中的位置（中心点阵以布局原点为中心）。
        /// 偏移为零时相机就停在这里。
        /// </summary>
        public Vector3 Origin { get; }

        /// <summary>
        /// 能让每一行都留在取景框内的 <c>Camera.orthographicSize</c>。
        /// </summary>
        public float OrthographicSize { get; }

        /// <summary>
        /// 取景框覆盖的世界坐标高度。始终不小于 <c>2 * MapHalfDepth</c>。
        /// </summary>
        public float VisibleHeight { get; }

        /// <summary>
        /// 取景框覆盖的世界坐标宽度。不可单独配置。
        /// </summary>
        public float VisibleWidth { get; }

        /// <summary>
        /// 地图沿平移轴方向的完整宽度。
        /// </summary>
        public float MapWidth
        {
            get { return MapHalfWidth * 2f; }
        }

        /// <summary>
        /// 地图沿屏幕竖直方向的完整深度。
        /// </summary>
        public float MapDepth
        {
            get { return MapHalfDepth * 2f; }
        }

        /// <summary>
        /// 偏移量在任一方向上离中心可移动的最大距离。
        /// 当取景框宽度不小于地图宽度时为零，此时相机被锁定在中心。
        /// </summary>
        public float MovableHalfRange
        {
            get
            {
                var range = MapHalfWidth - VisibleWidth * 0.5f;
                return range > 0f ? range : 0f;
            }
        }

        /// <summary>
        /// 沿地图局部 +X 轴允许的最小偏移。
        /// </summary>
        public float MinOffset
        {
            get { return -MovableHalfRange; }
        }

        /// <summary>
        /// 沿地图局部 +X 轴允许的最大偏移。
        /// </summary>
        public float MaxOffset
        {
            get { return MovableHalfRange; }
        }

        /// <summary>
        /// 取景框宽度不小于地图宽度、因而无法平移时为 true。
        /// </summary>
        public bool IsLockedToCenter
        {
            get { return MovableHalfRange <= 0f; }
        }

        /// <summary>
        /// 取景框覆盖地图所有列时为 true。视口较宽时就会如此；这是几何结果，不是缺陷。
        /// </summary>
        public bool ShowsEveryColumn
        {
            get { return VisibleWidth >= MapWidth; }
        }

        private OrthographicMapFraming(
            float mapHalfWidth,
            float mapHalfDepth,
            Vector3 origin,
            float zoom,
            float viewMargin,
            float aspect,
            float baseOrthographicSize)
        {
            MapHalfWidth = mapHalfWidth;
            MapHalfDepth = mapHalfDepth;
            Origin = origin;
            Zoom = zoom;
            ViewMargin = viewMargin;
            Aspect = aspect;
            BaseOrthographicSize = baseOrthographicSize;
            OrthographicSize = baseOrthographicSize / zoom;
            VisibleHeight = OrthographicSize * 2f;
            VisibleWidth = VisibleHeight * aspect;
        }

        /// <summary>
        /// 返回同一张地图在另一缩放级别下的取景结果。包络、原点、视图边距与宽高比保持不变，
        /// 只有可见尺寸及其派生值发生变化。缩放为 1 时精确复现当前取景；缩放是绝对级别而非
        /// 倍率，因此重复应用不会产生累积误差。
        /// </summary>
        public OrthographicMapFraming WithZoom(float zoom)
        {
            if (!IsFinitePositive(zoom))
            {
                throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "Zoom must be positive and finite.");
            }

            return new OrthographicMapFraming(
                MapHalfWidth,
                MapHalfDepth,
                Origin,
                zoom,
                ViewMargin,
                Aspect,
                BaseOrthographicSize);
        }

        /// <summary>
        /// 返回同一张地图在另一缩放级别下的取景结果；缩放非法时通过返回值报告，而不是抛出异常。
        /// </summary>
        public bool TryWithZoom(float zoom, out OrthographicMapFraming framing, out string error)
        {
            framing = this;

            if (!IsFinitePositive(zoom))
            {
                error = "Zoom must be positive and finite.";
                return false;
            }

            framing = WithZoom(zoom);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 创建取景快照；当输入无法描述一个取景框时抛出异常。
        /// </summary>
        public static OrthographicMapFraming Create(
            HexLayout layout,
            int mapRadius,
            float viewMargin,
            float aspect,
            float zoom = DefaultZoom)
        {
            // 在这里校验 zoom，而不是让 TryCreate 把它归入布局错误，这样非法缩放上报的
            // 异常类型和参数名与 WithZoom 保持一致。
            if (!IsFinitePositive(zoom))
            {
                throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "Zoom must be positive and finite.");
            }

            OrthographicMapFraming framing;
            string error;
            if (!TryCreate(layout, mapRadius, viewMargin, aspect, zoom, out framing, out error))
            {
                throw new ArgumentException(error, nameof(layout));
            }

            return framing;
        }

        /// <summary>
        /// 以缩放 1 创建取景快照，该级别下整张地图的深度刚好放入取景框。
        /// </summary>
        public static bool TryCreate(
            HexLayout layout,
            int mapRadius,
            float viewMargin,
            float aspect,
            out OrthographicMapFraming framing,
            out string error)
        {
            return TryCreate(layout, mapRadius, viewMargin, aspect, DefaultZoom, out framing, out error);
        }

        /// <summary>
        /// 在指定缩放级别下创建取景快照；输入非法时通过返回值报告，而不是抛出异常。
        /// </summary>
        public static bool TryCreate(
            HexLayout layout,
            int mapRadius,
            float viewMargin,
            float aspect,
            float zoom,
            out OrthographicMapFraming framing,
            out string error)
        {
            framing = default(OrthographicMapFraming);

            if (mapRadius <= 0)
            {
                error = "Map radius must be positive.";
                return false;
            }

            if (!IsFinitePositive(layout.OuterRadius))
            {
                error = "Layout outer radius must be positive and finite.";
                return false;
            }

            if (!IsFinitePositive(layout.SecondaryScale))
            {
                error = "Layout secondary scale must be positive and finite.";
                return false;
            }

            if (!IsFinite(layout.Origin))
            {
                error = "Layout origin must be finite.";
                return false;
            }

            if (!IsFinitePositive(aspect))
            {
                error = "Viewport aspect must be positive and finite.";
                return false;
            }

            if (!IsFinite(viewMargin) || viewMargin < MinimumViewMargin)
            {
                error = "View margin must be finite and at least " + MinimumViewMargin
                    + " so that every row stays inside the frame.";
                return false;
            }

            if (!IsFinitePositive(zoom))
            {
                error = "Zoom must be positive and finite.";
                return false;
            }

            var outerRadius = layout.OuterRadius;
            var secondaryScale = layout.SecondaryScale;
            var radius = mapRadius;

            // 格子轮廓：单个六边形凸包的半尺寸。网格会对平面内分量应用次级缩放，
            // 因此两个方向的尺寸并不相等。
            float cellAxisExtent;
            float cellSecondaryExtent;
            if (layout.Orientation == HexOrientation.Pointy)
            {
                // 顶点位于 30 + 60k 度：平边朝向 ±X，尖角朝向 ±平面方向。
                cellAxisExtent = outerRadius * Mathf.Sqrt(3f) * 0.5f;
                cellSecondaryExtent = outerRadius * secondaryScale;
            }
            else
            {
                // 顶点位于 60k 度：尖角朝向 ±X，平边朝向 ±平面方向。
                cellAxisExtent = outerRadius;
                cellSecondaryExtent = outerRadius * Mathf.Sqrt(3f) * 0.5f * secondaryScale;
            }

            // 中心点阵跨度：从原点格到最外层环上最远的格子。
            float centerAxisExtent;
            float centerSecondaryExtent;
            if (layout.Orientation == HexOrientation.Pointy)
            {
                centerAxisExtent = Mathf.Sqrt(3f) * radius * outerRadius;
                centerSecondaryExtent = 1.5f * radius * outerRadius * secondaryScale;
            }
            else
            {
                centerAxisExtent = 1.5f * radius * outerRadius * secondaryScale;
                centerSecondaryExtent = Mathf.Sqrt(3f) * radius * outerRadius * secondaryScale;
            }

            var mapHalfWidth = centerAxisExtent + cellAxisExtent;
            var mapHalfDepth = centerSecondaryExtent + cellSecondaryExtent;

            // Camera.orthographicSize 表示半高，因此“每一行都在取景框内”意味着在缩放 1 时
            // size >= mapHalfDepth。构造函数由该基准值与缩放推导出实际大小，
            // 所以缩放变化无需再关心视图边距。
            var baseOrthographicSize = mapHalfDepth * viewMargin;

            framing = new OrthographicMapFraming(
                mapHalfWidth,
                mapHalfDepth,
                layout.Origin,
                zoom,
                viewMargin,
                aspect,
                baseOrthographicSize);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 将偏移量限制在 <see cref="MinOffset"/>..<see cref="MaxOffset"/> 范围内。
        /// 非有限值映射为 0。
        /// </summary>
        public float ClampOffset(float offset)
        {
            if (!IsFinite(offset))
            {
                return 0f;
            }

            if (offset < MinOffset)
            {
                return MinOffset;
            }

            if (offset > MaxOffset)
            {
                return MaxOffset;
            }

            return offset;
        }

        /// <summary>
        /// 限制偏移量；当输入为非有限值时通过返回值报告，而不是静默替换。
        /// </summary>
        public bool TrySetOffset(float offset, out float clampedOffset, out string error)
        {
            if (!IsFinite(offset))
            {
                clampedOffset = 0f;
                error = "Offset must be finite.";
                return false;
            }

            clampedOffset = ClampOffset(offset);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 将偏移量映射到可移动范围内的 0..1。相机锁定在中心时返回 0.5。
        /// </summary>
        public float NormalizeOffset(float offset)
        {
            var range = MovableHalfRange;
            if (range <= 0f)
            {
                return 0.5f;
            }

            return (ClampOffset(offset) + range) / (range * 2f);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && IsFinite(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}

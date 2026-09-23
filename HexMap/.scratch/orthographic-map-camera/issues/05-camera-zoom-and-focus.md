# 05 — 相机缩放、焦点与二维平移

**What to build:** 把 `OrthographicMapCamera` 的偏移量从一维标量升级为二维、加入 zoom 状态与插值、加入 `FocusOn`，并让每一次 zoom 变化与焦点变化都按新范围夹取。**不含输入**。

**Blocked by:** 04 — 取景的缩放变换

**Status:** ready-for-agent

## 范围

### 二维偏移

- [ ] `Offset` 从 `float` 变为 `Vector2`（沿地图局部 `+X` 与 `+Z` 两个平面分量）
- [ ] 夹取**逐轴独立**：`clamp(offset.x, 水平范围)`、`clamp(offset.y, 垂直范围)`
- [ ] `TrySetOffset(Vector2, out string error)`：非有限分量返回 `false` + 原因（逐轴报告是哪个分量）
- [ ] `MinOffset` / `MaxOffset` 改为 `Vector2`；`NormalizedOffset` 改为 `Vector2`（范围为 0 的轴取 `0.5`）
- [ ] 世界位置换算：`地图原点 + right × offset.x + up × offset.y`（`right` / `up` 仍取自地图 transform，与 02 一致）

### zoom 状态

- [ ] 序列化 `m_Zoom`（初值 1）、`m_TargetZoom`（初值 1）、`m_MinVisibleWidthRatio`（初值 `0.15`）
- [ ] `m_Zoom <= 0` 视为未初始化，**回落到 1.0**（Unity 新建组件的 `float` 是 0，必须处理）
- [ ] zoom 上限由"最小可视宽度 = 地图宽 × `m_MinVisibleWidthRatio`"反推，下限固定 `1.0`
- [ ] `TargetZoom` 读写（写入即夹到上下限）；`Zoom` 只读
- [ ] 每帧把 `Zoom` 向 `TargetZoom` 插值（用 `Mathf.MoveTowards` 或 `SmoothDamp`，速率序列化可调），插值中每帧重算 `size` 与两轴范围
- [ ] 端点**硬停**，无回弹、无惯性
- [ ] `TryRefresh` 后重新夹取当前 zoom 下的偏移

### 焦点

- [ ] 序列化初始焦点（`HexCoord` 或 `Vector3`，二选一，配一个"是否启用"布尔）
- [ ] `FocusOn(HexCoord)` / `FocusOnWorld(Vector3)`：把世界点转成地图局部平面分量，**瞬时**把 `m_DesiredCenter` 设为 `clamp(焦点, 当前范围)`
- [ ] `zoom == 1` 时**强制居中** `m_DesiredCenter = (0,0)`，忽略焦点
- [ ] **焦点只在两个时刻生效**：`FocusOn` 被调用时，以及**非手势的** zoom 变化时
- [ ] 拖拽（即 `TrySetOffset` / `Offset` 写入）只改 `m_DesiredCenter`，**不改焦点**
- [ ] 需要一个标志让输入层声明"手势进行中"（例如 `BeginGesture()` / `EndGesture()` 或 `IsGestureActive` 属性），手势期间 zoom 变化**不**重新对准焦点

### 边界与失败

- [ ] 所有失败走 `Try* + out string error`，不抛异常到调用方
- [ ] 地图未 `Build()` / 缺引用 / 相机非正交 / `ViewMargin < 1` → 与 02 相同的失败原因，行为不变

## 验收

- [ ] **回归：02 的全部用例必须继续通过**（`zoom = 1`、竖屏、横屏锁死、平面/朝向组合、非恒等 transform、Layer 自检、各失败路径）
- [ ] 新增：
  - `zoom = 1` ⇒ `OrthographicSize == 17.325`、两轴范围里垂直为 0、写入任何偏移后位置不变、且强制居中
  - `zoom = 2` ⇒ `OrthographicSize == 8.663`、水平范围 `±15.04`、垂直范围 `±7.09`（容差 0.01）
  - `zoom = 1` 时 `VisibleHeight >= MapDepth` 成立；`zoom = 2` 时**不成立**（钉住"这是预期"）
  - 二维逐轴夹取：`Offset = (1000, 1000)` 后两轴分别落在各自范围内
  - `TrySetOffset(new Vector2(float.NaN, 1))` → `false`，且原因指明是 X 分量
  - 焦点在中央 ⇒ 偏移为 `(0,0)`；焦点在最外圈 Hex ⇒ 夹取到该轴范围端点，且**该 Hex 的世界位置落在视口内**（用相机视口或已知范围判断均可，不要只断言夹取后的数值）
  - `zoom = 1` ⇒ 偏移回到 `(0,0)`（忽略焦点）
  - **拖拽后 zoom 变化不会把镜头拉回焦点**：先 `FocusOn`、再 `TrySetOffset` 移到别处、再改 `TargetZoom` 并推进插值 → `Offset` 仍是非手势时的位置（钉住 6.4 的规则，防止"每帧从焦点重算"的回归）
  - **手势期间 zoom 变化不重新对准焦点**：`BeginGesture` 后改 `TargetZoom` → `Offset` 不被拉回焦点；`EndGesture` 后再改 → 重新对准
  - zoom 上下限夹取；`m_Zoom = 0` 回落 `1.0`；`m_MinVisibleWidthRatio` 越小 zoom 上限越大
- [ ] C# 编译通过
- [ ] 回归：`powershell -File scripts\run-tests.ps1 -Assembly HexMap.UnityRuntime.Tests.EditMode`

## 已知取舍

- 焦点过渡**瞬时**（无飞行动画），但 zoom 本身的插值仍平滑改变视野大小。
- 不引入"每帧从焦点重算"的路径——见上文那条必须写死的规则。
- 不做惯性 / 回弹 / 吸附到整格。
- 不改 `OrthographicMapFraming` 的既有语义；`zoom = 1` 时行为必须与 02 完全一致。

## Comments

- 决策依据见 `../spec.md` 第 6.1 / 6.2 / 6.4 节。

# 05 — 相机缩放、焦点与二维平移

**What to build:** 把 `OrthographicMapCamera` 的偏移量从一维标量升级为二维、加入 zoom 状态与插值、加入 `FocusOn`，并让每一次 zoom 变化与焦点变化都按新范围夹取。**不含输入**。

**Blocked by:** 04 — 取景的缩放变换

**Status:** done

## 范围

### 二维偏移（已完成）

- [x] `Offset` 从 `float` 变为 `Vector2`？**最终改成 `Center`（地图局部平面坐标）**：`m_DesiredCenter` 沿地图局部 `+X` 与平面轴两个分量
- [x] 夹取**逐轴独立**：水平范围用 `framing.MovableHalfRange`，垂直范围由 `地图半深 − 可视半高` 现算（`VerticalHalfRange`）
- [x] `TrySetOffset(Vector2, out string error)`：非有限分量返回 `false`，原因分别指明 `Offset X` / `Offset Y`
- [x] `MinOffset` / `MaxOffset` 改为 `Vector2`；`NormalizedOffset` 改为 `Vector2`（范围为 0 的轴取 `0.5`）
- [x] 世界位置换算：`地图原点 + right × center.x + up × center.y − forward × height`

### zoom 状态（已完成）

- [x] 序列化 `m_Zoom`（初值 `1`）、`m_TargetZoom`（初值 `1`）、`m_MinVisibleWidthRatio`（初值 `0.15`）、`m_ZoomSpeed`（初值 `6`）
- [x] `m_Zoom <= 0` 视为未初始化，**回落到 1.0**
- [x] zoom 上限 = `1 / (m_MinVisibleWidthRatio × aspect)`，下限固定 `1.0`
- [x] `TargetZoom` 读写（写入即夹到上下限）；`Zoom` 读写（直设，不走插值）
- [x] 每帧 `Tick(deltaTime)` 把 `Zoom` 向 `TargetZoom` 用 `Mathf.MoveTowards` 插值（`Update` 调它，并**公开出来供测试与调用方驱动**）；插值中每帧重算 `size` 与两轴范围
- [x] 端点**硬停**
- [x] `TryRefresh` 后重新夹取当前 zoom 下的偏移

### 焦点（已完成）

- [x] 序列化初始焦点 `m_InitialFocus` + `m_HasInitialFocus`
- [x] `FocusOn(HexCoord)` / `FocusOnWorld(Vector3)`：**瞬时**把 `m_DesiredCenter` 设为 `clamp(焦点, 当前范围)`。`FocusOn` 用 `HexCoord.Distance` 判断是否在地图内（半径 R 的地图恰好是"距原点 ≤ R"的集合，不必碰渲染器）
- [x] `zoom == 1` 时**强制居中**，忽略焦点
- [x] 焦点只在两个时刻生效：`FocusOn` 被调用时，以及**非手势的** zoom 变化时
- [x] 拖拽只改 `m_DesiredCenter`，**不改焦点**
- [x] `BeginGesture()` / `EndGesture()` / `IsGestureActive` 让输入层声明手势
- [x] **`TryZoomTo(targetZoom, viewportAnchor)`**：锚点缩放的实现入口，见下方"实现时抓到的 bug"

## 验收

- [x] **回归：02 的全部用例继续通过**（已按二维 API 与 zoom 改写，逻辑不变）
- [x] 新增：
  - `zoom = 1` ⇒ `OrthographicSize == 17.325`、垂直范围为 0、`Center == (0,0)`、水平范围 `±10.173`
  - `zoom = 2` ⇒ `OrthographicSize == 8.663`、水平 `±15.046`、垂直 `±7.0875`；且 `VisibleHeight < MapDepth`（钉住"所有行不再保证"是预期）
  - `MaxZoom == 1 / (0.15 × aspect) == 11.85185`；改 `MinVisibleWidthRatio` 会改变上限；`TargetZoom` 上下限夹取
  - 插值：`Tick` 多帧后到达目标；`steps > 1`（不会一帧跳到位）；`size` 跟着插值走
  - 焦点在中央 ⇒ 偏移 `(0,0)`；焦点在东/西/北边缘 ⇒ 夹到对应轴的范围端点，且 `Center.x < Focus.x`（边缘焦点到不了正中心）
  - `zoom → 1` ⇒ 居中但 `HasFocus` 仍为真；再放大 ⇒ 重新对准焦点
  - **手势期间 zoom 变化不把镜头拉回焦点**；`FocusOn` 后再非手势 zoom ⇒ 重新对准
  - **锚点缩放**：`anchor = (0,0)` 不移动中心；`anchor = (0.5, 0)` 时"锚点下的世界点"在缩放前后位置不变，且中心朝锚点一侧移动
  - **锚点缩放优先于焦点**：`FocusOn(中心)` 后以 `(0.6, 0)` 为锚缩放，`Center.x > 0`（不回弹到焦点）
  - 二维逐轴夹取；`NaN` 的 X / Y 分别被拒且原因点名分量
  - `Zoom = 0` 后 `TryRefresh` ⇒ 回落到 1
  - `FocusOn` 地图外的坐标 ⇒ 失败；`(0,0)` 与 `(-11,11)` 这类边界坐标成功
  - 平面 × 朝向四种组合下 zoom、`VisibleWidth == VisibleHeight × Aspect` 都成立
- [x] **临时 stub harness 跑通相机全部断言**，另外单独跑通拖拽适配器的二维换算（含竖直分量与退化输入）
- [ ] Unity 内尚未执行：NUnit 用例由使用者运行
- [x] C# 编译通过

## 已知取舍

- **`Offset` 没有保留**，对外叫 `Center`（地图局部平面坐标）。理由是二维化之后"offset"这个名字不再自解释，而 `Center` 同时表达了"相机看向哪里"。
- `MaxZoom` 的公式是 `1 / (ratio × aspect)`。初版 spec 写的 `8.2` 是错的——那是"可视宽 / 地图宽"算错的产物。测试里把它钉成 `11.85185`。
- `Zoom`（直设）与 `TargetZoom`（插值）并存：前者给调试与"立刻到位"，后者给输入。两者写 `Zoom` 时会同时更新，避免状态分叉。
- 焦点过渡瞬时；不做惯性 / 回弹 / 吸附整格。

## Comments

- 决策依据见 `../spec.md` 第 6.1 / 6.2 / 6.3 / 6.4 节。
- **实现时抓到一个真 bug，而且是我方案里漏掉的优先级**：`TryZoomTo` 按锚点算出了正确的新中心 `11.624`，但随后的 `ApplyZoom` 因为"`m_HasFocus` 为真就重新对准焦点"把它覆盖成焦点 `(0,0)`——锚点缩放与"zoom 变化重对准焦点"直接打架。修法是让 `TryZoomTo` 在应用 zoom 期间把 `m_IsGestureActive` 置真（锚点缩放本身就算一次手势），应用完再恢复。spec 6.4 已补上这条。
- 另一个修掉的状态坑：新组件的 `m_DesiredCenter` 默认是 `(0,0)`，与"用户拖到正中"不可区分，会让 `TryRefresh` 把用户拖回的位置重置掉。加了 `m_HasCenter` 单独记录。
- `Zoom` / `TargetZoom` 的 setter 在**没有 framing 时不做上限夹取**（上限依赖 aspect，此时还不知道），否则编辑器里预设的 zoom 会被静默夹成 1。
- **后续修正：上限从"最小可视宽比例"改成了绝对档位 `MaxZoom`（默认 `12`）**，上面这条特例随之删除（上限不再依赖 framing，setter 一律夹取）。原因：`1 / (ratio × aspect)` 与它的名字不符——aspect 以除法进入，展开后可视宽里 aspect 出现两次，竖屏下实际最小可视宽是地图宽的 4.1% 而不是名字所称的 15%（按名字反推应得 `MaxZoom = 3.26`，实现给了 `11.85`）。而 zoom 本身就是"基础档位 / zoom"，绝对档位已经与地图尺寸、半径、屏幕无关，比例参数买不到额外的东西。推导与数值见 `docs/implementation/orthographic-map-camera-internals.md` §4.6；spec 6.1 的表格按历史记录保留，不再改写。
- **后续修正：焦点状态整块删除**。`m_Focus` / `m_HasFocus`、`AlignCenterToFocus`、`Focus` / `HasFocus` getter、`ClearFocus`，以及为仲裁"锚点 vs 焦点"而存在的 `m_IsGestureActive` / `IsGestureActive` / `BeginGesture` / `EndGesture` 与本票修掉的那个 save/restore hack（`:659-662`）全部删除。取而代之的是把对准目标**随调用传递**：
  - `FocusOn` / `FocusOnWorld` 变成一次性请求：立即按**当前**范围夹取中心，**不记住**；
  - 新增 `TryZoomToPoint(zoom, worldPoint)`：**先定档位与 framing，再按新范围夹取对准点**，即本票 6.4 表里"先 focus 再 zoom"的正确顺序被固化成一次调用；
  - 缓动路径**不提供**"带瞄准"的变体：在还在变形的框里瞄准是移动靶，而每帧重瞄正是被删掉的那份状态；生产代码里缓动只被锚点手势使用（`TargetZoom` 全仓只有测试在写）。
  - 为什么这不是"把状态推给上层"：那条规则跨帧、跨调用方（zoom 由输入组件改、refresh 由视口变化触发），搬上去只会散到每个调用点，而 refresh 没有回调可接。把它变成**参数**才是既无状态又不丢正确性的做法。触发这次改动的具体证据：`MapViewModeSwitcher` 是"先 `FocusOnWorld` 后 `SetZoomImmediate`"，而 `FocusOnWorld` 按旧档位夹取——`sw.unity` 下会把边缘目标夹到 `9.09` 而正确值是 zoom 3 的 `16.31`，目标整个出画面。
  - 行为变化：`FocusOn` 过的地方不会再被之后的 zoom 变化"重新对准"；`zoom = 1` 居中后放大不再回到原处。测试 `ZoomOneIsAlwaysCenteredButRemembersTheFocus` / `AGestureZoomDoesNotPullTheCameraTowardsTheFocus` / `AnAnchoredZoomOutranksTheFocus` 已按新契约改写。
  - `m_InitialFocus` / `m_HasInitialFocus` **保留**（它是序列化配置而不是运行时记忆），但"是否还要应用"的判据从 `m_HasFocus` 改成 `m_HasCenter` —— 这顺手修掉一个潜伏 bug：拖拽只设 `m_HasCenter`，旧判据会让一次 refresh 把玩家的拖拽推回初始焦点。

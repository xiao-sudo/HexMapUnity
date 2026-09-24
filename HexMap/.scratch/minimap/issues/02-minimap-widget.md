# 02 — 小地图控件本体（RenderTexture、重烤、队伍点、点击）

**What to build:** 在 01 的接口之上做出可见可用的小地图：一张 128² 的 `RenderTexture`、一个 `RawImage`、按需重烤的状态机、队伍位置点，以及 `Clicked` 事件。

**Blocked by:** 01 — 小地图取景、跟随与投影接口

**Status:** ready-for-agent

## 范围

- [ ] `RenderTexture` 的创建与 `Release`（`OnDestroy` 里释放，避免重复进入场景泄漏）；`targetTexture` 常驻 ⇒ 相机永不画到屏幕
- [ ] 重烤状态机，**可被 `Tick(deltaTime)` 驱动**（与 `OrthographicMapCamera.Tick` 同一套路，便于 EditMode 测试）：`RequestRefresh()` / `SetFocus` 置脏 → `Tick` 里 `camera.enabled = true` → 下一帧 `false`
- [ ] 队伍位置点：一个 `Image`，位置由 01 的 `TryGetLocalPoint(部队世界位置)` 得出；`local` 越界时**隐藏**（不夹取）
- [ ] `event Action<Vector3> Clicked`：实现 `IPointerClickHandler` → 局部点 → `TryGetWorldPoint` → 抛事件。**事件里不含 PlotId / HexCoord**，只给世界点
- [ ] **不引用** `MapClickDispatcher`、`GvgMapRuntimeController`、`GvgMapRuntimeController.Select`
- [ ] `MapViewModeSwitcher` 增加"小地图相机"槽：进俯视时 `enabled = false`，回 gameplay 时恢复（与现有 drag / zoom 槽同形）
- [ ] 小地图相机与 `OrthographicMapLayerSettings` 都用**独立实例**；mask 必须包含 `HexMapView.CellLayer`，否则 `TryValidate` 会报错

## 验收

- [ ] EditMode：脏标记 → `Tick` 后相机被 enable 一帧、随后回到 disable；**不脏时 `Tick` 不动相机**
- [ ] EditMode：`SetFocus` 到另一格 ⇒ 置脏；`SetFocus` 到同一格 ⇒ **不置脏**（同一格内移动不该触发重烤）
- [ ] EditMode：把控件局部坐标直接喂给点击处理器 ⇒ `Clicked` 收到的世界点落在预期平面位置（误差 1e-2）
- [ ] EditMode：`SetFocus` 到地图外 ⇒ 返回 `false`、不置脏、不移动相机
- [ ] 人工（`sw.unity`）：控件可见、地图带归属色、点跟着 `SetFocus` 走、靠近边缘时点偏离中心
- [ ] 人工：**点小地图不会触发主地图的选中/面板**（点击隔离；防 `raycastTarget` 被关或被别的 UI 盖住）
- [ ] 人工：进入俯视模式后控件隐藏且小地图相机 `enabled == false`
- [ ] 人工：反复进出两个模式 5 次，无 Console error、无 RT 泄漏警告
- [ ] C# 编译通过

## 已知取舍

- **重烤"下一帧生效"**：这个 URP 没有即时渲染 API（spec 第 1 节），所以相机必须真的被 enable 一帧。小地图滞后一帧完全可接受。
- `sw.unity` 用的是 `MeshRenderer` 策略 ⇒ 每次烤约 35–42 个 draw call（视锥剔除后）。切成 `DrawMeshInstanced` 会降到 1–2 个，但**这是场景侧决策，本票不改**。
- 队伍点是**边缘补偿**而非常驻指示：地图中部时它恒在正中（spec 2.3）。
- 按格跳动：跟随源是 `HexCoord`，同一格内移动不动（spec 2.3）。

## Comments

- 决策依据见 `../spec.md` 第 2.4 / 2.5 节；失败模式见第 4 节（尤其"一片黑"与"点击穿透"两条）。

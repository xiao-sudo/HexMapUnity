# 01 — 小地图取景、跟随与投影接口

**What to build:** `MinimapView` 的三个接口：`SetFocus(HexCoord)`（跟随 + 夹取 + 请求重烤）、`TryGetLocalPoint(Vector3 world, out Vector2 local)`（世界 → 控件局部）、`TryGetWorldPoint(Vector2 local, out Vector3 world)`（控件局部 → 世界）。本票**不含 RT / UI / 点击**，只做到"给定一台配置好的小地图相机，这三个函数给出正确结果"。

**Blocked by:** 无（复用现有 rig）

**Status:** ready-for-agent

## 范围

- [ ] 序列化字段：`m_MapCamera`（`OrthographicMapCamera`）、`m_MapView`（`HexMapView`）、`m_Zoom`（默认 **3.667**）、`m_ViewMargin`（默认 **1.1**，**显式钉死，不与主视图共用**）、`m_RtSize`（默认 128）
- [ ] `SetFocus(HexCoord)`：先确保 framing（没有就 `TryRefresh`）再 `FocusOn`；地图外坐标返回 `false` + 原因，**不抛异常**
- [ ] `TryGetLocalPoint`：转调 `m_MapCamera.Camera.WorldToViewportPoint`
- [ ] `TryGetWorldPoint`：转调 `m_MapCamera.Camera.ScreenPointToRay(uv × m_RtSize)` + `m_MapView.WorldPlane.Raycast`
- [ ] 世界点不在视口内时 `local` **如实落在 0..1 之外并返回 `true`**（隐藏还是夹取由调用方决定），不静默夹取
- [ ] `RequestRefresh()` 只置一个脏标记（真正的重烤属于 02）
- [ ] `TryGetFraming` 之类的只读查询透传，便于测试与调试

## 验收

- [ ] EditMode：`zoom 3.667` 下 `TryGetFraming` ⇒ 可视高 ≈ `10.5`、行 ≈ `7.0`、方形列 ≈ `6.1`（误差 1e-3）
- [ ] EditMode：`m_Zoom` 改成 2 ⇒ 行 ≈ `12.8`；改成 6.667（方形上限）⇒ 不报错且行 ≈ `3.9`
- [ ] EditMode：地图中央格 `SetFocus` ⇒ 该格世界位置投影回 `(0.5, 0.5)`（误差 1e-3）
- [ ] EditMode：最外圈格（`(11,0)` 与 `(0,11)`）`SetFocus` ⇒ 中心被夹取，该格投影偏离 0.5 **但仍在 0..1 内**，且偏向预期一侧
- [ ] EditMode：地图外坐标 `SetFocus` ⇒ `false` 且带原因
- [ ] EditMode：未 `TryRefresh` 时 `SetFocus` **自己把它补上**（不返回 "Refresh the camera before focusing."）
- [ ] EditMode：`TryGetLocalPoint` → `TryGetWorldPoint` 往返回到同一平面点（误差 1e-2）
- [ ] EditMode：远在地图外的世界点 ⇒ `local` 在 0..1 之外，函数仍返回 `true`
- [ ] C# 编译通过

## 已知取舍

- **投影转调相机**：以后若改成 C# 画贴图（spec 2.5 的"后路"），这两个方法体要重写（约 40 行）。现在不预留模块——没有第二个实现就不造缝。
- `m_Zoom` 默认 `3.667` 是"7 行"反推出来的，不是整数；**改行数就改它**，行数公式见 spec 2.2。
- `m_ViewMargin` 显式钉死为 1.1：它直接缩放可视范围（基值 = 半深 × margin），跟主视图共用直觉会静默改变"看几行"。

## Comments

- 决策依据见 `../spec.md` 第 2.2 / 2.3 / 2.5 节；数值全部按 `sw.unity` 的 `o=1, s=1` 计算。

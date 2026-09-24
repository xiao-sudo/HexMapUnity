# 03 — 打开大图时以队伍格为中心

**What to build:** `.scratch/gvg-hex-map/issues/09` 要求"小地图**打开时**以当前队伍所在 Hex 为中心"。这里说的"打开的大图"就是现有的俯视模式，所以这条等于：**给俯视模式一个"带着焦点格打开"的入口**。

**Blocked by:** 无

**Status:** ready-for-agent

## 范围

- [ ] `MapViewModeSwitcher` 增加焦点格入口（如 `FocusCell(HexCoord)`，或在打开前推入一个待用焦点格）：进俯视时先 `FocusOn(格)` 再 `SetZoomImmediate(m_FocusZoom)`
- [ ] 现有 `m_FocusTarget`（`Transform`）那条路径**行为不变**，两条路径不互相覆盖（后设置者生效，或明确二选一 —— 在实现时定，并写进注释）
- [ ] 焦点格在地图外 ⇒ `LogWarning`（复用现有 `FocusFailureMessage` 的措辞风格）**但模式照常切换**，与 `m_FocusTarget` 的现有行为一致
- [ ] 复用 `OrthographicMapCamera.FocusOn` 的夹取，**不新写夹取**

## 验收

- [ ] EditMode（扩 `MapViewModeSwitcherTests`）：推入中央格 ⇒ `m_MapCamera.Center ≈ 该格的局部平面坐标`（误差 1e-2）
- [ ] EditMode：推入最外圈格 ⇒ 中心被夹取，且偏离方向正确（`Center` 小于该格坐标）
- [ ] EditMode：地图外焦点格 ⇒ 模式切换仍完成、有 warning、相机未被移动
- [ ] EditMode：**不推焦点格时行为与现在完全一致**（回归）
- [ ] 人工（`sw.unity`）：按地图按钮打开大图，画面中心是队伍所在格；退出后常规相机完全还原
- [ ] C# 编译通过

## 已知取舍

- 焦点格与 `m_FocusTarget` 同时存在时的优先级要在实现时钉死并写进注释；两条路径的语义区别是"推格"与"跟 Transform"。
- 打开大图用的是 `m_FocusZoom`（默认 3），与小地图的 `m_Zoom`（3.667）**是两个独立的值** —— 它们的用途不同（大图是玩家要看的，小地图是态势），不要合并。

## Comments

- 决策依据见 `../spec.md` 第 6 节：`09` 的"打开时以队伍格为中心"由本票覆盖，"拖动浏览"不在本次范围。

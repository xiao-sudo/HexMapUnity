# 03 — 打开大图时以队伍格为中心

**What to build:** `.scratch/gvg-hex-map/issues/09` 要求"小地图**打开时**以当前队伍所在 Hex 为中心"。这里说的"打开的大图"就是现有的俯视模式，所以这条等于：**给俯视模式一个"带着焦点格打开"的入口**。

**Blocked by:** 无

**Status:** ready-for-agent

> ⚠️ **前置变更**：`MapViewModeSwitcher` 已于「视图模式状态机」重构中**彻底移除焦点与缩放**（`m_FocusTarget` / `m_FocusZoom` / `m_MapCamera` 三个槽与 `FocusIfRequested` 都没了），见 `.scratch/map-view-mode-state-machine/spec.md` 第 4 节。本票因此不再是"给切换器加一个焦点格入口"，而是"给**调用方**加一条'先对准、再 `Toggle()`'的路径"。下面的范围与验收已按新结构改写。

## 范围

- [ ] 「打开大图时以队伍格为中心」的实现落点是**打开地图的调用方**（未来接在按钮上的那个组件），不是 `MapViewModeSwitcher`。切换器在重构后只剩 `Configure` / `ApplySerializedMode` / `Toggle` / `CurrentMode`
- [ ] 调用方在 `Toggle()` **之前**做**一次** `OrthographicMapCamera.TryZoomToCell(格, 档位)`。不要拆成"先 `FocusOn(格)` 再设档位"：`FocusOn` 按**旧档位**的范围夹取，边缘格会被永久夹偏。原先切换器里那条 `TryZoomToPoint` 路径与它同源，现已不存在
- [ ] 打开大图用的档位值（原 `m_FocusZoom`，默认 3）**跟随本票搬到调用方**，成为调用方自己的配置
- [ ] 焦点格在地图外 ⇒ 调用方 `LogWarning`（可复用原 `FocusFailureMessage` 的措辞风格）**但模式照常切换**
- [ ] 复用 `OrthographicMapCamera` 的夹取，**不新写夹取**

## 验收

- [ ] EditMode（扩 `HexMap.Sample.Tests.EditMode`，夹具按重构后的公开 API 写）：推入中央格 ⇒ `mapCamera.Center ≈ 该格的局部平面坐标`（误差 1e-2）
- [ ] EditMode：推入最外圈格 ⇒ 中心被夹取，且偏离方向正确（`Center` 小于该格坐标）
- [ ] EditMode：地图外焦点格 ⇒ 模式切换仍完成、有 warning、相机未被移动
- [ ] EditMode：**不推焦点格时行为与现在完全一致**（回归）
- [ ] 人工（`sw.unity`）：按地图按钮打开大图，画面中心是队伍所在格；退出后常规相机完全还原
- [ ] C# 编译通过

## 已知取舍

- 档位值的归属：原 `m_FocusZoom` 是切换器的序列化字段，重构后切换器不再有它 ⇒ 本票在调用方引入一个同义的档位配置。不要因此把档位塞回切换器。
- 打开大图用的档位（3）与小地图的 `m_Zoom`（3.667）**是两个独立的值** —— 它们的用途不同（大图是玩家要看的，小地图是态势），不要合并。

## Comments

- 决策依据见 `../spec.md` 第 6 节：`09` 的"打开时以队伍格为中心"由本票覆盖，"拖动浏览"不在本次范围。
- 本票的范围与验收已按 `.scratch/map-view-mode-state-machine/spec.md`（视图模式状态机重构）改写：焦点不再经过切换器，改为调用方在 `Toggle()` 之前一次调用 `TryZoomToCell`；档位配置随之下沉到调用方。

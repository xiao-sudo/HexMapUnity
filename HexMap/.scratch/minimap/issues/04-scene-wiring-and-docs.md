# 04 — `sw.unity` 接线与文档

**What to build:** 把 02 / 03 做出来的东西接进 `sw.unity`，并把这一版决策写进 `docs/implementation/`。

**Blocked by:** 02 — 小地图控件本体; 03 — 打开大图时以队伍格为中心

**Status:** ready-for-agent

## 范围

- [ ] 扩展一键接线工具 `TwoModeSampleSceneWirer`（菜单 `Tools/Hex Map/Wire Two-Mode Sample Scene`）：新建小地图相机 + 第三个 `OrthographicMapCamera` + **独立** `OrthographicMapLayerSettings` + `MinimapView` + `RawImage` 控件，并接好 `MapViewModeSwitcher` 的新槽（小地图相机、焦点格入口所需的引用）
- [ ] 工具保持**幂等**：已存在的复用，不重复添加；报告改了什么（沿用现有 `changes` / `notes` 机制）
- [ ] `docs/implementation/minimap.md`：用法、数值表（spec 2.2）、排查表（spec 第 4 节）
- [ ] 在 `.scratch/gvg-hex-map/issues/09` 的 `## Comments` 记录覆盖与未覆盖范围（spec 第 6 节）

## 验收

- [ ] 工具连跑两次：第二次报告"nothing to change"（或只报告真正缺失的）
- [ ] 工具的报告里包含"没有 `OrthographicMapCamera` / 没有 `HexMapView` / mask 不含 cell 层"这类前置条件检查
- [ ] 文档含四项：① `zoom ↔ 行数` 公式与 `sw.unity` 的数值表；② **方形控件装不下全图**的结论与原因；③ 重烤时序（enable 一帧 → disable，下一帧生效）；④ 点击隔离（小地图是 UI，点击不进 `MapClickDispatcher`）
- [ ] 人工验收清单跑通并记进文档
- [ ] C# 编译通过

## 已知取舍

- 工具会**新建**一台小地图相机与一个 UI 控件，这与现有工具"只补组件与引用、不建相机"的克制不同：因为小地图的关键部件（相机、RT 控件）在场景里**完全不存在**，没有可复用的对象。工具仍**不碰**已有相机的栈与 render type。

## Comments

- 场景侧现状（侦察）：`sw.unity` 的 `HexMapView` 是 `o=1, s=1, MeshRenderer`，与 `map.unity` 的 `o=2, s=0.8, DrawMeshInstanced` 不同 ⇒ 所有数值按 `sw.unity` 算（spec 第 1 节）。

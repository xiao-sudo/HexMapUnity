# 05 — 运行时组合场景配置和逻辑数据表

**要构建的内容：**  
运行时使用场景中的 `HexMapView` 配置创建地图拓扑和渲染布局，并使用 MapId 对应的逻辑数据表创建 Plot 和其他运行时逻辑。运行时不依赖 `GvgMapAuthoringAsset` 或 GVG 编辑窗口。

**Blocked by:** 01 — 让 HexMapView 成为场景地图配置来源；04 — 使用绑定地图拓扑校验逻辑表格数据。

**Status:** done

## 范围决策（2026-09-15 grilling 确认）

- **Q8（关键）：** MapId 是外层概念，本层不关心。运行时组合管线（数据 → 地图 + PlotRegistry）不承担「按地图身份查表」，也不给 `HexMapView` 加 MapId；查表与注册表由未来外层 bootstrap 处理。
- **Q15（关键）：** 不引入 `IGvgMapTableProvider` 接口。永久接缝 = `GvgPlotRuntimeData → GvgMapRuntimeComposer → PlotRegistry`；真实数据对接 = 替换/删除临时适配器 + 新增真实表 loader。
- 其余 Q1–Q7、Q9–Q14 均按 grilling 推荐执行；Q12（缺失 Camp 引用）→ `Debug.LogError` + 降级为普通 Plot；Q13（每格最多一个 open 行）→ Composer 显式校验。

- [x] ~~运行时可以根据地图身份找到对应的逻辑数据表。~~ 按 Q8 决策：MapId 是外层概念，本层不做查表；由未来外层 bootstrap/注册表负责。
- [x] RuntimeHexMap 使用 `HexMapView` 的 Radius 和布局配置创建。
- [x] 表格中的 Plot 数据可以转换为现有运行时 Plot 和 PlotRegistry 结构。
- [x] 表格中的 HexId 超出场景拓扑时，运行时显示明确的校验错误。
- [x] Plane、Orientation、OuterRadius、SecondaryScale 和 Origin 正确影响运行时渲染。
- [x] 运行时不需要依赖编辑器 Asset 或 GVG 编辑窗口。

## Comments

### 决策记录（2026-09-15 grilling）

- 运行时不依赖 `GvgMapAuthoringAsset` / GVG 编辑窗口；运行时配置表的格式和数据来源未定，先用模拟数据源驱动运行时管线，未来再对接真实运行时数据。
- Q8：MapId 暂时忽略，视为外层概念；本层只管用数据生成地图，不关心 MapId，不实现查表。
- Q15：扩展点 = 不加 `IGvgMapTableProvider` 接口；永久接缝 `GvgPlotRuntimeData → GvgMapRuntimeComposer → PlotRegistry`。
- Composer 校验「每格最多一个 open（Start==0）行」（Q13）；空 rows → true + 空 registry。
- Camp 未归属 → 回退到自身 PlotId 的逻辑放适配器（Editor-only），Composer 保持表格忠实。
- 缺失 Camp 引用 → `Debug.LogError` + 当普通 Plot（Q12）。

### 实现记录（2026-09-15）

- 新增 `GvgPlotRuntimeData`（`HexMap.Gvg` 运行时程序集，不可变 DTO）：PlotId / HexIds / PlotType / GenerationType / Start / End / AffiliatedCampId；便捷构造 `(plotId, hexIds, plotType)` 默认 start=0 / end=-1。
- 新增 `GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error)`：纯静态、无状态，只消费 DTO + 地图，与数据来源无关。
  - 校验：null map/rows、行内 null、重复 PlotId、空 HexIds、行内重复 HexId、HexId 越界、每格最多一个 open 行（Start==0）、构造 `Plot`/`PlotRegistry` 异常捕获 → 明确错误信息。
  - 映射：Obstacle → `BlockingState.Blocked`；Start==0 → `PlotState.Open`，否则 `NotOpen`；AffiliatedCampId 透传。
- 新增临时模拟 provider `GvgMapAuthoringRuntimeAdapter`（Editor-only `HexMap.Gvg.Authoring` 程序集）：`ToRuntimeData(asset)` 把 `GvgMapAuthoringAsset` 转成 DTO 行，含 Camp 未归属回退 + 缺失 Camp `Debug.LogError`。
- `GvgMapAuthoringUtility.CreateRuntimePlots` 改为：Validate → 适配器 → `TryCompose` → `new List<Plot>(registry.Plots)`；删除重复语义与 `using UnityEngine`。
- 组装/寻路验证（`GvgMapRuntimeComposerTests`，13 条）：单格/多格寻路、Obstacle 阻断、NotOpen 阻断、时间层保持每格一个 open、越界/重复 PlotId/重复 HexId/双 open 行报错、空 rows、null、旧语义对齐（Adapter+Composer ≡ legacy CreateRuntimePlots）、`HexMapView` 布局 → 世界坐标路径点。
- 验证：`HexMap.Gvg.Tests.EditMode` 全量回归 62/62 通过（`GvgMapAuthoringTests` 17、`GvgMapBindingResolverTests` 7、`GvgMapExcelRoundTripTests` 13、`GvgMapRuntimeComposerTests` 13、`PlotAndBattlefieldRulesTests` 12）。

### 扩展点：对接真实运行时数据

当前数据来源是 Editor-only 的模拟适配器。未来接真实表时：

1. 在**运行时程序集**（如 `HexMap.Gvg` 或新增 `HexMap.Gvg.Data`）新增真实表 loader：读取表 → 产出 `GvgPlotRuntimeData` 列表。
2. 调用 `GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error)` 得到 `PlotRegistry`，即可寻路。
3. 删除/替换临时适配器 `GvgMapAuthoringRuntimeAdapter`；`GvgMapAuthoringUtility.CreateRuntimePlots`（Editor 专用）可随之退役。
4. MapId → 表 的查找/注册表、以及按 MapId 实例化地图，属于未来外层 bootstrap（本层刻意不实现）。

不引入 `IGvgMapTableProvider` 接口；永久接缝就是 `GvgPlotRuntimeData → GvgMapRuntimeComposer → PlotRegistry`，真实数据只要产出 DTO 即可接入。

### 补充记录（2026-09-16）：OwnerFactionId 透传

- `GvgPlotRuntimeData` / `GvgMapAuthoringRuntimeAdapter` / `GvgPlotAuthoringData` / `GvgMapRuntimeComposer` 随 09 新增 `OwnerFactionId`（int，默认 -1 中立）透传字段，运行时 Plot 携带 `OwnerFactionId`；Composer 只透传不推导。
- 详见 09 — 势力 id 改为 int 并删除 FactionId 枚举。

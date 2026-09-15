# 04 — 使用绑定地图拓扑校验逻辑表格数据

**要构建的内容：**  
Excel 导入和表格导出继续只处理 GVG 逻辑数据，同时使用绑定的场景地图拓扑进行校验。显示配置不进入逻辑表格结构。

**Blocked by:** 02 — 建立 GVG 逻辑 Asset 与场景地图的绑定。

**Status:** wontfix

> **已弃用（2026-09-15）：** 本 ticket 不再作为独立交付项。拓扑校验（导出前每个 HexId 属于绑定拓扑、缺绑定/拓扑不一致阻止导入导出）已由 02/03 的绑定门控实现；两条新需求（导出 Excel 与导入格式完全一致、冗余列信息记录并在导出时回写）移入 **08 — Excel 往返保真**。

- [ ] 导出的表格只包含 HexId、PlotType、时间层、归属关系等逻辑字段。
- [ ] 导出前校验每个逻辑 HexId 是否属于绑定的 HexMapView 拓扑。
- [ ] 缺少绑定或拓扑不一致时阻止导出，并显示可定位的错误。
- [ ] Excel 导入后更新 Asset，并刷新绑定场景中的逻辑预览。
- [ ] 导入导出继续保持现有逻辑表格格式。
- [ ] 没有场景绑定时，原有独立 Asset 导入导出能力仍可用。

## Comments

### 决策记录（2026-09-15 grilling）

经 grilling 确认，04 的范围被重组：

- 拓扑校验 checklist 由 02/03 的绑定门控覆盖，不再单独立项。
- 往返保真（按列名导入、全量重建导出、冗余列记录与回写）另开 ticket 08。
- 关键决策点：
  - 导出 xlsx 采用全量重建，非 patch 原文件；数据单元格样式/合并/列宽/其他 sheet 明确放弃。
  - 导入按列名读取，不对列序号做假设；逻辑列名常量：`ID / Coordinates / Type / GridType / Safe / Start / End`。
  - 冗余数据存进 `GvgMapAuthoringAsset`，每次导入整体覆盖；键 = PlotId，`NormalizePlotIds` 重编号时冗余跟随 Plot。
  - 所有列都导出；D（Type）按 Start 重算，F（Safe）回写 AffiliatedCampId（未归属写 -1，表头名保持 Safe）。
  - 表头前 5 行的内容、颜色、批注保留；数据行排序：多格 Plot 在前（组内按 PlotId），单格 Plot 在后（按 PlotId）。
  - CSV 保留作调试用途，与 Excel 格式互不约束。

# 08 — Excel 往返保真（按列名导入 + 全量重建导出 + 冗余列回写）

**要构建的内容：**  
Excel 导入按列名读取（不对列序号做假设）；导出生成与导入格式完全一致的 xlsx（全量重建）；导入时记录对编辑器冗余的列信息，导出时原样回写。CSV 保留作调试用途，与 Excel 格式互不约束。

**Blocked by:** 02 — 建立 GVG 逻辑 Asset 与场景地图的绑定。

**Status:** done

## 设计约束（2026-09-15 grilling 确认）

### 出口与口径

- 新增「Export Excel (.xlsx)」全量重建导出；现有「Export CSV」保留，仅调试用。
- 导出 xlsx 为规范形态：前 5 行表头块 + 排序后的数据区。
- 明确放弃（全量重建的代价）：数据单元格样式、合并单元格、列宽、其他 sheet、表头 5 行以外的批注。

### 按列名读取（取代现有按列字母 A/C/D/E/F/I/J 的读法）

- 导入在前 5 行内定位列名行（含 `Coordinates` 的那一行），按 列名 → 列号 建立映射，数据行一律按名字取值。
- 代码中删除所有列字母/列号假设。
- 逻辑列名常量（策划约定名字不变，可扩展；新增逻辑列需同步改编辑器逻辑）：

  | 逻辑字段 | 表头名 |
  | --- | --- |
  | PlotId | ID |
  | HexIds | Coordinates |
  | GenerationType | Type |
  | PlotType | GridType |
  | AffiliatedCampId | Safe |
  | Start | Start |
  | End | End |

- 不在常量表中的列 = 冗余列：记录「列名 + 内容」，导出时保持名字和内容原样回写；新冗余列在导入时自动发现，不需要改编辑器逻辑。
- 逻辑列缺失 → 导入硬失败并报「缺少列：<列名>」；逻辑列名重复 → 报错。

### 冗余记录与存储

- 冗余数据存进 `GvgMapAuthoringAsset`（Editor-only 程序集，可序列化），每次导入整体覆盖。
- 记录内容：表头块（前 5 行）文本 + 单元格填充色/字体色 + 批注；列名行（列顺序）；每列类型（int / int[] / string）；数据行各列内容（逻辑列 + 冗余列）。

### 键与行身份

- 键 = PlotId。`NormalizePlotIds` 重编号时冗余数据跟随 Plot（单格→多格、多格→单格、时间层变首层均跟随）→ `NormalizePlotIds` 需回传 旧→新 PlotId 映射供冗余表重挂。
- 编辑器新增 Plot：冗余列按类型填默认（int→0、int[]→[]、string→""）。
- 编辑器删除 Plot：对应行不再导出。

### 列处理

- 所有列都导出，不能因编辑器只编辑部分列而忽略其他列。
- 逻辑列从 Asset 回写：ID=PlotId、Coordinates=HexIds、GridType=PlotType、Start、End；Type 按 Start 重算（Start==0 ? 0 : 1）；Safe=AffiliatedCampId（未归属写 -1，表头名保持 Safe）。
- 冗余列按最近一次导入的记录原样回写。

### 排序

- 数据行排序：多格 Plot 在前（组内按 PlotId 升序），单格 Plot 在后（按 PlotId 升序）；编辑器新建的行按该规则插入。

### 校验与归属

- 导出前每个逻辑 HexId 属于绑定拓扑、缺绑定/拓扑不一致阻止导入导出：由 02/03 绑定门控覆盖，本 ticket 只验证门控生效，不重复实现。
- 04 已弃用（wontfix），本 ticket 承接其往返保真部分。

## Requirements

- [x] 导入改为按列名读取，删除现有按列字母（A/C/D/E/F/I/J）的假设。
- [x] 逻辑列名常量表 + 缺失列/重复列报错。
- [x] 冗余信息（表头 5 行文本+颜色+批注、列名行、列类型、数据行各列内容）记录进 Asset，每次导入整体覆盖。
- [x] `NormalizePlotIds` 回传旧→新 PlotId 映射，冗余数据跟随重挂。
- [x] 新增 Plot 冗余列按类型填默认；删除 Plot 不再导出。
- [x] Export Excel (.xlsx) 全量重建：前 5 行表头块 + 排序后的数据区。
- [x] 逻辑列回写规则：Type 按 Start 重算、Safe 写 AffiliatedCampId（-1 表示未归属）。
- [x] 冗余列按记录原样回写，列名保持。
- [x] CSV 保留作调试用途，与 Excel 格式互不约束。
- [x] 样例 fixture xlsx + 往返断言测试（import(export(import(x))) 稳定；列名/内容一致；列顺序变化不影响导入；新冗余列自动识别）。

## Verification

- 样例 fixture 往返测试通过。
- 导出的 xlsx 在 Excel/WPS 中可正常打开，前 5 行内容、颜色、批注与导入一致。
- 列顺序变化不破坏导入；新增冗余列自动识别并回写。
- 绑定门控（02/03）在缺绑定/拓扑不一致时仍阻止导入导出。
- 相关 EditMode 项目编译 0 错误。

## Comments

- 字体：全表使用微软雅黑，表头加粗（xlsx 全量重建的样式表按 字体名+加粗+颜色 生成）。
- 排序：多格 Plot 最前；单格 Plot 按 PlotType 升序，Normal 类型放到最后，同类型按 PlotId。
- 导出位置：Export Excel / Export CSV 不再写入固定 `Assets/HexMap/Gvg/Exports/...` 目录，改为 `EditorUtility.SaveFilePanel` 由用户选择目录与文件名（默认名 `GVGMap_<MapId>.xlsx/.csv`）。

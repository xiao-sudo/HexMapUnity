# 10 — 地图配置编辑、预览与导出

**What to build:** 设计师可以在 Unity Editor 中创建和调整 GVG 地图配置，并导出运行时可直接加载的地图数据。

**Blocked by:** 02 — 运行时 Hex 地图与格子拾取; 05 — GVG 地块模型与战场状态规则.

**Status:** ready-for-human

- [x] 编辑器可以创建地图范围并将 Hex 分配给 Plot，而不要求直接维护策划原始坐标表。
- [ ] 编辑器可以配置地块类型、状态、归属槽位、可通行、可占领、代表格和可停驻格。
- [x] 预览显示坐标、地块 ID、类型、阻碍、大营、城池和归属色，并与运行时数据一致。
- [x] 导出结果可以被运行时地图生成和 GVG 规则加载。

## Refined design contract

本节记录经方案拷问后确认的执行契约。它覆盖顶部旧验收项中关于代表格、可停驻格、归属色、状态和阻碍字段的表述：第一版地图 Authoring 只负责地图拓扑和 Plot 类型，动态状态、阵营归属和战场初始化由运行时逻辑负责。

### Scope

本 issue 交付：

- `GvgMapAuthoringAsset` 和可序列化 authoring 数据。
- Unity EditorWindow 入口和 SceneView 编辑/预览。
- CSV 导出和导出目录 `README.md` 生成。
- 导出前硬校验。
- 纯 C# / EditMode 测试覆盖 authoring 数据规则、CSV 行生成和必要的运行时模型调整。
- 对 issue 05 追加 amendment，说明 `RepresentativeCell` 删除和 PlotId 规则变化。

本 issue 不交付：

- CSV 导入。
- 运行时 CSV parser/loader。
- 完整连通性、重要地块可达性、阻碍切断地图和寻路调试；这些属于 issue 11。
- Plot 上的模型、Prefab、Addressables key 或正式战场表现资源。
- 运行时动态阵营分配、占领、状态流转和战斗初始化逻辑。

### Runtime contract changes required by this issue

- 删除 `RepresentativeCell` 概念。多格 Plot 的所有 Hex 都代表该 Plot 的一部分。
- 多格 Plot 后续如果需要 UI 标签、镜头聚焦或小地图文字锚点，应从 Plot 内所有 Hex 的世界中心派生，例如几何平均点或包围中心，而不是配置一个代表格。
- `Plot` 构造函数调整为：

```text
Plot(
    int plotId,
    IReadOnlyList<HexCell> cells,
    PlotType plotType,
    PlotState plotState,
    FactionId ownerFaction,
    OwnershipMode ownershipMode,
    BlockingState blockingState)
```

- 运行时 `Plot` 允许负数 `PlotId`。唯一性仍由 `PlotRegistry` 保证。
- Authoring 层强制编号规则：
  - 单格 Plot 的 `PlotId` 必须等于它唯一 Cell 的 `HexId`。
  - 多格 Plot 的 `PlotId` 必须小于 0。
  - 多格 Plot 自动编号从 `-1` 开始递减。

### Authoring assets

Source of truth 是 Unity `ScriptableObject`，不是 CSV。

建议新增程序集：

- `Assets/Scripts/HexMap/Gvg/Authoring`：运行时可见的 authoring 数据类型和纯数据转换。
- `Assets/Scripts/HexMap/Gvg/Editor`：EditorWindow、SceneView 交互、`AssetDatabase`、`Handles` 和 CSV 文件写出。

`GvgMapAuthoringAsset` 第一版字段：

```text
MapId       // string，默认等于 asset name，允许手动改
Radius
Orientation
Plane
OuterRadius
Plots[]
```

`GvgPlotAuthoringData` 第一版字段：

```text
PlotId
HexIds      // List<int>
PlotType
```

不在 authoring asset 中保存 `PlotState`、`OwnerFaction`、`OwnershipMode`、`BlockingState`、`RepresentativeHexId`、可停驻格、模型 key 或 Prefab 引用。

### Default initialization rules

创建或重建地图范围后，自动为每个 Hex 生成一个默认单格 Plot：

```text
PlotId = HexId
HexIds = [HexId]
PlotType = Normal
```

Editor 预览和 authoring-to-runtime 投影使用以下默认运行时规则：

```text
PlotState = Open
OwnerFaction = Neutral
OwnershipMode = Capturable
BlockingState = Passable
```

类型覆盖规则：

```text
PlotType.Obstacle -> BlockingState.Blocked
PlotType.Camp -> OwnershipMode.Fixed
```

`Camp` 的具体阵营不由地图表配置；由运行时战场初始化逻辑绑定参战方槽位。

### Editing workflow

工具入口为 EditorWindow + SceneView。SceneView 使用 `Handles` 绘制和交互，不生成持久 GameObject。

SceneView 工具模式：

- `Select Plot`：点击任一 Hex 选中其所属 Plot，并在 EditorWindow/Inspector 中显示该 Plot。
- `Paint Add`：将点击或拖拽经过的 Hex 加入当前 Plot。
- `Paint Remove`：从当前多格 Plot 移除 Hex。

第一版不提供 `Set Representative`，因为代表格概念已删除。

批量操作：

- 支持 SceneView 多选 Hex。
- 支持 `Merge To Multi-Plot`，将选中的 Hex/Plot 合并到当前主 Plot。
- 支持将 HexId 列表粘贴到当前 Plot，便于从 Excel 或 CSV 辅助修正。

归属维护规则：

- 一个 Hex 任意时刻最多属于一个 Plot。
- `Paint Add` 可以抢占其他 Plot 的 Hex：从原 Plot 移除该 Hex；原 Plot 变空则删除；原 Plot 从多格变单格时自动把 `PlotId` 改为剩余 Hex 的 `HexId`。
- 从多格 Plot `Paint Remove` 非最后一个 Hex 时，被移出的 Hex 自动恢复为默认单格 Plot。
- 禁止通过 `Paint Remove` 移除 Plot 的最后一个 Hex；删除 Plot 必须使用专门的 `Delete Plot` 操作。
- `Delete Plot` 自动把该 Plot 的所有 Hex 恢复为默认单格 Plot。
- 当 Plot 的 Cell 数量跨过单格/多格边界时，编辑器自动修正 `PlotId`：单格改为唯一 `HexId`，多格若当前非负则分配空闲负数。
- 多格负数 PlotId 分配优先复用最接近 0 的空闲负数，例如 `-1`、`-2`、`-3`。
- 修改 `Radius` 必须弹确认。缩小半径会删除地图外 Plot/Hex；扩大半径会为新增 Hex 自动生成默认单格 Plot。
- 创建、刷格、删除、合并、半径修改和属性修改都必须接 Unity Undo/Redo。

### Preview

默认预览：

- 颜色显示 `PlotType`，并突出 `Obstacle`、`Camp`、`SmallCity`、`BigCity` 和 `Capital`。
- 标签默认只显示 `PlotId`。
- `HexId`、坐标和类型名是可开关显示层。
- 未分配 Hex 使用明显错误色，并在错误列表中显示数量和前若干 `HexId`。

第一版不显示动态归属色。归属由运行时初始化，不是 authoring 配置字段。

### CSV export

CSV 是给策划审核和未来导入使用的导出件；本 issue 不实现导入。导出结果应包含足够的地图拓扑和类型信息，供后续运行时加载器或初始化逻辑消费。

导出格式：

- 标准 `.csv`。
- UTF-8 with BOM，优先兼容 Excel 直接打开。
- 英文字段名。
- 枚举字段使用数字值。
- `HexIds` 列使用标准 CSV quoting，精确格式为 `"[1,2,3,4]"`，无空格。

导出路径固定为：

```text
Assets/HexMap/Gvg/Exports/<AssetName>/
```

每次导出覆盖生成：

```text
Map.csv
Plots.csv
Cells.csv
README.md
```

`Map.csv` 列：

```text
MapId,Radius,Orientation,Plane,OuterRadius
```

`Plots.csv` 列：

```text
PlotId,HexIds,PlotType
```

`Cells.csv` 列：

```text
HexId,Q,R,PlotId
```

排序规则：

- 每个 Plot 的 `HexIds` 按 HexId 升序。
- `Plots.csv` 行按 `PlotId` 升序；负数多格 Plot 自然排在非负单格 Plot 前。
- `Cells.csv` 行按 `HexId` 升序。

`README.md` 每次导出覆盖，说明：

- 文件用途。
- CSV 编码和数组字段格式。
- `PlotType` 数字枚举对照。
- `PlotId` 编号规则。
- 默认运行时初始化规则。
- CSV 当前是导出审核件和未来导入源，本 issue 不支持导入。

### Export validation

导出前硬失败条件：

- `PlotId` 唯一。
- `HexId` 存在于当前 `Radius` 生成的地图中。
- Plot 非空。
- Hex 唯一归属。
- 地图范围内所有 Hex 都被 Plot 覆盖。
- 单格 Plot 的 `PlotId == HexId`。
- 多格 Plot 的 `PlotId < 0`。

`Obstacle` 的阻碍语义由默认初始化规则保证，不再校验单独的 `BlockingState` 字段。

### Tests

优先使用纯 C# / EditMode 测试，覆盖：

- 删除 `RepresentativeCell` 后的 `Plot` 构造和现有 GVG 规则测试更新。
- 运行时允许负数多格 `PlotId`。
- 默认全图单格 Plot 生成。
- 单格/多格 PlotId 规则校验。
- 合并、抢占、移除和删除后的全覆盖与唯一归属。
- Radius 扩大/缩小后的 Plot 修正规则。
- CSV row 生成、排序、`HexIds` 数组格式。
- UTF-8 BOM 写出。
- `README.md` 内容包含 `PlotType` 数字枚举对照和默认初始化规则。

不做 SceneView UI 自动化测试；EditorWindow 和 SceneView 交互第一版通过手测验收。


## Comments

### Agent implementation update - 2026-09-10

Implemented the refined authoring contract: ScriptableObject source asset, runtime-visible authoring utilities, EditorWindow + SceneView preview/editing, validation, CSV/README export, Plot runtime contract update, and focused EditMode coverage. The legacy checklist item for configuring status/ownership/representative/standable cells remains governed by the refined design contract: first version stores only topology and PlotType; runtime defaults derive state, ownership, occupancy, and blocking semantics.

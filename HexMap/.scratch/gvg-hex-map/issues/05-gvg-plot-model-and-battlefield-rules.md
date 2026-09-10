# 05 — GVG 地块模型与战场状态规则

**What to build:** 将一个或多个 HexCell 组织为 GVG 操作对象 Plot，并让类型、状态、归属和阻挡统一驱动战场规则。

**Blocked by:** 02 — 运行时 Hex 地图与格子拾取.

**Status:** ready-for-agent

- [ ] 一个 Plot 可以包含一个或多个 HexCell；每个 HexCell 最多属于一个 Plot，并可从任意 Cell 反查 Plot。
- [ ] 代表格和可停驻格分别配置，且两者都能被规则服务独立查询。
- [ ] 支持大营、普通地块、草地、小城、大城、都城和阻碍等地块类型，以及未生成、未开放、中立、已归属和战斗中状态。
- [ ] 归属、开放状态和阻碍能够统一决定地块的可见、可选中、可通行和可进入结果；地图外区域默认不可通行。
- [ ] 通过小型合成地图测试单格地块、多格地块和状态变化行为。


## Refined design contract

> 本节是经过设计审查确认后的实现契约；它覆盖顶部旧验收项中“代表格和可停驻格分别配置”的表述。首版删除固定 `StandableCells` 配置，改为多格 Plot 的全部 Cell 都是潜在进入目标。

### Scope

本 issue 交付：

- `Plot`、`PlotRegistry` 和 Cell-to-Plot 反查模型。
- Plot 类型、生命周期状态、归属、固定归属和 Plot 级阻挡。
- `PlotPathService`：只接受 Plot 作为寻路起点和终点。
- 通用 `HexPathfinder` 的多起点、多目标扩展。
- GVG Plot 路径策略和小型合成地图 EditMode 测试。

本 issue 不交付占领、攻击、驻守、战斗队列、战斗结算、状态转换、编辑器、UI、小地图、Cell 起点入口、正式场景表现或路径移动重算。

### Plot model

`Plot` 是 GVG 规则的聚合对象。一个 Plot 包含一个或多个 `HexCell`；每个 `HexCell` 最多属于一个 Plot。

所有 GVG 规则属性归属 Plot：

```text
PlotId
Cells
RepresentativeCell
PlotType
PlotState
OwnerFaction
OwnershipMode       // Capturable 或 Fixed
BlockingState       // Passable 或 Blocked
```

`HexCell` 只保留基础地图身份（HexId 和坐标），不直接引用 GVG `Plot`，也不持有阵营、类型、状态或阻挡。由 `PlotRegistry` 提供 `PlotId -> Plot` 和 `CellId/HexCell -> Plot` 查询。

每个 Plot 恰好有一个代表格，且代表格必须属于该 Plot。删除固定 `StandableCells`：多格 Plot 的所有 Cell 都是潜在进入目标，路径到达任意合法 Cell 即视为到达 Plot；单格 Plot 的唯一 Cell 同时是代表格和进入目标。

支持的类型为 `Camp`、`Normal`、`Grass`、`SmallCity`、`BigCity`、`Capital` 和 `Obstacle`。Grass 第一版不引入特殊移动规则；开放城池（包括都城）理论上都可作为占领目标。

生命周期状态与归属分开：

```text
PlotState = NotGenerated | NotOpened | Open | Battle
OwnerFaction = 一个阵营值；Neutral 也是合法阵营
```

本 issue 不实现状态转换。寻路把 `Open` 和 `Battle` 视为相同的可寻路状态；`NotGenerated` 和 `NotOpened` 不参与寻路。

`OwnershipMode.Capturable` 表示归属可由后续占领业务改变；`OwnershipMode.Fixed` 表示归属不可由占领业务改变。寻路阵营不等于 Fixed Plot 归属时，不能生成到该 Plot 的路径；归属匹配时可以经过并作为目标。Neutral 按普通阵营处理。

第一版只支持 Plot 级阻挡：Plot 内所有 Cell 共享 `BlockingState`，不支持单个 Cell 的阻挡覆盖。 `PlotType.Obstacle` 必须验证为 `BlockingState.Blocked`；寻路策略只读取阻挡状态，不硬编码类型分支。

### Plot-to-Plot pathfinding

Plot 层首版入口：

```text
FindPath(startPlotId, targetPlotId, movingFaction, result)
```

`PlotPathService` 将起点 Plot 的全部 Cell 作为起点集合，将目标 Plot 的全部 Cell 作为目标集合，再调用通用 `HexPathfinder`。首版不支持 `startCell -> targetPlot`。

通用请求从 `Start + Targets + Policy` 扩展为 `Starts + Targets + Policy`，并保留单起点兼容构造；`ReusablePathRequest` 同步支持可复用的起点集合和目标集合。

一次搜索将所有起点作为 BFS 根节点，距离均为 0；起点不调用 `CanPass`。目标集合由 `CanEnter` 过滤，任意起点到任意合法目标 Cell 可达即成功。起点与目标集合相交时返回零步路径，不调用 `CanPass` 或 `CanEnter`。

`PathResult` 保持纯 Hex 语义：`Cells[0]` 是实际起点，`ReachedTarget` 是实际终点，不增加 PlotId。路径先按步数选择目标，再按目标 `q/r` 选择；同一起点到同一目标的等长路径继续使用固定六邻居顺序。多起点完全等长时，不把起点排序或起点坐标作为业务契约，测试不得依赖具体等长路径形状。

`HexPathfinder` 只理解 HexCell、起点集合、目标集合和 `IHexPathPolicy`，不引用 Plot、阵营、公会、阻挡或战斗。Plot 规则由 Plot 层策略捕获 `movingFaction` 后实现。

### PlotPathPolicy

```text
CanPass(cell)
    PlotState 为 Open 或 Battle
    BlockingState == Passable
    OwnerFaction == movingFaction
```

```text
CanEnter(cell)
    PlotState 为 Open 或 Battle
    BlockingState == Passable
    若 OwnershipMode == Fixed，则 OwnerFaction == movingFaction
    否则允许作为路径终点
```

| Plot 情况 | `CanPass` | `CanEnter` |
|---|---:|---:|
| 己方普通 Plot | 是 | 是 |
| 敌方普通 Plot | 否 | 是 |
| 中立普通 Plot | 否 | 是 |
| 己方 Fixed Plot | 是 | 是 |
| 敌方 Fixed Plot | 否 | 否 |
| 阻挡 Plot | 否 | 否 |
| 未生成/未开放 Plot | 否 | 否 |
| Battle Plot | 按归属和阻挡判断 | 按归属和阻挡判断 |

`CanEnter` 只决定路径能否生成到目标 Cell，不决定到达后的占领、攻击、战斗队列或驻守动作。

### Plot validation

进入通用寻路前，Plot 层或验证层必须拒绝：

- 不存在的 PlotId、空 Plot 或地图外 Cell。
- 一个 Plot 内重复 Cell，或一个 Cell 多重归属。
- 不属于 Plot 的代表格。
- 展开后的起点或目标集合为空。
- 未满足 `Obstacle -> Blocked` 约束的配置。

### Tests

使用小型合成地图的纯 C# EditMode 测试覆盖：

- 单格/多格 Plot、代表格、Cell-to-Plot 反查和重复归属验证。
- 多起点/多目标最短路径、零步相交、无效输入、不可达目标和结果容量不足。
- 起点不调用 `CanPass`，目标只调用 `CanEnter`。
- 多格 Plot 任意 Cell 可作为起点或终点。
- 己方 Plot 可通行；敌方/中立 Plot 不可作为中间通路但开放非 Fixed 时可作为终点。
- 敌方 Fixed Plot 不可作为终点，己方 Fixed Plot 可通行并可作为终点。
- Plot 级阻挡、Obstacle、未生成和未开放 Plot 均不能生成路径。
- Battle 不产生独立寻路分支；起点 Plot 与目标 Plot 相同返回零步路径。
- Plot 配置错误在进入通用 HexPathfinder 前被拒绝。

## Amendment from issue 10 authoring design

Issue 10 的地图编辑与导出方案对本 issue 的运行时模型做出以下修正。后续实现应以本 amendment 为准：

- 删除 `RepresentativeCell` 概念。多格 Plot 的所有 Hex 都代表该 Plot 的一部分；路径目标仍然使用 Plot 内全部 Cell。
- 如果表现层需要 Plot 锚点，应从 Plot 内所有 Hex 的世界中心派生，例如几何平均点或包围中心，而不是配置代表格。
- `Plot` 构造函数不再接收 representative cell，改为：

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

- 运行时 `Plot` 允许负数 `PlotId`，唯一性仍由 `PlotRegistry` 保证。
- Authoring 层采用编号约定：单格 Plot 的 `PlotId` 等于唯一 Cell 的 `HexId`；多格 Plot 使用负数 `PlotId`，从 `-1` 开始递减。该编号约定由 authoring/export validator 强制，不要求通用运行时 `Plot` 构造函数理解 authoring 规则。

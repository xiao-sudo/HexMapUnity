# GVG Plot 附属大营寻路规则

## 目的

为 GVG 地图中的 Plot 增加静态“附属大营”关系，使某些 Plot 只能由指定大营当前所属的阵营进入或通过。

本文只描述寻路访问规则，不描述占领动作、占领条件、Owner 修改、资源消耗或战斗结算。

核心边界是：

- `HexCell` / `HexMap` 只负责底层地图几何和 Hex 身份。
- `Plot` 负责玩法区域及其静态附属关系、开放状态、阻挡状态和当前 Owner。
- `CampId` 是编辑器地图中的静态大营身份。
- `FactionId` 是运行时分配给大营的动态阵营身份。
- `HexPathfinder` 保持通用 Hex BFS，不知道 Plot、Camp 或 Faction。
- `PlotPathPolicy` 组合 Plot 状态、Owner 和附属大营访问规则。

## 代码地图

- `Assets/Scripts/HexMap/Gvg/Plot.cs`
  - 保存 `PlotId`、Hex 集合、Plot 类型、开放状态、OwnerFaction、阻挡状态和 `AffiliatedCampId`。
  - `OwnershipMode` 删除；Plot 类型不再隐式决定寻路授权。
- `Assets/Scripts/HexMap/Gvg/PlotPathPolicy.cs`
  - 实现 `IHexPathPolicy`。
  - 通过 `ICampFactionResolver` 将静态 `CampId` 解析为当前 `FactionId`。
  - 保持 `CanPass` 与 `CanEnter` 的现有语义，并追加附属阵营判断。
- `Assets/Scripts/HexMap/Gvg/PlotPathService.cs`
  - 持有 `PlotRegistry`、`HexPathfinder` 和 Camp/Faction 解析器。
  - 构造时创建一次 `PlotPathPolicy`，每次寻路仅更新移动阵营，避免热路径分配。
- `Assets/Scripts/HexMap/Gvg/PlotRegistry.cs`
  - 保持 Hex 到当前开放 Plot 的查询职责，不承载 Camp 运行时状态。
- `Assets/Scripts/HexMap/Gvg/Authoring/GvgPlotAuthoringData.cs`
  - 序列化 `AffiliatedCampId`，普通 Plot 使用 `-1` 表示无附属大营。
- `Assets/Scripts/HexMap/Gvg/Authoring/GvgMapAuthoringUtility.cs`
  - 将编辑器字段投影到运行时 Plot。
  - 校验 CampId 引用和时间层约束。
  - 归一化多格 PlotId 后，使用旧 CampId 到新 PlotId 的映射同步更新所有 AffiliatedCampId 引用。
- `Assets/Tests/EditMode/HexMap/Gvg/PlotAndBattlefieldRulesTests.cs`
  - 覆盖运行时 Plot 和路径策略。
- `Assets/Tests/EditMode/HexMap/Gvg/GvgMapAuthoringTests.cs`
  - 覆盖编辑器数据、投影、校验和 PlotId 规范化。

## 运行时流程

`PlotPathService.FindPath` 仍然以 Plot 的所有 Hex 作为多源、多目标 BFS 的起点和目标：

```text
PlotPathService
    -> 根据 startPlotId / targetPlotId 获取 Plot
    -> 收集两个 Plot 的 HexCell
    -> 复用 PlotPathPolicy，仅更新 movingFaction
    -> HexPathfinder.FindPath

HexPathfinder
    -> 起点：已在起点，不检查 CanPass
    -> 中间 Hex：调用 CanPass
    -> 目标 Hex：调用 CanEnter，不继续扩展
```

一个多 Hex Plot 的所有 Hex 共享同一组 Plot 级访问规则。寻路图仍然是 Hex 图，Hex 不保存玩法归属字段。

## 数据模型

### CampId 与 FactionId

`CampId` 和 `FactionId` 不可互换：

```text
CampId
    地图编辑器中的静态大营身份
    等于对应 PlotType.Camp Plot 的稳定 PlotId
    地图设计完成后保持稳定

FactionId
    运行时阵营身份
    由 Camp 状态系统动态分配
    例如左下角 Camp 可以在不同运行时属于不同阵营
```

一个 `PlotType.Camp` Plot 对应一个 Camp。一个 Camp 可以有多个附属 Plot，但只有一个 Camp 本体 Plot。

附属关系示例：

```text
Camp Plot:
    PlotId = 12000
    AffiliatedCampId = 12000

Attached Plot A:
    AffiliatedCampId = 12000

Attached Plot B:
    AffiliatedCampId = 12000
```

普通 Plot 使用 `AffiliatedCampId = -1`。

附属 Plot 可以初始未占领：

```text
AffiliatedCampId = 有效 CampId
OwnerFaction     = FactionId.Neutral
```

本次寻路规则不修改 Owner，也不判断占领条件。

### Camp/Faction 解析

寻路层依赖一个外部运行时解析器：

```csharp
public interface ICampFactionResolver
{
    bool TryGetFaction(int campId, out FactionId factionId);
}
```

解析器只回答 Camp 当前属于哪个阵营，不拥有 Plot，也不执行占领逻辑。

有效 CampId 但当前没有分配 Faction 时，附属 Plot 对所有阵营不可访问。

编辑器配置中不存在的 CampId 属于配置错误：记录错误后按普通 Plot 处理。编辑器校验应尽量提前发现这类错误。

## 寻路规则

附属关系只是在现有 Plot 规则上追加一个条件。

```csharp
bool affiliationAllowed =
    plot.AffiliatedCampId == -1
    || campFactionResolver.TryGetFaction(
           plot.AffiliatedCampId,
           out var campFaction)
       && campFaction == movingFaction;
```

最终规则：

```csharp
CanPass =
    plot.IsOpenForPathfinding
    && plot.BlockingState == BlockingState.Passable
    && plot.OwnerFaction == movingFaction
    && affiliationAllowed;
```

```csharp
CanEnter =
    plot.IsOpenForPathfinding
    && plot.BlockingState == BlockingState.Passable
    && affiliationAllowed;
```

这意味着初始未占领附属 Plot 的访问行为为：

```text
指定 Camp 当前所属阵营：
    CanEnter = true
    CanPass  = false，直到 OwnerFaction 变为该阵营

其他阵营：
    CanEnter = false
    CanPass  = false
```

`CanEnter` 和 `CanPass` 都是无副作用查询。进入后由外围系统决定是驻扎还是占领，但本次实现不包含这些动作。

## 外部配置字段映射

- GVGMap.xlsx 的 F 列外部名称是 Safe：Safe=0 映射为内部 AffiliatedCampId=-1，Safe>0 映射为对应的 CampId。
- 第一版 CSV 导出继续使用内部字段名 AffiliatedCampId。
- 当前版本不增加 Excel 导出；后续增加 Excel 导出时，才将内部值写回 xlsx 的 F 列 Safe。

## 编辑器与稳定 ID 约束

- `AffiliatedCampId` 写入 `GvgPlotAuthoringData`，并由 `Clone` 保留。
- 带多个时间层的单 Hex Plot 不配置附属大营；校验应拒绝这类配置。
- Camp Plot 的 PlotId 仍按现有 PlotType ID 规则归一化；如果发生重编号，`NormalizePlotIds` 必须同步更新引用它的 `AffiliatedCampId`。
- 编辑器合并、拆分、删除 Camp Plot 时，必须显式处理附属引用；本次实现至少记录错误并阻止产生无法解析的配置。
- 多 Hex Plot 的 ID 仍遵守现有 PlotType ID 区间规则，Camp 引用不阻止重编号，但引用必须跟随新 ID 更新。

## 约束与取舍

- 不把 `AffiliatedCampId` 放进 `HexCell`；Hex 是底层地图概念。
- 不在 `HexPathfinder` 中加入 Plot、Camp 或 Faction 依赖。
- 不通过 `PlotType.Camp` 隐式推导访问权限。
- 不保留 `OwnershipMode` 的兼容分支；删除后，访问授权统一由 `AffiliatedCampId` 和运行时 resolver 表达。
- 不扩展 `PathResult` 的失败原因；寻路失败继续使用当前 `NoReachableTarget` 语义。
- 不在本次实现占领逻辑、Owner 状态变更或 Camp 消灭逻辑。

## 扩展点

- 若未来出现一个 Plot 允许多个 Camp，应将单个 `AffiliatedCampId` 抽象为授权集合或独立访问策略；当前版本不预留多授权数据结构。
- 若未来需要临时通行证或事件解锁，应扩展 `ICampFactionResolver` 之外的访问策略组合，而不是把临时状态写入 Hex。
- 若未来需要区分“无效 CampId”和“有效但未分配阵营”的运行时诊断，应把 resolver 返回值扩展为带状态的解析结果；当前编辑器校验负责过滤无效引用。

## 验证

项目所有者运行 Unity 测试并反馈结果；本次实现不代替项目所有者运行测试。

应重点验证：

- 普通 Plot 的既有 `CanPass` / `CanEnter` 行为不变。
- 指定阵营可以 `CanEnter` 附属 Plot。
- 非指定阵营不能 `CanEnter` 或 `CanPass` 附属 Plot。
- 附属 Plot 的 `CanPass` 仍受 `OwnerFaction` 规则限制。
- Camp 当前 Faction 变化后访问权限变化。
- Camp 尚未分配 Faction 时附属 Plot 对所有阵营关闭。
- 无效 CampId 记录错误并按普通 Plot 处理。
- 多 Hex Plot 的每个 Hex 共享相同附属限制。
- 时间层 Plot 不接受附属配置。
- Camp Plot 重编号后，所有引用它的 AffiliatedCampId 同步为新的 PlotId。

# 09 — 势力 id 改为 int 并删除 FactionId 枚举

**要构建的内容：**  
`Plot` 的 `FactionId` 枚举体系不合理，势力 id 应是一个 `int`，由外围（数据表/服务端）在构造时指定，-1 代表中立（无主）。`HexMap.Gvg` 面上统一使用 `int` 势力 id。

**Blocked by:** 05 — 运行时组合场景配置和逻辑数据表。

**Status:** done

## 范围决策（2026-09-16 grilling 确认）

- Q1：`FactionId` 枚举在整个 `HexMap.Gvg` 面上删除；`Plot.OwnerFactionId` / `PlotPathPolicy.MovingFactionId` / `PlotPathService.FindPath` 参数 / `ICampFactionResolver.TryGetFaction` 的 `out` 统一改为 `int`。
- Q2：-1 代表中立，普通 id 语义；`CanPass` 依然按 `owner == mover` 等式判断，无通配特例。
- Q3：`GvgPlotRuntimeData` / `GvgMapAuthoringRuntimeAdapter` / `GvgPlotAuthoringData` 透传加入 `OwnerFactionId`，默认 -1，给外围数据源留确定接缝。
- Q4：`Plot.OwnerFactionId` 属性，`Plot.NoFactionId = -1` 常量，构造器参数默认 -1，校验 `>= -1`（与 `AffiliatedCampId` 对称）。
- Q5：势力 id 为不透明 id，测试/demo 用 10/20 示例，不暗示 1/2 是稳定约定。
- Q6：`ICampFactionResolver.TryGetFaction` 查不到时 `out` 为 `Plot.NoFactionId`，写入接口注释。
- Q7：DTO 尾部参数 + 字段初始化器兜底，已有 `GvgMapAuthoring.asset` / `GvgMapAuthoring2.asset` 自动得到 -1，不需手动改 YAML。
- Q8：`GvgMapRuntimeComposer` 透传 `row.OwnerFactionId`，不推导；`Plot` 不可变。
- Q9：测试/demo 全量迁移，新增 4 个用例。

## Comments

### 实现记录（2026-09-16）

- `Plot.cs`：删除 `FactionId` 枚举，新增 `NoFactionId = -1` 常量；`m_OwnerFactionId`（int, readonly），构造器在 blockingState 后的可选参数 `ownerFactionId = NoFactionId` + 校验 `>= -1`。
- `PlotPathPolicy.cs` / `PlotPathService.cs`：`MovingFactionId`（int）、`FindPath(..., int movingFactionId, ...)`、`ICampFactionResolver.TryGetFaction(int, out int)`，查不到写 `Plot.NoFactionId`。
- `GvgPlotRuntimeData.cs` / `GvgPlotAuthoringData.cs`：新增 `OwnerFactionId` 属性，尾部可选参数，字段初始化 -1，校验 `>= -1`，`Clone` 同参透传。
- `GvgMapAuthoringRuntimeAdapter.cs` / `GvgMapRuntimeComposer.cs`：透传 `OwnerFactionId` 到运行时 Plot，不推导。
- 测试：`PlotAndBattlefieldRulesTests` 新增 `PlotRejectsOwnerFactionIdBelowNoFactionId` / `PlotDefaultsOwnerFactionIdToNoFactionId` / `PlotPathServiceWithoutResolverRejectsAffiliatedTarget`；`GvgMapRuntimeComposerTests` 新增 `AdapterCarriesOwnerFactionIdThroughToRuntimePlots`。
- 验证：`HexMap.Gvg.Tests.EditMode` 全量 66/66 通过（含新增 4）；多个 .asset 老数据反序列化兜底 -1 中立。
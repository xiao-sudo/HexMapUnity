# GVG 地图运行时总控

**Status:** complete

## Problem Statement

当前 GVG 地图已经具备 HexMap 场景拓扑、外部 `GvgPlotRuntimeData` 快照、Plot 运行时组装、Plot 查询以及 Plot 寻路服务，但这些能力仍由调用方分别创建、串接和持有。调用方需要知道 `HexMapView` 的构建时机、如何把 HexMap 与 Plot 快照组合、如何保留路径服务，以及如何在不破坏运行时状态的前提下重载快照。

这使地图初始化、阵营归属更新和寻路入口分散在多个调用点。外部数据加载完成的时机也与 Unity 场景的 Awake 时机不同，容易出现尚未构建 HexMap、半初始化、或快照失败后错误清空已可用地图的情况。`OwnerFactionId` 同时需要作为外部快照输入和可变的运行时 Plot 状态，但现有 Plot 对该状态只读。

## Solution

提供一个挂载在 Unity 场景中的 GVG 地图运行时总控组件。它在 Awake 中负责驱动并校验 `HexMapView` 的 HexMap 构建；外部数据源只需在完整 `GvgPlotRuntimeData` 快照到达后调用 `TryInitialize(plots)`。

总控将有效快照原子组装为 PlotRegistry 与 PlotPathService，并集中提供 Plot 查询、阵营归属更新和带移动阵营的 Plot 寻路门面。总控不定义路径规则，而是原样委托既有 PlotPathService。快照或几何配置无效时记录 Unity 错误日志并安全失败；成功重初始化时以新快照替换先前运行时状态。

## User Stories

1. As a Unity 场景配置者, I want 在总控组件上指定 HexMapView, so that 地图几何拓扑由唯一的场景来源提供。
2. As a Unity 生命周期调用方, I want 总控在 Awake 中构建 HexMapView, so that 外部快照到达前地图几何已经准备好。
3. As an 外部地图数据加载器, I want 仅传入完整的 GvgPlotRuntimeData 快照进行初始化, so that 我不必组装或持有运行时 Plot 服务。
4. As an 外部地图数据加载器, I want 初始化返回 bool, so that 我可以同步处理成功或失败而不依赖异常控制流。
5. As a 开发者, I want 缺失 HexMapView、几何构建失败或无效快照被明确记录为 Unity 错误日志, so that 场景和数据配置问题可以被定位。
6. As a gameplay caller, I want 首次初始化失败后总控保持未初始化, so that 不会使用半成品地图状态。
7. As a gameplay caller, I want 重初始化失败后继续使用先前成功的地图, so that 一份坏快照不会中断进行中的地图功能。
8. As an 外部地图数据加载器, I want 成功的完整快照覆盖旧的运行时 Plot 状态, so that 外部快照始终是重初始化后的权威来源。
9. As a gameplay system, I want 通过 PlotId 查询运行时 Plot, so that 我可以在不重新解析外部表数据的情况下读取地块信息。
10. As a gameplay system, I want 只长期保存 PlotId 而不保存 Plot 实例, so that 重初始化替换内部地图后不会继续引用过期对象。
11. As a gameplay system, I want 通过总控设置某个 Plot 的 OwnerFactionId, so that 归属的运行时变化有统一入口。
12. As a gameplay system, I want 设置合法且存在的 Plot 即使 OwnerFactionId 未变化也返回成功, so that 阵营同步调用可以保持幂等。
13. As an 外部集成方, I want OwnerFactionId 被当作不透明的 int, so that 势力、无主或其他业务语义由外部系统定义。
14. As a gameplay system, I want 不能绕过总控直接修改查询到的 Plot 归属, so that 后续归属变化的扩展行为仍有一个统一入口。
15. As a movement system, I want 通过总控以起点 PlotId、目标 PlotId 和 movingFactionId 请求路径, so that 阵营相关的可通行规则得到正确输入。
16. As a movement system, I want 获取连续的 Hex/Cell 路径结果, so that 单位能够逐格移动并且多格 Plot 可由路径服务选择可达入口。
17. As a performance-sensitive caller, I want 复用 PathResult 缓冲区, so that 高频寻路不需要总控为每次调用分配新结果对象。
18. As a movement system, I want 路径失败原因保留在 PathResult 中, so that 我能区分未初始化、Plot 不存在、无路和结果容量不足。
19. As a runtime caller, I want 常规寻路失败不产生 Unity 错误日志, so that 正常的无路分支不会污染控制台。
20. As a maintainer, I want 总控只在 Unity 主线程顺序调用, so that 可复用寻路工作区和结果缓冲区无需引入并发同步。
21. As a gameplay developer, I want PlotPathService 继续独立拥有阵营、开放、阻挡和 Camp 相关的路径策略, so that 总控不会复制或固化 GVG 移动规则。
22. As a future feature developer, I want 总控保留 MonoBehaviour 形态, so that 后续确有需要时可在组件上增加经过确认的序列化运行时配置或引用。

## Implementation Decisions

- 新增一个 GVG 地图运行时总控 MonoBehaviour，作为场景生命周期和外部 Plot 快照之间的唯一组合点。
- 总控序列化引用 HexMapView。它在 Awake 中调用 HexMapView 的构建操作，并保存构建后的 HexMap 供后续初始化使用。
- 如果 HexMapView 引用缺失、构建过程失败或构建后没有有效 HexMap，总控记录 Debug.LogError、保持不可初始化，并且之后的 TryInitialize 直接返回 false；不会在初始化调用中重试或隐式重建几何地图。
- 对外初始化入口为 `TryInitialize(IReadOnlyList<GvgPlotRuntimeData> plots)`。HexMap 不是该接口参数；外部调用方只负责在完整快照可用后调用它。
- 初始化在临时状态中调用既有 GVG 运行时组合能力。只有所有校验和组装成功后才替换内部 PlotRegistry 与 PlotPathService，保证原子性。
- 无效或空快照初始化失败时记录包含组装错误上下文的 Debug.LogError。首次失败不建立运行时地图；重初始化失败不替换既有成功状态。
- 每次成功初始化均以新快照完全替换运行时 Plot 状态，包括此前由阵营设置接口做出的 OwnerFactionId 改动。
- GvgPlotRuntimeData 继续是不可变外部输入 DTO。Plot 是运行时实体，OwnerFactionId 从只读状态改为可由 GVG 地图模块更新的状态。
- OwnerFactionId 保持不透明 int。总控不验证或解释其中立、势力或其他业务含义；相同值的设置是成功且无副作用的幂等操作。
- Plot 的 OwnerFactionId 对外仅可读。总控提供 `TrySetPlotOwnerFactionId(int plotId, int ownerFactionId)` 作为唯一修改入口，未初始化或 PlotId 不存在时返回 false。
- 总控提供 `TryGetPlot(int plotId, out Plot plot)` 作为只读查询。调用方的持久引用边界是 PlotId；重初始化后不得继续使用旧的 Plot 实例。
- 总控提供 `TryFindPlotPath(int startPlotId, int targetPlotId, int movingFactionId, PathResult result)`。它验证自身初始化状态与 PlotId 后委托既有 PlotPathService，不在总控中复制路径规则。
- `movingFactionId` 是路径接口必需参数。PlotPathService 独立决定阵营归属、开放状态、阻挡、Camp 解析、可通过与可进入规则。
- PathResult 由调用方创建和复用；总控不在热路径分配结果对象。路径的权威输出是连续 Hex/Cell 序列，失败原因沿用 PathResult 的可观察状态。
- 未初始化、PlotId 不存在、无路径和结果容量不足等常规调用结果不输出 Debug.LogError；只有 Awake 几何失败和快照初始化失败属于错误日志范围。
- 首版运行在 Unity 主线程的顺序调用模型中，不引入锁、任务、Job 或并发寻路支持。
- Start、End、GenerationType 的时间推进、生成刷新以及 Plot 开关控制不纳入本总控；它只根据输入快照组装当前运行时状态。
- 总控首版不序列化外部 Plot 快照、地图几何参数或路径策略配置。HexMapView 保持场景拓扑与显示配置的唯一来源。

## Testing Decisions

- 测试只验证总控的外部可观察行为：初始化状态、返回值、可查询 Plot、OwnerFactionId 变化、路径委托结果以及失败后的旧状态保留；不测试私有字段、临时组装顺序或具体容器实现。
- 最高且唯一的新增功能测试接缝是总控的公开 API。测试通过该组件初始化、查询、设置归属和寻路，不直接测试其内部持有的 Registry 或 PathService。
- 使用小型合成 HexMap 和 GvgPlotRuntimeData 快照构造测试，覆盖有效初始化、空或无效快照、重复或越界 Plot 配置、首次初始化失败与重初始化失败保留旧状态。
- 覆盖 Awake 在缺失 HexMapView、构建抛错或构建后无 Map 时的可初始化性契约与错误日志行为。
- 覆盖成功重初始化会替换 Plot 查询结果和 OwnerFactionId，而失败重初始化不破坏旧路径与旧 Plot 查询。
- 覆盖 TrySetPlotOwnerFactionId 的未初始化、未知 PlotId、成功更新和重复设置同值行为，并验证外部不能通过 Plot 查询绕过总控更新归属。
- 覆盖 TryFindPlotPath 将起点、目标、movingFactionId 与可复用 PathResult 交给既有路径服务的可观察结果；包括成功、多格 Plot 路径、未初始化、未知 Plot、无路径和容量不足结果。
- 验证常规路径失败不输出错误日志；无效快照和 Awake 几何失败会输出错误日志。
- 测试继续沿用仓库现有的纯 C# GVG/HexMap EditMode 测试风格作为先例；仅在需要验证 MonoBehaviour Awake 与 Debug.LogError 时使用最小 Unity EditMode 组件夹具，而不依赖完整场景或渲染。
- 总控接口是本功能唯一新增 seam；既有 GvgMapRuntimeComposer、PlotRegistry 与 PlotPathService 的单元测试继续负责其各自的低层规则契约。

## Out of Scope

- 修改 HexMapView 的几何算法、Radius、Layout、渲染、拾取或场景美术表现。
- 修改 PlotPathService 的具体阵营、Camp、开放或阻挡寻路策略。
- 服务端权威状态、网络同步、快照拉取、持久化，或把 OwnerFactionId 改动反写到外部数据源。
- Start、End、GenerationType 的时间推进、生成调度和地图阶段开放逻辑。
- Plot 归属变更后的地图显示、小地图、统计、事件派发或其他表现刷新。
- 多线程、Unity Job、异步寻路与跨线程复用 PathResult。
- 在总控中序列化外部 Plot 快照、路径配置或第二套地图几何配置。
- 长期持有 Plot 实例的兼容层；外部应以 PlotId 为稳定引用。

## Further Notes

- 本规格延续既有架构边界：HexMap 提供完整六边形 Cell 拓扑，GVG Plot 与路径策略独立于基础地图；地图总控只负责在 Unity 场景与 GVG 运行时服务之间组合它们。
- GVG 运行时总控取代调用方手工串接“HexMap、完整 Plot 快照、组合器、Registry、PlotPathService”的负担，但不替代这些模块的职责。
- 该组件未来可承载额外序列化数据，不过任何新字段都应先明确其唯一数据源，避免与 HexMapView 或外部快照形成双重权威。
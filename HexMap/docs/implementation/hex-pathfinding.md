# Hex 寻路实现与扩展说明

## 目的

本文说明当前 HexMap 通用寻路的实现原理、对象生命周期、零分配设计和扩展边界，供维护寻路、移动、GVG 规则和路径表现的程序员使用。

本文覆盖：

- HexPathfinder 的入口和 BFS 搜索流程；
- PathSearchWorkspace、PathRequest、ReusablePathRequest、PathResult 的职责；
- 路径结果和世界坐标输出的生命周期；
- 运行时重复寻路如何避免托管内存分配；
- 通过 IHexPathPolicy 扩展阵营、阻挡和进入规则；
- HexCell 按阵营限制通行的推荐实现方式；
- 后续增加移动代价、地形和路线失效处理时应保留的边界。

本文不负责 GVG 地块、领地、战斗结算、移动表现、路径平滑、多线程或带权搜索算法。

## 代码地图

- Assets/Scripts/HexMap/Runtime/HexMap.cs：维护地图中的 HexCell、坐标查询、Cell ID 查询和地图规模。
- Assets/Scripts/HexMap/Runtime/HexCell.cs：定义 Cell 的稳定身份。目前 Cell 只有 Id 和 Coordinate，不包含阵营或阻挡等玩法状态。
- Assets/Scripts/HexMap/Runtime/HexPathfinder.cs：绑定一张 HexMap 和一个 PathSearchWorkspace，执行同步 BFS 寻路并填充 PathResult。
- Assets/Scripts/HexMap/Runtime/PathSearchWorkspace.cs：持有一次性创建、跨搜索复用的 Dictionary、HashSet 和 Queue。
- Assets/Scripts/HexMap/Runtime/PathRequest.cs：包含防御性复制目标列表的 PathRequest，以及内部拥有并复用目标 List 的 ReusablePathRequest。
- Assets/Scripts/HexMap/Runtime/IHexPathPolicy.cs：寻路规则扩展 seam，提供 CanPass 和 CanEnter。
- Assets/Scripts/HexMap/Runtime/PathResult.cs：持有调用者提供的路径 List，保存当前搜索状态，并提供世界中心点输出。
- Assets/Tests/EditMode/HexMap/Runtime/HexPathfindingTests.cs：验证通用寻路语义、结果复用、输出容量和世界坐标 List 复用。

程序集边界：

- HexMap.Runtime 只引用 HexMap.Core。
- HexMap.Core 不引用 GVG、地块、阵营或场景对象。
- 阵营、阻挡和其他玩法规则应通过 HexMap.Runtime 的规则接口注入。
- 运行时核心不依赖 Scene、GameObject 或 Camera，因此 EditMode 测试可以直接构造地图对象。

## 运行流

### 初始化阶段

调用者在重复寻路之前创建：

1. 一张 HexMap；
2. 与该地图绑定的 PathSearchWorkspace；
3. 使用同一张地图和工作区创建的 HexPathfinder；
4. 一个长期复用的 IHexPathPolicy；
5. 一个 PathRequest 或 ReusablePathRequest；
6. 一个容量足够的 List<HexCell> 和 PathResult；
7. 可选的 List<Vector3> 世界坐标输出缓冲区。

PathSearchWorkspace 和 PathResult 都由调用者持有。它们不能在同一时间被多个搜索共享。

### 寻路入口

HexPathfinder 提供两个入口：

- FindPath(PathRequest, PathResult)：请求构造时复制目标列表，适合需要快照语义的调用；
- FindPath(ReusablePathRequest, PathResult)：读取 request 内部复用的目标 List，适合重复搜索。目标通过 ClearTargets 和 TryAddTarget 更新，不需要外围每次创建数组或 List。

两个入口最终进入同一个搜索核心。寻路器不创建新的 PathResult，而是清空并填充调用者传入的结果对象。

### 输入准备

一次搜索开始时，寻路器会清空：

- PathResult 的路径 List；
- 目标坐标集合；
- 父节点字典；
- 距离字典；
- 待处理队列；
- 已评估目标集合。

Clear 只清除元素，不主动释放集合已有的内部容量。

起点必须同时满足：

- 坐标存在于当前地图；
- Cell Id 与地图中该坐标的 Cell Id 一致。

否则返回 InvalidInput / StartMissing。

目标列表会逐项过滤和去重：

- 地图外或 Id 不匹配的目标被过滤；
- 相同坐标只保留一份；
- 全部目标无效时返回 InvalidInput / NoValidTargets。

### BFS 搜索

当前每条相邻边的代价固定为 1，因此使用 BFS。工作区状态含义如下：

- TargetCoordinates：合法且去重后的目标坐标；
- Parents：已发现节点的前驱坐标；
- Distances：起点到已发现节点的最短步数；
- Pending：等待展开的坐标队列；
- EvaluatedTargets：已经执行过 CanEnter 判断的目标坐标。

搜索从起点入队，反复取出队首节点并检查六个固定顺序的邻居。

对每个邻居：

1. 不在地图中则跳过；
2. 如果是目标，只调用 CanEnter；
3. 被 CanEnter 拒绝的目标不能成为最终目标，也不能成为中间节点；
4. 通过 CanEnter 的目标参与最近目标裁决，但不会进入 Pending；
5. 非目标节点已经访问过则跳过；
6. 非目标节点通过 CanPass 后才写入距离、父节点并入队。

如果当前节点距离已经不小于已知最佳目标距离，则不再展开当前节点。

### 目标裁决

目标选择规则保持确定：

1. 优先选择路径步数更少的目标；
2. 步数相同时先比较 q；
3. q 相同时再比较 r；
4. 同一目标的等长路径由固定邻居方向顺序决定。

因此结果不依赖目标列表顺序、HashSet 顺序或地图查询顺序。

### 路径重建

找到目标后，从目标沿 Parents 反向回溯：

1. 将目标 Cell 写入结果 List；
2. 沿父节点继续写入；
3. 写入起点后停止；
4. 原地反转 List；
5. 设置 ReachedTarget、Cost、Status 和 Reason。

不会创建中间坐标列表或新的 Cell 列表。

如果 List 容量不足：

- 不允许 List 自动扩容；
- 清空已写入的部分路径；
- 返回 InvalidInput / ResultCapacityExceeded；
- Cost 为 0；
- ReachedTarget 为默认值；
- 不暴露部分路径。

### 世界坐标转换

PathResult 只保存权威 Hex 路径。CopyWorldCentersTo 会把路径转换到调用者提供的 List<Vector3>：

1. 清空输出 List；
2. 检查 Capacity；
3. 按路径顺序调用 HexLayout.HexToWorld；
4. 写入输出 List。

世界坐标不会缓存到 PathResult，也不会改变权威路径。

## 实现原理与关键取舍

### 工作区所有权

HexPathfinder 的工作区是可变状态：

- 同一工作区不能同时服务两个搜索；
- 同一工作区不能被重入；
- 工作区绑定创建时的 HexMap；
- 并行寻路必须为每个活动搜索创建独立工作区。

让调用者创建工作区，可以明确内存归属和生命周期。寻路器只持有引用，不在热路径中创建或销毁容器。

### 容量复用

地图构造后 Cell 数量固定：

- 合法目标数量不会超过地图 Cell 数；
- 可搜索节点数量不会超过地图 Cell 数；
- 最终路径长度不会超过地图 Cell 数。

因此工作区的 Dictionary、HashSet 和 Queue 可以按 map.Count 初始化。路径 List 通常也按 map.Count 初始化。这是用一次性初始化内存换取稳定热路径的设计。

### 为什么保留坐标容器

本次改造优先消除托管分配，不同时改变搜索数据结构：

- Dictionary 和 HashSet 在初始化后复用；
- Clear 保留内部容量；
- 坐标键与现有 HexMap 查询 API 一致。

如果 profiling 证明哈希查找成为主要耗时，再单独评估按 Cell Id 索引的数组方案。

### 地图坐标遍历

HexMap 构造阶段使用直接 q/r 嵌套循环，不使用 yield return 迭代器。这样避免坐标迭代器状态机对象，并保持原有坐标遍历顺序和稳定 Cell Id 分配。

### 请求和结果生命周期

PathRequest 在构造时复制目标列表，适合快照语义。

ReusablePathRequest 内部持有可复用的 List<HexCell>。初始化时指定目标容量，之后通过 ClearTargets 和 TryAddTarget 更新目标；TryAddTarget 在容量不足时返回 false，不允许自动扩容。它只能在两次搜索之间更新；搜索期间必须保持 Start、Targets 和 Policy 稳定。若业务已经持有长期复用的 List，也可以通过接收 List<HexCell> 的构造入口把该缓冲区交给 request。

PathResult 和内部路径 List 由调用者创建并复用，下一次搜索会覆盖上一次结果。需要持久路线时必须显式复制。

### 零分配热路径

预热后的热路径不应创建：

- request、policy、result；
- Dictionary、HashSet、Queue、List；
- 数组、lambda 或闭包；
- 世界坐标输出集合。

调用者必须提前准备足够 Capacity，不能让 List 在搜索期间自动扩容。策略回调内部也必须遵守同样的约束。

## 扩展点

### 新增通行规则

新增规则应实现 IHexPathPolicy，不要修改 HexPathfinder。

- CanPass 判断非目标中间格；
- CanEnter 判断目标最终是否可进入。

这允许表达：

- 可以经过但不能停留；
- 可以进入但不能继续穿过；
- 目标格拥有单独的进入条件；
- 障碍物、阵营、开放状态和单位类型组合规则。

### HexCell 按阵营限制通行

当前 HexCell 是基础地图身份，只有 Id 和 Coordinate。阵营属于玩法状态，不应直接硬编码进通用 BFS。

推荐做法是维护按 Cell Id 索引的阵营状态，并通过策略对象读取：

~~~csharp
public enum FactionId
{
    Neutral = 0,
    Red = 1,
    Blue = 2
}

public sealed class FactionPathPolicy : IHexPathPolicy
{
    private readonly IReadOnlyList<FactionId> m_FactionByCellId;
    private readonly FactionId m_MovingFaction;

    public FactionPathPolicy(
        IReadOnlyList<FactionId> factionByCellId,
        FactionId movingFaction)
    {
        if (factionByCellId == null)
        {
            throw new ArgumentNullException(nameof(factionByCellId));
        }

        m_FactionByCellId = factionByCellId;
        m_MovingFaction = movingFaction;
    }

    public bool CanPass(HexCell cell)
    {
        return m_FactionByCellId[cell.Id] == m_MovingFaction;
    }

    public bool CanEnter(HexCell cell)
    {
        return m_FactionByCellId[cell.Id] == m_MovingFaction;
    }
}
~~~

这个策略表达“只有本阵营 Hex 才能行走”：

- 本阵营格可作为中间格；
- 本阵营格可作为最终目标；
- 敌方格不能作为中间格；
- 敌方格不能作为最终目标；
- 目标列表包含敌我目标时，只会从本阵营目标中选择；
- Neutral 是否可走必须显式定义，不能隐式假设。

阵营表应在初始化阶段创建并长期复用。阵营变化时，只在两次寻路之间更新状态，下一次搜索即可读取新状态。

如果确实要求 HexCell 自身携带阵营，可以把 FactionId 加入 HexCell，并让地图创建过程填充它。但这会改变基础 Cell 数据契约，并把动态 GVG 状态带入通用地图层。只有阵营是不可变地图配置时，才建议采用这种方式。

### 阵营加阻挡

规则组合仍然放在策略对象中：

~~~csharp
public bool CanPass(HexCell cell)
{
    return IsOwnFaction(cell) && !IsBlocked(cell);
}

public bool CanEnter(HexCell cell)
{
    return IsOwnFaction(cell) && CanStopOn(cell);
}
~~~

不要在 HexPathfinder 中增加 faction、guild、plot 或 battle 分支。

### 动态阵营变化与路线失效

寻路不会监听状态变化。一次搜索期间，地图和策略状态被视为稳定。

移动执行期间如果发生以下变化，移动系统负责重新验证或重新寻路：

- 路径格改变阵营；
- 路径格变为阻挡；
- 目标不再可进入；
- 地图阶段改变通行规则。

推荐流程是：检查下一格，发现失效后停止或标记路线失效，更新状态，再复用原有工作区、请求和结果重新寻路。

### 增加移动代价

当前 BFS 假设每条边代价为 1。若要支持地形代价或移动力，不能只增加一个 Cost 回调后继续使用 Queue。需要重新设计带权搜索的优先队列、距离更新、父节点更新、目标裁决和结果 Cost 语义。

### 多种单位规则

不同单位可以使用不同的策略实例：

- 本阵营单位：只允许本阵营格；
- 工程单位：允许本阵营和道路格；
- 侦察单位：允许中立格，但不能进入敌方格；
- 特殊单位：可以穿过某些阻挡，但不能停留。

地图可以共享，但同一时刻不能共享正在使用的工作区和 PathResult。

## 约束

- 不要在 HexPathfinder 内部创建新的搜索集合或结果对象。
- 不要在热路径中触发 List 自动扩容。
- 不要使用全局静态工作区。
- 不要让工作区并发或重入。
- 不要在 CanPass 或 CanEnter 中修改地图、修改影响后续判断的策略状态，或重入同一个寻路器。
- 不要把 GVG、阵营、公会、地块或战斗分支硬编码进 HexPathfinder。
- 不要把 HexCell 的稳定身份与动态玩法状态混为一谈。
- ReusablePathRequest 的 Start、目标 List 和 Policy 只能在两次搜索之间更新；目标应通过 ClearTargets 和 TryAddTarget 管理，不要在每次搜索外围创建新的数组或 List。
- PathResult 会被下一次搜索覆盖，需要持久数据时必须显式复制。
- 世界坐标属于派生表现数据，不要缓存到寻路核心结果。
- 新增 HexMap Runtime 私有和实例字段时使用 m_ 前缀。
- 修改公共 API 后同步更新 HexMap.Runtime.Tests.EditMode 测试。
- HexMap.Runtime 不能反向引用 GVG 或场景程序集。

## 验证

### 通用语义

在 Unity Test Runner 的 EditMode 中运行 HexMap.Runtime.Tests.EditMode，确认：

- 有效路径正确返回；
- 起点即目标不调用 CanPass 和 CanEnter；
- 起点不需要通过 CanPass；
- 最终目标使用 CanEnter；
- 被拒绝目标不能成为中间格；
- 无效起点、无效目标和无可达目标返回正确原因；
- 多目标选择最近可达目标；
- 等代价目标和等长路径保持确定性；
- Cost 等于路径边数；
- 失败结果不包含部分路径。

### 复用和容量

确认：

- ReusablePathRequest 可在两次搜索之间更新目标；
- 同一个 PathResult 可以连续经历成功、失败、再次成功；
- 失败后路径 List 为空；
- 容量不足返回 ResultCapacityExceeded 且不自动扩容；
- 世界坐标 List 可以多次清空和填充；
- 下一次搜索覆盖结果的生命周期契约符合调用方预期。

### 阵营规则

至少覆盖：

- 单位只能经过本阵营 Hex；
- 敌方 Hex 不能作为中间格；
- 敌方 Hex 不能作为最终目标；
- 混合目标中只选择本阵营可进入目标；
- Neutral 的行为符合显式配置；
- 两次搜索之间更新阵营表后，复用型策略读取新状态；
- 策略对象在重复寻路期间不创建临时集合、闭包或上下文对象。

### 零分配

创建地图、工作区、策略、request、result 和输出 List 后：

1. 预热若干次寻路；
2. 在测量区间内重复调用寻路；
3. 不创建新的 request、result、List、数组或闭包；
4. 测量 GC 分配增量；
5. 单独测量世界中心点转换；
6. 将策略回调内部的分配与寻路器自身分配分开。

预热后的核心寻路和使用预分配输出 List 的世界坐标转换应当不产生托管内存分配。首次构造、泛型初始化和容量准备不属于稳定热路径测量。

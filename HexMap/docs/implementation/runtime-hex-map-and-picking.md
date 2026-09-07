# 运行时 Hex 地图与格子拾取实现说明

## 目的

本文说明 issue 02 — 运行时 Hex 地图与格子拾取的实现边界和后续维护方式，面向负责地图运行时、Unity 场景接线和 EditMode/运行时测试的程序员。

目标是把有限地图配置生成为可查询、可遍历的 HexCell 集合，并把一次屏幕点击稳定地转换为地图中的 HexCell。本文覆盖地图配置到运行时索引的生成、有效/地图外/范围内缺失坐标的区别、Raycast 到 HexCell 的调用顺序，以及 SampleScene 的最小验证组成。

本文不定义 GVG 地块、归属、阻挡、开放状态、战斗或寻路规则。根据 docs/specs/gvg-hex-map.md，这些属于建立在基础 HexMap 之上的玩法层。

issue 02 的运行时地图、拾取器、示例视图和测试用例已经按本文契约落地。本文中仍标为“建议新增”的文件名表示职责约定；若后续重命名，必须保留相同的依赖方向和行为边界。

## 代码地图

### 已存在的稳定依赖

- Assets/Scripts/HexMap/Core/HexCoord.cs：以 Axial (q, r) 作为值语义坐标，派生 s = -q - r，提供相等比较、哈希、六邻居和距离。
- Assets/Scripts/HexMap/Core/HexCubeCoord.cs：保持 q + r + s = 0 的 Cube 值类型。
- Assets/Scripts/HexMap/Core/HexDirection.cs：固定六邻居方向及 Axial 增量。邻居顺序由 HexCoordTests.SixNeighborDirectionsUseTheCanonicalAxialOrder 锁定。
- Assets/Scripts/HexMap/Core/HexLayout.cs：负责 pointy/flat、XY/XZ、尺寸和原点下的中心点投影与反投影。它只做几何计算，不判断坐标是否存在，也不接触 Physics 或场景对象。
- Assets/Scripts/HexMap/Core/HexMap.Core.asmdef：当前核心程序集没有对其他项目程序集的引用；运行时地图应依赖它，而不是让核心反向依赖地图或场景。
- Assets/Tests/EditMode/HexMap/Core/：坐标和布局的纯值行为测试。地图测试应延续“小型合成地图、直接实例化值类型、不依赖正式场景”的模式。

### 建议新增的运行时代码

以下是建议的文件职责和命名；实现时如果采用不同名称，仍应保留相同的依赖方向和行为边界。

- Assets/Scripts/HexMap/Runtime/HexCell.cs：最小格子数据。第一版至少包含 HexCoord Coordinate；不要在这里加入 GVG 归属或可通行状态。
- Assets/Scripts/HexMap/Runtime/HexMapDefinition.cs：运行时生成所需的纯数据输入，包括地图边界和被排除的坐标，或等价的显式坐标集合。
- Assets/Scripts/HexMap/Runtime/HexMap.cs：拥有生成后的 Dictionary<HexCoord, HexCell>，提供查询和稳定遍历，不负责渲染和点击。
- Assets/Scripts/HexMap/UnityRuntime/HexMapConfigAsset.cs：可选的 Unity 序列化适配层，把 Inspector 字段转换为 HexMapDefinition，不把 Unity 序列化字段直接暴露给核心地图 API。
- Assets/Scripts/HexMap/UnityRuntime/HexMapView.cs：根据 HexLayout.HexToWorld 创建简单的格子表现和可被 Raycast 命中的碰撞体。
- Assets/Scripts/HexMap/UnityRuntime/HexMapPicker.cs：将屏幕点转换为 Raycast 命中点，再转换为 HexCoord 和 HexCell；不把点击结果直接解释成 GVG 地块。
- Assets/Tests/EditMode/HexMap/Runtime/：地图生成、查询、遍历和配置错误测试。
- Assets/Tests/PlayMode/HexMap/ 或等价的 Unity 集成测试目录：需要真实 Camera、Collider 和 Physics.Raycast 时使用；纯坐标到格子的测试仍放在 EditMode。

程序集建议分为 HexMap.Runtime 和 HexMap.UnityRuntime：前者引用 HexMap.Core，后者引用 HexMap.Runtime、HexMap.Core 和 Unity 运行时 API。若第一版合并两个运行时程序集，也必须保持 HexMap.Core 不引用它们，且 HexMap 数据查询不引用 Camera、Physics、MonoBehaviour 或场景资源。

## 运行流或编辑器流程

### 地图生成

1. HexMapConfigAsset（如果采用 ScriptableObject）或其他配置入口提供有限地图定义。
2. 配置转换为 HexMapDefinition。坐标输入以 q/r 为主；不要把 Cube 的 s 当作第二份可编辑真相。
3. HexMap 校验边界、坐标重复和坐标合法性，然后按确定顺序创建 HexCell。
4. HexMap 将格子按 HexCoord 建立字典索引，同时保留一个稳定的遍历序列。渲染、调试和小地图等上层消费者都从这份运行时数据读取。
5. HexMapView 遍历 HexMap.Cells，对每个坐标调用同一个 HexLayout.HexToWorld，在地图根节点的局部空间中放置表现和碰撞体。

第一版建议使用“边界 + 排除坐标”的定义：边界内且未排除的坐标生成格子，边界内被排除的坐标保留为“缺失”，边界外坐标标记为“地图外”。如果改用显式坐标列表，仍必须单独保留可判定地图外区域的边界，否则无法区分“范围外”和“范围内未生成”。

### 坐标查询

地图查询应提供明确结果状态，而不是只返回一个可能为 null 的格子。目标语义如下：

- HexCellQueryStatus.Found：坐标在地图内且存在 HexCell。
- HexCellQueryStatus.OutsideMap：坐标超出配置边界。
- HexCellQueryStatus.Missing：坐标在边界内，但配置没有生成该格子。

Found 时结果携带 HexCell；其余状态不携带有效格子。普通查询失败是正常业务结果，不应抛异常。配置本身不合法（例如边界反转、重复坐标或无法表示的坐标）应在生成阶段拒绝。

地图稳定 API 至少应覆盖按 HexCoord 查询、判断坐标是否存在、遍历全部已生成 HexCell，以及暴露生成数量。遍历顺序应固定，或明确声明不保证顺序；第一版建议固定顺序，便于测试和调试。

HexCell 的存在与“可通行”“可选中”“属于哪个地块”是不同概念。后续 GVG 层可以给地图查询结果加规则判断，但不能通过删除 HexCell 来表达阻碍或未开放状态。

### 点击拾取

拾取器的执行顺序必须保持为：

屏幕点 -> Camera.ScreenPointToRay -> Physics.Raycast（地图 LayerMask/地图碰撞体） -> 命中世界点 -> mapRoot.InverseTransformPoint（如地图根节点有变换） -> HexLayout.WorldToHex -> HexMap 查询 -> Found 才返回 HexCell。

这里的 HexLayout 应被视为地图局部空间中的布局。HexLayout 只知道 Origin、朝向、平面和半径，不知道某个 Unity Transform。因此：

- HexMapView 使用 mapRoot.TransformPoint(layout.HexToWorld(cell.Coordinate)) 放置世界中的格子。
- HexMapPicker 对 RaycastHit.point 使用同一个 mapRoot.InverseTransformPoint，再传给 WorldToHex。
- 地图根节点没有变换时，局部点和世界点恰好相同；不要因此省略转换，避免后续移动、缩放或旋转地图时拾取失配。
- View 和 Picker 必须共享同一个 HexLayout 参数实例或等价值。Orientation、Plane、OuterRadius、Origin 任一项不一致都可能把点击归到相邻格。

Raycast 未命中、命中非地图碰撞体、反投影坐标是 OutsideMap 或 Missing 时，拾取器都必须返回失败，不得返回上一次点击的格子，也不得根据最近格子猜测结果。地图 LayerMask 只能减少误命中；最终仍要经过 HexMap 查询确认坐标属于当前地图。

### Sample Scene

Assets/Scenes/SampleScene.unity 是 issue 02 的示例场景，当前包含 Main Camera、Directional Light 和 Hex Map 根节点。Hex Map 根节点引用 Assets/Scenes/HexMapSampleConfig.asset，并已接线以下运行时组成：

- 一个可配置的地图定义或配置资产引用。
- 一个与配置一致的 HexLayout。
- HexMap 的生成入口。
- 为生成格子创建可见表现和 Collider 的 HexMapView。
- 使用场景 Camera 的 HexMapPicker。
- 能显示最后一次查询状态和坐标的最小调试输出；可用 Debug.Log，不必提前引入 GVG UI。

当前 ProjectSettings/EditorBuildSettings.asset 的 m_Scenes 为空，不能假定 SampleScene 已经是构建入口。验证时应从 Unity Editor 直接打开 SampleScene；如果 issue 需要打包后启动，再单独把它加入 Build Settings，并记录该配置变化。仓库中不存在 Assets/Scenes/Start.unity，不要为本 issue 虚构固定的 Start Scene。

## 实现笔记

### 数据归属和生成不变量

- Axial HexCoord 是格子的唯一键。HexLayout 只将坐标映射到中心点，不拥有地图成员关系。
- HexMap 是地图成员关系的唯一拥有者：一个坐标最多对应一个 HexCell，字典键和格子坐标必须一致。
- 生成完成后，Count、遍历结果和按坐标查询结果在一次地图生命周期内保持一致。若地图需要替换，生成一份新实例并由上层切换引用，避免边遍历边修改字典。
- 配置校验和运行时查询分开：生成阶段报告结构错误，查询阶段只报告 Found、OutsideMap 或 Missing。
- HexCell 第一版应保持轻量、不可变或由 HexMap 集中修改。不要让渲染 GameObject 成为格子数据的真相；销毁一个视觉对象不能让地图查询自动变成缺失。

### 局部空间和边界

HexLayout.HexToWorld/WorldToHex 的现有实现已经通过 HexLayoutTests 覆盖 pointy/flat、XY/XZ、原点、半径、中心点往返和相邻边界 tie-break。issue 02 应复用这些 API，不要在拾取器中重新实现 Axial 反算或自行对 q/r 分别四舍五入。

六边形边界上的点没有天然唯一归属；当前内核通过 cube rounding 的最大误差分量修正以及固定比较顺序决定 tie-break。拾取器只负责把物理命中点交给内核，不添加 epsilon、邻居搜索或“最近可用格”补偿，否则 Scene 点击和 EditMode 结果会出现两套规则。

### 表现与数据的依赖方向

推荐的依赖关系如下：

HexMap.Core
  <- HexMap.Runtime
      <- HexMap.UnityRuntime（View/Picker/Sample 接线）
          <- GVG 玩法层（后续）

HexMap.Runtime 不应引用 GVG、地块、归属、战斗或寻路规则。HexMap.UnityRuntime 可以依赖 Unity 的 Camera、Physics、Collider 和 Transform，但不应把这些类型塞回 HexMap.Core 的值计算 API。

## 扩展点

### 新增地图类型或配置字段

新增地图范围、排除坐标或格子元数据时，先扩展 HexMapDefinition/配置适配层，再在 HexMap 的生成校验中建立不变量。序列化层可以使用专用的 q/r DTO，避免为了 Inspector 直接修改现有 HexCoord 的值语义。

如果需要不规则地图，保留“地图外”和“范围内缺失”的状态区分。不要把所有不存在的坐标都压成一个 bool，否则编辑器验证和点击反馈无法定位配置错误。

### 新增渲染方式

更换单格 Mesh、合并 Mesh、Tilemap 或其他表现时，只改 HexMapView 及其资源接线。所有视觉实现必须使用 HexLayout.HexToWorld，所有点击实现必须使用 HexLayout.WorldToHex；不要让渲染器把坐标写进 Collider 名称后再由拾取器解析。

### 新增点击行为

选择高亮、悬停、长按和拖拽应建立在 HexMapPicker 输出的 HexCell 或坐标之上。拾取器可以提供无命中、命中格子和查询状态，但不应直接触发 GVG 地块操作。多格地块由上层把 HexCell.Coordinate 映射到地块，基础地图不需要知道 Plot。

### 接入 GVG 规则

GVG 层应持有 HexMap，通过坐标查询得到格子，再独立判断归属、开放和可通行。阻碍格、未开放格和地图外区域的玩法语义不能通过破坏基础地图字典来实现；需要变化时由规则层提供谓词或状态服务。

### 新增测试

优先增加外部行为测试：

- 生成后查询已生成坐标返回 Found，返回的格子坐标与查询键一致。
- 边界外坐标返回 OutsideMap。
- 边界内排除坐标返回 Missing。
- 重复坐标、非法边界和无法表示的坐标在生成时被拒绝。
- 遍历数量等于生成数量，且不产生重复坐标。
- 四种布局组合下，HexToWorld 后再 WorldToHex 能找到同一格。
- Raycast 未命中或命中地图外对象时返回失败；命中地图碰撞体时返回正确坐标。
- 地图根节点有平移/旋转/缩放时，View 和 Picker 使用同一局部空间转换，不返回相邻错误格。

## 约束

- 不修改 HexCoord、HexLayout 的坐标语义来适配地图查询；地图成员资格属于 HexMap。
- 不在 HexMap.Core 或纯运行时地图查询中引用 Camera、Physics.Raycast、GameObject、MonoBehaviour、Prefab 或 Scene。
- View 与 Picker 必须共享布局参数、地图根节点空间和地图 LayerMask 约定。
- 物理命中不是格子存在性的证明；命中点必须经过 WorldToHex 和 HexMap 查询。
- 普通未命中不能抛异常，也不能复用上一次有效格子；配置结构错误应尽早在生成阶段失败。
- HexCell 的存在、可点击、可通行和 GVG 地块归属必须保持独立状态，避免基础模块被玩法规则污染。
- 使用 Dictionary<HexCoord, ...> 时只能以 HexCoord 作为键；不要以浮点世界坐标作为身份或序列化主键。
- 当前示例场景不是构建入口；除非明确修改 Build Settings，否则验证指向 Editor 中打开的 Assets/Scenes/SampleScene.unity。

## 验证

### EditMode

在 Unity Test Runner 的 EditMode 运行 Assets/Tests/EditMode/HexMap/Runtime/ 下的地图测试，并同时运行现有 Assets/Tests/EditMode/HexMap/Core/ 测试。重点确认：

1. 配置生成出的数量、坐标唯一性和稳定遍历。
2. Found、OutsideMap、Missing 三种结果可区分。
3. 非法配置在生成阶段被拒绝。
4. 地图查询使用 HexCoord 相等和哈希，不依赖字符串或浮点坐标。

### Unity 集成或 PlayMode

在 SampleScene 中逐项验证：

1. 进入场景后能看到由配置生成的有限 Hex 格子。
2. 点击多个已生成格子的中心，调试输出中的 q/r 与格子位置一致。
3. 点击格子边界附近时，结果遵循核心布局测试定义的 tie-break，不因 Picker 自己的修正而改变。
4. 点击地图外空白、未命中 Collider 或非地图对象时不返回格子。
5. 给地图根节点加平移（必要时再加旋转/缩放）后，显示位置和拾取结果仍一致。
6. 改变 pointy/flat、XY/XZ 或半径配置后，View 与 Picker 使用同一配置，中心点击仍返回正确坐标。

若使用一体化 MeshCollider，需另外确认 Collider 的可拾取层、Mesh 生成时机和 Physics.Raycast 时机；不能只凭 Scene 中“看得见格子”推断点击链路已生效。

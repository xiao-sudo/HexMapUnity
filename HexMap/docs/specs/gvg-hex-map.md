# GVG Hex Map 规格说明

## Problem Statement

GVG 战场需要一套可靠的 Hex Map 基础能力。现有策划文档描述了战场玩法意图，包括六边形地图、地块、多格地块、归属、阻挡、移动、寻路、小地图和地图阶段变化，但文档里的坐标表结构和配置方式不应作为技术实现约束。

当前需要把 Hex Map 能力拆清楚：通用 Hex 坐标、世界坐标转换、点击拾取、格子数据和基础寻路应该是可复用底座；地块归属、阻挡、开放状态、移动规则和小地图属于 GVG 玩法层；地图配置需要编辑和验证工具保障质量，避免多格地块不连通、坐标重复、阻碍切断路径等问题。

## Solution

将交付拆成三个模块：

1. HexMap 基础实现：负责 Hex 坐标、布局转换、格子数据、地图生成、点击拾取、通用寻路和路径表现数据。
2. GVG 地图玩法逻辑：负责地块、多格地块、归属、阻挡、开放状态、移动规则、连通判断、战场表现和小地图。
3. 地图编辑与验证工具：负责地图配置编辑、预览、导出、错误检查、连通性验证和寻路调试。

HexMap 基础模块不依赖公会、归属、战斗或 GVG 规则。GVG 玩法模块通过规则谓词告诉基础寻路系统哪些格子可通过、哪些格子可进入、哪些地块可选中、哪些目标可抵达。

## User Stories

1. As a gameplay developer, I want 一个稳定的 Hex 坐标类型, so that 每个六边形格子都能被唯一标识。
2. As a gameplay developer, I want 使用 Axial 坐标作为主要运行时坐标, so that 格子数据结构简单且易于序列化。
3. As a gameplay developer, I want 能从 Axial 坐标推导 Cube 坐标, so that 距离、邻居和舍入计算可以保持正确。
4. As a gameplay developer, I want Hex 坐标支持相等比较和哈希, so that 它可以安全用于字典、集合和查找表。
5. As a gameplay developer, I want 查询一个 Hex 的 6 个邻居, so that 移动、连通性和寻路可以基于网格拓扑运行。
6. As a gameplay developer, I want 计算两个 Hex 的距离, so that 最近目标、路径成本和调试信息可以一致。
7. As a gameplay developer, I want 将 Hex 坐标转换为 Unity 世界坐标中心点, so that 格子、单位和特效可以正确摆放。
8. As a gameplay developer, I want 将 Unity 世界坐标转换回 Hex 坐标, so that Raycast 点击可以定位到正确格子。
9. As a gameplay developer, I want Hex Layout 支持 pointy top 和 flat top, so that 美术表现可以选择不同六边形朝向。
10. As a gameplay developer, I want Hex Layout 支持 XY 和 XZ 平面, so that 地图可以用于 2D 风格界面或 3D 场景。
11. As a gameplay developer, I want 地图有可查询的 Hex Cell 数据, so that 玩法可以判断坐标是否存在、是否可用、属于哪个地块。
12. As a gameplay developer, I want 能遍历地图内所有 Hex Cell, so that 渲染、校验和小地图可以复用同一份数据。
13. As a gameplay developer, I want 根据数据生成 Hex 地图, so that 地图布局可以通过配置或编辑器调整。
14. As a gameplay developer, I want 点击地图时得到 Hex Cell, so that 上层玩法可以进一步解析为地块。
15. As a gameplay developer, I want 通用 A* 或 BFS 寻路, so that GVG 移动可以复用底层路径搜索。
16. As a gameplay developer, I want 寻路接受外部 CanPass 判断, so that 通用寻路不耦合 GVG 归属和阻挡规则。
17. As a gameplay developer, I want 寻路接受多个目标格, so that 多格地块可以自动选择最近可达目标。
18. As a gameplay developer, I want Hex 路径能转换成世界坐标点列表, so that 移动表现和路径线可以复用同一结果。
19. As a player, I want 点击任意地图格子时选中对应地块, so that 我可以查看并操作战场目标。
20. As a player, I want 点击多格地块的任意格都选中同一个地块, so that 大营和城池表现为一个整体目标。
21. As a player, I want 点击阻碍格时看到不可达反馈, so that 我知道这里不能经过或抵达。
22. As a player, I want 点击未开放地块时看到未开放反馈, so that 我知道该地块尚未进入可操作阶段。
23. As a player, I want 选中地块后看到名称、归属和代表坐标, so that 我能理解当前选择对象。
24. As a player, I want 根据归属看到“讨伐”或“驻守”按钮, so that 我能快速发起正确操作。
25. As a player, I want 选中多格地块时所有格子都有选中特效, so that 我能看清完整地块范围。
26. As a gameplay designer, I want 一个地块能包含一个或多个 Hex Cell, so that 大营、大城和都城可以占据更大空间。
27. As a gameplay designer, I want 每个 Hex Cell 最多只属于一个地块, so that 地块归属和点击不会冲突。
28. As a gameplay designer, I want 每个地块有代表格或中心点, so that UI 锚点、镜头定位和小地图文字位置稳定。
29. As a gameplay designer, I want 每个地块有可停驻格, so that 单位能在多格地块中落到明确位置。
30. As a gameplay designer, I want 支持大营、普通地块、草地、小城、大城、都城和阻碍类型, so that GVG 地图有足够的玩法表达。
31. As a gameplay designer, I want 地块类型能影响可通行、可占领、显示资源和战斗表现, so that 配置能驱动地图行为。
32. As a gameplay designer, I want 地块支持未生成、已生成未开放、开放中立、已归属、战斗中状态, so that 地图能随赛程变化。
33. As a gameplay designer, I want 地块支持中立和公会归属, so that 战场领地可以被争夺。
34. As a gameplay designer, I want 大营初始归属对应公会且不可攻占, so that 每个公会有稳定出生点。
35. As a gameplay designer, I want 普通地块和城池能从中立变为公会归属, so that 领地扩张玩法成立。
36. As a gameplay designer, I want 归属变化刷新地图表现、小地图、统计数据和寻路连通性, so that 所有系统反映同一战场状态。
37. As a gameplay designer, I want 阻碍可以作为显式地块存在, so that 它既能阻挡移动，也能承载特殊资源或阶段变化。
38. As a gameplay designer, I want 未生成、未开放、阻碍和地图外区域默认不可通行, so that 可移动区域受到明确约束。
39. As a player, I want 进入战场时镜头定位到己方大营或当前队伍位置, so that 我能立即看到相关区域。
40. As a player, I want 拖拽和缩放地图, so that 我能观察完整战场和局部细节。
41. As a player, I want 镜头被限制在地图边界内, so that 我不会看到地图外空区域。
42. As a gameplay developer, I want 通过代码聚焦某个 Hex 或地块, so that 断点推荐、小地图跳转和任务引导可以定位地图。
43. As a player, I want 从当前队伍位置移动到目标地块, so that 我可以攻占或驻守地块。
44. As a player, I want 目标是多格地块时自动选择最近有效目标格, so that 我不需要手动指定城池内部格子。
45. As a player, I want 队伍按 Hex 路径逐格移动, so that 移动过程符合六边形地图结构。
46. As a player, I want 每格移动消耗固定或配置出的时间, so that 行军速度可被玩法平衡。
47. As a player, I want 移动中看到指向目标的路径线或箭头线, so that 我知道队伍正在去哪里。
48. As a player, I want 自己、友方和敌方移动线颜色不同, so that 战场移动态势可读。
49. As a player, I want 移动中可以更改目标, so that 我能应对战场变化。
50. As a gameplay developer, I want 移动逻辑位置和表现位置分离, so that 插值中的表现不会污染真实所在格。
51. As a gameplay developer, I want 路径中途因归属或阻挡变化失效时能重新处理, so that 行军能响应战场变化。
52. As a player, I want 目标不连通时看到“道路不连通，请先前往铺路”, so that 我知道为什么不能移动。
53. As a player, I want 目标不连通时自动推荐最近铺路断点并定位镜头, so that 我知道下一步该打哪里。
54. As a player, I want 归属地块显示公会颜色或归属特效, so that 领地归属一眼可见。
55. As a player, I want 中立地块、战斗中地块和安全区都有清晰表现, so that 战场状态容易判断。
56. As a player, I want 当前队伍位置显示头像或角色标记, so that 我能快速找到自己的队伍。
57. As a player, I want 其他移动玩家显示头像、名称和队伍成员, so that 我能理解附近行动。
58. As a player, I want 从战场打开小地图, so that 我能快速查看全局态势。
59. As a player, I want 小地图以当前队伍所在格为中心打开, so that 它从我关心的位置开始显示。
60. As a player, I want 小地图显示我的队伍位置、地块归属、大营信息和城池名称, so that 战场概览可读。
61. As a player, I want 小地图支持左右拖动查看, so that 我能浏览更多地图区域。
62. As a player, I want 小地图随归属变化刷新, so that 概览信息保持准确。
63. As a player, I want 小地图显示己方占领数量、总地块数、势力积分和排名, so that 我能评估公会局势。
64. As a gameplay designer, I want 编辑器中创建地图范围并分配 Hex 到地块, so that 地图不需要直接手写坐标表。
65. As a gameplay designer, I want 编辑器中配置地块类型、状态、归属槽位、可通行、可占领、代表格和可停驻格, so that 地块行为清晰可查。
66. As a gameplay designer, I want 编辑器预览坐标、地块 ID、类型、阻碍、大营、城池和归属色, so that 配置结果能被直接检查。
67. As a gameplay designer, I want 校验一个 Hex 不能属于多个地块, so that 点击和归属不会冲突。
68. As a gameplay designer, I want 校验地块不能引用不存在的 Hex, so that 导出数据不会包含无效坐标。
69. As a gameplay designer, I want 校验多格地块必须连通, so that 大地块不会被错误配置成分散区域。
70. As a gameplay designer, I want 校验代表格和可停驻格必须属于对应地块, so that UI 和移动目标稳定。
71. As a gameplay designer, I want 校验重要地块可达, so that 战场目标不会因为阻碍而无法参与。
72. As a gameplay designer, I want 校验阻碍是否意外切断地图, so that 地图连通性符合预期。
73. As a gameplay designer, I want 校验阶段性开放和替换后的地图状态, so that 后续阶段不会出现不可达或重复占用。
74. As a gameplay designer, I want 在编辑器中选择起点和终点调试寻路, so that 路线问题可以在配置阶段定位。
75. As a gameplay designer, I want 导出前自动运行校验, so that 错误地图不会进入运行时。

## Implementation Decisions

- 交付拆成三个模块：HexMap 基础实现、GVG 地图玩法逻辑、地图编辑与验证工具。
- HexMap 基础实现负责通用坐标、布局、格子、点击、通用寻路和路径表现数据，不引用 GVG、公会、归属、战斗等概念。
- GVG 地图玩法逻辑建立在 HexMap 基础实现之上，负责地块、归属、阻挡、开放状态、移动规则、连通判断、战场表现和小地图。
- 地图编辑与验证工具负责配置生产、预览、导出和错误检查。
- 内部运行时坐标优先使用 Axial(q, r)。
- Cube(q, r, s) 作为派生数学模型使用，满足 q + r + s = 0。
- 不直接沿用策划文档中的坐标表设计；表结构可以根据实现策略重设。
- pointy top 和 flat top 是布局参数，不影响 Hex 坐标本身。
- XY 和 XZ 是 Unity 平面映射参数，不影响 Hex 坐标本身。
- HexToWorld 返回 Hex 中心点。
- WorldToHex 使用布局反算和 cube rounding 得到最近 Hex。
- 地图点击流程为：屏幕点 -> Raycast -> 命中世界点 -> HexMap 局部坐标 -> HexCoord -> HexCell -> Plot。
- 通用寻路接受起点、目标集合、通行谓词、进入谓词，返回 Hex 路径或失败。
- 地块是 GVG 操作对象，Hex Cell 是地图最小坐标单位。
- 一个地块可包含一个或多个 Hex Cell。
- 一个 Hex Cell 最多属于一个地块。
- 多格地块需要有代表点和可停驻格，这两个概念不能混用。
- 队伍当前位置记录为具体 Hex，而不是只记录地块 ID。
- 目标可以是地块，移动系统自动选择目标地块内最近的合法目标 Hex。
- 移动按 Hex 路径逐格执行。
- 移动逻辑位置和视觉插值位置分离。
- 归属、阻挡、开放状态变化后，需要刷新地图显示、小地图显示、统计数据和寻路连通性。
- 小地图复用主地图的 Hex 和地块运行时数据，不维护第二套地图状态。
- 编辑器和运行时应共享同一份数据结构或导入模型，避免预览和实际表现不一致。

## Testing Decisions

- 测试应验证外部行为，不直接绑定内部实现细节。
- 主要测试切点为三个高层服务：HexMap 基础服务、GVG 地图规则服务、地图验证服务。
- HexMap 基础服务测试覆盖 Axial/Cube 推导、6 邻居查询、Hex 距离、HexToWorld 与 WorldToHex 往返、pointy/flat、XY/XZ、点击命中位置转 Hex、通用寻路成功和失败、多目标最近可达选择。
- GVG 地图规则服务测试覆盖单格/多格地块解析、点击任意多格 Cell 返回同一地块、状态对可见/可点/可通行/可进入的影响、归属变化对通行和连通的影响、阻碍和未开放区域不可通行、多格目标选择最近可停驻格、目标不连通时返回推荐断点、路径中途失效时的重算或断路处理、小地图数据从主地图状态派生。
- 地图验证服务测试覆盖重复 Hex 占用、引用不存在 Hex、多格地块不连通、代表格不属于地块、可停驻格不属于地块、大营配置缺失或数量错误、重要目标不可达、阻碍导致异常分区、阶段性开放或替换后地图无效。
- 测试数据应优先使用小型合成地图，而不是完整生产地图。
- 当前仓库只有 Unity Sample Scene，没有看到项目既有测试先例；建议优先建立纯 C# EditMode 测试，避免一开始依赖复杂场景。

## Out of Scope

- 客户端与服务器一致性。
- 服务器权威移动和同步。
- 完整 GVG 战斗结算。
- 奖励、排行、商店、礼包、协战、献计、聊天等非 Hex Map 功能。
- 最终美术资源、正式特效和性能优化。
- 完全兼容策划原始表结构。
- 最终战场地图布局。
- 路径平滑和复杂路线美化。
- 大规模地图 Mesh 合批优化。

## Further Notes

- 策划文档应作为玩法意图参考，不作为固定技术 schema。
- 最关键的架构边界是：通用 HexMap 能力与 GVG 玩法规则分离。
- 如果第一版地图规模较小，可以先使用 Prefab 或简单 Mesh 表现；后续再根据性能需求升级为合批或分块渲染。
- 地图编辑与验证工具不是锦上添花。多格地块、阻碍、阶段开放和连通性都很容易出错，必须有自动校验。
- 本环境没有可用的 issue tracker 集成，GitHub CLI 也不存在，因此规格文档已先保存到仓库本地。后续接入 issue tracker 后，应以 ready-for-agent 标签发布。

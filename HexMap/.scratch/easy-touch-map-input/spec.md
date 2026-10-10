# EasyTouch 地图输入与镜头手势路由

Status: ready-for-agent

## 问题陈述

移动端玩家需要以熟悉的触摸手势操作正交地图相机：轻点以选择 Cell，单指拖动以平移，以及双指 Pinch 以拉近或拉远镜头。当前示例输入分散在多个轮询 Unity 旧版 `Input` 的组件中。这些组件绕过 Map 输入路由契约，未使用项目已集成的 EasyTouch，并使手势归属、UI 优先级和模式行为无法保持一致。

尤其是，相机距离输入必须像 Map 点击和拖拽一样，经由当前活动的 Map 输入 Handler 处理；它须同时适用于 Gameplay 与 TopDown 模式，且不得将 EasyTouch 类型、依赖屏幕尺寸的 Pinch 数值或相机实现细节泄漏到 Map 输入契约中。

## 解决方案

以一个基于 EasyTouch 的 Map 手势适配器替代直接读取旧输入的示例适配器。该适配器负责设备手势识别、UI 归属、手势取消，以及转换为 Map 层输入。`MapInputDispatcher` 保持唯一的路由 seam：它为 Tap 构造 Map 点击上下文，把拖拽屏幕增量及 Pinch / 滚轮镜头距离意图路由给活动模式的 Handler。

活动的 TopDown Map 输入 Handler 消费路由后的镜头手势，并调用共享的正交相机控制行为；Gameplay Handler 不控制正交地图相机。缩放以 Pinch 开始中心或鼠标位置为锚点，保持有边界的即时正交缩放；即使到达相机边界，输入也视为已消费。旧的直接 Unity 输入适配器将被移除。本次不修改场景，仅提供场景接线说明。

## 用户故事

1. 作为移动端玩家，我希望轻点一个 Map Cell，以便当前 Gameplay 或 TopDown 模式能够响应我的选择。
2. 作为移动端玩家，我希望 Tap 按手指按下位置解析 Cell，以便抬起前的轻微移动不会改变我选择的目标。
3. 作为移动端玩家，我希望轻微手抖仍被视作 Tap，以便普通 Cell 选择足够可靠。
4. 作为移动端玩家，我希望用单指 Swipe 平移 Map 相机，以便查看较远的 Cell。
5. 作为移动端玩家，我希望首次平移包含超过 Tap 阈值后的全部位移，以便相机不会明显落后于手指。
6. 作为移动端玩家，我希望第二根手指落下时取消正在进行的单指平移，以便平移和 Pinch 缩放不会互相争夺控制权。
7. 作为移动端玩家，我希望双指张开时镜头拉近、合拢时镜头拉远，以便以合适比例查看 Map。
8. 作为移动端玩家，我希望 Pinch 始终锚定在双指开始时的中心，以便不稳定的双指中点移动不会让 Map 漂移。
9. 作为移动端玩家，我希望 Pinch 锚点下的 Map 位置在地图边界允许的范围内保持稳定，以便缩放直接且可预期。
10. 作为移动端玩家，我希望相机在允许的最小和最大缩放处可靠停止，以便镜头不会超出有效 framing。
11. 作为桌面开发者，我希望鼠标滚轮围绕当前指针位置缩放，以便在 Editor 中快速查看目标 Cell。
12. 作为桌面开发者，我希望使用 EasyTouch 的 Second Finger Simulation 模拟 Pinch，以便桌面验证与移动端使用相同输入后端。
13. 作为 UI 使用者，我希望从 UI 开始的任意 Map 手势均归 UI 所有，以便操作控件时不会意外选择、平移或缩放 Map。
14. 作为 UI 使用者，我希望只要 Pinch 的任一参与手指起始于 UI，该 Pinch 就被拒绝，以便 UI 操作不会意外缩放 Map。
15. 作为玩家，我希望任何曾变为多指的触摸序列都不能生成 Map Tap，以便 Pinch 结束时不会误选 Cell。
16. 作为 Gameplay 模式玩家，我希望 Gameplay 不消费正交地图相机手势，以便 Gameplay 视图不被 TopDown 相机控制逻辑改变。
17. 作为 TopDown 模式玩家，我希望相机手势交给活动的 TopDown Map 输入 Handler，以便 TopDown 通过正式的 Map 输入契约控制正交地图相机。
18. 作为会切换模式的玩家，我希望被打断的手势不会在新模式继续生效，以便持续按住的手指不会操作错误模式。
19. 作为开发者，我希望输入 Handler 接收与设备无关的镜头距离意图，以便将来替换 EasyTouch 时无需改变 Map 行为契约。
20. 作为开发者，我希望 `MapInputDispatcher` 始终是进入活动 Map Handler 的唯一入口，以便输入归属和无操作行为可观察、可测试。
21. 作为场景集成人员，我希望获得明确的 EasyTouch 配置说明，以便新适配器能在不依赖过期旧输入组件的情况下接线和验证。
22. 作为维护者，我希望移除已废弃的直接 Unity 输入适配器，以便同一个物理手势不会被两个竞争的状态机处理。

## 实现决策

- 现有 `MapInputDispatcher` 是首选且最高层的行为 seam。输入适配器不得自行拾取 Plot、检查活动模式或直接控制相机。Dispatcher 继续负责构造点击上下文，并把拖拽及镜头距离意图路由给活动 channel 的 `IMapInputHandler`。
- `IMapInputHandler` 新增一个镜头距离回调，并像现有点击和拖拽契约一样返回 `bool`。Handler 接受手势后返回 `true`；即便相机边界阻止实际移动，仍视为已消费。
- 镜头距离回调传递一个 Map 自有的小型上下文，而非 EasyTouch 的 `Gesture`：包含有符号、按屏幕尺寸归一化的缩放增量和屏幕像素锚点。Pinch 距离增加表示镜头拉近，减少表示镜头拉远。
- 回调不设置独立开始或结束方法。每次 Pinch 开始时，适配器先投递零增量上下文，Handler 以此重置状态；后续每个 Pinch 增量携带相同的 Pinch 开始锚点。每个独立滚轮输入也遵循“零标记、再实际增量”的约定。
- EasyTouch 适配器拥有原始手势状态：第二指开始时启动 Pinch、记录固定中点锚点、取消当前单指手势，并由 EasyTouch Pinch 事件产生后续变化。非零增量先通过可序列化、归一化的 Pinch 死区过滤。
- Pinch 意图按屏幕较短边归一化。正交相机控制层拥有可配置的 Pinch 灵敏度，并将累计意图转换为倍率式缩放变化；由此既避免分辨率影响，也能调节相机手感而不改输入契约。
- 滚轮保留为桌面调试入口：使用鼠标屏幕位置作为固定锚点，指针位于 UI 上时忽略，走相同 Dispatcher 回调，并保留每格 `1.15` 倍的现有缩放手感。
- Pinch 和滚轮仅由活动的 TopDown Map 输入 Handler 消费。无活动 Handler 或 Gameplay 活动时安静忽略。TopDown Handler 调用共享的正交相机控制逻辑，不重复平移或缩放数学。
- 共享相机控制沿用既有的即时、有边界的正交缩放和锚点保持中心偏移。在 Map framing 边界处中心会被钳制，因此锚点保持是尽力而为的行为；不引入惯性或 easing。
- EasyTouch `On_Swipe` 是单指相机平移来源，不能使用 `On_Drag`，因为后者属于可拾取 GameObject 的对象拖拽语义，而 Map 平移属于屏幕空间手势。Swipe 屏幕增量经既有拖拽回调路由，仅由活动 TopDown Handler 以现有 framing / 边界规则执行平移。
- EasyTouch `On_SimpleTap` 是 Map 点击来源。它投递 `gesture.startPosition`，使 Dispatcher 保持当前按下位置 Plot 拾取语义。EasyTouch 的 `StationaryTolerance` 配置为 20 像素，保持现有 Tap 边界。
- UI 归属在手势开始时确定，并在该次手势中保持不变。单指从 UI 开始时不产生 Map Tap 或 Swipe。Pinch 时适配器显式检查两根起始手指，只要任一根从 UI 开始便抑制整次 Pinch。参与双指操作将永久取消候选 Map Tap。
- 手势取消覆盖第二指加入、触摸取消、输入失活和 Map 模式切换。模式切换后，必须有新的物理手势才能向新的活动 Handler 投递输入。
- 移除旧的直接 Unity 输入 Tap、拖拽和缩放示例适配器。不得手动编辑任何 Unity `.meta` 文件。
- 本次不修改构建场景。实现会交付场景接线说明，覆盖 EasyTouch Second Finger Simulation、20 像素静止容差、初始 UI 归属、适配器注册、活动 Handler 注册及旧适配器组件移除；说明必须强调 UI 归属不得改为逐帧重新判定。

## 测试决策

- 测试可观察的 Map 行为，而不是 EasyTouch 私有状态或事件订阅细节。首选 seam 是“Dispatcher 到活动 Handler”的契约，沿用现有 Map 输入 Dispatcher 测试中的记录型 Handler 和可控 Map 相机 / Controller。
- 扩展 Dispatcher 测试：验证镜头距离上下文只路由给活动 channel 的 Handler、保持屏幕锚点与归一化增量、返回 Handler 的消费结果，并在无活动 Handler 时为无操作。
- 在手势适配器的 Map 意图边界测试：有效 Tap 发送按下位置；Swipe 路由屏幕增量；第二指取消 Swipe 与 Tap；Pinch 发送零标记以及带固定锚点的有符号归一化增量；滚轮在指针锚点发送零标记与实际增量。
- 从外部测试归属行为：UI 起始的单指手势不产生 Map 点击或拖拽；任一 UI 起始手指会拒绝 Pinch；多指序列之后不产生 Map 点击。
- 通过既有正交相机 seam 测试镜头行为：Pinch 拉近 / 拉远按配置灵敏度改变 zoom、尊重最小与最大 zoom、尽可能保持请求锚点，并在边界钳制时继续消费输入。
- 测试 TopDown Handler 的正交相机平移 / 缩放行为，并验证 Gameplay Handler 不消费或改变正交地图相机。
- 现有 Dispatcher 点击 / 拖拽测试，以及正交相机锚点、边界和 zoom 转换测试是测试先例。新测试应扩展这些行为套件，而不是创建第二套相机数学测试 harness。

## 范围外

- 编辑 Unity 场景、Prefab、EasyTouch Inspector 状态或 Unity `.meta` 文件。
- 让 Gameplay 或 TopDown 以外的其他任意视图模式控制正交地图相机。
- 相机惯性、easing、回弹、双击缩放、双指平移、旋转或 Twist 手势。
- 用透视相机移动替代现有正交相机模型。
- 重设计 Plot 拾取语义，或改变未命中点击交给活动 Map Handler 的方式。
- 替换 EasyTouch、引入 Unity Input System，或构建跨项目的通用手势框架。

## 补充说明

- Map 是由 Hex 坐标组成的 Cell 集合；本功能仅涉及表现层和输入路由，不改变 Cell 身份、Coordinate、Radius、Decoration、Overlay、归属或任何玩法状态。
- 适配器只在边界处转换设备手势。EasyTouch 是可替换的输入后端，而 Map Dispatcher 与模式 Handler 仅使用面向 Map 的点击、拖拽和镜头距离术语。
- 场景接线按用户要求延后。完成文档中的手动配置前，删除旧适配器脚本可能使已有场景组件暂时显示为 Missing Script；采用本功能时必须按接线说明完成替换。

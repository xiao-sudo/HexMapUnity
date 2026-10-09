# 01 — 统一 Map 输入分发并迁移点击处理

**要实现的行为：** 玩家可以继续在 Gameplay 或 TopDown Map 中点击以解析 Plot；每个 Map 视图模式拥有一个输入 handler 和一个模式 channel。统一的 Map 输入分发器将点击转换为完整的 Map 点击上下文，将拖拽屏幕增量原样路由而不执行 Map 拾取，并将某模式未处理某种手势视为正常的无操作。

**阻塞项：** 无——可立即开始。

**状态：** ready-for-agent

## 实现策略

- 将现有的点击分发器迁移为 `MapInputDispatcher`，使其成为 Map 输入的唯一模式路由入口。它继续持有当前的 active camera 和 active channel。
- 将点击 handler 合同迁移为 `IMapInputHandler`。每个 mode channel 只注册一个 handler；该接口包含点击与拖拽两个方法，暂不处理某手势的模式在相应方法中返回 `false`。
- 保留点击入口的现有拾取责任：仅点击会根据 active camera 解析 Plot，并构造完整 `MapClickContext`；未命中 Plot 仍要交付，以支持业务取消选择或关闭面板。
- 新增拖拽入口，仅将 `ScreenDelta` 原样交给当前 channel 的 handler。拖拽不访问 Map controller、不解析 Cell 或 Plot，也不改变最近点击上下文。
- 将 click 专用的 channel 名称迁移为 input 语义的名称，保留 Gameplay 与 TopDown 的 channel 值及模式切换模型。
- 将现有 Gameplay 与 TopDown 点击 handler 迁移为各自的输入 handler，保持已有点击业务行为。Gameplay 的拖拽实现先返回 `false`；TopDown 的拖拽业务留给后续 ticket 接入。
- 为保证迁移期间点击功能完整，现有点击手势适配器在本 ticket 中改为调用新 dispatcher；现有独立拖拽组件暂时保留到下一张 ticket 替换。

- [ ] Gameplay 和 TopDown 视图模式各自注册一个输入 handler，并保留当前的 Plot 点击行为，包括将未命中 Plot 的点击交付给 handler。
- [ ] 点击通过当前 active camera 拾取，并将屏幕坐标、Plot 结果和拾取状态交给当前模式 handler。
- [ ] 拖拽增量原样路由给当前模式 handler，且不会触发 Map 或 Plot 拾取。
- [ ] 拒绝在同一模式 channel 中重复注册 handler；未支持点击或拖拽中的某一种时，不输出诊断信息并安静忽略。
- [ ] EditMode 行为测试覆盖 active channel 路由、点击命中与未命中的上下文、无拾取的拖拽路由以及注册生命周期行为。

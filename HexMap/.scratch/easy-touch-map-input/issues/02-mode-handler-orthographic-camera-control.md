# 02 — 让 TopDown Handler 控制正交相机

**What to build:** 让活动的 TopDown Map 输入 Handler 消费 Map 拖拽与镜头距离意图，并通过共享正交相机控制实现平移、锚点缩放和边界行为；Gameplay 不控制正交地图相机。

**Blocked by:** 01 — 扩展 Map 镜头距离路由契约.

**Status:** ready-for-agent

- [ ] TopDown 模式消费 Dispatcher 路由的拖拽和镜头距离输入；Gameplay 明确不消费正交地图相机输入。
- [ ] 缩放使用可调 Pinch 灵敏度、保持屏幕锚点所对应的 Map 位置，并在最小或最大 Zoom 边界仍消费输入。
- [ ] 行为测试证明 TopDown 的正交相机语义保持既有 framing、中心钳制与锚点边界规则，且 Gameplay 不会改变该相机。

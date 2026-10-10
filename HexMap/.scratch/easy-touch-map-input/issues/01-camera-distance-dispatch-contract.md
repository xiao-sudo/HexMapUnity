# 01 — 扩展 Map 镜头距离路由契约

**What to build:** 让活动的 Map 输入 Handler 能通过现有 Dispatcher 接收带屏幕锚点的、设备无关的镜头拉近/拉远意图；无活动 Handler 时安静忽略，且调用方可得知输入是否被消费。

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] `IMapInputHandler` 与 `MapInputDispatcher` 支持镜头距离意图的路由，且保持既有点击、拖拽通道的行为不变。
- [ ] 输入上下文表达归一化的有符号增量与屏幕像素锚点，不暴露 EasyTouch 或相机实现类型。
- [ ] 行为测试覆盖活动 channel 路由、Handler 消费结果、上下文保真，以及无活动 Handler 时的无操作。

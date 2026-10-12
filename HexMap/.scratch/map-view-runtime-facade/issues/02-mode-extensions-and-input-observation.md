# 02 — 实现模式导向的扩展与输入观察契约

**What to build:** 让玩法扩展可以在地图视图门面初始化时安装，并以 Gameplay 或 TopDown 的业务语义登记唯一主输入 handler。地图点击、拖拽和相机距离调整通过活动模式的主 handler 处理后，外部观察者可订阅只读结果与是否消费，而无需接触内部输入通道或 Dispatcher。

**Blocked by:** 01 — 建立地图视图运行时门面与可重复生命周期。

**Status:** ready-for-agent

## Comments

### 已确认设计：固定主 handler，不引入扩展抽象

运行时不需要通用的 `IMapViewRuntimeExtension` 安装模型。它唯一需要安装的内容，就是为每个
支持的视图模式注册唯一的主 `IMapInputHandler`。

因此，`MapViewRuntimeFacade.Initialize` 使用固定且具有模式语义的参数：

```csharp
bool Initialize(
    MapViewMode initialMode,
    in MapViewModeWiring wiring,
    IMapInputHandler gameplayHandler,
    IMapInputHandler topDownHandler)
```

- `gameplayHandler` 在内部注册到 `SampleMapInputChannels.Gameplay`。
- `topDownHandler` 在内部注册到 `SampleMapInputChannels.TopDown`。
- Dispatcher 和数值 channel 始终是实现细节；调用方只使用视图模式语义，不能自行向 Dispatcher
  注册。
- handler 为 null，或任一 Dispatcher 注册失败时，写入可定位日志并使初始化返回 `false`。若第二个
  注册失败，立即注销第一个已成功注册的 handler，保证初始化的原子性。
- 固定参数从 API 结构上消除了对同一模式重复注册的可能。即使两个参数传入同一 handler 实例，也不
  因此拒绝：两个模式仍各自拥有唯一注册；该实现是否适合复用，由调用方决定。
- 成功初始化时只注册一次。`Stop()` 清空活动路由但保留注册，因此后续 `Start()` 不会重复安装。
  `Dispose()` 按反序注销：先 TopDown，后 Gameplay。
- `GameplayMapInputHandler` 与 `TopDownMapInputHandler` 不再从 `OnEnable` / `OnDisable` 自行注册。
  `SampleMapViewBootstrapper` 持有它们的序列化引用并将其传入 `Initialize`，使 Facade 成为唯一注册
  所有者。

输入观察仍是 Facade 的独立职责。它发布带有模式语义、在 handler 处理后的结果（包括 `Consumed`），
而不向观察者暴露 `MapInputDispatcher`。

- [ ] 扩展只在门面首次初始化时有序安装一次，并在门面销毁时按反序释放其资源；禁用再启用不重复安装。
- [ ] 每个支持模式在启动前必须恰有一个主输入 handler；缺失、重复或安装失败将拒绝启动并提供可定位诊断。
- [ ] 处理后输入观察、模式变更和运行时停止均通过返回注销句柄的订阅方式提供；已销毁 Unity 订阅目标会自动清除。
- [ ] 观察者异常不会阻止其他观察者；单次主 handler 异常只使本次输入失败，不停止地图运行时。
- [ ] 测试覆盖扩展安装/释放、模式 handler 互斥性、订阅取消、异常隔离与处理后 consumed 语义。

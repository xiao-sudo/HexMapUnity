# 03 — 将 EasyTouch 接入收敛为门面私有输入源

**What to build:** 让当前唯一的物理输入源 EasyTouch 由地图视图门面显式启动、停止和释放。用户的单指点击、拖拽、双指 Pinch 与编辑器滚轮仍产生相同的标准化地图输入，但门面停机后不再收到任何 EasyTouch 回调或保留静态订阅。

**Blocked by:** 02 — 实现模式导向的扩展与输入观察契约。

**Status:** ready-for-human

- [x] EasyTouch 输入适配器为门面私有的显式生命周期对象，而不是独立管理地图输入生命周期的组件。
- [x] 保持既有的 UI 起点过滤、手势路由版本失效、单指点击/拖拽、双指 Pinch、缩放死区和编辑器滚轮语义。
- [x] 启动时只订阅一次 EasyTouch 静态事件；停止和释放后完整退订并清空正在进行的手势状态。
- [x] 适配器不会泄露到门面的公开 API，未来可在相同边界增加其他输入源。
- [ ] 测试覆盖手势翻译、模式切换中断旧手势、停机退订和重复启停。

## Comments

### 2026-10-12 — 决策：EasyTouch 是门面拥有的私有输入源

将 EasyTouch 收敛为由 `MapViewRuntimeFacade` 内部拥有、显式管理生命周期的输入源。它不再是可挂载、可独立启停的 `MonoBehaviour`，也不通过门面的公开 API 暴露。

目标所有权与运行时结构：

```text
SampleMapViewBootstrapper（薄 Unity host）
└─ 显式驱动 MapViewRuntimeFacade
   ├─ MapViewRuntimeModeController
   │  └─ MapInputDispatcher 与两个模式的 handler
   └─ 私有 EasyTouchMapInputSource
      └─ EasyTouch 静态事件订阅与手势状态
```

实现决策：

- 将 `EasyTouchMapInputAdapter` 重命名并改造为 `internal sealed class EasyTouchMapInputSource`；它是普通 C# 对象，不再继承 `MonoBehaviour`。
- 该输入源不拥有 `GameObject`，不包含序列化字段，也不再有 `OnEnable` 或 `OnDisable` 回调。
- 构造函数接收 `MapInputDispatcher` 与 pinch dead-zone 配置。Dispatcher 在构造时固定，运行期间不允许替换。
- 仅提供内部生命周期操作：
  - `Start()`：订阅全部所需的 EasyTouch 静态事件。
  - `Stop()`：完整退订这些静态事件，并清理进行中的单指、双指和 pinch 状态。
  - `Dispose()`：幂等地调用 `Stop()`，并禁止后续继续使用。
  - `Tick()`：仅在 `UNITY_EDITOR` 下执行现有鼠标滚轮缩放轮询。
- `MapViewRuntimeFacade` 拥有该输入源，并将其纳入门面既有的显式 `Initialize` / `Start` / `Stop` / `Dispose` 生命周期。`SampleMapViewBootstrapper` 仍仅作为驱动门面的薄 Unity host。

以下既有行为必须保持不变：UI 起点过滤、手势路由版本失效、单指点击与拖拽、双指手势、pinch 语义及 dead-zone，以及编辑器鼠标滚轮缩放。

补充验收条件：

- 门面停止或释放后不得保留任何 EasyTouch 事件订阅；重复启动/停止后同样成立。
- 停止时必须清除活跃手势状态，使下次启动不能延续此前手势。
- 门面的任何公开 API 都不得暴露 EasyTouch 输入源，也不得允许替换其 Dispatcher。

### 2026-10-12 — 实现完成，待人工执行测试

- 已以 `EasyTouchMapInputSource` 替换 `EasyTouchMapInputAdapter`；输入源为 `internal sealed` 普通 C# 对象，由 `MapViewRuntimeFacade` 私有创建和持有。
- 门面在 `Start` 中订阅、在 `Stop` 中退订并清理手势状态、在 `Dispose` 中幂等释放输入源；编辑器滚轮由 `SampleMapViewBootstrapper` 每帧调用门面内部 `Tick()` 驱动。
- 场景接线工具不再向 Dispatcher 的 GameObject 添加 EasyTouch 输入组件，且未修改任何场景、Prefab 或 `.meta` 文件。
- 已更新实现文档。按要求，本次未运行测试；请人工执行本票列出的手势、停机退订和重复启停验证。

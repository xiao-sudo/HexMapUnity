# 04 — 迁移 Sample 场景装配并退役旧入口

**What to build:** 让 Sample 场景完全通过地图视图运行时门面运行。Gameplay 与 TopDown 的既有输入行为以门面扩展存在，场景装配工具仅配置门面和扩展，旧 Bootstrapper 与对 Switcher、Dispatcher、EasyTouch Adapter 的直接运行路径被退役。

**Blocked by:** 01 — 建立地图视图运行时门面与可重复生命周期；02 — 实现模式导向的扩展与输入观察契约；03 — 将 EasyTouch 接入收敛为门面私有输入源。

**Status:** ready-for-agent

## Comments

### Confirmed migration design (2026-10-12)

This ticket adopts a destructive migration to one runtime ownership path. `MapViewRuntimeFacade` is the sole Map view runtime owner; a new thin Unity host named `SampleMapViewRuntimeHost` replaces `SampleMapViewBootstrapper`.

#### Runtime host and lifecycle

The host stores only runtime-composition references: gameplay camera, `OrthographicMapCamera`, `MapInputDispatcher`, the two mode handlers, initial mode, and pinch dead zone. It constructs `MapViewModeWiring` and owns one facade instance. It contains no mode, input-translation, camera, or UI-policy logic.

Its lifecycle is fixed:

```text
Awake      -> construct facade and Initialize (validate and wire only; do not enter a mode)
OnEnable   -> Start (each enable enters the configured initial mode)
OnDisable  -> Stop (unsubscribe input, exit presentation, clear the active route)
OnDestroy  -> Dispose (idempotent final cleanup)
Update     -> Tick in UNITY_EDITOR only
```

The host records whether initialization succeeded. A failed `Initialize` disables the host and a later `OnEnable` must not call `Start` on an uninitialized facade. `Dispose` remains the final safety net if destruction occurs without an expected disable path.

The host may expose the facade, or a `SetViewMode(MapViewMode)` forwarding command, to business UI. It must not expose writable Dispatcher or handler routing APIs.

#### Dispatcher ownership and mode extensions

`MapViewRuntimeModeController`, reached only through `MapViewRuntimeFacade`, is the sole Sample implementation allowed to call:

```csharp
Register(...)
Unregister(...)
SetActiveCamera(...)
SetActiveChannel(...)
```

It registers exactly one handler for each mode during initialization, selects the active route while entering a mode, clears camera and channel on stop, and unregisters both handlers on disposal.

`GameplayMapInputHandler` and `TopDownMapInputHandler` remain the modes' primary input extensions. They keep their gameplay selection and top-down camera-control responsibilities, respectively. They must not own, serialize, expose, or query `MapInputDispatcher`.

#### Dynamic viewport input context

The TopDown handler cannot serialize a fixed viewport camera: it can be authored as a Prefab and the active camera is selected dynamically by the current mode. It therefore receives viewport information as input context rather than reading `Dispatcher.ActiveCamera`.

Introduce a drag context such as:

```csharp
public readonly struct MapDragContext
{
    public Vector2 ScreenDelta { get; }
    public float ViewportAspect { get; }
}
```

and add `ViewportAspect` to `MapCameraDistanceContext`. Evolve `IMapInputHandler` so drag and camera-distance callbacks consume these contexts. `MapInputDispatcher` remains the sole owner of `m_ActiveCamera`, derives the current viewport aspect internally, and supplies it when dispatching drag and pinch/scroll intents. If no active camera exists, those intents are not dispatched and return `false`.

This preserves dynamic camera routing and Prefab reuse while keeping handlers independent of Dispatcher.

#### Editor wiring and retired entries

`SampleMapViewBootstrapper` has been physically removed after the target Sample scene migration. `Wire Two-Mode Sample Scene` repeatably configures and diagnoses the host, the independent UI presenter, the two handlers, and panel bindings. It may not register handlers, alter Dispatcher runtime state, add input components, configure handler Dispatcher references, or create a second lifecycle owner.

Known direct input entries are retired, including the old EasyTouch adapter, `MapClickTapInput`, `OrthographicMapDragInput`, and `OrthographicMapZoomInput`.

For Missing Script cleanup, auto-remove only components whose serialized script GUID is a known retired-entry GUID. Do not remove unknown Missing Script components. Report every unconfirmed occurrence with its GameObject hierarchy path, component slot, and GUID when available.

#### UI policy

`MapViewRuntimeFacade`, `MapViewModeWiring`, the mode controller, the mode states, and the runtime host do not own UI roots or set UI GameObjects active. `SampleMapViewModeUiPresenter` is an outer Sample-level presentation component: an outer application coordinator explicitly binds it to a facade, after which it subscribes to mode-changed and stopped events, shows the root selected by the completed mode transition, and hides both roots while stopped.

This ticket introduces no mode-switch button, Toggle, UnityEvent, or HUD policy. Business UI chooses its own policy and invokes only:

```csharp
runtime.SetViewMode(MapViewMode.TopDown);
runtime.SetViewMode(MapViewMode.Gameplay);
```

#### Verification additions

In addition to existing functional coverage, verify:

- failed host initialization cannot later start through enable/disable;
- disable/enable stops all routing and restarts at the configured initial mode;
- repeated start/stop does not duplicate physical-input subscriptions or handler registrations;
- stop clears active camera and channel; disposal unregisters both handlers exactly once;
- drag, pinch, and editor scroll receive the aspect of the current active camera through input contexts, and do not dispatch without one;
- TopDown and Gameplay handlers contain no Dispatcher reference or public Dispatcher entry point;
- the result has exactly one host and no old Bootstrapper or known legacy input entries;
- repeatable wiring does not create legacy input components and reports missing or ambiguous configuration clearly.

- [ ] Sample 的 Gameplay 与 TopDown 地图点击、相机拖拽和相机距离行为通过各自模式的唯一主 handler 保持可用且互斥。
- [ ] 场景装配工具只认识门面及其扩展，不再直接接线旧的模式切换、输入分发或 EasyTouch 组件。
- [ ] Sample 业务 UI 若需要切换模式，只调用门面命令；门面本身不引入 UI 策略。
- [ ] 旧 Bootstrapper 和旧直接入口不再构成 Sample 的可执行运行路径，避免两个生命周期所有者并存。
- [ ] 通过场景/编辑器接线验证和完整回归测试，确认模式切换、地图输入与停机清理均可工作。

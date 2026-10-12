# 01 — 建立地图视图运行时门面与可重复生命周期

**What to build:** 为上层组合根提供一个唯一的地图视图运行时模块：它显式接受 Gameplay / TopDown 的初始模式和 MapViewModeWiring，随后由调用方显式启动、停止和释放。它不依赖 Awake、OnEnable、OnDisable 或 Inspector 序列化来收集运行时 Camera 等依赖；配置错误时不进入半可用状态，而是返回可检查的诊断。

**Blocked by:** None — can start immediately.

**Status:** done

## Comments

- `MapViewRuntimeFacade` is now the only public map-view lifecycle API. The former
  `MapViewModeSwitcher` Unity adapter and its parallel state-machine implementation were removed.
- `MapViewRuntimeModeController` is the sole internal mode driver and implements
  `IMapViewModeHost` explicitly, so entering a mode cannot be invoked through the controller type.
- The scene composition root initializes, starts, and disposes the facade; the editor wirer now
  wires that composition root rather than UI button callbacks.

- [ ] 运行时模块只提供显式的 Initialize(initialMode, wiring)、Start、SetViewMode、Stop 和 Dispose 生命周期；Initialize 不自动进入任何模式。
- [ ] Initialize 从运行时传入的 MapViewMode 与 MapViewModeWiring 验证所有依赖，成功后处于“已初始化、未运行”状态；失败时返回完整可检查诊断，且 Start 不可运行。
- [ ] Start 进入初始模式并发布模式变更通知；Stop 退出呈现、还原临时相机状态、清理输入路由并发布独立停止通知；Stop 与 Dispose 均幂等。
- [ ] Camera、UI Root、Dispatcher 和内部模式切换实现不出现在运行接口中，除 Initialize 的组合参数外不向业务调用方泄露。
- [ ] 测试通过运行时模块的显式接口构造 MapViewModeWiring 并验证初始化、启动、切换、停止、重复停止与释放，无需反射或触发 Unity 隐式生命周期。

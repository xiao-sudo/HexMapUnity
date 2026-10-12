# EasyTouch 地图输入

## 目的

`MapViewRuntimeFacade` 私有拥有的 `EasyTouchMapInputSource` 将设备手势转换为既有的 `MapInputDispatcher` 语义：轻点解析为地图点击，单指 Swipe 解析为屏幕拖拽，双指 Pinch 与仅 Editor 可用的鼠标滚轮解析为相机距离意图。

输入源不拣取 Cell、不选择视图模式，也不直接修改相机。`MapInputDispatcher` 仍是进入活动 `IMapInputHandler` 的唯一入口：TopDown Handler 消费相机拖拽与距离意图，Gameplay Handler 对两者返回 `false`。

## 代码地图

- `Assets/Scripts/HexMap/Sample/EasyTouchMapInputSource.cs`：门面私有的普通 C# 输入源；订阅 EasyTouch 手势、维护手势归属，并向 Dispatcher 路由 Map 输入。
- `Assets/Scripts/HexMap/Sample/MapViewRuntimeFacade.cs`：拥有输入源，并在显式的 `Initialize` / `Start` / `Stop` / `Dispose` 生命周期中创建、启停和释放它。
- `Assets/Scripts/HexMap/UnityRuntime/MapInputDispatcher.cs`：按照活动 channel 路由点击、拖拽和 `MapCameraDistanceContext`；`InputRouteRevision` 用来使跨模式或跨相机的旧手势失效。
- `Assets/Scripts/HexMap/UnityRuntime/MapCameraDistanceContext.cs`：设备无关的距离输入契约，包含屏幕像素锚点和按屏幕短边归一化的有符号增量；正值表示拉近。
- `Assets/Scripts/HexMap/Sample/TopDownMapInputHandler.cs`：在 TopDown 模式将路由后的距离意图交给正交相机控制器。
- `Assets/Scripts/HexMap/Sample/OrthographicMapCameraInputController.cs`：以指数方式把距离意图变成缩放倍率，并通过现有 framing 和边界规则保持锚点。
- `Assets/Tests/EditMode/HexMap/Sample/MapInputHandlerCameraControlTests.cs`：验证距离意图经过 Dispatcher 后由 TopDown 消费，其中包含一格滚轮等价于 `1.15` 倍缩放的回归用例。

`HexMap.Sample.asmdef` 已引用 `EasyTouch` 和 `UnityEngine.UI`；后者供输入源在手势开始时做 UI 射线检测。

## 运行流程

### 单指轻点和 Swipe

1. `On_TouchStart` 记录主手指、按下位置、起始 UI 归属和 `InputRouteRevision`。
2. 首次 `On_Swipe` 发送 `position - startPosition`，补回 EasyTouch 的静止容差；后续 Swipe 发送逐帧 `deltaPosition`。
3. `On_SimpleTap` 使用保留的按下位置调用 `OnMapClicked`，由 Dispatcher 构造 `MapClickContext`。
4. `On_SwipeEnd`、`On_SimpleTap`、取消、禁用或路由版本改变均会清理候选状态。

从 UI 起始的单指序列不会成为 Map 手势。第二指到来会永久取消单指候选资格，因此双指结束或其中一指先抬起都不会错误产生 Map Tap 或 Swipe。

### 双指 Pinch

1. `On_TouchStart2Fingers` 首先取消单指候选。
2. 输入源对两个原生触点的起始屏幕位置逐个进行 UI 射线检测；任何一个命中 UI，就拒绝整次 Pinch。桌面端 EasyTouch Second Finger Simulation 没有原生 `Input.touches` 条目时，退回使用 EasyTouch 的 UI 结果。
3. 若 Pinch 有效，输入源锁定 `gesture.startPosition` 作为锚点，并记录当前 `InputRouteRevision`。随后先发送同一锚点、增量为零的 `MapCameraDistanceContext` 作为开始标记。
4. 后续 `On_Pinch` 把 `gesture.deltaPinch / min(Screen.width, Screen.height)` 转为归一化增量。张开两指的正增量表示拉近；收拢两指的负增量表示拉远。
5. 小于 pinch dead-zone 配置的单次增量被过滤。默认值为 `0.003`，由 `SampleMapViewBootstrapper` 的 Inspector 配置并在门面初始化时固定。
6. 后续回调只使用开始时锁定的锚点；双指中点即使移动也不会让地图漂移。路由版本变化、`On_PinchEnd`、取消、门面停止或释放都会清除 Pinch 状态。

零增量标记是距离输入的开始契约，不额外引入 backend 特有的 begin/end 方法。当前正交相机控制器是无状态的，但未来消费方如需在一次 Pinch 开始时重置状态，应识别该标记而不是绕过 Dispatcher。

### Editor 鼠标滚轮

鼠标滚轮仅用于 Unity Editor 中验证地图缩放，相关常量和 `RouteMouseWheel` 都包在 `#if UNITY_EDITOR` 内；薄 Unity host 在编辑器帧中调用门面的 `Tick()`，因此不会编译进 Player。

每个非零滚轮输入都是一段独立距离手势：

1. 若鼠标指针命中 UI，不发送输入。
2. 使用 `Input.mousePosition` 作为锚点，先发送零增量标记。
3. 再发送 `mouseScrollDelta.y * log(1.15)`。正交相机控制器采用 `exp(delta * sensitivity)`，所以灵敏度为默认值时，滚轮向上严格对应 `×1.15`，向下对应 `÷1.15`。

滚轮同样只调用 Dispatcher，不得直接设置相机 Zoom。它是编辑器验证入口，不是运行时鼠标控制功能。

## 实现约束与扩展点

- UI 归属只在单指或 Pinch 的开始时确定，之后不得逐帧重新判定。Physics Raycaster 命中的是 Map 世界对象，不是 UI，不得据此抑制地图相机输入。
- EasyTouch 类型只能存在于门面私有的 `EasyTouchMapInputSource` 边界；`MapInputDispatcher`、`IMapInputHandler` 和相机控制器只使用 Map 自有的点击、拖拽和距离术语。
- 输入源不是 `MonoBehaviour`，不拥有 `GameObject` 或序列化字段；`Start()` 只订阅一次静态事件，`Stop()` 完整退订并清理手势状态，`Dispose()` 幂等地停止并禁止后续使用。
- 任何新增的缩放输入后端都应生成 `MapCameraDistanceContext` 并调用 Dispatcher；不要直接控制 `OrthographicMapCamera`，也不要增加第二套相机手势状态机。
- 若增加新的相机消费模式，应由该模式注册自己的 `IMapInputHandler` 并明确决定是否消费距离意图。Gameplay 保持不消费正交地图相机手势。
- 不修改场景、Prefab、EasyTouch Inspector 状态或 Unity `.meta` 文件。场景接线由集成人员完成。

## 场景接线

1. 确保场景中存在并启用 EasyTouch 对象，并将 `StationaryTolerance` 设为 **20** 像素。
2. 将 `SampleMapViewBootstrapper` 的 Dispatcher、两个 Handler 和模式引用接线完整；门面会在启动时创建私有输入源，无需也不得向任何 GameObject 添加 EasyTouch 输入组件。
3. 需要时，在 `SampleMapViewBootstrapper` 的 Inspector 中调整 pinch dead-zone；运行后该值在输入源生命周期中固定。
4. 确保活动的 TopDown Handler 已注册，且引用 `OrthographicMapCameraInputController`；Gameplay Handler 仍应注册但不消费相机输入。
5. 移除已废弃的 `EasyTouchMapInputAdapter`、`MapClickTapInput`、`OrthographicMapDragInput` 与 `OrthographicMapZoomInput` 组件。旧脚本不存在时，场景会显示 Missing Script，需由集成人员手动清除。

## 验证

运行 `HexMap.Sample.Tests.EditMode` 和 `HexMap.UnityRuntime.Tests.EditMode`。当前相机控制回归用例验证：TopDown 经 Dispatcher 收到 `log(1.15)` 距离意图时，Zoom 从 `2.0` 变为 `2.3`。

在已接线的 Unity Editor 场景手动验证：

- TopDown 中，张开/收拢双指分别拉近/拉远，且缩放围绕 Pinch 开始中点；在边界仍由 TopDown 消费输入。
- 任一参与 Pinch 的手指从 UI 开始时，不缩放；Pinch 结束后不会触发 Cell 点击。
- Pinch 期间切换模式或相机、取消触摸、停止或释放门面后，旧手势不影响新路由。
- 非 UI 区域一格滚轮分别产生 `×1.15` / `÷1.15`；UI 上滚轮不缩放；Player 构建中不包含该鼠标入口。

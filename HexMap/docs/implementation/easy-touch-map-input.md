# EasyTouch 地图点击与滑动输入

## 目的

`EasyTouchMapInputAdapter` 是面向设备输入的适配器，负责将 EasyTouch 的轻点与单指滑动转换为既有的 `MapInputDispatcher` 输入语义。它不自行拾取 Cell、不选择视图模式，也不直接控制相机。

双指 Pinch 与鼠标滚轮的相机距离输入不属于本实现，留给 04 号票处理。

## 代码地图

- `Assets/Scripts/HexMap/Sample/EasyTouchMapInputAdapter.cs`：订阅 EasyTouch 手势生命周期事件，维护一次候选单指手势，并调用 Dispatcher。
- `Assets/Scripts/HexMap/UnityRuntime/MapInputDispatcher.cs`：除既有点击和拖拽路由外，提供 `InputRouteRevision`；活动 channel 或相机改变时，使进行中的物理手势失效。
- `Assets/Scripts/HexMap/Sample/TopDownMapInputHandler.cs`：消费已路由的拖拽增量，并委托 `OrthographicMapCameraInputController` 平移相机。
- `Assets/Scripts/HexMap/Sample/GameplayMapInputHandler.cs`：有意不消费相机拖拽输入。
- `Assets/Scripts/HexMap/Sample/Editor/TwoModeSampleSceneWirer.cs`：运行编辑器菜单命令时，在 Dispatcher 所在对象上添加并接线 adapter。
- `Assets/Scripts/HexMap/Sample/HexMap.Sample.asmdef`：引用 `EasyTouch` 程序集，使 adapter 可以使用插件的 `Gesture` 类型与静态事件。

## 运行流程

1. 收到 `EasyTouch.On_TouchStart` 时，adapter 记录主手指、`startPosition`、是否从 UI 开始，以及 `MapInputDispatcher.InputRouteRevision`。
2. 首次收到 `On_Swipe` 时，adapter 发送 `position - startPosition`；后续 Swipe 发送 EasyTouch 的 `deltaPosition`。这样可以保留 EasyTouch 判断是否越过静止容差时累计的位移。
3. 收到 `On_SimpleTap` 时，adapter 用保存的按下位置调用 `OnMapClicked`。仅 Dispatcher 构造 `MapClickContext` 并路由给活动 Handler。
4. `On_SwipeEnd` 和 `On_SimpleTap` 都会清理已完成的候选手势。被 UI 或多指规则拒绝的 Tap 同样是终态，不能阻塞下一次物理触摸。
5. 双指开始会把候选手势标记为多指；即使其中一根手指稍后抬起，也不会产生 Map Swipe 或 Map Tap。
6. `On_Cancel`、`On_Cancel2Fingers`、adapter 停用、Dispatcher 缺失或路由版本变化都会清理候选手势。因此，模式切换前开始的手势不会作用于切换后的模式。

## EasyTouch 回调与设计取舍

`EasyTouchMapInputAdapter` 订阅 EasyTouch 的静态事件，而不是在 `Update` 中轮询 Unity 的旧 `Input`。这样设备输入识别、20 像素静止容差和桌面端 EasyTouch 模拟都由同一个后端处理；Map 层只接收抽象后的点击位置或拖拽增量。

| 回调 | adapter 的处理 | 为什么这样做 |
| --- | --- | --- |
| `On_TouchStart` | 记录主手指编号、按下位置、UI 起始归属和当前 `InputRouteRevision`。 | 后续事件只属于这一次物理手势。UI 归属必须在按下瞬间固定，不能因手指后来移出 UI 而变为 Map 手势。记录 revision 能阻止模式切换前的手势泄漏到新模式。 |
| `On_Swipe` | 若手势仍有效，首次传递 `position - startPosition`，后续传递 `deltaPosition` 到 `OnMapDragged`。 | EasyTouch 在移动超过 `StationaryTolerance` 后才开始发出 Swipe；首次完整位移能补回被容差吞掉的前段，避免相机比手指落后约 20 像素。后续增量可直接驱动既有 TopDown 相机平移。 |
| `On_SwipeEnd` | 清理候选手势。 | Swipe 不会产生 `On_SimpleTap`。若不在结束时清理，下一次按下会被误判为第二根手指，后续输入会被错误丢弃。 |
| `On_SimpleTap` | 若仍是有效单指 Map 手势，使用保存的按下位置调用 `OnMapClicked`；无论是否被拒绝，之后都清理状态。 | 点击目标应是按下时瞄准的 Cell，而不是抬起时的位置。UI 起始或多指造成的 Tap 虽不会路由，也必须结束本次状态。 |
| `On_TouchStart2Fingers` | 将当前候选单指标记为“曾变为多指”。本票不发送 Pinch。 | 参与过第二指的序列不能在结束时回退为点击，否则 Pinch 或双指操作结束时可能误选 Cell。Pinch 距离输入和双指 UI 归属检查留给 04 号票。 |
| `On_Cancel` | 当前主手指被系统取消时清理状态。 | 处理系统取消、输入中断等非正常结束，确保旧手势状态不会残留。 |
| `On_Cancel2Fingers` | 无条件清理状态。 | 双指序列已不可能恢复为可用的单指 Map 手势，直接终止最安全。 |

没有订阅 `On_Drag`：EasyTouch 的 Drag 带有可拖拽 GameObject 的语义，而 Map 平移属于屏幕空间手势，应使用 `On_Swipe`。adapter 也不订阅或处理 Pinch、滚轮、旋转和双指平移；这些将在 04 通过同一个 Dispatcher 的相机距离意图独立实现，避免多个状态机竞争同一次物理手势。

## 场景接线

本改动有意不修改场景或 Prefab。

1. 确保场景中存在并启用了 EasyTouch 对象。
2. 将其 `StationaryTolerance` 设置为 **20** 像素。
3. 在 `MapInputDispatcher` 所在的 GameObject 上添加 `EasyTouchMapInputAdapter`，并在 Inspector 中为其赋值 Dispatcher。也可运行 **Tools > Hex Map > Wire Two-Mode Sample Scene**，由工具添加并赋值。
4. 保存场景前移除旧的 `MapClickTapInput`、`OrthographicMapDragInput` 和 `OrthographicMapZoomInput` 组件。它们的脚本已不存在，先前的组件引用会显示为 Missing Script，需手动移除。

## 约束与扩展点

- UI 归属只能在触摸开始时通过 `Gesture.IsOverUIElement()` 采样。不要改为逐帧 UI 判定；从 UI 开始的手势在离开 UI 后仍属于 UI。
- EasyTouch 专有类型必须留在 `EasyTouchMapInputAdapter`。`MapInputDispatcher` 和 `IMapInputHandler` 只接受 Map 领域的屏幕位置与增量。
- 不要绕过 Dispatcher 直接移动相机或拾取 Cell。活动 channel 是唯一决定已路由拖拽是否被消费的所有者。
- 04 号票应在检查两根参与手指的起始归属后，扩展 adapter 的多指生命周期来发送相机距离意图。不得引入第二套手势状态机，也不得恢复被移除的 Unity Input adapter。

## 验证

执行 `HexMap.UnityRuntime.Tests.EditMode` 与 `HexMap.Sample.Tests.EditMode` 程序集。Dispatcher 测试覆盖路由版本行为；既有 Handler 测试覆盖 TopDown 消费拖拽与 Gameplay 拒绝拖拽的行为。

在已接线的场景中手动验证：点击 Map Cell、TopDown 单指平移、从 UI 开始的手势、滑动中加入第二指，以及按住手指时切换模式。确认第二指不会在结束时造成 Cell 误选，且旧 adapter 组件均已移除。
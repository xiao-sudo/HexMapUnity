# EasyTouch map tap and swipe input

## Purpose

`EasyTouchMapInputAdapter` is the device-facing adapter for Map tap and one-finger swipe. It translates EasyTouch events into the existing `MapInputDispatcher` vocabulary without picking a Cell, selecting a mode, or controlling a camera directly. Pinch and mouse-wheel distance input are intentionally outside this implementation and belong to ticket 04.

## Code map

- `Assets/Scripts/HexMap/Sample/EasyTouchMapInputAdapter.cs`: subscribes to EasyTouch lifecycle events, owns one candidate single-finger gesture, and calls the dispatcher.
- `Assets/Scripts/HexMap/UnityRuntime/MapInputDispatcher.cs`: publishes `InputRouteRevision` in addition to its existing click and drag routing. A changed active channel or camera invalidates in-flight physical gestures.
- `Assets/Scripts/HexMap/Sample/TopDownMapInputHandler.cs`: consumes routed drag deltas and delegates panning to `OrthographicMapCameraInputController`.
- `Assets/Scripts/HexMap/Sample/GameplayMapInputHandler.cs`: deliberately does not consume camera drag input.
- `Assets/Scripts/HexMap/Sample/Editor/TwoModeSampleSceneWirer.cs`: adds and wires the new adapter on the dispatcher host when its editor command is run.
- `Assets/Scripts/HexMap/Sample/HexMap.Sample.asmdef`: references the `EasyTouch` assembly so the adapter can use the plugin's `Gesture` type and static events.

## Runtime flow

1. On `EasyTouch.On_TouchStart`, the adapter records the primary finger, its `startPosition`, whether it began over UI, and `MapInputDispatcher.InputRouteRevision`.
2. On the first `On_Swipe`, the adapter sends `position - startPosition`; later swipe events send EasyTouch's per-event `deltaPosition`. This retains movement accumulated while EasyTouch was deciding that the touch exceeded its stationary tolerance.
3. On `On_SimpleTap`, the adapter sends the stored press position to `OnMapClicked`. The dispatcher alone resolves it to a `MapClickContext` and routes it to the active handler.
4. `On_SwipeEnd` and `On_SimpleTap` clear the completed candidate. A rejected UI or multi-touch tap is also terminal, so it cannot block the following physical touch.
5. A two-finger start marks the candidate gesture as multi-touch. It produces neither a Map swipe nor a Map tap, including when one finger is released later.
6. `On_Cancel`, `On_Cancel2Fingers`, disabling the adapter, a missing dispatcher, or a changed route revision clear the candidate gesture. A gesture started before a mode switch therefore cannot act in the mode entered afterward.

## EasyTouch 回调与设计取舍

`EasyTouchMapInputAdapter` 订阅的是 EasyTouch 的静态事件，而不是在 `Update` 中轮询 Unity 的旧 `Input`。这样设备输入的识别、20 像素静止容差和桌面端的 EasyTouch 模拟都由同一个后端负责；Map 层只接收已经抽象好的点击位置或拖拽增量。

| 回调 | adapter 的处理 | 为什么这样做 |
| --- | --- | --- |
| `On_TouchStart` | 记录主手指编号、按下位置、UI 起始归属和当前 `InputRouteRevision`。 | 后续事件只会被视为这一次物理手势的一部分。UI 归属必须在按下瞬间固定，不能因手指后来移出 UI 而变成 Map 手势。记录 revision 则使模式切换前的手势不能泄漏到新模式。 |
| `On_Swipe` | 若手势仍有效，第一次传递 `position - startPosition`，后续传递 `deltaPosition` 到 `OnMapDragged`。 | EasyTouch 在移动超过 `StationaryTolerance` 后才开始发 Swipe；第一次使用完整位移可补回被容差吞掉的前段，避免镜头比手指落后约 20 像素。后续增量可直接驱动现有 TopDown 相机平移。 |
| `On_SwipeEnd` | 清理候选手势。 | Swipe 不会产生 `On_SimpleTap`。如果不在结束时清理，下一次按下会被误判为第二根手指，之后的输入会被错误丢弃。 |
| `On_SimpleTap` | 若仍是有效的单指 Map 手势，使用保存的按下位置调用 `OnMapClicked`；无论是否被拒绝，之后都清理状态。 | 点击目标应是按下时瞄准的 Cell，而不是抬起时的位置。UI 起始或多指造成的 Tap 虽不会路由，也必须结束本次状态。 |
| `On_TouchStart2Fingers` | 将当前候选单指标记为“曾变为多指”。本票不发送 Pinch。 | 第二指参与过的序列不能在结束时回退为点击，否则 Pinch 或双指操作结束时可能误选 Cell。Pinch 的距离输入和双指 UI 归属检查留给票 04。 |
| `On_Cancel` | 当前主手指被系统取消时清理状态。 | 处理系统取消、输入中断等非正常结束，确保旧手势状态不会残留。 |
| `On_Cancel2Fingers` | 无条件清理状态。 | 双指序列已不可能恢复成可用的单指 Map 手势，直接终止最安全。 |

没有订阅 `On_Drag`：EasyTouch 的 Drag 带有可拖拽 GameObject 的语义，而 Map 平移是屏幕空间手势，应使用 `On_Swipe`。也没有在 adapter 中订阅或处理 Pinch、滚轮、旋转和双指平移；这些会在 04 通过同一个 Dispatcher 的相机距离意图单独实现，避免多个状态机竞争同一次物理手势。

## Scene wiring

This change intentionally does not modify scenes or prefabs.

1. Ensure an EasyTouch object is present and enabled in the scene.
2. Set its `StationaryTolerance` to **20** pixels.
3. Add `EasyTouchMapInputAdapter` to the GameObject that owns `MapInputDispatcher`, then assign that dispatcher in the adapter's Inspector field. Alternatively use **Tools > Hex Map > Wire Two-Mode Sample Scene**; it adds and assigns the adapter.
4. Remove the old `MapClickTapInput`, `OrthographicMapDragInput`, and `OrthographicMapZoomInput` components before saving the scene. Their scripts no longer exist, so previous component references appear as Missing Script until removed.

## Constraints and extension points

- UI ownership is sampled only from `Gesture.IsOverUIElement()` at touch start. Do not change it into a per-frame UI check; a gesture that began on UI remains UI-owned after leaving it.
- Keep EasyTouch-specific types in `EasyTouchMapInputAdapter`. `MapInputDispatcher` and `IMapInputHandler` accept only Map-domain screen positions and deltas.
- Do not bypass the dispatcher for camera movement or Cell picking. The active channel is the sole owner of whether a routed drag is consumed.
- Ticket 04 should extend the adapter's multi-touch lifecycle to emit camera-distance intent only after checking both participating fingers' start ownership. It must not reintroduce a second gesture state machine or revive the removed Unity Input adapters.

## Verification

Run the `HexMap.UnityRuntime.Tests.EditMode` and `HexMap.Sample.Tests.EditMode` assemblies. The dispatcher tests include route-revision behavior; existing handler tests cover TopDown drag consumption and Gameplay refusal.

In a wired scene, manually verify a map-cell tap, a TopDown single-finger pan, a gesture beginning on UI, a second finger joining an in-flight swipe, and holding a finger while switching modes. Confirm that the second finger produces no late Cell selection and that the old adapter components are absent.

# 地图点击分派

本文说明"同一个 Plot 被点击，如何按当前模式交给不同的处理者"。相机与取景见 `docs/implementation/orthographic-map-camera.md` 与 `-internals.md`。

## 1. 契约：入口与出口

```
外围输入（InputSystem / EasyTouch / 自写）      ← 识别点击 vs 拖拽、UI 防穿透
        │  OnMapClicked(Vector2 screenPosition)
        ▼
MapClickDispatcher (HexMap.UnityRuntime)       ← 拾取 + 构造上下文 + 按通道派发
        │
        ▼
IMapClickHandler.OnMapClicked(in MapClickContext) → bool   ← 注册进来的处理者
```

| 层 | 负责 | 不负责 |
| --- | --- | --- |
| 外围 | 这次指针交互是不是"点击"；是否落在 UI 上；用哪套输入后端 | 用哪个相机、点到了哪个 Plot |
| `MapClickDispatcher` | 用活动相机拾取、构造上下文、按活动通道找处理者、诊断日志 | 点击之后做什么 |
| `IMapClickHandler` | 收到上下文后的业务 | 怎么分辨模式（上下文里没有模式，因为处理者本身就是为某个通道注册的） |

**拾取必须在接口内部**。否则外围就得知道"用哪个相机""点空了算不算一次点击""点在 UI 上要不要吞掉"——这三件事都属于内部。

## 2. 类型

| 类型 | 位置 | 说明 |
| --- | --- | --- |
| `MapClickChannels` | `HexMap.UnityRuntime` | 只有 `None = 0`。**具体通道由应用层定义**（例如 `Gameplay = 1` / `TopDown = 2`），这样运行时库不认识任何模式名 |
| `MapClickContext` | `HexMap.UnityRuntime` | `ScreenPosition` / `HasPlot` / `PlotId` / `PickStatus` |
| `IMapClickHandler` | `HexMap.UnityRuntime` | `bool OnMapClicked(in MapClickContext)` |
| `MapClickDispatcher` | `HexMap.UnityRuntime` | 注册表 + 入口 + 时序 |

### 为什么用 `int` 通道号而不是模式枚举

模式是**应用层/场景层**的概念（"玩家打开了地图功能"），`HexMap` 的领域里没有"模式"。把 `MapViewMode.TopDown` 放进可复用库，换一个游戏或换一套模式名就废了。库只认"通道号"，**"当前哪个通道激活"由应用层决定**。

## 3. `OnMapClicked` 的时序

```
1. 无 Controller            → 记一次错误日志，return false
2. 无活动相机               → 记一次错误日志，return false
3. 活动通道 == None         → 静默 return false
4. 该通道无处理者           → 记一次错误日志，return false
5. 拾取：Controller.PickPlotAtScreenPosition(screenPosition, 活动相机)
6. 记下 LastContext（含 PickStatus）
7. 处理者已被销毁           → 记一次错误日志，return false
8. handler.OnMapClicked(context)
```

`PickPlotAtScreenPosition` 已覆盖全部失败原因（`MapNotInitialized` / `NoCamera` / `NoPlaneIntersection` / `OutsideMap` / `NoSelectablePlot`），直接转成 `PickStatus`。

### 四条刻意的语义

| 决定 | 理由 |
| --- | --- |
| **点空也派发**（`HasPlot = false`） | "点空白关闭面板"是常规需求。点空不派发的话，处理者永远收不到"关掉"的信号 |
| **`PickStatus` 进入上下文** | "点了没反应"必须能查：被 UI 吞掉？相机没设？通道没注册？点在界外？**这是把这类问题从"猜"变成"查"的唯一字段** |
| **`LastContext` 保留最近一次** | 不用开日志就能问"刚才那下解析成了什么"。注意"在拾取之前就被丢弃的点击"**不会**留下上下文，避免读到过期结果 |
| **UI 防穿透不在这里** | 那是外围的职责：它知道指针 id，也知道 `EventSystem`。分派器只认屏幕坐标 |

### 时序上与地图初始化的关系

`GvgMapRuntimeController.Awake` → `BuildMapFromView()` → `HexMapView.Build()` 建好**地图**；但 **Plot 注册表要等 `TryInitialize`**（通常是 `Facade.Start`）。所以：

- `TryInitialize` 之前：`PickStatus = MapNotInitialized`，`HasPlot` 恒为 false，**点击仍然派发**；
- 之后：正常解析出 `PlotId`。

即"预览先能点、Plot 后能点"是自然成立的，不需要额外安排时序。

## 4. 注册规则

```csharp
private void OnEnable()  { m_Dispatcher.Register(MyChannel, this); }
private void OnDisable() { m_Dispatcher.Unregister(MyChannel, this); }
```

| 规则 | 行为 | 原因 |
| --- | --- | --- |
| 同通道重复注册 | `Register` 返回 `false` + 一次错误日志，**不覆盖** | 两个处理者抢同一个通道会互相偷点击，是配置错误 |
| 注册到 `None` 通道 | 拒绝 | `None` 是"当前无通道"的哨兵 |
| `null` 处理者 | 拒绝 | — |
| `Unregister` 未注册 / 传了别的处理者 / `null` | 返回 `false`，**不改动任何东西，不报错** | `OnDisable` 可能在从未注册时被调用 |
| 注销时机 | **`OnDisable`**，不用 `OnDestroy` | `OnDisable` 在对象销毁与 `SetActive(false)` 时都会调用，`OnDestroy` 会漏后者 |
| 处理者已被销毁仍被派发 | 跳过 + 一次错误日志，**不抛异常** | 忘记注销的后果。Unity 会抛 `MissingReferenceException`，这里换成可读警告 |
| 通道切换 | 不迁移处理者 | 处理者与通道是静态绑定；"这个模式不响应点击"用 `SetActiveChannel(MapClickChannels.None)` |

### `IsAlive` 的坑（实现时踩过）

```csharp
private static bool IsAlive(IMapClickHandler handler)
{
    if (handler == null) return false;          // 接口本身的真 null

    var unityObject = handler as Object;
    if (ReferenceEquals(unityObject, null)) return true;   // 非 Unity 对象 ⇒ 不可能被销毁

    return unityObject != null;                 // 走 Unity 的 == 重载
}
```

两个必须注意的点：

1. **不能要求处理者必须是 `UnityEngine.Object`**。初版写成 `return (handler as Object) != null;`，结果是**纯 C# 处理者永远收不到点击**——一个服务端契约不该有这种隐含要求。harness 立刻抓到了它（12 项断言同时失败）。
2. **不能把两种判空合成一个表达式**。Unity 为它的对象重载了 `==`（被销毁的对象"等于 null"但引用非空），同时提供 `implicit operator bool`。混用会得到一句恒真或依赖重载选择的表达式——我中途写出过 `unityObject == null || unityObject != null`，那是一句恒真式。

## 5. 模式值的归属

**模式值必须有唯一主人，且必须能被读。** 至少要回答这些问题的消费者有四类：分派器（派给谁）、相机栈迁移（UI 相机挂哪个栈）、模式 UI 的显隐、以及将来任何"只在某种模式下生效"的输入。所以不能用"注册在哪个处理者上"来隐式表达模式。

```
MapViewModeSwitcher（应用层 Sample）       ← 模式值的唯一主人
   ├ 切相机：快照 / 栈迁移 / enabled
   ├ 切 UI：两个 Canvas 根节点
   └ 推给分派器：SetActiveCamera(活动相机) + SetActiveChannel(通道)
```

**为什么由模式主人推"活动相机"而不是每次点击去问**：既然切模式时就是它在切相机，它同时知道两者。推送一次让"分派器当前用哪个相机"成为可打印、可断言的状态；每次去问会把相机归属变成隐式依赖。

## 5.1 相机栈：UI 会随主相机一起消失

这是 Camera 模式 UI（`Screen Space - Camera` 的 Canvas + 一个 Overlay 的 UI 相机）**唯一**的真实陷阱：

```csharp
// UniversalRenderPipeline.RenderCameraStack 的第一句
// Overlay cameras will be rendered stacked while rendering base cameras
if (baseCameraAdditionalData.renderType == CameraRenderType.Overlay) return;
```

**Overlay 相机只在"它的 base 相机被渲染时"才会被渲染。** 现在 UI 相机挂在常规模式相机的栈里，所以**一旦为了进俯视而 `gameplayCamera.enabled = false`，整个 UI 会一起消失**——包括你想在俯视模式显示的那些面板。

因此切换时**移动 UI 相机所在的栈**，而不是重建或隐藏它：

```csharp
// 先摘再挂：一个 Overlay 只能属于一个栈，重复添加会被 URP 判定为不合法的栈成员
if (m_GameplayCamera != null && m_GameplayCamera != baseCamera) RemoveUiCameraFrom(m_GameplayCamera);
if (m_TopDownCamera  != null && m_TopDownCamera  != baseCamera) RemoveUiCameraFrom(m_TopDownCamera);
var baseData = baseCamera.GetUniversalAdditionalCameraData();
if (!baseData.cameraStack.Contains(m_UiCamera)) baseData.cameraStack.Add(m_UiCamera);
```

三个要点：

1. **UI 相机全程 `enabled = true`**，只是在两个栈之间搬。想隐藏某块 UI 就切 Canvas 根节点，不要用"移出栈"——移出栈后它是个"无主的 Overlay"，不渲染，而这种状态在 Inspector 里看不出来。
2. **用 `GetUniversalAdditionalCameraData()`**（`UnityEngine.Rendering.Universal` 的 `Camera` 扩展方法），不要自己 `GetComponent`——它还会补挂缺失的组件。
3. `renderType` 才是渲染时的真相来源，`enabled` 只是开关。排查时要看 Camera Type，不能只看勾选框。
4. **`UI 相机.depth (0) > Base 相机.depth`**。栈**内部**顺序由 `cameraStack` 列表决定、不由 depth 决定，但 depth 决定 URP 遍历 Base 相机的顺序。

## 5.2 切换的时序

```
进入俯视：
  快照常规相机(position / rotation / fieldOfView / orthographicSize)
  迁移 UI 相机栈 → 常规相机 enabled = false → 俯视相机 enabled = true
  切两个 Canvas 根节点
  dispatcher.SetActiveCamera(俯视相机) + SetActiveChannel(TopDown)   ← 与相机切换同一步
  可选：FocusOnWorld(焦点) + SetZoomImmediate(焦点档位)

退出俯视：
  迁移 UI 相机栈 → 俯视相机 enabled = false → 常规相机 enabled = true
  还原常规相机快照
  切两个 Canvas 根节点
  dispatcher.SetActiveCamera(常规相机) + SetActiveChannel(Gameplay)
```

**`SetActiveCamera` 与 `enabled` 切换必须在同一帧内完成**。若隔一帧，那一帧的点击会拿着上一模式的相机去拾取（画面已换、拾取还旧），表现为"第一次点击响应错面板"。

**为什么要快照而不是重算**：退出时重算常规相机的位置需要复制玩法相机的跟随逻辑，两份实现必然漂移。快照是"完全复原"的唯一可靠方式。`SetZoomImmediate` 而不是写 `TargetZoom`——后者会让镜头从 1 平滑追到目标档位，而复原应该是无缝的。

`Start()` 里按序列化的 `m_IsTopDown` 应用一次初始状态（只推状态、不触发切换），这样**第一次点击就能找到正确的相机与通道**，即使模式被留在 Inspector 里勾着的状态。

## 6. 依赖方向（Review 时的关键检查点）

```
应用层（Sample）      模式主人、各模式的处理者、UI 面板
        │ 注册 / 推送
        ▼
HexMap.UnityRuntime   MapClickDispatcher、IMapClickHandler、MapClickContext、MapClickChannels
        │ 调用
        ▼
HexMap.UnityRuntime   GvgMapRuntimeController.PickPlotAtScreenPosition
```

- `MapClickDispatcher` **不引用** `UnityEngine.UI`，也不认识任何面板类型；
- 运行时库**不认识"俯视"这个词**；
- 拾取**显式传活动相机**，不用 `Camera.main`（切模式后 `Camera.main` 的归属会变）。

## 7. 应用层：两个处理者与切换器

| 类型 | 作用 |
| --- | --- |
| `SampleMapClickChannels` | 应用层的通道常量：`Gameplay = 1`、`TopDown = 2`。**运行时库里没有这些名字** |
| `GameplayMapClickHandler` | `OnEnable` 注册 `Gameplay`、`OnDisable` 注销；点中则调 `GvgMapRuntimeController.Select`，然后抛 `PlotClicked(int)` 事件 |
| `TopDownMapClickHandler` | 同样注册 `TopDown`；**不碰玩法选中**（预览不是玩），只抛 `PlotClicked(int)` |
| `MapViewModeSwitcher` | 模式值的唯一主人：栈迁移、相机快照与还原、两个 Canvas 根节点的切换、向分派器推相机与通道 |

**处理者为什么只抛事件、不直接操作面板**：面板是 UI 类型，而处理者在 `Sample` 里虽然可以引用 UI，但把"点击 → 事件"和"事件 → 面板"分开之后，两个模式对**同一个 Plot 点击**给出不同 UI 这件事就只是"谁订阅了这个事件"的差别，不需要在两个处理者里各写一遍面板逻辑。面板在 `OnEnable` 订阅、`OnDisable` 退订（与分派器的注册规则同形）。

**`PlotId == -1` 表示点空**，事件仍然抛出——这正是"点空白关闭面板"的入口。

## 8. 测试

`Assets/Tests/EditMode/HexMap/UnityRuntime/MapClickDispatcherTests.cs`（`HexMap.UnityRuntime.Tests.EditMode`）覆盖：

- 活动通道的处理者收到点击；切换通道切换处理者；`None` 静默丢弃；该通道无处理者则丢弃；
- 无 `Controller` / 无活动相机 ⇒ 丢弃且不调用处理者；
- **打在地图外** ⇒ 仍派发、`HasPlot == false`、`PickStatus == OutsideMap`；射线打不到平面另有一条；
- 屏幕坐标原样带进上下文；处理者可以拒绝（返回 `false`）且**拒绝不会注销自己**；
- 重复注册被拒且原处理者保住通道；`None` 通道与 `null` 处理者被拒；
- `Unregister` 幂等、传别的处理者不影响、注销后可重新注册；
- **被销毁的处理者被跳过而不抛异常**；
- `TryGetLastContext` 的三种情况（没点过 / 拾取前被丢弃不留过期结果 / 正常）；
- 地图未 `TryInitialize` ⇒ `PickStatus == MapNotInitialized` 且点击仍派发。

切换器与两个处理者**没有自动化测试**（`HexMap.Sample` 只被 PlayMode 测试程序集引用，EditMode 测不到）。它们的验收靠手动：按地图按钮、点几个格子、再按一次退出，确认 UI 不消失、面板切换正确、退出后相机回到原位。设计讨论与决策全文见 `.scratch/orthographic-map-camera/spec.md`。

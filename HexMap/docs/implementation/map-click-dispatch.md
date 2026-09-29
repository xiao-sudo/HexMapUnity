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

### 被丢弃的点击会报错，且每个问题只报一次

`no-controller` / `no-camera` / `no-handler` 三条路径都调 `Debug.LogError`（**错误级**，不是警告——这三条都意味着接线漏了）。`MapClickChannels.None` 是例外：它**静默丢弃**，因为这个通道的语义就是"这个模式不响应点击"。

去重键让同一个问题**只报一次**：这个函数每次点击都会跑，重复报会把控制台埋掉并拖慢帧率。

**对测试的影响**：Unity Test Framework 会把"未被声明的日志"当作失败。所以任何**故意**走到这几条路径的用例必须在触发日志**之前**声明 `LogAssert.Expect(LogType.Error, new Regex(...))`，且模式要转义（`Regex.Escape`）。测试里为此建了 `NoHandlerMessage` / `DeadHandlerMessage` / `NoCameraMessage` 三个助手，让预期文本跟着源码走，而不是漂成一个"碰巧还能匹配"的子串。

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

**"唯一主人"这条结论没变，变的是它的实现层次**：模式值不再是一个布尔字段，而是"当前是哪个状态对象"。每一个模式都是一个状态对象，自己知道要开哪台相机、哪个 UI 根、推哪个通道；`MapViewModeSwitcher` 退化成驱动它们的那个人，外加两件不属于任何单个模式的机制。

```
MapViewModeSwitcher（应用层 Sample）              ← 驱动器：决定何时换模式、换不成时不换
   ├ m_CurrentState                                ← 模式值的唯一真相（CurrentMode 由它派生）
   ├ 建两个状态（惰性、一次）：相机 / UI 根 / 通道各自注入
   ├ 共享机制①：UI 相机栈迁移与 render type（唯一主人）
   ├ 共享机制②：gameplay 相机快照与还原（唯一主人）
   └ 一次切换 = 退出 → 归零 → 进入

MapGameplayViewState / MapTopDownViewState        ← 模式自己：开自己的相机、自己的 UI 根、推自己的通道
   └ 互不认识；加第三个模式 = 加一个状态类，驱动器与已有状态一行不改
```

**为什么由模式主人推"活动相机"而不是每次点击去问**：既然切模式时就是它在切相机，它同时知道两者。推送一次让"分派器当前用哪个相机"成为可打印、可断言的状态；每次去问会把相机归属变成隐式依赖。

**为什么"归零"由驱动器做，而不是每个状态在进入时关掉别人的东西**：关掉别的模式的东西，等于每个状态都要知道别的模式存在。驱动器有一张**扁平清单**（每个状态声明自己可能打开的组件），归零就是逐个关掉——它不知道有几个模式、也不知道它们叫什么。代价是 `退出 → 归零 → 进入` 中间有一瞬"没有任何模式在呈现"，而这一瞬在同一个同步方法里、不落到帧上。

## 5.1 相机栈：UI 会随主相机一起消失

这是 Camera 模式 UI（`Screen Space - Camera` 的 Canvas + 一个 Overlay 的 UI 相机）**唯一**的真实陷阱：

```csharp
// UniversalRenderPipeline.RenderCameraStack 的第一句
// Overlay cameras will be rendered stacked while rendering base cameras
if (baseCameraAdditionalData.renderType == CameraRenderType.Overlay) return;
```

**Overlay 相机只在"它的 base 相机被渲染时"才会被渲染。** 现在 UI 相机挂在常规模式相机的栈里，所以**一旦为了进俯视而 `gameplayCamera.enabled = false`，整个 UI 会一起消失**——包括你想在俯视模式显示的那些面板。

因此切换时**移动 UI 相机所在的栈**，而不是重建或隐藏它：两个状态在进入时各调一次 `context.EnterOwnUiStack(自己的相机)`，实现只有一份（`MapViewModeSwitcher.MoveUiCameraIntoStack`）。

```csharp
// 先摘再挂：一个 Overlay 只能属于一个栈，重复添加会被 URP 判定为不合法的栈成员
if (m_GameplayCamera != null && m_GameplayCamera != baseCamera) RemoveUiCameraFrom(m_GameplayCamera);
if (m_TopDownCamera  != null && m_TopDownCamera  != baseCamera) RemoveUiCameraFrom(m_TopDownCamera);
// 挂进栈之前必须是 Overlay，否则 URP 跳过它：见下面第 3 点
m_UiCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
var baseData = baseCamera.GetUniversalAdditionalCameraData();
if (!baseData.cameraStack.Contains(m_UiCamera)) baseData.cameraStack.Add(m_UiCamera);
```

四个要点：

1. **UI 相机全程 `enabled = true`**，只是在两个栈之间搬。想隐藏某块 UI 就切 Canvas 根节点，不要用"移出栈"——移出栈后它是个"无主的 Overlay"，不渲染，而这种状态在 Inspector 里看不出来。
2. **用 `GetUniversalAdditionalCameraData()`**（`UnityEngine.Rendering.Universal` 的 `Camera` 扩展方法），不要自己 `GetComponent`——它还会补挂缺失的组件。
3. `renderType` 才是渲染时的真相来源，`enabled` 只是开关。排查时要看 Camera Type，不能只看勾选框。**而且新挂上的 `UniversalAdditionalCameraData` 默认是 `Base`**，所以"加进 `cameraStack` 列表"本身不够：URP 遍历栈时对 `renderType != CameraRenderType.Overlay` 的成员只留一行告警然后 `continue`（`UniversalRenderPipeline.cs`），表现为"栈里明明有它、UI 却完全不渲染"。因此 `MoveUiCameraIntoStack` 在挂之前顺手把它置为 `Overlay`——栈归属与 render type 归同一个 owner 管。
4. **`UI 相机.depth (0) > Base 相机.depth`**。栈**内部**顺序由 `cameraStack` 列表决定、不由 depth 决定，但 depth 决定 URP 遍历 Base 相机的顺序。

**同一个相机被同时接成 UI 相机与 base 相机**时，`MoveUiCameraIntoStack` 会 `Debug.LogError` 并跳过栈操作：它既当 base 又当自己的 overlay 会让自己的 `cameraStack` 失效，而 `NullReferenceException` 不是排查的起点。这条只报一次（`m_HasReportedSharedCameraWiring`），因为错误在场景接线里、不在每次切换上。

## 5.2 切换的时序

```
冷启动（Start → ApplySerializedMode）：
  m_CurrentState == null ⇒ 没有前一个模式可退（不 Exit）
  但**照常归零**：先推 (null, None)，再关掉每个状态声明的相机与 UI 根
  然后进入 m_StartMode —— 它只负责打开自己的东西

按下地图按钮：
  Toggle → SetMode(另一个模式)
    当前状态存在且它的模式 == target ⇒ 到此为止（连按两次不是一次模式切换）
                                      注意比的是"当前状态"，不是 CurrentMode：冷启动时
                                      CurrentMode 会回落成起始模式，拿它比会把"起始模式就是
                                      要进入的模式"误判成重复请求，于是整个进入被跳过
    当前状态.Exit(context)          ← 冷启动时没有当前状态，跳过
                                      gameplay 退出时在这里记下自己相机的位姿（见下）
    ResetPresentation()   → 分派器推 (null, None)；逐个关掉每个状态声明的相机与 UI 根
    m_CurrentState = 目标状态
    目标状态.Enter(context)：
      [gameplay] 若记过位姿 ⇒ 先把自己的相机还原回去
      context.Enter(自己的相机, 自己的通道)      ← 相机 enabled 与通道推送在同一句里
      context.ShowOwnView(自己的 UI 根)
      context.EnterOwnUiStack(自己的相机)
```

**归零是"恰好一个模式在呈现"这条性质的唯一来源，所以冷启动也要跑**：进入模式只打开自己的东西，关掉别人的只有归零。少了它，"开局即俯视"的场景会让两台相机同时开着（gameplay 相机不会被关）。旧实现从不依赖场景里相机的作者状态，进入任一模式都会把两台相机设成确定值，这里保持一致。

**gameplay 相机的位姿由 gameplay 状态自己记、自己还**（`Exit` 记、`Enter` 还），俯视状态与驱动器都不知道这件事。三条约束因此同时成立：

- **开局即俯视**：`Gameplay.Exit` 从未执行 ⇒ 没有记忆 ⇒ 首次进入 gameplay 时**不还原**，相机停在场景给它的位置；
- **真切换**：`Exit` 发生在相机仍是玩家那台的最后一刻，所以记下的就是"玩家离开时的视图"；
- **俯视期间相机被移动过**：`Exit` **每次离开都覆盖**记忆，所以下一次回来是"最近一次离开 gameplay 时的位置"，与旧实现（每次 `EnterTopDown` 都重新快照）一致。

**不要改成"只记一次"**：那会把"俯视里被移动过的相机"永久当成应还的位姿。而"开局即俯视不该记"这件事，本来就由"`Exit` 没被调用过"表达，不需要额外条件 —— 更不需要驱动器把"上一个模式是谁"传给状态。

**`SetActiveCamera` 与 `enabled` 切换必须在同一帧内完成**。若隔一帧，那一帧的点击会拿着上一模式的相机去拾取（画面已换、拾取还旧），表现为"第一次点击响应错面板"。现在这条不是靠纪律维持的：`SetMode` 是一个同步方法，退出／归零／进入之间没有协程、没有第二个入口，`context.Enter(camera, channel)` 也把"相机与通道同时换"写进了签名。

**为什么要记住而不是重算**：退出时重算常规相机的位置需要复制玩法相机的跟随逻辑，两份实现必然漂移。记住并原样写回是"完全复原"的唯一可靠方式。（复原写的是常规相机自己的 `position` / `rotation` / `fieldOfView` / `orthographicSize`，与 `OrthographicMapCamera` 无关。）**每次离开 gameplay 都覆盖这份记忆**，所以它代表的始终是"最近一次离开 gameplay 时的视图"。

`Start()` 里按序列化的 `m_StartMode` 应用一次初始模式（**不退出、但照常归零**），这样**第一次点击就能找到正确的相机与通道**，即使场景被留在"开局即俯视"的状态。这段逻辑在 `ApplySerializedMode()` 里，`Start()` 只是调它：EditMode 测试没有 start 回调，留一个可调用的入口才能验证它。

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
| `SampleMapClickChannels` | 应用层的通道常量：`Gameplay = 1`、`TopDown = 2`，取值由 `MapViewMode` 派生（`For(mode)`）。**运行时库里没有这些名字**。通道与模式仍是两套词汇：合并等于宣布"以后每加一个模式就必须加一个通道" |
| `MapViewMode` | 模式的词汇表：`Gameplay = 0`、`TopDown = 1`。序列化的起始模式按名字写进场景 |
| `IMapViewModeState` | 一个模式的全部行为：`Mode` / `Enter` / `Exit` / `Presentations`。状态之间互不认识 |
| `MapGameplayViewState` / `MapTopDownViewState` | 两个模式各自开自己的相机与 UI 根、推自己的通道；gameplay 模式还**自己记、自己还**自己相机的位姿（离开时记、进入时还），俯视模式对此一无所知 |
| `MapViewPresentation` | 一个可被"归零"的东西（相机或 UI 根）。驱动器只对它做一件事：关掉 |
| `MapViewModeContext` | 状态改世界的唯一通道：进模式（相机+通道）、搬自己的 UI 栈、显隐自己的 UI 根、捕获/还原 gameplay 相机。它自己不持有状态 |
| `MapClickTapInput` | **唯一认识指针的组件**：判断一次按下是"点在图上"还是"拖拽/点在 UI 上"，是则调 `MapClickDispatcher.OnMapClicked(屏幕坐标)`。换 Input System 或 EasyTouch 只需替换它 |
| `GameplayMapClickHandler` | `OnEnable` 注册 `Gameplay`、`OnDisable` 注销；点中则调 `GvgMapRuntimeController.Select`，然后抛 `PlotClicked(int)` 事件 |
| `TopDownMapClickHandler` | 同样注册 `TopDown`；**不碰玩法选中**（预览不是玩），只抛 `PlotClicked(int)` |
| `IMapPlotClickSource` | 两个处理者共同实现的事件契约，让一个面板类型能服务两个模式而不用写两份 |
| `MapClickPanel` | 最小面板：`OnEnable` 订阅自己模式的 `PlotClicked`、`OnDisable` 退订；`-1` 关闭，否则显示 PlotId |
| `MapViewModeSwitcher` | **驱动器，不再逐项切东西**：持有唯一真相 `m_CurrentState`（`CurrentMode` 由它派生）、懒建两个状态、一次切换走"退出 → 归零 → 进入"；另外持有两件不属于任何单个模式的机制——UI 相机栈迁移与 render type、gameplay 相机快照与还原。公开面只有 `Configure` / `ApplySerializedMode` / `Toggle` / `CurrentMode`（+ 只读的 `Wiring`，供编辑器接线工具读回） |

**处理者为什么只抛事件、不直接操作面板**：面板是 UI 类型，而处理者在 `Sample` 里虽然可以引用 UI，但把"点击 → 事件"和"事件 → 面板"分开之后，两个模式对**同一个 Plot 点击**给出不同 UI 这件事就只是"谁订阅了这个事件"的差别，不需要在两个处理者里各写一遍面板逻辑。面板在 `OnEnable` 订阅、`OnDisable` 退订（与分派器的注册规则同形）。

**`PlotId == -1` 表示点空**，事件仍然抛出——这正是"点空白关闭面板"的入口。

### 7.1 场景接线清单

跑通"按地图按钮 → 俯视 → 点格子出面板 → 再按一次退出"这条链路，场景里需要：

1. **两个 base 相机**：玩法相机（透视，保留 `MainCamera` tag 与 `AudioListener`）+ 俯视相机（正交，不带 tag）。
2. **UI 相机**：URP `Camera Type = Overlay`、culling mask 只留 UI 层、`depth` 大于两个 base 相机。`Screen Space - Camera` 的 Canvas 必须把 `worldCamera` 指到它；它现在这份栈由 `MapViewModeSwitcher` 迁移（render type 也由它设）。
3. **正交 rig 必须驱动俯视相机**：`OrthographicMapCamera.m_Camera` 要指向俯视相机。**这是最容易错的一处**——若它指向玩法相机，聚焦/缩放/拖拽全作用在一个被禁用的相机上，表现为"切过去了但地图不动、缩放没反应"。
4. **`MapViewModeSwitcher`**：`m_GameplayCamera` / `m_TopDownCamera` / `m_UiCamera` / `m_Dispatcher` / 两个 UI root / `m_StartMode`。代码侧用一次 `Configure(new MapViewModeWiring(...))` 写；场景侧就是这几个序列化字段。**不再有 `m_MapCamera` / `m_FocusTarget` / `m_FocusZoom` / `m_MapDragInput` / `m_MapZoomInput`**——焦点与手势都不是"模式"的事（见 §7.2）。
5. **两个 UI root 互不包含**，且每个模式自己的按钮放在**自己那个 root** 里：进某个模式时它自己的 root 被打开、另一个被关掉，所以一个按钮负责"进去"、另一个负责"出来"。**不要按早先版本的写法把它们挪进某个常驻 root**：那样会把"两个按钮各管一个方向"改成"一个按钮要判当前模式"。（本条此前写反了，2026-02 修正。）
6. **`MapClickTapInput`** 挂到场景里并接上 `m_Dispatcher`（这是点击链路的入口）。
7. **两个 `MapClickPanel`**：各自接自己模式的处理者（`m_Source`），并放在各自的 UI root 下。
8. **plot 数据要先初始化**（例如 `Facade`），否则每次点击都是 `MapNotInitialized`/`NoSelectablePlot`，面板永远打不开。
9. `EventSystem` 必须存在（`Button` 与 `IsPointerOverGameObject` 都依赖它）。

### 7.2 焦点与手势：不在这个组件里

- **焦点**（旧 `m_FocusTarget` / `m_FocusZoom` / `FocusIfRequested`）已从切换器**删除**，没有替代组件。要在"打开大图时对准某处"就由**打开地图的调用方**在 `Toggle()` **之前**做**一次** `OrthographicMapCamera.TryZoomToCell(格, 档位)`（或 `TryZoomToPoint`）。**不要拆成"先对准、再设档位"**：对准会按**旧档位**的范围夹取，边缘目标会被永久夹偏（`.scratch/orthographic-map-camera/issues/05`）。原来的两条焦点的测试也随之删除，因为那条路径不存在了。
- **平移与缩放手势**（旧 `m_MapDragInput` / `m_MapZoomInput` / `SetMapInputEnabled`）也已删除。手势组件自己判断相机是否可用（`IsCameraLive()`），相机没开就不产生任何效果。这是**必须**的：相机 `enabled = false` 并不会阻止 `OrthographicMapCamera` 接受一次 pan 或 zoom（pan 甚至不需要 framing），所以"相机灭着还能偷偷改视角"要靠输入侧自己挡住。切换器从此不知道手势存在。

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

`Assets/Tests/EditMode/HexMap/Sample/MapViewModeSwitcherTests.cs`（`HexMap.Sample.Tests.EditMode`）覆盖切换器。它可以驱动的公开面只有四个成员——`Configure` / `ApplySerializedMode` / `Toggle` / `CurrentMode`——所以每条测试按的按钮和场景里那个 UnityEvent 按的是同一个方法：

- 按一次地图按钮 ⇒ 整个地图视图起来：相机 `enabled`、分派器的相机与通道、两个 UI 根节点，一次性全换；
- 再按一次 ⇒ 全部换回 gameplay（相机、通道、两个根）；
- **UI 相机既是 `Overlay`、又只在"正在渲染的那个栈"里**（只断言"在列表里"是不够的，见 5.1 第 3 点），两个方向都查；
- UI 相机全程不被移动、不被禁用、不被改父节点；
- 回到 gameplay 时常规相机的 position / rotation / fieldOfView / orthographicSize **完全还原**，包括俯视期间被别处移动过的情况；
- 重复按地图按钮**不是一次模式切换**：不重新快照（否则会把"被移动过的相机"当成原状态存下来）、不重推通道、不动呈现；
- `Toggle` 两个方向都正确；**连按 5 个来回无漂移**；
- `m_StartMode` 被序列化成 `TopDown` 的场景（`ApplySerializedMode`）能正确进入俯视；且这种"开局就在俯视"的场景退出时**没有快照可还原**，相机保持原位（gameplay 从未退出过，所以从未捕获）；
- **按钮是第一个动作时也能建立状态**：起始模式决定第一次按往哪走（这条覆盖 `Toggle` 的冷启动分支，它与 `ApplySerializedMode` 是两条不同的入口）；
- 模式仍然决定地图相机何时活着（gameplay 关掉它、俯视打开它）；**手势本身是否生效由手势组件自己判断**，不在切换器的断言范围里；
- 全字段为 null 时 `Toggle` / `ApplySerializedMode` 不抛异常；UI 相机与 base 相机接成同一个时 `LogError` 一次且不去动栈。

**已删除的断言（连同它们测的 API）**：焦点与焦点失败、平移/缩放手势的 `IsEnabled`、`m_MapCamera` 的存在、`EnterTopDown` / `ExitTopDown` / `IsTopDown`。它们测的是被这次重构删掉的路径，留着只会让"什么还成立"变得含糊。

`MapClickTapInput` 与 `MapClickPanel` **没有自动化测试**：前者依赖 `Input` 与 `EventSystem.current`，后者依赖 UI 对象与 `TMP_Text`，两者都要玩家真的按一下、点一下才有意义。它们的验收靠手动：按地图按钮、点几个格子、再按一次退出，确认 UI 不消失、面板切换正确、点空关闭、退出后相机回到原位。设计讨论与决策全文见 `.scratch/orthographic-map-camera/spec.md`；本次状态机重构的决策全文见 `.scratch/map-view-mode-state-machine/spec.md`。

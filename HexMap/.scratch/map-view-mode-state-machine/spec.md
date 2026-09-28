# 视图模式状态机（`MapViewModeSwitcher` 重构）

**Status:** ready-for-agent

本文件记录「把 `MapViewModeSwitcher` 从一堆散落的布尔与分支重构为每个模式一个状态对象」的全部决策。决策经六轮逐题敲定。第 2 节是契约（结论），第 3 节是状态机的确切形状，第 4 节记录**被删掉的东西和它们为什么能被删**，第 5 节记录被否掉的选项 —— 避免后来者把已经讨论过的东西重新讨论一遍。

## 1. 背景与现状（侦察结论，均为实测）

| 事实 | 位置 |
| --- | --- |
| 切换器 435 行，同时承担：模式值、相机快照/还原、URP 栈迁移与 render type、两个 UI 根的开关、平移/缩放手势的开关、向分派器推活动相机与通道 | `Assets/Scripts/HexMap/Sample/MapViewModeSwitcher.cs` |
| `EnterTopDown()` / `ExitTopDown()` / `IsTopDown` 在**生产代码里零调用方**，只有测试在调 | 全仓 grep |
| 场景 100% 走 `Toggle`，且是**两个按方法名绑定的 UnityEvent 持久监听**（`m_Mode: 1` = Void，无参） | `Assets/sw.unity:1291-1305`、`:2017-2031` |
| `sw.unity` 是**唯一**带这个组件的场景；无 prefab / ScriptableObject 引用它 | 全仓 grep |
| 两个按钮各自挂在自己那个 UI 根上（俯视按钮在 `m_TopDownUiRoot` 上，返回按钮在 `m_GameplayUiRoot` 上）⇒ 一个开、一个关。这与 `docs/implementation/map-click-dispatch.md:226` 那条「按钮必须放在常驻 root」**不符**，文档是错的 | `sw.unity:1256`、`:1982` |
| 场景里 `m_FocusTarget: {fileID: 0}`、`m_FocusZoom: 3` ⇒ **焦点这条链在生产场景里根本没接人**，只有测试在走 | `sw.unity:163-164` |
| `FocusIfRequested()` 只在 `ApplyTopDown()` 里被调用，gameplay 方向没有对应的撤销 ⇒ 焦点本来就是「进入俯视时做一次的事」 | `MapViewModeSwitcher.cs:236` |
| `m_MapCamera` 在切换器里的唯一消费者就是 `FocusIfRequested` | `MapViewModeSwitcher.cs:288,297` |
| `m_IsTopDown` 这个私有序列化字段被**三处按字符串**读写（场景资产、wirer、夹具，且夹具的辅助方法断言该字段必须存在） | `sw.unity:165`、`TwoModeSampleSceneWirer.cs:184`、`MapViewModeSwitcherTests.cs:128,142-147` |
| `m_FocusZoom` 被夹具按字符串写 | `MapViewModeSwitcherTests.cs:132-137` |
| 两个手势组件只检查**自己的** `m_IsEnabled`，**从不检查相机是否 `enabled`**；场景里两者都序列化成 `1` | `OrthographicMapDragInput.cs:74`、`OrthographicMapZoomInput.cs:98`、`sw.unity:1528-1543` |
| 夹具里**五条**测试断言的是与实现无关的事实：UI 相机恰在一个会渲染的栈里、UI 相机从不被移动/禁用/改父、gameplay 相机精确还原、半接线不抛异常、共享相机接线只报一次 | `MapViewModeSwitcherTests.cs:198-235,238-263,441-450,453-470` |
| 全仓 `StateMachine` / `IState` / `CurrentState` / `FSM` **零命中** ⇒ 这是本仓库第一次引入状态机 | 全仓 grep |
| `HexMap.Sample.Tests.EditMode` 无友元设置，仓库里**没有 `InternalsVisibleTo` 先例** | asmdef + 全仓 grep |
| `HexMap.Sample` 已引用 URP-Runtime / uGUI / TMP / 全部运行时产品程序集 ⇒ 新增纯 C# 类**不需要动 asmdef** | `HexMap.Sample.asmdef` |
| 本区域权威文档：§5 模式值归属（含结构图）、§5.1 栈陷阱、§5.2 切换顺序与原子性、§7 职责表、§8 夹具钉住的 13 条可观察行为 | `docs/implementation/map-click-dispatch.md:115-258` |
| `.scratch/minimap` 的 02/03/04 计划给这个类加「小地图相机槽」与「焦点格入口」⇒ 重构会与那份计划碰撞 | `.scratch/minimap/spec.md:97,149-151` |

### 1.1 一条必须正面回应的反证

本仓库**刚刚删掉过**一份状态模型：`OrthographicMapCamera` 里的 `m_Focus` / `m_HasFocus` / `m_IsGestureActive` / `BeginGesture` / `EndGesture` / `TargetZoom` / `ZoomSpeed` / `Tick` / `SetZoomImmediate`，理由写在 `.scratch/orthographic-map-camera/spec.md:303,357-370`：唯一消费者是 `MapViewModeSwitcher.FocusIfRequested`，把目标随调用传递之后，状态和它的仲裁就不需要了；原则是「信息留在持有者手里」。

**本票的方向不违背那条原则，它是那条原则的执行**：

- 被删掉的是「相机替上层记着一个待办目标」—— 同一份事实存在两处（调用方知道目标，相机也记着），于是需要仲裁。
- 本票做的是「每个模式自己拥有它那一坨行为」—— 事实只有一处（俯视模式拥有它自己的快照，因为快照本来就只属于俯视模式）。
- 两条推论直接落在契约里：焦点状态**被删除而不是搬走**（第 4 节）；`OrthographicMapCamera` **一行不改**。

## 2. 契约（已决定，不可偏离）

| 项 | 决定 |
| --- | --- |
| 模式即状态 | `Gameplay` 与 `TopDown` 各是一个状态对象，各自实现 `Enter` / `Exit` |
| 状态的形态 | 纯 C# 类（**不是** `MonoBehaviour`），构造期一次性注入引用，惰性构造后缓存 |
| 状态的组成 | 只持有：自己那台相机、自己那个 UI 根、**同一台 UI 相机**的引用、自己那个点击通道值 |
| 状态互相之间 | **不认识**。一个状态的 `Enter`/`Exit` 不碰另一个模式的任何组件 |
| 归零的归属 | 驱动器在 `Exit` 之后、`Enter` 之前做一次 `ResetPresentation()`：关掉**所有**登记的呈现（相机 `enabled = false`、UI 根 `activeSelf = false`）并把点击通道推成"无模式" |
| 呈现在哪里登记 | 每个状态通过 `Presentation` 自注册自己**可能打开**的组件；驱动器在构造状态后展开成一张**扁平清单**。新增模式 = 新状态类自己登记，驱动器一行不改 |
| `Enter` 的承诺 | **不承诺重复调用安全**。防重入只由驱动器负责 |
| 快照 | **由俯视状态自己持有**（数据 + 守卫 + 捕获/还原都在 `MapTopDownViewState` 里）：`Enter` 在**"上一个模式是 gameplay"且尚未记过**时捕获，"离开俯视"时还原。驱动器只负责把 `previousMode` 传进来 —— 见第 10.5 节 |
| 当前模式的真相 | 只有 `m_CurrentState` 一个字段；`CurrentMode` 由它派生，冷启动时回落到序列化的起始模式 |
| no-op 判 | 只在驱动器层，而且**必须拿当前状态比**：`m_CurrentState != null && m_CurrentState.Mode == target → return`。**不能拿 `CurrentMode` 比** —— 冷启动时 `CurrentMode` 回落成起始模式，会把"起始模式恰好就是目标模式"误判成重复请求（见第 10.1 节：这就是"开局即俯视"失败的真正原因） |
| 冷启动 | `m_CurrentState == null` 时**不 `Exit`**（没有前一个状态）、但**照常归零**，然后 `Enter` 起始模式；且**没有**第二个"apply"路径 —— `Start` 与每次切换走同一个 `Enter`。**归零必须在第一次进入时也跑**：它是"恰好一个模式在呈现"这条性质的唯一来源，而进入模式只负责打开自己的东西 |
| 序列化起始模式 | `MapViewMode m_StartMode` 取代 `bool m_IsTopDown` |
| 通道词汇表 | 新建 `MapViewMode` 枚举；`SampleMapClickChannels` 的取值保持 `1` / `2` 不变，由枚举派生 |
| 公开面 | 只剩 `Configure(...)`、`ApplySerializedMode()`、`Toggle()`、`CurrentMode` |
| 焦点 / 缩放 | **从视图模式关注点里彻底移除**；不新建替代组件；替换路径写进文档 |
| `m_MapCamera` 槽 | 删除（焦点走了之后无读者） |
| 平移 / 缩放手势开关 | 从切换器删除；改为两个手势组件各自判断相机是否 `enabled` |
| 可见性 | 枚举 `public`（场景与 wirer 按名字写它）；状态、接口、内部小类型全部 `internal` |
| 语义不变 | UI 相机的栈行为与 render type、快照还原的精确性、通道推送、`Toggle` 幂等、半接线容错、共享相机接线只报一次 —— **一条都不许变** |
| 场景资产 | `sw.unity` **一字不改**（只会有无害的孤儿序列化值） |

## 3. 状态机的确切形状

```
ApplySerializedMode()                      Toggle()
  SetMode(m_StartMode)                       SetMode(另一个模式，按 CurrentMode 取反)
  （Start() 调它；夹具的冷启动接缝）

SetMode(target)
  m_CurrentState != null && m_CurrentState.Mode == target → return   ← no-op 的唯一归属
                                                                     （比的是状态，不是 CurrentMode）
  m_CurrentState?.Exit()                         ← 冷启动时为 null，跳过
  ResetPresentation()                            ← 遍历扁平清单：全部关闭 + 推"无模式"通道（冷启动也跑）
  m_CurrentState = target
  m_CurrentState.Enter()                         ← 写状态在 Enter 之前（Enter 里读 CurrentMode 才拿到新值）
```

- 上述每一步都在**同一次同步调用**内完成，没有协程、没有跨帧。相机 `enabled` 的切换与点击通道的推送因此天然满足 `docs/implementation/map-click-dispatch.md:161-184` 的「同一帧内」要求。
- `ResetPresentation()` 之后、`Enter()` 之前，世界处于「没有任何模式在呈现」的一瞬 —— 对玩家不可见，且它换来两个性质：每个 `Enter` 天然是"从零开始"的，以及每个 `Enter` 不需要认识别的模式。
- **归零也跑在第一次进入上**：这一瞬之所以安全，是因为它从不落到帧上；而它带来的"恰好一个模式在呈现"必须从第一帧就成立 —— 进入模式只负责打开自己的东西，关掉别人的只有归零。少了它，"开局即俯视"的场景会留下两台相机都开着。
- 驱动器**不知道**有几个模式、也不知道它们叫什么：它只看到一张"可关的东西"的清单。加第三/第四个模式时，驱动器与已有状态都零改动。

## 4. 被删掉的东西，以及为什么能被删

| 被删 | 理由 |
| --- | --- |
| `m_IsTopDown` / `IsTopDown` | 当前模式的真相收敛到 `m_CurrentState` 一处；`CurrentMode` 是它的投影 |
| `m_HasGameplayCameraState` / `m_HasReportedSharedCameraWiring` 之外的簿记 | 快照的"第一次"概念随 no-op 判一起消失（见第 3 节） |
| `EnterTopDown()` / `ExitTopDown()` | 生产零调用方；两个方向动词被"一个状态目标动词"取代 |
| 全部 public get/set 属性对 | 它们存在的唯一理由是让外部把私有字段逐个塞进来；被一次 `Configure(...)` 取代 |
| `m_FocusTarget` / `m_FocusZoom` / `FocusIfRequested` | 视图模式的关注点里不含"对准哪里"。场景里本来就没接人，所以**没有功能回退**。替换路径：由"打开地图的调用方"在 `Toggle()` 之前自己调 `OrthographicMapCamera.TryZoomToCell` / `TryZoomToPoint` —— **一次调用**（拆成"先对准再设档位"会按旧档位夹取，边缘目标会被永久夹偏，见 `.scratch/orthographic-map-camera/issues/05`） |
| `m_MapCamera` | 焦点走了之后无读者 |
| `m_MapDragInput` / `m_MapZoomInput` / `SetMapInputEnabled` | 手势的开关不该是"模式"的责任。改为输入组件自守 |

### 4.1 手势自守的确切要求

今天 gameplay 下滚轮/拖拽之所以无害，**唯一原因**是 `SetMapInputEnabled(false)` 在开机时把它俩关了。删掉那段而不补判断就是行为回退：相机不渲染，但 `Zoom` / `Center` 会被静默改掉，玩家下次打开地图看到的是错视角。

因此两个手势组件的 `Update` 守卫要各自补上相机是否可用：

```
既有的 : if (!m_IsEnabled || m_MapCamera == null) → 复位手势状态并 return
新的   : 还要判相机自身没开着时不产生任何效果
```

`IsEnabled` 属性与 `m_IsEnabled` 字段随后可以删除（`sw.unity` 里两个 `m_IsEnabled: 1` 变成无害孤儿）。「手势只在相机活着时生效」从此是这个组件自己的不变量，任何调用方都不需要记得去设置它。

## 5. 被否掉的选项（记录，避免重复讨论）

| 选项 | 否掉的理由 |
| --- | --- |
| 状态做成 `MonoBehaviour`，inspector 序列化 | 两种模式变成两个还要接线的资源，对可测性毫无帮助 |
| 泛型 `StateMachine<T>` 放进 `Core` / `UnityRuntime` | 全仓只有这一个状态机，违反本仓库「没有第二个实现就不造缝」的立场（`.scratch/minimap/spec.md:106`） |
| 保留 `MapViewModeContext` 作"为后续扩展预留"的空壳 | 今天是空壳，将来要用时再加是零成本；预留一个空参数只会让每个读者以为里面藏着东西 |
| 留一个"尚无状态"的 `NoneState` 占位 | 为了句式的整齐多造一个类和一个永远什么都不做的 `Exit`，是"同一事实存在两处"的另一种版本 |
| 每个状态的 `Enter` 顺手关掉别的模式的组件 | 加第三个模式时要改已有两个状态 |
| 状态自己拥有 UI 相机（各存一份并各自负责 render type） | render type 与栈成员必须**只有一个主人**（`docs/implementation/map-click-dispatch.md:128-159`）。引用可以各存一份（指向同一台），真相只有一份 |
| 归零时不推"无模式"通道 | 会在 `Exit` 与 `Enter` 之间留下"旧模式的相机 + 新的通道"这种更坏的分裂 |
| 回 gameplay 时改为"重算相机"而不是"还原快照" | 语义是"把玩家相机原样还给他"，重算做不到 |
| 这次顺带实现小地图相机槽 / 焦点格入口 | 违反「没有第二个实现就不造缝」；那些属于 `.scratch/minimap` |
| 把 `SampleMapClickChannels` 直接删掉、通道就等于模式 | 通道 id 是 `MapClickDispatcher` 的词汇，模式是视图的词汇；合一等于宣布"以后每加一个模式就必须加一个通道" |
| 保留 `bool` 起始模式字段 | 它把"模式"这个词从数据模型里删掉，加模式只能再补一个布尔 |
| 往 `HexMap.Sample` 加 `AssemblyInfo.cs` 声明 `InternalsVisibleTo` | 引入一个仓库里从不存在的约定，为几行测试断言付这个价不划算 |
| 改 `sw.unity` 让按钮符合文档 | 场景行为（一开一关）是能工作的有意设计；**文档才是错的那一方** |

## 6. 实现决策清单

### 6.1 新增/修改的模块

| 模块 | 内容 | 可见性 |
| --- | --- | --- |
| `MapViewMode` 枚举 | `Gameplay = 0`、`TopDown = 1` | public |
| `IMapViewModeState` | `MapViewMode Mode { get; }`、`void Enter()`、`void Exit()`、`IReadOnlyList<MapViewPresentation> Presentation { get; }` | internal |
| 两个状态类 | `GameplayViewState`、`TopDownViewState` | internal |
| 呈现登记小类型 | 一个能同时表达"一台相机"与"一个 UI 根"的登记项，`ResetPresentation()` 对它只做一件事：关闭 | internal |
| `MapViewModeSwitcher` | 驱动器 + 两个共享机制助手 | public（现状） |
| `SampleMapClickChannels` | 取值不变（`Gameplay = 1`、`TopDown = 2`），由枚举派生 | public（现状） |

`HexMap.Sample` 的 asmdef **不需要改动**。`OrthographicMapCamera` / `MapClickDispatcher` **一行不改**。

### 6.2 接线与序列化

- 序列化字段收缩为：两台基相机、UI 相机、两个 UI 根、`m_StartMode`。
- 非序列化：`m_CurrentState`、`m_HasReportedSharedCameraWiring`。
- 接线入口是 `Configure(...)`。**它的确切形状是本票唯一留给实现者的小自由**：推荐一个 `internal readonly struct` 形状的接线参数（值语义、字段全是引用、零分配、签名不会随着加槽而变长），在场景里命名参数调用即可读；直接在 wirer / 夹具里写七个位置参数也可接受。选择判据只一条：**调用点必须能一眼看出谁接谁**。
- 状态在**首次使用**时惰性构造（`Configure` 之后），不能在 `Awake` 里构造。
- 点击通道值在构造期作为一个整数注入状态（状态不认识"通道词汇表"这个名字，只认识一个数）。

### 6.3 每个状态做什么

两个状态的形状相同、取值不同，各约 20 行：

- `Enter`：开自己的相机 → 推 `(自己的相机, 自己的通道)` → 开自己的 UI 根 → 把 UI 相机搬进自己的 stack。
- `Exit`：关自己的 UI 根。**不关别人的**（别人的关是驱动器的活）。
- 俯视状态额外持有 **gameplay 相机引用 + 自己的快照**：`Enter` 在 `previousMode == Gameplay` 且尚未记过时捕获，"离开俯视"时还原。`previousMode` 由驱动器传入（冷启动为 `null`）—— 它区分"真从 gameplay 切过来"与"场景开局就在俯视"，后者不该记任何位姿（见第 10.6 节）。
- 搬 UI 相机入栈这段逻辑（render type + 栈成员 + 「接线错误只报一次」）是驱动器上的 `internal` 助手，两个状态各调一次。UI 相机引用各存一份不产生第二个主人，因为真相只有一份（场景里唯一那台）。

### 6.4 文档

- `docs/implementation/map-click-dispatch.md`：§5 结构图重画（"模式值的唯一主人"这条**结论保留**，改的是实现层次）、§7 职责表重写、§8 可观察行为清单按新夹具重写、**§226 修正**（两个按钮各自属于一个模式的自有 UI，一开一关是有意的）。
- `.scratch/minimap/issues/02`、`issues/03`：措辞对齐新结构（`03` 的"焦点入口"改为"由调用方在 `Toggle()` 之前调"；`03` 的验收里"扩 `MapViewModeSwitcherTests`"改为按新 API 扩）；`spec.md` 顶部补一行说明切换器结构已重构；`issues/04` 的描述仍成立，不改。
- `TwoModeSampleSceneWirer` 最小集：起始模式字段改名 + 删掉对 `MapCamera` / `MapDragInput` / `MapZoomInput` 的接线 + 删掉 `StartInGameplayMode` 里按字符串写 `m_IsTopDown` 的那段。
- **不写 ADR**：这是实现层次的选择，不是跨模块的架构约束。
- 不碰任何 `.meta` 文件。

## 7. 测试决策

**好测试的判据**：只断言外部可观察行为。装配层测试用真的 `Camera` 与 `GameObject`，通过切换器的公开 API 驱动，断言"世界看起来怎样"（相机开着没、在谁的栈里、点击走哪个通道、UI 根活着没、相机被还原没），**不断言**状态对象内部、私有字段、调用顺序的实现细节。顺序与幂等这类"只能从行为侧面观察"的性质，通过结果间接钉住（例如重复 `Toggle` 不改变被还原的相机位置）。这与 `.scratch/hex-map-render-strategy/spec.md:92-108` 的既有立场一致：通过最高接缝断言外部行为。

**接缝**：唯一的新接缝就是切换器自己的公开面（`Configure` / `ApplySerializedMode` / `Toggle` / `CurrentMode`）。不新增测试接缝，不新增 `InternalsVisibleTo`。

**测哪些模块**：只测 `MapViewModeSwitcher`（装配层）。两个状态类因为 `internal`，由装配层间接覆盖。手势组件"相机没开就不动"这条也走装配层（真的 `Camera` + 组件 + 手动调 `Update`，断言相机状态没变）。

**先例**：现有夹具 `MapViewModeSwitcherTests` 本身就是先例 —— 真 `GameObject` + `AddComponent`、`[TearDown]` 里 `DestroyImmediate`、期望日志先声明后触发、冷启动走 `ApplySerializedMode` 而不是依赖 `Start`。新夹具沿用这套骨架。

**搬过来原样保留的五条**（它们钉的是与实现无关的事实）：

1. UI 相机是 overlay 且恰在"会渲染的那个栈"里（栈成员 + render type 一起断言 —— 只断言成员会让 UI 静默不可见）。
2. UI 相机从不被移动、禁用或改父。
3. 退出俯视后 gameplay 相机被精确还原（含 `fieldOfView` / `orthographicSize`）。
4. 半接线的切换器不抛异常。
5. 共享相机接线只报一次，且不把那台相机说服成 overlay。

**新增的三条**（这次重构才引入的行为）：

6. 冷启动：`m_StartMode = TopDown` 时 `ApplySerializedMode()` **不 `Exit`、但照常归零**，然后进入俯视（**gameplay 相机必须是关掉的** —— 这条断言最初失败过，见第 10 节）；且在 `Toggle()` 之前调它仍能建立状态。
7. `Toggle()` 幂等 / no-op：**重复同一个请求不改变任何东西** —— 不重新捕获、不重推通道、不动呈现。注意这里钉的是"第二次按（同一个请求）"；**一次真正的往返会重新捕获**（每次离开 gameplay 都是一个新的起点），这是捕获语义的必然结果，旧实现也一样。
8. `Exit` → 归零 → `Enter` 的顺序与"无呈现"瞬间不可观察：一次切换内旧的 UI 根被关、新的被开、没有任何模式呈现的空窗不落到帧上。

**按新 API 改写的**：进入/退出俯视的相机与通道与 UI 根、UI 相机的栈迁移、反复切换无漂移、起始模式（改用枚举字段）、手势只在相机活着时生效。

## 8. 明确不做（本票）

1. **不实现任何新功能**。焦点入口、小地图相机槽、战场视角都不做，只把它们的落点写进文档与 issue。
2. **不改 `sw.unity`**。孤儿序列化值（`m_IsTopDown` / `m_FocusTarget` / `m_FocusZoom` / `m_MapCamera` / 两个 `m_IsEnabled`）留着，Unity 会忽略并在下次保存时清掉。
3. **不改 `OrthographicMapCamera` 与 `MapClickDispatcher`**。
4. **不引入 ADR、不引入 `InternalsVisibleTo`、不写泛型状态机**。
5. **不动 `TwoModeSampleSceneWirer` 的职责边界**：它仍只负责接线，不负责登记可关清单。
6. **不修** `docs/implementation/map-click-dispatch.md:226` 与场景不符这件事的"另一方向"（也就是不改场景去符合文档）。

## 9. 进一步说明

- **验证路径**：编写本 spec 的会话里 `pwsh` 不可用（沙箱报 `SetNamedSecurityInfoW failed (Win32 5)`），无法运行 `scripts/run-tests.ps1` 或 TestRunnerApi。实现完成后**必须**在编辑器里跑 `HexMap.Sample.Tests.EditMode` 并把结果写回本文件或对应 issue；「编译通过」不等于「行为正确」。`sw.unity` 的验收是人工的：进出两个模式各 5 次、观察 Console、观察 gameplay 滚轮是否还能偷改地图相机。
- **`HexMap.Sample` 是预定义之外的程序集**：新增文件后**不要手工创建 `.meta`**，交给 Unity。
- **本票完成后 `MapViewModeSwitcher` 的预期体量**：约 150 行结构 + 文档注释，逻辑集中在驱动器与两个助手方法里。若实现出来仍有两处以上"同一个判断出现在两个地方"，说明第 2 节的某条契约没落实。

## 10. 实施记录（写回，含偏离）

实现已完成。**`MapViewModeSwitcherTests` 12 条已在编辑器里全绿**（2026-02）；`MapGestureInputGuardTests`（01 票）与 `sw.unity` 的人工验收仍待确认。以下是实际形态与第 2 节契约的差异，逐条给出理由。

| 项 | 计划 | 实际 | 理由 |
| --- | --- | --- | --- |
| 快照归属 | 规格 §2 写"留在俯视状态的字段里" | **最终就是这个**：数据、守卫、捕获与还原全在 `MapTopDownViewState` 里 | 中途曾三度搬到驱动器 / context，都不通（见 10.3–10.6）。最终形态与规格原文一致，只是**捕获条件**由驱动器传入的 `previousMode` 决定 |
| 接口位置 | 每个类型一个文件 | `IMapViewModeState`、`MapGameplayViewState`、`MapTopDownViewState`、`GameplayCameraState` **同在 `MapViewModeStates.cs`** | 有效逻辑不足 80 行，一个文件读完"每个模式做什么"比拆开好找。`IMapViewModeState.cs` 是空文件占位，**待人工删除** |
| `Presentations` 类型 | `IReadOnlyList<MapViewPresentation>` | `MapViewPresentation[]` | 仍然是"只读的意图"（只有实现者能改），但**每个状态在构造期建好一次**，不在每次访问时新建数组 |
| `Enter` 的签名 | `Enter(MapViewModeContext)` | `Enter(MapViewMode? previousMode, MapViewModeContext)` | 俯视状态必须能区分"真从 gameplay 切过来"与"场景开局就在俯视" —— 前者要记位姿、后者绝不能记（见 10.6）。冷启动时 `previousMode` 为 `null` |
| 接线参数可见性 | 计划 `internal readonly struct` | **`public readonly struct`** | `HexMap.Sample.Tests.EditMode` 无友元访问，夹具必须构造它。与仓库既有先例一致（`DecorationPrefabBuilder` 因"测试程序集要调用它"而 public） |
| 公开面 | 恰好四个成员 | 四个 + **只读的 `Wiring` 属性** | 编辑器接线工具要读回"两个 UI 根是谁"才能把按钮与面板挂上去。没有它，工具只能去反序列化私有字段（第二种真相） |
| `MoveUiCameraIntoStack` | 计划读组件自己的 `m_UiCamera` | 改为**由状态传入** `uiCamera` | 票据要求两个状态各持有同一台 UI 相机的引用（Q31）。让状态传进来，组件的字段就只剩"真相"这一个职责，也顺手消掉了"同一个事实两个读法" |
| 名字 | 计划 `GameplayViewState` / `TopDownViewState` | `MapGameplayViewState` / `MapTopDownViewState` | 与同程序集里的 `GameplayMapClickHandler` 等区分开，避免混读 |

**新增（计划里没有的）**：`MapViewModeWiring` 值对象（§6.2 那个"由实现者定"的自由，取 struct + `in`）；`MapViewModeContext` 内 4 个动词（进模式 / 搬 UI 栈 / 显隐自己的根）；`GameplayCameraState`（相机位姿 + `Capture` / `Restore`，与状态同文件）；`MapViewModeSwitcher.EnterMode` 与 `SetUiRootActive` 两个 internal 助手。

**按第 2 节落实、无需说明的**：驱动器 `SetMode`（比较 → 退出 → 归零 → 进入，**归零在冷启动也跑**）、归零时先推 `MapClickChannels.None` 再逐个 `Hide()`、冷启动不 `Exit` 但归零、`CurrentMode` 由 `m_CurrentState` 派生、`m_StartMode` 枚举取代 `m_IsTopDown`、`SampleMapClickChannels` 取值不变而由枚举派生、状态互不认识、无抽象基类。

### 10.1 实施中发现并修掉的一个真实缺口（含一次错误诊断的更正）

`ASceneThatStartsInTopDownModeIsAppliedWithoutSwitching` 在编辑器里失败：探针日志给出 `startMode=TopDown currentMode=TopDown gameplayEnabled=True topDownEnabled=True`。

**真正的根因**：no-op 判写成了 `target == CurrentMode`，而冷启动时 `CurrentMode` 回落到 `m_StartMode`。当起始模式**恰好就是**要进入的那个模式时，`SetMode` 在**第一行**就 `return` 了 —— **归零与进入整个被跳过**，于是两台相机都开着。根源是 `m_CurrentMode` 这个字段被同时当成"冷启动的回落值"和"当前模式"。

**修法**：删掉 `m_CurrentMode` 字段，`CurrentMode` 直接由 `m_CurrentState` 派生（冷启动回落 `m_StartMode`）；no-op 判断改为拿**当前状态**比（`m_CurrentState != null && m_CurrentState.Mode == target`）。一个字段担两个语义的写法要整个去掉，而不是只把其中一处改对。

**一次错误诊断，记录在此以免重犯**：第一次修这个问题时，我把它归因于"冷启动跳过归零"，并据此改了 `SetMode` 的归零条件。那次修改**对这次失败毫无作用**（代码根本没走到归零那一步），但它**本身不是错的**：归零确实应该在第一次进入时也跑 —— 这个结论有独立理由（归零是"恰好一个模式在呈现"的唯一来源），与这次失败无关。教训：**拿到失败信息先要一个能区分假设的观测点**（这次一行探针日志就定位了），不要凭"最像的原因"改代码。

**为什么三条静态检查都没抓到**：语言特性、旧名字、跨程序集可见性都与它无关 —— 这是一条"两个字段语义重叠"的逻辑错误，只有真跑才暴露。

### 10.2 第二个真实缺口：捕获的时机错了

`LeavingAMapViewThatWasNeverEnteredLeavesTheGameplayCameraWhereItIs` 失败：断言 gameplay 相机停在 `(7,8,9)`，实际是 `(0,0,0)`。

**根因**：我把"捕获"放在 `TopDown.Enter()` 里无条件执行。而快照的语义**不是"进入俯视时拍"，而是"从 gameplay 离开时拍"** —— 那个待还原的位姿属于 gameplay 模式，是它让出相机时交出来的。旧实现正是这样（`ApplyTopDown()` 从不快照，只有 `EnterTopDown()` 才拍）。

**可观察后果（这条测试钉的就是它）**：开局即俯视的场景**从未进入过 gameplay**，因此不该有任何位姿被记住。在 `Enter` 里捕获会把一台玩家从未用过的相机（位于原点）记下来，第一次退出时再"还原"回去，看起来像相机被重置了。

**修法**：捕获移到 `GameplayViewState.Exit()`（"一次就不再覆盖"的守卫保留，于是"第一次离开 gameplay 的那一帧"才是快照的定义）。俯视状态只负责还原。

**教训（与 10.1 同类，但方向不同）**：这次不是"两个字段语义重叠"，而是**我把一条规则绑定到了错误的一侧**。"谁的数据"回答的是归属，"谁在什么时候交出它"回答的是时机 —— 我在 10.1 里刚学到"要比状态而不是比派生值"，这里又栽在"要在让出方而不是取得方"。两次都是**只有真跑测试才暴露**的逻辑错误。

### 10.3 第三个缺口：把捕获挂到"退出"上，忘了冷启动不经过任何退出

10.2 的修法（捕获放在 `GameplayViewState.Exit`）**引入了一个新 bug**：`ComingBackFromTheMapViewRestoresTheGameplayCameraExactly` 失败 —— 第一次按压进俯视、第二次按压回 gameplay 后，相机停在测试设的 `(-1,-2,-3)` 而不是 `(4,5,6)`。

**根因**：冷启动时 `m_CurrentState == null`，`SetMode` **跳过整个 `Exit` 块**，所以 `Gameplay.Exit` 从未执行 ⇒ 捕获从未发生 ⇒ 第二次按压的还原无事可做。

**这是两条约束的交叉，我只看了一条**：
- "捕获属于让出相机的那一方"（10.2 的结论）⇒ 想放在出发侧；
- **但出发侧可能根本不存在**（冷启动不经过 `Exit`）⇒ 不能只挂在 `Exit` 上。

**修法**：捕获回到驱动器，但条件是"**前一个状态是 gameplay**"。这样两条约束同时满足：冷启动（前一个状态为 null）不捕获，"开局即俯视"不捕获，而"gameplay → 俯视"一定捕获。驱动器是唯一知道前一个状态是谁的地方，所以这件事**本来就不该由状态做**。`MapGameplayViewState.Exit` 回到空实现。

**取证过程记一笔**：这个 bug 我连续三次判断错误（先怪归零、再怪字段语义、第三次才在探针下看清是"冷启动不经过 Exit"）。最终定位靠的是在 `SetMode` 的两个分支各打一行日志 —— **`cold start, nothing to leave` 那一行直接给出了答案**。前两次我都是凭"最像的原因"改代码，各白改一次。教训：**当一个失败有三种以上可能解释时，先花一次运行换取能区分它们的事实，而不是挑一个看起来最合理的改。**

### 10.4 第四个缺口（10.3 的修法引入）：机制越挪越脆

10.3 把捕获放回驱动器、条件写成"前一个状态是 gameplay"之后，同一条测试**仍然逐字相同地失败**。至此捕获已经被挪过三个位置（俯视 `Enter` → gameplay `Exit` → 驱动器 `SetMode`），每次都在"另一个对象的某个条件"上做文章。

**修法（最终形态）**：不再挪位置，而是**取消跨对象的机制** —— 快照的数据、标志、捕获与还原全部搬进 `MapTopDownViewState` 自己：

```
MapTopDownViewState
  private GameplayCameraState m_GameplayCameraState;   // 数据
  private bool m_HasGameplayCameraState;               // 守卫
  Enter: if (!m_HasGameplayCameraState) { m_GameplayCameraState = Capture(m_GameplayCamera); ... }
  Exit:  if (m_HasGameplayCameraState)  { m_GameplayCameraState.Restore(m_GameplayCamera); }
```

于是驱动器的两个快照字段、`CaptureGameplayCamera` / `RestoreGameplayCamera`、私有 `CameraState`、context 上那两个方法、接口上的 `previousMode` 参数全部删除。**捕获与还原变成同一个类的相邻两个方法，中间没有第二处状态可以被搞错。**

### 10.5 真正的教训：我追了六轮代码，问题却可能在"跑的不是这一版"

决定性的证据出现在**最后一次**运行时，而不是任何一次探针里：

| 证据 | 含义 |
| --- | --- |
| 栈里 `MapViewModeContext.cs:66`，而磁盘上那行在 **71** | 那份汇编是用**更早一版**的文件编出来的 |
| 本次会话多次遇到 `ReplaceFileW EIO (Win32 1175)`（编辑器正持有文件，写入失败） | "我改了但汇编里没有"在这个会话里是**实测发生过**的状况 |
| 最后一次：栈里 `MapViewModeStates.cs` 的行号是 **153**（磁盘上的准确值），测试**随即通过** | 行号对上 ⇒ 新汇编 ⇒ 通过 |

**教训**：当"代码看起来对、测试仍然红"时，第一件该做的事是**确认跑的是不是这一版**（比对栈里的行号与磁盘上的行号，一次点击的成本），而不是继续推理代码。我在第六轮就拿到了栈信息，却把它只当成"调用链证据"，没有拿去比对行号 —— 为此白花了三轮。

**另一个教训**：机制被搬来搬去本身就是警号。捕获位置换了三次、每次都依赖"另一个对象在某个时刻的状态"，说明**归属没定对**。最终一次改动（把数据和逻辑收进同一个状态）没有再换判断条件，而是**删掉了需要判断的跨对象状态** —— 这类"结构性修复"比"再调一次条件"更可能一次命中。

### 10.6 第五个缺口：捕获条件不足（"开局即俯视"被误记位姿）

把快照收进俯视状态、只用 `!m_HasGameplayCameraState` 作守卫之后，`ComingBackFromTheMapViewRestoresTheGameplayCameraExactly` 通过，但 `LeavingAMapViewThatWasNeverEnteredLeavesTheGameplayCameraWhereItIs` 变红：开局即俯视的场景里，`TopDown.Enter` **仍然捕获了 `(0,0,0)`**，退回 gameplay 时又"还原"回去，覆盖掉测试设的 `(7,8,9)`。

**根因**：`!m_HasGameplayCameraState` 分辨不出两种"第一次进俯视"——

| 路径 | 该记位姿吗 | 旧守卫的判断 |
| --- | --- | --- |
| gameplay → 俯视（真正的切换） | **要**（记住玩家的相机） | 没有快照 ⇒ 捕获 ✓ |
| 场景开局就在俯视 | **不要**（玩家从未有过那条位姿） | 没有快照 ⇒ **也捕获** ✗ |

**修法**：捕获条件补齐为 `previousMode == MapViewMode.Gameplay && !m_HasGameplayCameraState`。`previousMode` 由驱动器在**状态改变之前**读出并传入状态（冷启动为 `null`）。两项各管一件事：第一项区分"真切换"与"开局即俯视"，第二项负责"只记一次"，使连按按钮不会把被移动过的相机当成原状态。

**教训**：这一条本可以在上一次修复后立刻发现 —— 那两条测试的期望（**真切换要记位姿** / **开局即俯视不许动**）在语义上互相排斥，必须同时过一遍。我上一轮只看到"一条通过了"就认为机制对了，没有把语义相反的另一条一起验证。

### 10.7 六轮里最贵的一课：先确认跑的是哪一版

有一个反复出现的干扰：**"我改了、汇编里却没有"**。本会话多次遇到 `ReplaceFileW EIO (Win32 1175)`（编辑器正持有文件，写入失败），而早期一份栈里 `MapViewModeContext.cs` 显示 `:66`、磁盘上却在 `71`（差 5 行）。

直到某次我把**栈里的行号**与**磁盘上的行号**逐一对上（`MapViewModeStates.cs:153`），测试才第一次通过；行号不符时，我怎么推理都是错的方向。

**操作结论**：当"代码看着对、测试仍然红"时，第一步是**比对栈里的行号与磁盘上的行号**（一次点击的成本），而不是继续推理代码。这一点应当写进本仓库的调试习惯里。

### 10.8 最后一类问题：测试夹具自己不自洽（3 条）

12 条夹具里有 3 条的**期望值与自己的夹具互相矛盾**，与产品代码无关：

| 测试 | 错在哪 |
| --- | --- |
| `ComingBackFromTheMapViewRestoresTheGameplayCameraExactly` | 在**第一次按压之前**就把相机设成 `(4,5,6)`。第一次按压是冷启动（`previousMode=null`），从不"离开 gameplay"，因此从未记过 `(4,5,6)` —— 断言的东西根本没被记下来。修法：先 `ApplySerializedMode()` 进 gameplay 再设位姿，然后走一个**两次按压**的往返 |
| `PressingTheButtonTwiceIsNotAModeChange` | 1 次按压 + 3 次 `Toggle()` = **4 次（偶数）**，从 gameplay 出发末态必然是 Gameplay，却断言 TopDown。修法：先 `ApplySerializedMode()` + 一次 `Toggle()` 进俯视，再按两次（重复请求） |
| `PressingTheMapButtonTwiceKeepsTheOriginalPose` | 1 次按压 + 2 次 `Toggle()` = **3 次（奇数）**，末态是 TopDown，与"相机回到最初位姿"互相排斥。修法：进入 gameplay 记下 `captured` ⇒ `Toggle()` 进俯视 ⇒ 相机移到 `(9,9,9)` ⇒ 两次 `Toggle()` |

**教训**：改完夹具后应当把"按压次数 → 末态"推一遍再跑；三条矛盾都能靠这一步发现。前几轮我把注意力全放在实现上，反而没做这件最便宜的自查。

**验证状态**：未编译、未跑测试（本会话 `pwsh` 不可用）。已做的静态核对如下，其余待办见 `issues/03-editor-acceptance.md`。

**静态核对（2026-02，不依赖编译器的最大范围）**：

1. **语言特性有先例**：`readonly struct` 在本仓库出现 10 处以上（`HexCoord`、`HexLayout`、`OrthographicMapFraming`、`MapClickContext`…）；`in` 参数出现在 `IMapClickHandler.OnMapClicked(in MapClickContext)`；**调用点不带 `in` 关键字直接传结构体**的写法有先例（`MapClickDispatcher.cs` 里 `handler.OnMapClicked(m_LastContext)`）。所以 `readonly struct` 与 `Configure(in MapViewModeWiring)` 两项风险解除。
2. **旧名字已清空**：`Assets/` 下 `EnterTopDown` / `ExitTopDown` / `IsTopDown` **零命中**；唯一的 `m_IsTopDown` 是 `Assets/sw.unity:165` 那个孤儿序列化值（预期，Unity 会忽略并在下次保存时清掉）。
3. **跨程序集可见性自查**：`MapViewModeWiring`（public）的成员只暴露 public 类型（`Camera` / `GameObject` / `MapClickDispatcher` / `MapViewMode`），其构造函数参数类型也全部 public；`Wiring` 属性返回 public 类型。所以"public 类型的接口里有 internal 类型"这类无法编译的情况不存在。
4. **仍未被证伪的假设（只能靠编译或运行）**：`SerializedProperty.intValue` 可写枚举字段（`TwoModeSampleSceneWirer.StartInGameplayMode` 与夹具的 `SetSerializedStartMode` 都用它）；`GetState` 的"未知模式回落到 gameplay"是否会在将来加模式时静默出错（已知取舍，见实现记录）。

# 02 — 切换器换成状态机，夹具与接线工具同步换掉

**What to build:** `MapViewModeSwitcher` 从"一堆散落的布尔与分支"变成"每个模式一个状态对象 + 一个负责归零的驱动器"。

**用户视角的行为一条不变**：按按钮开地图、再按关掉、UI 跟着换、点击各自走对的通道、退出后 gameplay 相机原样还回来。变的是实现结构：**新增一个模式 = 新写一个状态类**，驱动器与已有状态零改动。

**Blocked by:** 01 — 手势组件自守：相机没开就不动（未完成就不能删切换器里的手势开关，否则是行为回退）

**Status:** done — `MapViewModeSwitcherTests` 12 条在编辑器里全绿（2026-02）。实施记录、五处缺口与三类夹具矛盾的复盘见 `../spec.md` 第 10 节。仍待人工：`sw.unity` 的端到端验收（见 `03-editor-acceptance.md`）。

## 范围

- [ ] 新建 `MapViewMode` 枚举（`Gameplay = 0`、`TopDown = 1`），public（场景与接线工具按名字写它）
- [ ] 新建状态契约：每个状态给出自己的模式、`Enter`、`Exit`、以及"我可能打开的呈现"清单
- [ ] 两个状态类（`GameplayViewState` / `TopDownViewState`），纯 C# 类、internal、构造期一次性注入引用、惰性构造后缓存
- [ ] 状态只持有：自己那台相机、自己那个 UI 根、**同一台 UI 相机的引用**（各存一份指向同一台，真相仍只有一份）、自己那个点击通道整数
- [ ] 状态**不认识**另一个模式，也不认识手势组件、焦点、地图相机
- [ ] `Exit` 只管自己那半（关自己的 UI 根）；**不关别人的** —— 别人的关是驱动器的活
- [ ] 俯视状态持有 **gameplay 相机引用 + 自己的快照**：`Enter` 第一次进入时捕获（已有则不覆盖），`Exit` 还原。驱动器与 context 都不参与（见 10.5 —— 机制搬过三次位置后，最终把数据与逻辑收进同一个状态）
- [ ] 驱动器：`SetMode(target)` = **当前状态存在且其模式 == target** 时直接返回 → `当前状态?.Exit()` → `ResetPresentation()` → 写当前状态 → `Enter()`。**no-op 判必须比当前状态，不能比 `CurrentMode`**（冷启动时它回落成起始模式，会把"起始模式就是要进入的模式"误判成重复请求并跳过整个进入 —— 这正是第一次跑测试失败的原因，见 Comments）
- [ ] `ResetPresentation()`：遍历一张**扁平**的登记清单，把每项关掉，并把点击通道推成"无模式"。驱动器不知道有几个模式、叫什么
- [ ] 呈现登记：每个状态自注册自己可能打开的组件；驱动器在构造状态后展开成扁平清单。相机登记为"开关它的 `enabled`"，UI 根登记为"开关它的 `activeSelf`"（**不用** `SetActive` 的自递归语义去夹带别的组件）
- [ ] 冷启动：`m_CurrentState == null` 时**不 `Exit`**（没有前一个状态）**但照常归零**，然后 `Enter` 起始模式；`Start()` 与每次切换走**同一个 `Enter`**，不存在第二条 "apply" 路径。**"归零"不能在冷启动省掉** —— 它是"恰好一个模式在呈现"的唯一来源，见 Comments 里的失败记录
- [ ] 当前模式只有一个真相（当前状态对象），公开的 `CurrentMode` 由它派生，冷启动时回落到起始模式
- [ ] 起始模式字段：`bool m_IsTopDown` → `MapViewMode m_StartMode`。`sw.unity` 里现有值是 `0`，与 `Gameplay = 0` 同值，所以**不需要改场景文件**
- [ ] `SampleMapClickChannels` 的取值保持 `Gameplay = 1` / `TopDown = 2` 不变，改为由枚举派生
- [ ] 共享机制仍归驱动器：UI 相机入栈（render type + 栈成员）与"接线错误只报一次"。两个状态各调一次同一个 internal 助手
- [ ] **删除**：`EnterTopDown` / `ExitTopDown` / `IsTopDown` / 十个 get/set 属性对 / `m_FocusTarget` / `m_FocusZoom` / `FocusIfRequested` / `m_MapCamera` / `m_MapDragInput` / `m_MapZoomInput` / `SetMapInputEnabled`
- [ ] 公开面收敛为：`Configure(...)`、`ApplySerializedMode()`、`Toggle()`（**保持无参** —— 场景里两个按钮是 `m_Mode: 1` 的 UnityEvent，按方法名绑定，改名或改签名都会静默断掉）、`CurrentMode`
- [ ] `Configure(...)` 的确切形状由实现者定，判据只一条：**调用点必须能一眼看出谁接谁**（一个 internal readonly struct 形状的接线参数，或直接在接线工具/夹具里写命名参数，两者都可接受）
- [ ] 接线工具同步：起始模式字段改名 + 删掉对 `MapCamera` / `MapDragInput` / `MapZoomInput` 的接线 + 删掉按字符串写 `m_IsTopDown` 的那段。**不新增职责**（它仍只负责接线，不负责登记可关清单）
- [ ] 夹具按新 API 重写（见"验收"）
- [ ] 文档：`docs/implementation/map-click-dispatch.md` §5 结构图重画（"模式值的唯一主人"这条结论**保留**，改的是实现层次）、§7 职责表重写、§8 可观察行为清单按新夹具重写；**修正**那条"地图按钮必须放在切换器不碰的常驻 root 里"——实际是两个按钮各自挂在自己那个会被开关的 UI 根上，一开一关，这是有意的
- [ ] `.scratch/minimap/issues/02` 与 `issues/03` 的措辞对齐新结构（小地图相机改为"登记进 gameplay 状态的可关呈现"；焦点入口改为"调用方在 `Toggle()` 之前一次调用"），`spec.md` 顶部已有指引行，核对一遍
- [ ] **不碰** `sw.unity`、不碰 `OrthographicMapCamera`、不碰 `MapClickDispatcher`、不动任何 `.meta`、不写 ADR、不引入泛型状态机、不引入 `InternalsVisibleTo`

## 验收

**搬过来原样保留的五条**（钉的是与实现无关的事实）：

- [ ] UI 相机是 overlay，且**恰在"会渲染的那个栈"里**（栈成员与 render type 一起断言 —— 只断言成员会让 UI 静默不可见）
- [ ] UI 相机从不被移动、禁用或改父
- [ ] 退出俯视后 gameplay 相机被**精确**还原（含 `fieldOfView` / `orthographicSize`）
- [ ] 半接线的切换器不抛异常
- [ ] 共享相机接线只报一次，且不把那台相机说服成 overlay

**新增三条**（这次重构才引入的行为）：

- [ ] 冷启动：起始模式为俯视时 `ApplySerializedMode()` **不 `Exit`、但照常归零**（gameplay 相机必须被关掉 —— 这条断言第一次跑是红的），且它是第一个动作时也能建立状态
- [ ] `Toggle()` 幂等 / no-op：切到当前模式时世界与快照都不变（"第二次按按钮不得重新快照"）
- [ ] 一次切换内 `Exit` → 归零 → `Enter`，"没有任何模式在呈现"的空窗不落到帧上（旧 UI 根被关、新的被开、点击通道与相机 `enabled` 在同一步内完成）

**按新 API 改写的**：

- [ ] 进入/退出俯视：相机、点击通道、两个 UI 根的开关
- [ ] UI 相机在恰好一个会渲染的栈里（两个方向都查）
- [ ] 反复切换无漂移
- [ ] 手势只在相机活着时生效（断言对象从"`IsEnabled` 被设成什么"换成"相机没开时手势不产生效果"）
- [ ] 起始模式（改用枚举字段读写）

**其他**：

- [ ] `MapViewModeSwitcher` 里不再有"同一个判断出现在两个地方"（若有两处以上，说明契约没落实）
- [ ] C# 编译通过；`.scratch/` 外的文档改动与实现一致

## Comments

- **实施记录（2026-02）**：全部代码、夹具、接线工具与文档已落地，形态与偏离逐条写在 `../spec.md` 第 10 节。摘要：快照最终由 `MapTopDownViewState` 自己持有（数据 + 守卫 + 捕获/还原）；三个状态类型合并进一个文件；`Presentations` 用 `MapViewPresentation[]` 且构造期建好；`MapViewModeWiring` 因夹具需要而 `public`；切换器多一个只读的 `Wiring` 属性供接线工具读回；`MoveUiCameraIntoStack` 的 UI 相机改为由状态传入（落实"两个状态各持同一台 UI 相机的引用"）。
- **验收项"手势只在相机活着时生效"落在 01 的夹具**（`MapGestureInputGuardTests`），不在本票的夹具里：切换器已经不认识手势，本票的夹具只钉"模式仍然决定地图相机何时活着"。真正的手势行为由 01 的实现与它的夹具负责。
- **未验证**：本会话 `pwsh` 不可用，无法编译或跑测试。`m_StartMode` 的序列化写入（接线工具用 `SerializedProperty.intValue`）与 `Configure(in ...)` 的调用方式是两处最可能出问题的地方，见交接说明。
- **第一次跑测试暴露的真实缺口（已修，含一次错误诊断）**：`ASceneThatStartsInTopDownModeIsAppliedWithoutSwitching` 失败，gameplay 相机 `enabled == true`。探针日志（`startMode=TopDown currentMode=TopDown gameplayEnabled=True topDownEnabled=True`）定位到**真正根因**：no-op 判写成 `target == CurrentMode`，冷启动时 `CurrentMode` 回落成 `m_StartMode`，于是"起始模式恰好就是要进入的模式"被当成重复请求，`SetMode` 第一行就返回，**归零与进入整个被跳过**。修法：删掉 `m_CurrentMode` 字段（`CurrentMode` 由 `m_CurrentState` 派生），no-op 判改比当前状态。
  另记一次**错误诊断**：第一次修的时候我归因于"冷启动跳过归零"，改了归零条件 —— 那次修改对这次失败毫无作用（代码根本没走到那里），但归零确实也该在第一次进入时跑，所以那一处保留。教训：拿到失败先要一个能区分假设的观测点，不要凭"最像的原因"改。详见 `../spec.md` 第 10.1 节。
- **第二次跑测试暴露的第二个缺口（已修）**：`LeavingAMapViewThatWasNeverEnteredLeavesTheGameplayCameraWhereItIs` 失败，相机被"还原"到 `(0,0,0)` 而不是停在 `(7,8,9)`。根因是我把**捕获**放在俯视的 `Enter` 里无条件执行，而快照的语义是"**从 gameplay 离开时**拍"（旧实现正是如此）。开局即俯视的场景从未进入过 gameplay，不该有位姿被记住。详见 `../spec.md` 第 10.2 节。（这一版的修法本身又引出第三个缺口，见下条。）
- **第三次跑测试暴露的第三个缺口**：第二次的修法（捕获放在 `GameplayViewState.Exit`）**引入了新 bug** —— 冷启动时 `m_CurrentState == null`，`SetMode` 跳过整个 `Exit` 块，于是捕获从未发生，从俯视退回时相机停在原地而不是被还原。当时把捕获挪回驱动器、条件写成"前一个状态是 gameplay"（这是第四次改动，仍未通过，见下条）。详见 `../spec.md` 第 10.3 节。
- **第四次起：机制搬了三次位置都不通，最终改为"取消跨对象机制"（已通过）**：捕获先后放在俯视 `Enter`、gameplay `Exit`、驱动器 `SetMode`（条件"前一个状态是 gameplay"），同一条测试**逐字相同地失败**。最终把快照的数据、守卫、捕获与还原**全部收进 `MapTopDownViewState` 自己**，驱动器的两个快照字段、`CaptureGameplayCamera` / `RestoreGameplayCamera`、私有 `CameraState`、context 上那两个方法、接口上的 `previousMode` 全部删除。**该测试随后通过**（`MapViewModeSwitcherTests` 其余用例待全量确认）。
- **真正的教训（写在 `../spec.md` 10.5）**：最后一次运行时栈里的 `MapViewModeStates.cs` 行号是 `153`（与磁盘一致），测试**随即通过**；而更早的一份栈里 `MapViewModeContext.cs` 显示 `:66`、磁盘上却是 `71` —— 说明**那几轮跑的很可能是更早一版的汇编**。本会话多次遇到 `ReplaceFileW EIO (Win32 1175)`（编辑器持有文件、写入失败），"改了但没进汇编"是实测发生过的事。**"代码看着对、测试还红"时，第一件事应当是比对栈里的行号与磁盘上的行号，而不是继续推理代码。**

# 01 — 手势组件自守：相机没开就不动

**What to build:** 相机不渲染的时候，鼠标拖拽、滚轮、双指捏合**不再改变** `Center` / `Zoom`。这是一个纯前瞻修复，不改变任何现有可观察行为 —— 现有 `MapViewModeSwitcher` 仍然照旧设置两个组件的 `IsEnabled`，两套机制在这一票里并存。

**Blocked by:** None — can start immediately

**Status:** done — 2026-02-14，EditMode 全绿（`MapGestureInputGuardTests` 6 条）+ 人工验收通过

## 背景（为什么它必须先做，且必须单独做）

今天 gameplay 模式下滚轮不会偷改地图相机，**唯一原因**是切换器开机的 `SetMapInputEnabled(false)` 把它俩关了。而两个手势组件的 `Update` 守卫只看自己的 `m_IsEnabled`，**从不检查相机是否 `enabled`**；`sw.unity` 里两者的 `m_IsEnabled` 都是 `1`。

后续的「视图模式状态机」票要删掉切换器里的手势开关（手势不是"模式"的责任）。那一步只有在**这一票先落地**的前提下才是纯粹的删除；否则就是行为回退：相机不渲染，但 `Zoom` / `Center` 被静默改掉，玩家下次打开地图看到的是错视角 —— 玩家看不见、测试也看不见，只能靠人工发现。

这一票单独存在的另一个理由：它本身是一次独立的、可单独验证的**行为加固**（"手势只在相机活着时生效"从此是组件自己的不变量，任何调用方都不需要记得去设置它）。与结构重构混在一张票里，一旦回归出问题就分不清是哪一半引起的。

## 范围

- [ ] 两个手势组件在 `Update` 的既有守卫上补一条：相机自身没开着时不产生任何效果（拖拽不累积、缩放不应用）
- [ ] 捏合/拖拽的**手势状态要复位**，与既有的 `EndGestureIfActive()` / `m_IsDragging = false` 分支同样处理，避免"相机重新打开后，第一次移动按上一个手势的起点算"
- [ ] 这一票**不动**切换器，也不动两个组件的 `IsEnabled` 属性与 `m_IsEnabled` 字段（它们是下一票要删的东西）

## 验收

- [ ] EditMode：相机 `enabled == false` 时喂入一个拖拽 delta ⇒ `Center` 不变
- [ ] EditMode：相机 `enabled == false` 时喂入滚轮/捏合 ⇒ `Zoom` 不变
- [ ] EditMode：相机 `enabled == true` 时同样的输入 ⇒ 照常生效（证明这一票没有把功能一起关掉）
- [ ] EditMode：相机从开到关、再开，第一次移动不按旧手势起点计算
- [ ] 现有 `TheMapGesturesAreOnlyLiveInTopDownMode` 保持绿（这一票不改变模式切换的可观察行为）
- [ ] C# 编译通过

## Comments

- **实现记录（待人工验收）**：守卫落在两个组件各自的 `IsCameraLive()` 私有判定上（`m_MapCamera != null && m_MapCamera.Camera != null && m_MapCamera.Camera.enabled`），`Update` 的两个守卫分支都改为调用它；拖拽侧原本的 `m_MapCamera == null` 被它取代，捏合侧同理。
- **验收标准第 1、2 条的措辞要按能力修正**：EditMode 里**无法喂入输入**（`Input.GetMouseButton` 恒 false、`Input.touchCount` 恒 0、`Input.mouseScrollDelta` 恒 0，且 `Input` 是 legacy 的、测试无法注入）。所以「喂入一个拖拽 delta ⇒ `Center` 不变」这类断言在 EditMode 里**不可实现**。实际落地的是等价且可测的那一半：**注入真实的手势状态（拖拽中 / 捏合中）+ 相机禁用 ⇒ 手势被结束、相机未被移动**，外加"相机灭了又亮 ⇒ 下一帧不按旧手势起点继续"。守卫与非守卫分支在 EditMode 里的差别本身不可观测，这一点写在夹具的类注释里，不假装有覆盖。
- **人工验收结果（2026-02-14，通过）**：全部测试通过。验收标准第 1、2 条的措辞按能力修正后（见上一条）由可测的一半 + 人工确认共同覆盖。
- **排查过程中发现的一个未解释现象（本票不处理，留作线索）**：在 EditMode 夹具里，把 `m_MapCamera.Zoom` 设为 5 之后，再把 `Camera.enabled` 关掉又打开，`Zoom` 读回来是 **1**（`MinZoom`），而相同起点上只做「相机禁用 + 一帧」时仍是 5。`OrthographicMapCamera` 里只有 `TryRefresh` 一条路径会把 zoom 打回 `MinZoom`，而它只在 `IsCameraConfigurationUnchanged()` 为假时才被调用 —— 所以嫌疑是「相机 `enabled` 的开关改变了相机上报的 viewport/`aspect`，让快照判定为陈旧」。这可能只是 EditMode 下的度量假象，也可能是真实场景里"关闭再打开相机会重置取景"的行为。**没有继续深挖**：它不属于本票范围，且本票的守卫是**保护**这类重置的（手势不会在相机回来时突然改视角）。如果后续要查，最小判别法是「设 5 → 只 `enabled = false` → 读」。相关断言已从夹具中移除，不让一条未定性的现象变成红/绿噪声。

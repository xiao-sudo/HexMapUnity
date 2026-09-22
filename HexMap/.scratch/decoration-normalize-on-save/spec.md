# 保存时归一化装饰物容器

把「装饰物落到 `Decoration` / `Overlay` 容器下」从**每次拖放**改成**每次保存**。本文取代"拖放收编"方案（那份决策文档已随方案清理；其中仍然成立的实测事实见第 8 节）。

## 1. 为什么换方案

拖放收编的实现已经验证可用（活编辑器实测：5/5 次拖放都正确入容器），但它把"归位"绑在**每一次创建事件**上，于是必须在 Unity 的创建事件、`delayCall`、undo 分组之间逐帧周旋。复杂度不在"找容器"（`DecorationContainerPolicy` + `DecorationBands.BandFor` + 19 条单测已全绿），而在**触发时机**。

真正暴露出来的缺陷是 undo：拖放的"创建"与我们的"收编"落在两个 undo 组里，于是**第一次 Ctrl+Z 会把实例退回场景根**——看起来就像工具从未生效。实测：收编发生在 `19:47:07.745`，随后的 Ctrl+Z 在 `19:47:07.757`，撤销快照显示 `Overlay(0)` 且该实例出现在场景根。用户报告"有的时候放不进对应的 GameObject"正是这个。

**保存不是 undo 步骤**，所以新方案从根上不存在这个问题。

## 2. 契约

| 项 | 决定 |
| --- | --- |
| 归一化对象 | 一个**根**下面的所有 `DecorationView`（`GetComponentsInChildren<DecorationView>(true)`）。装饰 Prefab 的组件恒定在根上，所以每个实例只被处理一次 |
| 容器的位置 | **归一化根下**：Prefab → Prefab 根的直接子物体；场景 → 场景根。不再有"投放落点父级"这条分支 |
| 容器名 | `DecorationBand` 的枚举名：`Decoration` / `Overlay`（`DecorationBands.BandFor(queue)` 是唯一反查） |
| 容器判定 | 名字精确相等 **且** 自身没有 `DecorationView`（`DecorationContainerPolicy.IsContainer`），其余组件一概不管 |
| 移动方式 | 收敛：无论实例原本在哪一层、是否已经在**另一个**容器里，一律移到目标容器下；已在目标容器下则不动 |
| 世界位姿 | 换父级后写回世界坐标与旋转（`AdoptPreservingWorldPose` 的同一套做法） |
| 未定义 band | 留在原地 + `LogWarning`（与旧方案一致） |
| 幂等 | 是。第二次归一化不产生任何变化（判据：移动计数为 0） |
| Undo | **不注册 undo**。这是一条结构不变量，不是用户动作；保存后的文件才是事实。代价：Ctrl+Z 不会撤销归位（想挪出去就手动拖，下次保存再收敛） |
| 触发 | 显式保存：Prefab 保存前 + 场景保存前；另有一个手动菜单项做兜底 |
| 不触发 | Prefab 模式的**自动保存**（见第 4 节的待调研项） |

## 3. 编辑位置与结构

用户确认：装饰物**在两个地方都会被编辑**。

| 位置 | 归一化入口 | 容器在哪 |
| --- | --- | --- |
| 新建的「装饰物合集」Prefab（Prefab 模式） | `NormalizePrefab(prefabRoot)` | Prefab 根的直接子物体 |
| `map.unity` 场景 | `NormalizeScene(scene)` | 场景根 |

场景里现有的两个空容器（`Decoration` / `Overlay`）正好符合"场景根"这条规则，不需要迁移。

## 4. 待调研（唯一的技术依赖，后台调研进行中）

1. **Prefab 保存前钩子**：`PrefabStage.prefabSaving` 是否在写盘**之前**触发、回调里改的层级是否会被写进文件。
2. **"只在显式保存时跑"能否成立**：Prefab 模式的自动保存默认开启，且可能触发同一个事件。若无法区分，退路是：① 关掉 Prefab 模式的自动保存（用 EditorPrefs/偏好设置）；② 只保留手动菜单 + 场景保存钩子。
3. **场景保存前钩子**：`EditorSceneManager.sceneSaving` 的签名与"回调里改层级是否会被写进文件"。
4. **要不要标记脏**：在保存回调里改层级之后，是否需要 `EditorSceneManager.MarkSceneDirty` 之类，才能保证改动落盘。
5. **重入**：归一化本身会不会再次触发保存/导入。

## 5. 保留 / 删除

| 件 | 处理 |
| --- | --- |
| `DecorationContainerPolicy`（找/建容器） | **保留**，入口从"按实例的父级"改为"按显式给定的父级"（`Resolve(Transform parent, Scene scene, band)`） |
| `DecorationBands.BandFor` / `ContainerName` / `IsContainer` | **保留** |
| 原 19 条策略单测 | **保留并改造**（现为 18 条，入口从"按实例的父级"改为显式父级），精确匹配、不递归、同名装饰被跳过并告警、多候选取第一个、非法 band 不留半成品等断言照旧有效；另新增 14 条归一化单测 |
| `DecorationInstanceAdopter.cs`（订阅 + delayCall + undo 合并） | **删除**，含临时的 `[DEBUG-drop-7f31]` 仪表 |
| 新增 | `DecorationNormalizer`（纯逻辑、可测）+ 保存钩子薄壳 + 手动菜单项 |

## 6. 交付切分

| Issue | 内容 | 状态 |
| --- | --- | --- |
| `01-normalizer.md` | `DecorationNormalizer`（`NormalizePrefab` / `NormalizeScene` / `NormalizeRoots`）+ 策略入口改为显式父级 + 单测 | **完成**（策略 18 + 归一化 14 = 32 条，Unity 侧 `HexMap.Editor.Tests.EditMode` **61/61 全绿**） |
| `02-save-hooks.md` | `DecorationContainerHooks`：`prefabSaving` + `sceneSaving` + 两个兜底菜单 | **完成并人工验证**：场景里拖入的装饰物在 Ctrl+S 后进入 `GVGRoot` 实例的 `Decoration`，YAML 记录为 `m_AddedGameObjects`（`targetCorrespondingSourceObject` = 预制体内 `Decoration` 的 transform） |
| `03-retire-drop-hook.md` | 删除 `DecorationInstanceAdopter` 与 `[DEBUG-drop-7f31]` 仪表；实现文档第 13 节标注取代 + 新增第 14 节 | **完成** |

## 7. 验证方法

- **自动化**：归一化与容器策略的 EditMode 单测（多实例、嵌套、幂等、跨容器纠正、未知 band 留原地、世界位姿不变）。
- **人工**：新建「装饰物合集」Prefab → 在 Prefab 模式里随手拖入装饰/覆盖物 → Ctrl+S → 结构收敛；在 `map.unity` 里同样操作 → Ctrl+S → 收敛；再保存一次确认幂等（无变化）。
- **回归**：`HexMap.Editor.Tests.EditMode` 全绿；`DecorationLayerOrderTests`（PlayMode）不受影响（层级与排序无关）。

## 8. 从上一版方案继承的实测事实（2026-09-22，活编辑器）

上一版是"拖放时归位"。它的决策文档与探针日志已随方案清理，但其中**实测到的事实**仍然成立，留在这里以免重新踩：

| 事实 | 结论 |
| --- | --- |
| 拖放是否发布 `CreateGameObjectHierarchy` | 是：一次拖放恰好 1 条，对象是实例根、带 `DecorationView`、`parent=<null>`（空白处投放）、`scene='map'` |
| `CreateGameObjectHierarchyEventArgs` 的字段 | 本版本**只有** `instanceId` 与 `scene`，没有 `parent` / `createdGameObject` |
| `args.scene` 是否可信 | **否**：实测打印过 `scene=''`（空/无效场景）→ 场景必须从解析出的对象或实例上取 |
| 事件里的对象是否总能解析 | **否**：同帧内被创建又被销毁的对象，`EditorUtility.InstanceIDToObject` 返回 null |
| 同帧多条事件 | 一次性**批量发布**（实测一次 5 条）→ "收集 → 帧末处理"是必要形态 |
| `Undo.SetTransformParent` | 本版本只有 `(Transform, Transform, string name)`，**没有** `worldPositionStays` 重载；文档写明它等价于 `transform.parent = newParent`（保留**局部**位姿）。照抄 `(t, parent, true)` 会得到 `CS1503` |
| 为什么不能按创建事件归位 | 创建与归位落在两个 undo 组里：第一次 Ctrl+Z 会把实例退回场景根，看起来就像工具没生效（这正是换方案的直接原因） |

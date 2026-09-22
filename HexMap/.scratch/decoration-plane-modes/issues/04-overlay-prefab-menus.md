# 04 — 覆盖物 Prefab 的创建菜单

**What to build:** 把「创建装饰物 Prefab」的工具扩到覆盖物：同一套构建器、同一套右键菜单、同样的 XY/XZ 两个平面，多出一个「带」维度。覆盖物的 `sortingOrder` 是 `100`，命名 `{SpriteName}_Overlay_{XY|XZ}`。

**Blocked by:** None — 01 / 02 / 03 已落地。

**Status:** complete

## 范围

- [x] `DecorationBand` + `DecorationBands`：带 → 队列 / 排序号的**唯一**映射，数字引自 `DecorationQueue`，放在 `HexMap.Editor`（运行时程序集零改动）
- [x] `DecorationPrefabBuilder` 加 `band` 维度：`BuildAssetName` / `CreateHierarchy` / `CreateAsset` 三个签名都加必填参数（不加重载 —— 默认成 Decoration 会让「忘了传带」静默编过），命名模板改为 `{sourceName}_{band}_{plane}`
- [x] `ConfigureBand`：经 `SerializedObject` 写 `m_Queue` / `m_SortingOrder`，并**排在 `Apply()` 之前**
- [x] 4 个新菜单项：`Assets/Create/HexMap/Overlay (XY)` / `(XZ)`、`HexMap/Create Overlay Prefab (XY)` / `(XZ)`，优先级 3 / 4；已提交的 4 条路径不变
- [x] 守卫测试扩成 band × plane 四格：从保存后的 Prefab 读回 `Queue` / `SortingOrder`、子节点四元数、两条带的排序号分居基线两侧、命名四格、未定义带被拒绝、两带只差数字
- [x] 文档：`decoration-rendering.md` §1 / §4.1 / §10、`spec.md` §3.1、`unity-render-order-rules.md` §3 补第二次实测

## 验收

- [x] band × plane 四格在活编辑器里通过（`HexMap.Editor.Tests.EditMode` 从 19 条增到 29 条）
- [x] 回归没有新增失败：`TOTAL=172 PASSED=172 FAILED=0 SKIPPED=0`，五个程序集全绿
- [x] `DecorationView` / `DecorationMeshFactory` / `DecorationMaterialCache` / `Decoration.shader` / 现有 Prefab / `map.unity` 零改动
- [ ] 八条菜单项在 UI 里点一遍 —— 与 issue 01 同一个空缺：`DecorationPrefabMenu` 读的是 `Selection` 与保存文件面板，测不了

## Comments

- 决策依据见 `../spec.md` 第 3.1 节；O1 / O2 之争的实测见同节末尾。
- 计划外的一处改动：`DecorationQueue.Overlay` 的 XML 注释原文声称「Overlays are drawn after the HexMap, so they sit on top of it」，把层级归因于队列号，与同文件第 9 行的类注释、ADR-0001 第 14 行直接矛盾。按第二次实测改写为「层级由 `OverlaySortingOrder` 承担，队列号负责材质标识」。这是 O3 测量留下的唯一改动。
- **刻意不覆盖的一条**：`ConfigureBand` 与 `Apply()` 的先后顺序。本 fixture 用无 Sprite 的骨架，且 `Apply()` 在无网格时会提前 return，所以在骨架上看不到这两个值的差别；而把顺序写反在成品上没有后果 —— 带存在序列化字段里，材质与 renderer 的排序号每次 enable 都会重新派生。故不为它引入 `Shader.Find` 依赖。
- 回归证据：`.scratch/unity-test-harness/results.xml`（该文件不入库）。`HexMap.Editor.Tests.EditMode` 29 条里 16 条属于 `DecorationPrefabBuilderTests`，含 `TheSavedPrefabCarriesItsBandsQueueAndSortingOrder(Decoration,XY)` 等四个 band × plane 组合 —— 它们读的是**保存后的 Prefab 资产**，所以「`SerializedObject` 写的值确实落进了文件」这件事是被证明的，不是被假设的。

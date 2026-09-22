# 05 — 创建菜单的幂等性

**What to build:** 右键创建 Decoration / Overlay 时，如果同目录下这张图的该「带 + 平面」Prefab 已经存在，就不要再生成一个。现在不检查，于是重复调用会堆出 `Foo_Decoration_XY 1.prefab`。

**Blocked by:** None。

**Status:** complete

## 范围

- [x] `DecorationPrefabBuilder.BuildAssetPath(folder, sourceName, band, plane)`：把「路径怎么拼」收成一个函数，存在性检查与创建必须看同一个路径
- [x] `DecorationPrefabBuilder.InspectTarget(assetPath, sprite)` + `DecorationPrefabTargetState { Empty, AlreadyCreated, Occupied }`
- [x] `DecorationPrefabMenu.CreateFromSelectedSprites`：占用即跳过，不再调用 `AssetDatabase.GenerateUniqueAssetPath`
- [x] 汇总日志（`created` / `already there` / `blocked`），占用时 `LogWarning` 说明是哪种占用
- [x] 单测：`BuildAssetPath` 拼接 1 条、三种状态共 4 条（`Empty` / `AlreadyCreated` / 两种 `Occupied`）
- [x] 文档：`decoration-rendering.md` §4.1 补幂等规则（并删掉两处已过时的「重名走 `GenerateUniqueAssetPath`」）

## 验收

- [x] 回归没有新增失败：`TOTAL=209 PASSED=209 FAILED=0`，`HexMap.Editor.Tests.EditMode` 61 → 66
- [ ] 对同一张图重复调用菜单，第二次不产生任何新文件（需要人在 UI 里点，菜单读 `Selection`，测不了）
- [ ] 已有 Prefab 上美术手改过的缩放 / 位置 / 排序号不被覆盖（同上）

## Comments

- 决策依据见 `../spec.md` 第 3.1 节。
- **为什么是跳过而不是覆盖**：建出来的 Prefab 是美术会手调（缩放、位置、`m_SortingOrder`）的资产，覆盖会把这些改动抹掉；菜单又会被同一个人在同一张图上反复调用。所以「什么都不做」才是正确语义。
- **路径被别的东西占用时也不改名**：不生成 `Foo_Decoration_XY 1.prefab` —— 用户报的正是这种冗余文件。代价是这种情况必须由人删掉或改名，所以用 `LogWarning` 明确说出来，而不是让它看起来像菜单坏了。
- 范围外：Sprite 资产被删掉重导后，旧 Prefab 会变成 `Occupied`（引用已断）。菜单只会告警，不会自愈 —— 自愈要么覆盖（否决）要么改 Sprite 引用（另一件事）。
- 骨架菜单不受影响：它走保存面板，覆盖与否由 Unity 管。
- 本票在 `cc59344`（保存时归一化容器）与 `7f0096b`（`HexMap.Editor` 源码加 `#if UNITY_EDITOR`）之上做的。新增代码放在既有的 `#if` 块内；本票没有新建 C# 文件，所以无需额外包裹。归一化器不会碰这类 Prefab：`DecorationNormalizer.NormalizePrefab` 只遍历**根的子物体**，注释里写明「A prefab whose own root carries a `DecorationView` *is* a decoration, not a collection of them」，所以菜单产出的 Prefab 在保存时是 no-op。

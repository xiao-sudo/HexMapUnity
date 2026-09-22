# 03 — 装饰物层序与几何的测试

**What to build:** 用两个 EditMode 接缝加一个 PlayMode 像素回读接缝，把"UV 直通"、"材质按队列隔离"、"装饰物被 HexMap 盖住且相机移动后不变"三件事变成可执行的断言。

**Blocked by:** 02 — 装饰物 Prefab 与自渲染组件。

**Status:** tests landed with 02 (`e7cc504`, review fixes in `d6c6813`); two items still open, listed below.

## 接缝一：`DecorationMeshFactory`（EditMode）

- [x] 先例：`HexCellMeshFactory` 是同形态、同程序集的静态几何工厂。实际断言方式未沿用 `HexMapViewTests`，而是各自独立构造 Sprite —— 因为它需要精确控制 UV 与 Pivot。
- [x] 断言：一个占据非 0~1 矩形、且 Pivot 不在中心的 Sprite，产出的顶点以 `Sprite.bounds.center` 为原点居中。
- [x] 断言：UV 与 `Sprite.uv` 逐项相等。
- [ ] ~~断言：顶点数为 4、索引数为 6。~~ **已作废**：`Tight` 导入的轮廓网格顶点数本就大于 4，写死 4/6 与「保留 Sprite 拓扑」的新约束直接冲突。改为断言拓扑被保留（见下一条）。
- [ ] **未做：故意退化实现确认测试真的红。** 计划中要求的这一步没有执行。风险具体是：「居中」与「UV 直通」两条断言在**程序化 Sprite** 上无法证伪退化实现 —— 因为 `Sprite.uv` 恒为单位方格、`Sprite.bounds.center` 也恒为可复现值，一个硬编码单位 Quad 会同时通过这两条。**所以这两条断言目前只记录契约，不具备证伪能力。**
- [x] **不引入真实 Sprite Atlas 资产做测试。** 只测几何学。已把「为什么测不到图集」写进 `DecorationGeometryTests` 的类文档：程序化 `Texture2D` 没有 `.meta`，无法进入 Sprite Atlas。
- [x] 断言外部数据（顶点 / UV / 索引），不绑定内部缓存结构或私有字段。
- [x] 断言：`Tight` 导入的轮廓网格被保留而不是被压成四边形。用真实导入管线（写 PNG + `TextureImporterSettings.spriteMeshType = Tight`）构造，因为 `Sprite.Create(..., SpriteMeshType.Tight)` 在运行时返回的仍是 4 顶点矩形。

## 接缝二：`DecorationMaterialCache`（EditMode）

- [x] 断言：同一纹理同一队列返回同一材质实例。
- [x] 断言：同一纹理不同队列返回互相独立的实例（这是装饰物不会改到覆盖物队列的保证）。
- [x] 断言：写入的队列值与请求一致。另断言材质带上纹理作为 `_BaseMap`。
- [x] 断言：清理后缓存为空 —— `ClearDropsBothCaches` 对 Mesh 缓存与材质缓存都断言。
- [x] 断言：纹理或 Shader 为空时返回 null 而不是抛异常，并各自报告一条错误日志（用 `LogAssert.Expect` 声明）。
- [x] 无直接先例；旧 `StaticDecorationRenderer` 的按纹理材质字典是半个前身。**注意**：该旧实现的 `ReleaseTexture` 曾被我照搬进来，但零调用点，已按「死代码」删除 —— 这条路径不需要再补测试。

## 接缝二之补充：`DecorationView` 的编辑期装配（EditMode）

- [ ] 断言：对一个「根挂 `DecorationView` + 子物体持 `MeshFilter`/`MeshRenderer`」的对象设好 Sprite 后，子物体的 `sharedMesh` 与 `sharedMaterial` 均非空。
- [ ] 这一条守的是**编辑模式可见**这个能力本身：它执行的是 `[ExecuteAlways]` → `OnEnable` → `Apply()` 这条链路，而几何接缝与 PlayMode 测试都不经过它。
- [ ] 尚未实现。`DecorationView` 目前只在 PlayMode 测试里被用到，且那些测试是运行时 `AddComponent`，不覆盖编辑期启用路径。

## 接缝二之补充二：生命周期钩子（PlayMode，尚未实现）

- [ ] 断言：一个**在场景里**（而非运行时 `AddComponent`）的 `DecorationView`，在进入 Play 之后 `MeshFilter.sharedMesh` 仍然有效。
- [ ] 这条守的是一类已经在实现中真实发生过的缺陷：`RuntimeInitializeOnLoadMethod` 的清理时机若晚于场景对象的 `OnEnable`，会把刚赋给 `MeshFilter` 的网格销毁，表现为「编辑模式正常、一运行就没引用」。
- [ ] 现有 PlayMode 测试抓不到它，因为它们在 `SetUp`/`TearDown` 里手动清缓存，`DecorationCacheLifetime` 整个类从未被执行。
- [ ] 需要仓库里有一个测试专用场景，用 `SceneManager.LoadScene` 加载。是否要做由人决定：这类缺陷在运行期肉眼可见，仓库当前没有测试专用场景资产。

## 接缝三：运行时层序与稳定性（PlayMode 像素回读）

- [x] 先例：被 01 删除的旧 PlayMode 装饰物渲染测试。**手法照搬**——256² ARGB32 RenderTexture、专用图层、正交相机看向 XY 平面、逐点回读像素并带容差比较。材质改为测试自建，因为旧测试拿的是贴在已删类型上的 shader 引用。
- [x] 断言：半透明格子叠在装饰物上得到混合色。
- [x] 断言：不透明格子完全盖住装饰物。
- [x] 断言：装饰物在相机平移与缩放后仍固定在世界空间。
- [ ] 部分完成：**隐藏后重新显示且不重建几何/材质**已断言（`IsReady` 仍为真）；**「初始可见为假时装饰物不出现」未断言** —— 现有用例走的是运行时 `m_Visible` 切换，没有覆盖 Prefab 上 `m_Visible == false` 的初始状态。这一条可以补，成本很低。
- [ ] 部分完成：同一 Sprite 在装饰物队列与覆盖物队列上的**层序**未在 PlayMode 断言。已有的是「覆盖物队列的不透明方块盖住不透明 Hex」，以及 EditMode 里的材质实例隔离。缺的是「两个实例同屏、改其一不影响另一个」。
- [x] 拆除时清理材质缓存与 Mesh 缓存。

## Comments

### 一条断言比没有断言更危险的情形

本票的接缝一里，「居中」与「UV 直通」两条断言**在程序化 Sprite 上无法证伪退化实现**。原因是程序化 Sprite 的 `Sprite.uv` 恒为单位方格、`Sprite.bounds.center` 恒为可复现值，所以一个硬编码的 0~1 单位 Quad 会让这两条**同时通过**。

它们的真实价值因此是「记录契约」而不是「守卫」：将来有人改 `DecorationMeshFactory` 时会看到这两条要求，但它们不会在他改错时报红。**唯一能证伪退化的场景是图集**，而图集需要真实资产，本票明确不做。

知道这一点的意义是：不要把这两条绿色当作「UV 直通已被验证」。它没有。

### 同一类失误在本特性里出现过三次

三次都是「自己引入的东西，自己的测试测不到」：

1. `DecorationCacheLifetime` 的清理时机（票面未要求我加的类，测试手动清缓存因而绕过了它）—— 表现为「编辑模式正常、一运行 MeshFilter 没引用」。
2. `Queue` 的公开 setter（票面明文禁止的入口，而 PlayMode 测试**正在使用它**）—— 测试依赖了不该存在的 API，因而对该 API 的合理性完全失明。
3. 接缝一的`顶点数 == 4`与票面后来修改的「保留 Sprite 拓扑」互相矛盾，而写死顶点数的测试无法发现自己的前提已经作废。

第 2 条是 ticket 01 里「测试与实现同构、退化了也不会红」的另一个面：**测试依赖了不该存在的接口时，它对这个接口就是失明的。**

## 手动验证（不是自动化测试，但必须执行）

- [ ] 在真实生产场景的实例化绘制路径下，用 Frame Debugger 确认装饰物没有被绘制到 HexMap 之前。
- [ ] 确认不同的 `sortingOrder` 没有把 SRP Batch 切碎（`docs/reference/unity-render-order-rules.md` 中"会切出额外批次"的说法需要被证实或证伪）。
- [ ] 确认装饰物 Shader 在 Inspector 中显示 SRP Batcher 兼容。
- [ ] 图集验证：把一个打包进 Sprite Atlas 的 Sprite 放到装饰物上，确认渲染的是该 Sprite 本身而不是整张图集的一角。这是接缝一唯一无法覆盖的场景。

### 为什么装饰物几何进不了 Tier 1

`docs/agents/testing.md` 规定 Tier 1 的 `dotnet test` harness 不得引用 `UnityEngine.dll`。装饰物几何必然穿过 `UnityEngine`（`Sprite.vertices` / `Sprite.uv` / `Sprite.bounds` → `Mesh`），所以它进不了 Tier 1。既有的 `HexCellMeshFactory` 是同一情形，同样留在 Tier 2。**不要为它搭 Tier 1 harness。**

### 什么算好测试

只断言外部可观测的行为：屏幕像素、Mesh 的顶点/UV/索引数据、材质实例同一性、渲染器开关状态。不绑定内部缓存结构、私有字段、Mesh 或材质的命名。一个测试如果为了实现方便而必须暴露私有成员，就应该换个层级断言。

### 三条手动验证为什么单列

它们依赖 Frame Debugger 或真实资产，无法断言。混进自动化测试会让人误以为 CI 会替他们跑。它们是交付前置，不是测试。

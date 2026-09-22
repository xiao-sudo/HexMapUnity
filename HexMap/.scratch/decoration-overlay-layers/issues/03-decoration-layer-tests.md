# 03 — 装饰物层序与几何的测试

**What to build:** 用两个 EditMode 接缝加一个 PlayMode 像素回读接缝，把"UV 直通"、"材质按队列隔离"、"装饰物被 HexMap 盖住且相机移动后不变"三件事变成可执行的断言。

**Blocked by:** 02 — 装饰物 Prefab 与自渲染组件。

**Status:** implemented. Manual Frame Debugger and atlas checks remain, listed at the end.

## 接缝一：`DecorationMeshFactory`（EditMode）

- [x] 先例：`HexCellMeshFactory` 是同形态、同程序集的静态几何工厂。实际断言方式未沿用 `HexMapViewTests`，而是各自独立构造 Sprite —— 因为它需要精确控制 UV 与 Pivot。
- [x] 断言：一个占据非 0~1 矩形、且 Pivot 不在中心的 Sprite，产出的顶点以 `Sprite.bounds.center` 为原点居中；**并断言顶点跨度等于 `Sprite.bounds.size`**。
- [x] 断言：UV 与 `Sprite.uv` 逐项相等。
- [x] ~~断言：顶点数为 4、索引数为 6。~~ **已作废**：`Tight` 导入的轮廓网格顶点数本就大于 4。改为断言拓扑被保留。
- [x] **已解决「断言无法证伪退化实现」的问题。** 原判断是「程序化 Sprite 的 UV 恒为单位方格，故退化 Quad 也能通过」。**这个判断是错的**：`Sprite.Create` 的 `rect` 可以只覆盖纹理的一块子区域，此时 `Sprite.uv` 只寻址该角。测试现在就用这种子矩形 Sprite（`rect = (1,1,2,2)`，纹理 4×4），并先断言该 Sprite 的 UV 跨度确实小于整张纹理，否则测试自身无效。**退化实现（硬编码 0~1 UV）会因 UV 跨度断言而失败。**
- [x] **不引入真实 Sprite Atlas 资产做测试。** 但已用子矩形 Sprite 复现了图集的**几何情形**（UV 只覆盖纹理一角）。仍未覆盖的是**打包这一步本身**，留作人工验证。
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

- [x] 断言：对一个「根挂 `DecorationView` + 子物体持 `MeshFilter`/`MeshRenderer`」的对象设好 Sprite 后，**启用组件**会让子物体的 `sharedMesh` 与 `sharedMaterial` 均非空。见 `DecorationViewEditModeTests`。
- [x] 这一条守的是**编辑模式可见**这个能力本身：它执行的是 `[ExecuteAlways]` → `OnEnable` → `Apply()` 这条链路。测试为此**先禁用组件、写好序列化字段、再启用**，否则 `AddComponent` 会在字段就位前就调用 `OnEnable`，链路根本没被真正检验。
- [x] 另断言装配结果：`renderer.enabled`、`sortingOrder`、阴影/探针/运动矢量全关、材质 `renderQueue == DecorationQueue.Decoration`。
- [x] 另断言：`m_Visible == false` 的装饰物**不画但仍被装配**（否则日后显示它就得重建）。这条同时覆盖了 PlayMode 侧的初始隐藏场景，但 PlayMode 侧另有像素断言。

## 接缝二之补充二：生命周期钩子（PlayMode，**决定不做**）

- [ ] **不做，理由如下。** 需求是「一个在场景里的 `DecorationView`，进入 Play 后 `MeshFilter.sharedMesh` 仍有效」。
- [ ] 需要仓库新增一个测试专用场景、用 `SceneManager.LoadScene` 加载。仓库当前没有测试专用场景资产。
- [ ] 该缺陷类型（`RuntimeInitializeOnLoadMethod` 清理时机晚于场景对象 `OnEnable`）已在实现中真实发生过一次并修复，且**在运行期肉眼可见** —— 进 Play 即见。
- [ ] 决策：由人判定不值得为它引入场景资产与配套维护成本。**因此 `DecorationCacheLifetime` 仍然没有任何自动化覆盖**，这是本特性已知的测试空缺。

## 接缝三：运行时层序与稳定性（PlayMode 像素回读）

- [x] 先例：被 01 删除的旧 PlayMode 装饰物渲染测试。**手法照搬**——256² ARGB32 RenderTexture、专用图层、正交相机看向 XY 平面、逐点回读像素并带容差比较。材质改为测试自建，因为旧测试拿的是贴在已删类型上的 shader 引用。
- [x] 断言：半透明格子叠在装饰物上得到混合色。
- [x] 断言：不透明格子完全盖住装饰物。
- [x] 断言：装饰物在相机平移与缩放后仍固定在世界空间。
- [x] 部分完成：**隐藏后重新显示且不重建几何/材质**已断言（`IsReady` 仍为真）。
- [x] **「初始可见为假时装饰物不出现」已断言** —— 新增 `ADecorationAuthoredHiddenNeverDraws`，像素回读确认不画，并确认仍被装配。
- [x] **同一 Sprite 双队列互不影响已断言** —— 新增 `TwoQueuesFromOneSpriteDoNotAffectEachOther`：同一 Sprite 派生两个材质实例、同屏各画一处、隐藏其一不影响另一个的像素。
- [x] 拆除时清理材质缓存与 Mesh 缓存。

## Comments

### 一条断言比没有断言更危险的情形 —— 已解决

接缝一里「居中」与「UV 直通」两条断言起初**在程序化 Sprite 上无法证伪退化实现**，依据是当时认为 `Sprite.uv` 恒为单位方格。**那个前提是错的**：`Sprite.Create` 的 `rect` 可以只覆盖纹理的一块子区域。

现在测试用 `rect = (1,1,2,2)` 对 4×4 纹理构造子矩形 Sprite，其 UV 只寻址纹理一角。两条断言因此都能失败于退化实现：

- **UV**：测试先断言该 Sprite 的 UV 跨度小于整张纹理（否则 fixture 无区分力），再逐项比对 `Sprite.uv`。
- **居中**：除了「平均值为 0」，另断言顶点跨度等于 `Sprite.bounds.size`。这条是必要的 —— 用「纹理四角」构造的退化 Quad 居中后平均值同样是 0，但跨度会是整张纹理，只有跨度能识破它。

**退化验证仍未实机执行。** 以上是逐条推演：每条断言各有一个会使它失败的退化实现，且测试自带守卫断言防止 fixture 自己退化为无区分力。但「真的红一次」没有跑过。

### 同一类失误在本特性里出现过三次

三次都是「自己引入的东西，自己的测试测不到」：

1. `DecorationCacheLifetime` 的清理时机（票面未要求我加的类，测试手动清缓存因而绕过了它）—— 表现为「编辑模式正常、一运行 MeshFilter 没引用」。
2. `Queue` 的公开 setter（票面明文禁止的入口，而 PlayMode 测试**正在使用它**）—— 测试依赖了不该存在的 API，因而对该 API 的合理性完全失明。
3. 接缝一的`顶点数 == 4`与票面后来修改的「保留 Sprite 拓扑」互相矛盾，而写死顶点数的测试无法发现自己的前提已经作废。

第 2 条是 ticket 01 里「测试与实现同构、退化了也不会红」的另一个面：**测试依赖了不该存在的接口时，它对这个接口就是失明的。**

## 手动验证（不是自动化测试，但必须执行）

- [x] 在真实生产场景的实例化绘制路径下，用 Frame Debugger 确认装饰物没有被绘制到 HexMap 之前。**已由人确认。**
- [x] ~~确认不同的 `sortingOrder` 没有把 SRP Batch 切碎。~~ **已实测，但结论比问题本身重要**：`sortingOrder` 不切碎 SRP Batch，然而它的优先级**高于** `renderQueue` —— 给装饰物（2800）设 `sortingOrder = 1` 会让它盖住 HexMap（3000）。因此 `DecorationView` 的 `SortingOrder` 字段已删除，`docs/reference/unity-render-order-rules.md` 中「`sortingLayer → renderQueue → sortingOrder`」的推理已被实测证伪并更正为「`sortingLayer → sortingOrder → renderQueue`」。
- [x] 确认装饰物 Shader 在 Inspector 中显示 SRP Batcher 兼容。**已由人确认。**
- [x] 图集验证：工程使用 Atlas V2，Frame Debugger 上确认已用图集渲染且效果正确。**已由人确认。**

### 为什么装饰物几何进不了 Tier 1

`docs/agents/testing.md` 规定 Tier 1 的 `dotnet test` harness 不得引用 `UnityEngine.dll`。装饰物几何必然穿过 `UnityEngine`（`Sprite.vertices` / `Sprite.uv` / `Sprite.bounds` → `Mesh`），所以它进不了 Tier 1。既有的 `HexCellMeshFactory` 是同一情形，同样留在 Tier 2。**不要为它搭 Tier 1 harness。**

### 什么算好测试

只断言外部可观测的行为：屏幕像素、Mesh 的顶点/UV/索引数据、材质实例同一性、渲染器开关状态。不绑定内部缓存结构、私有字段、Mesh 或材质的命名。一个测试如果为了实现方便而必须暴露私有成员，就应该换个层级断言。

### 三条手动验证为什么单列

它们依赖 Frame Debugger 或真实资产，无法断言。混进自动化测试会让人误以为 CI 会替他们跑。它们是交付前置，不是测试。

# 03 — 装饰物层序与几何的测试

**What to build:** 用两个 EditMode 接缝加一个 PlayMode 像素回读接缝，把"UV 直通"、"材质按队列隔离"、"装饰物被 HexMap 盖住且相机移动后不变"三件事变成可执行的断言。

**Blocked by:** 02 — 装饰物 Prefab 与自渲染组件。

**Status:** ready-for-agent

## 接缝一：`DecorationMeshFactory`（EditMode）

- [ ] 先例：`HexCellMeshFactory` 是同形态、同程序集的静态几何工厂；`HexMapViewTests` 是它现有的断言方式。
- [ ] 断言：一个占据非 0~1 矩形、且 Pivot 不在中心的 Sprite，产出的顶点以 `Sprite.bounds.center` 为原点居中。
- [ ] 断言：UV 与 `Sprite.uv` 逐项相等。
- [ ] 断言：顶点数为 4、索引数为 6。
- [ ] 构造要点是让"居中"与"UV 直通"二者可被区分——如果实现退化成通用单位 Quad，居中与 UV 两条断言都必须失败。**写完后要故意退化一次实现，确认测试真的红。**
- [ ] **不引入真实 Sprite Atlas 资产做测试。** 程序化 `Texture2D` 没有 `.meta`，无法进入 Sprite Atlas。只测几何学；图集行为由 Unity 保证，前提是实现直通 `Sprite.uv`。
- [ ] 断言外部数据（顶点 / UV / 索引），不绑定内部缓存结构或私有字段。

## 接缝二：`DecorationMaterialCache`（EditMode）

- [ ] 断言：同一纹理同一队列返回同一材质实例。
- [ ] 断言：同一纹理不同队列返回互相独立的实例（这是装饰物不会改到覆盖物队列的保证）。
- [ ] 断言：写入的队列值与请求一致。
- [ ] 断言：清理后缓存为空。
- [ ] 断言：纹理或 Shader 为空时返回 null 而不是抛异常，并各自报告一条错误日志（用 `LogAssert.Expect` 声明）。
- [ ] 无直接先例；旧 `StaticDecorationRenderer` 的按纹理材质字典是半个前身。

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

- [ ] 先例：被 01 删除的旧 PlayMode 装饰物渲染测试。**手法照搬**——256² ARGB32 RenderTexture、专用图层、正交相机看向 XY 平面、逐点回读像素并带容差比较。
- [ ] 断言：半透明格子叠在装饰物上得到混合色。
- [ ] 断言：不透明格子完全盖住装饰物。
- [ ] 断言：装饰物在相机平移与缩放后仍固定在世界空间。
- [ ] 断言：组件初始可见为假时装饰物不出现；隐藏后重新显示，重叠关系与其它装饰物不受影响。
- [ ] 断言：同一个 Sprite 分别以装饰物队列与覆盖物队列各放一个实例，改变其一不影响另一个的层序。
- [ ] 拆除时清理材质缓存与 Mesh 缓存。

## 手动验证（不是自动化测试，但必须执行）

- [ ] 在真实生产场景的实例化绘制路径下，用 Frame Debugger 确认装饰物没有被绘制到 HexMap 之前。
- [ ] 确认不同的 `sortingOrder` 没有把 SRP Batch 切碎（`docs/reference/unity-render-order-rules.md` 中"会切出额外批次"的说法需要被证实或证伪）。
- [ ] 确认装饰物 Shader 在 Inspector 中显示 SRP Batcher 兼容。

## Comments

### 为什么装饰物几何进不了 Tier 1

`docs/agents/testing.md` 规定 Tier 1 的 `dotnet test` harness 不得引用 `UnityEngine.dll`。装饰物几何必然穿过 `UnityEngine`（`Sprite.vertices` / `Sprite.uv` / `Sprite.bounds` → `Mesh`），所以它进不了 Tier 1。既有的 `HexCellMeshFactory` 是同一情形，同样留在 Tier 2。**不要为它搭 Tier 1 harness。**

### 什么算好测试

只断言外部可观测的行为：屏幕像素、Mesh 的顶点/UV/索引数据、材质实例同一性、渲染器开关状态。不绑定内部缓存结构、私有字段、Mesh 或材质的命名。一个测试如果为了实现方便而必须暴露私有成员，就应该换个层级断言。

### 三条手动验证为什么单列

它们依赖 Frame Debugger，无法断言。混进自动化测试会让人误以为 CI 会替他们跑。它们是交付前置，不是测试。

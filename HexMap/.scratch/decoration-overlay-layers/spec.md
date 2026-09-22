# 装饰物与覆盖物作为 HexMap 上下两层的渲染方案

**Status:** ready-for-agent

## Problem Statement

地图上需要摆放装饰物，并且它们必须被 HexMap 盖住；后续还要有一层覆盖物浮在 HexMap 之上表达格子状态。当前工程里既没有一个能把「装饰物、HexMap、覆盖物」三层稳定定序的契约，也没有任何装饰物 Prefab——旧的一套装饰物实现（`StaticDecoration*` 系列）已经作废，而它内部硬编码的队列号、被注释掉的 `sortingOrder`、以及一个自相矛盾的测试，让"谁盖住谁"这件事在代码里既不可读也不可验证。

具体到使用层面：

- 美术没有任何可以放进场景、拖一下就看见真实效果的装饰物资产。摆放装饰物意味着改一段序列化的数组数据并等运行时生成。
- 装饰物与 HexMap 都在透明队列、都关深度写入、都做 alpha 混合，所以"层级"不能靠几何高度或深度缓冲表达。这个约束不明显，很容易被后来人用深度偏移或改渲染管线的方式"修好"，从而破坏 HexMap 的三半透明格子与渐变表现。
- 覆盖物要浮在 HexMap 之上，但它和装饰物在观感上完全一致，区别只有队列号——如果没人把这件事写下来，它一定会被实现成第二套 Shader 和第二套组件。

## Solution

提供一套「一个装饰物一个 Prefab」的装饰物渲染能力，并把三层定序固化成一份不可轻易偏离的契约：

- 装饰物、HexMap、覆盖物三层用透明队列（Transparent 3000）的三个子区间定序：装饰物 2800、HexMap 3000、覆盖物 3005。三个数字集中在一个常量类里，代码里只有这一处来源。
- 每个装饰物是一个自渲染的 Prefab：根挂 `DecorationView` 组件，子物体持 `MeshFilter` + `MeshRenderer`。组件在编辑期就把几何建好，所以拖进场景、不进 Play 就能看到真实效果。
- 一装饰一纹理一材质，**不共享材质**，Draw Call 不作为指标；只承诺新增 SetPass Call 为 1~2。
- 覆盖物复用同一个组件和同一套 Shader，只是队列值不同。加一层覆盖物不需要写任何新渲染代码。
- 旧的 `StaticDecoration*` 实现、demo 场景与材质全部清除，避免两套并存的装饰物路径。

## User Stories

1. 作为美术，我想要把一个装饰物 Prefab 拖进场景就看到真实贴图效果，这样我不用进 Play 就能确认摆放是否正确。
2. 作为美术，我想要每个装饰物类型是独立的一个 Prefab，这样改一个装饰物不会影响场景里其它装饰物。
3. 作为美术，我想要只换 Prefab 上的 Sprite 引用就完成换图，这样我不需要碰材质、不需要手动建 Mesh。
4. 作为美术，我想要装饰物在编辑期就渲染出来，而不是只在运行时出现，这样我能在场景视图里直接构图。
5. 作为美术，我想要装饰物的显示尺寸遵循 Sprite 自身的 Pixels Per Unit 与 Pivot，再叠加 Transform，这样我不用为了对齐去手工试数字。
6. 作为美术，我想要装饰物的朝向完全由它的 Transform 决定，这样我可以自由摆放倾斜角而不受组件干预。
7. 作为美术，我想要在同一个队列内用 `sortingOrder` 微调前后关系，这样我不用为了调顺序去改队列号。
8. 作为美术，我想要把同一个装饰物 Prefab 复制多份，这样重复摆放不需要重复配置。
9. 作为美术，我想要把装饰物 Prefab 的队列值改成覆盖物的值，就得到一层浮在 HexMap 之上的覆盖物，这样我不需要另一套组件或另一套 Shader。
10. 作为美术，我想要图集重新打包后场景里的装饰物自动跟随，这样我不需要回头改任何 Prefab。
11. 作为美术，我想要图集侧有明确的打包约束（Padding、不用 Mipmap），这样相邻贴图在缩小时不会串色。
12. 作为客户端工程师，我想要三个队列号集中定义在唯一一处，这样调整层级时不会漏改。
13. 作为客户端工程师，我想要装饰物与覆盖物共用同一套 Shader，只以材质上的 `renderQueue` 区分，这样加覆盖物不引入新的 Shader 变体。
14. 作为客户端工程师，我想要装饰物的材质由纹理与队列号派生、不在仓库里留下材质资产，这样版本控制里没有 100 个没人会调的 `.mat` 文件。
15. 作为客户端工程师，我想要 Shader 引用可以在 Prefab 上显式指定、为空时回退查找，这样打包后 Shader 未被引用而被剥离时不会静默变成不可见。
16. 作为客户端工程师，我想要 Shader 丢失时记录错误并禁用渲染器而不是抛异常，这样单个错配不会让整个场景起不来。
17. 作为客户端工程师，我想要显隐切换只动渲染器开关、不重建几何、不改变排序位置，这样运行时开关装饰物是零成本的。
18. 作为客户端工程师，我想要装饰物与 Cell 的玩法语义完全独立，这样加装饰物不会污染地图数据。
19. 作为性能工程师，我想要装饰物的新增 SetPass Call 为 1~2，这样我不用为每个装饰物付一次渲染状态切换。
20. 作为性能工程师，我想要明确的书面记录说明 Draw Call 等于纹理数且不作为验收指标，这样我不会为追这个数字推翻架构。
21. 作为性能工程师，我想要知道材质数等于纹理数乘以实际用到的队列数，这样我能预判 SRP Batcher 常驻常量缓冲的内存规模。
22. 作为性能工程师，我想要在真实生产场景里验证装饰物没有插到 HexMap 之前，这样我能确认生产用的实例化绘制路径也遵守同一套层序。
23. 作为 QA，我想要用像素回读验证"装饰物被 HexMap 盖住"，这样层序不是靠阅读代码来确认的。
24. 作为 QA，我想要验证相机平移与缩放后层序与装饰物位置都不变，这样动态视角下的表现也被覆盖。
25. 作为 QA，我想要验证同一个 Sprite 在不同队列上产出互相独立的材质，这样装饰物不会意外改到覆盖物的队列。
26. 作为架构师，我想要这个决策以 ADR 形式固化并带明确后果，这样后来人不会把它当成"顺手写的数字"改掉。
27. 作为架构师，我想要 `CONTEXT.md` 里"装饰物"与"覆盖物"有明确术语与边界，这样文档与代码用同一套词。
28. 作为架构师，我想要旧实现被彻底清除而不是并存，这样不存在两条装饰物渲染路径。

## Implementation Decisions

**模块与所在程序集**

- 全部新类型落在 `HexMap.UnityRuntime`，与既有的 `HexCellMeshFactory` 同层。`HexMap.Runtime` 被 `docs/implementation/hex-map-view-rendering.md` 禁止引用 `MonoBehaviour` / `GameObject`，所以组件不可能放在那里。
- 新增四个类型：`DecorationQueue`（常量）、`DecorationView`（MonoBehaviour 组件）、`DecorationMeshFactory`（几何）、`DecorationMaterialCache`（材质）。

**队列契约**

- `DecorationQueue` 拥有三个数字：装饰物 2800、HexMap 3000、覆盖物 3005。它只放常量，不引用任何 Unity 类型。
- HexMap 的 3000 由 `InstancedHexCell.shader` 的 `Queue` tag 拥有，C# 侧只做断言，不作为来源。
- `sortingLayer` 一律留 `Default`（工程只有这一个 sorting layer）；队列内顺序只用 `sortingOrder`。
- 分层只靠 `renderQueue` 子区间，**不得**引入深度偏移、几何高度分层或渲染管线改动。

**Prefab 形态与组件字段**

- 结构：根 GameObject 挂 `DecorationView`；子 GameObject 持 `MeshFilter` + `MeshRenderer`。
- `DecorationView` 序列化字段：`Sprite` 引用、`int` 队列（默认取 `DecorationQueue` 中装饰物的值）、`int` 排序顺序（默认 0）、`bool` 初始可见、`MeshFilter` 子物体引用、可空的 `Shader` 引用。
- **不包含颜色字段。** 图什么样就渲染成什么样。

**几何**

- `DecorationMeshFactory` 从 Sprite 产出单个 Quad：顶点取 `Sprite.vertices`、三角形取 `Sprite.triangles`、UV 取 `Sprite.uv`，以 `Sprite.bounds.center` 居中。
- **UV 直通是硬约束。** 正因为直通 `Sprite.uv`，Sprite Atlas 的重映射、Trim、自定义 Pivot 三者自动成立，实现里不需要任何判断分支。**绝不能用顶点与 UV 都硬编码 0~1 的单位 Quad**——那会采样到整张图集页。
- 分辨率上限：一个 Sprite 一个 Quad，不做 tight mesh 或自定义多顶点轮廓。

**Mesh 生命周期（编辑期与运行时的缓存分裂）**

- 编辑期：组件在 `[ExecuteAlways]` 下构建 Mesh 并赋给子物体的 `MeshFilter`，Mesh 标记 `HideFlags.DontSave`，以便编辑期可见且不被写进 Prefab 或场景文件。**只在 Sprite 引用变化时重建**，不在每帧执行。
- 运行时：Mesh 按 `Sprite` 做静态缓存共享，与 `HexCellMeshFactory` 的共享 Mesh 用法一致；进入运行时后销毁编辑期的那份实例 Mesh。
- 缓存必须提供显式清理入口，供域重载与测试拆除使用。
- 显隐只切渲染器开关；隐藏后重新显示不重建几何、不改变排序位置。隐藏的装饰物保留全部配置。

**材质**

- `DecorationMaterialCache` 在运行时按 `(Texture2D, 队列值)` 派生材质，场景里不存在材质资产。**键必须包含队列值**：只按纹理缓存会让装饰物与覆盖物互相覆盖对方的队列。
- 队列值在材质创建时写入。**组件不提供任何会在运行时逐帧改队列值的入口**，并在注释中写明"改队列值会创建新的常驻材质"。
- 材质派生自 Shader；渲染器配置沿用旧实现的做法（关闭阴影、接收阴影、光照探针、反射探针、运动矢量）。

**Shader**

- `StaticDecoration.shader` 改名为 `Decoration.shader`，内部 Shader 名同步改为 `HexMap/Decoration`，**内容不改**（保留 `CBUFFER_START(UnityPerMaterial)` 与全部 Pass 设置）。
- fragment 中乘以 `input.color` 的算式保持原样：装饰物几何没有顶点色通道，该值恒为白，等价于无操作。
- Shader 查找两段式：优先组件上的序列化引用，为空则回退按名查找。这是为了防止打包时未被引用的 Shader 被剥离导致运行时取到空。
- Shader 为空时记录错误并禁用渲染器，**不抛异常**。

**清除范围**

- 删除：`StaticDecorationRenderer`、`StaticDecorationPlacement`、`StaticDecorationSpritePlacement`、`StaticDecorationDemo`、装饰物 demo 菜单、`decorate.unity`、`Decoration.mat`、旧 EditMode 装饰物测试、旧 PlayMode 装饰物渲染测试。
- 旧 PlayMode 测试的**像素回读手法**照搬，断言改绑到新组件。
- 不手动创建或修改任何 `.meta` 文件。

**美术侧约束（不属于代码范围但要记录）**

- 图集需要 Padding（建议 4~8 px）并关闭 Mipmap；相邻 Sprite 紧贴叠加 mipmap 会在缩小时串色。这是图集导入设置，代码无法兜住。

## Testing Decisions

**什么算好测试。** 只断言外部可观测的行为：屏幕像素、Mesh 的顶点/UV/索引数据、材质实例同一性、渲染器开关状态。不绑定内部缓存结构、私有字段、Mesh 或材质的命名。一个测试如果为了实现方便而必须暴露私有成员，就应该换个层级断言。

**为什么不进 Tier 1。** `docs/agents/testing.md` 规定 Tier 1 的 `dotnet test` harness 不得引用 `UnityEngine.dll`。装饰物几何必然穿过 `UnityEngine`（`Sprite.vertices` / `Sprite.uv` / `Sprite.bounds` → `Mesh`），所以它进不了 Tier 1——既有的 `HexCellMeshFactory` 是同一情形，同样留在 Tier 2。不要为它搭 Tier 1 harness。

**接缝一：`DecorationMeshFactory`（Tier 2 EditMode 为主）**

- 先例：`HexCellMeshFactory` 是同一形态、同一程序集的静态几何工厂；`HexMapViewTests` 是它现有的断言方式。
- 断言：一个占据非 0~1 矩形、且 Pivot 不在中心的 Sprite，产出的顶点以 `Sprite.bounds.center` 为原点居中；UV 与 `Sprite.uv` 逐项相等；顶点数为 4、索引数为 6。
- 这个用例的构造要点是让"居中"与"UV 直通"二者可被区分——如果实现退化成通用单位 Quad，居中与 UV 两条断言都会失败。
- 图集下的 UV 重映射**不引入真实 Sprite Atlas 资产做测试**：程序化 `Texture2D` 没有 `.meta`，无法进入 Sprite Atlas。只测几何学。图集行为由 Unity 保证，前提是实现直通 `Sprite.uv`。

**接缝二：`DecorationMaterialCache`（Tier 2 EditMode）**

- 断言：同一纹理同一队列返回同一材质实例；同一纹理不同队列返回互相独立的实例；清理后缓存为空；写入的队列值与请求一致。
- 无直接先例；旧 `StaticDecorationRenderer` 的按纹理材质字典是半个前身。

**接缝三：运行时层序与稳定性（Tier 2 PlayMode，像素回读）**

- 先例：被替换的 `StaticDecorationRenderingTests`，手法照搬——256² ARGB32 RenderTexture、专用图层、正交相机看向 XY 平面、逐点回读像素并带容差比较。
- 保持的断言：半透明格子叠在装饰物上得到混合色；不透明格子完全盖住装饰物；装饰物在相机平移与缩放后仍固定在世界空间。
- 新增的断言：组件初始可见为假时装饰物不出现；隐藏后重新显示，重叠关系与其它装饰物不受影响。
- 新增的断言：同一个 Sprite 分别以装饰物队列与覆盖物队列各放一个实例，改变其一不影响另一个的层序。
- 所有测试在拆除时清理材质缓存与 Mesh 缓存。

**手动验证（不是自动化测试，但必须执行）**

这三项依赖 Frame Debugger，无法断言，列为交付前置：

1. 在真实生产场景的实例化绘制路径下，确认装饰物没有被绘制到 HexMap 之前。
2. 确认不同的 `sortingOrder` 没有把 SRP Batch 切碎（现有 `docs/reference/unity-render-order-rules.md` 中"会切出额外批次"的说法需要被证实或证伪）。
3. 确认装饰物 Shader 在 Inspector 中显示 SRP Batcher 兼容。

## Out of Scope

- 装饰物 Prefab 的批量生成工具。第一版由人工创建 Prefab。
- 装饰物之间的正确互相遮挡。同队列内只保证不破坏与 HexMap 的相对关系，前后关系靠 `sortingOrder` 微调。
- Billboard。朝向完全交给 Transform，运行时不做变换更新。
- 共享材质、纹理图集或纹理数组等降低 Draw Call 的方案。
- 覆盖物的具体业务形态、数据来源与交互逻辑。本方案只保证它能浮在 HexMap 之上且不需要新 Shader。
- 微信小游戏真机性能验收。
- 渲染管线改动、自定义 RendererFeature、深度预pass。
- 图集本身的组织与打包规则。
- 队列值的运行时逐帧修改。

## Further Notes

- 本方案由 `docs/adr/0001-decoration-overlay-render-layers.md` 记录，Status `accepted`。该 ADR 里已写下四条被否决的替代路线（深度缓冲分层、纹理数组或图集共享材质、覆盖物加 Shader 关键字、手写逐纹理材质资产）及各自的否决理由。改动本方案前先读它。
- `CONTEXT.md` 已补充"装饰物"与"覆盖物"两个术语，并把"装饰物与覆盖物是纯表现、与 Cell 存在性及玩法状态独立"写入领域边界。
- 旧实现里的死代码（被注释掉的 `sortingOrder` 赋值、从未被调用的排序方法、与被注释代码相矛盾的测试断言）随旧实现一并作废，不需要单独处理。
- HexMap 侧的内圆透明与渐变表现（`_InteriorAlpha`、`_GradientEnabled`）必须保留。它是"不能用深度或不透明几何分层"的根本原因。
- 装饰物 Shader 的 `DisableBatching` 标签只关闭 legacy Dynamic Batching，工程本身也已关闭动态合批，所以它对本方案没有影响；保留它是为了避免在本次改动中夹带无关修改。
- 本方案的关键未知量是性能：SetPass 承诺为 1~2，但 Draw Call 等于纹理数（按 50 以内规划），材质数等于纹理数乘实际用到的队列数。这些数字在微信 WebGL 2.0 上尚未实测。

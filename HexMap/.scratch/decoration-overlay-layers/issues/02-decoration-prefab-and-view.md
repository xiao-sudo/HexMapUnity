# 02 — 装饰物 Prefab 与自渲染组件

**What to build:** 一个「一装饰物一 Prefab」的装饰物渲染能力。拖进场景、不进 Play 就能看到真实贴图效果；换图只需改 Sprite 引用；材质由纹理与队列号派生，仓库里不留材质资产。

**Blocked by:** 01 — 队列契约与新 Shader 名，清除旧装饰物实现。

**Status:** ready-for-agent

- [ ] `DecorationMeshFactory`：从 Sprite 产出网格。顶点取 `Sprite.vertices`、三角形取 `Sprite.triangles`、UV 取 `Sprite.uv`，以 `Sprite.bounds.center` 居中。与既有 `HexCellMeshFactory` 同形态、同程序集。
- [ ] **UV 直通是硬约束。** 正因直通 `Sprite.uv`，Sprite Atlas 的重映射、Trim、自定义 Pivot 三者自动成立，实现里不需要任何判断分支。绝不使用顶点与 UV 都硬编码 0~1 的单位 Quad——那会采样到整张图集页。
- [ ] 保留 Sprite 报告的网格拓扑：`Full Rect` 导入得到一个 Quad，`Tight` 导入得到 Unity 为不透明区域生成的轮廓网格。不按顶点数做任何假设，也不要求渲染器必须是四边形。
- [ ] `Tight` 是**支持**的导入方式。约束放宽为「任意合法网格拓扑」——顶点数不限，只拒绝无几何、UV 数与顶点数不匹配、索引数非 3 的倍数、以及索引越界。注意轮廓网格只能由导入管线生成，`Sprite.Create(..., SpriteMeshType.Tight)` 在运行时返回的仍是 4 顶点矩形。
- [ ] `DecorationMaterialCache`：按 `(Texture2D, 队列值)` 派生材质，场景里不存在材质资产。**键必须包含队列值**——只按纹理缓存会让装饰物与覆盖物互相覆盖对方的队列。
- [ ] 队列值在材质创建时写入。组件不提供任何会在运行时逐帧改队列值的入口，并在注释写明"改队列值会创建新的常驻材质"。
- [ ] 两个缓存都必须提供显式清理入口，供域重载与测试拆除使用。
- [ ] `DecorationView` 组件，序列化字段：`Sprite` 引用、`int` 队列（默认取 `DecorationQueue` 中装饰物的值）、`int` 排序顺序（默认 0）、`bool` 初始可见、`MeshFilter` 子物体引用、可空的 `Shader` 引用。**不包含颜色字段。**
- [ ] Prefab 结构：根 GameObject 挂 `DecorationView`；子 GameObject 持 `MeshFilter` + `MeshRenderer`。
- [ ] 编辑期：`[ExecuteAlways]` 下构建 Mesh 并赋给子物体的 `MeshFilter`，Mesh 标记 `HideFlags.DontSave`，以便编辑期可见且不被写进 Prefab 或场景文件。只在 Sprite 引用变化时重建，不在每帧执行。
- [ ] 运行时：Mesh 按 `Sprite` 做静态缓存共享；进入运行时后销毁编辑期的那份实例 Mesh。
- [ ] 显隐只切渲染器开关；隐藏后重新显示不重建几何、不改变排序位置。隐藏的装饰物保留全部配置。
- [ ] Shader 查找两段式：优先组件上的序列化引用，为空则回退按名查找（`HexMap/Decoration`）。这是为了防止打包时未被引用的 Shader 被剥离导致运行时取到空。
- [ ] Shader 为空时记录错误并禁用渲染器，**不抛异常**。
- [ ] 渲染器配置沿用旧实现：关闭阴影投射、接收阴影、光照探针、反射探针、运动矢量。
- [x] `sortingLayer` 留 `Default`。**分层由 `sortingOrder` 表达**：装饰物 `-100`、HexMap `0`（基线）、覆盖物 `100`，由 `DecorationQueue` 集中拥有；组件默认取装饰物的值。排序号是只读的摆放设置，不是运行时状态。**已推翻了中途的一版设计** —— 曾一度删掉该字段，因为当时认为 `sortingOrder` 只能在组内微调；实测证明它才是跨带排序的唯一旋钮（`renderQueue` 在排序号不等时被完全忽略），所以它回归了，并被提升为分层机制本身。见 `docs/reference/unity-render-order-rules.md` 第 3 节与 ADR-0001。
- [ ] 分层只靠 `renderQueue` 子区间。**不得**引入深度偏移、几何高度分层或渲染管线改动。
- [ ] 提供一个装饰物 Prefab 作为实例参考（人工创建，非批量工具）。
- [ ] 用同一个 Prefab 改队列值为覆盖物的值，即可得到浮在 HexMap 之上的一层，不需要新组件或新 Shader。

## Comments

### 为什么编辑期与运行时的 Mesh 缓存要分裂

编辑期需要每个实例有自己的 Mesh 才能在不进 Play 时可见；运行时则需要同一 Sprite 的多个实例共享 Mesh 以省内存。若统一用共享缓存，编辑期生成的 Mesh 会随 Prefab 被写进 `.prefab` 文件，成为脏资产。所以：编辑期实例 Mesh 标 `DontSave`、进运行时销毁，运行时改用按 Sprite 的共享缓存。这件事会让启用/禁用路径有一小段状态机，是刻意的。

### 不在本票范围

测试不在本票。本票只要求组件能跑、能在编辑期看见效果。断言在 03。

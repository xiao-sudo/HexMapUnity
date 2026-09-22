# 装饰物与覆盖物作为 HexMap 之下的渲染层

**Status:** accepted

## 决策

地图的贴片渲染分成若干**带（band）**，用 `Renderer.sortingOrder` 定序：装饰物 `-100`、HexMap `0`（基线）、覆盖物 `100`，后续特效继续往上取正值。四个数字由 `DecorationQueue` 常量类集中拥有。每个装饰物是一个自渲染的 Prefab，一装饰一纹理一材质，**不共享材质**，Draw Call 不作为指标；只承诺 SetPass Call 为 1~2。

**排序号小者先画。** HexMap 的 `0` 是基线且**不可移动** —— 见下条实测前提。因此任何要画在 HexMap 之下的带都必须取负值。

- **装饰物（Decoration）是 Prefab**，一装饰一 Prefab，根挂 `DecorationView` 组件，子物体持 `MeshFilter` + `MeshRenderer`。同一个 Prefab 把排序号改成覆盖物的值就成了覆盖物，不需要另一套组件或 Shader。
- **覆盖物（Overlay）共用同一套 Shader**；它有装饰物不具备的视觉能力时，才需要重新讨论 Shader 边界（关键字会翻倍 shader 变体、打掉 SetPass 预算）。
- **网格拓扑跟随 Sprite 导入设置**：`Full Rect` 得到一个 Quad，`Tight` 得到 Unity 为不透明区域生成的轮廓网格。实现不假设顶点数，只拒绝无几何、UV 数与顶点数不匹配、索引数非 3 的倍数、索引越界。**推荐 `Tight`**：它减少片元填充，并让图集打包更紧凑。
- **分层不靠深度缓冲，也不靠几何高度**，靠 `sortingOrder` 带；`sortingLayer` 一律留 `Default`。三个队列号（2800 / 3000 / 3005）**保留但不再决定层级**，它们退化为材质标识，任务只剩「留在透明带内」—— 所有带都 alpha 混合且关深度写入，谁都不能挪到不透明队列。
- **实测前提：`sortingOrder` 的优先级高于 `renderQueue`。** 排序键是 `sortingLayer → sortingOrder → renderQueue`，只有 `sortingOrder` 相等时 `renderQueue` 才参与比较。这把队列子区间分层彻底否决了：只要排序号不等，队列差异就被忽略。反过来它也让排序号成为唯一可用的跨带旋钮 —— 因为它是**唯一两边都能设**的排序维度（见 Considered Options 中 `sortingLayer` 被否决的原因）。
- **排序号是摆放设置，不是运行时状态**：只读属性，值写在 Prefab 上。运行时逐帧改它等于逐帧改变层级归属。
- **不做**：Prefab 批量生成工具、装饰物之间的正确互相遮挡、Billboard、运行时变换更新、运行时队列值逐帧修改、装饰物排序号的符号校验（见 Consequences）。

## Considered Options

- **用 `sortingLayer` 表达带（一条带一个排序层）** —— 否决，且是**被 API 挡住**而非设计取舍。`sortingLayer` 只能通过 `Renderer.sortingLayerID` 设置，而 `Graphics.DrawMeshInstanced` / `RenderMeshInstanced` 既不接受排序参数、也不产生 `Renderer` 实例：反射打印的 17 个 `DrawMeshInstanced` 重载里只有渲染 layer（剔除层），`RenderParams` 的全部字段中没有任何排序字段。所以实例化绘制的 HexMap **无法加入任何指定的排序层**，带序列会缺掉中间一环。
- **让 HexMap 改用 `MeshRenderer` 以获得完整排序控制** —— 未采纳。它确实可行（`MeshRendererStrategy` 就在仓库里），代价是放弃实例化绘制，且需要先量化 per-cell Renderer 的开销；而当前排序号方案在**两条绘制策略下都成立**，所以不值得为它重做绘制策略。若将来需要让特效插到 HexMap 之下，这是首选方案。
- **用 `rendererPriority` 做跨带排序** —— 否决。它在 `Renderer` 与 `RenderParams` 上都存在，名字也像排序，但只作用于同一材质/shader 批次内部，不是跨带旋钮。
- **用深度缓冲/几何高度分层** —— 否决。装饰物与 HexMap 都在透明队列、都 `ZWrite Off` 且 alpha 混合；HexMap 的 `_InteriorAlpha` 与渐变必须以半透明绘制，改成不透明几何等于废掉该特性。
- **`Texture2DArray` 或纹理图集共享材质，把 Draw Call 压到 1~3** —— 否决。纹理数组要求所有纹理同尺寸同格式，且在微信 WebGL 2.0 上未验证；纹理图集重新打包会使 Prefab 的纹理引用漂移，与"一装饰一纹理一 Prefab"的工作流直接冲突。
- **覆盖物复用装饰物 Shader 的关键字** —— 否决。关键字翻倍 shader 变体，与 SRP Batcher 的"变体越少越好"直接冲突。
- **每纹理每队列手写 `.mat` 资产** —— 否决。材质是 `(Texture, Queue)` 的纯派生数据，没有需要人工调校的参数；100 个 `.mat` 进版本控制是净负债。
- **要求装饰 Sprite 必须是四边形（`Full Rect` 导入）** —— 否决。最初按「一装饰一 Quad」写死了 4 顶点 / 6 索引的校验，但装饰网格本来就可能不是四边形：导入器的 `Tight` 模式会按不透明区域的 alpha 轮廓与 Tessellation Detail 生成轮廓网格，顶点更多却不必为透明像素做片元着色，因而更省填充率。硬性要求四边形等于把「必须记得改导入设置」变成一条隐形前提，且失败模式是「看不见 + 一行日志」，容易被误判成代码缺陷。代价是消费方必须接受任意顶点数。
  注意两个容易混淆的收益：**图集打包更紧凑**来自 `Tight` 本身；**省填充率**来自轮廓细分（Tessellation Detail），二者是导入器里两个独立的开关。另外轮廓网格**只能由导入管线生成**——`Sprite.Create(..., SpriteMeshType.Tight)` 返回的仍是 4 顶点矩形。

## Consequences

- **低于 HexMap 的带必须取负排序号，这条规则没有代码能自然表达。** 组件默认值就是 `DecorationQueue.DecorationSortingOrder`（负），所以正常路径不会错；但**任何正整数排序号都会盖住 HexMap，无论多小**。已刻意不加符号校验：同一个组件就是要靠更大的正值变成覆盖物，禁止正值会挡掉正当用法。**代价是「加特效 = 50」这类新带会静默压住地图** —— 这正是本方案曾经真实发生过的缺陷。若将来要加护栏，方向是在编辑器里对「未通过 `DecorationQueue` 取值的排序号」报警，而不是限制符号。
- **Draw Call = 纹理数（按 50 以内规划）**。将来要降这个数字，必须改"共享材质 + 纹理图集"，那是推翻本决策。
- **材质数 = 纹理数 × 实际用到的队列数（≤ 100）**，SRP Batcher 为每个材质在 GPU 常驻一份 `UnityPerMaterial` 常量缓冲。这是微信小游戏真机内存的待验证项。排序号不增加材质数，因为它是 `Renderer` 属性而非材质属性。
- **HexMap 的 `0` 在实例化路径上不可设。** `Graphics.DrawMeshInstanced` 没有排序参数，那条路径的 HexMap 是被钉在 0 上的。`DecorationQueue.HexMapSortingOrder` 因此是**声明性常量**（供其它带对照），只有 `MeshRendererStrategy` 会真的把它写到 cell 上。两条路径行为一致纯属 0 恰好是默认值。
- **这份契约依赖一个曾经的错误推理。** 项目的渲染排序参考文档原先推导出 `sortingLayer → renderQueue → sortingOrder`，并自行标注为推理而非官方结论。该推导被实测证伪，文档已更正。改动分层相关代码前先读那份文档第 3 节。
- **覆盖物不会被压暗**：它画在 HexMap 之上，不需要也不应该被半透明格子压暗。
- 旧实现的 `StaticDecoration*` 系列类型、`decorate.unity`、`Decoration.mat` 全部作废；旧 PlayMode 测试的断言随之废弃，但像素回读手法照搬。

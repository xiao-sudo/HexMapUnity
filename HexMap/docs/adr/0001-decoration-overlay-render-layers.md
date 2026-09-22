# 装饰物与覆盖物作为 HexMap 之下的渲染层

**Status:** accepted

## 决策

地图的贴片渲染分成三层，用透明队列（Transparent 3000）的三个子区间定序：装饰物 2800、HexMap 3000、覆盖物 3005。三个数字由 `DecorationQueue` 常量类集中拥有。每个装饰物是一个自渲染的 Prefab，一装饰一纹理一材质，**不共享材质**，Draw Call 不作为指标；只承诺 SetPass Call 为 1~2。

- **装饰物（Decoration）是 Prefab**，一装饰一 Prefab，根挂 `DecorationView` 组件，子物体持 `MeshFilter` + `MeshRenderer`。
- **覆盖物（Overlay）共用同一套 Shader**，只在材质上使用不同 `renderQueue`；它有装饰物不具备的视觉能力时，才需要重新讨论 Shader 边界（关键字会翻倍 shader 变体、打掉 SetPass 预算）。
- **分层不靠深度缓冲，也不靠几何高度**，只靠 renderQueue 子区间。`sortingLayer` 一律留 `Default`，`sortingOrder` 只做队列内微调。
- **不做**：Prefab 批量生成工具、装饰物之间的正确互相遮挡、Billboard、运行时变换更新、运行时队列值逐帧修改。

## Considered Options

- **用深度缓冲/几何高度分层** —— 否决。装饰物与 HexMap 都在透明队列、都 `ZWrite Off` 且 alpha 混合；HexMap 的 `_InteriorAlpha` 与渐变必须以半透明绘制，改成不透明几何等于废掉该特性。
- **`Texture2DArray` 或纹理图集共享材质，把 Draw Call 压到 1~3** —— 否决。纹理数组要求所有纹理同尺寸同格式，且在微信 WebGL 2.0 上未验证；纹理图集重新打包会使 Prefab 的纹理引用漂移，与"一装饰一纹理一 Prefab"的工作流直接冲突。
- **覆盖物复用装饰物 Shader 的关键字** —— 否决。关键字翻倍 shader 变体，与 SRP Batcher 的"变体越少越好"直接冲突。
- **每纹理每队列手写 `.mat` 资产** —— 否决。材质是 `(Texture, Queue)` 的纯派生数据，没有需要人工调校的参数；100 个 `.mat` 进版本控制是净负债。

## Consequences

- **Draw Call = 纹理数（按 50 以内规划）**。将来要降这个数字，必须改"共享材质 + 纹理图集"，那是推翻本决策。
- **材质数 = 纹理数 × 实际用到的队列数（≤ 100）**，SRP Batcher 为每个材质在 GPU 常驻一份 `UnityPerMaterial` 常量缓冲。这是微信小游戏真机内存的待验证项。
- **`sortingOrder` 不增加材质数**（它是 `Renderer` 属性而非材质属性），但会按排序值把 draw call 序列切成若干段。
- **`Graphics.DrawMeshInstanced` 不参与常规排序流程**，而生产场景的 HexMap 走的是该路径。已有验证只覆盖了 `MeshRenderer` 策略，因此"装饰物不插到 HexMap 之前"必须在真实生产场景里用 Frame Debugger 确认。
- **覆盖物不会被压暗**：它画在 HexMap 之上，不需要也不应该被半透明格子压暗。
- 旧实现的 `StaticDecoration*` 系列类型、`decorate.unity`、`Decoration.mat` 全部作废；旧 PlayMode 测试的断言随之废弃，但像素回读手法照搬。

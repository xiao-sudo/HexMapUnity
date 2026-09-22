# Unity 渲染排序规则（URP）

## 目的

本文记录 Unity 透明物体渲染排序的**真实规则**，以及本仓库 HexMap 的当前配置，用于回答"如何让一组几何稳定地渲染在另一组之上，同时不破坏合批/Instancing"这类问题。

本文的规则部分以 Unity 官方文档为准，逐条给出原始链接；推理部分单独标注，不混入结论。

**适用版本**：Unity 2022.3.50f1 + URP 14.0.11（见 `ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json`）。

> **状态（2026-09-22）：本文的官方规则部分仍然有效；本仓库特有配置部分已被 ADR 取代。**
> 装饰物层的实现已于 `e3d9103` 作废并重做，当前的分层契约以
> [`docs/adr/0001-decoration-overlay-render-layers.md`](../adr/0001-decoration-overlay-render-layers.md) 为准：
> 装饰物 2800 / HexMap 3000 / 覆盖物 3005，三个数字由 `DecorationQueue` 唯一拥有。
> 下文引用 `StaticDecoration*` 类型、`Decoration.mat`、`decorate.unity` 与旧队列值的段落属于历史记录，
> 对应的文件和类型已不存在；`StaticDecoration.shader` 现名为 `HexMap/Decoration`。

## 范围

本文覆盖：

- `renderQueue` 的取值来源与覆盖方式；
- 排序标志的**应用顺序**；
- URP 透明物体的实际排序键；
- 本仓库 `Decoration.shader` / `InstancedHexCell.shader` 的实际队列值；
- 合批与 Instancing 的兼容性规则；
- 验证方法。

本文不覆盖：

- URP Renderer Feature / RenderObjects 的自定义 pass 排序；
- 2D Renderer 的 `SortingLayerRange` 批次机制（与本文的 3D 排序是两套东西）；
- 深度图、阴影、后处理的 pass 顺序。

## 官方规则

### 1. Queue 标签设置队列，Material 可覆盖

`Queue` 是 SubShader 标签。SubShader 标签只对 SubShader 生效，放进 Pass 里无效。

> The `Queue` tag tells Unity which render queue to use for geometry that it renders. The render queue is one of the factors that determines the order that Unity renders geometry in.

命名队列取值：`Background`(1000)、`Geometry`(2000)、`AlphaTest`(2450)、`Transparent`(3000)、`Overlay`(4000)。也支持 `"Queue" = "Named+Offset"` 的偏移写法。

**每个 Material 可以覆盖这个值**：

> By default, Unity renders geometry in the render queue specified in the [Queue] tag. You can override this value on a per-material basis. … In a C# script, you can do this by setting the value of `Material.renderQueue`.

因此同一个 Shader 的两个 Material 可以落在不同队列上。反之，两个不同的 Shader 也可以被 Material 强制到同一个队列上。

`Material.renderQueue` 的两个取值约定：

> Render queue value should be in [0..5000] range to work properly; or **-1** to use the render queue from the shader.

> Note that if a shader on the material is changed, the render queue resets to that of the shader itself.

后一条是个陷阱：在 Inspector 里换 Shader 会静默重置 renderQueue 覆盖值。本仓库两个 `.mat` 的 `m_CustomRenderQueue: -1` 正表示"不覆盖，用 shader 的值"。

来源：[ShaderLab: assigning tags to a SubShader](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-SubShaderTags.html)、[Material.renderQueue](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Material-renderQueue.html)

### 2. 排序标志按枚举顺序依次生效

这是最容易记错的一条。`SortingCriteria` 是 flag 组合，**多个 flag 组合时按下面列举的顺序依次应用**：

```
SortingLayer  →  RenderQueue  →  BackToFront  →  QuantizedFrontToBack
              →  OptimizeStateChanges  →  CanvasOrder
```

> Multiple flags, when combined, are applied in the above order.

原文的 "the above order" 指的就是基本 flag 的列举顺序：`SortingLayer`、`RenderQueue`、`BackToFront`、`QuantizedFrontToBack`、`OptimizeStateChanges`、`CanvasOrder`。

**注意这里的两个后果**：

1. `SortingLayer` 排在 `RenderQueue` **之前** —— sorting layer 的优先级高于 render queue，可以压过队列差异。
2. `OptimizeStateChanges`（为减少状态切换而排序，即合批友好排序）排在最后 —— 它是**在**排序键之后的次级优化，不是独立通道。

来源：[SortingCriteria](https://docs.unity3d.com/2020.3/Documentation/ScriptReference/Rendering.SortingCriteria.html)

URP 的透明 pass 使用 `CommonTransparent`：

```csharp
// Library/PackageCache/com.unity.render-pipelines.universal@14.0.11/Runtime/Passes/DrawObjectsPass.cs
var sortFlags = (data.m_IsOpaque)
    ? renderingData.cameraData.defaultOpaqueSortFlags
    : SortingCriteria.CommonTransparent;
```

`CommonTransparent` 的语义是"需要用不透明 + 从后到前排序"的典型组合，即包含 `SortingLayer | RenderQueue | BackToFront | OptimizeStateChanges | CanvasOrder`。

来源（本地源码）：`Library/PackageCache/com.unity.render-pipelines.universal@14.0.11/Runtime/Passes/DrawObjectsPass.cs`

### 3. 结论：透明物体的排序键

由上两条推出，本仓库这种"两个不同 shader、不同 material"的场景，实际排序键是：

```
sortingLayer  →  renderQueue  →  sortingOrder  →  距离（从后到前）
```

其中 `sortingOrder` 参与 `RenderQueue` 这一级的桶内比较（它是 `Renderer` 的属性，与 material 的 renderQueue 共同决定桶）。

> **推理标注**：官方文档把 `SortingLayer` 和 `RenderQueue` 列为两个 flag，没有单独把 `Renderer.sortingOrder` 列成 flag。`sortingOrder` 属于 `RenderQueue` 这一级的比较键，是本仓库从"排序层→队列→顺序"的常见语义推得的，不是官方文档逐字写明的结论。实践上该模型与现有 PlayMode 测试的观测结果一致。

### 三个容易踩的约束

1. **`sortingOrder` 有取值范围**：-32768 到 32767。
   > Note: The value must be between -32768 and 32767.

2. **不要用 `sortingLayerID` 比较层级先后**。ID 是随机分配的，不是有序值：
   > This is the unique id assigned to the layer. **It is not an ordered running value and it should not be used to compare with other layers to determine the sorting order.**
   
   需要比较层级先后时用 `SortingLayer.GetLayerValueFromID` 或 `SortingLayer.value`。

3. **默认 sorting layer 一定存在**：
   > There is always a default SortingLayer named "Default" which all sprites are added to initially.

来源：[Renderer.sortingOrder](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Renderer-sortingOrder.html)、[SortingLayer](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SortingLayer.html)

## 合批与 Instancing 的兼容性

### SRP Batcher

> A GameObject must meet the following requirements to be compatible with the SRP Batcher code path:
> - The GameObject must contain either a mesh or a skinned mesh. It can't be a particle.
> - **The GameObject mustn't use MaterialPropertyBlocks.**
> - The shader that the GameObject uses must be compatible with the SRP Batcher.

> The SRP Batcher reduces the CPU time Unity requires to prepare and dispatch draw calls for materials that use the same shader variant.

来源：[Scriptable Render Pipeline Batcher](https://docs.unity3d.com/2022.3/Documentation/Manual/SRPBatcher.html)

### GPU Instancing

> GPU instancing is a draw call optimization method that renders multiple copies of a mesh with the same material in a single draw call.

> Unity uses GPU instancing for GameObjects that share the same mesh and material.

> **GPU instancing isn't compatible with the SRP Batcher. The SRP Batcher takes priority over GPU instancing.** If a GameObject is compatible with the SRP Batcher, Unity uses the SRP Batcher to render it, not GPU instancing.

> Unity does not support GPU instancing for SkinnedMeshRenderers or MeshRenderer components attached to GameObjects that are **SRP Batcher compatible**.

来源：[GPU instancing](https://docs.unity3d.com/2022.3/Documentation/Manual/GPUInstancing.html)

### 优化手段的优先级

当同一个 GameObject 同时满足多种优化条件时，Unity 只采用优先级最高的那种：

> Unity prioritizes draw call optimizations in the following order:
> 1. SRP Batcher and static batching
> 2. GPU instancing
> 3. Dynamic batching

> If you mark a GameObject for static batching and Unity successfully batches it, Unity disables GPU instancing for that GameObject, even if the renderer uses an instancing shader.

这条对本仓库有直接含义：在一个**启用了静态批处理**的场景里，Hex 格子即使满足 instancing 条件，也可能被静态批处理接管而**禁用 GPU instancing**。测量 instancing 效果时必须先确认场景里这些格子的 Static Editor Flags，否则测到的不是 instancing。

来源：[Optimizing draw calls](https://docs.unity3d.com/2022.3/Documentation/Manual/optimizing-draw-calls.html)

### 要点

1. **SRP Batcher 与 GPU Instancing 互斥，SRP Batcher 优先。** 材质上勾了 `Enable GPU Instancing` 不等于一定会走 instancing —— 如果该 GameObject 对 SRP Batcher 兼容，Unity 会优先用 SRP Batcher。
2. **两者都要求候选对象在排序结果中连续。** 排序键把两次 draw 分到不同区段，它们就不可能进同一个批次。
3. **不同 shader、不同 material 之间永远无法合批或 instance。** 所以"用 renderQueue 把两组分开"对合批是零成本操作。

## 本仓库实际配置

| | `HexMap/Decoration` | `HexMap/InstancedHexCell` |
| --- | --- | --- |
| SubShader `Queue` 标签 | `Transparent-200` | `Transparent` |
| 换算后的 renderQueue | **2800** | **3000** |
| Material 是否覆盖 | 见 ADR-0001：由 `(Texture, Queue)` 派生 | 否，继承 shader 的 3000 |
| `RenderType` | `Transparent` | `Transparent` |
| `DisableBatching` | `True` | 未声明 |
| 混合 / 深度 | `Blend SrcAlpha OneMinusSrcAlpha`、`ZWrite Off`、`ZTest LEqual` | 同左 |
| Sorting Layer | Default (uniqueID 0) | Default (uniqueID 0) |

证据：

- `Assets/HexMap/Shaders/Decoration.shader:11` —— `"Queue" = "Transparent-200"`；`ZWrite Off`。
- `Assets/HexMap/Shaders/InstancedHexCell.shader:21` —— `"Queue" = "Transparent"`；`ZWrite Off`。
- `Assets/HexMap/Shaders/Decoration.shader.meta` guid `458f582936cb26d48b075f1219810945`（沿用旧 `StaticDecoration.shader` 的 GUID，因此既有引用未断）。
- `Assets/HexMap/Shaders/InstancedHexCell.shader.meta` guid `cb6086c4515b58f4a977505a21257777` = `Assets/HexMap/BaseMap.mat:11` 引用的 shader；该 material `m_CustomRenderQueue: -1`。
- `Assets/Tests/EditMode/HexMap/UnityRuntime/HexMapViewTests.cs` —— 断言 `InstancedHexCell` 的 `renderQueue` 等于 `DecorationQueue.HexMap`，即 shader 拥有该数字、C# 只验证。
- `Assets/Scripts/HexMap/UnityRuntime/DecorationQueue.cs` —— 2800 / 3000 / 3005 的唯一来源。
- `ProjectSettings/TagManager.asset:40-42` —— 项目只有一个 sorting layer：`Default` (uniqueID 0)。

`DisableBatching = True` 的含义需要精确理解：它只关闭 **Dynamic Batching**，不影响 SRP Batcher。

> The `DisableBatching` SubShader Tag prevents Unity from applying Dynamic Batching to geometry that uses this SubShader.

`Decoration.shader:10` 的注释说的正是这件事。

### 由队列顺序得到的可见性结论

装饰物 2800 先绘制，Hex 3000 后绘制。两个 shader 都是 `ZWrite Off` 的 alpha 混合，因此**先绘制的颜色留在后面像素上，后绘制的覆盖它** —— Hex 覆盖装饰物。

这一结论不依赖 `sortingOrder`，也不依赖物体到相机的距离。旧 PlayMode 测试曾把装饰物故意放在比 Hex 更靠近相机的位置，注释写明"queue ordering must still win"，验证的正是这一点；该测试随旧实现一并作废，其像素回读手法由 ticket 03 重建。

### 推理标注：Hex 单元实际走的批次路径

`MeshRendererTarget.Apply` 对每个可见格子调用 `Renderer.SetPropertyBlock`（`Assets/Scripts/HexMap/UnityRuntime/MeshRendererTarget.cs:40-43`）。按上文 SRP Batcher 的 GameObject 兼容性要求（不得使用 MaterialPropertyBlock），这些格子对 SRP Batcher **不兼容**，因此会落到 GPU Instancing 路径（`InstancedHexCell.shader:39` 有 `#pragma multi_compile_instancing`）。

**这是推理，不是文档逐字结论**，需要用 Frame Debugger 确认（见下文验证章节）。

## 配置建议

### 分层原则

**让 renderQueue 决定跨组的前后关系，让 sortingOrder 只做组内微调，sorting layer 全部保持默认。**

| 目标 | 用什么 | 不要用什么 |
| --- | --- | --- |
| A 组整体在 B 组之下 | A 的 renderQueue < B 的 renderQueue | sorting layer、sorting order |
| 同组内固定排序 | 组内统一的 `Renderer.sortingOrder` | 混用不同值（切批次） |
| 同组内按距离排序 | 不动，默认行为 | — |

### 本仓库的推荐取值

| | RenderQueue | Sorting Layer | Sorting Order |
| --- | --- | --- | --- |
| 装饰物 | 2800（`DecorationQueue.Decoration`） | Default | 组件上的 `sortingOrder`，组内微调用 |
| InstancedHexCell | 3000（shader 拥有，`DecorationQueue.HexMap` 引用） | Default | 组内统一即可，任意固定值 |
| 覆盖物 | 3005（`DecorationQueue.Overlay`） | Default | 组件上的 `sortingOrder`，组内微调用 |

### 反例

- **给 Hex 用比装饰物更低的 sorting layer。** 因为 `SortingLayer` 排序在 `RenderQueue` 之前，这会越过队列差异，破坏 Hex 覆盖装饰物的关系。
- **用 `sortingOrder` 让 Hex 超过装饰物，而把两者放在同一队列。** 同队列时组内退化为距离排序，跨组关系变得依赖相机位置。
- **给同一组内不同对象设不同 `sortingOrder`。** 会切出额外批次。对 SRP Batcher 是切 SRP Batch，对 instancing 是切 instanced draw call。

## 验证

1. 打开 **Window → Analysis → Frame Debugger**，定位到 `Render Camera → Render Transparents → RenderLoopNewBatcher.Draw`。
2. 展开列表，确认：
   - 装饰物的所有 draw 排在 Hex 的所有 draw **之前**；
   - 每个条目右侧显示未被合批的原因（如 "Nodes have different shaders"）。
   - 走 instancing 的 draw 显示为 **Render Mesh (instanced)**。
3. 用 **Profiler** 对比改动前后的 batch 数与 `SetPass Calls`；结合 GPU 侧耗时判断 instancing 是否真的更优。

来源：[Frame Debugger](https://docs.unity3d.com/2022.3/Documentation/Manual/FrameDebugger.html)、[GPU instancing](https://docs.unity3d.com/2022.3/Documentation/Manual/GPUInstancing.html)

## 原始链接

### Unity Manual

- [ShaderLab: assigning tags to a SubShader](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-SubShaderTags.html) —— `Queue`、`RenderType`、`DisableBatching` 标签语义，以及 material 覆盖队列。
- [Scriptable Render Pipeline Batcher](https://docs.unity3d.com/2022.3/Documentation/Manual/SRPBatcher.html) —— SRP Batcher 的 GameObject 兼容性要求（含 MaterialPropertyBlock 排除项）。
- [GPU instancing](https://docs.unity3d.com/2022.3/Documentation/Manual/GPUInstancing.html) —— instancing 成立条件，以及与 SRP Batcher 的互斥与优先级。
- [Frame Debugger](https://docs.unity3d.com/2022.3/Documentation/Manual/FrameDebugger.html) —— 验证批次划分。
- [Optimizing draw calls](https://docs.unity3d.com/2022.3/Documentation/Manual/optimizing-draw-calls.html) —— 优化手段之间的优先级。

### Unity Scripting API

- [SortingCriteria](https://docs.unity3d.com/2020.3/Documentation/ScriptReference/Rendering.SortingCriteria.html) —— flag 组合按列举顺序应用；`CommonTransparent` 说明。（该页在 2020.3/2020.1/2022.3 内容一致，按可访问性引用 2020.3。）
- [Material.renderQueue](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Material-renderQueue.html) —— 逐材质覆盖队列。
- [Renderer.sortingLayerID](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Renderer-sortingLayerID.html) / [Renderer.sortingOrder](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Renderer-sortingOrder.html) —— Renderer 侧排序属性。
- [SortingLayer](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SortingLayer.html) —— sorting layer 的 ID 与 `Default`。

### 本仓库相关实现

- `Assets/HexMap/Shaders/Decoration.shader`
- `Assets/HexMap/Shaders/InstancedHexCell.shader`
- `Assets/Scripts/HexMap/UnityRuntime/DecorationQueue.cs`
- `Assets/Scripts/HexMap/UnityRuntime/HexMapRenderer.cs`
- `Assets/Scripts/HexMap/UnityRuntime/MeshRendererStrategy.cs`
- `Assets/Scripts/HexMap/UnityRuntime/MeshRendererTarget.cs`

# HexMap View 渲染架构

## 目的

本文定义 `HexMapView` 后续重构的目标架构，供实现智能体直接执行。目标是把 Unity 场景生命周期、地图数据、单 Hex 渲染能力和输入拾取分开，同时保留后续切换批量渲染后端的空间。

本文描述的是目标设计，不是当前代码行为。当前实现仍在 `Assets/Scripts/HexMap/UnityRuntime/HexMapView.cs` 中直接创建每个 Hex 的 `GameObject`、Mesh、Renderer 和 Collider；当前拾取流程仍由 `HexMapPicker` 使用 Physics Raycast 完成。实现时必须以本文的目标行为为准，并通过测试确认迁移没有破坏已有的地图和布局契约。

## 范围

本文覆盖：

- `HexMapView` 的场景适配职责；
- 非 `MonoBehaviour` 的 `HexMapRenderer`；
- 单 Hex 的 `HexView` 门面和生命周期；
- `HexAppearance` 表现数据；
- 共享 Mesh、Material 和运行时资源的所有权；
- 无 Collider 的世界坐标拾取；
- 为 SRP Batcher 和未来批量渲染保留的边界；
- 实现顺序和验收标准。

本文不定义：

- 高亮、选择、可移动、可攻击等游戏语义；
- GVG 地块、归属、阻挡、战斗或寻路规则；
- 最终 Shader、Shader Graph 或具体 SRP Pipeline；
- 最终 GPU Instancing、批量 Mesh 或分块渲染算法。

已有的地图模型和坐标布局契约继续参考：

- [`runtime-hex-map-and-picking.md`](runtime-hex-map-and-picking.md)：Runtime Hex Map 与现有拾取背景；
- [`hex-coordinate-layout-kernel.md`](hex-coordinate-layout-kernel.md)：`HexCoord`、`HexLayout` 和 XY/XZ、Pointy/Flat 布局契约；
- `Assets/Scripts/HexMap/Core/HexLayout.cs`：当前 `HexToWorld` / `WorldToHex` 实现；
- `Assets/Scripts/HexMap/Runtime/HexMap.cs`：当前地图查询和遍历 API。

## 目标架构

```text
HexMapView : MonoBehaviour
    ├── Unity Inspector 配置
    ├── RuntimeHexMap
    ├── HexLayout
    ├── HexMapRenderer 生命周期
    ├── TryGetHexView(HexCoord)
    ├── WorldToHex(Vector3)
    └── TryGetHexViewAtWorldPoint(Vector3)

HexMapRenderer
    ├── 非 MonoBehaviour
    ├── 共享 Mesh / Material / 运行时资源
    ├── HexView 集合
    ├── RenderHandle 生命周期
    └── 当前渲染后端

HexView
    ├── HexCoord 身份
    ├── 只读 HexCell
    ├── Build 生命周期有效性
    ├── HexAppearance 缓存
    └── RenderHandle

逻辑层
    ├── 维护领域状态
    ├── 维护或计算表现状态
    ├── 合成 HexAppearance
    └── 通过 HexMapView 查询 HexView 并应用表现
```

核心方向是：`HexMapView` 仍然可以继承 `MonoBehaviour`，但不再是所有渲染细节的承载者；`HexView` 是逻辑层可以查询到的单 Hex 渲染门面；Unity 组件、生成对象和资源由 `HexMapRenderer` 隐藏管理。

## 代码地图

目标实现建议落在 `HexMap.UnityRuntime` 程序集，因为这些类型需要使用 Unity 的 `GameObject`、`Mesh`、`Material`、`Transform` 或 `Color`。不得把渲染资源或 `MonoBehaviour` 依赖加入 `HexMap.Runtime`。

- `Assets/Scripts/HexMap/UnityRuntime/HexMapView.cs`：保留场景入口，委托给 `HexMapRenderer`。
- `Assets/Scripts/HexMap/UnityRuntime/HexMapRenderer.cs`：新增，负责创建、更新、销毁当前渲染后端资源。
- `Assets/Scripts/HexMap/UnityRuntime/HexView.cs`：新增，负责单 Hex 身份、有效性和表现操作。
- `Assets/Scripts/HexMap/UnityRuntime/HexAppearance.cs`：新增，第一版只包含 `Visible` 和 `Color`。
- `Assets/Scripts/HexMap/UnityRuntime/HexMapRenderConfig.cs`：新增，承载 Parent、共享 Material、Layer 等 Unity 渲染配置。
- `Assets/Scripts/HexMap/UnityRuntime/HexRenderHandle.cs`：建议新增为内部句柄类型，包含 Renderer 身份或 Generation。
- `Assets/Scripts/HexMap/UnityRuntime/HexMapPicker.cs`：改为可选的屏幕输入适配器，不再依赖 Physics 或 Collider。
- `Assets/Scripts/HexMap/Core/HexLayout.cs`：保留坐标数学契约；如需增加世界坐标适配，应由 UnityRuntime 上层包裹，不要把场景对象塞入布局核心。

当前 `HexMap.Core` 的 `HexLayout` 已使用 `UnityEngine.Vector3` 和 `Mathf`，因此本文只要求新的渲染类型不依赖 `MonoBehaviour`，不要求本次重构顺便完成 Core 的 Unity 脱离。

## 公共 API 目标

以下签名表达目标契约；实现智能体可以根据仓库的 C# 版本和现有命名进行等价调整，但不能改变职责和生命周期语义。

### HexMapView

```csharp
public sealed class HexMapView : MonoBehaviour
{
    public RuntimeHexMap Map { get; }
    public HexLayout Layout { get; }

    public bool TryGetHexView(HexCoord coordinate, out HexView view);

    public HexCoord WorldToHex(Vector3 worldPoint);

    public bool TryGetHexViewAtWorldPoint(
        Vector3 worldPoint,
        out HexView view);
}
```

`TryGetHexView` 只返回当前地图中实际存在的 Cell。地图范围外的坐标不能返回伪造的 `HexView`。

`WorldToHex` 负责世界坐标到当前 `HexMapView` 局部空间的变换，再调用共享的 `HexLayout.WorldToHex`。它只负责几何转换，不负责判断坐标是否存在。

`TryGetHexViewAtWorldPoint` 是便利组合 API，语义等价于 `WorldToHex` 加 `TryGetHexView`；它不应该复制另一套坐标数学。

### HexView

```csharp
public sealed class HexView
{
    public HexCoord Coordinate { get; }
    public HexCell Cell { get; }
    public bool IsValid { get; }

    public void SetAppearance(HexAppearance appearance);
}
```

约束：

- `Coordinate` 和 `Cell` 只读；
- `HexView` 不暴露 `GameObject`、`Transform`、`MeshFilter`、`MeshRenderer`、`MeshCollider` 或 `Material`；
- `HexView` 不理解 `Selected`、`Hovered`、`Reachable`、`Attackable` 等游戏语义；
- `SetAppearance` 应缓存当前表现，相同值不重复应用到底层；
- `HexView` 在一次 Build 生命周期内与坐标稳定绑定；
- Rebuild 或 Dispose 后旧对象的 `IsValid` 必须为 `false`，旧对象不能操作新地图中的同坐标对象。

### HexAppearance

第一版使用有限、不可变的表现数据：

```csharp
public readonly struct HexAppearance
{
    public bool Visible { get; }
    public Color Color { get; }
}
```

`HexAppearance` 属于 `HexMap.UnityRuntime` 的表现契约，因为当前版本使用 Unity `Color`。它不包含：

- `Selected`、`Hovered`、`Reachable` 等游戏语义；
- `Material` 或 Shader 引用；
- Shader 属性名称或通用 `object` 参数字典；
- Keyword、Texture、Buffer 等后端资源。

逻辑层负责把多个语义状态合成为一个最终 `HexAppearance`。不要让多个逻辑模块按不确定的调用顺序直接覆盖 `HexView` 的颜色。

### HexMapRenderConfig

建议由 `HexMapView` 根据 Inspector 字段创建配置对象，再传给 `HexMapRenderer`：

```csharp
public sealed class HexMapRenderConfig
{
    public Transform Parent { get; }
    public Material SharedMaterial { get; }
    public int Layer { get; }
}
```

构造完成的 `HexMapRenderer` 应处于可用状态，避免无状态构造后再通过 `Initialize` 进入半初始化状态。

## Build 和生命周期

目标 Build 流程如下：

1. 使当前 Renderer 和所有旧 `HexView` 进入失效状态。
2. 释放旧 Renderer 自己创建的 GameObject、共享 Mesh 和内部资源。
3. 从 `HexMapView.Radius` 创建新的 `HexMapDefinition`。
4. 创建新的 `RuntimeHexMap` 和 `HexLayout`。
5. 创建 `HexMapRenderConfig`。
6. 创建新的 `HexMapRenderer`。
7. Renderer 为当前布局创建一次共享基础 Hex Mesh。
8. 遍历 `RuntimeHexMap.Cells`，为每个 Cell 创建一个 `HexView` 和一个内部 RenderHandle。
9. 第一版仍可为每个 Cell 创建一个 GameObject，但所有 Cell 使用共享基础 Mesh 和共享 Material。
10. 使用 `HexLayout.HexToWorld(cell.Coordinate)` 设置每个 Cell 的局部位置。
11. 不创建 `MeshCollider`。
12. 发布新的 Map、Layout 和 Renderer，使查询 API 对外可用。

如果使用 Generation，应在旧 View 失效后推进 Generation；句柄校验必须同时确认 Renderer 身份或 Generation。不能因为新地图恰好复用了相同的数组索引，就让旧 View 重新获得访问权限。

逻辑层应以 `HexCoord` 作为跨 Build 的稳定主键。它可以在当前 Build 内缓存 `HexView`，但 Rebuild 后必须重新查询；如确实需要集中刷新缓存，可以提供 `Rebuilt` 通知，但不能让旧 `HexView` 自动绑定到新对象。

## 资源所有权

| 资源或状态 | 所有者 | 规则 |
| --- | --- | --- |
| `RuntimeHexMap` | `HexMapView` | 当前 View 绑定的地图快照 |
| `HexLayout` | `HexMapView` | 当前 View 的布局配置 |
| 生成的 GameObject | `HexMapRenderer` | Rebuild/Dispose 时释放 |
| 共享基础 Mesh | `HexMapRenderer` | 同一布局内 Cell 共享；Renderer 自建资源由 Renderer 释放 |
| 外部传入 Material | 外部调用方 | Renderer 使用但不销毁 |
| Renderer 自建 fallback 资源 | `HexMapRenderer` | 由 Renderer 创建和释放 |
| Cell 身份 | `HexView` | 只读 Coordinate/Cell，不拥有地图数据 |
| RenderHandle | `HexMapRenderer` / `HexView` | 必须带生命周期校验 |
| 当前 `HexAppearance` | `HexView` 或其后端 | 用于跳过重复写入 |

`HexView` 不拥有 Mesh 和 Material。这样未来把每 Cell GameObject 后端替换成批量 Mesh、Instance ID 或 GPU 数据索引时，逻辑层仍然只依赖 `HexView`。

## 渲染和 SRP Batcher 边界

SRP Batcher 是架构约束，但当前项目尚未完成最终 Shader 和 SRP Pipeline 的选择。当前实现只能保证不主动破坏未来的批处理路径，不能在 Shader 未确定时宣称最终进入 SRP Batcher。

Renderer 必须遵守这些前置条件：

- 不为每个 Hex 创建独立 Material；
- 尽量共享 Material 和 Shader Variant；
- 不把 Shader 属性名泄露给逻辑层；
- 不把 `MaterialPropertyBlock`、材质实例或具体 GPU 数据机制写入 `HexView` 公共契约；
- 让 `HexAppearance` 进入 Shader 的方式由渲染后端决定；
- 未来替换批量渲染后端时，不修改逻辑层的 `HexView` 和 `HexAppearance` 依赖。

当前第一版只承诺 `Visible` 和 `Color`。最终 Shader 确定后，再选择合适的 per-object 数据路径，并通过 Profiler 或 Frame Debugger 验证实际 SRP Batcher 行为。若 Shader 不兼容 SRP Batcher，必须记录为渲染后端或资源配置问题，不能通过让逻辑层直接操作 Material 来规避。

## 坐标和拾取

正式查询路径为：

```text
外部 HexCoord
    -> HexMapView.TryGetHexView

世界坐标
    -> HexMapView.WorldToHex
    -> HexMapView.TryGetHexView

屏幕坐标
    -> HexMapPicker
    -> Camera.ScreenPointToRay
    -> 与地图平面求交
    -> 世界坐标
    -> HexMapView.WorldToHex
    -> HexMapView.TryGetHexView
```

`HexMapPicker` 是可选的 Unity 输入适配器，不是 Hex Map 核心查询服务。它不使用 Physics Raycast，不检查 Collider 归属，也不保存“上一次成功的 Cell”作为失败结果。

平面求交应使用当前地图的局部平面：

- `HexPlane.XZ` 使用局部 `y = 0` 平面；
- `HexPlane.XY` 使用局部 `z = 0` 平面；
- 世界射线应先转换到地图局部空间，或使用等价的世界平面；
- 射线与平面平行时返回明确的 `NoPlaneHit` 结果；
- 坐标在地图半径外时返回 `OutsideMap`；
- 不存在的 Cell Id 查询返回 `Missing`；地图半径内的坐标都有对应 Cell。

地图 Transform 支持平移、旋转和均匀缩放。非均匀缩放不支持，必须被拒绝或明确记录为无效配置，因为它会使逻辑布局和视觉六边形的几何比例不一致。

## 逻辑层边界

逻辑层的推荐流程：

```text
读取或计算领域状态
    -> 计算表现优先级
    -> 合成 HexAppearance
    -> 按 HexCoord 查询 HexView
    -> SetAppearance(appearance)
```

例如选择和悬停同时存在时，逻辑层决定颜色和可见表现的优先级；`HexView` 只接收最终结果。 `HexCell` 保持地图身份和领域数据，不加入颜色、Material、Shader 参数或 Unity Renderer 引用。

第一版不要求地图级 `BeginUpdate/EndUpdate`。逐个 `HexView.SetAppearance` 是正式可用的操作路径；渲染后端应缓存表现并跳过相同值。只有性能验证显示批量更新确有收益时，才增加地图级批量提交 API。

## 扩展规则

### 新增表现参数

先判断参数是否是所有 Hex 后端都理解的稳定视觉概念：

1. 如果是，扩展 `HexAppearance` 的有限字段，并为相等比较和默认值增加测试。
2. 如果只属于特定 Shader，放到渲染后端扩展，不修改逻辑层语义接口。
3. 如果是游戏语义，放到逻辑状态或表现协调器，不放入 `HexView`。

不要通过 `SetMaterial(string, object)` 或暴露 `Material` 来绕过这条边界。

### 更换渲染后端

新后端应继续实现以下可观察契约：

- 按 `HexCoord` 获得稳定的当前 Build `HexView`；
- Rebuild 后让旧 View 失效；
- 能应用 `HexAppearance`；
- 不改变 `HexMapView` 的坐标查询 API；
- 不要求逻辑层知道 GameObject、Mesh 或 Renderer 的存在。

单元 GameObject、合批 Mesh、GPU Instancing、分块渲染都应是 `HexMapRenderer` 内部选择。

### 新增 Cell 领域数据

先扩展 `HexCell`、`HexMap` 和地图输入模型，再让逻辑层读取。不要把渲染 GameObject 作为 Cell 数据源，也不要把表现状态反向写入 `HexMap.Runtime`。

## 约束

- 私有和实例字段遵守仓库约定，使用 `m_` 前缀；局部变量和参数不使用该前缀。
- `HexMap.Runtime` 不得引用 `Camera`、`Physics`、`GameObject`、`MonoBehaviour` 或场景资源。
- `HexMapView` 不得把游戏语义塞进渲染对象。
- `HexView` 不得暴露 Unity 组件和资源所有权。
- `HexCoord` 仍是逻辑层和地图查询的稳定身份。
- `HexLayout` 的 Pointy/Flat、XY/XZ、Origin、OuterRadius 语义保持不变。
- 同一布局实例内的基础六边形 Mesh 应共享。
- 外部 Material 不由 Renderer 销毁。
- Rebuild 后旧 View 必须失效，不能静默转发到新地图。
- 首版不创建 MeshCollider，拾取不依赖 Physics。
- 不以当前 `Standard` fallback Material 推导未来 Shader API。
- 不在 Shader 未确定时承诺具体 SRP Batcher per-object 数据方案。

## 验证

### EditMode 验证

优先为不依赖场景的值和生命周期行为编写测试：

- `HexAppearance` 相等值不会触发重复后端应用；
- 同一 Build 中，同一坐标返回同一个 `HexView`；
- 不存在坐标不会返回伪造 View；
- Rebuild 后旧 `HexView.IsValid` 为 `false`；
- 旧 View 不能操作新地图；
- `WorldToHex` 与 `HexLayout` 的 Pointy/Flat、XY/XZ 组合保持一致；
- 世界坐标转换支持平移、旋转和均匀缩放；
- 非均匀缩放被拒绝或标记为不支持；
- `HexMapRenderConfig` 不改变外部 Material 所有权。

### PlayMode / Unity 验证

- Build 后每个存在的 Cell 都能找到对应的 `HexView`；
- 同一基础 Mesh 被多个 Hex 使用；
- 场景中不生成 `MeshCollider`；
- Rebuild 和销毁后生成对象及 Renderer 自建资源被释放；
- 外部 Material 在 Rebuild 和销毁后仍由外部拥有；
- 屏幕坐标经 `HexMapPicker` 转换后能得到正确的 XY/XZ Hex；
- 射线平行于地图平面时得到 `NoPlaneHit`；
- 地图外坐标得到 `OutsideMap`；完整半径地图内的坐标都有对应 Cell；
- `HexMapPicker` 不调用 Physics Raycast；
- 逻辑层仅依赖 `HexMapView`、`HexView` 和 `HexAppearance`，不依赖生成的 Unity 组件。

### SRP Batcher 验证

最终 Shader 和 Pipeline 确定后再执行：

1. 确认所有 Hex 使用预期的共享 Material 和 Shader Variant。
2. 确认 per-object 颜色数据路径不会让逻辑层依赖具体 Shader。
3. 使用 Unity Profiler 或 Frame Debugger 检查实际批处理结果。
4. 记录目标平台、地图规模、状态更新规模和渲染后端配置。

没有最终 Shader 和 SRP Pipeline 时，不把 SRP Batcher 的实际命中率作为本次 View 重构的完成条件；本次重构的完成条件是架构没有主动封死这条路径。

## 实现顺序和完成条件

建议按以下顺序实现：

1. 新增 `HexAppearance`、`HexMapRenderConfig` 和带 Generation 的内部 RenderHandle。
2. 新增 `HexView`，实现身份、有效性和表现缓存。
3. 把共享基础 Mesh 生成和生成对象资源管理移入 `HexMapRenderer`。
4. 将 `HexMapView.Build` 改为创建 Map、Layout、Renderer，并发布查询能力。
5. 增加 `TryGetHexView`、世界坐标转换和世界点便利查询。
6. 移除渲染路径中的 MeshCollider 创建和销毁逻辑。
7. 将 `HexMapPicker` 改为屏幕射线与地图平面求交，不再调用 Physics。
8. 增加 EditMode 和 PlayMode 验收测试。
9. 检查 `HexMapPicker`、场景配置和现有测试的调用方，保持兼容或明确迁移。
10. 在 Unity 编译、测试和场景验证通过后，再评估是否需要批量表现提交 API。

实现完成必须同时满足：

- 15 项已确认验收标准全部通过；
- 逻辑层没有新增对 Unity 生成对象的依赖；
- Rebuild 生命周期和资源所有权有测试覆盖；
- 无 Collider 拾取路径在 XY/XZ 和 Transform 变换下正确；
- 未来替换渲染后端不需要修改逻辑层的 Hex 身份和表现接口。

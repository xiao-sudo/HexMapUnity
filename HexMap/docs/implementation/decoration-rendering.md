# 装饰物渲染与资源生命周期

本文说明装饰物（Decoration）、覆盖物（Overlay）与 HexMap 的分层渲染如何落地，以及每个资源由谁创建、由谁持有、在什么时机释放。

决策依据见 [`docs/adr/0001-decoration-overlay-render-layers.md`](../adr/0001-decoration-overlay-render-layers.md)；排序规则的实测证据见 [`docs/reference/unity-render-order-rules.md`](../reference/unity-render-order-rules.md)。

## 1. 需求与实现对应

| 需求 | 实现 |
| --- | --- |
| 装饰物用 SRP Batch 渲染 | 一纹理一材质，材质由 `(Texture, Queue)` 派生；装饰物几何无逐实例数据，不使用 `MaterialPropertyBlock`，因此对 SRP Batcher 兼容 |
| HexMap 仍用 GPU Instancing | 未改动，`DrawMeshInstancedStrategy` 原样保留 |
| 装饰物在 HexMap 之下 | 装饰物 `sortingOrder = -100`，HexMap 基线 `0`，小者先画 |
| 覆盖物在 HexMap 之上 | 同一个 Prefab 把 `sortingOrder` 改成 `100`，不需要另一套组件或 Shader |
| 一装饰一 Prefab、放进场景可预览 | 根挂 `DecorationView`、子物体持 `MeshFilter` + `MeshRenderer`；`[ExecuteAlways]` 让编辑期直接可见 |

## 2. 分层契约

排序键的实测顺序是 `sortingLayer → sortingOrder → renderQueue`。**`sortingOrder` 优先于 `renderQueue`**，所以分层由 `sortingOrder` 承担，队列号只保证物体留在透明带内。

| 带 | `sortingOrder` | `renderQueue` | 常量 |
| --- | --- | --- | --- |
| 装饰物 | **-100** | 2800 | `DecorationQueue.DecorationSortingOrder` / `.Decoration` |
| HexMap | **0（基线）** | 3000 | `DecorationQueue.HexMapSortingOrder` / `.HexMap` |
| 覆盖物 | **100** | 3005 | `DecorationQueue.OverlaySortingOrder` / `.Overlay` |
| 后续特效 | 继续取正值 | 留在透明带内 | — |

**排序号小者先画**，因此任何要画在 HexMap 之下的带必须取负值。实测边界：装饰物排序号在 `-2 / -1 / 0` 时 HexMap 盖住装饰物，在 `1` 时装饰物盖住 HexMap。

`HexMapSortingOrder` 是**声明性常量**：`Graphics.DrawMeshInstanced` 没有排序参数也不产生 `Renderer`，所以实例化绘制的 HexMap 被钉在 0 上，只有 `MeshRendererStrategy` 会真的把它写到 cell 上。两条路径都是 0 属于默认值巧合。

## 3. 数据流

```
Sprite (资产)
  │
  ├─ DecorationMeshFactory.GetOrCreateMesh(sprite)
  │     ├─ 取 Sprite.vertices / .triangles / .uv
  │     ├─ 以 Sprite.bounds.center 居中
  │     ├─ 校验：无几何、UV 数与顶点数不匹配、索引数非 3 的倍数、索引越界 → 报错并返回 null
  │     └─ 产出并缓存 Mesh（键 = sprite.GetInstanceID()）
  │
  ├─ DecorationMaterialCache.GetOrCreateMaterial(texture, shader, queue)
  │     ├─ 键 = (texture.GetInstanceID(), queue)
  │     ├─ new Material(shader) { renderQueue = queue }
  │     └─ 设 _BaseMap = texture、_BaseColor = 白
  │
  └─ DecorationView.Apply()
        ├─ 解析子物体的 MeshFilter / MeshRenderer
        ├─ 赋 sharedMesh、sharedMaterial
        ├─ DecorationRenderSettings.Apply(renderer)  ← 关阴影/探针/运动矢量
        ├─ renderer.sortingOrder = m_SortingOrder
        └─ renderer.enabled = m_Visible
```

**UV 直通是硬约束。** 正因为 UV 取自 `Sprite.uv`，Sprite Atlas 的重映射、Trim、自定义 Pivot 三者自动成立，实现里没有任何判断分支。绝不使用顶点与 UV 都硬编码 0~1 的单位 Quad —— 那会采样到整张图集页。

**网格拓扑跟随导入设置。** `Full Rect` 得到一个 Quad，`Tight` 得到 Unity 为不透明区域生成的轮廓网格；实现不假设顶点数。

## 4. 组件与 Prefab

```
DecorationView Prefab
├── 根 GameObject   →  DecorationView 组件
└── 子 GameObject   →  MeshFilter + MeshRenderer
```

`DecorationView` 的序列化字段：

| 字段 | 默认 | 说明 |
| --- | --- | --- |
| `m_Sprite` | 空 | 纹理与几何的唯一来源 |
| `m_Queue` | `DecorationQueue.Decoration` | 材质标识；只读属性 `Queue` |
| `m_SortingOrder` | `DecorationQueue.DecorationSortingOrder` | 所属带；只读属性 `SortingOrder` |
| `m_Visible` | `true` | 可读写属性 `Visible` |
| `m_MeshFilter` | 空 | 留空则自动在自身与子物体中查找 |
| `m_Shader` | 空 | 留空则回退 `Shader.Find("HexMap/Decoration")`；显式赋值可防打包时 Shader 被剥离 |

`Queue` 与 `SortingOrder` **刻意只读**：前者改值会派生新的常驻材质，后者是摆放设置而非运行时状态。两者都用反射在测试里写入，模拟 Prefab 资产。

**无 `m_Visible` 的真相来源冲突**：`Visible` 可写，因为它只切换渲染器开关，不派生任何资源。

## 5. 资源清单与归属

| 资源 | 谁创建 | 谁持有 | 生命周期 |
| --- | --- | --- | --- |
| `DecorationView` Prefab | 人（可用菜单 `HexMap/Create Decoration Prefab` 建骨架） | 版本控制 | 资产 |
| `Sprite` | 美术导入 | 版本控制；Prefab 持有引用 | 资产 |
| `Texture2D` | 美术导入或 Sprite Atlas 产出 | `Sprite` 持有 | 资产 / 图集页 |
| Shader `HexMap/Decoration` | 仓库资产 | 版本控制 | 资产 |
| **`Mesh`（装饰物几何）** | `DecorationMeshFactory` | **静态缓存 `s_Meshes`** | 见下节 |
| **`Material`（派生材质）** | `DecorationMaterialCache` | **静态缓存 `s_Materials`** | 见下节 |
| `MeshRenderer` / `MeshFilter` | Prefab 结构（人或菜单创建） | 场景实例 | 随 GameObject |
| HexMap `Mesh` / `Material` | `HexCellMeshFactory` / 调用方 | 策略持有 | 由 `HexMapRenderer.Dispose()` 释放 |

**两个缓存都是静态的，且刻意不挂在任何组件上。** 理由：一份 Mesh 可能被同一 Sprite 的多个装饰物共享，一份 Material 被同一 `(纹理, 队列)` 的多个装饰物共享。若由某个装饰物在 `OnDestroy` 里释放，会误伤邻居。

## 6. 生命周期详解

### 6.1 静态缓存的创建

```
第一个用到该 Sprite 的 DecorationView.Apply()
    → DecorationMeshFactory 建 Mesh，标 HideFlags.DontSave，存入 s_Meshes
第一个用到该 (纹理, 队列) 的 DecorationView.Apply()
    → DecorationMaterialCache 建 Material，标 HideFlags.DontSave，存入 s_Materials
后续同键请求直接命中缓存，不重建
```

`HideFlags.DontSave` 的用途是**防止编辑期生成的 Mesh/Material 被写进场景或 Prefab 文件**。它们不是资产，也不该出现在 `.prefab` 里。

### 6.2 清理时机 —— 唯一正确的挂点

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ClearBeforeFirstSceneLoads()
{
    DecorationMeshFactory.Clear();
    DecorationMaterialCache.Clear();
}
```

进入 Play 的顺序：

```
1. SubsystemRegistration  → 清空缓存（此时还没有任何场景对象）   ← 挂在这里
2. 场景对象创建 + OnEnable → DecorationView.Apply() 重建所需资源
3. AfterSceneLoad         → 若在此清理，会把第 2 步刚赋给 MeshFilter 的 Mesh 销毁
```

**这是本特性真实踩过的缺陷。** 清理挂在 `AfterSceneLoad` 时，表现是「编辑模式正常、一进入 Play 装饰物的 `MeshFilter` 就没有引用」：`OnEnable` 刚建好 Mesh 并赋值，随后被清理销毁，`MeshFilter` 就持有一个已销毁的对象。

**不要往这个类里再加清理点。** `SubsystemRegistration` 是唯一在场景对象存在之前的时机。

### 6.3 编辑期与运行时

两条路径是**同一条代码**：`Apply()` 里没有 `#if UNITY_EDITOR` 分支。

| | 编辑模式 | Play 模式 |
| --- | --- | --- |
| 谁触发 | `[ExecuteAlways]` → `OnEnable` / `OnValidate` | `OnEnable` |
| 资源 | 静态缓存，`DontSave` | 静态缓存，`DontSave` |
| 可见性 | 场景视图直接显示 MeshRenderer | 相机渲染 |
| 缓存起点 | 编辑器会话内持续 | `SubsystemRegistration` 清空后重建 |

### 6.4 显隐切换

```
DecorationView.Visible = false
    → m_Visible = false → Apply() → renderer.enabled = false
```

**不重建几何、不改变排序位置、不释放资源。** 隐藏的装饰物保留全部配置，重新显示时零成本。

### 6.5 纹理销毁

当前**没有**纹理销毁路径：纹理是资产或图集页，生命周期长于任何装饰物。`DecorationMaterialCache.ReleaseTexture` 曾按旧实现照搬进来，但因**零调用点**（无人持有可靠的销毁时机，且共享材质会被邻居误伤）已按死代码删除。

### 6.6 整体时序

```
[进入 Play]
  SubsystemRegistration → 清空 s_Meshes / s_Materials
  ↓
  场景加载 → 每个 DecorationView.OnEnable → Apply()
      → 按需建 Mesh / Material，命中缓存则复用
  ↓
  [运行期] Visible 切换只改 renderer.enabled；队列与排序号不变
  ↓
  [退出 Play] 静态缓存保留（编辑器会话内），DontSave 保证不落盘
  ↓
  [再次进入 Play] SubsystemRegistration 再次清空 → 重建
```

## 7. 性能特征

| 指标 | 值 | 说明 |
| --- | --- | --- |
| SetPass Call | 1~2 | 同一 Shader 的多个材质由 SRP Batcher 合并状态切换 |
| Draw Call | = 纹理数（按 50 以内规划） | SRP Batcher 不跨材质合并 draw call |
| 材质数 | = 纹理数 × 实际用到的队列数（≤ 100） | 每个材质在 GPU 常驻一份 `UnityPerMaterial` 常量缓冲 |
| 排序号 | 不增加材质数 | 它是 `Renderer` 属性而非材质属性 |

`DrawMeshInstanced` 与 SRP Batcher 互斥，两者分属不同带，互不影响。

## 8. 陷阱清单

这些都是本特性实现过程中**真实发生过**的，不是推演：

1. **低于 HexMap 的带必须取负排序号。** 任何正整数都会盖住地图，无论多小。已刻意不加符号校验（同一个组件要靠正值变成覆盖物），代价是「加特效 = 50」这类新带会静默压住地图。
2. **不要用 `sortingLayer` 给实例化绘制的物体分带。** 它只能经 `Renderer.sortingLayerID` 设置，而 `DrawMeshInstanced` / `RenderMeshInstanced` 没有排序参数也不产生 `Renderer`（17 个重载只带渲染 layer，`RenderParams` 无排序字段）。这是本仓库试过并放弃的方案。
3. **不要用 `rendererPriority` 做跨带排序。** 它只作用于同一材质/Shader 批次内部。
4. **`Sprite.Create(..., SpriteMeshType.Tight)` 不产生轮廓网格。** 运行时创建总是 4 顶点矩形；轮廓只能由导入管线按 alpha 与 Tessellation Detail 生成。
5. **`Sprite.uv` 在程序化 Sprite 上恒为单位方格。** 想验证「UV 直通」是否退化成硬编码 Quad，必须用**子矩形** Sprite：`Sprite.Create` 的 `rect` 可以只覆盖纹理一块，此时 `Sprite.uv` 只寻址那一角。
6. **`Mesh.triangles` 要 `int[]`，而 `Sprite.triangles` 是 `ushort[]`；`Sprite.vertices` 是 `Vector2[]` 不是 `Vector3[]`。**
7. **`Renderer` 不继承 `Behaviour`。** 它没有 `isActiveAndEnabled`；用 `gameObject.activeInHierarchy`。
8. **清理静态缓存不能挂在 `AfterSceneLoad`。** 见 6.2。

## 9. 如何新增一个带

需要「再插一层特效」时，按这个顺序做：

1. **定号**：在 `DecorationQueue` 加一对常量（排序号 + 队列号），并在注释里写明它相对基线的位置。
2. **判符号**：要画在 HexMap **之下** → 取负值；**之上** → 取正值。**这是唯一容易错的一步**，且错了的表现是「某层静默消失或被压住」，不是报错。
3. **复用组件**：`DecorationView` 已经支持任意 `(队列, 排序号)`，通常不需要新组件或新 Shader。
4. **加断言**：在 `DecorationViewEditModeTests.TheBandsAreOrderedAroundTheHexMapBaseline` 里补一条相对基线的符号断言 —— 这是唯一能自动守住该带位置的测试。
5. **加像素守卫**：在 `DecorationLayerOrderTests` 加一个「该带相对 HexMap 的覆盖关系」用例。
6. **只有在需要新视觉能力时**才动 Shader；加关键字会翻倍 Shader 变体，并可能打掉 SetPass 预算。

## 10. 测试覆盖与已知空缺

| 接缝 | 位置 | 覆盖 |
| --- | --- | --- |
| `DecorationMeshFactory` | `DecorationGeometryTests`（EditMode） | 居中、UV 直通（子矩形 Sprite）、Tight 轮廓保留、缓存复用、清理 |
| `DecorationMaterialCache` | 同上 | 同键复用、异队列隔离、`_BaseMap`、空输入报错且返回 null、`Clear()` |
| `DecorationView` 编辑期装配 | `DecorationViewEditModeTests`（EditMode） | `[ExecuteAlways]` → `OnEnable` → `Apply()` 链路、渲染器配置、初始隐藏、带序符号契约 |
| 跨带层序 | `DecorationLayerOrderTests`（PlayMode） | HexMap 盖住装饰物、覆盖物盖住 HexMap、隐藏装饰物 |
| 装饰物渲染与相机移动 | `DecorationRenderingTests`（PlayMode） | 半透明 Hex 混合、相机平移缩放稳定、显隐、队列隔离 |

**已知空缺**：

- **`DecorationCacheLifetime` 零自动化覆盖。** 要覆盖它需要「装饰物在场景里、真的进入 Play」的测试，即一个测试专用场景 + `SceneManager.LoadScene`；仓库当前没有测试专用场景资产，且该缺陷类进 Play 即可见，故决定不做。
- **实例化绘制路径下的层序未自动化覆盖。** 所有跨带测试都用 `DrawMeshInstanced` 搭 HexMap，但**测试里的 HexMap 是当帧新建的**，与「装饰物作为 Prefab 随场景加载」的生产路径不同。生产场景的层序仍需 Frame Debugger 人工确认（见第 11 节）。
- **真实 Sprite Atlas 资产未自动化覆盖。** 子矩形 Sprite 复现了图集的几何情形，但打包这一步本身留给人工验证。
- **`GetInstanceID()` 被复用的理论风险。** 两个缓存都以实例 ID 为键；若对象被销毁后 ID 被复用，可能命中陈旧网格。静态地图场景下不可达，未加防护。

## 11. 验证方法

自动化测试之外，层序必须在**生产场景**（`map.unity`，走 `DrawMeshInstanced`）用 Frame Debugger 确认，因为现有测试只覆盖 `MeshRenderer` 策略这一条路径：

1. 装饰物的所有 draw 排在 Hex 的 draw **之前**；
2. 覆盖物的 draw 排在 Hex 的 draw **之后**；
3. `Decoration.shader` 在 Inspector 中显示 SRP Batcher **compatible**；
4. 把一个打包进 Sprite Atlas 的 Sprite 放到装饰物上，确认渲染的是该 Sprite 本身而不是整张图集的一角。

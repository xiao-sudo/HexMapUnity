# 运行时 Hex 地图与格子拾取

## 目的

本模块把有限半径的 Hex 地图配置生成成可查询、可遍历的 `HexCell` 集合，并把 Unity 场景点击转换为对应的 `HexCell`。它不负责 GVG 地块归属、阻挡、开放状态、战斗或寻路规则。

## 代码地图

- `Assets/Scripts/HexMap/Core/HexCoord.cs`：Axial `(q, r)` 坐标，并派生 `s = -q - r`。
- `Assets/Scripts/HexMap/Core/HexLayout.cs`：局部世界坐标与 Hex 坐标的转换。
- `Assets/Scripts/HexMap/Runtime/HexMapRadius.cs`：地图半径、Cube 合法范围和理论格子数量。
- `Assets/Scripts/HexMap/Runtime/HexMapDefinition.cs`：半径和排除坐标的不可变配置及校验。
- `Assets/Scripts/HexMap/Runtime/HexMap.cs`：按半径生成 `HexCell`，维护坐标字典和稳定遍历顺序。
- `Assets/Scripts/HexMap/Runtime/HexCellQuery.cs`：`Found`、`Missing` 和 `OutsideMap` 查询结果。
- `Assets/Scripts/HexMap/UnityRuntime/HexMapConfigAsset.cs`：Inspector 配置到运行时定义的适配层。
- `Assets/Scripts/HexMap/UnityRuntime/HexMapView.cs`：生成格子表现和 `MeshCollider`。
- `Assets/Scripts/HexMap/UnityRuntime/HexMapPicker.cs`：Raycast、空间转换和地图查询。
- `Assets/Scenes/HexMapSampleConfig.asset`：SampleScene 的半径配置。

## 运行流程

### 地图生成

1. `HexMapConfigAsset.CreateDefinition()` 读取 `radius` 和排除坐标。
2. `HexMapDefinition` 创建 `HexMapRadius`，拒绝负半径、超大半径、重复排除坐标和越界排除坐标。
3. `HexMap` 按 `q` 升序、同一 `q` 下 `r` 升序生成所有未排除的格子。
4. `HexMapView.Build()` 使用相同的布局参数，为每个格子建立表现对象和碰撞体。

### 半径语义

半径为 `R` 时，合法坐标必须满足：

```text
|q| <= R
|r| <= R
|s| <= R
s = -q - r
```

这会生成以 `(0, 0, 0)` 为中心的标准 Cube 六边形，而不是任意的 `q/r` 矩形。为了避免遍历矩形后过滤，生成器直接计算每个 `q` 的 `r` 范围：

```text
q = -R .. R
r = max(-R, -q-R) .. min(R, -q+R)
```

完整地图格子数为 `1 + 3 * R * (R + 1)`；半径 0、1、2、3 分别是 1、7、19、37 格。`HexMapRadius.MaxSupportedRadius` 当前为 26754，以确保数量和集合容量仍可由 `int` 表示。

### 坐标查询

`HexMap.Query(coordinate)` 先用 `HexMapRadius.Contains` 判断三个 Cube 分量：

- `Found`：坐标在半径内且未被排除，返回对应 `HexCell`。
- `Missing`：坐标在半径内但被排除，没有对应的 `HexCell`。
- `OutsideMap`：至少一个 Cube 分量超出半径。

查询失败是正常业务结果，不抛出异常，也不会复用上一次成功查询的格子；配置错误在创建 `HexMapDefinition` 时拒绝。

### 点击拾取

拾取链路必须保持：

```text
屏幕点 -> Camera.ScreenPointToRay -> Physics.Raycast
-> HexMapView.OwnsCollider -> mapRoot.InverseTransformPoint
-> HexLayout.WorldToHex -> HexMap.Query -> Found 时返回 HexCell
```

`HexMapView` 和 `HexMapPicker` 必须共享同一个 `HexLayout` 和地图根节点空间。Raycast 未命中、命中非地图碰撞体、坐标在半径外或坐标被排除时，都返回失败；拾取器不能猜测最近格子或返回上一次结果。

## 实现笔记

`HexCoord` 是格子的唯一键。`HexMap` 同时维护 `Dictionary<HexCoord, HexCell>` 和只读 `IReadOnlyList<HexCell>`，分别用于坐标查询和稳定遍历。生成完成后两者保持一致；替换地图时创建新的 `HexMap` 实例，不要在遍历期间修改字典。

`HexMapConfigAsset` 只负责 Unity 序列化适配，不把 Unity 类型带入 `HexMap.Runtime`。排除坐标继续序列化 `q/r`，`s` 由 `HexCoord` 派生，不重复保存。`Assets/Scenes/HexMapSampleConfig.asset` 当前使用 `radius: 3`。

## 扩展点

- 增加格子数据：先扩展 `HexCell`、`HexMap` 的生成输入，再扩展配置 DTO；不要让渲染 GameObject 成为格子数据来源。
- 更换渲染方式：只修改 `HexMapView` 及资源接线；位置统一调用 `HexLayout.HexToWorld`，拾取统一调用 `HexLayout.WorldToHex`。
- 增加 GVG 规则：通过 `HexMap.Query` 获取格子，再独立判断归属、阻挡、开放和可通行状态；不要删除 `HexCell` 来表达这些规则。
- 增加悬停、高亮或拖拽：建立在 `HexMapPicker` 查询结果上，不让拾取器直接触发 GVG 规则。

## 约束

- 地图范围必须使用 Cube 半径条件，不要重新引入 `minQ/maxQ/minR/maxR` 矩形字段。
- `HexMap.Runtime` 不得依赖 `Camera`、`Physics.Raycast`、`GameObject`、`MonoBehaviour` 或场景资源。
- `HexMap.Core` 不得反向依赖运行时地图程序集。
- Collider 命中只证明命中了物理对象，最终格子必须经过 `WorldToHex` 和 `HexMap.Query` 确认。
- 基础地图的“存在”、GVG 的“可通行”和玩法的“归属”必须保持独立。
- SampleScene 不是项目默认启动入口；验证时直接打开 `Assets/Scenes/SampleScene.unity`，除非明确修改 Build Settings。

## 验证

以下测试和场景验证由开发者在 Unity 中执行：

1. EditMode 确认半径 1 为 7 格、半径 2 为 19 格，并且每个格子满足三个 Cube 分量的半径约束。
2. EditMode 确认 `Found`、`Missing`、`OutsideMap` 可区分，并确认非法配置会被拒绝。
3. SampleScene 确认 `radius: 3` 生成标准六边形，点击多个格子得到正确 q/r。
4. 移动、旋转或缩放地图根节点后，确认 View 和 Picker 仍使用同一局部空间。
5. 点击空白区域、非地图 Collider、地图外区域和排除格子时，确认不会返回错误或上一次格子。

本次实现只进行静态检查和程序集编译检查，不代替开发者运行上述测试。
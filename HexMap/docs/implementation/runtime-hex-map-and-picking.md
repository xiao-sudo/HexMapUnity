# 运行时 Hex 地图与格子拾取

## 目的

本模块把有限半径的完整 Hex 地图生成成可查询、可遍历的 `HexCell` 集合，并把 Unity 世界坐标转换为对应的格子。它不负责 GVG 地块归属、阻挡、开放状态、战斗或寻路规则。

## 代码地图

- `Assets/Scripts/HexMap/Core/HexCoord.cs`：Axial `(q, r)` 坐标，并派生 `s = -q - r`。
- `Assets/Scripts/HexMap/Core/HexLayout.cs`：局部空间与 Hex 坐标之间的转换。
- `Assets/Scripts/HexMap/Runtime/HexMapRadius.cs`：地图半径、Cube 合法范围和理论格子数量。
- `Assets/Scripts/HexMap/Runtime/HexMapDefinition.cs`：不可变的半径定义及校验。
- `Assets/Scripts/HexMap/Runtime/HexMap.cs`：按半径生成 `HexCell`，维护坐标、Id 字典和稳定遍历顺序。
- `Assets/Scripts/HexMap/Runtime/HexCellQuery.cs`：`Found`、`Missing` 和 `OutsideMap` 查询结果。
- `Assets/Scripts/HexMap/UnityRuntime/HexMapView.cs`：直接配置半径、布局和渲染参数，并生成地图表现。
- `Assets/Scripts/HexMap/UnityRuntime/HexMapPicker.cs`：世界坐标、地图平面和地图查询之间的拾取适配。

## 运行流程

### 地图生成

1. `HexMapView.Radius` 提供地图半径。
2. `HexMapDefinition` 创建并校验 `HexMapRadius`，拒绝负半径和超大半径。
3. `HexMap` 生成半径内的所有坐标，不存在地图内的空缺。
4. `HexMapView.Build()` 使用相同的布局参数，为每个 Cell 建立表现对象。

### 半径语义

半径为 `R` 时，合法坐标必须满足：

```text
|q| <= R
|r| <= R
|s| <= R
s = -q - r
```

这会生成以 `(0, 0, 0)` 为中心的标准 Cube 六边形，而不是任意的 `q/r` 矩形。生成器直接计算每个 `q` 对应的 `r` 范围：

```text
q = -R .. R
r = max(-R, -q-R) .. min(R, -q+R)
```

完整地图格子数为 `1 + 3 * R * (R + 1)`；半径 0、1、2、3 分别是 1、7、19、37 格。`HexMapRadius.MaxSupportedRadius` 当前为 26754，以确保数量和集合容量仍可由 `int` 表示。

### 坐标和 Id 查询

`HexMap.Query(coordinate)` 先用 `HexMapRadius.Contains` 判断三个 Cube 分量：

- `Found`：坐标在半径内，返回对应 `HexCell`。
- `OutsideMap`：至少一个 Cube 分量超出半径。

`HexMap.Query(cellId)` 对不存在的 Id 返回 `Missing`。查询失败是正常业务结果，不抛出异常，也不会复用上一次成功查询的格子。

Cell Id 从中心 `Hex(0, 0)` 的 0 开始，按距离向外扩展；相同环上的顺序是确定的。半径扩大时，已有坐标的 Id 保持不变。

### 世界坐标拾取

拾取链路保持为：

```text
屏幕点 -> Camera.ScreenPointToRay -> Physics.Raycast
-> HexMapPicker -> HexMapView.WorldToMapLocal
-> HexLayout.WorldToHex -> HexMap.Query -> Found 时返回 HexCell
```

`HexMapView` 和 `HexMapPicker` 必须共享同一个 `HexLayout` 和地图根节点空间。Raycast 未命中、命中非地图碰撞体、坐标在半径外或点不在地图平面时，都返回失败；拾取器不能猜测最近格子或返回上一次结果。

## 实现约束

`HexCoord` 是格子的唯一坐标键。`HexMap` 同时维护 `Dictionary<HexCoord, HexCell>` 和只读 `IReadOnlyList<HexCell>`，分别用于坐标查询和稳定遍历。地图替换时创建新的 `HexMap` 实例，不在遍历期间修改字典。

基础地图的“存在”与 GVG 的“可通行”、玩法的“归属”必须保持独立。后续规则应通过 `HexMap.Query` 获取格子，再在其他模块中判断这些状态。

## 扩展点

- 增加格子数据：先扩展 `HexCell`、`HexMap` 的生成输入，再扩展 View 层配置。
- 更换渲染方式：只修改 `HexMapView` 及资源接线；位置统一调用 `HexLayout.HexToWorld`，拾取统一调用 `HexLayout.WorldToHex`。
- 增加悬停或高亮：建立在 `HexMapPicker` 查询结果之上，不让拾取器直接触发 GVG 规则。

## 验证

以下测试和场景验证由开发者在 Unity 中执行：

1. EditMode 确认半径 1、2 分别生成 7、19 个完整格子，并确认每个坐标满足 Cube 半径约束。
2. EditMode 确认中心 Id 为 0、Id 查询可反查坐标、地图外坐标返回 `OutsideMap`。
3. SampleScene 确认 `HexMapView` 直接使用 Radius 生成标准六边形地图。
4. 移动、旋转或缩放地图根节点后，确认 View 和 Picker 仍使用同一局部空间。
5. 点击空白区域、非地图 Collider、地图外区域和平面外位置时，确认不会返回错误或上一次格子。

本次实现只进行静态检查和程序集编译检查，不替代开发者运行 Unity 测试。
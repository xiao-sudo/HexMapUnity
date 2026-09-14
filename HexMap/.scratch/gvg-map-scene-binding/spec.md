# GVG 地图场景绑定

## 目标

将 GVG 逻辑格子数据与地图场景表现解耦：

- `GvgMapAuthoringAsset` 负责编辑逻辑数据、Excel 导入、数据校验和表格导出；
- 场景中的 `HexMapView` 负责地图拓扑和显示配置；
- 编辑器将两者绑定，使逻辑格子可以在真实地图表现上编辑；
- 运行时使用场景配置和逻辑数据表，不依赖编辑器 Asset 或编辑窗口。

## 已确定的决策

- 复用 `HexMapView` 作为 Radius、Orientation、Plane、OuterRadius、SecondaryScale、Origin 的配置来源。
- `GvgMapAuthoringAsset` 不再维护 Radius，也不再独立维护地图拓扑。
- 场景侧建立绑定关系，由场景对象引用 `GvgMapAuthoringAsset`；Asset 不反向引用场景对象。
- 显示配置不加入逻辑 Excel/CSV 表格。
- 运行时根据场景地图配置创建地图，并读取 MapId 对应的逻辑数据表。
- 运行时校验表格中的逻辑格子是否属于场景地图拓扑。
- GVG 编辑工具激活且鼠标位于逻辑地图范围内时，SceneView 输入优先交给 GVG 编辑逻辑。

## 数据归属

| 数据 | 唯一来源 |
| --- | --- |
| HexId、PlotType、时间层、归属关系 | GvgMapAuthoringAsset 和导出表格 |
| Radius、Orientation、Plane | HexMapView |
| OuterRadius、SecondaryScale、Origin | HexMapView |
| 背景、地形、装饰物 | 地图场景 Prefab |
| 运行时 Plot 和 RuntimeHexMap | 运行时根据表格和 HexMapView 创建 |

## 交付顺序

1. 让 HexMapView 成为场景地图配置的唯一来源；
2. 建立场景地图与 GvgMapAuthoringAsset 的绑定；
3. 在绑定的真实场景中编辑 GVG 逻辑格子；
4. 使用绑定地图拓扑校验逻辑数据导入导出；
5. 运行时组合场景配置和数据表；
6. 迁移旧地图并移除重复配置路径。

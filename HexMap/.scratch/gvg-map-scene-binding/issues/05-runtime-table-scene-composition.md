# 05 — 运行时组合场景配置和逻辑数据表

**要构建的内容：**  
运行时使用场景中的 `HexMapView` 配置创建地图拓扑和渲染布局，并使用 MapId 对应的逻辑数据表创建 Plot 和其他运行时逻辑。运行时不依赖 `GvgMapAuthoringAsset` 或 GVG 编辑窗口。

**Blocked by:** 01 — 让 HexMapView 成为场景地图配置来源；04 — 使用绑定地图拓扑校验逻辑表格数据。

**Status:** ready-for-agent

- [ ] 运行时可以根据地图身份找到对应的逻辑数据表。
- [ ] RuntimeHexMap 使用 `HexMapView` 的 Radius 和布局配置创建。
- [ ] 表格中的 Plot 数据可以转换为现有运行时 Plot 和 PlotRegistry 结构。
- [ ] 表格中的 HexId 超出场景拓扑时，运行时显示明确的校验错误。
- [ ] Plane、Orientation、OuterRadius、SecondaryScale 和 Origin 正确影响运行时渲染。
- [ ] 运行时不需要依赖编辑器 Asset 或 GVG 编辑窗口。

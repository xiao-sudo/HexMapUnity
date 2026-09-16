# 01 — 让 HexMapView 成为场景地图配置来源

**要构建的内容：**  
让场景中的 `HexMapView` 成为地图拓扑和显示配置的唯一来源。GVG 编辑操作可以从当前场景地图获取 Radius 和布局参数，不再由 `GvgMapAuthoringAsset` 单独维护 Radius。

**Blocked by:** 无，可立即开始。

**Status:** done

- [x] `HexMapView` 成为 Radius、Orientation、Plane、OuterRadius、SecondaryScale、Origin 的权威来源。
- [x] `GvgMapAuthoringAsset` 不再序列化或独立维护 Radius。
- [x] 需要地图定义的现有 GVG 编辑操作都可以从当前场景地图上下文获得拓扑配置。
- [x] 非法拓扑和布局参数能够产生明确的校验错误。
- [x] 现有 HexMap 和 GVG 编辑模式测试继续通过。

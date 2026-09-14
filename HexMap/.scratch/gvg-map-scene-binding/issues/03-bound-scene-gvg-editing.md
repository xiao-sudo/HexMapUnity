# 03 — 在绑定的真实场景中编辑 GVG 逻辑格子

**要构建的内容：**  
GVG 编辑窗口使用绑定的 `HexMapView` 配置和场景坐标绘制逻辑 Hex，使 Plot 选择、绘制和时间层编辑直接作用于真实地图表现中的逻辑格子。

**Blocked by:** 02 — 建立 GVG 逻辑 Asset 与场景地图的绑定。

**Status:** ready-for-agent

- [ ] Handles 使用绑定地图的 Radius、Plane、Orientation、OuterRadius、SecondaryScale、Origin 和场景 Transform。
- [ ] 逻辑 Hex 与背景、地形和其他场景表现正确对齐。
- [ ] 地图编辑模式下点击或拖动逻辑 Hex 时，输入优先交给 GVG 编辑器。
- [ ] Plot 选择、Paint Add、Paint Remove 和时间层编辑都会更新绑定的 Asset。
- [ ] Asset 编辑支持 Undo、Dirty 标记和 SceneView 重绘。
- [ ] 逻辑地图范围之外仍可以选择 GameObject，并保留现有修饰键选择路径。

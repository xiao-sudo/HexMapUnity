# 03 — 在绑定的真实场景中编辑 GVG 逻辑格子

**要构建的内容：**  
GVG 编辑窗口使用绑定的 `HexMapView` 配置和场景坐标绘制逻辑 Hex，使 Plot 选择、绘制和时间层编辑直接作用于真实地图表现中的逻辑格子。

**Blocked by:** 02 — 建立 GVG 逻辑 Asset 与场景地图的绑定。

**Status:** done

- [x] Handles 使用绑定地图的 Radius、Plane、Orientation、OuterRadius、SecondaryScale、Origin 和场景 Transform。
- [x] 逻辑 Hex 与背景、地形和其他场景表现正确对齐。
- [x] 地图编辑模式下点击或拖动逻辑 Hex 时，输入优先交给 GVG 编辑器。
- [x] Plot 选择、Paint Add、Paint Remove 和时间层编辑都会更新绑定的 Asset。
- [x] Asset 编辑支持 Undo、Dirty 标记和 SceneView 重绘。
- [x] 逻辑地图范围之外仍可以选择 GameObject，并保留现有修饰键选择路径。

## Comments

### 实现记录（2026-09-14）

- `GvgMapAuthoringWindow` 接入 `GvgMapBindingResolver`：新增 `TryResolveHealthyBinding` + `DescribeBinding`。
  - SceneView 编辑前先解析绑定；绑定不健康（NotBound / AssetMissing / TopologyMismatch / ViewMissing）时不绘制、不抢输入、不编辑。
  - `DrawMapControls` / `DrawExportControls` 显示诊断 HelpBox；导入/导出在未绑定时禁用。
  - 健康绑定时 SceneView 编辑强制锁定到绑定 Asset（不同则 `ClearSelection()`）。
- 新增 `m_SceneEditing` toggle（默认 true）作为简单激活开关：关闭时不绘制、不抢输入，可正常操作场景对象。
- 编辑更新绑定 Asset：`RecordAndApply` 走 `Undo.RecordObject` + `EditorUtility.SetDirty` + `SceneView.RepaintAll`；挂载 `Undo.undoRedoPerformed` 使 Undo/Redo 触发重绘。
- 对齐：绘制路径 `layout.HexToWorld` + `transform.TransformPoint`，拾取路径 `WorldToMapLocal` + `layout.WorldToHex`，二者共用视图 Transform（平移/旋转/均匀缩放）。
- 输入优先：仅当鼠标命中合法格子时才 `AddDefaultControl` 抢占输入；范围外保留 Unity 默认 GameObject 选择与修饰键选择。
- 测试：`HexMapViewTests.SceneEditingDrawAndPickPathsRoundTripEveryCell`（Radius=2、非零位移+旋转+2x 缩放，遍历每个 cell 验证绘制/拾取往返一致）。
- 验证：`HexMap.Gvg.Editor` / `HexMap.UnityRuntime.Tests.EditMode` / `HexMap.Gvg.Tests.EditMode` 编译 0 错误；实机 EditMode 测试 `HexMap.UnityRuntime.Tests.EditMode` 11/11、`HexMap.Gvg.Tests.EditMode` 36/36 通过。

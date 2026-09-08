# 03 — Hex Grid Debug Draw 与预览

**What to build:** 为 HexMapView 提供编辑器 Scene View 调试绘制，让开发者能在非运行时检查网格边界、Hex 编号和地图预览结果。

**Blocked by:** 02 — 运行时 Hex 地图与格子拾取.

**Status:** ready-for-human

- [x] 编辑器 Scene View 通过 DrawGizmo 绘制实际 Cell 的 Hex 边界和 Cell Id。
- [x] Cell Id 默认显示；Axial 坐标可通过编辑器选项单独开启，标签内容居中对齐到 Hex 中心。
- [x] Debug Draw 配置仅存在于 Unity Editor，不向 Game 窗口或运行时加入调试信息；边界和标签可以独立开关。
- [x] 切换 pointy/flat 或 XY/XZ 布局，以及应用平移、旋转和统一缩放后，绘制位置仍与运行时布局一致。
- [x] 编辑器预览根据 HexMapView 当前半径和布局配置重建临时地图；运行时使用实际生成的地图和布局。

悬停和当前选中 Hex 的高亮不属于本 issue 的最终范围；交互反馈应在后续拾取/选择相关 issue 中实现。

## Implementation notes

- `HexMapDebugDrawEditor` 完全位于 `#if UNITY_EDITOR` 下，仅使用 Scene View 的 `DrawGizmo`、`Handles.Label` 和范围线绘制。
- 标签默认只显示 `#Id`，坐标显示默认关闭；标签样式使用 `MiddleCenter`，保证文本中心落在 Hex 中心。
- Hex 边界使用与 `HexMapRenderer` 相同的 Pointy/Flat 顶点约定，只绘制当前地图中实际存在的 Cell。
- 地图半径直接配置在 `HexMapView`，地图是半径内的完整 Hex 集合，不再使用 `HexMapConfigAsset` 或 Exclude 坐标。

## Verification

- `HexMap.Runtime.csproj` 编译通过。
- `HexMap.UnityRuntime.csproj` 编译通过。
- Runtime、UnityRuntime EditMode 和 PlayMode 测试程序集编译通过。
- Unity/NUnit 测试和 Scene View 视觉验收待人工执行。

## Comments

- 2026-09-08：完成 Debug Draw、标签居中、坐标可选、Hex 边界绘制和半径配置收敛；状态改为 `ready-for-human`，等待 Unity 场景及测试验证。

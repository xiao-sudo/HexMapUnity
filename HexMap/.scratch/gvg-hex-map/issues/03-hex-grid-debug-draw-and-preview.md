# 03 — Hex Grid Debug Draw 与预览

**What to build:** 为已生成的 Hex 地图提供可开关的调试绘制，让开发者能在 Unity 场景中检查网格边界、Hex 编号和地图预览结果。

**Blocked by:** 02 — 运行时 Hex 地图与格子拾取.

**Status:** ready-for-agent

- [ ] 调试绘制显示生成地图的 Hex 边界以及每个 Hex 的 Axial 编号和 Cell 标识。
- [ ] 编辑器预览和运行时查看都可以独立开关，不改变正式地图或 GVG 玩法状态。
- [ ] 鼠标悬停和当前选中的 Hex 能够高亮，且高亮信息与实际拾取结果一致。
- [ ] 切换 pointy/flat 或 XY/XZ 布局后，绘制位置、编号和地图预览仍与运行时数据一致。

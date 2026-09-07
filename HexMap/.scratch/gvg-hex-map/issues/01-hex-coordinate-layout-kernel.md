# 01 — Hex 坐标与布局内核

**What to build:** 提供可复用的 Hex 坐标和布局能力，让运行时、编辑器和测试可以使用同一套坐标数学。

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] Axial 坐标作为运行时主坐标，Cube 坐标可稳定推导并保持 `q + r + s = 0`；坐标支持相等比较和哈希。
- [ ] 六邻居查询和 Hex 距离计算在合成地图上得到正确结果。
- [ ] pointy top、flat top、XY 平面和 XZ 平面均支持 HexToWorld/WorldToHex 往返，边界位置使用正确的 cube rounding。
- [ ] 通过纯 C# EditMode 测试验证上述外部行为。

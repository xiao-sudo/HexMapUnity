# 02 — 运行时 Hex 地图与格子拾取

**What to build:** 根据地图配置生成可查询、可遍历的 HexCell 地图，并让玩家点击场景中的格子时得到对应的 HexCell。

**Blocked by:** 01 — Hex 坐标与布局内核.

**Status:** ready-for-agent

- [ ] 地图可以从配置生成有限的 HexCell 集合，并按 Hex 坐标查询和遍历。
- [ ] 有效坐标、地图外坐标和不存在的坐标具有明确且可测试的结果。
- [ ] 屏幕点经过 Raycast、世界坐标转换后能定位到正确 HexCell，未命中地图时不会返回错误格子。
- [ ] Sample Scene 展示一个可生成、可点击和可检查的基础 Hex 地图。

# HexMap Domain Context

## Glossary

- 地图（Map）：以 Hex 坐标组成的基础空间集合，表示哪些 Cell 在地图中存在。
- 地图半径（Radius）：从中心 Hex `(0, 0)` 向外扩展的 Cube 距离上限；半径内的坐标构成完整六边形。
- Cell：地图中的一个基础 Hex 单元，拥有稳定的坐标和唯一 Id。
- 坐标（Coordinate）：Cell 的 Axial `(q, r)` 坐标；`s` 由 `q` 和 `r` 推导，不单独作为业务配置。
- 布局外半径（Outer Radius）：Hex 几何尺寸，表示中心到顶点的距离；它与地图半径是不同概念。
- 地图外（Outside Map）：坐标超出地图 Radius 的状态。

## Domain Boundaries

基础地图是半径内的完整 Hex 集合，不包含地图内的空缺。Cell 的存在与 GVG 的归属、可通行性、阻挡和其他玩法状态相互独立。
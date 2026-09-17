# Hex Cell 透明六边框与实例化渐变渲染规格

**Status:** ready-for-agent

## 问题陈述

当前 Hex Cell 使用不透明的纯色填充 Shader，无法表现透明六边框，也无法让同一共享材质下的不同 Cell 分别使用窄纯色边框或宽渐变边框。

当前生成的 Hex 网格是中心顶点加六个外圈顶点组成的三角扇，顶点只有位置属性。如果 Shader 依赖 Plane、Orientation、OuterRadius 或 SecondaryScale 等布局参数，就会复制 HexLayout 的几何知识，难以保证 XY/XZ、Pointy/Flat 和不同纵横比的一致行为。

当前 HexAppearance 只有可见性和颜色，HexRenderHandle 只通过 MaterialPropertyBlock 设置 _BaseColor，无法为每个 Hex 传递边框宽度、渐变开关和渐变曲线参数。

## 解决方案

将 Hex Cell 的渲染 Shader 改为 Transparent，并使用网格自身提供的归一化中心权重计算六边框。

网格生成器为中心顶点写入 TEXCOORD0.x = 1，为六个外圈顶点写入 TEXCOORD0.x = 0。三角形插值后，Shader 可以得到当前片元从外边缘向中心的归一化距离，不需要知道 Hex 的平面、方向、半径或纵横比例。

HexAppearance 只携带每个 Hex 的颜色和渐变开关。HexRenderHandle 只将 _BaseColor 与 _GradientEnabled 写入 MaterialPropertyBlock；边框宽度和渐变幂次由共享材质统一提供。

## 用户故事

1. 作为地图渲染器，我希望 Hex Cell 使用 Transparent 队列，以便 Cell 后方的场景内容保持可见。
2. 作为地图渲染器，我希望 Shader 使用 Alpha 混合，以便 _BaseColor.a 和内部填充透明度真正影响画面。
3. 作为地图设计者，我希望 Cell 显示为六边形框，以便清晰表达地图格子的边界。
4. 作为地图设计者，我希望默认六边形内部完全透明，以便默认效果是纯六边框。
5. 作为地图设计者，我希望可以增加半透明内部填充，以便同一 Shader 也能表现填充式高亮。
6. 作为地图设计者，我希望通过 _InteriorAlpha 控制内部填充强度，以便填充强度独立于边框宽度。
7. 作为地图设计者，我希望普通 Cell 可以使用窄边框，重点 Cell 可以使用宽边框，以便同一地图表达不同视觉层级。
8. 作为地图设计者，我希望每个 Cell 独立设置渐变开关，以便同一共享材质中的 Cell 可以选择是否使用渐变。
9. 作为地图设计者，我希望渐变只发生在边框宽度内，并从外边缘向内边缘逐渐减弱。
10. 作为地图渲染器，我希望渐变保持 _BaseColor 的颜色语义，只改变透明度而不改变 Cell 颜色身份。
11. 作为游戏逻辑开发者，我希望 HexAppearance 携带每个 Cell 的渐变开关，以便通过现有外观接口选择渐变。
12. 作为游戏逻辑开发者，我希望现有两参数 HexAppearance 构造方式继续有效，并使用默认的渐变开关。
13. 作为渲染器维护者，我希望外观相等性比较包含所有边框参数，以便样式变化可以刷新 MaterialPropertyBlock。
14. 作为渲染器维护者，我希望共享网格提供边框距离输入，以便 Shader 不复制 HexLayout 几何规则。
15. 作为渲染器维护者，我希望 Shader 支持 XY、XZ、Pointy 和 Flat 的全部组合。
16. 作为渲染器维护者，我希望不同 Outer Radius 和 Secondary Scale 不需要 Shader 布局参数。
17. 作为渲染器维护者，我希望缺少边框距离顶点属性的网格不属于支持范围，以便不加入脆弱回退。
18. 作为地图设计者，我希望 Hex 使用硬边界，以免相邻 Hex 之间出现缝隙。
19. 作为地图渲染器，我希望外、内边界不使用片元导数，以便斜视角下也保持连续。
20. 作为地图渲染器，我希望边框遵守正常深度测试且不写入深度，以便场景遮挡关系保持正确。
21. 作为地图渲染器，我接受相邻 Cell 共享边可能绘制两次，以便保留当前每 Cell 一个 Renderer 的架构。
22. 作为游戏逻辑开发者，我希望 Runtime GVG Demo 业务行为不变，以便渲染功能不擅自根据 Plot 语义分配样式。
23. 作为测试作者，我希望验证生成网格的中心权重和外圈权重，以便 Shader 的核心几何输入不会回归。
24. 作为测试作者，我希望验证每 Cell 的实例化颜色和渐变开关，以便测试外观传播而不依赖私有实现。
25. 作为测试作者，我希望验证旧、新 HexAppearance 构造路径，以便同时覆盖兼容性和样式定制。
26. 作为测试作者，我希望验证 XY/XZ、Pointy/Flat、Outer Radius 和 Secondary Scale 矩阵。
27. 作为维护者，我希望规格排除手写 Unity .meta 文件，以便元数据始终由 Unity 生成。

## 实现决策

- 现有实例化纯色 Shader 改为生成 Hex Cell 使用的 Transparent 非光照 Shader。
- Shader 使用 Alpha 混合、正常深度测试并关闭深度写入；渲染队列和渲染类型标记为 Transparent。
- 每 Cell 的 _BaseColor 保持为实例化属性，继续作为源颜色和 Alpha 乘数。
- 生成的共享网格通过 TEXCOORD0.x 增加标量边框距离输入。
- 三角扇中心顶点权重为 1，六个外圈顶点权重为 0；插值后的外边缘距离为 0，中心距离为 1。
- Shader 不接收或读取 HexPlane、HexOrientation、OuterRadius 或 SecondaryScale；XY/XZ、Pointy/Flat 和几何缩放都由网格表达。
- 只支持提供边框距离顶点属性的生成网格，不增加 object-space 布局回退。
- _BorderWidth 为材质级浮点属性，范围 0.001 到 0.5，默认 0.05。
- _GradientEnabled 为实例化 Toggle，按浮点 Shader 属性表示，默认关闭。
- _GradientPower 为材质级浮点属性，范围 0.1 到 8，默认 1。
- 关闭渐变时，边框宽度内为纯色；开启渐变时，Alpha 从外侧向内侧按 _GradientPower 减弱。
- _InteriorAlpha 为材质级浮点属性，范围 0 到 1，默认 0。
- 内部填充 Alpha 为 _BaseColor.a 乘以 _InteriorAlpha；边框 Alpha 独立计算后也乘以 _BaseColor.a。
- Hex Cell 使用硬边界，不提供抗锯齿开关；基于片元导数的边界覆盖会在相邻 Hex 之间产生可见缝隙。
- HexAppearance 增加 GradientEnabled。
- 保留 HexAppearance(bool visible, Color color)。
- 新构造方式允许显式提供每 Cell 的渐变开关。
- HexAppearance.Equals 和 GetHashCode 比较可见性、颜色和渐变开关。
- HexRenderHandle 使用现有 MaterialPropertyBlock 写入 _BaseColor 和 _GradientEnabled。
- _InteriorAlpha 保持材质级，不扩展为每 Cell 参数。
- Runtime GVG Demo 不根据 Plot 类型或游戏状态自动推断样式，已有调用继续使用兼容默认值。
- 相邻 Cell 继续分别绘制自己的边框；共享边去重和集中式边界渲染另行设计。
- HexView.SetAppearance(HexAppearance) 继续作为更新 Cell 外观的公开入口。
- HexMapRenderer 生成的共享网格是几何测试接缝。
- Unity 自动生成的 .meta 文件不属于实现内容，不得手动创建或修改。

## 测试决策

- 测试验证公开可观察的 Renderer 和外观行为，不断言私有辅助方法或不影响契约的 Shader 内部细节。
- 主要外观接缝是 HexView.SetAppearance(HexAppearance)，通过现有 HexMapView 和生成 Renderer 验证参数传播。
- 主要几何接缝是 HexMapRenderer 生成的共享网格，验证边框距离顶点属性。
- 扩展现有 HexAppearance 相等性测试，覆盖边框宽度、渐变开关和渐变幂次。
- Renderer 测试验证共享网格七个权重：中心为 1，六个外圈为 0。
- 网格测试覆盖 Pointy、Flat、XY、XZ、不同 Outer Radius 和不同 Secondary Scale。
- 外观传播测试验证 _BaseColor 和 _GradientEnabled，材质测试验证 _BorderWidth 和 _GradientPower。
- 兼容性测试验证旧两参数构造方式继续有效。
- 相等性测试验证任意边框参数变化都会使外观不相等并触发 Renderer 更新。
- Shader 和材质检查验证支持的属性存在且默认值符合规格，并确认不公开抗锯齿属性。
- Unity 视觉验证在同一共享材质下展示窄纯色 Cell 和宽渐变 Cell。
- 视觉验证覆盖 _InteriorAlpha 为 0、中间值和 1，以及斜视角下的硬外、内边界。
- 视觉验证覆盖 XY/XZ、Pointy/Flat 和非默认 Secondary Scale。
- 视觉验证确认正常深度遮挡以及透明边框不写入深度。
- 测试不要求手写 .meta 文件，也不把 .meta 文件作为功能契约。

## 不在范围内

- 相邻 Cell 共享边的集中式网格、边界去重或边归属系统。
- 每 Cell 独立控制 _InteriorAlpha。
- 根据 GVG Plot 类型、归属、阻挡状态或寻路状态自动映射样式。
- 修改 Runtime GVG Demo 的游戏逻辑或视觉业务规则。
- 纹理驱动渐变、光照、法线、阴影及额外表面着色。
- 对缺少边框距离属性的任意网格提供 Shader 回退。
- 修改 HexCoord、HexLayout 坐标换算、地图半径语义或 Cell 身份。
- 网格合并、分块渲染、批处理重构或大地图性能优化。
- 新的全局材质管理、输入、选择、悬停或游戏状态系统。
- 手写 Unity .meta 文件。

## 补充说明

- 本功能是生成 Hex Cell 几何的表现层改动，不改变逻辑 HexMap 或 GVG 领域模型。
- 网格相对中心权重是关键接缝：它让 Shader 独立于布局参数，同时支持现有所有 HexLayout 变体。
- 边框宽度和渐变幂次由材质表达；每个 Cell 只通过颜色和渐变开关表达差异。
- 如果未来需要语义化命名样式，可以在显式 HexAppearance 参数之上增加样式预设，而无需改变 Shader 契约。
- 相邻 Cell 的共享边在颜色或 Alpha 不同的情况下可能受到重复绘制和透明排序影响；解决边界归属需要单独规格。
- 本规格可交给实现代理；实现应保留现有 Renderer 资源所有权和 View 失效行为，只增加本功能所需的网格属性和外观参数。
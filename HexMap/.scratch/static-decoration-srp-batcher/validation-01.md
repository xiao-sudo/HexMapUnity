# 01 — 最小装饰渲染闭环：运行与验证

## 当前状态

代码已实现，C# 编译检查已通过；用户要求测试由用户执行，因此没有运行 Unity 测试、Shader 导入验证、Frame Debugger 或性能采样。票据中的 GPU/画面/SetPass 验收仍待执行，不能标为已通过。

新增代码只涉及独立装饰渲染器、位置配置、专用 Shader、演示组件、场景创建菜单及 PlayMode 像素测试。HexMap 原有实现和项目 URP 配置未改动。

## 创建并运行场景

相机朝向修正：用户已确认，旧演示从 Z=-10 朝 +Z 观察 XY 六角格背面，导致 Game 视图被背面剔除。菜单和像素测试现改为 Z=10、Y 旋转 180°，保留 HexMap 的背面剔除。旧场景需手动修改相机或重新创建；已序列化的 Placements 不会随代码默认值更新，若要验证装饰更近但仍被 Hex 覆盖，将装饰 Z 改为 0.2。此人工验证仅确认可见性原因，不表示像素测试或 SetPass 验收已通过。

1. 在 Unity 中等待脚本和 Shader 导入完成，确认 Console 无编译错误。
2. 点击 **HexMap → Demos → Create Static Decoration Scene**。菜单会通过 Unity 标准保存提示保护当前场景，并创建一个未保存的新场景；需要保留时自行保存。
3. 检查当前质量级别实际使用的 URP Asset 已启用 SRP Batcher。演示发现未启用时会提示，不修改现有项目资产。
4. 选中 Static Decoration Demo，按需指定 Image；未指定时生成带平滑透明边缘的绿色测试图。Placements 可配置位置、旋转和缩放，进入 Play 前修改；运行时不重建摆放列表。
5. 进入 Play：看到三个绿色装饰矩形和七个红色 Hex。中心 Hex 半透明，应透出绿色；其余 Hex 不透明，应覆盖绿色。矩形边缘向黑背景平滑过渡。
6. 默认装饰比 Hex 更靠近相机，但队列仍保证 Hex 在其上方。修改相机 X/Y 及 Orthographic Size，检查无排序翻转；不要旋转镜头。
7. Inspector 的 Decorations Visible 开关只控制装饰，HexMap 保持绘制，可用于同场景 A/B 计数。
8. 多批次验证：退出 Play，将 Demo 的 Map Radius 改为 19、相机 Orthographic Size 改为 40，再进入 Play。此时有 1,141 个 Cell，超过每批 1,023 的上限，可捕获多个 HexMap 实例批次并确认装饰全部在其前面。默认半径 1 的七格场景只用于最小画面验证。

## 约定与资源所有权

- StaticDecorationRenderer 接收父 Transform、专用材质、位置配置列表和 Unity Layer，一次创建独立 MeshRenderer，共享一个单位 XY 四边形及调用者提供的材质，无逐帧几何重建。
- 可通过 Visible 显隐。调用者负责 Dispose，并在其后释放自有材质、图片；渲染器只销毁自己的节点和 Mesh。
- 专用 Shader 队列固定为 2900，现有 HexMap 默认 3000。默认排序层保持一致；如果外部自定义 HexMap 的队列或排序层，需重新验证这一契约。
- 不使用 MaterialPropertyBlock。DisableBatching 标签禁用传统动态合批，以便观察 SRP Batcher 路径；不要把它误读为 SRP Batcher 开关。
- Shader 单 Pass、无光照、Alpha Blend、ZWrite Off、ZTest LEqual、Cull Off；不生成阴影 Pass，材质属性位于 UnityPerMaterial。
- 输入 Shader 通过演示组件序列化引用保留，构建演示时应包含保存后的演示场景；不依赖仅靠 Shader.Find 保留构建资源。
- 本票不实现图集、多层排序、网格合并或微信真机性能验收，这些按后续票处理。

## 用户执行的测试

在 Unity **Test Runner → PlayMode** 中运行：

```text
HexMap.UnityRuntime.Tests.StaticDecorationRenderingTests
```

唯一用例 TransparentEdgesAndHexCoverageSurviveCameraMovement 使用实际相机、RenderTexture、独立装饰 Renderer 及 HexMap Instancing，检查：
- 半透明地块与蓝色装饰混合的像素；
- 地块外的部分透明纹理像素；
- 相机平移缩放后，不透明地块覆盖装饰，装饰位置保持不变。

使用图形界面的 Unity，测试期间保持 Game 视图可见；WaitForEndOfFrame 像素测试不适合无图形模式。测试使用 Layer 31 隔离捕获，请避免现有场景同层对象进入测试相机。

仓库脚本当前在 PlayMode 分支不应用 Group 过滤；单个测试类请使用 Unity Test Runner 选择执行，不用脚本声称已做定向运行。

单用例通过后建议执行相关 EditMode 回归与 PlayMode；实现者未运行这些命令：

```powershell
powershell -File scripts/run-tests.ps1 -Assembly HexMap.UnityRuntime.Tests.EditMode
powershell -File scripts/run-tests.ps1 -Mode PlayMode
```

像素测试不能证明 SRP Batcher 或 SetPass 达标，下面的人工检查不可省略。

## Frame Debugger 与计数验收

1. 保持同一场景、相机、Game 分辨率及 URP 配置，关闭不需要的 Scene 视图渲染和额外相机，关闭 Gizmos。
2. 启用装饰，Frame Debugger 捕获主相机：找到 HexMap/StaticDecoration，确认 SRP Batcher 路径，所有装饰先于 InstancedHexCell，且没有装饰穿插进 HexMap 绘制。
3. 查看 Shader Inspector 的 SRP Batcher 兼容信息和 Frame Debugger 实际事件；兼容标志不能代替实际生效的证据。
4. 关闭 Frame Debugger 后用同一 Profiler/Rendering 计数口径分别记录装饰开、关的稳定帧 SetPass Calls 与 Draw Calls。装饰新增 SetPass 应为 1～2；Draw Call 不限制为 1～2。
5. 保留 Shader/批次截图、帧计数、Unity 版本、图形 API、分辨率、设备及配置。不要将 SRP Batch 数量直接视为 SetPass 数量。
6. 若未达标，提供具体事件及计数差异，继续诊断材质、变体与排序；不自动改成网格合并。

| 项目 | 结果 |
| --- | --- |
| 新增 C# 编译 | 已通过；有项目既有依赖版本冲突警告 |
| Unity Shader 导入/编译 | 待用户执行 |
| PlayMode 像素测试 | 待用户执行 |
| 相关 EditMode/PlayMode 回归 | 待用户执行 |
| 实际 SRP Batcher 路径及多个 HexMap 批次顺序 | 待用户捕获 |
| 装饰新增 1～2 SetPass | 待用户测量 |
| 微信真机 60 FPS | 本票不验收，属于第 04 票 |

## 编译检查说明

Unity 当前生成的项目尚未列入新增源码。使用临时 MSBuild targets 将新增运行时和 PlayMode 文件加入对应项目进行 C# 编译；编辑器菜单以现有有效的 HexMap.Gvg.Editor 引用集编译检查。临时 targets 和日志仅用于本地验证，不修改生成的 csproj，不执行测试，不替代 Unity 的程序集和 Shader 导入检查。

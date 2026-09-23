# 正交俯视地图相机

本文件记录「用一台正交相机俯视观察地图、并支持左右移动」的全部决策。经五轮逐题敲定，第 2 节即结论。第 1 节是侦察结论，第 3 节是被否掉的选项，**第 4 节是本特性最容易重蹈的失败模式，请先读那一节**。

## 0. 一句话需求与它的内在张力

需求原文：「俯视地图，用正交相机观察整个地图，正交相机朝向地图的中心线，可以左右移动」。

**目标平台是微信小游戏、竖屏游玩**（官方 `deviceOrientation` 默认即 `portrait`）。在竖屏下，"看到所有行"与"可以左右移动"**不冲突**，而是互相加强：屏幕高被"所有行"钉死，而竖屏的屏幕宽 = 屏高 × 0.5625 只剩很窄一条，天然装不下宽地图。最终确认的目标是：

> **屏幕内从上到下容纳地图的所有行；从左到右只容纳一部分，其余靠用户拖拽浏览。**

**这个目标只在竖屏成立。** 横屏（16:9 / 4:3）下同一个公式会得出相反的结论：屏幕宽会宽到装下整张地图，可移动范围恒为 0（推导与数值见 2.4）。因此：

- 本特性的**目标宽高比是 `9:16` 量级（竖屏）**，`m_TargetAspect` 默认取 `9f/16f`。
- 一旦在横屏下取景，**"可以左右移动"会静默失效**（相机锁死居中），这不是缺陷而是几何结论。第 4 节的失败模式表列了这条。

## 1. 背景与现状（侦察结论，均为实测）

| 事实 | 位置 |
| --- | --- |
| `map.unity` 主相机：`(0, 30, 0)`、绕 X `+90°`、**透视**（`orthographic: 0`）、size 5、near 0.3 / far 1000、无子节点 | `map.unity:377,466-471,495,500` |
| `map.unity` 的 HexMap 参数：`Radius 11`、`Pointy`、`XZ`、`OuterRadius 1`、`SecondaryScale 0.9`、`Origin (0,0,0)`、transform 恒等旋转 | `map.unity:255-260`、HexMap transform 为 identity |
| 生产场景用 **`DrawMeshInstanced`** 策略（`m_RenderStrategy: 1`） | `map.unity:263` |
| `DrawMeshInstancedStrategy` **不创建任何 GameObject**，只有父矩阵 + `Graphics.DrawMeshInstanced` | `DrawMeshInstancedStrategy.cs:110,120-123,156,187-193` |
| `MeshRendererStrategy` 才建层级：`HexMapView` → `"Generated Hex Map"` → `"Cell #…"` | `MeshRendererStrategy.cs:44-81` |
| cell 网格**厚度为 0**，`XZ` 下每个 cell 的世界 Y 恒为 `Origin.y` | `HexCellMeshFactory.cs:25-27`、`HexLayout.cs:90` |
| 装饰物**没有高度常量**：`DecorationPrefabBuilder.CreateHierarchy` 从不设置子节点 `localPosition` | `DecorationPrefabBuilder.cs:162-197` |
| 故 `XZ`（平放）装饰物**与地图共面**（Y = 根节点 Y）；`XY`（竖立）装饰物**骑在地图平面上**，约 −h/2 ~ +h/2 | 同上 + `Decoration.shader:17-19`（`ZWrite Off` / `ZTest LEqual` / `Cull Off`） |
| 分层靠 `sortingOrder` 带，不靠深度缓冲、不靠几何高度 | `docs/adr/0001-decoration-overlay-render-layers.md` |
| `map.unity` 的 3 个装饰实例 + 1 个覆盖实例**全是 `_XZ`（平放）**，Y 全为 0 | `map.unity:142-152,518-527,615-624,821-828` |
| `HexMap.Runtime` / `HexMap.Core` **禁止**引用 Camera/Physics/GameObject/MonoBehaviour | `docs/implementation/hex-map-view-rendering.md:307` |
| 仓库里唯一的相机代码是拾取用的 `Camera.main` 兜底，**无任何正交/取景代码** | `GvgMapRuntimeController.cs:114-133` |
| 同构先例：早期手工验证改的就是"相机 X/Y + Orthographic Size，不要旋转镜头"，size 用 40 | `.scratch/static-decoration-srp-batcher/validation-01.md:18,20,40` |
| 明确被划出范围的先例："camera controller behavior, camera movement, zooming and map framing" | `.scratch/hex-map-picker-world-position-refactor/spec.md:143` |
| **仓库里没有 `game.json`**（微信小游戏配置不在版本控制内） | 全仓库 glob 无命中 |
| 微信 `deviceOrientation` **默认即 `portrait`**，横屏须显式写 `landscape`；`displayMode` 三档为 `mobile` 736×414 / `pad` 1024×768 / `desktop` 1024×768（后两者是 4:3） | [小游戏配置](https://developers.weixin.qq.com/minigame/dev/reference/configuration/app.html) |
| **需求方确认：目标是竖屏游玩**，要"上下容纳所有行、左右只容纳一部分" | 本 spec 第 0 节与 2.3 / 2.4 即据此写成 |
| Unity 版本 `2022.3.50f1` | `ProjectSettings/ProjectVersion.txt` |

## 2. 契约（已决定，不可偏离）

### 2.1 几何：垂直俯视，朝向可配置

| 项 | 决定 |
| --- | --- |
| 投影 | **正交**（`orthographic = true`） |
| 朝向 | **垂直俯视**：相机绕 X `+90°`，视线沿 −Y |
| "垂直"的参照 | **相对地图平面**，不是相对世界。相机 `up` 由地图平面法线派生 |
| `XZ` 平面 | 屏幕上方 = 地图局部 `+Z`（= 世界 +Z）；相机 up = 地图平面法线（世界 +Y） |
| `XY` 平面 | 屏幕上方 = 地图局部 `+Y`；相机 up = 地图平面法线（世界 +Z，经地图 transform 变换） |
| **朝向** | **`Pointy` 与 `Flat` 都必须成立**，控制器内不得出现朝向分支。屏幕"上下"= 地图局部 `+Z`（`Pointy` 时六边形的尖朝 ±X），移动轴 = 地图局部 `+X`；两个轴在 `Flat` 下对调 |
| 平面来源 | 组件序列化 `m_PlaneMode`：`FollowMapView`（默认，读 `HexMapView.m_Plane`）/ `ForceXZ` / `ForceXY` |
| 显式平面不一致 | `Force*` 与地图实际平面不符时 `Refresh()` 直接报错，**不静默取其一** |
| 朝向来源 | **只有 `FollowMapView` 一种**（读 `HexMapView.m_Orientation`）。朝向不提供 `Force*`：它决定的是"地图长什么样"，而不是"相机看哪个面" |
| 移动轴 | 地图局部 `+X` 的世界方向（`transform.right`） |
| 地图旋转 | **不校验**。地图绕 X/Z 旋转时相机跟着转，"俯视"永远是相对地图的；世界空间里可能是斜的 |
| 相机高度 | 序列化 `m_Height`（默认 30，对齐现状） |
| `near` / `far` | 序列化，默认 `m_Height ∓ 2`（即 28 / 32）。必须覆盖骑在地图平面上的竖立装饰物 |

**控制器内不得出现 `if (plane == XZ)` 或 `if (orientation == Pointy)` 这样的分支。** 全部从 `HexLayout.Plane / Orientation / SecondaryScale` 派生：屏幕"上下"对应哪个世界轴、地图"深度"是哪个量，都是派生结果。

### 2.2 取景：高度是硬约束，宽度是自由变量

取景数学对**平面 × 朝向的四种组合全部成立**。两个轴的定义（`R` = 地图半径，`o` = `outerRadius`，`s` = `secondaryScale`）：

```
x 轴（相机移动轴）  = 地图局部 +X。Pointy 时六边形的尖朝 ±平面轴，Flat 时朝 ±X。
深度轴（屏幕上下）  = 平面轴。XZ 时是世界 Z，XY 时是世界 Y。

包络 = 格心铺开 + 一格半尺寸。两步都必须算：HexLayout 的铺开量只到最外圈格心，
      最外圈格子的凸包还要再往外伸一个半格。漏掉后一步会裁掉最外圈的尖。

              格心铺开（x / 平面）                  格子半尺寸（x / 平面）
Pointy:  √3·R·o          1.5·R·o·s        √3/2·o           o·s
Flat:    1.5·R·o·s       √3·R·o·s         o                √3/2·o·s
```

- **注意 `s` 被应用了两次**：`HexLayout.HexToWorld` 把 `secondaryScale` 乘进**格心间距**，`HexCellMeshFactory` 又把它乘进**格子顶点**。所以格子会缩到 `s` 倍而格心不变，地图上会出现 `1−s` 比例的缝隙（`s = 0.9` 时约 0.65 世界单位）。这是**既有行为**，本特性只如实反映它，不在本票修。

```
地图半宽 = 格心 x 铺开 + 格子 x 半尺寸
地图半深 = 格心平面铺开 + 格子平面半尺寸

orthographicSize = 地图半深 · m_ViewMargin            （m_ViewMargin，默认 1.1）
可视高度         = 2 · orthographicSize = 地图深 · m_ViewMargin
可视宽度         = 可视高度 · aspect
可移动范围       = ±max(0, 地图半宽 − 可视半宽)
初始偏移         = 0（居中）
```

**`Camera.orthographicSize` 本身就是半高**，所以 `size` 必须 ≥ 地图半深；写成"地图半深的一半"会让可视高只有地图深的一半（本特性实现时真踩过这个坑，测试里有一条专门钉住 `VisibleHeight ≥ MapDepth`）。

- **所有行恒定可见**：构造保证（可视高度恒 ≥ 地图深），必须由测试对**四种组合**逐一钉死。
- **所有列不保证可见**：看不全多少由地图自己扁不扁决定，代码不关心。
- **没有独立的宽度旋钮**。`m_ViewWidth` 之类的字段一律不加——那会允许配置出"看不到所有行"的状态。
- `m_ViewMargin ≥ 1` 且有限，否则 `Refresh()` 报错。

### 2.3 可移动范围：竖屏下有、横屏下没有

因为可视高度被"所有行"钉死成 `地图深 × margin`，所以**可视宽度也随之被决定**，它与地图宽度的大小关系只取决于**屏幕宽高比**：

```
可移动范围 > 0  ⟺  地图宽/地图深 > aspect × margin   ⟺   aspect < (地图宽/地图深) / margin
```

竖向屏幕（`aspect < 1`）让可视宽大幅缩小，于是**必然能拖动**；横向屏幕（`aspect > 1`）把可视宽放大，于是容易装下整张地图而锁死。

现状参数（`Pointy`，地图 `39.84 × 31.50`，深宽比 `0.79`，margin `1.1`）下：

| 屏幕 | `aspect` | 可视宽 | 可移动范围 | 结论 |
| --- | --- | --- | --- | --- |
| **竖屏 9:16（目标）** | `0.5625` | `19.49` | **`±10.17`（约地图宽的一半）** | ✅ 所有行入画 + 左右可拖 |
| 竖屏 9:19.5 | `0.4615` | `15.99` | `±11.92` | ✅ 拖得更多 |
| 方屏 | `1.0` | `34.65` | `±2.59` | ⚠️ 勉强能拖 |
| 横屏 4:3 | `1.333` | `46.20` | `0` | ❌ 锁死 |
| 横屏 16:9 | `1.778` | `61.60` | `0` | ❌ 锁死 |

竖屏下的可拖范围恰好在半张地图上下（±50%），即"左右各能拖过另外约一半"——与需求描述吻合。

### 2.4 生产地图的实际数值（`map.unity` 现状参数：`R=11, o=1, s=0.9, margin=1.1`）

`OrthographicMapFraming` 已实现并用 71 项断言复核（见 issue 01）。

| 量 | `Pointy`（现状，采用） | `Flat` |
| --- | --- | --- |
| 地图包络（宽 × 深） | `39.837 × 31.500` | `31.700 × 35.853` |
| `orthographicSize` | `17.325` | `19.720` |
| **竖屏 9:16** 可视高 × 可视宽 | **`34.650 × 19.491`** | `39.439 × 22.184` |
| **竖屏 9:16** 可移动范围 | **`±10.173`（50% 地图宽）** | `±5.003`（32%） |
| 横屏 16:9 可视宽 / 可移动范围 | `61.600` / `0` | `70.114` / `0` |

**`Pointy` 是竖屏下的正确选择**（可拖范围约为 `Flat` 的两倍，且行高更合适：一行占屏高 `2.86`，约占视口高的 `8.3%`，即约 12 行同屏；一列占屏宽 `3.62`，约占视口宽的 `18.6%`，即约 5～6 列同屏）。

**现状参数无需任何改动即可满足需求**：`Radius 11`、`Pointy`、`XZ`、`s = 0.9` 全部保持原样。这与横屏下的结论相反（横屏需要把 `s` 压到 `0.566` 以下才能拖动），因为竖屏把可视宽从 `61.6` 压到了 `19.49`。

### 2.5 宽高比：默认竖屏 `9:16`，允许一次性纠正

- 序列化 `m_TargetAspect`，默认 **`9f / 16f`（0.5625，竖屏）**。
- `Refresh()`（初始化 / 分辨率变化时调用，**不进逐帧路径**）：若 `Camera.aspect` 与 `m_TargetAspect` 的差超过容差，改用 `Camera.aspect` 重算，并按 2.2 重夹偏移。
- 动机：目标是竖屏小游戏，而官方 `displayMode` 三档与 PC 端都可能是横向（`mobile` 736×414、`pad`/`desktop` 1024×768）。**在横屏下重算会把可移动范围压到 0**，所以"默认值"必须是竖屏，让编辑器预览与真机一致；同时保留一次性纠正，避免在真正拿到横屏视口（PC 大屏 / iPad 旋转）时用错误的宽高比取景。
- 编辑器 Game 视图应设为竖屏分辨率（如 750×1334 / 1080×1920），否则预览取景与真机不一致。

### 2.6 偏移与 API

| 项 | 决定 |
| --- | --- |
| 偏移单位 | **地图局部 X 上的标量**（世界单位）。对外表现为世界坐标，内部沿局部轴移动（地图旋转时不会跑偏） |
| 写入 | `Offset` 属性读写；写入即夹取到 `[MinOffset, MaxOffset]` |
| 只读 | `MinOffset` / `MaxOffset` / `NormalizedOffset` |
| 立即重算 | `Refresh()`：按当前视口/平面/宽高比重算 size、范围、位置 |
| 失败方式 | `Try* + out string error`（与 `HexMapView.TryCreateSnapshots` 同风格），**不抛异常到调用方** |
| 初始偏移 | 可指定（"以某个位置为中心"），默认 0 |
| 不做 | `FocusOn(HexCoord)`、平滑过渡/缓动、惯性 |

### 2.7 输入

- **拖拽**：鼠标 / 触摸，**内容跟手**（手指向左拖 → 地图向左走 → 相机向右移）。
- **无阻尼、无缓动、无惯性**，1:1 直接映射（不加灵敏度系数）。
- **不做键盘**（目标是微信小游戏，没有键盘）。
- 适配器放 `HexMap.Sample`，与 `Facade.cs` 的 `Input.GetMouseButtonDown` 同层。`HexMap.UnityRuntime` **不读输入**。
- 已知代价：适配器**没有自动化测试覆盖**，验收靠 `map.unity` 里手动拖一次。

### 2.8 环境与配置

| 项 | 决定 |
| --- | --- |
| 相机 | **改造 `map.unity` 现有主相机**，不新建相机 |
| `orthographic` 的写入者 | **编辑器 `OnValidate` 静态设定**。控制器运行时只校验、不偷偷改（"摆放设置不是运行时状态"，与 ADR-0001 的排序号同构） |
| Layer 配置 | **独立于 `HexMapView` 的新 MonoBehaviour**，序列化 culling mask 等信息 + 自检（未含 HexMap 层时报错） |
| Layer 的所有权 | 该组件**不接管** `HexMapView.m_CellLayer` 的写入，两者只须一致 |
| 场景接线 | 只接 `map.unity`；`test.unity` / `single.unity` / `SampleScene.unity` 不碰 |

### 2.9 落点与测试

| 项 | 决定 |
| --- | --- |
| 取景数学 | 纯结构体 + 纯函数，归 `HexMap.Core`（零场景依赖，可在 `HexMapView.Build()` 之前算） |
| 控制器 / Layer 组件 | `HexMap.UnityRuntime`（`Assets/Scripts/HexMap/UnityRuntime/`） |
| 输入适配器 | `HexMap.Sample` |
| 测试程序集 | `HexMap.UnityRuntime.Tests.EditMode`（Tier 2 活编辑器） |
| 测试内容 | ① 取景公式；② `TrySetOffset` 的夹取与失败；③ **在 EditMode 里造 Camera，断言 `orthographic` / `orthographicSize` / `position` / `rotation` 真的被写成期望值** |
| 不做 | PlayMode 像素回读（成本高，且会被装饰物颜色干扰） |

第 ③ 条是必须的：只测公式等于只证明"函数对"，没证明"它接到了相机上"——而"算对了没写进相机"正是最容易发生的一次性错误。

## 3. 明确不做

1. **不做缩放**。`orthographicSize` 由取景公式唯一确定，不暴露滚轮/双指改它。用户原始需求里没有缩放；加缩放会引入"当前视野不等于整图"这个状态，把 2.2 的构造保证整个推翻。若将来要做，届时 `FocusOn` / 可移动范围 / 列数可见性都要重新推导。
2. **不做 `FocusOn(HexCoord)`**。它会引入"以某个格子为中心"而非"以包围盒中心为中心"的第二种中心定义，与 2.2 冲突。当前"以某个位置为中心"通过设置初始 `Offset` 即可满足。
3. **不做平滑过渡 / 惯性 / 阻尼**（2.7 已定）。
4. **不做竖立装饰物在垂直俯视下的可见性处理**。`HexPlane.XY` 贴片垂直于地图平面，垂直俯视下**投影退化成一条零宽度线段**，且与平放装饰物在平面上交叉。**这是已知限制，不是缺陷**：它由"俯视"和"竖立贴片"两个前提共同决定，要处理就得引入倾斜或剔除层，等于推翻 ADR-0001 的分层契约。当前 `map.unity` 全是平放装饰物，看不到该现象。**下一个人若看到"竖立装饰物不见了"，先读这一条，不要去改渲染代码。**
5. **不校验地图绕 X/Z 的旋转**。见 2.1：俯视是相对地图的，旋转地图是自洽配置，禁止它等于挡掉正当用法。
6. **不做输入层的自动化测试**（2.7）。
7. **不改 `HexMapView` 的平面/朝向/尺寸字段**，也不把取景逻辑塞进 `HexMapView`。相机是观察者，地图是数据。
8. **不做小地图 / RenderTexture**。`.scratch/gvg-hex-map/issues/09` 的小地图是另一个需求（渲染到 RenderTexture、以队伍所在格为中心），它未来可以复用本特性的取景函数，但不属于本次范围。
9. **不写 ADR**。本特性是取景实现，不是领域契约；决策记进 `docs/implementation/`。

## 4. 最容易重蹈的失败模式（先读这一节）

| 症状 | 真相 | 正确的反应 |
| --- | --- | --- |
| 竖立装饰物（`_XY`）在画面里消失/变成一条线 | 垂直俯视的必然结果 | 读 3.4，不要改渲染代码 |
| **左右拖不动**（相机锁死居中） | 可视宽已经 ≥ 地图宽。可视高被"所有行"钉死，可视宽 = 可视高 × aspect ⇒ **`aspect` 越大越拖不动**。竖屏（`0.5625`）必然能拖；横屏 4:3 与 16:9 必然锁死 | **先把 Game 视图 / `m_TargetAspect` 确认成竖屏 9:16**（见 2.5），再看 `m_SecondaryScale`；**不是**改相机取景代码 |
| 在编辑器里能拖、打包到真机后拖不动（或反之） | 编辑器 Game 视图还是横屏分辨率，或 `m_TargetAspect` 与真机宽高比不一致 | 把 Game 视图设为 750×1334 / 1080×1920，`m_TargetAspect` 保持 `9f/16f` |
| 看到全所有列 | 同"拖不动"：屏幕（相对地图）太宽 | 先确认宽高比，再考虑压扁地图 |
| 以为改成更宽的比例（16:9、21:9）能换来移动余地 | 反了：更宽的视口更容易装下整张地图 | 往**更窄**（竖屏）的比例调 |
| 最上一行 / 最下一行贴边或被切一条 | `m_ViewMargin` 被设成 1.0 或更小 | 保持 ≥ 1.1 |
| 画面上下能看到地图外的空白 | 正常。可视高度 = 地图深度 × 1.1，多出来的是边距 | 不是 bug |
| 地图绕 X/Z 转了，相机在世界空间里是斜的 | 俯视是相对地图的（2.1） | 不是 bug |
| 相机是透视的 | `orthographic` 是**摆放设置**，由编辑器写 | 在场景里勾上，不是运行时问题 |
| 拖拽手感反了 | 2.7 定的是"内容跟手" | 改适配器的一处符号 |

## 5. 交付切分

| Issue | 内容 | 依赖 |
| --- | --- | --- |
| `01-core-visibility-math.md` | `HexMap.Core` 的取景纯函数 + 单测 | 无 |
| `02-camera-controller.md` | 控制器 + Layer 配置组件 + `map.unity` 接线 + EditMode 测试 | 01 |
| `03-sample-input-and-docs.md` | `Sample` 拖拽适配器 + `docs/implementation/` 文档 | 02 |

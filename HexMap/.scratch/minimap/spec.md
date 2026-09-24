# 小地图（角落控件）

本文件记录「在角落放一个跟随当前队伍的小地图控件」的全部决策。第 2 节是结论，**第 4 节是本特性最容易重蹈的失败模式，请先读那一节**。需求原文来自 `.scratch/gvg-hex-map/issues/09`，本文件只覆盖其中"控件本体"这一部分，与 `09` 的覆盖关系见第 6 节。

## 0. 一句话需求与它的内在张力

需求：**一个很小的地图控件，只显示地图的一部分，以当前部队所在格为中心（经过夹取）。**

内在张力：「小」与「看得清」是对立的。解法不是把整张图缩小，而是**只显示一小块、并跟着部队走**。这决定了它在结构上不是「整图的缩略图」，而更接近**第三台俯视相机**。

## 1. 背景与现状（侦察结论，均为实测）

| 事实 | 位置 |
| --- | --- |
| 生产场景 `sw.unity` 的 `HexMapView`：`Radius 11`、`Pointy`、`XZ`、**`OuterRadius 1`**、**`SecondaryScale 1`**、`CellLayer 0`、**`RenderStrategy 0 = MeshRenderer`** | `Assets/sw.unity:532-542` |
| `map.unity` 的同一组件是 `OuterRadius 2`、`SecondaryScale 0.8`、`RenderStrategy 1 = DrawMeshInstanced` ⇒ **两个场景的地图尺寸不同，本文件的数值一律按 `sw.unity` 算** | `Assets/Scenes/map.unity:255-263` |
| 取景基值**只由深度决定**：`baseOrthographicSize = mapHalfDepth × viewMargin`（**不是** `max(深度, 宽度/aspect)`） | `OrthographicMapFraming.cs:360` |
| ⇒ **可视高度与 aspect 无关**：`可视高(zoom) = 2 × mapHalfDepth × margin / zoom` | 由上一行推出 |
| `zoom` 上限是**绝对档位** `MaxZoom`（序列化，默认 `12`），与 aspect 无关；zoom 下限恒为 `1` | `OrthographicMapCamera.cs`、`docs/implementation/orthographic-map-camera-internals.md` §4.6 |
| `FocusOn(HexCoord)` 的原注释：**"The center ends up clamped, so an edge cell sits beside the viewport center rather than outside the map."** | `OrthographicMapCamera.cs:664-686` |
| `FocusOn` 要求先有 framing，否则返回 `false` + `"Refresh the camera before focusing."` | `OrthographicMapCamera.cs:670-674` |
| 垂直可拖范围 = `地图半深 − 可视半高`；水平用 `framing.MovableHalfRange` | `.scratch/orthographic-map-camera/issues/05-camera-zoom-and-focus.md:14` |
| **运行时库里没有任何"地图/归属变了"的事件**（全库 grep `event` 只命中 Sample 的 `PlotClicked`） | 全库 grep |
| 阵营→颜色表是 `GvgMapRuntimeController` 的**私有**字段，只在 `TrySetPlotOwnerFactionId` 里用；外部无法查询"某格是什么颜色" | `GvgMapRuntimeController.cs:71,84,385` |
| 这个 Unity/URP 上**没有** `RenderPipeline.SubmitRenderRequest`（core 包 357 个 `.cs` 全量搜 `RequestData|RenderRequest` = 0 命中）；`UniversalRenderPipeline.RenderSingleCamera` 已标 Obsolete | 包源码 |
| `ResolveAspect()` 只在相机 aspect 与 `m_TargetAspect` **接近**时用后者，否则用相机真实 aspect ⇒ 小地图相机的 aspect **不需要配置** | `OrthographicMapCamera.cs:992-1003` |
| 运行时**没有**队伍/单位系统；`GvgPlotRuntimeData` 的 8 个字段与 `Plot` 都没有名字字段 ⇒ 「城池名称」没有数据来源，名字只在作者侧的 Excel 冗余列里 | `GvgPlotRuntimeData.cs:62-100`、`Plot.cs`、`GvgExcelDocumentRedundancy.cs` |
| 已有 `HexMap.Sample.Tests.EditMode` 程序集与一键接线工具 `TwoModeSampleSceneWirer`（菜单 `Tools/Hex Map/…`） | `Assets/Scripts/HexMap/Sample/Editor/` |

## 2. 契约（已决定）

### 2.1 形态与本次范围

| 项 | 决定 |
| --- | --- |
| 形态 | **角落常驻小控件**（`RawImage` 显示一张 `RenderTexture`） |
| 内容 | 地图本身（含**地块归属色**，由相机烤自动带上，零额外工作）+ **队伍位置点** |
| 跟随 | 玩法**推送 `HexCoord`**；小地图不认识"部队"这个词 |
| 输入 | **可点击**，只暴露 `event Action<Vector3> Clicked`（世界点）；**本次不接任何具体行为** |
| 本次不做 | 拖动浏览、大营/城池文字标签、战场摘要、城池名称（见第 3 节） |

### 2.2 取景：复用第三份 `OrthographicMapCamera`

小地图 = 第三份 `OrthographicMapCamera` 实例 + 第二份 `OrthographicMapLayerSettings` 实例。**不需要任何新的取景数学**——这是本特性最重要的结论。

`sw.unity` 数值（`o = 1`、`s = 1`、`margin = 1.1`）：

```
mapHalfWidth  = √3·11·1   + √3/2·1·1 = 19.0526 + 0.8660 = 19.9186   ⇒ 地图宽 39.837
mapHalfDepth  = 1.5·11·1·1 + 1·1     = 16.5000 + 1.0000 = 17.5000   ⇒ 地图深 35.000
baseSize      = mapHalfDepth × margin = 17.5 × 1.1 = 19.2500
可视高(zoom)  = 2 × baseSize / zoom = 38.5 / zoom      （与 aspect 无关）
可视宽(zoom)  = 可视高 × aspect
列间距 = √3·o = 1.732        行间距 = 1.5·o·s = 1.500
行数 = 可视高 / 1.5 = 25.667 / zoom       列数 = 可视宽 / 1.732 = 22.23 × aspect / zoom
```

| zoom | 可视高 | 方形控件下 行 × 列 | 备注 |
| --- | --- | --- | --- |
| 1 | 38.500 | 25.7 × 22.3 | 所有行可见（35 < 38.5），**但看不全列** |
| 2 | 19.250 | 12.8 × 11.1 | |
| **3.667** | **10.500** | **7.0 × 6.1** | ⭐ 目标：≈5 列 × 7 行 |
| 4.444 | 8.663 | 5.8 × 5.0 | |
| 12（默认上限） | 3.208 | 2.1 × 1.9 | **绝对上限**，与 aspect 无关 |

**「5 列 × 7 行」的精确解**：`zoom = 25.667 / 7 = 3.667`；控件 aspect `= 5 / (7 × 0.866) = 0.825`（略竖长）。方形控件在同一 zoom 下得到 **6.1 列 × 7.0 行**，差别很小，**先用方形**。

⚠️ **方形控件永远看不全地图宽度**：`zoom` 下限是 1，此时可视宽 38.5 < 地图宽 39.837（差 3.4%）。要"整张图入画"必须 `aspect ≥ 39.837 / 38.5 = 1.035` 且 zoom 固定 1。⇒ **小地图不能兼作全图缩略图，这是几何结论而不是缺陷**（想看全图用俯视模式）。

### 2.3 跟随与夹取

```
玩法：minimap.SetFocus(coord)  →  内部先 TryRefresh（若无 framing）再 FocusOn(coord)（自带夹取）  →  请求一次重烤
```

`zoom = 3.667`、方形控件（可视 10.5 × 10.5，`size = 5.25`）下的夹取：

| 轴 | 范围 | 最外圈格心 | 被夹到 | 偏离视口中心 |
| --- | --- | --- | --- | --- |
| 水平 | `±(19.9186 − 5.25) = ±14.67` | `19.05` | `14.67` | 4.38 世界单位 = **半宽的 83%** |
| 垂直 | `±(17.5000 − 5.25) = ±12.25` | `16.50` | `12.25` | 4.25 世界单位 = **半高的 81%** |

⇒ **在地图中部，部队恒在控件正中；只有靠近边缘被夹取时才偏出去**。这就是队伍位置点存在的意义（**边缘补偿**），不是每时每刻的指示。

⚠️ **粒度**：玩法推的是 `HexCoord` ⇒ 小地图**按格跳动**（同一格内移动不动）。要平滑就得改推世界坐标或 `Transform`，本次不做。

### 2.4 画面：RenderTexture + 按需重烤

| 项 | 决定 |
| --- | --- |
| RT | **128² RGBA32 = 64 KB**（cell ≈ 21 × 24 px，足够清晰）；若采用 0.825 的控件比例则用 106×128（54 KB） |
| 控件 | `RawImage`，**`raycastTarget` 保持 `true`** |
| 重烤时机 | **按需**：`SetFocus` 改变了中心时、`RequestRefresh()` 被调用时（内容变了） |
| 重烤机制 | **把相机 `enabled` 打开一帧 → 帧末 RT 填好 → 关掉**。本版本没有即时渲染 API（第 1 节），所以 `RequestRefresh()` 的语义是「**下一帧生效**」 |
| 省钱 | 小地图相机的 URP 数据关掉后处理 / HDR / MSAA；`targetTexture` 常驻 ⇒ 它永远不会画到屏幕上 |
| 每次烤的量 | 16,384 px = 1080×1920 主帧的 **1/127**；`sw.unity` 是 `MeshRenderer` 策略，视锥剔除后约 **35–42 个 cell** 的绘制（若改成 `DrawMeshInstanced` 则降到 1–2 个 draw call） |
| 模式 | **只在 gameplay 模式显示**：控件挂在 `m_GameplayUiRoot` 下随根节点隐藏；相机也要跟着 `enabled = false`（否则白烤一张没人看的图）—— 交给 `MapViewModeSwitcher` 再加一个槽 |

### 2.5 标记与点击：转调相机，不自己算投影

| 项 | 做法 |
| --- | --- |
| 队伍点 | `minimapCamera.WorldToViewportPoint(队伍世界位置)` → `anchoredPosition = (uv − 0.5) × rectSize`。**不用翻 Y**：viewport 的 y 朝上，UI 局部 y 也朝上 |
| 点击 | 控件局部点 → `uv = local / rectSize + 0.5` → `minimapCamera.ScreenPointToRay(uv × RT 尺寸)` → `m_MapView.WorldPlane.Raycast` → `ray.GetPoint(d)` = 世界点 |
| 为什么这样对 | **标记与图片同源**（同一台相机、同一份 framing），夹取只有一份（在 `FocusOn` 里），不可能出现"模块夹一次、相机夹一次"的分歧 |
| 后路 | 以后若改成 C# 画贴图，只需重写 `TryGetLocalPoint` / `TryGetWorldPoint` 两个方法体（约 40 行），其余不动。**现在不预留模块**：没有第二个实现就不造缝 |
| 点击隔离 | 小地图是 UI ⇒ `MapClickTapInput` 的 `IsPointerOverGameObject()` 闸门**天然忽略它**，点击**永不进入 `MapClickDispatcher`**。契约：`MinimapView` **不引用** `MapClickDispatcher`，也不引用 `GvgMapRuntimeController` |

### 2.6 落点与测试

| 项 | 决定 |
| --- | --- |
| 新组件 | `HexMap.Sample` 的 `MinimapView`（要 RT / UI / 模式，与 `OrthographicMapDragInput` 同层） |
| 复用 | `HexMap.UnityRuntime` 的 `OrthographicMapCamera` / `OrthographicMapLayerSettings`，`HexMap.Core` 的 `OrthographicMapFraming` —— **零新增运行时数学** |
| 测试 | `HexMap.Sample.Tests.EditMode`：zoom↔行列换算、`SetFocus` 的夹取、`TryGetLocalPoint` / `TryGetWorldPoint` 往返与越界行为、重烤状态机的 `Tick` 行为 |
| 不做 | PlayMode 像素回读（渲染路径靠人工验收） |

## 3. 明确不做（本次）

1. **不做拖动浏览**（`09` 要求）：与本轮"只做控件本体"的范围冲突，另开切片。
2. **不做大营 / 城池名称文字标签**：名字**没有运行时数据来源**（第 1 节），要先做数据模型改动。
3. **不做战场摘要**（占领数 / 总地块 / 势力积分 / 排名）：需要新增统计接口与变更信号。
4. **不做城池名称的数据链路**（导出列 → DTO → `Plot` → composer）。
5. **不做点击后的行为**：只暴露事件，订阅者以后接。
6. **不做平滑跟随**：按格跳动；要平滑得改推世界坐标。
7. **不做整图缩略模式**：方形控件在 `zoom ≥ 1` 下装不下地图宽度（2.2）。

## 4. 最容易重蹈的失败模式（先读这一节）

| 症状 | 真相 | 正确的反应 |
| --- | --- | --- |
| 小地图一片黑 | 相机 `enabled = false` 时渲染不出东西；或实现成了"先 disable 再烤" | 顺序必须是 **enable → 烤一帧 → disable**；`RequestRefresh()` 是「下一帧生效」 |
| 小地图显示的区域比预期大一倍 | 拿 `map.unity` 的参数（`o=2, s=0.8`）算了 `sw.unity` | **按 `sw.unity` 的 `o=1, s=1` 算**（第 2.2 节） |
| 小地图四周一圈空白 | 用了 1.1 的 `ViewMargin` 且 zoom 偏小 | 设计内的边距；想更满就提高 zoom |
| 部队点总是贴着控件正中、看不出用途 | 在地图中部本来就居中（夹取不生效） | 读 2.3：点只在边缘有意义，这是预期 |
| 部队点跑到控件外 | 把 `WorldToViewportPoint` 的结果当 0..1 直接用了 | 越界要**隐藏**，不是夹取（夹取是相机的事） |
| 点小地图时主地图也跟着选中 | `RawImage` 没拦住射线（`raycastTarget = false`，或被别的 UI 盖住） | 保持 `raycastTarget = true`，确认场景里有 `EventSystem` |
| 想让它显示城池名字 | 运行时数据里没有名字 | 读 3.2 与第 6 节，先做数据链路 |
| 想让它显示整张地图 | `zoom` 下限是 1，方形控件可视宽 38.5 < 地图宽 39.837 | 几何结论；全图请用俯视模式（`aspect ≥ 1.035` 才可能） |
| 以为相机 aspect 要跟着控件配 | `ResolveAspect()` 只在"接近 `m_TargetAspect`"时才用后者 | 不用配，相机的真实 aspect 会被自动采用 |
| 改了 `m_ViewMargin` 后"看几行"跟着变了 | 基值 = 半深 × margin，margin 直接缩放可视范围 | 小地图的 margin 要**显式钉死**，不要跟主视图共用直觉 |
| 拖动小地图时主地图不走 | 本次根本没有实现拖动浏览（第 3.1 节） | 别去改相机代码，那是还没做的事 |

## 5. 交付切分

| Issue | 内容 | 依赖 |
| --- | --- | --- |
| `01-minimap-framing-and-follow.md` | `MinimapView` 的取景 / 跟随 / 投影接口（`SetFocus`、`TryGetLocalPoint`、`TryGetWorldPoint`）+ EditMode 测试 | 无（复用现有 rig） |
| `02-minimap-widget.md` | RenderTexture + `RawImage` + 按需重烤状态机 + 队伍点 + 点击事件 + switcher 槽 | 01 |
| `03-open-map-focused-on-squad.md` | 打开大图时以队伍格为中心（switcher 扩展 + 测试） | 无 |
| `04-scene-wiring-and-docs.md` | `sw.unity` 接线（扩展一键接线工具）+ `docs/implementation/minimap.md` | 02, 03 |

## 6. 与 `.scratch/gvg-hex-map/issues/09` 的关系

`09` 的四条要求里，**本次覆盖**：① 复用主地图状态、不维护第二套地图数据（这正是"相机烤"而不是"自己画一张"的依据）；③ 的"队伍位置"与"地块归属"。

**本次不覆盖、仍留在 `09` 名下**：

- ② 的「打开时以队伍格为中心」由切片 03 覆盖，但「**拖动浏览**」不覆盖；
- ③ 的「**大营信息和城池名称**」不覆盖（无运行时数据来源）；
- ④ 的「**战场摘要**」不覆盖（需要统计接口与变更信号）。

⇒ **`09` 不应被标记为完成**。已在它的 `## Comments` 里留了指向本目录的说明。

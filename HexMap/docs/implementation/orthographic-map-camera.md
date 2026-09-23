# 正交俯视地图相机

用一台正交相机从正上方观察 Hex 地图，上下容纳地图的**所有行**，左右只容纳**一部分列**，其余靠拖拽浏览。

决策全文见 `.scratch/orthographic-map-camera/spec.md`。本文只讲**怎么用**、**数字是多少**、**看到异常时先查哪里**。

## 组成

| 类型 | 程序集 | 职责 |
| --- | --- | --- |
| `OrthographicMapFraming` | `HexMap.Core` | 纯数学：给定 `HexLayout` + 半径 + 边距 + 宽高比，算出 `orthographicSize`、地图包络、可移动范围 |
| `OrthographicMapCamera` | `HexMap.UnityRuntime` | 把上面的结果写进场景相机，并沿地图局部 +X 平移 |
| `OrthographicMapLayerSettings` | `HexMap.UnityRuntime` | 独立持有相机的 culling mask，并自检能否看见地图 |
| `OrthographicMapDragInput` | `HexMap.Sample` | 把水平拖拽翻译成相机偏移（内容跟手） |

`HexMap.Core` / `HexMap.Runtime` 不引用 `Camera`：相机是观察者，地图是数据。

## 取景公式

```
包络 = 格心铺开 + 一格半尺寸        ← 两步都要，漏掉后一步会裁掉最外圈格子的尖
orthographicSize = 地图半深 × m_ViewMargin      ← orthographicSize 本身就是半高
可视宽度 = 2 × orthographicSize × aspect        ← 没有独立的宽度旋钮
可移动范围 = ±max(0, 地图半宽 − 可视半宽)
```

四种组合（平面 `XZ`/`XY` × 朝向 `Pointy`/`Flat`）都成立，代码里没有任何平面/朝向分支：平面只决定"屏幕上下"落在哪个世界轴，朝向决定两个铺开量哪个是深度。

## `map.unity` 的实际数值

参数：`Radius 11`、`Pointy`、`XZ`、`OuterRadius 1`、`SecondaryScale 0.9`、`m_ViewMargin 1.1`。

| 量 | 值 |
| --- | --- |
| 地图包络 | `39.8372 × 31.5000` |
| `orthographicSize` | `17.325` |
| 竖屏 9:16 可视区 | `34.65 × 19.4906` |
| 竖屏 9:16 可移动范围 | `±10.1733`（约地图宽的一半） |
| 一行占屏高 | `2.86`（约 12 行同屏） |
| 一列占屏宽 | `3.62`（约 5～6 列同屏） |

**现状参数无需改动。** `Radius`、`Orientation`、`SecondaryScale` 都保持原样就满足"上下容纳所有行、左右只容纳一部分"。

## 两条已知限制（不是缺陷）

1. **竖屏是前提。** 屏高被"所有行"钉死成 `地图深 × 1.1`，屏宽随之确定。竖屏下屏宽只有 `19.49`，装不下 `39.84` 宽的地图，所以能拖；**横屏 4:3 或 16:9 下屏宽会大于地图宽，可移动范围恒为 0，相机锁死居中**。这与朝向无关，是几何。若在横屏下发现拖不动，先查宽高比，不要去改渲染或取景代码。编辑器 Game 视图应设为竖屏分辨率（如 750×1334、1080×1920），否则预览与真机不一致。

2. **竖立装饰物（`HexPlane.XY`）在垂直俯视下不可见。** 它垂直于地图平面、骑在平面上，垂直俯视时投影退化成一条零宽线段，且与平放装饰物交叉。`map.unity` 现在的装饰物全是 `_XZ`（平放），所以看不到这个现象。要处理它必须引入倾斜或剔除层，等于推翻 `docs/adr/0001-decoration-overlay-render-layers.md` 的分层契约。**看到"竖立装饰物不见了"，先读这一条。**

## 接线（主相机上三个组件）

`map.unity` 的主相机：

1. `Camera`：勾上 **Orthographic**（这是摆放设置，控制器运行时只校验、不偷偷改）。
2. `OrthographicMapCamera`：`m_HexMapView` → 场景里的 **HexMap**；`m_Camera` → **自身**；`m_Height = 30`、`m_Near = 28`、`m_Far = 32`、`m_ViewMargin = 1.1`、`m_TargetAspect = 0.5625`、`m_PlaneMode = FollowMapView`。
3. `OrthographicMapLayerSettings`：`m_HexMapView` → **HexMap**；`m_CullingMask` 必须包含 HexMap 的 cell 层（默认 `Everything` 即可）。
4. 回到 `OrthographicMapCamera`，把 `m_LayerSettings` 指向第 3 步那个组件。
5. `OrthographicMapDragInput`：`m_MapCamera` → 那个 `OrthographicMapCamera`。它可以从 `HexMap.Sample` 程序集挂到任意常驻物件上。

相机在 `Start()` 里自动取景一次。之后**视口变化需要调用方显式调 `TryRefresh()`**，本特性不做逐帧监听。

## 看到异常时先查这里

| 症状 | 真相 | 怎么办 |
| --- | --- | --- |
| 左右拖不动 | 屏宽 ≥ 地图宽。屏高被"所有行"钉死，屏宽 = 屏高 × aspect ⇒ **aspect 越大越拖不动** | 先确认 Game 视图 / `m_TargetAspect` 是竖屏 9:16 |
| 编辑器能拖、真机不能（或反之） | 编辑器 Game 视图还是横屏分辨率 | 设为 750×1334 / 1080×1920 |
| 相机是透视的 | `orthographic` 是摆放设置 | 在场景里勾上，不是运行时问题 |
| 相机在地图**下方** | 位置必须写成 `原点 − forward × 高度`（`forward` 指向世界 −Y） | 这是实现时真踩过的坑，测试有断言钉住 `position.y == 30` |
| 最上一行/最下一行贴边或被切 | `m_ViewMargin` ≤ 1 | 保持 ≥ 1.1 |
| 画面上下有地图外空白 | 正常，可视高 = 地图深 × 1.1，多出来的是边距 | 不是 bug |
| 竖立装饰物不见了 | 垂直俯视的必然结果 | 读"两条已知限制"第 2 条 |
| 地图绕 X/Z 转了，相机在世界空间是斜的 | "俯视"是相对地图平面的 | 不是 bug |
| 拖拽手感反了 | 唯一的方向常量是 `OrthographicMapDragInput.DragDirection` | 翻它的符号 |

## 测试与验证

- 取景数学：`Assets/Tests/EditMode/HexMap/Core/OrthographicMapFramingTests.cs`（`HexMap.Tests.EditMode`）。
- 相机与 Layer：`Assets/Tests/EditMode/HexMap/UnityRuntime/OrthographicMapCameraTests.cs`（`HexMap.UnityRuntime.Tests.EditMode`）。

```powershell
powershell -File scripts\run-tests.ps1 -Assembly HexMap.Tests.EditMode
powershell -File scripts\run-tests.ps1 -Assembly HexMap.UnityRuntime.Tests.EditMode
```

**拖拽适配器没有自动化测试**：`HexMap.Sample` 只被 PlayMode 测试程序集引用，EditMode 测不到它。它的验收靠手动拖一次；所有夹取与边界逻辑都在有测试的 `OrthographicMapCamera` 里。

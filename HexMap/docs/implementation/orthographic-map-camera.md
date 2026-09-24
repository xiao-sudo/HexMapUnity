# 正交俯视地图相机

用一台正交相机从正上方观察 Hex 地图，上下容纳地图的**所有行**，左右只容纳**一部分列**，其余靠拖拽浏览；支持**缩放到细节**与**对准焦点格**。

决策全文见 `.scratch/orthographic-map-camera/spec.md`。本文只讲**怎么用**、**数字是多少**、**看到异常时先查哪里**。

## 组成

| 类型 | 程序集 | 职责 |
| --- | --- | --- |
| `OrthographicMapFraming` | `HexMap.Core` | 纯数学：给定 `HexLayout` + 半径 + 边距 + 宽高比 + zoom，算出 `orthographicSize`、地图包络、可移动范围 |
| `OrthographicMapCamera` | `HexMap.UnityRuntime` | 把上面的结果写进场景相机，沿平面两轴平移、按 zoom 缩放、对准焦点 |
| `OrthographicMapLayerSettings` | `HexMap.UnityRuntime` | 独立持有相机的 culling mask，并自检能否看见地图 |
| `OrthographicMapDragInput` | `HexMap.Sample` | 水平/垂直拖拽 → 平移（内容跟手） |
| `OrthographicMapZoomInput` | `HexMap.Sample` | 双指捏合 / 滚轮 → 缩放（锚点在手指中点或鼠标位置） |

`HexMap.Core` / `HexMap.Runtime` 不引用 `Camera`：相机是观察者，地图是数据。`HexMap.UnityRuntime` 不引用输入：输入在最外层的 `Sample`。

## 取景公式

```
包络 = 格心铺开 + 一格半尺寸        ← 两步都要，漏掉后一步会裁掉最外圈格子的尖
orthographicSize = 地图半深 × m_ViewMargin      ← orthographicSize 本身就是半高
可视宽度 = 2 × orthographicSize × aspect        ← 没有独立的宽度旋钮
可移动范围 = ±max(0, 地图半宽 − 可视半宽)
```

四种组合（平面 `XZ`/`XY` × 朝向 `Pointy`/`Flat`）都成立，代码里没有任何平面/朝向分支：平面只决定"屏幕上下"落在哪个世界轴，朝向决定两个铺开量哪个是深度。

## 缩放与焦点

**zoom ≥ 1，越大越近。`zoom = 1` 是唯一"所有行可见"的档位**，放大后允许丢掉上下两端的行——因为这时纵向有了可移动余量，可以拖过去看。

| zoom | `orthographicSize` | 可视宽 × 高 | 所有行可见 | 水平可拖 | 垂直可拖 |
| --- | --- | --- | --- | --- | --- |
| **1.0（最远）** | `17.325` | `19.49 × 34.65` | **✅** | `±10.17` | `±0` |
| 1.5 | `11.550` | `12.99 × 23.10` | ❌ | `±13.42` | `±4.20` |
| 2.0 | `8.663` | `9.75 × 17.33` | ❌ | `±15.05` | `±7.09` |
| 4.0 | `4.331` | `4.87 × 8.66` | ❌ | `±17.48` | `±11.42` |
| **12（上限，默认）** | `1.444` | `1.62 × 2.89` | ❌ | `±19.11` | `±14.31` |

**上限是一个绝对档位**：`MaxZoom`（序列化，默认 `12`），不是从地图或视口反推的。因为 `size = 基础 size / zoom` 而基础 size 随地图缩放，`12` 在任何地图、任何半径、任何屏幕上含义都一样：**最大档位下竖向看到地图深的 1/12**。想放大得更近就调大 `MaxZoom`。

（这里原来是从"最小可视宽占地图宽的比例"反推的 `1 / (ratio × aspect)`。那个公式与它的名字不符——aspect 以除法进入，展开后可视宽里 aspect 出现两次，竖屏下实际最小可视宽是地图宽的 4.1% 而不是 15%。原委见 `orthographic-map-camera-internals.md` §4.6。）

**对准**用 `FocusOn(HexCoord)` / `FocusOnWorld(Vector3)`，它**立即**把中心设成"该点被夹取后的结果"，并且**不记住任何东西**：

```
实际中心 = clamp(该点, 当前 zoom 的可移动范围)
```

所以**边缘格永远到不了视口正中心**——地图在那里就结束了。放大后夹取范围变宽，同一个边缘格会**越来越接近**中心但不会到达（`zoom = 2` 时距右边约 42% 屏宽，`zoom = 4` 时约 46%）。

**要"改档位 + 对准某点"，用一次调用 `TryZoomToPoint(zoom, worldPoint)`**，它内部**先定框、再对准**。顺序不能反：反过来（先 `FocusOnWorld` 再改 zoom）会拿**旧**档位的范围去夹取目标，边缘目标被夹在旧范围上、之后再也没有机会回到正确位置——`map.unity` 下从 zoom 1 会夹到 `10.17`，而正确值是 zoom 3 的 `16.67`，目标整个落在画面外。

**这也是相机不再保存焦点状态的原因**：把"对准谁"放进调用本身，就不需要一条"等以后某次 zoom 变化时再重新对准"的规则，也就没有那条规则与缩放锚点抢中心的问题（旧实现为此需要 `BeginGesture` / `EndGesture` 与一段 save/restore 来仲裁，那套机制随焦点状态一起删掉了）。

**两条中心规则**：

1. **降到 `zoom = 1` 会强制居中**（它代表"看全貌"）。之后再放大**不会**回到之前对准的地方——相机不记得它。
2. **其他任何 zoom 变化都保持当前中心**，只在框变窄装不下时才被夹回来。拖拽同理：没有任何"焦点"会把镜头拉回去。

**缩放锚点**取双指中点（移动端）或鼠标位置（滚轮），这样"捏住的地方不动"。三个细节：

- **锚点在手势开始时抓一次并固定整个手势**——双指中点会漂移，每帧重取会让地图抖动；
- **每帧增量式套公式**（比例始终对"手势起始距离"取，不对上一帧取），否则多帧累积会漂；
- **边缘不加补偿**：夹取会限制偏移，锚点在边缘时会略微滑动，这是可接受的。

## `map.unity` 的实际数值

参数：`Radius 11`、`Pointy`、`XZ`、`OuterRadius 1`、`SecondaryScale 0.9`、`m_ViewMargin 1.1`。

| 量 | 值 |
| --- | --- |
| 地图包络 | `39.8372 × 31.5000` |
| 最远档（zoom 1）`orthographicSize` | `17.325` |
| 竖屏 9:16 可视区 | `34.65 × 19.4906` |
| 竖屏 9:16 可移动范围 | 水平 `±10.1733`、垂直 `±0` |
| 一行占屏高 | `2.86`（约 12 行同屏） |
| 一列占屏宽 | `3.62`（约 5～6 列同屏） |

**现状参数无需改动。** `Radius`、`Orientation`、`SecondaryScale` 都保持原样就满足"上下容纳所有行、左右只容纳一部分"。

## 两条已知限制（不是缺陷）

1. **竖屏是前提。** 屏高被"所有行"钉死成 `地图深 × 1.1`，屏宽随之确定。竖屏下屏宽只有 `19.49`，装不下 `39.84` 宽的地图，所以能拖；**横屏 4:3 或 16:9 下屏宽会大于地图宽，可移动范围恒为 0，相机锁死居中**。这与朝向无关，是几何。若在横屏下发现拖不动，先查宽高比，不要去改渲染或取景代码。编辑器 Game 视图应设为竖屏分辨率（如 750×1334、1080×1920），否则预览与真机不一致。

2. **竖立装饰物（`HexPlane.XY`）在垂直俯视下不可见。** 它垂直于地图平面、骑在平面上，垂直俯视时投影退化成一条零宽线段，且与平放装饰物交叉。`map.unity` 现在的装饰物全是 `_XZ`（平放），所以看不到这个现象。要处理它必须引入倾斜或剔除层，等于推翻 `docs/adr/0001-decoration-overlay-render-layers.md` 的分层契约。**看到"竖立装饰物不见了"，先读这一条。**

## 接线（主相机上三个组件）

`map.unity` 的主相机：

1. `Camera`：勾上 **Orthographic**（这是摆放设置，控制器运行时只校验、不偷偷改）。
2. `OrthographicMapCamera`：`m_HexMapView` → 场景里的 **HexMap**；`m_Camera` → **自身**；`m_Height = 30`、`m_Near = 28`、`m_Far = 32`、`m_ViewMargin = 1.1`、`m_PlaneMode = FollowMapView`。（宽高比没有配置项，一律取 `Camera.aspect`。）
3. `OrthographicMapLayerSettings`：`m_HexMapView` → **HexMap**；`m_CullingMask` 必须包含 HexMap 的 cell 层（默认 `Everything` 即可）。
4. 回到 `OrthographicMapCamera`，把 `m_LayerSettings` 指向第 3 步那个组件。
5. `OrthographicMapDragInput`：`m_MapCamera` → 那个 `OrthographicMapCamera`。它可以从 `HexMap.Sample` 程序集挂到任意常驻物件上。
6. `OrthographicMapZoomInput`：`m_MapCamera` → 同一个 `OrthographicMapCamera`；`m_UseMouseWheel` 默认开（触摸捏合不受它影响）。

相机在 `Start()` 里自动取景一次。之后**视口变化需要调用方显式调 `TryRefresh()`**，本特性不做逐帧监听。

## 看到异常时先查这里

| 症状 | 真相 | 怎么办 |
| --- | --- | --- |
| 左右拖不动 | 当前档位下屏宽 ≥ 地图宽。可视高被"所有行"钉死，可视宽 = 可视高 × aspect ⇒ **aspect 越大越拖不动** | 先确认 Game 视图是竖屏 9:16（相机 aspect 直接取视口比例，没有配置项）；放大一档就会有可拖余量 |
| 编辑器能拖、真机不能（或反之） | 编辑器 Game 视图还是横屏分辨率 | 设为 750×1334 / 1080×1920 |
| 相机是透视的 | `orthographic` 是摆放设置 | 在场景里勾上，不是运行时问题 |
| 相机在地图**下方** | 位置必须写成 `原点 − forward × 高度`（`forward` 指向世界 −Y） | 这是实现时真踩过的坑，测试有断言钉住 `position.y == 30` |
| 最上一行/最下一行贴边或被切 | `m_ViewMargin` ≤ 1 | 保持 ≥ 1.1 |
| 画面上下有地图外空白 | 正常，可视高 = 地图深 × 1.1，多出来的是边距 | 不是 bug |
| 竖立装饰物不见了 | 垂直俯视的必然结果 | 读"两条已知限制"第 2 条 |
| 地图绕 X/Z 转了，相机在世界空间是斜的 | "俯视"是相对地图平面的 | 不是 bug |
| 拖拽手感反了 | 唯一的方向常量是 `OrthographicMapDragInput.DragDirection` | 翻它的符号（**当前场景里取 `-1`**，即按下并拖动时地图朝指针方向移动；改为 `+1` 得到"抓住地图拖"的手感） |
| 捏合时地图从手指下面滑走 | 锚点用了屏幕中心而不是双指中点 | 读"缩放与焦点"一节；这是手感问题，不是夹取问题 |
| 捏合时地图抖动 | 每帧取当前双指中点当锚点，而中点在一个手势内会漂移 | 锚点必须在**手势开始时抓一次并固定整个手势** |
| 捏合时地图被弹回某个位置 | 锚点缩放没有声明"手势进行中"，于是被"zoom 变化重新对准焦点"覆盖 | 这是实现时真踩过的坑；`TryZoomTo` 内部必须让手势优先于焦点 |
| 拖拽时地图被拽回焦点，拖不动 | 实现成了"每帧从焦点重算目标中心" | 焦点只在 `FocusOn` 与**非手势** zoom 变化两个时刻生效 |
| 放大后看不到上下两端的行 | `zoom > 1` 的必然结果（只有 `zoom = 1` 保证所有行可见） | 不是 bug；此时纵向可拖 |

## 测试与验证

- 取景数学：`Assets/Tests/EditMode/HexMap/Core/OrthographicMapFramingTests.cs`（`HexMap.Tests.EditMode`）。
- 相机、缩放、焦点、Layer：`Assets/Tests/EditMode/HexMap/UnityRuntime/OrthographicMapCameraTests.cs`（`HexMap.UnityRuntime.Tests.EditMode`）。

```powershell
powershell -File scripts\run-tests.ps1 -Assembly HexMap.Tests.EditMode
powershell -File scripts\run-tests.ps1 -Assembly HexMap.UnityRuntime.Tests.EditMode
```

**两个输入适配器没有自动化测试**：`HexMap.Sample` 只被 PlayMode 测试程序集引用，EditMode 测不到它。验收靠手动拖一次 / 捏一次；所有夹取、zoom 上下限、焦点与锚点的数学都在有测试的 `OrthographicMapCamera` 里。适配器本身只有两个纯静态函数（`ScreenDeltaToOffsetDelta`、`ScreenToViewportAnchor`、`PinchRatioToZoom`），可以脱离设备推理。

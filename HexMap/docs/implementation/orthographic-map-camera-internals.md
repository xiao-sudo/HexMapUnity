# 正交俯视地图相机：实现原理与代码导读

本文解释**为什么这样实现**，以及**每一段的数学依据**，供对着代码 Review。使用方式与排查手册见 `docs/implementation/orthographic-map-camera.md`；决策全文与已否掉的选项见 `.scratch/orthographic-map-camera/spec.md`。

代码位置：

| 文件 | 程序集 | 职责 |
| --- | --- | --- |
| `Assets/Scripts/HexMap/Core/OrthographicMapFraming.cs` | `HexMap.Core` | 全部数学。不可变结构体，零场景依赖 |
| `Assets/Scripts/HexMap/UnityRuntime/OrthographicMapCamera.cs` | `HexMap.UnityRuntime` | 状态机 + 写相机 |
| `Assets/Scripts/HexMap/UnityRuntime/OrthographicMapLayerSettings.cs` | `HexMap.UnityRuntime` | culling mask 与自检 |
| `Assets/Scripts/HexMap/Sample/OrthographicMapDragInput.cs` | `HexMap.Sample` | 拖拽输入 |
| `Assets/Scripts/HexMap/Sample/OrthographicMapZoomInput.cs` | `HexMap.Sample` | 捏合 / 滚轮输入 |

**分层理由**：`HexMap.Core` 与 `HexMap.Runtime` 禁止引用 `Camera` / `GameObject`（见 `docs/implementation/hex-map-view-rendering.md`）。取景是"给定布局算一个矩形"，属于纯数学；相机是观察者。`Sample` 才碰 `Input`。

---

## 1. 五条不变式（Review 时先记这五条）

读懂实现的前提是接受这五条约定。**任何一段代码如果在破坏它们，就是 bug。**

| # | 不变式 | 违反后的症状 |
| --- | --- | --- |
| **I1** | `zoom = 1` ⟺ 竖直方向容纳地图全部深度（还多出 `m_ViewMargin`） | 最远端切掉上下行 |
| **I2** | 可视高度只由 `地图深 × margin` 决定，**与 aspect 无关** | 换屏幕比例导致行被切 |
| **I3** | 可视宽度 = 可视高度 × aspect，**没有独立宽度旋钮** | 能配出"看不到所有行"的状态 |
| **I4** | `zoom` 是**绝对档位**，不是倍率 ⇒ 同一 zoom 应用两次结果不变 | zoom 连点两次变成两倍 |
| **I5** | 相机的实际中心 = `clamp(想要的中心, 当前 zoom 的范围)` | 相机跑到地图外 / 边缘格把地图拖偏 |

I2 与 I3 合起来就是"能不能左右拖由 aspect 决定"这条结论的来源；I1 说明"所有行可见"只属于 `zoom = 1` 这一个档位；I4 是幂等性的来源；I5 是"焦点不在视口中心"这个现象的来源——**它是夹取的输出，不是需要额外处理的例外**。

---

## 2. 坐标与轴向约定

这是最容易 Review 错的部分，先把三个坐标系钉死。

### 2.1 地图布局（`HexMap.Core.HexLayout`）

`HexLayout.HexToWorld` 把 Axial 坐标 `(q, r)` 映射到**地图局部空间**：

| 平面 | x 轴 | "平面轴"（第二个轴） |
| --- | --- | --- |
| `XZ` | 地图局部 X | 地图局部 **Z** |
| `XY` | 地图局部 X | 地图局部 **Y** |

**平面轴由 `HexPlane` 决定**，这是"兼容 XZ 与 XY"的全部含义。

### 2.2 屏幕轴向（本实现的关键约定）

`OrthographicMapCamera.ApplyToCamera` 里：

```csharp
var right = m_AppliedTransform.TransformDirection(Vector3.right).normalized;
var upLocal = m_AppliedLayout.Plane == HexPlane.XY ? Vector3.up : Vector3.forward;
var up = m_AppliedTransform.TransformDirection(upLocal).normalized;
var forward = Vector3.Cross(right, up);
```

- **屏幕右 = 地图局部 +X**（移动轴）
- **屏幕上 = 地图局部 +Z**（`XY` 平面则是 +Y）
- **相机 forward = `Cross(right, up)`** ⇒ 必然垂直于地图平面，且指向地图（向下）

三者构成右手正交基，所以 `Quaternion.LookRotation(forward, up)` 一定得到"从正上方垂直俯视"的姿态。**没有用 `Quaternion.Euler(90,0,0)`**，因为那样硬编码了"世界 Y 是俯视轴"，地图绕 X/Z 旋转后就错了。用 `Cross` 派生等价于声明：**"俯视"是相对地图平面的**（`spec.md` 2.1）。

`Up` 指向地图平面法线的反方向（相机在上方往下看），`forward` 因此指向地图——这个符号在 3.4 节会解释一次踩坑。

### 2.3 相机位置（`ApplyToCamera` 的后半段）

```csharp
var position = m_AppliedTransform.TransformPoint(m_Framing.Origin)   // 地图中心（世界）
    + right * (m_DesiredCenter.x * m_AppliedScale)                    // 水平平移
    + up   * (m_DesiredCenter.y * m_AppliedScale)                     // 垂直平移
    - forward * (m_Height * m_AppliedScale);                          // 沿视线轴后退
```

- `m_Framing.Origin` 是**地图局部**中心，经 `TransformPoint` 变世界坐标——所以地图平移/旋转后相机跟着走，不需要额外代码。
- `m_DesiredCenter` 是**地图局部的平面坐标**（I5 里的"想要的中心"），不是世界坐标。这样地图旋转后平移方向仍然贴着地图的轴，不会跑偏。
- **`- forward`**：`forward` 指向地图，所以沿它**反方向**退 `m_Height` 才是"在平面上方"。写成 `+ forward` 会把相机放到地图**下面**（`spec.md` 第 4 节失败表有这一条，测试断言 `position.y == 30`）。
- `m_AppliedScale` 来自 `HexMapView.transform.lossyScale.x`（地图只允许均匀缩放，`HexMapView` 已校验）。所有世界距离都要乘它，否则当地图被放大 2 倍时 `m_Height` 与 `near/far` 会对不上——测试 `NonIdentityMapTransformAndScaleAreRespected` 钉住这一点。

---

## 3. 取景数学（`OrthographicMapFraming`）

### 3.1 为什么要算"包络"而不是"包围盒"

先看一个反直觉点：**不能用 `Renderer.bounds` 求地图范围**。生产场景 `map.unity` 用的是 `DrawMeshInstanced` 策略，那条路径**根本不创建 cell 的 `GameObject`**（`DrawMeshInstancedStrategy` 只提交矩阵）。所以包围盒只能**解析推导**。

包络 = **格心铺开 + 一格半尺寸**（`TryCreate` 中段）：

```
Pointy:  格心 (x, 平面) = (√3·R·o,        1.5·R·o·s)
         格子 (x, 平面) = (√3/2·o,        o·s)
Flat:    格心 (x, 平面) = (1.5·R·o·s,     √3·R·o·s)
         格子 (x, 平面) = (o,             √3/2·o·s)

MapHalfWidth = 格心.x + 格子.x
MapHalfDepth = 格心.平面 + 格子.平面
```

**两步都必须算**：`HexLayout` 的铺开量只到**最外圈格心**，格子自身的凸包还要再往外伸半格。漏掉第二步会裁掉最外圈格子的尖（`Pointy` 下少算 `0.866`），这是一个真实修过的 bug。

**格子半尺寸的两个分量不相等**，因为 `HexCellMeshFactory` 把 `secondaryScale` 乘进了平面分量：

```
Pointy:  (√3/2·o,  o·s)          ← 上下不等
Flat:    (o,       √3/2·o·s)     ← 左右不等
```

这条也推翻了"`Pointy` 与 `Flat` 的包络只是对调"这个直觉——**格心对调，格子半尺寸不对调**：

| | 地图包络（`R=11, o=1, s=0.9`） | 宽/深 |
| --- | --- | --- |
| `Pointy` | `39.8372 × 31.5000` | 1.265 |
| `Flat` | `31.7000 × 35.8535` | 0.884 |

测试 `FlatAndPointyEnvelopesAreNotSwappedBecauseCellProfilesDiffer` 钉住它，防止下一个人"顺手简化成对调"。

### 3.2 尺寸与可视区（I1 / I2 的来源）

```
BaseOrthographicSize = MapHalfDepth · viewMargin          // zoom = 1 时的半高
OrthographicSize     = BaseOrthographicSize / zoom         // I4：绝对档位
VisibleHeight        = 2 · OrthographicSize
VisibleWidth         = VisibleHeight · aspect              // I3：宽度是派生量
```

**`Camera.orthographicSize` 本身就是半高**，不是全高。这是最容易写错的一处：写成 `MapHalfDepth · viewMargin / 2` 会让可视高只有地图深的一半。测试 `OrthographicSizeIsTheHalfHeightThatFitsEveryRow` 与 `ZoomingInPastTheViewMarginDropsRowsFromTheFrame` 钉住它。

由 `BaseOrthographicSize = MapHalfDepth · margin` 直接得出：

```
zoom = 1 时   VisibleHeight = MapDepth · margin > MapDepth        ← I1：所有行可见
丢行的精确阈值 zoom = margin（不是 1！）                            ← margin = 1.1 时是 1.1
```

**这一点反复被写错**，所以文档和测试都用不等式表达，而不是让读者记数字。

### 3.3 可移动范围与夹取

`OrthographicMapFraming` 只算**水平**范围（`MovableHalfRange = max(0, MapHalfWidth − VisibleWidth/2)`）；**垂直**范围在相机里现算（`VerticalHalfRange`，`max(0, MapHalfDepth − VisibleHeight/2)`）。

为什么不对称？因为 framing 的职责是"给定额定取景算出**包络级**的量"，而垂直范围依赖 `zoom`（framing 已经带了 zoom），水平范围历史上是 framing 的主输出（`MinOffset/MaxOffset` 保留在 framing 上）。**这是历史分层留下的不一致，不是设计意图**；如果你 Review 时觉得应该把两轴都放进 framing，那是个合理的整理方向（`MinOffset/MaxOffset` 在 framing 上现在只剩 `ClampOffset` 等旧接口在用）。

`ClampOffset` / `TrySetOffset` / `NormalizeOffset` 是 `zoom = 1` 时代的一维接口，现在只服务于 framing 自身的单测；相机用的是自己的 `ClampToRange`。

### 3.4 相机里的两轴夹取

```csharp
private Vector2 ClampToRange(Vector2 offset)      // 逐轴独立
{
    var min = MinOffset;   // new Vector2(-MovableHalfRange, -VerticalHalfRange)
    var max = MaxOffset;   // new Vector2( MovableHalfRange,  VerticalHalfRange)
    return new Vector2(
        offset.x < min.x ? min.x : (offset.x > max.x ? max.x : offset.x),
        offset.y < min.y ? min.y : (offset.y > max.y ? max.y : offset.y));
}
```

**逐轴独立**很关键：范围是"矩形"，不是"圆"。用 `magnitude` 夹取会让画面沿对角方向提前停住。

---

## 4. 状态机（`OrthographicMapCamera`）

### 4.1 字段与它们的不变式

| 字段 | 含义 | 不变式 |
| --- | --- | --- |
| `m_BaseFraming` | `zoom = 1` 的 framing | `TryRefresh` 成功后非 `default` |
| `m_Framing` | 当前 zoom 的 framing = `m_BaseFraming.WithZoom(m_Zoom)` | 只在 `ApplyZoom` / `TryRefresh` 里更新 |
| `m_Zoom` / `m_TargetZoom` | 当前 / 目标档位 | 都在 `[1, MaxZoom]`；`Tick` 让前者追后者 |
| `m_DesiredCenter` | **想要**的中心（地图局部平面坐标） | 始终在 `[MinOffset, MaxOffset]` 内 |
| `m_HasCenter` | 是否被显式设过 | 见 4.4 的坑 |
| `m_Focus` / `m_HasFocus` | 焦点（地图局部平面坐标） | 独立于 `m_DesiredCenter` |
| `m_IsGestureActive` | 手势是否拥有本次 zoom | 见 4.3 |
| `m_AppliedLayout` / `m_AppliedTransform` / `m_AppliedScale` | 在 `TryRefresh` 里快照 | 让 `TrySetOffset` 等无需重新解析配置 |

**为什么快照 layout/transform/scale？** 因为 `TrySetOffset`、`FocusOnWorld`、`ApplyToCamera` 都需要它们，而重新解析要走 `HexMapView.HasMap` 校验与 `TryResolvePlaneAndOrientation`。快照让"平移/对焦"变成纯数学，**不需要渲染器已经构建**，也让 EditMode 测试不必依赖 `HexMapView.Build()`。

### 4.2 `TryRefresh`：唯一重建基础 framing 的地方

顺序（`OrthographicMapCamera.TryRefresh`）：

1. 引用与配置校验（`HexMapView` / `Camera` / 正交 / near-far / height）；
2. `TryGetLayout`：要求 `HexMapView.HasMap`（即 `Build()` 已跑过）；
3. `TryResolvePlaneAndOrientation`：`ForceXY` / `ForceXZ` 与地图实际平面不符 ⇒ **失败**，不静默取其一；
4. `LayerSettings.TryValidate`（有接的话），取 `cullingMask`；
5. `OrthographicMapFraming.TryCreate(...)` 得到 `m_BaseFraming`；
6. 修正 `m_Zoom` / `m_TargetZoom`：**非有限或 ≤ 0 回落到 1**（Unity 新建组件的 `float` 是 0，若不管会导致"放大到无穷"）；
7. `ApplyInitialFocus()`、按需清零中心、`WithZoom`、`AlignCenterToFocus`、`ClampCenter`、`ApplyToCamera`。

这个函数是**幂等**的：连续调两次结果相同（除了 `ResolveAspect` 会重新读 `Camera.aspect`）。

**`orthographic` 只校验、不写入**。它是"摆放设置"，与 `sortingOrder` 同类（ADR-0001 的结论）。控制器在运行时偷偷改它，会造成"编辑器里是透视、跑起来变了"。

### 4.3 zoom 变化的**唯一**通路：`ApplyZoom`

```csharp
private void ApplyZoom()
{
    m_Zoom = ClampZoom(m_Zoom);
    m_Framing = m_BaseFraming.WithZoom(m_Zoom);

    if (m_Zoom <= MinZoom) { m_DesiredCenter = Vector2.zero; m_HasCenter = false; }   // 规则 A
    else if (m_HasFocus && !m_IsGestureActive) { m_DesiredCenter = ClampToRange(m_Focus); m_HasCenter = true; }  // 规则 B

    ClampCenter();
    ApplyToCamera();
}
```

三条入口都汇聚到这里：`Zoom` setter、`Tick`（插值）、`TryZoomTo`。**Review 时确认没有任何别的地方改 `m_Framing`**。

- **规则 A**：`zoom = 1` 是"看全貌"状态 ⇒ 强制居中，忽略焦点。焦点本身仍保留，再放大时按规则 B 重新对准。
- **规则 B**：非手势的 zoom 变化重新对准焦点。`m_IsGestureActive` 是这条规则的闸门。

### 4.4 三个必须写死的优先级（都踩过坑）

**① 锚点缩放必须自己算一次手势。**

`TryZoomTo` 里：

```csharp
m_DesiredCenter = ClampToRange(m_DesiredCenter + anchorOffset * growth);
...
var wasGestureActive = m_IsGestureActive;
m_IsGestureActive = true;      // ← 关键
ApplyZoom();
m_IsGestureActive = wasGestureActive;
```

少了这三行，规则 B 会立刻把锚点算出的中心**覆盖成焦点**：镜头弹回焦点，捏合完全失效。这就是"锚点缩放与 zoom 重对准焦点打架"。

**② `m_HasCenter` 的存在理由。**

`m_DesiredCenter` 默认是 `(0,0)`，而"用户把地图拖到正中"也是 `(0,0)`。**两者不可区分**，若用 `m_DesiredCenter != Vector2.zero` 判断"用户是否设过"，那么用户拖回正中后 `TryRefresh` 会把它当成"没设过"而重置。所以需要一个独立的布尔。

**③ 拖拽不改焦点。**

`TrySetOffset` 只改 `m_DesiredCenter` 并置 `m_HasCenter`，**不碰 `m_Focus`**。于是：

| 动作序列 | 结果 |
| --- | --- |
| `FocusOn` → 拖走 → **捏合/手势** zoom | 保持拖走后的位置（手势闸门） |
| `FocusOn` → 拖走 → **代码**改 `Zoom` | 重新对准焦点（规则 B） |
| `FocusOn` → 拖走 → zoom 回 1 | 居中（规则 A） |

如果实现成"每帧从焦点重算目标中心"，用户会**拖不动**（每帧被拉回）。这条在 `AGestureZoomDoesNotPullTheCameraTowardsTheFocus` 里钉住。

### 4.5 插值

```csharp
public void Tick(float deltaTime)     // Update() 调用它，测试也直接调
{
    if (Mathf.Approximately(m_Zoom, m_TargetZoom)) return;
    m_Zoom = m_ZoomSpeed > 0f && deltaTime > 0f
        ? Mathf.MoveTowards(m_Zoom, m_TargetZoom, m_ZoomSpeed * deltaTime)
        : m_TargetZoom;
    ApplyZoom();
}
```

- **`zoom` 的插值是"档位/秒"**（`m_ZoomSpeed = 6`），不是指数逼近。这样到端点**硬停**（与"无惯性"一致），也不会出现永远逼近不到的死循环。
- **`Tick` 是 public**：让测试与调用方能驱动时间，不依赖 Unity 的帧循环。`Update` 只是它的一个调用点。
- 插值过程中**每帧**重算 `m_Framing` 与范围 ⇒ 相机在缩放动画里始终被夹取，不会中途跑出地图。

### 4.6 zoom 上下限

```csharp
public float MaxZoom { get { return m_MaxZoom; } set { m_MaxZoom = value; } }   // 序列化，默认 12
```

**上限是一个绝对档位，不是从地图或视口反推的。**

zoom 的定义本身就是相对的：`size(zoom) = BaseOrthographicSize / zoom`，而 `BaseOrthographicSize = 地图半深 × ViewMargin` 随地图缩放。所以「`MaxZoom = 12`」在任何地图、任何半径、任何屏幕上含义都一样：**最大档位下竖向看到地图深的 1/12**。这正是"比例参数"想买到的地图/设备无关性——而它本来就已经有了。

**为什么去掉了原来的 `m_MinVisibleWidthRatio`**（历史，别走回头路）：旧实现是 `MaxZoom = 1 / (ratio × aspect)`，名字声称"最小可视宽占地图宽的比例"，但 aspect 以**除法**进入，展开后

```
可视宽(MaxZoom) = 地图深 · margin · ratio · aspect²        ← aspect 出现两次
```

`ratio = 0.15`、竖屏 9:16 下实际最小可视宽是地图宽的 **4.13%**，不是 15%；按名字反推应得 `MaxZoom = 3.26`，实现却给了 `11.85`（**大 3.63 倍**）。它只在 `aspect = 1` 时与名字一致（精确条件是 `地图宽 == 2·BaseSize·aspect²`；旧版这里写的 `MapHalfDepth·ViewMargin == MapHalfWidth` 同样漏掉了 aspect²）。同一个耦合在横屏 16:9 下把上限压到 `3.75`，与竖屏差 3 倍多，却换不来任何能一句话说清的保证。

**结论**：上限就用绝对档位。如果哪天真要"内容保证"，用**高度型** `MaxZoom = margin / ratio`（与 aspect 无关），不要再把 aspect 乘进除法里。

三个端点与特例：

- 下限固定 `MinZoom = 1`（唯一"所有行可见"的档位）。
- `MaxZoom == 1` **合法**：那是一台"只平移、从不缩放"的相机。`MaxZoom < 1` 或非有限值 ⇒ `TryRefresh` 拒绝取景并报 `"Max zoom must be finite and at least 1."`（否则 `size` 会变成 0 而不是报错）；`ClampZoom` 另有兜底，坏上限退化成下限，不会变成 0。
- 上限**不再依赖 framing**，所以 `Zoom` / `TargetZoom` / `SetZoomImmediate` 的 setter **一律**走 `ClampZoom`。以前"没有 framing 时不做上限夹取、要等第一次 `TryRefresh` 才补夹"的那个特例（以及 `NormalizeZoom`）已经删除。

---

## 5. 焦点与锚点

### 5.1 焦点是"想去"，不是"就在"（I5）

```csharp
public bool FocusOnWorld(Vector3 worldPoint, out string error)
{
    m_Focus = ToPlaneCoordinates(worldPoint);
    m_HasFocus = true;
    m_DesiredCenter = ClampToRange(m_Focus);   // ← 夹取决定实际位置
    m_HasCenter = true;
    ApplyToCamera();
}
```

`ToPlaneCoordinates` 把世界点经 `InverseTransformPoint` 转地图局部，再取两个平面分量：

```csharp
return m_AppliedLayout.Plane == HexPlane.XY
    ? new Vector2(local.x, local.y)
    : new Vector2(local.x, local.z);
```

`FocusOn(HexCoord)` 先算世界中心再走上面这条：

```csharp
if (HexCoord.Distance(HexCoordOrigin, coordinate) > m_HexMapView.Radius) { 失败 }
var world = m_AppliedTransform.TransformPoint(m_AppliedLayout.HexToWorld(coordinate));
```

**用距离判断"是否在地图内"**，而不是 `HexMap.Query(...).HasCell`：半径 R 的六边形地图恰好是"距原点 ≤ R 步"的格子集合（`CONTEXT.md` 对"地图半径"的定义），所以距离判据是精确的，而且**不需要渲染器**。

数值示例（`map.unity`，`zoom = 2`，水平范围 `±15.05`）：把焦点设在最外圈 Hex（局部 `X = 19.05`），夹取后中心是 `+15.05`，该格落在屏幕偏右——**"焦点不在正中心"是 I5 的必然结果**。

### 5.2 锚点缩放（`TryZoomTo`）的推导

要让锚点下的世界点 `w` 固定，设锚点在视口内的归一化坐标 `anchor ∈ [-1,1]²`：

```
w = center + anchor ⊙ halfSize(old)                  （⊙ = 逐分量乘）
要求 center' + anchor ⊙ halfSize(new) = w
⇒ center' = center + anchor ⊙ halfSize(old) − anchor ⊙ halfSize(new)
          = center + anchor ⊙ halfSize(old) · (1 − newSize/oldSize)
```

代码里 `halfSize` 用的是 `VisibleWidth/2` 与 `VisibleHeight/2`：

```csharp
var anchorOffset = new Vector2(
    viewportAnchor.x * m_Framing.VisibleWidth * 0.5f,
    viewportAnchor.y * m_Framing.VisibleHeight * 0.5f);
var growth = 1f - newSize / oldSize;
m_DesiredCenter = ClampToRange(m_DesiredCenter + anchorOffset * growth);
```

**`newSize` 用 `m_BaseFraming.OrthographicSize / clamped` 而不是"当前 size 再乘系数"**——这是 I4（绝对档位）在代码里的体现。若写成 `m_Framing.OrthographicSize * k`，多帧捏合会累积误差，且 `TryRefresh` 之后基准会漂。

`anchor = (0,0)` 时 `anchorOffset = 0` ⇒ **退化成中心锚点，不需要两套代码**。

公式里**没有地图尺寸**，只有"锚点在视口内的归一化位置 + 两帧的 size"⇒ **不需要射线求交**。

### 5.3 三个必须遵守的细节

| 细节 | 代码位置 | 违反后果 |
| --- | --- | --- |
| 锚点在**手势开始**时抓一次并固定整个手势 | `OrthographicMapZoomInput.UpdatePinch` 的 `if (!m_IsGestureActive)` 分支 | 双指中点漂移 ⇒ 地图抖动 |
| 每帧增量式套公式（比值对**手势起始**距离取，不对上一帧取） | `PinchRatioToZoom(m_PinchStartZoom, m_PinchStartDistance, distance)` | 多帧累积 ⇒ 偏移漂移 |
| 边缘**不加补偿** | 夹取后就结束 | 加补偿会引入反方向漂移，比"边缘略滑"更难解释 |

`PinchRatioToZoom` 的签名刻意只收 `(startZoom, startDistance, currentDistance)`——**没有"上一帧距离"这个参数**，从类型上就无法写出累积式实现。这是把纪律编码进签名。

---

## 6. 输入适配器

### 6.1 拖拽（`OrthographicMapDragInput`）

```csharp
public static Vector2 ScreenDeltaToOffsetDelta(
    Vector2 screenDelta, float visibleWidth, float visibleHeight,
    float screenWidth, float screenHeight)
{
    ...
    return new Vector2(
        screenDelta.x / screenWidth * visibleWidth * DragDirection,
        screenDelta.y / screenHeight * visibleHeight * DragDirection);
}
```

- 用**当前 zoom 的** `visibleWidth/Height`（来自 `TryGetFraming`），所以放大后同样的手指位移对应更小的世界位移——手感一致。
- **不写死"每像素多少世界单位"**，否则换机型手感不同。整屏拖满 = 正好移动一个可视宽度。
- `DragDirection = +1` 表示"指针右移 ⇒ 相机右移 ⇒ 内容左移"，即**抓住地图拖**。这是唯一的方向常量，翻符号即反向手势。
- 拖拽**不声明手势**（它不改 zoom），也不夹取（夹取在相机里）。

### 6.2 捏合 / 滚轮（`OrthographicMapZoomInput`）

- 锚点：`ScreenToViewportAnchor` 把屏幕像素映射到 `[-1,1]²`。
- 捏合：`distance = |p0 − p1|`，`ratio = distance / startDistance`，`zoom = startZoom × ratio`。
- 滚轮：`zoom × 1.15^scroll`（指数，保证多格滚动可加）。
- 手势生命周期：进入两指时 `BeginGesture()` 并抓锚点；退出两指时 `EndGestureIfActive()`。**滚轮不开手势**（它每次只改一帧的目标，且不涉及"拖到别处后不该被拉回"这个场景）。
- `ApplyZoom` 统一调 `m_MapCamera.TryZoomTo(zoom, m_AnchorViewport)`；失败只记一条 warning，不抛。

**一个已知的小不一致**：滚轮路径设置 `m_AnchorViewport` 后调 `ApplyZoom`，而 `ApplyZoom` 里还调了 `m_MapCamera.BeginGesture()` 与 `EndGesture()`（见 `OnDisable` / `EndGestureIfActive`）——滚轮不经过捏合的 `m_IsGestureActive` 状态，所以 `OrthographicMapZoomInput.IsGestureActive` 对角滚轮**恒为 false**。这不影响正确性（相机内部的 `m_IsGestureActive` 由 `TryZoomTo` 自己临时置真），但属性语义只对触摸成立。Review 时可考虑改名或补上滚轮的手势标记。

---

## 7. 生命周期

| 时机 | 行为 | 理由 |
| --- | --- | --- |
| `Start()` | 自动 `TryRefresh()` 一次，失败 `Debug.LogError` | **不能用 `Awake()`**：`HexMapView.Build()` 在它自己的 `Awake()` 里跑，而组件间 `Awake` 顺序未定义 |
| 之后 | **不逐帧做任何事**（除非在 zoom 插值中） | 视口变化需调用方显式 `TryRefresh()`；本特性不监听分辨率 |
| `Tick` | 只在 `m_Zoom != m_TargetZoom` 时工作 | 静止时零开销 |

`Update` → `Tick(Time.deltaTime)`；`Tick` 也公开给测试与调用方。

---

## 8. 已知限制与刻意不做

| 项 | 说明 |
| --- | --- |
| 竖屏是前提 | 横屏 4:3 / 16:9 下 `zoom = 1` 的可移动范围就是 0（屏宽 > 地图宽）。这是几何，不是缺陷 |
| 竖立装饰物不可见 | `HexPlane.XY` 贴片垂直于地图平面，垂直俯视下投影退化成线段。要处理就得引入倾斜或剔除层，等于推翻 ADR-0001 |
| 输入层无自动化测试 | `HexMap.Sample` 只被 PlayMode 测试程序集引用。验收靠手动；纯静态换算函数（三个）可脱离设备推理 |
| 不做 | 惯性 / 回弹 / 吸附整格；双层相机 / LOD；边界回弹；`orthographicSize` 的独立宽度旋钮；"整图入画"的更远端 |
| `secondaryScale` 被乘两次 | `HexLayout.HexToWorld` 与 `HexCellMeshFactory` 都乘了 `s`，格心距与格子尺寸不一致 ⇒ 地图上有 `1−s` 比例的缝隙。**既有行为，本特性只如实反映，未修** |

---

## 9. Review 时的检查清单

对着下面几条逐项确认；每条都对应一个已发生过的错误。

- [ ] `ApplyToCamera` 的位置是 `- forward * height` 而不是 `+ forward`（相机在上方）
- [ ] `orthographicSize` 是**半高**（`halfDepth * margin / zoom`）
- [ ] 包络 = 格心铺开 **+** 格子半尺寸（两步）
- [ ] `WithZoom` 用 `BaseOrthographicSize / zoom`，不用当前 size 相乘
- [ ] `ApplyZoom` 是唯一更新 `m_Framing` 的地方
- [ ] `TryZoomTo` 里锚点公式先于 `ApplyZoom`，且期间 `m_IsGestureActive = true`
- [ ] `TrySetOffset` 不改 `m_Focus`
- [ ] 夹取**逐轴独立**
- [ ] 世界距离都乘了 `m_AppliedScale`
- [ ] 所有失败走 `Try* + out string error`，`error` 非空
- [ ] `m_Zoom <= 0` 回落到 1
- [ ] 没有 `if (plane == XZ)` / `if (orientation == Pointy)` 这类分支混进取景数学

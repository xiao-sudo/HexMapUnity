# 02 — 正交俯视相机控制器与场景接线

**What to build:** 一个挂在 `map.unity` 主相机上的 MonoBehaviour，把 01 的取景数学写成真实的 `Camera` 状态；加一个独立的 Layer 配置组件；把 `map.unity` 的主相机改成正交。**不含输入**。

**Blocked by:** 01 — 正交俯视取景的纯数学

**Status:** in-progress

## 范围

### 控制器（已完成）

- [x] 新增 `OrthographicMapCamera`（`Assets/Scripts/HexMap/UnityRuntime/OrthographicMapCamera.cs`）：
  - 序列化：`m_HexMapView`、`m_Camera`、`m_LayerSettings`（可选）、`m_Height`（30）、`m_Near` / `m_Far`（28 / 32）、`m_ViewMargin`（1.1）、`m_TargetAspect`（**`9f/16f`，竖屏**）、`m_PlaneMode`（`FollowMapView` / `ForceXY` / `ForceXZ`）、`m_Offset`
  - `TryRefresh(out error)`：按当前视口/平面/宽高比重算并写入相机；**一次性调用，不进逐帧路径**；`Start()` 里自动调一次
  - `Offset` 读写（写入即夹取并立即应用）、`TrySetOffset`、`TryGetFraming`、`IsLockedToCenter`
- [x] 朝向：相机 `up` 取地图平面的局部 `+Z`（`XY` 平面取 `+Y`）经地图 transform 变换；`right` 取地图局部 `+X`；`forward = cross(right, up)`；位置 = 地图原点 **后退 `-forward × m_Height`**（写在 `+forward` 方向会把相机放到地图下方——实现时真踩过）
- [x] `Offset` 是**地图局部 X 上的标量**，世界位置 = `right × Offset × lossyScale.x`
- [x] **平面与朝向的四种组合都成立**，`OrthographicMapFraming` 内部无平面/朝向分支；朝向只有跟随 `HexMapView` 一条路径
- [x] `ForceXY` / `ForceXZ` 与地图实际平面不符 → `TryRefresh` 返回失败原因，**不静默取其一**
- [x] 宽高比：`Camera.aspect` 与 `m_TargetAspect` 差 ≤ `AspectTolerance`（0.001）时保留目标值，否则用实际值并重夹偏移
- [x] `orthographic` **不由控制器写入**（只在 `ApplyToCamera` 之外校验）；为 `false` 时 `TryRefresh` 返回"请在场景里勾选"的原因
- [x] 校验地图 transform 的 `lossyScale.x` 有限且非零；高度、near/far、偏移都按该缩放换算

### Layer 配置组件（已完成）

- [x] 新增 `OrthographicMapLayerSettings`（独立于 `HexMapView`）：序列化 `m_HexMapView` + `m_CullingMask`（默认 `~0`）
- [x] 自检：未引用 `HexMapView`、层号越界、或 mask 未包含 cell 层 → `TryValidate` 返回失败原因
- [x] **不接管** `HexMapView.m_CellLayer` 的写入。为此给 `HexMapView` 加了一个只读 `CellLayer` 属性（唯一的既有文件改动）
- [x] 控制器在 `m_LayerSettings` 有值时校验并把 mask 写进相机；未接线时**不碰**相机的 `cullingMask`

### 场景接线（未完成，留给人工）

- [ ] `map.unity` 主相机：勾上 `orthographic`（**已由使用者在编辑器里完成**：场景现在是 `orthographic: 1`）
- [ ] `map.unity` 主相机：挂 `OrthographicMapCamera` + `OrthographicMapLayerSettings`，并把 `m_HexMapView` 指向 HexMap 的 `HexMapView`、`m_Camera` 指向自身
- [ ] `m_Near` / `m_Far` 按 28 / 32 配好；`m_TargetAspect` 保持 0.5625
- [ ] `test.unity` / `single.unity` / `SampleScene.unity` **一律不碰**
- [ ] 不新建任何 `.meta`（交给 Unity）

> 场景 YAML **不由本票手改**：Unity 很可能正打开着 `map.unity`（使用者在编辑器里改过它），外部改 YAML 会被覆盖或冲突。三条接线在 Inspector 里点三下即可。

## 验收

- [x] EditMode 测试（`HexMap.UnityRuntime.Tests.EditMode`）用例已写入 `OrthographicMapCameraTests.cs`，覆盖：
  - 造 GameObject + `Camera` + `HexMapView`，`TryRefresh()` 后断言 `orthographicSize` / `position` / `rotation` **真的被写成期望值**（不只是公式对）
  - **竖屏 9:16 下**：`orthographicSize == 17.325`、相机在 `y = 30`、`MaxOffset ≈ 10.173`，且地图两条深度边界都落在 `near..far` 之间（所有行入画）
  - **横屏 16:9 下**：`MinOffset == MaxOffset == 0`，写入偏移后位置不变（把"横屏锁死"钉成期望行为）
  - `TrySetOffset` 越界夹取、`NaN` 返回 `false` + 非空原因；`Offset` 属性写入同样夹取并立即应用
  - `Flat` 朝向 → 包络与 size 按各自公式变化，**所有行仍入画**，竖屏下仍可平移
  - `XY` 平面 → `right/up/forward` 三个轴正确，且 size 与 `XZ` 相同
  - `Camera.aspect` 从竖屏改成横屏 → size 不变（只由地图深决定）、偏移被夹回 0、**所有行仍入画**
  - 目标宽高比在容差内时被保留
  - `orthographic = false` / 未 `Build()` 的地图 / 缺视图 / 缺相机 / `ViewMargin < 1` → 各自返回可读原因，且不发布 framing
  - `PlaneMode = ForceXY` 与 `XZ` 地图不符 → 返回失败
  - Layer 组件：默认 mask 通过、未覆盖 cell 层被拒、覆盖后 mask 真的写进相机、缺视图被拒
  - 非恒等 transform 与 2 倍缩放：位置、size、偏移都按缩放换算，旋转仍垂直朝下
- [x] **用一个临时 stub harness 把上面这些断言跑通（56 项，全部通过）**，编入的是**真实的** `OrthographicMapFraming` / `OrthographicMapCamera` / `OrthographicMapLayerSettings` 源码；harness 不进仓库
- [ ] **Unity 内尚未执行**：NUnit 用例由使用者运行（`powershell -File scripts\run-tests.ps1 -Assembly HexMap.UnityRuntime.Tests.EditMode`）
- [ ] `map.unity` 在编辑器里接好线，Game 视图设为竖屏分辨率，看到完整所有行、左右可拖动的余量存在
- [ ] C# 编译通过（Unity 已生成含新文件的 `.csproj`，未报错）
- [ ] 回归：`powershell -File scripts\run-tests.ps1 -Assembly HexMap.UnityRuntime.Tests.EditMode`

## 已知取舍

- `Refresh()` 是**显式调用**而不是 `Update()` 里跑。分辨率变化（PC 拖窗口、iPad 旋转）需要调用方再调一次；本票不提供自动监听。
- 相机高度与 near/far 是**场景配置**（默认 30 / 28 / 32），不是从内容包围盒推导。理由：装饰物没有高度常量，唯一知道真实高度的是场景。
- 竖立装饰物（`HexPlane.XY`）在垂直俯视下不可见，**本票不处理**（见 `spec.md` 3.4）。
- **目标宽高比是竖屏 `9:16`**（`m_TargetAspect` 默认 `9f/16f`），并且在 `Camera.aspect` 与它差异超容差时改用实际值。**若在横屏下取景，可移动范围会变成 0（相机锁死居中）**——这不是控制器缺陷，是几何（见 `spec.md` 2.3 / 2.4 的对照表）。编辑器 Game 视图请设成 750×1334 / 1080×1920，否则预览与真机不一致。
- **现状地图参数无需改动**：`Radius 11`、`Pointy`、`XZ`、`s = 0.9` 在竖屏 9:16 下即为"所有行入画 + 可拖 ±10.17"。不要为了"能拖动"去压 `m_SecondaryScale`，那是横屏下的结论。

## Comments

- 决策依据见 `../spec.md` 第 2.1 / 2.2 / 2.3 / 2.4 / 2.5 / 2.6 / 2.8 / 2.9 节。
- 实现完成。临时 stub harness 跑通 56 项断言，其中抓到并修掉一个真 bug：**相机被放到了地图下方**（位置写成 `origin + forward × height`，而 `forward` 指向世界 −Y）。正确写法是 `origin − forward × height`。测试与 harness 都有断言钉住 `position.y == 30`。
- 自动取景放在 `Start()` 而不是 `Awake()`：`HexMapView.Build()` 在它自己的 `Awake()` 里跑，而 MonoBehaviour 之间的 `Awake` 顺序未定义，放 `Awake()` 会在部分情况下先于地图构建而失败。
- 给 `HexMapView` 加了只读属性 `CellLayer`（`m_CellLayer` 原本没有任何读取入口），供 Layer 组件自检使用。这是本票唯一的既有文件改动。
- 未写任何 `.meta`；Unity 已自行生成。

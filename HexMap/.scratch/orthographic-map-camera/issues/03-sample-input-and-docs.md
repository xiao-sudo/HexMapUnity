# 03 — 拖拽输入适配器与实现文档

**What to build:** 在 `HexMap.Sample` 里加一个薄薄的拖拽适配器，把鼠标/触摸水平拖动翻译成相机控制器的偏移变化（内容跟手）；并把本特性的决策与已知限制写进 `docs/implementation/`。

**Blocked by:** 02 — 正交俯视相机控制器与场景接线

**Status:** in-progress

## 范围

### 拖拽适配器（`HexMap.Sample`）（已完成）

- [x] 新增 `OrthographicMapDragInput`（`Assets/Scripts/HexMap/Sample/`，与 `Facade.cs` 同层）：`HexMap.UnityRuntime` **不引用它**
- [x] 输入：`Input.GetMouseButton(0)` + `Input.mousePosition`（同时覆盖鼠标与单指触摸）
- [x] 映射：**内容跟手**。方向只有一处符号：`OrthographicMapDragInput.DragDirection`（`+1` = 指针右移 → 相机右移 → 内容左移，即"抓住地图拖"）。把它翻符号就是反向手势
- [x] 换算：`屏幕像素位移 / Screen.width × framing.VisibleWidth`，**不写死"每像素多少世界单位"**，所以换机型手感一致。抽成纯静态方法 `ScreenDeltaToOffsetDelta`，可脱离设备推理
- [x] 无阻尼、无缓动、无惯性、无灵敏度系数：1:1
- [x] 只处理**水平**分量（垂直拖动不改变任何东西）。**注意坐标对应关系容易写反**：屏幕**水平**方向 = 地图局部 **+X**（相机的平移轴）；屏幕**上下**方向 = 地图局部 **+Z**。偏移量是沿局部 +X 的标量，由 `OrthographicMapCamera` 负责换算成世界位置
- [x] 越界时由控制器的夹取处理，适配器**不自己夹**
- [x] 控制器未就绪 / framing 未就绪 / `TrySetOffset` 失败 → 记一条 `Debug.LogWarning` 后忽略本次输入，不抛异常

### 文档（已完成）

- [x] 新增 `docs/implementation/orthographic-map-camera.md`：取景公式、`map.unity` 实际数值、**竖屏是前提**、竖立装饰物限制、接线步骤、异常排查表、测试位置
- [x] 更新 `docs/agents/testing.md` 的程序集表（`HexMap.UnityRuntime.Tests.EditMode` 补上 `OrthographicMapCameraTests`）
- [x] **不写 ADR**（`spec.md` 3.9）

## 验收

- [x] **用一个临时 stub harness 把适配器的映射与夹取跑通（20 项，全部通过）**，编入的是真实源码；harness 不进仓库
- [ ] 在 `map.unity` 里手动拖一次：内容跟手、方向正确、拖到两端停住、松手即停
- [ ] **在竖屏分辨率（如 750×1334、1080×1920）下与另一种竖屏比例下各拖一次**，手感一致（验证比例换算而非写死像素比）。**横屏下拖不动是预期行为**，见 `spec.md` 2.3
- [ ] C# 编译通过

## 已知取舍

- 适配器**没有自动化测试**是明确接受的代价（`spec.md` 2.7）。`HexMap.Sample` 只被 PlayMode 测试程序集引用，EditMode 测不到它。它足够薄：读一个水平位移 → 按比例换算 → 写一个值；所有夹取与边界逻辑都在 01/02 的有测试覆盖的代码里。
- 不做长按/双击/惯性/回弹吸附。
- 不在本票改 `map.unity` 的接线（那是 02 的范围）。

## Comments

- 决策依据见 `../spec.md` 第 2.7 节与第 3 节第 6、9 条。
- 实现完成。修正了一条**写反的说明**：本票初稿写"屏幕水平方向对应世界 Z 轴"，实际是屏幕水平 = 地图局部 +X（平移轴）、屏幕上下 = 地图局部 +Z。`map.unity` 的默认参数下两者恰好分别等于世界 X 与世界 Z，这也是它容易被写反的原因。
- 临时 harness 覆盖：整屏拖满 = 正好移动一个可视宽度、半屏为一半、反向对称、零位移为零偏移、零/负屏宽被忽略、整屏拖在手机与平板上得到相同世界距离、越界由控制器夹到 `MinOffset`/`MaxOffset`。

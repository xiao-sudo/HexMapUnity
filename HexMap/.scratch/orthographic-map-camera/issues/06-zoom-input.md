# 06 — 缩放输入（捏合 / 滚轮）与文档

**What to build:** 在 `HexMap.Sample` 里加缩放输入适配器（移动端双指捏合、PC 滚轮），**锚点取双指中点 / 鼠标位置**；把 03 的拖拽适配器升级为二维并跟着当前 zoom 换算；更新实现文档。

**Blocked by:** 05 — 相机缩放、焦点与二维平移

**Status:** ready-for-agent

## 范围

### 缩放输入（`HexMap.Sample`）（已完成）

- [x] 新增 `OrthographicMapZoomInput`：
  - 移动端：`Input.touchCount >= 2` 时按**双指距离比值**写 zoom（旧 Input Manager 没有内置捏合，手算）；比值始终对**手势起始距离**取，不对上一帧取
  - PC / 编辑器：`Input.mouseScrollDelta.y`，按 `1.15 ^ 滚动量` 指数缩放
  - 手指全部抬起 / 无滚轮输入时不写任何值
- [x] **锚点在手势开始时抓一次**（双指中点或鼠标位置），整个手势固定
- [x] 锚点交给相机：`OrthographicMapCamera.TryZoomTo(targetZoom, viewportAnchor)`，推移中心的公式在相机里（与 6.3 一致）
- [x] 手势开始时 `BeginGesture()`、结束时 `EndGesture()`，让相机在手势期间不把镜头拉回焦点
- [x] 锚点归一化坐标：`[-1,1]²`，相对视口中心；屏幕水平 → 地图局部 `+X`、屏幕上下 → 地图局部平面轴
- [x] 不做惯性、不做回弹、不做双击缩放
- [x] `m_UseMouseWheel` 可关（触摸捏合不受它影响）
- [x] 纯静态换算函数 `ScreenToViewportAnchor` / `PinchRatioToZoom`，可脱离设备推理

### 拖拽适配器升级（`HexMap.Sample`）（已完成）

- [x] `OrthographicMapDragInput` 改为二维：水平写 `Center.x`，垂直写 `Center.y`
- [x] 换算改用**当前 zoom 下的可视宽度 / 可视高度**，不用基础值
- [x] 方向仍"内容跟手"；竖直分量按 `Screen.height` 与 `VisibleHeight` 换算

### 文档（已完成）

- [x] `docs/implementation/orthographic-map-camera.md` 新增"缩放与焦点"一节：zoom 定义与数值表、`zoom = 1` 是唯一"所有行可见"档、上限公式 `1 / (ratio × aspect)`、焦点语义、两条优先级规则、锚点的三个细节
- [x] 失败模式表补上"捏合时地图从手指下滑走 / 抖动 / 被弹回某个位置 / 拖拽被拽回焦点"四行
- [x] 接线一节补上 `OrthographicMapZoomInput` 的槽位
- [x] 测试一节说明两个输入适配器都没有自动化测试，但纯静态函数可脱离设备推理

## 验收

- [x] **临时 stub harness 跑通 30 项断言**（编入真实源码与真实相机控制器）：锚点换算的四角与中心、退化视口、捏合比值（放大/缩小/不动/零距离）、**五帧增量等于一次直接比值**（防累积漂移）、离中心锚点的捏合让"锚点下的世界点"位置不变、缩到最远被夹到 zoom 1 且强制居中、放大超上限被夹到 `MaxZoom`、滚轮一格乘 1.15 且反向可逆、适配器默认值与接线
- [ ] 在 `map.unity` 里手动验：双指捏合以中点为中心缩放、地图不从手指下滑走、无抖动、到端点硬停
- [ ] PC 滚轮以鼠标位置为中心缩放
- [ ] 放大后上下左右都能拖；拖到别处后再捏合，**镜头不会跳回焦点**
- [ ] 缩回最远（zoom = 1）⇒ 镜头回正中、所有行可见
- [ ] C# 编译通过

## 已知取舍

- 输入适配器**没有自动化测试**（`spec.md` 2.7 一致的代价）：`HexMap.Sample` 只被 PlayMode 测试程序集引用。验算靠临时 harness + 手动。
- 不做双指平移（两指同时平移时只处理缩放，中点漂移被"锚点抓一次"吸收）。
- 滚轮倍率 `1.15` 是常量，不做可配项。

## Comments

- 决策依据见 `../spec.md` 第 6.3 节与第 4 节的失败模式表。
- 实现完成。实现过程中自查出三处代码问题：`UpdatePinch` 里注释与代码不符且 `m_PreviousPinchDistance` 是死状态（比值本就该对起始距离取，已删除该字段）；`UpdateWheel` 算出的锚点没被使用（漏赋值 `m_AnchorViewport`）；两处 `TrySetOffset` 的调用签名在 05 里已升为 `Vector2`，拖拽适配器同步改了二维。

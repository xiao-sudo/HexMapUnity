# 01 — 正交俯视取景的纯数学

**What to build:** 在 `HexMap.Core` 里加一组**零场景依赖**的纯函数：给定 `HexLayout` 与视口宽高比，算出正交取景所需的 `orthographicSize`、地图沿移动轴的半宽、以及可移动范围；再给定一个偏移量，算出夹取后的偏移。这一票不碰 Unity 场景、不碰 MonoBehaviour。

**Blocked by:** None — can start immediately.

**Status:** in-progress

## 范围

- [x] 新增 `OrthographicMapFraming`（`Assets/Scripts/HexMap/Core/OrthographicMapFraming.cs`）：
  - 输入：`HexLayout` 快照 + `mapRadius` + `viewMargin` + `aspect`
  - 输出：`OrthographicSize`、`MapHalfWidth` / `MapHalfDepth` / `MapWidth` / `MapDepth`、`Origin`、`VisibleHeight` / `VisibleWidth`、`MinOffset` / `MaxOffset`、`IsLockedToCenter`、`ShowsEveryColumn`
- [x] 包络按 `spec.md` 2.2 派生 = **格心铺开 + 一格半尺寸**（两步都要，漏掉后一步会裁最外圈的尖）：
  - 格心（x / 平面）：`Pointy = √3·R·o / 1.5·R·o·s`；`Flat = 1.5·R·o·s / √3·R·o·s`
  - 格子半尺寸（x / 平面）：`Pointy = √3/2·o / o·s`；`Flat = o / √3/2·o·s`
  - 平面只决定深度轴落在哪个世界轴（`XZ` → Z，`XY` → Y），**不改变任何数值**
- [x] `orthographicSize = 地图半深 · viewMargin`（**不是**半深的一半：`Camera.orthographicSize` 本身是半高）
- [x] `可视半宽 = orthographicSize · aspect`；`MinOffset = −max(0, MapHalfWidth − 可视半宽)`，`MaxOffset = +max(0, …)`
- [x] 夹取：`ClampOffset`、`TrySetOffset(offset, out clamped, out error)`、`NormalizeOffset`；非法输入（NaN / Infinity / `viewMargin < 1` / `aspect <= 0` / `mapRadius <= 0`）返回 `false` + 原因
- [x] `TryCreate` / `Create`（后者抛 `ArgumentException`），与 `HexMapView.TryCreateSnapshots` 同风格

## 验收

- [x] 用 `map.unity` 现状参数（`R = 11, o = 1, s = 0.9, margin = 1.1, aspect = 16/9`）断言：
  - `Pointy`：包络 `39.8372 × 31.5`、`orthographicSize = 17.325`、`VisibleWidth = 61.6`、**`MaxOffset == 0`（锁死）**
  - `Flat`：包络 `31.7 × 35.8535`、`orthographicSize = 19.7194`、**`MaxOffset == 0`（锁死）**
  - 且断言平面对包络与取景**完全无影响**（`XZ` 与 `XY` 数值相同）
- [x] 断言**可视高度恒 ≥ 地图深**（对 `margin ≥ 1` 与**四种平面×朝向组合 × 6 个 `s` × 5 个 aspect × 3 个 margin` 的笛卡尔积都成立）——"所有行恒定可见"的机器表达。
- [x] 断言 `s = 0.5`（阈值以下）时地图重新变得可移动（`MaxOffset ≈ 2.8076`）且所有行仍可见，把 **`s < 0.566` 这个阈值**钉成文档化的行为；同时断言 `s = 0.6` 仍锁死。
- [x] 断言 `Pointy` 与 `Flat` 的包络**不是干净的对调**（格心对调、格子半尺寸不对调），防止下一个人"顺手简化"成对调。
- [x] 断言 `VisibleWidth == VisibleHeight × aspect`（aspect 只影响宽度，不影响高度），并覆盖**竖屏 9:16**：`VisibleWidth = 19.4906`（同一份包络，只换 aspect）——这是"上下容纳所有行、左右只容纳一部分"的机器表达。
- [x] **不测**渲染、相机、场景。
- [x] C# 编译通过。

## 已知取舍

- 包络**只算地图本身，不含装饰物**。外圈装饰物可能被裁掉极少量，这是接受的（见 `spec.md` 2.2）。
- 不遍历已渲染 cell 求真实包围盒：生产场景用 `DrawMeshInstanced`，那条路径**根本没有 cell 的 GameObject**，遍历方案在该路径上直接失效。包络改为从 `HexLayout` + 网格顶点公式解析推导，因此与渲染器生命周期无关。
- **`secondaryScale` 的双重应用**（`HexLayout` 乘一次、`HexCellMeshFactory` 乘一次）是既有行为，本票只如实反映，不在本票修。它导致格子缩到 `s` 倍而格心不动，即地图上出现 `1−s` 比例的缝隙。
- 这一票是 Tier 1 候选（纯逻辑），但 `HexLayout` 依赖 `UnityEngine.Vector3` / `Mathf`，能否进 dotnet harness 取决于 stub 表面，先按 Tier 2 跑。

## Comments

- 决策依据见 `../spec.md` 第 2.2 / 2.3 / 2.4 节与第 3 节。
- 实现完成，并已用一个临时 stub harness（只 stub `Vector3` / `Mathf`，编入**真实的 `HexMap.Core` 源码**）跑通 71 项独立断言。该 harness 不进仓库。
- 实现过程中修正了三处**先前 spec 的错误**：① `orthographicSize` 应为半深 × margin（写成一半会让可视高只有地图深的一半）；② 包络必须含最外圈格子顶点；③ `s = 0.9` 在 16:9 下**锁死居中**（不是可移动 ±4.53）。
- **Unity 内尚未执行**：`HexMap.Tests.EditMode` 的 NUnit 用例由用户自行运行。

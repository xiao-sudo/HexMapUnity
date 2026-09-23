# 04 — 取景的缩放变换

**What to build:** 在 `HexMap.Core` 的 `OrthographicMapFraming` 上加一个**纯函数** `WithZoom(float zoom)`：返回一个新的 framing，其 `orthographicSize` 与可视高宽按 zoom 反比缩小。**不改动 01 的既有语义**——`zoom = 1` 就是现状，01 的 71 项断言必须全部继续成立。

**Blocked by:** 01 — 正交俯视取景的纯数学

**Status:** done

## 范围

- [x] `OrthographicMapFraming` 新增：
  - `public float Zoom { get; }`、`public float ViewMargin { get; }`、`public float Aspect { get; }`、`public float BaseOrthographicSize { get; }`
  - `public const float DefaultZoom = 1f`
  - `WithZoom(float zoom)`：返回缩放后的新实例，`MapHalfWidth` / `MapHalfDepth` / `Origin` / `ViewMargin` / `Aspect` **不变**，`OrthographicSize` / `VisibleHeight` / `VisibleWidth` 按 `1 / zoom` 缩放
  - `TryWithZoom(float zoom, out framing, out error)`
  - `Create(...)` / `TryCreate(...)` 增加 `zoom` 参数（`Create` 带默认值，`TryCreate` 新增重载，旧签名保留）
  - `MinOffset` / `MaxOffset` / `MovableHalfRange` / `IsLockedToCenter` / `ShowsEveryColumn` 无需改动：它们是派生属性，自动跟着缩放后的可视尺寸走
- [x] **幂等**：zoom 是**绝对档位**而非"再乘一个倍率"，所以 `WithZoom(2).WithZoom(2) == WithZoom(2)`。实现方式是存住 `BaseOrthographicSize` 并在构造函数里用 `base / zoom` 求值；**不能在 `WithZoom` 里乘当前尺寸**（初版就是这么写的，结果 zoom 2 连点两次变成 zoom 4）
- [x] 非法 zoom（`0` / 负数 / `NaN` / `Infinity`）：`Try*` 返回失败原因，`WithZoom` 抛 `ArgumentOutOfRangeException`，`Create` 经由 `TryCreate` 抛 `ArgumentException`
- [x] **不引入** zoom 的上下限（那是相机组件的事，见 `05`）

## 验收

- [x] `WithZoom(1)` 与自身逐属性相等（`AssertFramingEquals` 逐项断言 `Zoom` / `ViewMargin` / `Aspect` / `OrthographicSize` / `VisibleHeight` / `VisibleWidth` / `MinOffset` / `MaxOffset` / `Origin`）
- [x] `WithZoom(2)` 的 `OrthographicSize` / `VisibleHeight` / `VisibleWidth` 减半；`MapHalfWidth` / `MapHalfDepth` / `MapWidth` / `MapDepth` / `Origin` / `ViewMargin` / `Aspect` 不变
- [x] `WithZoom(2).MaxOffset > WithZoom(1).MaxOffset`
- [x] **幂等**：`WithZoom(2).WithZoom(2) == WithZoom(2)`、`WithZoom(4).WithZoom(4).Zoom == 4`、`WithZoom(3)` 与 `Create(..., 3f)` 一致
- [x] `zoom 0.5` 使 visible size **翻倍**（钉住"绝对档位"而不是"可逆倍率"）
- [x] 钉住阈值：`zoom 1` 时 `VisibleHeight == MapDepth × ViewMargin`；`VisibleHeight == MapDepth` 恰好发生在 `zoom == ViewMargin`；再往里就丢行——所以"所有行可见"属于 `zoom = 1`，而**丢行的精确阈值是 margin 本身，不是 1**（`m_ViewMargin = 1.1` 时是 `1.1`）
- [x] `WithZoom(0)` / 负数 / `NaN` / `Infinity` → `TryWithZoom` 与 `TryCreate(..., zoom, ...)` 都返回 `false` + 原因；`WithZoom` 抛 `ArgumentOutOfRangeException`
- [x] 四种平面×朝向组合 × 三个 `secondaryScale` × 三个 zoom × 两种 aspect 的笛卡尔积断言：`Zoom` 记录正确、`VisibleWidth == VisibleHeight × Aspect`、`OrthographicSize == BaseOrthographicSize / zoom`
- [x] **用临时 stub harness 跑通 47 项断言**（编入真实源码），harness 不进仓库
- [ ] Unity 内尚未执行：NUnit 用例由使用者运行
- [x] C# 编译通过

## 已知取舍

- `WithZoom` 用**绝对档位**而非相对倍率。相对倍率看起来更"组合友好"，但会让"重复设置同一个 zoom"变成累积错误——这是初版真实的 bug。
- 结构体多存了 `ViewMargin` / `Aspect` / `BaseOrthographicSize` 三个字段。代价是内存，收益是"设一次 zoom 不必重述所有输入"，并且让幂等成为构造上的性质而不是调用方的纪律。
- 不把 zoom 变成"可变状态"：`OrthographicMapFraming` 仍是不可变结构体，`WithZoom` 返回新实例。

## Comments

- 决策依据见 `../spec.md` 第 6.1 节。
- 实现完成。修掉一个真 bug：初版在 `WithZoom` 里对**当前** `OrthographicSize` 相乘，导致 zoom 2 连点两次变成 zoom 4（`WithZoom(2).WithZoom(2).OrthographicSize == 4.33` 而不是 `8.66`）。改为存 `BaseOrthographicSize` 后，幂等成为构造上的性质。
- 同时修正了我自己写测试时用错的模型：初版 probe 与 NUnit 都断言"`WithZoom(2).WithZoom(0.5)` 回到原尺寸"，那是**相对倍率**的模型。测试已改写为幂等断言 + 与 `Create(..., zoom)` 一致性断言。

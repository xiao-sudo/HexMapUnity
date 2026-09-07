# Hex 坐标与布局内核实现说明

## 目的

本文说明 issue [01 — Hex 坐标与布局内核](../../.scratch/gvg-hex-map/issues/01-hex-coordinate-layout-kernel.md) 的实现边界、数学模型和验证方式，供后续实现运行时地图、编辑器预览、拾取和寻路时复用。

该内核负责：

- 以 Axial (`q, r`) 表示 Hex 坐标，并派生 Cube (`q, r, s`)。
- 提供相等比较、哈希、六邻居和 Hex 距离。
- 在 pointy top / flat top、XY / XZ 四种布局组合之间进行 Hex 中心点投影和反投影。
- 使用 cube rounding 把任意平面点归属到最近 Hex。

该内核不负责 HexCell 数据、地图范围、Raycast、地块/归属、阻挡、GVG 规则、寻路或渲染资源。它只提供坐标数学和布局变换。

实现状态：本文所列核心类型、运行时程序集和 EditMode 测试程序集已经落地；后续 issue 应以这些公开 API 和程序集边界为依赖入口。

## 代码地图

当前实现事实：核心源码位于 `Assets/Scripts/HexMap/Core/`，EditMode 测试位于 `Assets/Tests/EditMode/HexMap/Core/`，并由两个 `.asmdef` 明确隔离；Unity 版本为 `2022.3.50f1`，测试框架包已在 `Packages/manifest.json` 的项目依赖中可用。实现布局如下：

- `Assets/Scripts/HexMap/Core/HexCoord.cs`：`HexCoord` 值对象，保存 Axial `q/r`，并提供 Cube 派生、相等比较和哈希。
- `Assets/Scripts/HexMap/Core/HexCubeCoord.cs`：已实现的 Cube 值对象，负责保持 `q + r + s = 0` 并支持值比较。
- `Assets/Scripts/HexMap/Core/HexDirection.cs`：六个固定方向及其 Axial 增量。
- `Assets/Scripts/HexMap/Core/HexLayout.cs`：布局朝向、Unity 平面、中心点投影、反投影和 cube rounding。
- `Assets/Scripts/HexMap/Core/HexMap.Core.asmdef`：已实现的运行时程序集边界；不引用 GVG、Editor 或场景对象。
- `Assets/Tests/EditMode/HexMap/Core/HexCoordTests.cs`：坐标不变量、邻居、距离、比较和哈希测试。
- `Assets/Tests/EditMode/HexMap/Core/HexLayoutTests.cs`：四种布局组合及边界舍入测试。
- `Assets/Tests/EditMode/HexMap/Tests.EditMode.asmdef`：已实现的 EditMode 测试程序集，只引用核心程序集、Unity Test Runner 和 NUnit。
- `Assets/Scenes/SampleScene.unity`：当前仓库唯一已有的示例 Scene。它不是坐标数学的依赖；待后续实现可视化桥接后，才适合做手动烟测。

## 运行流或编辑器流程

核心调用应保持为纯值计算，不需要加载 Scene 或创建 GameObject。

1. 调用方创建 `HexCoord(q, r)`。Axial 是唯一存储坐标；`s` 由 `s = -q - r` 推导。
2. 拓扑调用使用同一坐标值：六邻居通过固定方向表加上 Axial 增量，距离通过两个坐标的 Cube 差值计算。
3. 需要摆放格子时，调用 `HexLayout.HexToWorld(coord)`。布局先根据朝向计算二维平面中心，再把二维分量映射到 XY 或 XZ，返回 Hex 中心点。
4. 需要从命中点反查格子时，调用 `HexLayout.WorldToHex(point)`。方法先减去布局原点并取出对应平面分量，再使用当前朝向的逆矩阵得到 fractional Axial 坐标。
5. 反投影结果转为 fractional Cube `(q, r, -q-r)`，执行 cube rounding，并返回满足 `q + r + s = 0` 的 `HexCoord`。
6. 后续的 Raycast 流程应在上层完成 `屏幕点 -> 命中世界点 -> HexLayout.WorldToHex -> HexCell`；坐标内核不接触 Camera、Physics 或 Plot。

## 实现笔记

### 坐标和值语义

`HexCoord` 应是不可变值类型，至少包含 `q` 和 `r` 两个整数。`Cube` 访问器返回 `(q, r, -q-r)`，不得单独存储一个可能失真的 `s`。

六邻居使用一张唯一的方向表。建议以以下 Axial 增量作为规范集合：

```text
(+1,  0), (+1, -1), ( 0, -1),
(-1,  0), (-1, +1), ( 0, +1)
```

方向的具体枚举顺序属于 API 行为：路径调试、遍历和未来的确定性寻路都可能依赖它，因此实现后应固定顺序并用测试锁定。pointy/flat 只改变几何投影，不改变这组六邻居。运行时接口只提供 `HexCoord.GetNeighbor(HexDirection)`，该调用不分配数组或集合；当前不提供返回六邻居集合的便捷接口。

两个 Hex 的距离为：

```text
distance(a, b) = (abs(dq) + abs(dr) + abs(ds)) / 2
```

其中 `dq = a.q - b.q`、`dr = a.r - b.r`、`ds = a.s - b.s`。六邻居距离必须为 1，同一点距离必须为 0。

相等比较只比较 `q/r`（比较 Cube 也应得到同样结果）。哈希必须由同一组值组成，使 `HexCoord` 能安全作为 `Dictionary` 或 `HashSet` 的键；不要用浮点世界坐标作为格子身份。

### 布局参数和坐标平面

`HexLayout` 至少需要：

- `Orientation`：`Pointy` 或 `Flat`。
- `Plane`：`XY` 或 `XZ`。
- `OuterRadius`：中心到顶点的距离，必须为正数且有限。
- `Origin`：布局平面原点。

建议先在抽象的二维平面 `(x, v)` 上完成公式，再由 `Plane` 把 `v` 映射为 Unity 的 `y` 或 `z`。这样 XY/XZ 不会复制一套坐标数学，也不会改变 Axial 结果。

使用 `size = OuterRadius` 时，中心点公式为：

```text
pointy:
    x = size * sqrt(3) * (q + r / 2)
    v = size * 3 / 2 * r

flat:
    x = size * 3 / 2 * q
    v = size * sqrt(3) * (r + q / 2)
```

`HexToWorld` 返回中心点，不承担高度、地形采样或物体偏移。若后续需要把 Hex 放到倾斜 Transform 下，应由上层把局部点转换到世界空间，或为布局增加明确的变换抽象；不要把场景对象引用塞入核心值数学类型。

反投影应使用对应的逆公式：

```text
pointy:
    q = (sqrt(3) / 3 * x - 1 / 3 * v) / size
    r = (2 / 3 * v) / size

flat:
    q = (2 / 3 * x) / size
    r = (-1 / 3 * x + sqrt(3) / 3 * v) / size
```

然后计算 `s = -q-r`，对三个 fractional Cube 分量分别取最近整数，并把最大舍入误差对应的分量修正为 `-other1-other2`。这一步必须集中在 `WorldToHex` 使用的单一实现中，不能由调用方各自近似。

六边形边界上的点没有唯一所属格子。为保证点击、编辑器预览和测试结果一致，最大误差相同的修正顺序必须明确并保持稳定；边界测试应覆盖精确边界以及边界两侧的微小偏移。

### 程序集和依赖方向

程序集边界已经由 `.asmdef` 落实，实际依赖方向如下：

```text
HexMap.Core  <-  HexMap.Tests.EditMode
HexMap.Core  <-  后续 Runtime Map / GVG 玩法
HexMap.Core  <-  后续 Editor 预览与验证
```

核心程序集不得反向引用 GVG 玩法、地块归属、Editor API 或 Scene 资源。若使用 `UnityEngine.Vector3` 作为几何输入输出，仍应保持核心类无 `MonoBehaviour` 生命周期和无场景查找；这样 EditMode 测试可以直接构造值并运行。

### 与后续模块的边界

后续 `HexCell` 应以 `HexCoord` 作为身份键；地图外坐标和不存在坐标由地图层判断，不应让 `HexCoord` 自身携带“是否存在”的状态。`HexLayout` 只回答几何问题，不回答某格是否可通行或属于哪个 Plot。

## 扩展点

- 新增朝向：只扩展布局投影和逆投影的配对公式，并为新朝向补齐中心往返和边界测试；不要修改 `HexCoord`、方向表或距离公式。
- 新增 Unity 平面或 3D 变换：扩展二维分量映射/外部 Transform 适配；不要为每个平面复制 Hex 数学。
- 调整格子尺寸：只修改 `OuterRadius` 实例；同一个布局实例的正向和逆向必须使用同一尺寸。
- 新增邻居相关算法：复用固定方向表和 `HexCoord.GetNeighbor(HexDirection)`，不要自行维护第二套相邻坐标表。若未来确实需要批量结果，应由调用方提供并复用 `List<HexCoord>`，由新 API 填充它，而不是每次返回新数组。
- 新增序列化或地图配置：序列化 `q/r` 整数；Cube `s` 作为校验或派生字段，不作为独立真相源。
- 新增拾取、地图查询或 GVG 规则：在核心程序集之外接入 `HexLayout`，并把“坐标存在”“可通行”“属于哪个地块”等判断留在上层。
- 新增数学回归用例：优先在 `Assets/Tests/EditMode/HexMap/Core/` 使用小型合成坐标集合，不依赖 `SampleScene.unity`、Prefab 或真实地图资源。

## 约束

- Axial `(q, r)` 是运行时主坐标；Cube 只作为派生数学模型，始终满足 `q + r + s = 0`。
- 朝向和 Unity 平面是 `HexLayout` 参数，不能改变坐标身份、邻居关系或距离。
- `HexToWorld` 返回中心点；高度、地形、渲染偏移和对象朝向由上层负责。
- `WorldToHex` 必须经过 fractional Cube 和标准 cube rounding；不能分别对 `q/r` 独立四舍五入。
- `WorldToHex(HexToWorld(c))` 对有效参数和正常坐标必须返回 `c`。浮点误差处理应集中在 rounding，不要让调用方加各自的 epsilon。
- `OuterRadius` 必须经过参数校验；零值、负值、NaN 和无穷值不能静默生成布局。
- 核心不依赖 `Assets/Scenes/SampleScene.unity`。当前也不存在 `Assets/Scenes/Start.unity`，因此不要为数学测试假设一个未存在的固定入口 Scene。
- 当前没有既有序列化格式可兼容；后续 issue 应依赖已经落地的 `HexMap.Core` 命名空间和公开 API。
- 不把 GVG、公会、地块、阻碍、开放状态或战斗规则放进坐标/布局内核。

## 验证

测试应验证外部行为，不绑定字段布局或私有辅助方法。当前 EditMode 用例覆盖以下行为：

- 对正负 `q/r` 和较大整数范围验证 `s = -q-r`，并验证 Cube 不变量。
- 验证相同 `q/r` 的值相等、不同坐标不相等；用相等坐标作为 `Dictionary`/`HashSet` 键验证哈希一致。
- 验证六个邻居互异、邻居关系可逆，并验证邻居距离为 1。
- 使用小型合成地图验证中心、轴向移动和跨方向坐标的距离结果。
- 对 pointy/flat 与 XY/XZ 的四种组合，在多个正负坐标上验证 `WorldToHex(HexToWorld(c)) == c`。
- 使用不同 `OuterRadius` 和非零 `Origin` 验证缩放、平移不会改变反投影坐标。
- 在相邻 Hex 的公共边两侧取微小偏移，验证点被分配到正确一侧；在精确边界验证既定的确定性 tie-break 行为。
- 验证布局参数非法时返回明确错误或抛出明确异常，不能产生静默的无效坐标。

这些测试应直接实例化核心值类型，不加载 Scene、不创建 GameObject、不依赖 Raycast。待后续实现 `HexCell` 和 Sample Scene 可视化后，再增加从屏幕点到地图格子的集成测试；该测试不属于本 issue 的核心验收。

项目没有自定义测试命令；验证入口以 Unity Editor 的 Test Runner > EditMode 为准。实现 `.asmdef` 后，应确认测试程序集只引用核心程序集，并在编辑器编译和 EditMode 测试通过后再让 issue 02 依赖该内核。
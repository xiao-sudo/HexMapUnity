# 01 — 队列契约与新 Shader 名，清除旧装饰物实现

**What to build:** 建立装饰物 / HexMap / 覆盖物三层的队列契约，把装饰物 Shader 改到新的名字，并清除全部旧装饰物实现，让工程回到一个没有两套装饰物路径的干净状态。

**Blocked by:** 无。

**Status:** ready-for-agent

- [ ] 新增 `DecorationQueue` 常量类，落在 `HexMap.UnityRuntime`，拥有三个数字：装饰物 2800、HexMap 3000、覆盖物 3005。它只放常量，不引用任何 Unity 类型。
- [ ] 为 HexMap 的 3000 补一条断言：该数字由 `InstancedHexCell.shader` 的 `Queue` tag 拥有，C# 侧只验证不定义。
- [ ] `StaticDecoration.shader` 改名为 `Decoration.shader`，内部 Shader 名同步改为 `HexMap/Decoration`，**内容不改**（保留 `CBUFFER_START(UnityPerMaterial)` 与全部 Pass 设置）。
- [ ] 确认改的是文件名与内部 Shader 名，不动 `.meta`；Shader 的 GUID 必须保持不变，以免破坏既有引用（`Decoration.mat` 与 `InstancedHexCell.shader` 不受影响）。
- [ ] 删除：`StaticDecorationRenderer`、`StaticDecorationPlacement`、`StaticDecorationSpritePlacement`、`StaticDecorationDemo`、装饰物 demo 菜单、`decorate.unity`、`Decoration.mat`、旧 EditMode 装饰物测试、旧 PlayMode 装饰物渲染测试。
- [ ] 不手动创建或修改任何 `.meta` 文件；删除资产后由 Unity 自行生成 `.meta` 清理。
- [ ] 全仓库不再有任何对 `HexMap/StaticDecoration` 这个 Shader 名的引用。
- [ ] 工程编译通过。

## Comments

### 拆除期为空是预期的

本票删掉旧 PlayMode 装饰物渲染测试，因此**层序的自动化验证在本票完成后、03 落地前不存在**。这是刻意的：旧断言绑在已作废的类型上，救不回来。03 会以同一手法重建。在这段窗口内，层序只能靠人工确认。

### 与 ADR 的关系

本票实现 `docs/adr/0001-decoration-overlay-render-layers.md` 的队列契约部分。三个数字是那份 ADR 的全部魔法数字，只能出现在 `DecorationQueue` 一处。

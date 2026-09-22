# 03 — 文档与守卫测试

**What to build:** 把「朝向由 `Renderer` 子节点承担」这条契约写进文档，并加上三条自动化守卫，确保它不会被下一个人「顺手优化」掉。

**Blocked by:** None — 01 已落地，02 的守卫 C 也已随票落地。

**Status:** complete

## 范围

- [x] 守卫 **A**：构建器 XY / XZ 产物子节点的 `localRotation` 分别是 `identity` 与 `Quaternion.Euler(90,0,0)`；在 `TestAssets/` 下真实生成 Prefab，teardown 删除
- [x] 守卫 **B**：`DecorationMeshFactory` 产出的网格**所有顶点 `z == 0`** —— 钉死「朝向不由网格承担」
- [x] `docs/implementation/decoration-rendering.md`：新增 4.1 渲染平面、§5 资源行、陷阱 9/10、§10 覆盖行、第 12 节导入配置
- [x] `CONTEXT.md:11` 的 Decoration 定义补一句平面只决定姿态
- [x] `docs/agents/testing.md` 的程序集表与回归流程加 `HexMap.Editor.Tests.EditMode`
- [x] 全量回归：`HexMap.UnityRuntime.Tests.EditMode` + `HexMap.Editor.Tests.EditMode`

## 验收

- 三条守卫测试在活编辑器里通过。
- 文档与代码一致；没有只存在于对话里的约定。
- 回归没有新增失败。

## Comments

- 决策依据见 `../spec.md` 第 6 节。
- 守卫 A 落在 `Assets/Tests/EditMode/HexMap/Editor/DecorationPrefabBuilderTests.cs`（`HexMap.Editor.Tests.EditMode`），守卫 B 落在既有的 `DecorationGeometryTests`（`HexMap.UnityRuntime.Tests.EditMode`）—— 网格断言归它已经负责的接缝，不另开一处。
- 守卫 A 刻意用**无 Sprite 的骨架 Prefab**：结构与旋转不依赖 Sprite，这样该 fixture 不碰网格工厂、材质缓存和 `Shader.Find`，不会因为别的接缝失败。
- 离线编译验证（用 Unity 自己的 Roslyn 与它生成的响应文件）：`HexMap.Editor` 7 个源文件 EXIT=0；`HexMap.Editor.Tests.EditMode` 两个测试文件 EXIT=0；`HexMap.UnityRuntime.Tests.EditMode` 由 Unity 在 16:32:08 自行构建成功。过程中抓到并修掉两个真错：`AssetPostprocessor.OnPreprocessTexture` 不是 virtual（不能 `override`）、测试文件漏 `using HexMap.UnityRuntime;` 且测试 asmdef 缺 `HexMap.Core` / `HexMap.UnityRuntime` 引用。
- **活编辑器全量 EditMode 回归通过：`TOTAL=162 PASSED=162 FAILED=0 SKIPPED=0`**（`HexMap.Editor.Tests.EditMode` 19 / `HexMap.UnityRuntime.Tests.EditMode` 36 / `HexMap.Gvg.Tests.EditMode` 68 / `HexMap.Runtime.Tests.EditMode` 24 / `HexMap.Tests.EditMode` 15）。结果文件 `.scratch/unity-test-harness/results.xml`。
- 第一次回归是 161/162：`BothPlanesDifferOnlyByTheRendererChildsRotation` 断言根节点同名而失败 —— `PrefabUtility.SaveAsPrefabAsset` 会把保存后的根节点按**文件名**命名（`Skeleton_XY` / `Skeleton_XZ`）。是断言写错而不是实现错，已改为只比较子节点名/位置/缩放与两平面的夹角，并把这个行为写进注释。

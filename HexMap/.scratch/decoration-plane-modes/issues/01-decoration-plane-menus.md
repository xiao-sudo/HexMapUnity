# 01 — 装饰物平面模式：构建器与菜单

**What to build:** 让「创建 Decoration Prefab」的入口能选渲染平面（XY 竖直 / XZ 平放），并且能把选中的 Sprite 直接做成 Prefab。平面的实现只是**子节点 `Renderer` 的 Transform 旋转**，网格与运行时组件一行不改。

**Blocked by:** None — can start immediately.

**Status:** in-progress

## 范围

- [ ] 新建 asmdef `HexMap.Editor`（`Assets/Scripts/HexMap/Editor/`），引用 `HexMap.Core` + `HexMap.UnityRuntime`，`includePlatforms: ["Editor"]`，`autoReferenced: true`
- [ ] `DecorationPrefabBuilder`：唯一构建器。`RotationFor(HexPlane)`（XY → `identity`，XZ → `Euler(90,0,0)`）、`BuildAssetName(sourceName, plane)`、`CreateHierarchy(plane, sprite)`、`CreateAsset(path, plane, sprite)`
- [ ] 子节点名恒为 `Renderer`，`localRotation` 由平面决定；结构 = 根挂 `DecorationView` + 子挂 `MeshFilter` / `MeshRenderer`
- [ ] 四个菜单项共用构建器：`Assets/Create/HexMap/Decoration (XY)`、`(XZ)`；`HexMap/Create Decoration Prefab (XY)`、`(XZ)`
- [ ] 校验器：选中项全部能解析成 Sprite 资产时贴图版菜单才可用；**同时接受 `Texture2D` 主资产与子 `Sprite`**（Project 窗口选中 Sprite 贴图给的是 `Texture2D`）
- [ ] 产物落在源图同目录，名字 `{sprite.name}_Decoration_{XY|XZ}.prefab`，重名走 `AssetDatabase.GenerateUniqueAssetPath`
- [ ] 多选时逐个生成；完成后 `PingObject` + 全选新建产物
- [ ] 删除 `Assets/Editor/DecorationPrefabMenu.cs`（无资产引用）

## 验收

- 四条菜单项在 Unity 里都能跑通，产出的 Prefab 结构正确、子节点旋转正确。
- `DecorationView` / `DecorationMeshFactory` / `DecorationMaterialCache` / `Decoration.shader` 零改动。
- `Decoration.prefab`、`Decoration 1.prefab`、`map.unity` 零改动。
- C# 编译通过。

## 已知取舍

- 子 Sprite 用 `sprite.name` 命名（不是贴图文件名），所以 `spriteMode = Multiple` 的多张子图各自得到不同名字；单图情况下 `sprite.name` 就等于文件名去扩展名。
- 骨架版（无 Sprite）用 `EditorUtility.SaveFilePanelInProject` 让用户选路径，与旧菜单行为一致。

## Comments

- 决策依据见 `../spec.md` 第 3 节。
- 实现完成。`HexMap.Editor` 由 Unity 编译通过（16:10:45 产出 `HexMap.Editor.dll`，含 `DecorationPrefabBuilder` 与 `DecorationPrefabMenu`）；删除旧菜单文件引发的 `CS2001` 是 Tundra 陈旧依赖图，重跑后 `ExitCode: 0` 自愈。
- 产物结构与旋转由 `DecorationPrefabBuilderTests` 六条用例覆盖并在活编辑器里通过（issue 03 的回归）。
- **仍未验证**：四个菜单项在 UI 里点一次。`DecorationPrefabMenu` 读的是 `Selection` 与保存面板，无法在测试里驱动；这是本票唯一没走完的验收行。

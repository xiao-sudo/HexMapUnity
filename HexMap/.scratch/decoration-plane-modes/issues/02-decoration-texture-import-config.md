# 02 — 装饰贴图导入配置

**What to build:** 一个配置资产声明「哪些目录里的纹理会自动导成 Sprite」，配置目录内的纹理在导入时被强制设成 `Sprite` / `Single` / `Tight` 等一组参数；另给一个「只重导不一致项」的手动入口来覆盖已经导入过的老图。

**Blocked by:** None（与 01 无耦合，只共享 asmdef 位置与 `HexPlane`）。

**Status:** in-progress

## 范围

- [ ] `DecorationImportConfig : ScriptableObject` + `[CreateAssetMenu(menuName = "Hex Map/Decoration Import Config")]`；**不落 `.asset` 文件**
- [ ] 字段 `List<DefaultAsset> m_Folders` + 全局参数组（`textureType`、`spriteImportMode`、`spriteMeshType`、`spritePixelsToUnits`、`alphaIsTransparency`、`mipmapEnabled`、`wrapMode`）
- [ ] 发现：`AssetDatabase.FindAssets("t:DecorationImportConfig")`，多于一个取第一个 + `Debug.LogWarning`，静态缓存一次
- [ ] 兜底：配置不存在或加载失败 → 常量 `Assets/HexMap/Res`，**不抛异常**（`OnPreprocessTexture` 期间 `LoadAssetAtPath` 有递归导入风险）
- [ ] 纯函数：`(assetPath, folders, settings) → 是否命中 / 该设哪些值`；`AssetPostprocessor` 只做薄壳
- [ ] `OnPreprocessTexture`：命中即**强制覆盖**，覆盖目录下所有纹理、不做扩展名白名单
- [ ] 菜单 `HexMap/Reimport Decoration Sprite Folders`：只对当前设置与目标不一致的纹理 `SaveAndReimport`
- [ ] 单测（守卫 C）：命中 / 不命中 / 多目录 / 兜底路径各一条

## 验收

- 往配置目录里放一张新图，导入后 `textureType == Sprite`、`spriteMeshType == Tight`、`alphaIsTransparency == true`、无 mipmap。
- 目录外的图完全不受影响。
- 配置资产不存在时，兜底路径仍然生效且不报错。
- C# 编译通过；单测通过。

## 已知取舍

- 配置资产要由人在 Unity 里点一次 Create 才会存在（脚本 guid 只能由 Unity 生成，见 `../spec.md` 第 4 节）。
- 参数是**全局一组**，不做「每目录一组」。

## Comments

- 决策依据见 `../spec.md` 第 4 节。
- 守卫 C 的单测需要测试程序集才能落地，所以 `HexMap.Editor.Tests.EditMode` 在本票创建（原计划在 03）。issue 03 的清单已相应去掉重复项。
- 实现完成。`HexMap.Editor` 由 Unity 编译通过（16:10:45），13 条导入规则用例在活编辑器里通过（issue 03 的回归）。
- **仍未验证**：往配置目录里真的丢一张新图，确认导入后 `textureType == Sprite`、`spriteMeshType == Tight`、`alphaIsTransparency == true`。单测覆盖的是「命中判定 → 该设哪些值」这个纯函数，`OnPreprocessTexture` 的接线本身要靠这一次人工导入确认。注意兜底路径是 `Assets/HexMap/Res`，而该目录现有 5 张 TGA 的参数与目标一致，所以重导是空操作、不会改变现有资源。

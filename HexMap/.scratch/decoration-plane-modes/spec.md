# 装饰物平面模式（XY / XZ）与装饰贴图导入配置

本文件记录「让 Decoration 的 Sprite 可以平行于 XZ 平面渲染」以及配套编辑器工具链的全部决策。决策经一轮逐题敲定，下面「契约」与「工具」两节即结论，「明确不做」一节记录被否掉的选项，避免后来者把已经讨论过的东西重新讨论一遍。

## 1. 背景与现状（侦察结论，均为实测）

| 事实 | 位置 |
| --- | --- |
| 网格恒在**局部 XY** 生成（`new Vector3(v.x - c.x, v.y - c.y, -c.z)`），UV 直通 `Sprite.uv` | `DecorationMeshFactory.cs:52-76` |
| Prefab 结构：根 `Decoration`（`DecorationView`）+ 子 `Renderer`（`MeshFilter` + `MeshRenderer`） | `Assets/HexMap/Res/Decoration.prefab` |
| Shader 为 `Cull Off` / `ZWrite Off` / `ZTest LEqual` / 透明带 | `Decoration.shader:16-19` |
| 仓库**已有** `HexMap.Core.HexPlane { XY = 0, XZ = 1 }`；`HexCellMeshFactory` 已按 plane 分支建顶点**并翻转绕序** | `HexLayout.cs:12-16`、`HexCellMeshFactory.cs:25-27,36-45` |
| 生产场景 `map.unity` 的主相机在 `(0,30,0)`、绕 X 转 90°（正俯视）→ **屏幕上方 = 世界 +Z** | `map.unity:519-533` |
| 场景里 5 个装饰实例，4 个源自 `Decoration 1.prefab`、1 个源自 `Decoration.prefab`；全部只覆写了**根节点**旋转，**没有**任何实例覆写子节点旋转 | `map.unity:125-882` |
| `Assets/Editor/DecorationPrefabMenu.cs` 位于预定义程序集 `Assembly-CSharp-Editor`，测试 asmdef 无法引用 | — |
| 全仓库**没有任何** `AssetPostprocessor`；`Res/*.tga` 的 Sprite 设置是手配在 `.meta` 里的 | — |
| `ProjectSettings/` **不在版本控制内**（`git ls-files ProjectSettings` 为空） | — |

## 2. 契约（已决定，不可偏离）

| 项 | 决定 |
| --- | --- |
| 朝向的载体 | **子节点 `Renderer` 的 Transform**，唯一真相来源 |
| 网格 | 恒在局部 XY 生成。`DecorationMeshFactory` / `DecorationView` / `DecorationMaterialCache` / shader **一行不改** |
| XY | `Quaternion.identity` |
| XZ | `Quaternion.Euler(90f, 0f, 0f)` —— 图像上方 → 世界 **+Z** |
| 法线 | `+90°X` 把正面法线由 `+Z` 翻到 `-Y`，当前由 `Cull Off` 兜住 |
| 与分层的关系 | **正交**。band 仍由 `m_Queue` / `m_SortingOrder` 决定，不新增带 |
| 运行时 API | **不提供**「本装饰属于哪个平面」的读取接口（当前无消费方）。朝向只存在于 Transform 里 |
| 平面类型 | 复用 `HexMap.Core.HexPlane`，不新造同义枚举 |
| 子节点名 | 两种平面都叫 `Renderer`。平面是旋转，不是改名；`DecorationView` 按组件而非按名字查找 |

`+90°X` 的选择依据：把 Sprite 局部 +Y 映射到世界 +Z，正是 `map.unity` 顶视相机在画面顶端的方向，所以贴片在 Scene / Game 视图里是正的（而不是倒 180°）。两个方向都能渲染（`Cull Off`），所以这是一个**视觉**决策而非可见性决策。

## 3. 工具一：菜单与构建器

- 新 asmdef **`HexMap.Editor`**，根目录 `Assets/Scripts/HexMap/Editor/`；引用 `HexMap.Core` + `HexMap.UnityRuntime`，`includePlatforms: ["Editor"]`。
- `DecorationPrefabBuilder` 是**唯一**知道装饰 Prefab 长什么样的地方，`public`（测试程序集要调用它，本仓库无 `InternalsVisibleTo` 先例）。
- 四个菜单项共用它：

| 菜单项 | 输入 | 产出 |
| --- | --- | --- |
| `Assets/Create/HexMap/Decoration (XY)` | 选中的 Sprite | `{SpriteName}_Decoration_XY.prefab` |
| `Assets/Create/HexMap/Decoration (XZ)` | 选中的 Sprite | `{SpriteName}_Decoration_XZ.prefab` |
| `HexMap/Create Decoration Prefab (XY)` | 无 | 用户选路径的空骨架 |
| `HexMap/Create Decoration Prefab (XZ)` | 无 | 用户选路径的空骨架 |

- 贴图版产物落在**源图同目录**；名字用 `sprite.name`（不是文件名），这样 `spriteMode = Multiple` 的子图各自得到不同名字而不会撞在同一张图的名字上；重名一律走 `AssetDatabase.GenerateUniqueAssetPath`，不弹框。
- 校验器：选中项**全部**能解析成 Sprite 资产时该菜单项才可用；多选时逐个生成。解析规则见下。
- **Project 窗口选中一张 Sprite 贴图时，`Selection` 给的是 `Texture2D` 主资产**（不是 `Sprite`）；只有展开后点中子 Sprite 才是 `Sprite`。两种都要接受。
- 贴图版与骨架版**共用构建器**，所以它们的结构与旋转不可能漂移。
- 删除旧文件 `Assets/Editor/DecorationPrefabMenu.cs`（无任何资产引用）。`Assets/Editor/GraphicsSortingProbe.cs` 不动。

### 3.1 带（band）维度（追加）

覆盖物与装饰物共用同一套工具，差别只有一个 `DecorationBand`：

| 带 | 队列 | 排序号 | 命名后缀 |
| --- | --- | --- | --- |
| `Decoration` | `DecorationQueue.Decoration` (2800) | `DecorationSortingOrder` (−100) | `_Decoration_` |
| `Overlay` | `DecorationQueue.Overlay` (3005) | `OverlaySortingOrder` (100) | `_Overlay_` |

- 菜单从 4 项扩到 8 项（`Assets/Create/HexMap/{Decoration,Overlay} (XY|XZ)` 与 `HexMap/Create {Decoration,Overlay} Prefab (XY|XZ)`），**平铺不分组**，已提交的 4 条路径不变。
- `DecorationBand` 放在 `HexMap.Editor`（编辑器程序集），数字引自 `DecorationQueue` —— 运行时程序集仍然零改动，且那些数字依旧只有一个主人。
- 写成枚举而不是两个散落的 int，是为了让「队列 2800 配排序号 100」这种组合**不可表达**。
- 队列与排序号经 `SerializedObject` 写进 `DecorationView` 的私有序列化字段：那是 Inspector 自己的机制，`Queue` / `SortingOrder` 的公开 API 继续只读。**必须在 `Apply()` 之前写**。
- 骨架菜单也带所属带的数字，否则美术事后填 Sprite 会得到一个站在 −100 的「覆盖物」。

**队列号不承载层级，这条又实测了一次（反方向）。** 把 `Overlay` 临时改成 2800 后，`AnOverlayIsOpaqueAndCoversTheHexMap` 与 `OverlayStaysAboveTheHexMap` 两条像素回读用例**仍然通过** —— 拿掉队列优势，覆盖物照样盖住地图。测量已还原（`git diff` 为空），结论记进 `docs/reference/unity-render-order-rules.md` 第 3 节。

据此**保留 3005（O1）**：它的职责是「该带的材质标识」；而省下那一份材质只在同一张图同时被用作装饰与覆盖时才发生，那并不是 ADR 标注的那条内存风险的杠杆（ADR 第 34 行写明出路是「共享材质 + 纹理图集」）。`DecorationQueue.Overlay` 那句声称队列决定层级的注释已按实测改写。

## 4. 工具二：装饰贴图导入配置

- `DecorationImportConfig : ScriptableObject`，`[CreateAssetMenu(menuName = "Hex Map/Decoration Import Config")]`。
- **不落 `.asset` 文件**。原因：手写 ScriptableObject YAML 需要新脚本的 guid，而 guid 只有 Unity 导入 `.cs` 之后才存在；且 `AGENTS.md` 禁止手写 `.meta`。工具靠兜底常量开箱即用，显式配置是可选升级。
- 字段：`List<DefaultAsset> m_Folders`（目录引用，不是字符串 —— 拖拽赋值、改名不断链）+ 一组**全局**参数。
- 参数取值对齐 `Res/*.tga.meta` 的现状：`textureType = Sprite`、`spriteImportMode = Single`、`spriteMeshType = Tight`、`spritePixelsToUnits`、`alphaIsTransparency = true`、`mipmapEnabled = false`、`wrapMode = Clamp`。
- 发现方式 `AssetDatabase.FindAssets("t:DecorationImportConfig")`，多于一个取第一个并 `Debug.LogWarning`，静态缓存一次。
- **加载失败或配置不存在时退回常量 `Assets/HexMap/Res`，绝不抛异常**：`OnPreprocessTexture` 期间 `AssetDatabase.LoadAssetAtPath` 有递归导入风险。
- `OnPreprocessTexture`：路径命中任一配置目录即**强制覆盖**上述参数，覆盖目录下**所有**纹理、不做扩展名白名单。目录即契约。
- 附带菜单 `HexMap/Reimport Decoration Sprite Folders`：只对**当前设置与目标不一致**的纹理调 `SaveAndReimport`，避免无谓触发图集重打。
- 「路径是否命中配置目录 → 该设哪些值」抽成**纯函数**，`AssetPostprocessor` 只做薄壳。

`Tight` 与 `alphaIsTransparency` 一旦被改错，表现是 UV 直通 / 图集采样**静默退化**（不报错），所以这两项必须由目录契约钉死，也必须有单测。

## 5. 明确不做

1. **不动任何现有资产**：`Decoration.prefab`、`Decoration 1.prefab`、`map.unity` 及其 5 个实例一律不改。本工具面向未来生产的资源。
   - 已知副作用（接受）：`map.unity` 里 4 个实例源自 `Decoration 1.prefab`，其渲染器被作者禁用、Sprite 引用悬空，这些不一致由场景覆写掩盖着，不在本次范围内。
2. **不把朝向做进网格**。把网格按平面建成两份会翻倍网格缓存、破坏「一个 Sprite 一份网格」的契约，而且它比旋转 `Renderer` 少一层间接、更容易被下一个人「顺手优化」掉 —— 所以有一条断言专门钉住网格顶点 `z == 0`。
3. **不把朝向与目录配置绑定**（不出现「XZ 装饰目录」）。同一目录里竖立的树与贴地的路完全可能共存。
4. **不在 `DecorationView` 上加 `HexPlane` 字段**。`Queue` / `SortingOrder` 都刻意只读，理由是「摆放决策不是运行时状态」；朝向是同一类东西，做成可写字段会让 `Apply()` 开始反向覆盖美术手改的 Transform。
5. **不改 `Decoration.prefab` 与 `Decoration 1.prefab` 的命名**。虽然 `Decoration 1` 这个名字不自解释，但 PrefabInstance 引用的是 guid、重命名不断引用 —— 本轮既然不动现有资产，就不顺手改名。
6. **不写 ADR**。现有 `docs/adr/0001-decoration-overlay-render-layers.md` 讲的是分层；朝向是同层内的实现细节，记进 implementation 文档即可。

## 6. 交付切分

| Issue | 内容 |
| --- | --- |
| `01-decoration-plane-menus.md` | `HexMap.Editor` asmdef + 构建器 + 四个菜单项 + 删旧菜单文件 |
| `02-decoration-texture-import-config.md` | 配置类 + `AssetPostprocessor` + 重导菜单 + 纯函数单测 |
| `03-docs-and-guards.md` | 文档更新 + 三条守卫测试 + 测试程序集 |

守卫测试（issue 03，三条全要）：

- **A** 构建器 XY / XZ 产物的子节点四元数（临时 `TestAssets/`，teardown 删除）；
- **B** `DecorationMeshFactory` 产出的网格**所有顶点 `z == 0`** —— 钉死「朝向不由网格承担」；
- **C** 导入规则纯函数（命中 / 不命中配置目录 → 该设哪些值）。

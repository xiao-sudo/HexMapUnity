# 02 — 建立 GVG 逻辑 Asset 与场景地图的绑定

**要构建的内容：**  
增加场景侧的地图绑定，使地图 Prefab 或场景地图根节点可以引用一个 `GvgMapAuthoringAsset`，编辑器能够把逻辑数据和 `HexMapView` 配置解析为同一个活动编辑上下文。绑定只存在于编辑器上下文；Player 编译产物与 AssetBundle 依赖中不得包含 `GvgMapAuthoringAsset`。

**Blocked by:** 01 — 让 HexMapView 成为场景地图配置来源。

**Status:** verified
**Verification (2026-09-14 spike):** 直接引用方案已验证通过，进入实现。

## 设计约束（2026-09-14 grilling 确认）

### 关系与数据流

- 绑定是单向的：`HexMapView`（场景或 Prefab）→ `GvgMapAuthoringAsset`；Asset 永不保存场景对象、Prefab 对象或 `HexMapView` 的反向引用。
- `HexMapView` 只作为 Asset 的只读数据提供者；Asset 不直接修改场景对象。
- 设备运行时完全不读取该 Asset 与绑定关系；运行时地图由外围逻辑提供数据生成。
- 逻辑上一对一：每个 `GvgMapAuthoringAsset` 对应一张固定的逻辑地图，不可被多个地图复用；这是开发约定，不做项目级扫描或强制，重复绑定被静默允许。
- 绑定只允许在 `HexMapView` 根节点上；子物体通过父级解析到同一个绑定。
- Prefab 绑定是默认值，场景实例允许通过显式 override 覆盖。

### 字段与依赖

- 绑定字段形态：在 `HexMapView` 上用 `#if UNITY_EDITOR` 包裹序列化字段：

  ```csharp
  #if UNITY_EDITOR
  [SerializeField] private GvgMapAuthoringAsset m_GvgMapAuthoringAsset;
  #endif
  ```

- 字段只出现在 Inspector；不提供运行时公共属性；不在 `Awake`、运行时地图生成中访问；仅由 Editor-only 代码读取。
- **程序集布局：** 把 `GvgMapAuthoringAsset` 及 Authoring 代码（Csv、ExportWriter、Utility、Validation、GvgPlotAuthoringData）移到新建的 Editor-only 程序集（建议名 `HexMap.Gvg.Authoring`，`includePlatforms = Editor`）。`HexMap.Gvg` 只保留 Plot、PlotPath 等运行时逻辑类型。
- `HexMap.UnityRuntime` 增加对该 Editor-only 程序集的 asmdef 引用；字段与引用代码全部处于 `#if UNITY_EDITOR` 下，该引用由 Unity 在 Player 构建中剥离。**必须验证该引用模式在 Player 构建中不报错、不产生依赖。**
- **接受直接引用方案必须同时满足三个条件：**
  1. Player 编译产物中 `HexMapView` 不存在该字段及运行时访问路径；
  2. Player 构建不需要加载或引用 `GvgMapAuthoringAsset` 所在的程序集；
  3. 场景、Prefab、AssetBundle 的依赖列表不包含具体的 `GvgMapAuthoringAsset` 文件。
- 如果上述验证失败（例如 Unity 不允许 runtime asmdef 引用 Editor-only 程序集），后备方案为 GUID 字符串绑定：字段保存 Asset GUID，Editor-only 代码用 `AssetDatabase` 解析，Authoring 类型仍保持在 Editor-only 程序集。

### 解析 API

- 提供 Editor-only 的绑定解析入口，供 03（SceneView 编辑）和 04（导入导出校验）消费：

  ```csharp
  public enum GvgMapBindingDiagnostic { None, ViewMissing, AssetMissing, TopologyMismatch, NotBound }

  public static class GvgMapBindingResolver
  {
      // 从任意选中物体（根节点或子物体）解析根 HexMapView
      public static bool TryResolveView(GameObject selected, out HexMapView view);

      // 从 HexMapView 解析绑定 Asset，并给出诊断
      public static bool TryResolve(HexMapView view, out GvgMapAuthoringAsset asset, out GvgMapBindingDiagnostic diagnostic);
  }
  ```

- 失败语义明确：`ViewMissing`（根节点无 HexMapView）、`AssetMissing`（字段为空或 GUID 失效）、`TopologyMismatch`（最大 HexId 不一致）、`NotBound`（尚未绑定）。
- 该 API 不修改任何对象，只做只读解析。

### 校验与诊断

- Asset 与 `HexMapView` 的 Radius/HexId 拓扑必须一致；`Orientation`、`Plane`、`OuterRadius`、`SecondaryScale`、`Origin` 是场景表现配置，不改变逻辑地图身份。
- 绑定层用「最大 HexId」做快速拓扑比对；完整 Asset 内容校验（缺失、越界、重复、覆盖）仍由既有 GVG 校验负责。
- 校验只读，自动刷新诊断，但不自动改写 Asset 或场景。
- 拓扑不一致时：禁止 SceneView GVG 编辑、绘制、导入和导出，保留只读诊断；仍允许查看/修改 Asset、运行不依赖场景的校验、导出不带场景拓扑承诺的原始数据。
- 修复必须走显式命令，支持 Undo；具体的数据重建/迁移流程归 06，02 只负责绑定与诊断。
- 解绑（置空）直接生效，依赖 Undo/Redo 恢复，不弹确认。
- 缺失 Asset 或 `HexMapView` 时：显示可操作错误，禁用依赖绑定上下文的编辑/导入/导出；不阻止 Player 构建。
- 无场景绑定时：禁止 SceneView 编辑和依赖场景拓扑的导入/导出；保留 Asset 自身独立编辑及不依赖场景的导入/导出能力，对齐 04。

### Prefab 与 Undo

以下操作全部支持 Undo/Redo：

- Prefab Mode 中设置/清空绑定；
- 场景实例上覆盖绑定；
- 撤销实例覆盖并恢复 Prefab 默认绑定；
- 将实例 override 应用回 Prefab；
- Revert 实例到 Prefab 状态。

## 验收标准

- [x] `HexMapView` 在 Inspector 中可绑定 `GvgMapAuthoringAsset`（`#if UNITY_EDITOR` 字段，`m_` 前缀）。
- [x] 绑定在场景/Prefab 序列化数据中持久化，Editor 重开后可恢复。
- [x] 选中地图根节点或子物体都能解析到同一个绑定关系。
- [x] 提供 `GvgMapBindingResolver` 解析 API，失败语义明确（03/04 可消费）。
- [x] Asset 不保存任何场景对象引用（单向）。
- [x] 绑定设置/清空/实例覆盖/Apply/Revert 均支持 Undo/Redo 并正确保存。
- [x] 缺失 Asset 或 `HexMapView` 时显示可操作诊断，禁用依赖上下文的编辑/导入/导出；不阻止 Player 构建。
- [x] 无绑定或拓扑不一致时按上述约束阻止对应操作；Asset 独立编辑与不依赖场景的导入/导出仍可用。
- [x] 拓扑不一致时提供显式修复入口，支持 Undo；具体迁移流程归 06。
- [x] 三个依赖条件全满足：Player 无字段、Player 不依赖 Authoring 程序集、场景/Prefab/AssetBundle 依赖列表不含该 Asset 文件。
- [x] 自动化回归测试 + 真实 Player/AssetBundle 构建验收通过（测试 7/7 通过；Player/AB spike 实测通过，正式发版前建议重跑一次最终构建）。

## 验证计划

1. Editor 编译：`HexMapView` 能看到并序列化 Asset 引用。
2. Player 编译：产物中不存在该字段的运行时访问路径，Authoring 程序集不进入 Player 依赖。
3. 场景序列化：Editor 重开仍恢复绑定。
4. Prefab：保存、实例覆盖、Undo/Redo 后绑定正确。
5. AssetBundle：用 `BuildPipeline` 构建场景或 Prefab，验证依赖列表不含该 `GvgMapAuthoringAsset`。
6. 删除/移动 Asset：编辑器显示可操作诊断，Player 构建仍不依赖它。
7. 测试归属：绑定与解析 API 的 EditMode 测试放 `HexMap.Gvg.Tests.EditMode`，需新增对 `HexMap.UnityRuntime` 和 `HexMap.Gvg.Authoring` 的 asmdef 引用。

## Comments

- 2026-09-14 grilling：确认 Q1–Q28 设计决策，本 ticket 已按该理解改写。
- 2026-09-14 后续决策：Authoring 代码移入 Editor-only 程序集；解析 API 采用 `GvgMapBindingResolver`；测试归属 `HexMap.Gvg.Tests.EditMode`；数据迁移流程归 06。
- 2026-09-14 人工验收：在 Editor 中完成绑定/Unbind、Undo/Redo、实例 override/Apply/Revert、删除/移动 Asset 后的诊断显示，以及菜单打开按场景 HexMapView 初始化等验收，未发现问题，ticket 关闭。

## 验证结论（2026-09-14 spike）

直接引用方案实测通过，三项依赖条件全部满足（Unity 2022.3.50f1 / StandaloneWindows64 / AssetBundle）：

1. **Player 产物无字段**：Player 构建输出 HexMap.UnityRuntime.dll 反编译/反射核验，HexMapView 字段仅为 m_Radius, m_Orientation, m_Plane, m_OuterRadius, m_SecondaryScale, m_Origin, m_CellMaterial, m_CellLayer, m_Map, m_Layout, m_Renderer，**不存在 m_GvgMapAuthoringAsset**，也不存在任何对该字段的运行时访问。
2. **Player 不依赖 Authoring 程序集**：HexMap.UnityRuntime.dll 的 AssemblyRef 只有 
etstandard, UnityEngine.CoreModule, HexMap.Core, HexMap.Runtime；HexMap.Gvg.Authoring.dll 不出现在 Player Managed 目录与 BuildReport 文件中。
3. **场景/Prefab/AssetBundle 不含 Asset**：Prefab 序列化文本含 m_GvgMapAuthoringAsset: {fileID: 11400000, guid: <asset-guid>, type: 2}（绑定可持久化、Editor 重载可恢复）；构建该 Prefab 的 AssetBundle 后，bundle manifest 的 Dependencies: []，不含该 Asset。

附带事实与教训：

- Editor-only asmdef（HexMap.Gvg.Authoring，includePlatforms=[Editor]）被 HexMap.UnityRuntime 引用时，Editor 编译与 Player 编译均不报错；Player 编译时 Unity 自动剥离引用与字段。此模式可放心使用。
- 已验证的工程改动已落地：HexMap.Gvg.Authoring.asmdef（Editor-only，含全部 Authoring 代码）、HexMap.UnityRuntime.asmdef 增加对该程序集引用、HexMapView 的 #if UNITY_EDITOR 绑定字段、HexMap.Gvg.Tests.EditMode.asmdef 增加 Authoring/UnityRuntime 引用、HexMap.Gvg.Editor.asmdef 增加 Authoring 引用。
- **检查陷阱**：在 Editor 会话内用 Assembly.LoadFrom 检查 Player 输出的同名程序集，会复用已加载的 Editor 版程序集（返回 Library/ScriptAssemblies 路径），导致误判“字段/引用仍在 Player 中”。必须用 reflection-only / 独立进程检查实际 Player 输出 DLL。

## 实现记录（2026-09-14）

### 已落地代码

- `Assets/Scripts/HexMap/Gvg/Authoring/HexMap.Gvg.Authoring.asmdef`（Editor-only）+ `GvgMapAuthoringUtility.IsCompatibleWithMap`：快速拓扑比对（最大 HexId 不越界、空 Plots 视为兼容）。
- `Assets/Scripts/HexMap/UnityRuntime/HexMapView.cs`：`#if UNITY_EDITOR` 下序列化字段 `m_GvgMapAuthoringAsset` 与 Editor-only 属性 `GvgMapAuthoringAssetEditorOnly`。
- `Assets/Scripts/HexMap/Gvg/Editor/GvgMapBindingResolver.cs`：`GvgMapBindingDiagnostic` + 只读解析 API（`TryResolveView` / `TryResolve` 重载），不修改任何对象。
- `Assets/Scripts/HexMap/Gvg/Editor/GvgMapBindingEditor.cs`：`Bind`/`Unbind`，`Undo.RecordObject` + 实例连接时 `RecordPrefabInstancePropertyModifications`。
- `Assets/Scripts/HexMap/Gvg/Editor/HexMapViewInspector.cs`：HexMapView 自定义 Inspector，绑定字段 + 诊断 HelpBox + Unbind 按钮；标准字段手动绘制避免绑定字段重复显示。
- `Assets/Tests/EditMode/HexMap/Gvg/GvgMapBindingResolverTests.cs`：7 个 EditMode 测试。
- asmdef 引用：`HexMap.UnityRuntime → HexMap.Gvg.Authoring`；`HexMap.Gvg.Editor → HexMap.Gvg.Authoring`；`HexMap.Gvg.Tests.EditMode → HexMap.Gvg.Authoring / HexMap.UnityRuntime / HexMap.Gvg.Editor`。

### 附加功能：菜单打开时按场景 HexMapView 初始化（2026-09-14）

- `Assets/Scripts/HexMap/Gvg/Editor/GvgMapAuthoringWindow.cs`：`Open()`（Tools/Hex Map/GVG Map Authoring）打开窗口后调用 `InitializeFromScene()`——优先取选中物体所属的 `HexMapView`，否则取场景中唯一的 `HexMapView`；拿到后若其已绑定 `GvgMapAuthoringAssetEditorOnly` 则自动载入该 Asset 并清空选中地块。窗口数据生成本就以 `m_MapView` 为配置来源，设好 `m_MapView` 即完成按视图配置初始化。
- 边界行为：场景无 `HexMapView` 时不初始化；存在多个且无选中时返回 null（保守，不自动猜测），由窗口提示手动指定。初始化仅在菜单打开时触发，不随场景切换或选中变化持续刷新。

### 测试结果

- Editor 编译通过（2022.3.50f1 batchmode）。
- `GvgMapBindingResolverTests` 7/7 通过：子物体解析、ViewMissing、AssetMissing、TopologyMismatch、Unbind、Bind+Unbind、Bind 的 Undo/Redo。
- 依赖三条件已由 2026-09-14 spike 实测确认（Player 无字段 / Player 不依赖 Authoring 程序集 / bundle 依赖为空），本实现沿用同一引用模式。
- `GvgMapAuthoringWindow` 改动已在运行中的 Editor 内完成重编译（Tundra build success、domain reload 成功、无 error CS）；未加自动化测试（EditorWindow 私有方法依赖场景/Selection/窗口状态，EditMode 测试成本高），建议人工验收菜单打开时的初始化行为。

### 剩余验证 / 后续归属

- 03：SceneView 编辑入口接入 `GvgMapBindingResolver`，并按诊断禁用绘制/导入/导出。
- 04：导入导出校验消费同一解析 API。
- 06：拓扑不一致的显式数据修复/迁移入口（02 只提供诊断与 Unbind）。
- ~~建议人工验收~~：已在 2026-09-14 完成人工验收（见 Comments）；正式发版前重跑一次真实 Player + AssetBundle 构建验收仍建议保留。

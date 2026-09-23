# 测试环境与流程

本仓库的 Unity 测试按「三层」策略运行，从快到慢、从纯逻辑到真实运行时：

| Tier | 适用场景 | 速度 | 工具 |
| --- | --- | --- | --- |
| 1 纯逻辑 | 测试路径不碰 Unity API（纯 C# / enum） | ~5 s | `dotnet test`（netcoreapp3.1 harness，编译真实源码 + UnityEngine.Debug stub） |
| 2 活编辑器 | 需要真实 Unity 运行时（MonoBehaviour / Shader / HexMapView / LogAssert） | ~10–60 s（一次脚本重编译 + 域重载） | TestRunnerApi（与 Rider 同一机制），编辑器保持打开 |
| 3 Batch | 编辑器关闭 / CI / 发版门禁 | 分钟级 | `Unity.exe -batchmode -runTests` |

仓库入口：`scripts/run-tests.ps1`（包装 Tier 2 / Tier 3）；底层引擎是 unity-test-harness skill（`run_live_editor_tests.ps1` + 编辑器内 `[InitializeOnLoad]` runner）。

## Tier 1 — 纯逻辑 dotnet harness

适用：`HexMap.Core`、`HexMap.Runtime`、`HexMap.Gvg` 里不依赖 UnityEngine 的代码（坐标/布局内核、运行时 HexMap、Plot/寻路逻辑）。

搭建（一次性，按 feature 放在 `.scratch/<feature>/`）：
- `*.csproj`：`netcoreapp3.1`（Unity 2022.3 程序集是 netstandard 2.1，net48 宿主不了，会报 "NUnit failed to load"）+ `<Compile Include>` 直接编入真实源码和测试文件 + NUnit 包。
- `Stubs.cs`：只 stub 被测代码碰到的 UnityEngine 表面（例如 `UnityEngine.Debug`）。**不要引用 UnityEngine.dll**。
- 同一份测试文件在 Unity 里跑的部分用 `#if UNITY_INCLUDE_TESTS` 保护；不要写引用 harness stub 的 `#else`（会成为提交后的死代码）。

运行：
```powershell
dotnet test .scratch\<feature>\<harness>.csproj --nologo -v minimal
dotnet test .scratch\<feature>\<harness>.csproj --filter "FullyQualifiedName~YourFixture"
```

> 当前仓库还没有 Tier-1 csproj；需要时按上面模板新建。注意：`GvgMapRuntimeComposerTests` 整个 fixture 含 Unity 依赖（`HexMapView`、`GameObject`、`Vector3`），不能整文件进 Tier 1，只能挑纯逻辑用例或拆文件。

## Tier 2 — 活编辑器 TestRunnerApi（日常主力）

前置（一次性）：
1. 用 Unity Hub / 命令行打开本项目，保持编辑器运行（**一个项目只能一个 Unity 实例**）。
2. 测试程序集 asmdef 需 `testAssemblies: true` + `defineConstraints: ["UNITY_INCLUDE_TESTS"]`（本仓库 5 个程序集均已配置，见下表）。
3. harness 触发脚本首次运行会自动把 `LiveEditorTestRunner.cs` + `UnityTestHarness.Editor.asmdef` 装进 `Assets/Editor/Tests`（若该目录已有别的 asmdef 则装进子目录，不接管现有程序集），幂等。

运行：
```powershell
# 全量 EditMode
powershell -File scripts\run-tests.ps1
# 按程序集
powershell -File scripts\run-tests.ps1 -Assembly HexMap.Gvg.Tests.EditMode
# 按 fixture（regex）
powershell -File scripts\run-tests.ps1 -Group '^HexMap\.Gvg\.Tests\.GvgMapRuntimeComposerTests$'
# 按用例 fullname
powershell -File scripts\run-tests.ps1 -Test HexMap.Gvg.Tests.GvgMapRuntimeComposerTests.ComposeAndFindPathBetweenSingleCellPlots
# PlayMode（wrapper 直接写 mode=PlayMode flag，harness 脚本本身没有 -Mode 开关）
powershell -File scripts\run-tests.ps1 -Mode PlayMode
```

触发机制（了解即可）：
1. 写 `.scratch/unity-test-harness/run.flag`（空 = 全部 EditMode；支持 `mode=` / `assembly=` / `group=` / `test=`，每行一条）。
2. toggle `Assets/Editor/Tests/LiveEditorTestRunner.cs` 尾部 `// trigger` 注释 → 强制重编译 + 域重载（时间戳 touch 不可靠，必须是真实内容变更）。
3. 编辑器内 `[InitializeOnLoad]` 静态构造读 flag → `TestRunnerApi` 执行 → 写 `.scratch/unity-test-harness/results.xml` + `started.txt`。
4. 轮询并打印 `TOTAL=... PASSED=... FAILED=... SKIPPED=...` 与失败用例 fullname。

已知坑（实测）：
- **编辑器里手动跑 Test Runner 会覆盖 results.xml**（TestRunnerApi 回调是全局的）。采信前先确认 XML 里含目标 fixture/fullname（例如 `GvgMapRuntimeComposerTests`、`GvgMapAuthoringTests`），否则可能是用户手动跑的另一批结果。
- 每次运行 = 一次脚本重编译 + 域重载，编辑器短暂冻结；用户正在操作时先问再触发。
- 超时排查：看 `started.txt`（是否开始）+ `%LOCALAPPDATA%\Unity\Editor\Editor.log`（编译错误/测试跟踪）。
- 结果解析：活编辑器根元素是 `<test-suite>`；失败用例 `//test-case[@result='Failed']`。

## Tier 3 — Batch（编辑器关闭 / CI）

**不要加 `-quit`**（Unity 会在跑测试前退出，已验证的坑）。

```powershell
powershell -File scripts\run-tests.ps1 -Tier 3 -TestPlatform EditMode -UnityExe '<Unity.exe 路径>'
```

或直接：
```powershell
& '<Unity.exe>' -batchmode -nographics `
  -projectPath '<repo>' -runTests -testPlatform EditMode `
  -testResults '<repo>\.scratch\unity-test-harness\batch-results.xml' `
  -logFile '<repo>\.scratch\unity-test-harness\batch.log'
```

- 结果根元素是 `<test-run>`（Tier 2 是 `<test-suite>`），都带 total/passed/failed/skipped 属性。
- `-nographics` 加载不了 shader（`Shader.Find` 返回 null）：任何失败先到活编辑器复核再下结论。
- 编辑器开着时不能跑 batch（单实例限制）。

## 推荐流程

1. 日常 red-green（纯逻辑改动，如 Composer / DTO / 寻路策略）→ Tier 1（~5 s 循环）。
2. 涉及 Unity 表现 / 组件 / 场景绑定 / 编辑窗口 → Tier 2 活编辑器，过滤到目标程序集或 fixture。
3. 每个 issue 收尾 → 相关程序集全量回归 + 相关 PlayMode：
   `powershell -File scripts\run-tests.ps1 -Assembly HexMap.Gvg.Tests.EditMode`
   `powershell -File scripts\run-tests.ps1 -Assembly HexMap.UnityRuntime.Tests.EditMode`
   `powershell -File scripts\run-tests.ps1 -Assembly HexMap.Editor.Tests.EditMode`
   `powershell -File scripts\run-tests.ps1 -Mode PlayMode`
4. 合并 / 发版 → Tier 3 batch 全量（CI 门禁）。

## 本仓库测试程序集

| 程序集 | 模式 | 内容 |
| --- | --- | --- |
| `HexMap.Tests.EditMode` | EditMode | Core：`HexCoordTests`、`HexLayoutTests`（纯逻辑，Tier 1 候选） |
| `HexMap.Runtime.Tests.EditMode` | EditMode | Runtime：`HexMapTests`、`HexPathfindingTests`（纯逻辑，Tier 1 候选） |
| `HexMap.Gvg.Tests.EditMode` | EditMode | GVG：Authoring / BindingResolver / ExcelRoundTrip / RuntimeComposer / PlotAndBattlefieldRules（当前 62 条） |
| `HexMap.UnityRuntime.Tests.EditMode` | EditMode | `HexMapViewTests`、`OrthographicMapCameraTests` 等 Unity 组件（Tier 2） |
| `HexMap.Editor.Tests.EditMode` | EditMode | 编辑器工具：`DecorationPrefabBuilder` 的平面旋转与结构、装饰贴图导入规则（Tier 2；导入规则是纯字符串函数，是 Tier 1 候选） |
| `HexMap.UnityRuntime.Tests.PlayMode` | PlayMode | `HexMapPickerTests`、`HexMapScreenPointAdapterTests`（当前 11 条） |

## 相关路径

- harness skill：`C:\Users\xiaogang\Desktop\unity-test-harness\`（SKILL.md + `scripts/run_live_editor_tests.ps1` + `references/`）
- 结果文件：`.scratch/unity-test-harness/results.xml`（Tier 2）、`batch-results.xml`（Tier 3）
- 编辑器日志：`%LOCALAPPDATA%\Unity\Editor\Editor.log`
- 卸载 harness：删除 `Assets/Editor/Tests/LiveEditorTestRunner.cs` + `UnityTestHarness.Editor.asmdef`（或整个子目录）
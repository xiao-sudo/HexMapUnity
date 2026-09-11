# 05 鈥?GVG 鍦板潡妯″瀷涓庢垬鍦虹姸鎬佽鍒?
**What to build:** 灏嗕竴涓垨澶氫釜 HexCell 缁勭粐涓?GVG 鎿嶄綔瀵硅薄 Plot锛屽苟璁╃被鍨嬨€佺姸鎬併€佸綊灞炲拰闃绘尅缁熶竴椹卞姩鎴樺満瑙勫垯銆?
**Blocked by:** 02 鈥?杩愯鏃?Hex 鍦板浘涓庢牸瀛愭嬀鍙?

**Status:** ready-for-human

- [ ] 涓€涓?Plot 鍙互鍖呭惈涓€涓垨澶氫釜 HexCell锛涙瘡涓?HexCell 鏈€澶氬睘浜庝竴涓?Plot锛屽苟鍙粠浠绘剰 Cell 鍙嶆煡 Plot銆?- [x] 鍒犻櫎浠ｈ〃鏍煎拰鍙仠椹绘牸閰嶇疆锛屽鏍?Plot 鐨勫叏閮?Cell 閮芥槸娼滃湪杩涘叆鐩爣銆?- [x] 鏀寔鍥哄畾鏁板€肩殑 PlotType銆両nitial/TimedOpen 鐢熸垚绫诲瀷鍜?NotOpen/Open 鐘舵€侊紱Battle 鐢变笂灞備笟鍔＄淮鎶ゃ€?- [ ] 褰掑睘銆佸紑鏀剧姸鎬佸拰闃荤鑳藉缁熶竴鍐冲畾鍦板潡鐨勫彲瑙併€佸彲閫変腑銆佸彲閫氳鍜屽彲杩涘叆缁撴灉锛涘湴鍥惧鍖哄煙榛樿涓嶅彲閫氳銆?- [ ] 閫氳繃灏忓瀷鍚堟垚鍦板浘娴嬭瘯鍗曟牸鍦板潡銆佸鏍煎湴鍧楀拰鐘舵€佸彉鍖栬涓恒€?


## Refined design contract

> 鏈妭鏄粡杩囪璁″鏌ョ‘璁ゅ悗鐨勫疄鐜板绾︼紱瀹冭鐩栭《閮ㄦ棫楠屾敹椤逛腑鈥滀唬琛ㄦ牸鍜屽彲鍋滈┗鏍煎垎鍒厤缃€濈殑琛ㄨ堪銆傞鐗堝垹闄ゅ浐瀹?`StandableCells` 閰嶇疆锛屾敼涓哄鏍?Plot 鐨勫叏閮?Cell 閮芥槸娼滃湪杩涘叆鐩爣銆?
### Scope

鏈?issue 浜や粯锛?
- `Plot`銆乣PlotRegistry` 鍜?Cell-to-Plot 鍙嶆煡妯″瀷銆?- Plot 绫诲瀷銆佺敓鍛藉懆鏈熺姸鎬併€佸綊灞炪€佸浐瀹氬綊灞炲拰 Plot 绾ч樆鎸°€?- `PlotPathService`锛氬彧鎺ュ彈 Plot 浣滀负瀵昏矾璧风偣鍜岀粓鐐广€?- 閫氱敤 `HexPathfinder` 鐨勫璧风偣銆佸鐩爣鎵╁睍銆?- GVG Plot 璺緞绛栫暐鍜屽皬鍨嬪悎鎴愬湴鍥?EditMode 娴嬭瘯銆?
鏈?issue 涓嶄氦浠樺崰棰嗐€佹敾鍑汇€侀┗瀹堛€佹垬鏂楅槦鍒椼€佹垬鏂楃粨绠椼€佺姸鎬佽浆鎹€佺紪杈戝櫒銆乁I銆佸皬鍦板浘銆丆ell 璧风偣鍏ュ彛銆佹寮忓満鏅〃鐜版垨璺緞绉诲姩閲嶇畻銆?
### Plot model

`Plot` 鏄?GVG 瑙勫垯鐨勮仛鍚堝璞°€備竴涓?Plot 鍖呭惈涓€涓垨澶氫釜 `HexCell`锛涙瘡涓?`HexCell` 鏈€澶氬睘浜庝竴涓?Plot銆?
鎵€鏈?GVG 瑙勫垯灞炴€у綊灞?Plot锛?
```text
PlotId
Cells
PlotType
PlotGenerationType  // Initial 鎴?TimedOpen
PlotState           // NotOpen 鎴?Open
OwnerFaction
OwnershipMode       // Capturable 鎴?Fixed
BlockingState       // Passable 鎴?Blocked
```

`HexCell` 鍙繚鐣欏熀纭€鍦板浘韬唤锛圚exId 鍜屽潗鏍囷級锛屼笉鐩存帴寮曠敤 GVG `Plot`锛屼篃涓嶆寔鏈夐樀钀ャ€佺被鍨嬨€佺姸鎬佹垨闃绘尅銆傜敱 `PlotRegistry` 鎻愪緵 `PlotId -> Plot` 鍜?`CellId/HexCell -> Plot` 鏌ヨ銆?
鍒犻櫎浠ｈ〃鏍煎拰鍥哄畾 StandableCells 姒傚康锛氬鏍?Plot 鐨勬墍鏈?Cell 閮芥槸娼滃湪杩涘叆鐩爣锛岃矾寰勫埌杈句换鎰忓悎娉?Cell 鍗宠涓哄埌杈?Plot锛涘崟鏍?Plot 鐨勫敮涓€ Cell 鍚屾椂鏄繘鍏ョ洰鏍囥€?
鏀寔鐨勭被鍨嬪拰鍥哄畾鏁板€间负 Camp=1銆丯ormal=2銆丟rass=3銆丼mallCity=4銆丅igCity=5銆丆apital=6銆丱bstacle=7銆?
鐢熷懡鍛ㄦ湡鐘舵€佷笌褰掑睘鍒嗗紑锛?
```text
PlotState = NotOpen | Open
OwnerFaction = 涓€涓樀钀ュ€硷紱Neutral 涔熸槸鍚堟硶闃佃惀
```

Plot 鍙淮鎶?NotOpen 鍜?Open 涓ょ鐘舵€併€侽pen() 鎵ц NotOpen -> Open锛孋lose() 鎵ц Open -> NotOpen锛岄噸澶嶈皟鐢ㄥ箓绛夛紱寮€鏀惧拰鍏抽棴鐨勬椂鏈虹敱澶栧眰涓氬姟璐熻矗銆傚璺彧鍏佽 Open Plot锛涙垬鏂楃姸鎬佸睘浜庝笂灞備笟鍔★紝涓嶅湪 Plot 涓淮鎶ゃ€?
PlotGenerationType.Initial 鍒濆鎶曞奖涓?Open锛孭lotGenerationType.TimedOpen 鍒濆鎶曞奖涓?NotOpen锛汵otOpen Plot 涓嶅彲閫氳銆佷笉鍙綔涓哄璺洰鏍囦笖涓嶅彲鍗犻锛屽叿浣撳崰棰嗗垽鏂敱澶栧眰涓氬姟璐熻矗銆侽wnershipMode.Capturable 琛ㄧず褰掑睘鍙敱鍚庣画鍗犻涓氬姟鏀瑰彉锛汷wnershipMode.Fixed 琛ㄧず褰掑睘涓嶅彲鐢卞崰棰嗕笟鍔℃敼鍙樸€?
绗竴鐗堝彧鏀寔 Plot 绾ч樆鎸★細Plot 鍐呮墍鏈?Cell 鍏变韩 BlockingState锛屼笉鏀寔鍗曚釜 Cell 鐨勯樆鎸¤鐩栥€侾lotType.Obstacle 蹇呴』楠岃瘉涓?BlockingState.Blocked锛屼笖涓嶅厑璁镐娇鐢?PlotGenerationType.TimedOpen锛涘璺瓥鐣ュ彧璇诲彇闃绘尅鐘舵€侊紝涓嶇‖缂栫爜绫诲瀷鍒嗘敮銆?
### Plot-to-Plot pathfinding

Plot 灞傞鐗堝叆鍙ｏ細

```text
FindPath(startPlotId, targetPlotId, movingFaction, result)
```

`PlotPathService` 灏嗚捣鐐?Plot 鐨勫叏閮?Cell 浣滀负璧风偣闆嗗悎锛屽皢鐩爣 Plot 鐨勫叏閮?Cell 浣滀负鐩爣闆嗗悎锛屽啀璋冪敤閫氱敤 `HexPathfinder`銆傞鐗堜笉鏀寔 `startCell -> targetPlot`銆?
閫氱敤璇锋眰浠?`Start + Targets + Policy` 鎵╁睍涓?`Starts + Targets + Policy`锛屽苟淇濈暀鍗曡捣鐐瑰吋瀹规瀯閫狅紱`ReusablePathRequest` 鍚屾鏀寔鍙鐢ㄧ殑璧风偣闆嗗悎鍜岀洰鏍囬泦鍚堛€?
涓€娆℃悳绱㈠皢鎵€鏈夎捣鐐逛綔涓?BFS 鏍硅妭鐐癸紝璺濈鍧囦负 0锛涜捣鐐逛笉璋冪敤 `CanPass`銆傜洰鏍囬泦鍚堢敱 `CanEnter` 杩囨护锛屼换鎰忚捣鐐瑰埌浠绘剰鍚堟硶鐩爣 Cell 鍙揪鍗虫垚鍔熴€傝捣鐐逛笌鐩爣闆嗗悎鐩镐氦鏃惰繑鍥為浂姝ヨ矾寰勶紝涓嶈皟鐢?`CanPass` 鎴?`CanEnter`銆?
`PathResult` 淇濇寔绾?Hex 璇箟锛歚Cells[0]` 鏄疄闄呰捣鐐癸紝`ReachedTarget` 鏄疄闄呯粓鐐癸紝涓嶅鍔?PlotId銆傝矾寰勫厛鎸夋鏁伴€夋嫨鐩爣锛屽啀鎸夌洰鏍?`q/r` 閫夋嫨锛涘悓涓€璧风偣鍒板悓涓€鐩爣鐨勭瓑闀胯矾寰勭户缁娇鐢ㄥ浐瀹氬叚閭诲眳椤哄簭銆傚璧风偣瀹屽叏绛夐暱鏃讹紝涓嶆妸璧风偣鎺掑簭鎴栬捣鐐瑰潗鏍囦綔涓轰笟鍔″绾︼紝娴嬭瘯涓嶅緱渚濊禆鍏蜂綋绛夐暱璺緞褰㈢姸銆?
`HexPathfinder` 鍙悊瑙?HexCell銆佽捣鐐归泦鍚堛€佺洰鏍囬泦鍚堝拰 `IHexPathPolicy`锛屼笉寮曠敤 Plot銆侀樀钀ャ€佸叕浼氥€侀樆鎸℃垨鎴樻枟銆侾lot 瑙勫垯鐢?Plot 灞傜瓥鐣ユ崟鑾?`movingFaction` 鍚庡疄鐜般€?
### PlotPathPolicy

```text
CanPass(cell)
    PlotState 涓?Open
    BlockingState == Passable
    OwnerFaction == movingFaction
```

```text
CanEnter(cell)
    PlotState 涓?Open
    BlockingState == Passable
    鑻?OwnershipMode == Fixed锛屽垯 OwnerFaction == movingFaction
    鍚﹀垯鍏佽浣滀负璺緞缁堢偣
```

| Plot 鎯呭喌 | `CanPass` | `CanEnter` |
|---|---:|---:|
| 宸辨柟鏅€?Plot | 鏄?| 鏄?|
| 鏁屾柟鏅€?Plot | 鍚?| 鏄?|
| 涓珛鏅€?Plot | 鍚?| 鏄?|
| 宸辨柟 Fixed Plot | 鏄?| 鏄?|
| 鏁屾柟 Fixed Plot | 鍚?| 鍚?|
| 闃绘尅 Plot | 鍚?| 鍚?|
| NotOpen Plot | 鍚?| 鍚?|
| 鎴樻枟鐘舵€?| 鐢变笂灞傛垬鏂椾笟鍔￠檮鍔犲鐞?| 鐢变笂灞傛垬鏂椾笟鍔￠檮鍔犲鐞?|

`CanEnter` 鍙喅瀹氳矾寰勮兘鍚︾敓鎴愬埌鐩爣 Cell锛屼笉鍐冲畾鍒拌揪鍚庣殑鍗犻銆佹敾鍑汇€佹垬鏂楅槦鍒楁垨椹诲畧鍔ㄤ綔銆?
### Plot validation

杩涘叆閫氱敤瀵昏矾鍓嶏紝Plot 灞傛垨楠岃瘉灞傚繀椤绘嫆缁濓細

- 涓嶅瓨鍦ㄧ殑 PlotId銆佺┖ Plot 鎴栧湴鍥惧 Cell銆?- 涓€涓?Plot 鍐呴噸澶?Cell锛屾垨涓€涓?Cell 澶氶噸褰掑睘銆?- 涓嶅睘浜?Plot 鐨?Cell銆?- 灞曞紑鍚庣殑璧风偣鎴栫洰鏍囬泦鍚堜负绌恒€?- 鏈弧瓒?`Obstacle -> Blocked` 绾︽潫鐨勯厤缃€?
### Tests

浣跨敤灏忓瀷鍚堟垚鍦板浘鐨勭函 C# EditMode 娴嬭瘯瑕嗙洊锛?
- 鍗曟牸/澶氭牸 Plot銆丆ell-to-Plot 鍙嶆煡鍜岄噸澶嶅綊灞為獙璇併€?- 澶氳捣鐐?澶氱洰鏍囨渶鐭矾寰勩€侀浂姝ョ浉浜ゃ€佹棤鏁堣緭鍏ャ€佷笉鍙揪鐩爣鍜岀粨鏋滃閲忎笉瓒炽€?- 璧风偣涓嶈皟鐢?`CanPass`锛岀洰鏍囧彧璋冪敤 `CanEnter`銆?- 澶氭牸 Plot 浠绘剰 Cell 鍙綔涓鸿捣鐐规垨缁堢偣銆?- 宸辨柟 Plot 鍙€氳锛涙晫鏂?涓珛 Plot 涓嶅彲浣滀负涓棿閫氳矾浣嗗紑鏀鹃潪 Fixed 鏃跺彲浣滀负缁堢偣銆?- 鏁屾柟 Fixed Plot 涓嶅彲浣滀负缁堢偣锛屽繁鏂?Fixed Plot 鍙€氳骞跺彲浣滀负缁堢偣銆?- Plot 绾ч樆鎸°€丱bstacle 鍜?NotOpen Plot 鍧囦笉鑳界敓鎴愯矾寰勩€?- Plot 涓嶇淮鎶?Battle 鐘舵€侊紱鎴樻枟鏈熼棿鐨勯澶栬鍒欑敱涓婂眰涓氬姟鎻愪緵銆傝捣鐐?Plot 涓庣洰鏍?Plot 鐩稿悓浠嶈繑鍥為浂姝ヨ矾寰勩€?- Plot 閰嶇疆閿欒锛堝寘鎷?Obstacle + TimedOpen锛夊湪杩涘叆閫氱敤 HexPathfinder 鍓嶈鎷掔粷銆?
## Amendment from issue 10 authoring design

Issue 10 鐨勫湴鍥剧紪杈戜笌瀵煎嚭鏂规瀵规湰 issue 鐨勮繍琛屾椂妯″瀷鍋氬嚭浠ヤ笅淇銆傚悗缁疄鐜板簲浠ユ湰 amendment 涓哄噯锛?
- 鍒犻櫎 `RepresentativeCell` 姒傚康銆傚鏍?Plot 鐨勬墍鏈?Hex 閮戒唬琛ㄨ Plot 鐨勪竴閮ㄥ垎锛涜矾寰勭洰鏍囦粛鐒朵娇鐢?Plot 鍐呭叏閮?Cell銆?- 濡傛灉琛ㄧ幇灞傞渶瑕?Plot 閿氱偣锛屽簲浠?Plot 鍐呮墍鏈?Hex 鐨勪笘鐣屼腑蹇冩淳鐢燂紝渚嬪鍑犱綍骞冲潎鐐规垨鍖呭洿涓績锛岃€屼笉鏄厤缃唬琛ㄦ牸銆?- `Plot` 鏋勯€犲嚱鏁颁笉鍐嶆帴鏀?representative cell锛屾敼涓猴細

```text
Plot(
    int plotId,
    IReadOnlyList<HexCell> cells,
    PlotType plotType,
    PlotGenerationType generationType,
    PlotState plotState,
    FactionId ownerFaction,
    OwnershipMode ownershipMode,
    BlockingState blockingState)
```

- 杩愯鏃?`Plot` 鍏佽璐熸暟 `PlotId`锛屽敮涓€鎬т粛鐢?`PlotRegistry` 淇濊瘉銆?- Authoring 灞傞噰鐢ㄧ紪鍙风害瀹氾細鍗曟牸 Plot 鐨?`PlotId` 绛変簬鍞竴 Cell 鐨?`HexId`锛涘鏍?Plot 浣跨敤璐熸暟 `PlotId`锛屼粠 `-1` 寮€濮嬮€掑噺銆傝缂栧彿绾﹀畾鐢?authoring/export validator 寮哄埗锛屼笉瑕佹眰閫氱敤杩愯鏃?`Plot` 鏋勯€犲嚱鏁扮悊瑙?authoring 瑙勫垯銆?### Agent implementation update - 2026-09-11

Implemented the fixed PlotType values, PlotGenerationType initial-state projection, NotOpen/Open lifecycle with idempotent Open()/Close(), TimedOpen path restrictions, Obstacle + TimedOpen validation, and updated EditMode coverage.

### Design amendment from Excel authoring decisions - 2026-09-11

This amendment supersedes the earlier PlotGenerationType and authoring PlotId rules in this issue. Runtime Plot state remains NotOpen/Open, but time scheduling is an external concern and is not implemented by this issue.

#### Runtime boundary

- Remove PlotGenerationType from the runtime Plot model and constructor.
- Plot does not own opening time or schedule transitions.
- PlotScheduleService is an outer feature. It may later consume exported Start/End data and call Plot.Open() / Plot.Close().
- This issue does not implement time-driven runtime scheduling, runtime CSV loading, or changes to pathfinding based on the schedule.
- PlotState remains NotOpen/Open; the owner of state transitions is outside Plot.

#### Authoring time layers

For single-cell plots, authoring stores a per-Hex schedule:

    SingleHexSchedule
        HexId
        PlotType
        Layers[]

    Layer
        PlotId
        Start
        End

Start and End are seconds from the GVG gameplay start. The interval is left-closed and right-open: [Start, End). End = -1 means open forever.

- Start >= 0.
- The first layer may start after zero; [0, FirstStart) then has no active Plot.
- After the first layer, layers must be sorted, contiguous, and non-overlapping: Next.Start == Previous.End.
- Start == End is invalid.
- End = -1 is allowed only on the final layer.
- Every map Hex must have at least one layer.
- All layers belonging to one Hex use the same PlotType in this version.

Multi-cell Plots are separate authoring records and are limited to Start = 0, End = -1. A multi-cell Plot cannot overlap a single-Hex time layer in this version.

#### Authoring PlotId rules

- A single-cell Plot with one or more time layers uses the HexId for its first layer, regardless of whether that layer starts at zero.
- If a Hex has more than one layer, later layers use a global ID sequence beginning at the next whole hundred strictly greater than MaxHexId:

    TimedSinglePlotIdBase = ((MaxHexId / 100) + 1) * 100

- For the current map, MaxHexId = 397, so the sequence begins at 400.
- Existing IDs are retained whenever they still match the layer identity. New layers use the next unused ID; deleted IDs are not reused.
- Multi-cell Plot IDs use the PlotType range formula:

    PlotId = 10000 + 1000 * (int)PlotType + sequence

With the existing enum values, the ranges are Camp 11000+, Normal 12000+, Grass 13000+, SmallCity 14000+, BigCity 15000+, Capital 16000+, and Obstacle 17000+.

- An imported PlotId that is invalid for its PlotType or conflicts with another Plot is reassigned and reported.
- The first version does not support map expansion; Radius changes and new Hex generation are rejected.

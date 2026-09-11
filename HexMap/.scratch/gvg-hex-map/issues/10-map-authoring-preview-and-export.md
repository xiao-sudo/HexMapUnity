# 10 鈥?鍦板浘閰嶇疆缂栬緫銆侀瑙堜笌瀵煎嚭

**What to build:** 璁捐甯堝彲浠ュ湪 Unity Editor 涓垱寤哄拰璋冩暣 GVG 鍦板浘閰嶇疆锛屽苟瀵煎嚭杩愯鏃跺彲鐩存帴鍔犺浇鐨勫湴鍥炬暟鎹€?
**Blocked by:** 02 鈥?杩愯鏃?Hex 鍦板浘涓庢牸瀛愭嬀鍙? 05 鈥?GVG 鍦板潡妯″瀷涓庢垬鍦虹姸鎬佽鍒?

**Status:** ready-for-human

- [x] 缂栬緫鍣ㄥ彲浠ュ垱寤哄湴鍥捐寖鍥村苟灏?Hex 鍒嗛厤缁?Plot锛岃€屼笉瑕佹眰鐩存帴缁存姢绛栧垝鍘熷鍧愭爣琛ㄣ€?- [x] 缂栬緫鍣ㄥ彲浠ラ厤缃?PlotType 鍜?GenerationType锛屽苟棰勮鐢辫鍒欐淳鐢熺殑鍒濆鐘舵€併€佸彲閫氳鎬у拰鍙崰棰嗘€с€?- [x] 棰勮鏄剧ず鍧愭爣銆佸湴鍧?ID銆佺被鍨嬨€侀樆纰嶃€佸ぇ钀ャ€佸煄姹犲拰褰掑睘鑹诧紝骞朵笌杩愯鏃舵暟鎹竴鑷淬€?- [x] 瀵煎嚭缁撴灉鍙互琚繍琛屾椂鍦板浘鐢熸垚鍜?GVG 瑙勫垯鍔犺浇銆?

## Refined design contract

鏈妭璁板綍缁忔柟妗堟嫹闂悗纭鐨勬墽琛屽绾︺€傚畠瑕嗙洊椤堕儴鏃ч獙鏀堕」涓叧浜庝唬琛ㄦ牸銆佸彲鍋滈┗鏍笺€佸綊灞炶壊銆佺姸鎬佸拰闃荤瀛楁鐨勮〃杩帮細绗竴鐗堝湴鍥?Authoring 璐熻矗鍦板浘鎷撴墤銆丳lotType 鍜?GenerationType锛岃繍琛屾椂鐘舵€併€侀樀钀ュ綊灞炲拰鎴樺満鍒濆鍖栫敱杩愯鏃堕€昏緫璐熻矗銆?
### Scope

鏈?issue 浜や粯锛?
- `GvgMapAuthoringAsset` 鍜屽彲搴忓垪鍖?authoring 鏁版嵁銆?- Unity EditorWindow 鍏ュ彛鍜?SceneView 缂栬緫/棰勮銆?- CSV 瀵煎嚭鍜屽鍑虹洰褰?`README.md` 鐢熸垚銆?- 瀵煎嚭鍓嶇‖鏍￠獙銆?- 绾?C# / EditMode 娴嬭瘯瑕嗙洊 authoring 鏁版嵁瑙勫垯銆丆SV 琛岀敓鎴愬拰蹇呰鐨勮繍琛屾椂妯″瀷璋冩暣銆?- 瀵?issue 05 杩藉姞 amendment锛岃鏄?`RepresentativeCell` 鍒犻櫎鍜?PlotId 瑙勫垯鍙樺寲銆?
鏈?issue 涓嶄氦浠橈細

- CSV 瀵煎叆銆?- 杩愯鏃?CSV parser/loader銆?- 瀹屾暣杩為€氭€с€侀噸瑕佸湴鍧楀彲杈炬€с€侀樆纰嶅垏鏂湴鍥惧拰瀵昏矾璋冭瘯锛涜繖浜涘睘浜?issue 11銆?- Plot 涓婄殑妯″瀷銆丳refab銆丄ddressables key 鎴栨寮忔垬鍦鸿〃鐜拌祫婧愩€?- 杩愯鏃跺姩鎬侀樀钀ュ垎閰嶃€佸崰棰嗐€佺姸鎬佹祦杞拰鎴樻枟鍒濆鍖栭€昏緫銆?
### Enum and lifecycle contract

PlotType 浣跨敤鍥哄畾鏁板瓧锛?
1 = Camp
2 = Normal
3 = Grass
4 = SmallCity
5 = BigCity
6 = Capital
7 = Obstacle

PlotGenerationType 浣跨敤鍥哄畾鏁板瓧锛?
0 = Initial
1 = TimedOpen

PlotState 鍙湁 NotOpen=0 鍜?Open=1銆侷nitial 鍒濆鎶曞奖涓?Open锛汿imedOpen 鍒濆鎶曞奖涓?NotOpen銆侼otOpen 涓嶅彲閫氳銆佷笉鍙綔涓哄璺洰鏍囦笖涓嶅彲鍗犻锛涘紑鏀炬椂鏈虹敱澶栧眰涓氬姟璋冪敤 Open()锛屽叧闂椂鏈虹敱澶栧眰涓氬姟璋冪敤 Close()銆侭attle 鍜?NotGenerated 涓嶅睘浜?PlotState銆?
### Runtime contract changes required by this issue

- 鍒犻櫎 RepresentativeCell 姒傚康銆傚鏍?Plot 鐨勬墍鏈?Hex 閮戒唬琛ㄨ Plot 鐨勪竴閮ㄥ垎銆?- 澶氭牸 Plot 鍚庣画濡傛灉闇€瑕?UI 鏍囩銆侀暅澶磋仛鐒︽垨灏忓湴鍥炬枃瀛楅敋鐐癸紝搴斾粠 Plot 鍐呮墍鏈?Hex 鐨勪笘鐣屼腑蹇冩淳鐢燂紝渚嬪鍑犱綍骞冲潎鐐规垨鍖呭洿涓績锛岃€屼笉鏄厤缃竴涓唬琛ㄦ牸銆?- `Plot` 鏋勯€犲嚱鏁拌皟鏁翠负锛?
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

- 杩愯鏃?`Plot` 鍏佽璐熸暟 `PlotId`銆傚敮涓€鎬т粛鐢?`PlotRegistry` 淇濊瘉銆?- Authoring 灞傚己鍒剁紪鍙疯鍒欙細
  - 鍗曟牸 Plot 鐨?`PlotId` 蹇呴』绛変簬瀹冨敮涓€ Cell 鐨?`HexId`銆?  - 澶氭牸 Plot 鐨?`PlotId` 蹇呴』灏忎簬 0銆?  - 澶氭牸 Plot 鑷姩缂栧彿浠?`-1` 寮€濮嬮€掑噺銆?
### Authoring assets

Source of truth 鏄?Unity `ScriptableObject`锛屼笉鏄?CSV銆?
寤鸿鏂板绋嬪簭闆嗭細

- `Assets/Scripts/HexMap/Gvg/Authoring`锛氳繍琛屾椂鍙鐨?authoring 鏁版嵁绫诲瀷鍜岀函鏁版嵁杞崲銆?- `Assets/Scripts/HexMap/Gvg/Editor`锛欵ditorWindow銆丼ceneView 浜や簰銆乣AssetDatabase`銆乣Handles` 鍜?CSV 鏂囦欢鍐欏嚭銆?
`GvgMapAuthoringAsset` 绗竴鐗堝瓧娈碉細

```text
MapId       // string锛岄粯璁ょ瓑浜?asset name锛屽厑璁告墜鍔ㄦ敼
Radius
Orientation
Plane
OuterRadius
Plots[]
```

`GvgPlotAuthoringData` 绗竴鐗堝瓧娈碉細

```text
PlotId
HexIds      // List<int>
PlotType
GenerationType
```

涓嶅湪 authoring asset 涓繚瀛?PlotState銆丱wnerFaction銆丱wnershipMode銆丅lockingState銆丷epresentativeHexId銆佸彲鍋滈┗鏍笺€佹ā鍨?key 鎴?Prefab 寮曠敤銆侴enerationType 鏄?authoring 閰嶇疆瀛楁銆?
### Default initialization rules

鍒涘缓鎴栭噸寤哄湴鍥捐寖鍥村悗锛岃嚜鍔ㄤ负姣忎釜 Hex 鐢熸垚涓€涓粯璁ゅ崟鏍?Plot锛?
```text
PlotId = HexId
HexIds = [HexId]
PlotType = Normal = 2
GenerationType = Initial = 0
```

Editor 棰勮鍜?authoring-to-runtime 鎶曞奖浣跨敤浠ヤ笅榛樿杩愯鏃惰鍒欙細

```text
GenerationType.Initial = 0 -> PlotState.Open
GenerationType.TimedOpen = 1 -> PlotState.NotOpen
OwnerFaction = Neutral
OwnershipMode = Capturable
BlockingState = Passable
```

绫诲瀷瑕嗙洊瑙勫垯锛?
```text
PlotType.Obstacle -> BlockingState.Blocked
PlotType.Camp -> OwnershipMode.Fixed
PlotType.Obstacle + GenerationType.TimedOpen -> invalid
PlotState.NotOpen -> not passable and not capturable
```

`Camp` 鐨勫叿浣撻樀钀ヤ笉鐢卞湴鍥捐〃閰嶇疆锛涚敱杩愯鏃舵垬鍦哄垵濮嬪寲閫昏緫缁戝畾鍙傛垬鏂规Ы浣嶃€?
### Editing workflow

宸ュ叿鍏ュ彛涓?EditorWindow + SceneView銆係ceneView 浣跨敤 `Handles` 缁樺埗鍜屼氦浜掞紝涓嶇敓鎴愭寔涔?GameObject銆?
SceneView 宸ュ叿妯″紡锛?
- `Select Plot`锛氱偣鍑讳换涓€ Hex 閫変腑鍏舵墍灞?Plot锛屽苟鍦?EditorWindow/Inspector 涓樉绀鸿 Plot銆?- `Paint Add`锛氬皢鐐瑰嚮鎴栨嫋鎷界粡杩囩殑 Hex 鍔犲叆褰撳墠 Plot銆?- `Paint Remove`锛氫粠褰撳墠澶氭牸 Plot 绉婚櫎 Hex銆?
绗竴鐗堜笉鎻愪緵 `Set Representative`锛屽洜涓轰唬琛ㄦ牸姒傚康宸插垹闄ゃ€?
鎵归噺鎿嶄綔锛?
- 鏀寔 SceneView 澶氶€?Hex銆?- 鏀寔 `Merge To Multi-Plot`锛屽皢閫変腑鐨?Hex/Plot 鍚堝苟鍒板綋鍓嶄富 Plot銆?- 鏀寔灏?HexId 鍒楄〃绮樿创鍒板綋鍓?Plot锛屼究浜庝粠 Excel 鎴?CSV 杈呭姪淇銆?
褰掑睘缁存姢瑙勫垯锛?
- 涓€涓?Hex 浠绘剰鏃跺埢鏈€澶氬睘浜庝竴涓?Plot銆?- `Paint Add` 鍙互鎶㈠崰鍏朵粬 Plot 鐨?Hex锛氫粠鍘?Plot 绉婚櫎璇?Hex锛涘師 Plot 鍙樼┖鍒欏垹闄わ紱鍘?Plot 浠庡鏍煎彉鍗曟牸鏃惰嚜鍔ㄦ妸 `PlotId` 鏀逛负鍓╀綑 Hex 鐨?`HexId`銆?- 浠庡鏍?Plot `Paint Remove` 闈炴渶鍚庝竴涓?Hex 鏃讹紝琚Щ鍑虹殑 Hex 鑷姩鎭㈠涓洪粯璁ゅ崟鏍?Plot銆?- 绂佹閫氳繃 `Paint Remove` 绉婚櫎 Plot 鐨勬渶鍚庝竴涓?Hex锛涘垹闄?Plot 蹇呴』浣跨敤涓撻棬鐨?`Delete Plot` 鎿嶄綔銆?- `Delete Plot` 鑷姩鎶婅 Plot 鐨勬墍鏈?Hex 鎭㈠涓洪粯璁ゅ崟鏍?Plot銆?- 褰?Plot 鐨?Cell 鏁伴噺璺ㄨ繃鍗曟牸/澶氭牸杈圭晫鏃讹紝缂栬緫鍣ㄨ嚜鍔ㄤ慨姝?`PlotId`锛氬崟鏍兼敼涓哄敮涓€ `HexId`锛屽鏍艰嫢褰撳墠闈炶礋鍒欏垎閰嶇┖闂茶礋鏁般€?- 澶氭牸璐熸暟 PlotId 鍒嗛厤浼樺厛澶嶇敤鏈€鎺ヨ繎 0 鐨勭┖闂茶礋鏁帮紝渚嬪 `-1`銆乣-2`銆乣-3`銆?- 淇敼 `Radius` 蹇呴』寮圭‘璁ゃ€傜缉灏忓崐寰勪細鍒犻櫎鍦板浘澶?Plot/Hex锛涙墿澶у崐寰勪細涓烘柊澧?Hex 鑷姩鐢熸垚榛樿鍗曟牸 Plot銆?- 鍒涘缓銆佸埛鏍笺€佸垹闄ゃ€佸悎骞躲€佸崐寰勪慨鏀瑰拰灞炴€т慨鏀归兘蹇呴』鎺?Unity Undo/Redo銆?
### Preview

榛樿棰勮锛?
- 棰滆壊鏄剧ず PlotType锛屽苟绐佸嚭 Obstacle銆丆amp銆丼mallCity銆丅igCity 鍜?Capital銆?- 鏍囩榛樿鍙樉绀?PlotId锛涘紑鍚被鍨嬫樉绀烘椂鍚屾椂鏄剧ず PlotType銆丟enerationType 鍜?TimedOpen 鐨勫垵濮?NotOpen 鐘舵€併€?- `HexId`銆佸潗鏍囧拰绫诲瀷鍚嶆槸鍙紑鍏虫樉绀哄眰銆?- 鏈垎閰?Hex 浣跨敤鏄庢樉閿欒鑹诧紝骞跺湪閿欒鍒楄〃涓樉绀烘暟閲忓拰鍓嶈嫢骞?`HexId`銆?
绗竴鐗堜笉鏄剧ず鍔ㄦ€佸綊灞炶壊銆傞瑙堝悓鏃舵樉绀洪€変腑 Plot 鐨勫垵濮嬭繍琛屾椂鐘舵€併€佹槸鍚﹀彲閫氳鍜屾槸鍚﹀彲鍗犻锛汿imedOpen 鍒濆涓?NotOpen銆傚綊灞炰粛鐢辫繍琛屾椂鍒濆鍖栵紝涓嶆槸 authoring 閰嶇疆瀛楁銆?
### CSV export

CSV 鏄粰绛栧垝瀹℃牳鍜屾湭鏉ュ鍏ヤ娇鐢ㄧ殑瀵煎嚭浠讹紱鏈?issue 涓嶅疄鐜板鍏ャ€傚鍑虹粨鏋滃簲鍖呭惈瓒冲鐨勫湴鍥炬嫇鎵戝拰绫诲瀷淇℃伅锛屼緵鍚庣画杩愯鏃跺姞杞藉櫒鎴栧垵濮嬪寲閫昏緫娑堣垂銆?
瀵煎嚭鏍煎紡锛?
- 鏍囧噯 `.csv`銆?- UTF-8 with BOM锛屼紭鍏堝吋瀹?Excel 鐩存帴鎵撳紑銆?- 鑻辨枃瀛楁鍚嶃€?- 鏋氫妇瀛楁浣跨敤鏁板瓧鍊笺€?- `HexIds` 鍒椾娇鐢ㄦ爣鍑?CSV quoting锛岀簿纭牸寮忎负 `"[1,2,3,4]"`锛屾棤绌烘牸銆?
瀵煎嚭璺緞鍥哄畾涓猴細

```text
Assets/HexMap/Gvg/Exports/<AssetName>/
```

姣忔瀵煎嚭瑕嗙洊鐢熸垚锛?
```text
Map.csv
Plots.csv
Cells.csv
README.md
```

`Map.csv` 鍒楋細

```text
MapId,Radius,Orientation,Plane,OuterRadius
```

`Plots.csv` 鍒楋細

```text
PlotId,HexIds,PlotType,GenerationType
```

`Cells.csv` 鍒楋細

```text
HexId,Q,R,PlotId
```

鎺掑簭瑙勫垯锛?
- 姣忎釜 Plot 鐨?`HexIds` 鎸?HexId 鍗囧簭銆?- `Plots.csv` 琛屾寜 `PlotId` 鍗囧簭锛涜礋鏁板鏍?Plot 鑷劧鎺掑湪闈炶礋鍗曟牸 Plot 鍓嶃€?- `Cells.csv` 琛屾寜 `HexId` 鍗囧簭銆?
`README.md` 姣忔瀵煎嚭瑕嗙洊锛岃鏄庯細

- 鏂囦欢鐢ㄩ€斻€?- CSV 缂栫爜鍜屾暟缁勫瓧娈垫牸寮忋€?- PlotType 鍜?GenerationType 鏁板瓧鏋氫妇瀵圭収銆?- PlotId 缂栧彿瑙勫垯銆?- 榛樿杩愯鏃跺垵濮嬪寲瑙勫垯銆丯otOpen 鐨勪笉鍙€氳/涓嶅彲鍗犻瑙勫垯鍜?Open()/Close() 澶栧眰璋冪敤杈圭晫銆?- CSV 褰撳墠鏄鍑哄鏍镐欢鍜屾湭鏉ュ鍏ユ簮锛屾湰 issue 涓嶆敮鎸佸鍏ャ€?
### Export validation

瀵煎嚭鍓嶇‖澶辫触鏉′欢锛?
- PlotId 鍞竴銆?- HexId 瀛樺湪浜庡綋鍓?Radius 鐢熸垚鐨勫湴鍥句腑銆?- Plot 闈炵┖銆?- Hex 鍞竴褰掑睘銆?- 鍦板浘鑼冨洿鍐呮墍鏈?Hex 閮借 Plot 瑕嗙洊銆?- 鍗曟牸 Plot 鐨?PlotId == HexId銆?- 澶氭牸 Plot 鐨?PlotId < 0銆?- PlotType 鍜?PlotGenerationType 鏁板€煎繀椤诲凡瀹氫箟銆?- Obstacle 涓嶅厑璁镐娇鐢?TimedOpen銆?
Obstacle 鐨勯樆纰嶈涔夌敱榛樿鍒濆鍖栬鍒欎繚璇?BlockingState.Blocked锛涘悓鏃舵牎楠?Obstacle 涓嶅厑璁镐娇鐢?TimedOpen銆?
### Tests

浼樺厛浣跨敤绾?C# / EditMode 娴嬭瘯锛岃鐩栵細

- 鍒犻櫎 RepresentativeCell 鍚庣殑 Plot 鏋勯€犲拰鐜版湁 GVG 瑙勫垯娴嬭瘯鏇存柊銆?- 杩愯鏃跺厑璁歌礋鏁板鏍?`PlotId`銆?- 榛樿鍏ㄥ浘鍗曟牸 Plot 鐢熸垚銆?- 鍗曟牸/澶氭牸 PlotId 瑙勫垯鏍￠獙銆?- 鍚堝苟銆佹姠鍗犮€佺Щ闄ゅ拰鍒犻櫎鍚庣殑鍏ㄨ鐩栦笌鍞竴褰掑睘銆?- Radius 鎵╁ぇ/缂╁皬鍚庣殑 Plot 淇瑙勫垯銆?- CSV row 鐢熸垚銆佹帓搴忋€丠exIds 鏁扮粍鏍煎紡鍜?GenerationType 鏁板€笺€?- PlotType/PlotGenerationType 鏁板€笺€両nitial/TimedOpen 鍒濆鐘舵€併€丱pen()/Close() 骞傜瓑琛屼负鍜?NotOpen 瀵昏矾绾︽潫銆?- Obstacle + TimedOpen 鍦?authoring validation 鍜?runtime 鎶曞奖鍏ュ彛琚嫆缁濄€?- UTF-8 BOM 鍐欏嚭銆?- README.md 鍐呭鍖呭惈 PlotType/GenerationType 鏁板瓧鏋氫妇瀵圭収銆侀粯璁ゅ垵濮嬪寲瑙勫垯鍜?NotOpen 琛屼负銆?
涓嶅仛 SceneView UI 鑷姩鍖栨祴璇曪紱EditorWindow 鍜?SceneView 浜や簰绗竴鐗堥€氳繃鎵嬫祴楠屾敹銆?

## Comments

### Agent implementation update - 2026-09-10

Implemented the refined authoring contract and the generation-state amendment: authoring stores topology, PlotType, and GenerationType; Initial projects to Open, TimedOpen projects to NotOpen, and outer runtime business controls Open()/Close(). Obstacle cannot use TimedOpen.

### Agent implementation update - 2026-09-11

Implemented the generation-type authoring field, runtime projection, editor preview, CSV/README export contract, validation, and focused EditMode coverage. Existing authoring resources were intentionally not migrated.

### Design amendment from Excel import/export decisions - 2026-09-11

This amendment supersedes the earlier CSV-only, single PlotId-per-Cell, negative multi-PlotId, and PlotGenerationType authoring rules in this issue.

#### Source and workflow

The first version supports this workflow:

    GVGMap.xlsx
        -> raw import snapshot
        -> normalized GvgMapAuthoringAsset
        -> GVGMap_<MapId>.csv

The ScriptableObject Authoring Asset is the editable source of truth after import. The raw Excel snapshot is retained for reporting and future round-trip Excel support. CSV is the first-version comparison/export format; Excel export is future work.

Each Excel row is one source Plot:

- a row with multiple Coordinates becomes one multi-cell Plot;
- a row with one Coordinate becomes one single-cell time layer;
- multiple rows referencing one Hex are sorted by Start into that Hex schedule;
- the importer does not infer merges from Note, Name, adjacency, or row proximity.

Import is transactional. Any Error prevents the existing Asset from being updated. Warnings may continue. The report records errors, automatic corrections, retained IDs, and newly assigned IDs.

#### Authoring data

Single-cell schedules are stored per Hex:

    SingleHexSchedule
        HexId
        PlotType
        Layers[]

    Layer
        PlotId
        Start
        End

All layers of one Hex use the same PlotType in this version. A first layer may start after zero to represent an initially closed Hex. Layers from the first Start onward must be sorted, contiguous, and non-overlapping. The interval is [Start, End), times are seconds from GVG start, and End = -1 means forever. Every map Hex has at least one layer.

Multi-cell Plots are separate records. They must use Start = 0 and End = -1 and cannot overlap a single-cell time layer.

PlotGenerationType is removed from the Authoring Asset, editor, CSV, and runtime model. The legacy Excel D column may be checked for compatibility warnings but is not persisted or exported.

#### PlotId allocation

Single-cell PlotId rules:

- a single-cell Hex with one layer uses PlotId = HexId;
- a Hex with multiple layers uses HexId for the first layer;
- later layers use a global sequence starting at the next whole hundred strictly greater than MaxHexId;
- current MaxHexId is 397, so later single-cell layers begin at 400;
- existing IDs are retained when they still match the layer identity;
- new layers use the next unused ID and deleted IDs are not reused.

Multi-cell PlotId rules:

    PlotId = 10000 + 1000 * (int)PlotType + sequence

With PlotType values Camp=1, Normal=2, Grass=3, SmallCity=4, BigCity=5, Capital=6, Obstacle=7, the ranges are 11000+, 12000+, 13000+, 14000+, 15000+, 16000+, and 17000+ respectively.

Imported IDs that are invalid for their PlotType or are not unique are reassigned and reported. The first version rejects Radius changes, new Hex generation, and map expansion.

#### Editor behavior

The Hex editor shows the ordered Layer list and the shared Hex-level PlotType. It supports editing boundaries, inserting layers, deleting layers, automatic ordering, and Undo/Redo. It preserves existing PlotIds wherever possible. Invalid gaps, overlaps, zero-length intervals, and invalid End placement are errors.

A PlotType change is applied to all layers of the Hex. Mixed PlotTypes across the same Hex schedule are invalid and are not silently converted. The current workbook entries where the same Hex is configured as both Obstacle and Normal must be corrected before import.

#### CSV export

The first version exports one UTF-8 with BOM file:

    GVGMap_<MapId>.csv

The exact header is:

    PlotId,HexIds,PlotType,Start,End

Each Plot is one row. HexIds is always a quoted, no-space array such as "[46]" or "[271,331,332,343]". Rows are ordered by PlotId and HexIds are ordered numerically.

The export does not include GenerateType, Note, Safe, GridRes, Name, Coin, Score, Npc, or other Excel columns. Runtime schedule consumption and PlotScheduleService remain outside this issue.

# dev_log — dangeon_3

> 開発方針：原作『ダンジョンバトルロワイヤル』× Civ VI × CDO2。詳細は Claude メモリ（project-overview / novel-canon / game-references）を参照。

---

## 現在のプロジェクト構造（Assets/Scripts）
- `DungeonGridSystem` … 50×50配列＋TileType(None=壁/Corridor/Room/TreasureChest/Trap)。`PlaceTile`(DP消費)、`TryExpandDungeonArea`(10→50拡張)、`GridToWorld/WorldToGrid`。
- `GridInputHandler` … マウスでタイル/冒険者/ゾンビ配置、UIボタン連動 `SetToolMode`。
- `AdventurerAI` … BFSで最魅力の部屋へ徘徊、HP30%で入口へ退却、Conquer目的はボスへ。※ボス位置は従来(端,端)ハードコード。
- `DungeonAdventurerSpawner` … 戦闘フェーズでウェーブ召喚。
- `DungeonTurnManager` … 準備⇄戦闘フェーズ。
- `DungeonResourceManager` … DP/名声/素材。
- `RoomData` … 部屋の魅力/感情/クールダウン。
- `ZombieAI`/`ZombieData` … 配下ゾンビ。
- `DungeonUpgradeManager` … 罠部屋アンロック等（技術開発の芽）。
- `CameraController` … カメラ移動/ズーム。

---

## イニシアチブ：迷宮生成の刷新（手動描画 → 自動生成＋要素手動配置）
方針：区画分割法(BSP)で迷路を自動生成。TileType(None/Corridor/Room)に書き込むので既存のBFS徘徊・接敵はそのまま動く。主要要素(トーテム/罠/スポナー/ボス/特殊敵)は後段(Step3)で手動配置。

### Step 1（実装中）: 自動生成コア
- [x] `DungeonGenerator.cs` 新規：BSPで有効エリア(currentPlayableSize)に部屋+通路を生成、入口/ボスセルを決定。
- [x] `DungeonGridSystem` に `BuildFromMap()` と `EntranceCell/BossCell` を追加。
- [x] `AdventurerAI`：ボス位置を `BossCell` 参照に変更。
- [x] `DungeonAdventurerSpawner`：入口セルから湧かせるよう変更。
- [x] Unity実機：生成→冒険者が自動迷路を歩く/接敵まで確認（自律デバッグ）。
      検証結果(2026-07-08 Play): 生成ログ「size 10x10 / 入口(2,3) / ボス(7,8)」、Room27/Corridor6/Wall67、入口・ボスとも歩けるRoom。防衛戦を開始し冒険者3体が入口から(2,7)(2,6)へ移動＝BFS徘徊OK、部屋効果発動＝接敵/相互作用OK、**コンパイル/ランタイムエラー0**。
      シーンに `DungeonGenerator` GameObjectを追加済（gridSystemは自動検出）。デバッグ再生成キー=B。

### Step 1 完了 ✅

## Step 2A（実装中）: 迷宮タイプ/空間タイプ選択（単一フロア）
- [x] `DungeonGenerator` に `DungeonType{Standard,Labyrinth,Cavern,Warren}` と `SpaceType{Cave,Ruins,Fortress,Lava,Ice}` を追加。タイプ→BSPプリセット(ApplyTypePresets)でレイアウト変化。空間→タイルの色調(GetSpaceTint)。
- [x] `SetDungeonType/SetSpaceType(int)` 公開（UIボタン用）。`GenerateAndBuild()` 公開。
- [x] `DungeonGridSystem.BuildFromMap(...,Color spaceTint)` にテーマ色を反映。`RoomData.ApplyThemeTint()` 追加。
- [x] バグ修正：size小(10)で minLeafSize>size/2 だと分割されず1部屋(入口=ボス)化 → `ApplyTypePresets` でサイズ依存クランプ。
- [x] 検証(Play): 全4タイプで入口≠ボス・多様な生成（Standard R56/C2, Labyrinth R20/C9, Cavern R42/C8, Warren R54/C5）、エラー0。
- [x] 宝箱ランダム配置：`ChestAmount{Small,Medium,Large}` 追加。生成時にRoomセルの一部を `TreasureChest` に変換（入口/ボス除外）。量で数が変化（小2/中3/大4＠size10、size50で更にスケール）。既存 `RoomData(TreasureChest, 魅力50)` を再利用＝リチャージ/クールタイム/感情→DP処理そのまま。
- [x] コスト設計：`GetGenerationCost()`＝基本500＋宝箱サーチャージ(小0/中300/大700)。`TryGenerateWithCost()` でDP消費生成。宝箱多い＝コスト大／だが冒険者から得るDPも増える（トレードオフ）。検証: 小2/中3/大4・コスト500/800/1200・宝箱にRoomData付与を確認、エラー0。
- [x] UIモックアップ公開（CDO2/Civ意識、迷宮タイプ/空間/宝箱量選択＋生成パネル＋上部HUD＋下部コマンドバー）→ 方向性OK。
- [x] UI実装 `GameUIManager.cs`（プログラム生成、CDO2/Civ意識のダークファンタジー）:
      ①生成パネル（迷宮タイプ4/空間5/宝箱量 少中多＋生成コスト表示＋生成ボタン）②上部HUD（作品名/Turn・フェーズ/DP・名声・素材、ライブ更新）③下部コマンドバー（配置ツール＋侵略開始）。旧Canvasは非表示化。
      日本語フォント問題を解決：CreateFontAsset(Font)はnull化 → **システムフォント名overload `CreateFontAsset("Yu Gothic UI","Regular",90)`＋Dynamicモード**で動的グリフ追加。
      検証(Play+スクショ): 日本語表示OK、生成ボタン→DP1000→200(800消費=基本500+中300)＋宝箱3再生成、侵略開始/フェーズ連動、エラー0。

## 無限徘徊問題の解決（Ⅱ満足値＋Ⅲ制限時間＋Ⅰ微調整）
問題：部屋がクールタイムで復活＋冒険者は探索対象が尽きるまで帰らない設計で、戦闘フェーズが終わらないことがあった。
- [x] Ⅱ 満足値（AdventurerAI）：`satisfaction` を部屋=微増/宝箱・罠=大きめ/感情で加算。個体差の閾値(`satisfyThresholdRange`×目的補正 探索1.25/踏破0.8)を超えたら帰還。帰還時に感情DP清算(GrantReturnReward共通化)。
- [x] Ⅲ 制限時間（DungeonTurnManager）：`baseWaveSeconds`(180)＋`ExtendWaveLimit()`でDP消費永続延長。時間切れ→全員ForceRetreat、猶予`graceSeconds`後もいればForceDespawnWithReward→HardEndWave。HUDに残り時間表示、下部バーに「戦闘時間+1分」ボタン。
- [x] Ⅰ 微調整（RoomData）：通常部屋`roomRegenTime`(20s)＞宝箱`regenTime`(8s)。
- [x] 検証(Play timeScale15): 5体が満足帰還ログ(閾値7〜16の個体差)→Fame+50(=5帰還)→ウェーブ自然終了(Turn2/準備復帰)。延長 180→240s/DP−300。エラー0。

## ③-1 カメラ自動フィット
- [x] `CameraController.FitToDungeon()`：生成サイズに合わせてorthographicSize自動調整＋センタリング、右パネル分だけ左寄せ(`rightPanelFraction`)。ホイール上限も自動拡張。`DungeonGenerator.GenerateAndBuild`末尾で呼ぶ。
- [x] 検証(Play): size10でortho5.8にフィット、迷宮全体が中央表示・パネルに被らない。エラー0。

## ③-2 主要要素の手動配置モード（完了）
- [x] `DungeonFeatureManager.cs` 新規：歩けるマスに色マーカーで配置（T/S/B/E＋色）。準備フェーズのみ、DP/素材消費、右クリックor消去で撤去(50%返金)。再生成時ClearAllFeatures。
  - トーテム：隣接部屋の魅力+20（Civ隣接×CDO2）。スポナー：戦闘中に防衛ゾンビを`spawnerInterval`毎に湧かせ`spawnerMaxPerWave`まで。ボス：BossCell上書き＋戦闘開始時に強化防衛体(hp3/atk2)、1つ制限（将来1階層1つ）。特殊敵：戦闘開始時に精鋭防衛体(hp1.8/atk1.5)。
- [x] `ZombieAI`：生成元からの強化倍率(hpMult/atkMult/speedMult/tint)をStartで反映。
- [x] `GridInputHandler`：ToolMode拡張(Totem/Spawner/Boss/SpecialEnemy/Erase)、配置/撤去結線、右クリック撤去、色プレビュー、`ZombiePrefab`公開。
- [x] `DungeonGridSystem`：`SetBossCell`、生成時に配置物クリア。`GameUIManager`：下部ツールを トーテム/罠/スポナー/ボス/特殊敵/消去/冒険者(検証) に更新。
- [x] 検証(Play): 配置4種成功・重複ボス拒否・DP1000→200(150+250+400)・素材−3・マーカー4・ボスセル更新・トーテムで隣接2部屋強化・戦闘でボス/特殊敵即時＋スポナー定期湧き(計4体, tintで種別確認)、エラー0。

## 魔王（ダンジョンコア）実装 — Step A（完了）
CDO2×小説ハイブリッド。1ダンジョン1体、最深部の魔王の間に配置、討伐でゲームオーバー。
- [x] `DemonLord.cs` 新規：HP(base600+turn*120)、隣接冒険者へ反撃、TakeDamage→死亡でDie()→ゲームオーバー。色マーカー"DL"＋HP表示。static Instance(1体)。
- [x] `DungeonGridSystem`：`DemonLordCell`(=最深部/最遠)、生成時に `DemonLord.PlaceAt` で最深部へ配置＆HPリセット。
- [x] `AdventurerAI`：踏破(Conquer)目的の挙動を改良＝旧「ボス到達で踏破成功→帰還」を廃止し、`DemonLordCell` へ向かい到達したら `assaultingCore`→`HandleCoreAssault()` で魔王を攻撃。探索しつつ魅力の高い部屋に寄り道→最終的に魔王を狙う。HP30%で退却。
- [x] `GameUIManager`：ゲームオーバー全画面オーバーレイ(`ShowGameOver`)＝「GAME OVER／魔王が討伐された」。
- [x] 検証(Play): 魔王を最深部(7,7)に配置・満HP。直接討伐でオーバーレイ＋停止。瀕死設定で防衛戦→討伐者が到達し魔王討伐→ゲームオーバー実発火。満HP(720)は単騎では倒しきれず耐える(魔王の反撃20/s)＝適切な難度。エラー0。

## 魔王 — Step B 門番ゲート（完了）
案A(改)：AIの標的をボス→魔王に切替＋魔王は保険で無敵。門番＝手動配置の「ボス」要素。
- [x] `ZombieAI`：`isGuardian`フラグ＋`GetLivingGuardian()`静的取得。ボス要素の防衛体を門番としてマーク(DungeonFeatureManager)。
- [x] `DemonLord.TakeDamage`：門番生存中は無敵(ダメージ無効)＋「GUARDED」シールド表示(青)。ラベルはASCII化(豆腐回避)。
- [x] `AdventurerAI`(踏破)：門番生存中は`guardian.MyGridPos`を最優先で狙い交戦、撃破後(or不在)に魔王の間へ。HandleCoreAssaultも門番存在時は中断。
- [x] バグ防止：門番未配置なら最初から魔王を標的＋無敵なし。
- [x] 検証(Play, 決定的): 門番なし=魔王に通る / 門番生存=魔王無敵 / 門番解除=討伐可, エラー0。

## 防衛体のガードモード（バグ修正）
問題：ボス(防衛ゾンビ)が冒険者を追ってスポーン地点まで移動→入口で即死させ、ターンをまたいでも居座る。
- [x] `ZombieAI` ガードモード(`anchored`)：配置セル(`anchorCell`)から`leashRadius`以内をランダム徘徊し、接敵時のみ停止して戦う（冒険者を追いかけない）。`GuardUpdate`/`PickPatrolCell`追加。
- [x] `DungeonFeatureManager`：ボス/特殊敵/スポナー召喚体を anchored 化(アンカー=配置セル、leash=3)。戦闘終了(`OnBattleEnd`)で自分の召喚体を消滅→次ターン開始で再配置（位置リセット・重複防止）。
- [x] 検証(Play): 門番 anchored=True, アンカー距離2/入口距離7で留まる＝入口へ行かない。ターンまたぎでゾンビ1体のみ＝重複なし。エラー0。

## トラック2 A案 フェーズ①：魔王ステータス＆種族進化（完了）
原作準拠。魔王が5ステータス＋LVで成長し、条件を満たすと種族へ分岐進化。
- [x] `DemonLord`：5ステータス(肉体/魔力/知識/創造/錬成, ランクE〜S)＋LV＋BP＋種族。昇格コスト逓増(2/5/10/18/30)。
  - レベルアップ＝防衛戦を1ウェーブ耐えるごと(`OnWaveDefended`, +LV/+BP)。`DungeonTurnManager.EndBattlePhase`から呼ぶ。
  - `TrySpendBPOnStat`でBP消費強化。`RecomputeCombatStats`で肉体→最大HP・魔力→攻撃に反映。
  - 進化：LV3以上＋条件(鬼=肉体C/魔族=魔力C/エルフ=知識C/ドワーフ=錬成C/スライム=Lv3/吸血=Lv5)で`EvolveTo`。種族でHP/攻撃倍率＋`DefenderCostMult`(ドワーフ0.7/吸血0.8/エルフ0.9)。
- [x] `DungeonFeatureManager.CostOf`：`DefenderCostMult`で配置コスト補正。
- [x] `GameUIManager`：左に魔王パネル(上部HUD「魔王」ボタンで開閉)＝LV/BP/5ステータス(＋ボタン)/種族/進化選択、ライブ更新。
- [x] 検証(Play): 2ウェーブ→Lv3/BP18、錬成E→C(BP-7)、ドワーフ進化可(鬼不可)、進化でmaxHP690(×1.15)・トーテム150→105、パネル表示OK、エラー0。

## A案フェーズ②：感情ツリー＋Eureka（完了）
- [x] `EmotionTreeManager` 新規：4系統(歓喜/興奮/絶望/殺戮)×各2ノード。感情プール＋Eurekaカウンタ(宝箱/罠/撃破/魔王攻撃)。Eureka達成でコスト×0.6。
  - 効果：歓喜=集客(BonusAdventurers)／興奮=防衛体強化(DefenderPowerMult)／絶望=罠ダメージ(TrapDamageMult)／殺戮=撃破DP(KillDPMult)・素材(KillMaterialBonus)。
- [x] フック：AdventurerAI(宝箱→歓喜/罠→絶望+ダメージ倍率/撃破→殺戮+DP素材/魔王攻撃→興奮)、Spawner(集客)、FeatureManager(防衛体強化)。
- [x] UI：`GameUIManager` 感情ツリーパネル(HUD「感情」ボタン)＝4系統プール＋ノード解禁ボタン＋Eureka★。
- [x] 検証(Play): 処刑Eurekaでコスト20→12、解禁でKillDP×1.5、興奮解禁で防衛体×1.2、パネル表示OK、エラー0。
## A案フェーズ③：3層バフ＋眷属種族相性（MVP・完了）
CDO2の3層バフ(装備/トーテム/遺物)のうち「トーテム(範囲)＋遺物(全体)」＋眷属の種族相性を実装。装備(個体)層は後追い。
- [x] `RelicManager.cs` 新規：遺物＝全体パッシブ層。カタログ4種(不死の王笏HP+25%/獣爪の紋章ATK+25%/業火の宝珠罠+60%/強欲の金貨撃破DP+40%)、スロット2、Toggle装備。getter: DefenderHpMult/DefenderAtkMult/TrapDamageMult/KillDPMult。シーンに `RelicManager` GameObject追加。
- [x] トーテム戦闘バフ(範囲層)：`DungeonFeatureManager.TotemDefenderBuff(cell)`＝配置セルの半径(totemBuffRadius=4)内トーテム基数×15%(最大2重)で防衛体を強化。従来の隣接部屋魅力+20はそのまま。
- [x] 眷属種族(`ZombieAI.Species` 不死/獣/魔族)＋種族プロファイル(不死hp1.25/atk0.9・獣hp0.9/atk1.25・魔族hp1.05/atk1.1＋識別色)。配置バーの「眷属」セレクタで種族選択(`SetSelectedSpecies`)→要素に記録→召喚体へ適用。
- [x] 種族相性：`DemonLord.AffinitySpecies`(鬼/エルフ→獣, 魔族/吸血→魔族眷属, その他→不死)＋`DefenderAffinityMult`(一致で×1.2)。
- [x] 合成：`SpawnDefender`で 興奮ツリー×遺物×トーテム範囲×種族プロファイル×相性 を全乗算。罠/撃破DPは`AdventurerAI`で遺物倍率も乗算。
- [x] UI：上部HUDに「遺物」ボタン＋遺物パネル(スロット表示・カタログ4枚トグル・装備中ハイライト)、下部バーに眷属種族セレクタ(不死/獣/魔族・選択ハイライト)。
- [x] 検証(Play, 決定的): 遺物getter(Hp1.25/Atk1.25)・装備トグル・相性(Oni→Beast×1.2/他1.0)、実召喚で hpMult=4.6575=3.0×1.25×1.15×0.9×1.2 / atkMult=4.3125=2.0×1.25×1.15×1.25×1.2 が期待値と完全一致。UIスクショで遺物パネル/眷属セレクタ表示OK。エラー0。
- [ ] 後追い：装備(個体スロット)層、遺物カタログ拡充、相性表の精緻化。

## Step 2B-①：複数フロア（階層）土台（完了）
複数フロアを生成・保持・切替。魔王は最下層のみ実在。バトルは現行の単一フロア防衛のまま（descent=A案は2B-②）。
- [x] `FloorData.cs` 新規：1フロア分(map/入口/ボス/色調/配置要素リスト/最下層フラグ)。
- [x] `DungeonFloorManager.cs` 新規(static Instance・シーンにGO)：floorCount(1〜3)、GenerateAllFloors(全階層生成→B1F構築)、SwitchTo(準備中のみ・現フロア要素を退避→対象を構築→要素復元)、ActivateFloor。最下層のみ`BuildFromMap(...,placeDemonLord:true)`。
- [x] `DungeonGenerator`：生成処理を `BuildFloorData()`(グリッド非依存・FloorData返す)へ分離。`GenerateAndBuild`はFloorManager有れば`GenerateAllFloors`へ委譲(無ければ単一フロア後方互換)。`GetGenerationCost`×階層数。
- [x] `DungeonGridSystem.BuildFromMap(...,bool placeDemonLord=true)`：最下層以外は`DemonLord.SetPresent(false)`で不在化。
- [x] `DemonLord`：`present`/`IsPresent`/`SetPresent`(子Renderer一括ON/OFF)。不在フロアはUpdate反撃なし・TakeDamage無効(誤ゲームオーバー防止)。
- [x] `AdventurerAI`(踏破)：`corePresent`ガード＝魔王が居ないフロアでは核を狙わず探索へ、HandleCoreAssaultも不在なら討伐扱いにしない。
- [x] `DungeonFeatureManager`：`FeatureRecord`＋`ExportFeatures/ImportFeatures`(フロア切替で要素を退避/復元)、配置処理を`AddFeature`に共通化。
- [x] UI(`GameUIManager`)：上部にフロアタブ(B1F/B2F/…、現在=金・最下層=朱「魔」)、生成パネルに階層数セレクタ(1/2/3層)＋コスト連動。
- [x] 検証(Play, 決定的): 2層生成→B1F魔王不在/B2F(最下層)在、フロア別マップ、要素の退避/復元(B1Fトーテム保持・B2F空)。3層生成→B3Fのみ魔王在。コスト 1層800/2層1600/3層2400。エラー0。
## Step 2B-②：階層踏破式（descent）（完了）
侵略を最上階から開始し、突破するたびにアクティブフロアが1つ下へ。最下層で魔王討伐＝ゲームオーバー。
- [x] `DungeonFloorManager` に descent状態(battleActive)＋`BeginDescent`(侵略開始でB1F構築＋防衛体spawn)／`EndDescent`(終了→B1Fへ戻す)／`Update`(breach判定)／`Descend`(降下)。
  - breach条件：非最下層＆spawn完了(IsSpawning=false)＆門番不在＆踏破冒険者が下り階段(=このフロアのボスセル)に到達。
  - Descend：退却中は報酬清算し退場、生存者を次フロア入口へ`RelocateTo`（HP持ち越し＝消耗）、防衛体を撤収→次フロア構築→次フロアの防衛体spawn。最下層に降りると魔王が実在。
- [x] `DungeonFeatureManager`：`SpawnDefendersForActiveFloor`／`DespawnDefenders`をpublic化し、複数フロア時はFloorManagerが降下ごとに駆動（OnBattleStartはFloorManager有れば何もしない）。
- [x] `AdventurerAI`：`AdventurerPurpose`/`IsRetreating`公開、`RelocateTo(cell)`(位置/経路/標的/退却/討伐フラグをリセットして再ターゲット)。踏破の標的は最下層=魔王・それ以外=下り階段(ボスセル)。
- [x] `DungeonTurnManager`：StartBattlePhaseで`BeginDescent`(入口をB1Fに確定してからspawner起動)、EndBattlePhaseで`EndDescent`。
- [x] 検証(Play, 決定的): Descend()直呼び=生存者2体がB2F入口へ再配置・魔王present化。手動Update()でbreach判定=階段到達踏破者でB1F→B2F降下・魔王present=true・冒険者がB2F入口へ。エラー0。（AIの自然探索によるbreachはtimeScale依存で不安定なため手動Updateで決定検証）

## 2B 調整・バグ修正
- [x] バグ：準備中に最下層以外(B2F等)へ配置したボス/スポナーが侵略開始で消える → `BeginDescent` が今編集中フロアの要素を保存せずにB1Fへ切替＝`ClearAllFeatures`で消失していた。BeginDescent冒頭で `CurrentFloor.features = fm.ExportFeatures()` を追加。検証: B2Fにボス配置→タブ切替せず侵略開始→B2F降下でボス復元(liveFeatures=1)。
- [x] 調整：探索冒険者の帰還が早い → `satisfyThresholdRange` を (7,13)→(28,52)（約4倍）。※コード既定値だけでなくプレハブ資産にも古い(7,13)がキャッシュされていたため、`Adventurer_Prefab.prefab` の値も (28,52) に更新保存。検証: 探索閾値35〜65/踏破22〜42。

## descent不発バグ修正（ボス撃破→降下が起こらない）
- 症状：ボスを配置すると、撃破しても次フロアへ降下しない（ボス配置消失バグを直した副作用で顕在化）。
- 根本原因：`AdventurerAI.TargetNextDestination` の踏破ロジックで、門番排除後の核/階段ターゲットの魅力が **35** 固定。直後の部屋/宝箱ループが「魅力>現在値」で上書きするため、宝箱(50)や部屋に寄り道→満足→退却し、階段に到達しない＝`Descend`が発火しない。
- 修正：踏破目的＆門番不在(`conquerCommitted`)のときは部屋/宝箱ループをスキップし、核/階段(`conquerCoreAttraction=200`)へ直行させる。門番生存中は従来どおり門番最優先(999)。
- 検証(Play,実機リアルタイム): B1Fに弱体ボス→踏破6体が門番撃破→階段直行→`🚶⬇【突破】B2Fへ降下（生存者6）`ログ確認、current 0→1・魔王present化。決定的テストでも 門番生存=ブロック / 撃破(Destroy/isDead死体) / 2フロア両ボス で降下チェーンOK。
- 副作用メモ：踏破冒険者は寄り道looting無し＝目的直行に。探索冒険者は従来どおり収集。

## descent UI演出（完了）
- [x] 降下トースト：`GameUIManager.ShowDescentToast(floorLabel,survivors)`＝中央上に「B{n}Fへ降下！(生存者N)」を約1.7秒フェード表示(CanvasGroup, unscaledで動作)。`Descend`から呼ぶ。
- [x] 階段マーカー▼：`DungeonFloorManager` が非最下層のボスセル(降下地点)に▼マーカー(シアン)を表示、最下層は非表示。`ActivateFloor`末尾で`UpdateStairsMarker`(ImportFeatures後のBossCellに追従、B マーカーと重ならないよう右下オフセット)。
- [x] フロア切替フェード：`GameUIManager.PlayFloorTransition`＝全画面黒を alpha1→0 に0.35秒(unscaled)。`Descend`と`SwitchTo`から呼ぶ。
- [x] 検証(Play): B1Fに▼表示・降下トースト「B2Fへ降下！生存者4」表示・フェードalpha1→0、エラー0。スクショ確認済。

## descentスタック修正（ボス撃破後に降下しない）
- 症状：非最下層にボスを配置すると、門番を倒しても冒険者が別セルへ向かい降下せずスタック。
- 根本原因：ボス要素配置時 `grid.SetBossCell(cell)` は `BossCell` のみ更新し `DemonLordCell` は生成深部のまま。FloorManagerの降下判定は `grid.BossCell`（階段）を見るのに、`AdventurerAI` の踏破標的 `coreCell` は `DemonLordCell` を見ていたため両者が乖離。ボス無しでは両者一致するので露呈しなかった。
- 修正：`AdventurerAI` の `coreCell` を `corePresent ? DemonLordCell : BossCell` に。＝最下層は魔王、非最下層は下り階段(BossCell)を目標にし、降下判定と一致させる。
- 検証(Play,決定的): ボス配置で BossCell(1,1)≠DemonLordCell(8,9) を確認。門番撃破後の踏破パス終点=BossCell(1,1) に一致。BossCell到達→Descendは既検証済。

## 見た目仕上げ①：タイル（完了）
モックアップ承認済みの方向でタイルを手続き生成スプライト化（外部画像なし）。
- [x] `TileSpriteFactory.cs` 新規：Texture2Dで床/通路/宝箱/罠を32pxで描画しキャッシュ(key=type×tint)。床=石畳(縁取り＋上下ベベル)、通路=暗め平ら、宝箱=金の箱アイコン、罠=赤スパイク＋暗い窪み。空間テーマtintを焼き込み。
- [x] `DungeonGridSystem.SpawnTileVisual`/`PlaceTile`：プレハブのSpriteRendererに生成スプライトを割当（color=白）。
- [x] `RoomData.SetBaseColor(Color)` 追加：RoomDataがAwakeでプレハブ色を保持し再適用する問題を回避（テーマはスプライトに焼込済なので白基調に。クールダウン暗転もこの基準で動作）。
- [x] 検証(Play,スクショ): Cave(灰)/Lava(暖赤)/Ruins(緑灰) でテーマ差が明確、宝箱=金箱・罠=赤スパイク・通路=暗色・縁取り/ベベルOK、エラー0。
- 次: 見た目仕上げ②=ユニット(職業/種族色＋HPバー・発光魔王)、③=盤面フレーム/壁背景。

## 見た目仕上げ②-プロト：戦士キャラ（手続きリグ＋コードアニメ）（完了）
モックアップ承認済みのA案（外部素材なし・パーツ手続き生成＋コード制御）で戦士1体をプロト実装。
- [x] `PrimitiveSprites.cs`：白の円/角丸矩形/矩形スプライトを手続き生成（色=SpriteRenderer.color, サイズ=localScale）。
- [x] `CharacterVisual.cs`：戦士リグ（影/HPバー/脚/胴/盾/頭/兜/前立て/剣）を組み、コードでアニメ。歩行(脚振り+バウンド)/待機(呼吸)は移動を自動検知、攻撃(剣振り+スラッシュ軌跡)/被弾(白フラッシュ+のけぞり)は一発再生、死亡は親から切離し倒れ+フェードして自壊。向きは移動方向で反転。PlayAttack/PlayHurt/Die/SetHP。
- [x] `AdventurerAI`：Startで生成し旧スプライトを非表示。ExecuteJobSpecificAttack/HandleCoreAssaultでPlayAttack、TakeDamageでSetHP+PlayHurt、死亡でDie()→Destroy(カウント整合のためAI本体は即destroy、演出は切離した子が完遂)。
- [x] 検証(Play,スクショ): 待機=騎士シルエット/攻撃=剣振り下ろし+白軌跡/死亡=倒れ+フェード を確認、エラー0。※現状は全ジョブが戦士リグ表示（プロト）。
- 次: ②本実装＝ジョブ別リグ(盗賊/聖職者/魔法使い)＋詠唱/回復/罠解除モーション、眷属3種/門番/魔王(進化段階別)。

## 見た目仕上げ②-A：向き＋攻撃指向フレームワーク（完了）
- [x] `CharacterVisual`：進行方向で左右反転（水平移動で自動、攻撃時は`FaceTowards(x)`で対象を向き0.55s保持）。`MuzzlePos()`（手/武器の発射元）、`PlayHeal()`（武器を掲げる回復モーション）追加。HPバー/影は反転しない。
- [x] `BattleVfx.cs` 新規：手続きエフェクト（魔法弾の飛翔→着弾バースト／被弾フラッシュ／回復バースト＋上昇スパーク）。static ファクトリで短命自己アニメGO生成。
- [x] `AdventurerAI`：攻撃時に対象を向く。魔法=各対象へ`BattleVfx.Projectile(MuzzlePos→敵)`で弾を飛ばして着弾。MP切れ=素手の弱攻撃(0.3×)＋近接モーション。回復=詠者に回復モーション＋光輪、回復される側に`BattleVfx.Heal`＋HPバー更新。魔王攻撃時も対象を向く。
- [x] 検証(Play,決定的+スクショ): 左のゾンビへ Facing=-1・魔法弾 from2.84→to2.00（敵方向）、回復で味方HP20→40＋Vfx(healed側burst+spark/詠者burst)、魔法使いが左を向いて橙の弾を発射する画を確認、エラー0。
- 次: Phase B=ジョブ別リグ(盗賊/聖職者/魔法使い＋各モーション:詠唱/回復/罠解除/素手)、C=眷属/門番、D=魔王(進化段階別)。※向き/指向攻撃は眷属/魔王のリグ実装時に横展開。

## 見た目仕上げ②-B：ジョブ別リグ＋攻撃スタイル（完了）
- [x] `CharacterVisual`：`Init(RigType)`でジョブ別リグを構築（Awakeでは組まず、AdventurerAIがAddComponent後にInit）。共通ベース(影/HP/脚/胴/頭/武器ピボット)＋ジョブ別: 戦士=兜/前立て/盾/剣、盗賊=フード/とがり/短剣、聖職者=カウル/額当て/杖(玉+十字)、魔法使い=とんがり帽子/つば/杖(光る玉)。ジョブ別ボディ配色。
- [x] `PrimitiveSprites.Triangle()` 追加（帽子/フードのとがり用）。
- [x] 攻撃スタイル `AttackStyle{Swing,Stab,Cast,Punch}` を`PlayAttack(style)`で切替。Swing=斬りアーク+軌跡/Stab=前方突き+軌跡/Cast=杖を掲げる(魔法弾はAdventurerAI側)/Punch=素手ジャブ。
- [x] `AdventurerAI`：`RigOf(job)`でリグ選択、攻撃で職に応じたstyle(戦士Swing/盗賊Stab/聖職Swing/魔法Cast, MP切れPunch)。魔王攻撃も職別style＋魔法は弾。
- [x] 検証(Play,スクショ): 4ジョブの見た目が明確に別物、攻撃ポーズ(斬/突/杖掲げ)＋スラッシュ軌跡を確認、エラー0。
- 未: 罠解除モーション（解除ゲーム機構が未実装のため保留）。眷属/門番/魔王のリグ。

## 見た目仕上げ②-C：眷属3種＋門番リグ（完了）
- [x] `CharacterVisual`：RigType に Undead/Beast/Demonkin 追加、AttackStyle に Claw(爪の一撃=前方ランジ＋赤い軌跡)。`Init(type,scale,crown)` に拡大・王冠。眷属は前傾(baseLean)＋目(光)/牙 or 角/翼/尻尾/爪。門番=scale1.4＋金の王冠。`SetDowned(bool)`=倒れ状態(復活可・非破壊、色をグレー寄せ＋回転フェード)。
- [x] `ZombieAI`：Startで種族→リグ生成(門番は拡大+王冠)、旧SR/HPテキスト非表示。攻撃で対象を向き爪攻撃、被弾でSetHP+PlayHurt、死亡でSetDowned(true)、復活でSetDowned(false)+SetHP。
- [x] 検証(Play,スクショ): 不死(緑/前傾/黄目/爪)・獣(橙/角/牙)・魔族(紫/翼/赤目)が別物、門番=大きく金冠、門番の爪攻撃で左を向いてランジ、エラー0。
- 未: 特殊エネミー(精鋭)の視覚差別化は将来（現状は種族色のみ）。

## 見た目仕上げ②-D：魔王リグ＋進化段階別＋反撃演出（完了）
- [x] `DemonLordVisual.cs` 新規：魔王の大型リグを手続き生成。`BuildStage(Race)`で7種族の見た目差(人=紫/王冠, 鬼=赤/大角, 魔族=紫/角+翼, エルフ=緑/枝角, ドワーフ=茶/髭+角, スライム=緑ブロブ+目玉, 吸血=淡色/王冠+マント)。オーラ脈動(浮遊)、`SetGuarded`=無敵シアン輪、`PlayReprisal`=前傾一撃+暗い衝撃波(BattleVfx)、`PlayDeath`=崩落フェード。全アニメ unscaled(timeScale=0のゲームオーバーでも再生)。HPバー付き。
- [x] `DemonLord`：BuildVisualでリグ生成＋旧マーカー(四角/DL/HP)非表示。PlaceAt/EvolveToでBuildStage(進化反映)、Updateで無敵オーラ/HP更新/反撃時PlayReprisal、TakeDamageでHP更新、DieでPlayDeath。SetPresent(false)はGetComponentsInChildrenでリグごと非表示。
- [x] 検証(Play,スクショ): 7段階が明確に別物(スライムのブロブ含む)、無敵シアン輪/反撃衝撃波を確認、エラー0。
- 見た目②(タイル/ユニット/魔王)ひと通り完了。特殊エネミー差別化・盤面フレームは今後の余地。

## 見た目方針の確定：アセット導入→段階的フル・ピクセルダーク（2026-07-11 決定）
ユーザーが無料2Dアセットを導入。精査の結果、**「フル・ピクセルダーク」構成**に段階移行することを決定。
- 導入済アセット（詳細はClaudeメモリ [asset-store-eval] 参照）:
  - **Bloodlines - Dark UI** `Assets/Alebardium/Bloodlines UI`：HDダークゴシック(黒×赤)UI一式(枠/ボタン/進捗バー/トグル/スライダー/入力欄/アイコン/効果音)。9スライス。付属フォントはラテンのみ(日本語不可→Yu Gothic維持)。→**UIに採用確定**。
  - **Dungeon Tale** `Assets/Tileset/Dungeon Tale`：ピクセルのダークダンジョン(壁/床/ランプ+松明/宝箱/祭壇/スパイク/骨/旗+オカルト装飾+敵[赤悪魔ボス/金冠髑髏=魔王候補/スライム/ゴースト]+FX)。ノーマルマップ2Dライティング対応(URP設定要)。→**盤面タイル/小物/装飾に採用**。
  - **SPUM(Pixel Units)** `Assets/SPUM`：モジュール式ピクセルキャラ＋フルアニメ、完成プレハブHuman/Elf/Devil/Skelton。→**キャラに採用予定**。※**素のままURPでシアン**(同梱material=Built-in用+SpriteMask非互換)。要マテリアル差替+マスク調整。スプライト自体は正常。
  - **Tiny Swords** `Assets/Tiny Swords`：明るいカートゥーン＝テーマ不一致。**汎用FX(回復/矢/パーティクル)のみ拝借候補**、主役非採用。
  - **Space Game GUI kit**：SFで不使用。
- **段階プラン**:
  - **① Bloodlines UI 導入（次に着手・Opus）**：programmatic GameUIManager を Bloodlines のスプライト/prefabでスキン(HUD/各パネル/ボタン/魔王HP・ウェーブ時間の進捗バー)。日本語はYu Gothic維持、配色は黒×赤へ寄せる。ロジックは不変。
  - **② Dungeon Tale で盤面ピクセル化（Opus）**：TileSpriteFactory の手続きタイルを Dungeon Tale のスプライト/タイルへ差替、松明/宝箱/祭壇/オカルト装飾を配置。2Dライティングは任意。
  - **③ キャラのピクセル総入替（大工事・着手前 fable5 推奨）**：CharacterVisual/DemonLordVisualの手続きリグを SPUM(＋Dungeon Taleの敵)スプライト＋アニメに置換。SPUMのURP整備込み。既存のアニメ駆動フック(PlayAttack/Hurt/Die/FaceTowards/SetHP等)は流用しやすい設計。
- 注意: これまでの手続き生成(タイル①/ユニット②A-D/魔王D)は**②③で置換されるが、アニメ制御ロジックとフックは再利用**。移行中は一時的に画風が混在しうる。

### その先（アセット統合後）
- 研究ツリー画面／A案③後追い(装備層・遺物拡充)／特殊エネミー差別化。
- モデル運用: 複雑設計/非自明バグ/バランス詰め＝fable5を薦める（実装前に通知）。特に上記③は fable5 案件。

### 既知の調整余地
- 10×10は部屋が密。50拡張時に本領。タイプ別の差はサイズ50でより明確化。
- SpaceType色調：部屋はRoomData経由で乗算。より作り込むならテーマ別プレハブ/スプライトも検討。

### 懸念点
- 区画分割の最小サイズ/余白で迷路感が変わる → `[SerializeField]` で調整可能に。
- 初期 currentPlayableSize=10 のため生成される部屋数は少なめ（拡張=50で本領）。Step1は10で疎通確認。
- 既存の手動描画(GridInputHandler)はStep1では温存。Step3で「要素配置モード」へ改修予定。

### 次（Step2以降）
- 生成パラメータ(迷宮タイプ/階層/空間タイプ)＋準備フェーズの生成ボタン＋DP消費、拡張時の再生成。
- Step3：入力を要素手動配置へ改修。
- トラック2(A案)：種族進化＋感情ツリー(Eurekaブースト)＋3層バフ。

## 構成の深化：原作×Civ×CDO2の再統合（2026-07-11 設計）
アセット導入で"ユニット/機能の幅"が解放されたのを機に、3源流をより深く取り込む構成を再設計。承認スコープ=**B(UI枠＋ロスター刷新)**／配色=**黒×血の赤**。
- **統一スパイン**: 「魔王として金沢を制圧し世界統一」＝ローグライト・ダンジョン防衛(CDO2)を戦術層に持つ4Xキャンペーン(Civ)を、原作のカオス/ロウ/ニュートラル三勢力世界と配下/眷属/進化/誘導経済で演出。地下(戦術=完成済)／地上(戦略=Phase4)／**眷属＝二層の橋渡し**(原作最重要の未実装概念、SPUM名前付きキャラが解放)。
- **源流別の"深さの穴"**:
  - 原作: ①配下vs眷属の二層(眷属化＝真名+LP編成で分隊を率い外征) ②配下ロスター/ティア(スライム1…ダークエルフ50)+配下進化 ③誘導経済(錬成→宝箱で噂→勇者誘引→泳がせ狩り、両刃=与装備が敵強化) ④特殊制限創造(禁止/強化/緩和×種族/魔法/武器/人数/属性,DP) ⑤擬似的平和(有限の無敵準備期間)。
  - Civ: ①並列2ツリー(感情+研究) ②政策カード(=特殊制限と統合) ③都市国家=ニュートラル ④勝利条件=世界統一。
  - CDO2: ①部屋スロット編成(1部屋N体+役割comp+満員ボーナス=Civ隣接と接続) ②種族の機械的個性(不死=とどめ再生成/獣=加速stack/魔族=吸血) ③研究4系統/オーブ/イベント/盗賊団収入/2倍速・オート。
- **アセットが今すぐ解放する層(=Bで着手)**: 配下ロスター刷新(抽象3種→ティア×役割×種族の魔物図鑑)／部屋スロット編成／種族の機械的個性／眷属化の土台。
- **Phase①の再定義**: 単なる5パネル再スキンでなく、**Bloodlines製UIフレームワーク(研究/眷属/政策/図鑑/イベントの拡張スロット付き)**として構築。今は器だけでも用意し二度手間を回避。日本語=Yu Gothic維持、黒×赤。

### 実装ログ（このセッション）
- [x] `MinionCatalog.cs` 新規（純staticデータ土台・既存シーン/コード非依存）：配下ロスター16種＝3ファミリー(不死/獣/魔族)×5役割(盾/近接/遠隔/支援/妨害)。原作CPティア準拠(ラット1/バット2/ウルフ3/ゾンビ4/ゴブリン5/スケルトン…/コボルト10/大獣10/インプ15/オーク20/ダークエルフ50)。各Def=family/role/tierCP/hp・atk・spd倍率/rig(ファミリーリグ流用)/AttackStyle/spumHint(後でSPUM/Dungeon Taleへ差替の当たり)/note。FamilyTrait(不死=とどめ再生成/獣=加速/魔族=吸血)はデータのみ(挙動化は後)。ByFamily/ByRole/Get/TryGet/RoleName等の参照ヘルパ。検証: コンパイル0err＋実行時count=16/内訳5-5-6。commit 3e74a0f。
- [x] 配下ロスター配線（`DungeonFeatureManager`＋`ZombieAI`）：選択をファミリー→カタログindexへ。`SetSelectedMinion(index)`追加、`SetSelectedSpecies`は後方互換で家系代表種を選ぶ、`SelectedSpecies`はindexから導出。Feature/FeatureRecordは`minionIndex`保持(フロア退避/降下でも個体保存)。`SpawnDefender`で個体Def(hp/atk/spd/role)を既存層(要素役割×興奮×遺物×トーテム×家系×相性)に合成。ZombieAIに`minionIndex`/`role`保持。FloorManager/FloorDataはFeatureRecordを不透明に受け渡すため無改変。検証(Play,決定的): 選択API(不死→スケルトン/獣→ラット/魔族→ゴブリン,図鑑直接選択)＋オーク召喚 hp=5.1975/atk=3.0800/spd=0.8 が期待値と厳密一致。commit 6120e9f。
- [x] ステップ2a Bloodlines UI（HUD枠＋ボタン＋魔王HPバー）：`GameUIManager`にBloodlinesスプライトを serialized 参照で持たせ、ヘルパー経由でスキン。①主要ボタン(PrimaryButton)をBloodlinesボタン(灰/赤・SpriteSwapで状態)化、侵略/生成=血の赤。②上下HUD帯を黒(HUD_BG)＋血の赤の縁ライン、パレットに BLOOD/BLOOD_DK 追加。③**魔王HPバーを上部HUDに新設**(Bloodlinesバー・ライブ更新・不在フロアは淡色)。スプライト未割当時はフラット色にフォールバック。スプライト11枚をMCP(SerializedObject)でシーンの GameUIManager に割当→シーン保存。検証(Play,スクショ): 黒×赤HUD・魔王HPバー(満HP赤)・赤ボタン枠を確認、実行時エラー0。
- [x] ステップ2b Bloodlines UI（パネル枠）：`SkinPanel(Image)`ヘルパー追加＝不透明の暗い下地(HUD_BG)＋Bloodlines大枠(skinFrame=Frame_main_menu,border70)を最背面の子として重ねる方式。生成/魔王/感情/遺物の4パネルに適用(未割当時はOutlineにフォールバック)。検証(Play,スクショ): 4パネルに装飾フレーム(角飾り＋暗い内装)・内容の可読性OK、実行時err0。※MCPスクショはランタイム変更の反映に数フレーム遅延あり(2回撮る/ForceUpdateCanvasesで対処)。
- [x] ステップ2c Bloodlines UI（配下図鑑セレクタ）：下部バーの旧「不死/獣/魔族」ボタンを廃し、`BuildMinionCodex`で図鑑パネル(Bloodlines枠)を新設＝家系タブ(不死/獣/魔族)→個体行(名前/役割バッジ色分け/T・HP・ATK・SPD倍率/説明)。行クリックで`SetSelectedMinion(catalogIndex)`、選択行は金枠ハイライト、下部バーに「図鑑▸ {選択個体}[役割/Tティア]」表示。`RefreshMinionCodex`は再構築時に旧行をSetActive(false)→Destroyで同フレーム重なり回避。検証(Play,スクショ): 魔族6種の一覧・オーク選択ハイライト・バーlabel・役割色分け、実行時err0。MinionCatalog16種がUIから完全選択可能に。
- ついでにバグ修正: `DemonLordVisual.Update` が魔王リグ再構築の一瞬に空`baseCols[0]`を触りArgumentOutOfRangeExceptionを毎フレーム量産していた既存バグをガード(rig/bob/parts/baseCols空で早期return)。commit d030465。
## 部屋スロット編成＝部隊(Squad)方式（A案・完了）
CDO2の部屋スロット編成×Civ隣接を、現アーキ(要素配置)に自然に乗る「部隊」で実装。ユーザー承認=A案。
- [x] `DungeonFeatureManager`：FeatureType.Squad追加。編成API=`SquadAdd/SquadRemoveAt/SquadClear/CurrentSquad`、`SquadCost`(ティア合計×squadCostPerTier10×種族コスト補正)、`SquadDistinctRoles`、`SquadCompMult`(役割distinct-1×0.10＋満員(5枠)+0.15)。`TryPlaceSquad(cell)`=DP消費して編成を1セルに配置。Feature/FeatureRecordに`squad`(List<int>/int[])保持しフロア退避/降下でも保存。`SpawnDefendersForActiveFloor`にSquad分岐＝編成各体を`SpawnDefender(...,squadMult=comp)`でスポーン(コンプ倍率を全員のhp/atkに乗算)。撤去返金・マーカー(色STEEL/文字"隊")対応。
- [x] `GridInputHandler`：ToolMode.Squad(=11)追加、クリックで`TryPlaceSquad`、プレビュー色steel。
- [x] `GameUIManager`：図鑑パネルを高さ520に拡張し下部に**編成トレイ**(5枠・役割色分け・クリックで抜く・クリア・「コスト/役割N種/部隊バフ×N」表示)、各個体行に**＋隊ボタン**。下部バーに**「部隊」配置ツール**(青)追加。
- [x] 検証(Play,決定的+スクショ): 役割5種編成→コスト540DP/役割5種/コンプ×1.55、配置でDP1000→460、5体スポーン、skeleton hp=2.3250(prof1.25×相性1.2×def1.0×コンプ1.55)/atk=1.6740が期待値と厳密一致。トレイ/＋隊/部隊ツールのUI表示OK、実行時err0。
- 注: 検証中に`DemonLord.Instance`がNULL化する事象＝生成連打(GenerateAndBuild churn)による一時的なもの。クリーン再生ではpresent/相性1.2正常＝通常プレイでは問題なし。

### 改善: 部隊を「隊員ごとに個別配置」へ（ユーザー要望）
「部隊まるごと1セル」→「隊員を1体ずつ好きな場所に配置」へ変更。役割コンプは編成全体から算出し各隊員に付与＝コンプ機能は維持しつつ分散配置可能（部屋クラスタ方式より軽量）。
- [x] `DungeonFeatureManager`：`TryPlaceSquad`(まるごと)を廃し`TryPlaceSquadMember(cell)`＝選択中スロット(squadPlaceSlot)の隊員1体を配置。Feature/FeatureRecordの`squad(List)`→`squadComp(float)`スナップショットに変更。`SquadMemberCost`(隊員1体=ティア×係数)、`SetSquadPlaceSlot`追加。スポーンは隊員1体をsquadCompで召喚。コスト・返金も隊員単位。
- [x] `GridInputHandler`：Squadツールのクリックを`TryPlaceSquadMember`へ。
- [x] `GameUIManager`：下部バー上に**隊員配置ストリップ**(編成隊員を役割色分けで並べ、選択→ツールを部隊に切替→マスクリックで配置)。図鑑の編成トレイは編成編集用に併存。トレイ情報を「役割N種・部隊バフ×N（各隊員を部隊ツールで個別配置）」に更新。
- [x] 検証(Play,決定的+スクショ): 5体編成(コンプ×1.55)→3体を別セルに個別配置→DP380(=各隊員コスト合計)消費、3体のみスポーン、skeleton hp2.3250(コンプ1.55込み)厳密一致。ストリップ表示・3つの隊マーカー・分散スポーンを確認、err0。
## 種族の機械的個性（FamilyTrait）実挙動化（完了）
CDO2×原作の種族アイデンティティを戦闘挙動に。倍率差だけだった3家系に"戦い方の違い"を付与。
- [x] `ZombieAI`：**魔族=吸血**(攻撃で与ダメ×lifestealFrac0.25を自己回復,`Lifesteal`+緑Heal VFX)／**獣=加速**(攻撃・被弾のたび`AddFrenzy`でmoveSpeed/attackSpeedが+8%/stack,上限8)／**不死=再生成**(とどめ時`featureMgr.RaiseUndead(cell)`で弱い骸1体、`isRaised`で連鎖防止)。baseMoveSpeed/baseAttackIntervalをStartで保持、featureMgrキャッシュ。
- [x] `DungeonFeatureManager`：`SpawnDefender`が生成ZombieAIを返すよう変更、`RaiseUndead(cell)`=スケルトンを0.4倍で召喚しisRaised化＋暗緑Burst。raisedHp/AtkMult(0.4)設定。
- [x] 検証(Play,決定的): テスト用ZombieAIで 魔族HP10→20(吸血) / 獣ms1.80→1.94(×1.08) / 不死とどめでzombies3→4かつisRaisedフラグ1、err0。
- 注: 全家系common-onの常時発動(将来は研究ツリーで解禁/強化する余地)。
## 配下進化＝ロスターのアンロックツリー（完了）
原作の配下進化 × CDO2のアンロック進行。ロスターに既存の基本形/進化形を活かし、進化=解禁で使える配下が増える。
- [x] `MinionEvolution.cs` 新規(静的・MinionCatalog不変)：進化パス(進化形id→進化元id)を9本定義(スケルトン→スケルトンアーチャー、ゴースト→リッチ、ラット→ウルフ→大獣、バット→ハーピー、ゴブリン→アーチャー/コボルト→オーク、インプ→ダークエルフ)。基本形7種は初期解禁、進化形9種はロック。IsUnlocked/CanEvolve(前提解禁済み)/EvolveCost(ティア×25)/TryEvolve(DP消費で解禁)。解禁状態は静的保持(セッション内・ドメインリロードで基本形へ)。
- [x] `GameUIManager`図鑑：行を進化状態で分岐＝解禁済み=＋隊/進化可=「🔓 X から進化可・NDP」+進化ボタン(赤)/前提未達=「🔒 X の解禁が必要」(淡色・ボタン無)。ロック中は選択不可。進化ボタンでTryEvolve→即解禁反映。
- [x] 検証(Play,決定的+スクショ): 初期解禁7/16、skeleton_archer進化可・orc/great_beast不可、進化でDP150消費して解禁、wolf解禁でgreat_beastが進化可に(連鎖)。魔族タブUI=基本/進化/ロックの3状態表示OK、err0。
## 内政の深化（設計合意＋実装開始）
2026-07-12、内政3system(誘導経済/研究ツリー/特殊制限)＋魔王3ステ接続を設計合意。全仕様はClaudeメモリ[internal-affairs-design]。実装順=③誘導→①研究→②特殊制限。主要決定: 進化を研究ゲート先へ/領域研究で4層以降拡張＆罠種類を段階解禁(階層は追加のみ)/特殊制限は0枠開始＋研究でスロット開放(最大3)＋CDO2ショップ＋レアリティ/宝箱の任意手動配置(拾得装備を素材に錬成)/研究点=知識レート+Eureka/魔王 知識→研究・錬成→誘導・創造→コスト減。

### P1 誘導経済コア（完了）
- [x] `LureEconomy.cs` 新規(静的)：世界の脅威度threat(1.0〜6.0)。`OnHeroEscaped(level)`=逃走で脅威度↑(0.05×(1+lv×0.01))＋Fame+25。getter: HeroHpMult(=threat)/HeroAtkMult(1+(threat-1)×0.5)/ExtraWaveCount(floor((threat-1)×3))/RevenueMult。Reset()。
- [x] `AdventurerAI`：Startで maxHP×=HeroHpMult、threatAtkMult=HeroAtkMult(baseDmg/魔王ダメに乗算)。`GrantReturnReward`(生還=逃走)で`OnHeroEscaped`。撃破DPに×RevenueMult。
- [x] `DungeonAdventurerSpawner`：ウェーブ数に+ExtraWaveCount。
- [x] `GameUIManager`：HUDに脅威度チップ(赤)。
- [x] 検証(Play,決定的+スクショ): Lv20逃走×10→脅威度1.0→1.60・HP×1.60・攻撃×1.30・追加ウェーブ+1(total6)・撃破×1.30・Fame+250。HUD脅威度1.60表示、err0。
### P1b 装備ドロップ両刃（完了）
- [x] `LureEconomy`：世界の装備水準gearLevel(0〜100)追加。`OnGearEscaped(carriedGear)`=持ち逃げ装備×0.5を加算、`GearRecoverMaterials`=撃破で素材回収。HeroHpMult/HeroAtkMultに装備水準係数(HP+2%/ATK+3% per gear)を合成。
- [x] `AdventurerAI`：`carriedGear`＝宝箱略奪で加算(1+joy×0.05)。逃走(GrantReturnReward)で`OnGearEscaped`、撃破で`droppedMaterials += GearRecoverMaterials`(回収)。
- [x] 検証(決定的): 装備4持ち逃走→水準2.0/HP×1.040/ATK×1.060、+6で水準5.0、回収(7.4)=素材7。err0。誘導経済＝宝箱で釣る→略奪者逃走で脅威度＋装備水準↑(敵武装)／撃破で素材回収、の両刃が成立。
- 未(任意): HUDに装備水準チップ(現状は勇者強度に反映のみ)。
### P2 研究基盤＋魔物研究(進化ゲート)（完了）
- [x] `Research.cs` 新規(静的)：`ResearchCatalog`(18ノード×4分野=魔物/領域/錬成/魔王, id/field/name/desc/cost(RP)/prereq/row)＋`ResearchState`(RP・解禁集合・IsResearched/CanResearch/TryResearch/PrereqMet・OnTurnEnd(知識ランク)でRP獲得(基礎1+知識×1))。
- [x] `DungeonTurnManager.EndBattlePhase`：`ResearchState.OnTurnEnd(魔王知識ランク)`で毎ターンRP獲得。
- [x] 魔物研究で**進化ゲート化**(`MinionEvolution`)：進化段階Depth(基本0/進化形は進化元まで辿った段数)＋`TierResearchId`("m_evo"+depth)。CanEvolveに研究ゲート追加＝前提解禁＋該当段階(配下進化Ⅰ/Ⅱ/Ⅲ)研究済みで初めて進化可。`TierResearchNeeded`(研究待ち状態)。図鑑は 進化可/🔬研究で開放/🔒前提未達 の3状態表示。
- [x] `GameUIManager`：研究ツリーパネル(Bloodlines枠・4分野カラム・ノードは研究済(緑)/可(金+コスト)/前提未達(淡色)・クリックで研究)＋HUD「研究」ボタン＋RP表示。
- [x] 検証(Play,決定的+スクショ): OnTurnEnd(知識2)→RP3、進化前skeleton_archer不可(研究待ち)→m_evo1研究(RP3→0)→進化可、Depth(sa=1/great_beast=2)。パネル4分野18ノード表示・前提gating・金枠、err0。
- 未(効果配線): 領域研究(4層+拡張/罠5種)/錬成研究(宝箱手動配置)/魔王研究(反撃/回復)/特殊制限スロット。Eureka加算も後続。※現状はノード解禁は動くが進化以外の効果は未接続。
### P2続き-領域研究:横拡張（階層ごとの広さ）（完了）
ユーザー追加要望。縦(階層数)に加え横(各階の広さ10→50)を領域研究に。グローバル解禁での一括安価拡張を防ぐため階層ごとにRP＋DP投資。
- [x] 各階が独立サイズ：`FloorData.size`追加、`DungeonGridSystem.SetPlayableSize(n)`(アクティブ窓を階層サイズへ、配列は50固定なので再確保不要)、`DungeonGenerator.BuildFloorData(int targetSize)`でサイズ指定生成。`DungeonFloorManager.ActivateFloor`が構築前にSetPlayableSize、GenerateAllFloorsは各階10×10から。
- [x] `DungeonFloorManager.TryExpandFloor(i)`：準備中のみ、次サイズのRP(3/5/8/12)＋DP(400/800/1500/2500)を消費、その階を新サイズで再生成(既存配置はクリア＋`fm.RefundRecords`で50%返金)、アクティブ階なら再構築＋カメラフィット。順送り・縮小不可。`ResearchState.TrySpendRP`追加。
- [x] 階段は入口から最遠：既存`DecideEntranceAndBoss`(ボス=入口から最遠の部屋)がサイズ拡大でも自動で担保(検証で確認)。
- [x] UI：HUDに「拡張」ボタン＋階層拡張トラックパネル(各階の現在サイズ→次段のRP/DP＋拡張ボタン、準備中&RP&DP充足で有効)。
- [x] 検証(Play,決定的+スクショ): B1F 10→20→30(RP8/DP1200消費)・gridSize追従30・B2Fは10のまま(独立)・階段距離46(30マップでほぼ最大)。拡張トラックUIと30×30大迷宮を確認、err0。
### P2続き-領域研究:罠5種＋罠の永続化バグ修正（完了）
症状: 罠を配置してもターン開始(BeginDescent→ActivateFloor→BuildFromMap)でマップ再構築され消えていた（罠はタイルで、要素export/importに乗っていなかった）。※処理(RoomDataタイル/盗賊のMP解除/クールダウン)は既に健在＝永続化のみの問題。
- [x] 罠を`DungeonFeatureManager`の`FeatureType.Trap`要素化。TryPlaceTrapで種類選択・DP消費・配置→`grid.StampTile`(無コスト敷設・新設)で罠タイルを敷きRoomData(damage/trapKind)を設定。Feature/FeatureRecordに`trapKind`。**export/importに乗るので永続化**(BeginDescent/フロア切替で保存・復元)。撤去で床へ戻す＋返金。RefundRecordsも罠コスト対応。
- [x] `GridInputHandler`：罠クリックをTryPlaceTrapへ（旧isTrapUnlockedゲート廃し研究ゲートへ）。
- [x] `TrapCatalog.cs` 新規：罠6種(通常＋毒沼/炎/氷/電気/針)。name/color/dpCost/damage/statusPower/statusDur/researchId。IsUnlocked=通常常時/他は領域研究(d_trap_*)解禁。
- [x] `RoomData.trapKind`追加。`AdventurerAI`：踏むと種類に応じ状態異常＝DoT(毒/炎/出血,0.5秒毎)/凍結(氷,移動停止)/麻痺(電気,周期的に短停止)。Updateで凍結中は攻撃/移動/回復を停止。
- [x] UI：罠ツールで罠種ストリップ(6種・ロックは🔒・研究解禁で選択可)。
- [x] 検証(Play,決定的): 研究ゲート(通常T/毒F→d_trap_poison研究でT)、配置でtrapKind設定、**B2F往復で罠が残存(永続化バグ修正)**、状態異常(毒DoT5秒/氷凍結2.5秒)。err0。※罠ストリップはMCPスクショ遅延で未撮影だがactive/children確認済(実機で表示)。
### P2残（完了）
- [x] 魔王研究の効果配線(DemonLord.Update): k_reprisal=反撃×1.6 / k_regen=戦闘中1%/秒回復。検証: k_regenでHP300→600。commit 1615021。
- [x] 領域研究-縦拡張(DungeonFloorManager.TryAddFloor＋UI): 準備中に階層追加(最下層=魔王が移る)。3層までDPのみ、4層目d_floor4/5層目d_floor5研究ゲート、最大5・削除不可。フロアタブ3→5、拡張パネルに階層追加行。検証: 2→3(800DP)、4層目研究前不可→d_floor4後可(2000DP)、最深部移動。commit 5fa7c86。
- [x] 錬成研究-宝箱手動配置(FeatureType.BaitChest): r_baitchest解禁→DP200＋素材2(拾得装備)で任意配置。isBait宝箱=集客80(通常50)＋richなjoyValueでloot/gear多い(誘導と両刃連動)。罠同様に要素化しexport/importで永続化。宝箱ツール(SetToolMode12)。検証: 研究前不可→解禁で配置(DP/素材消費)、B2F往復で永続化。commit 予定。
- **★P2(研究基盤＋魔物/領域/錬成/魔王研究)ひと通り完了。** 未: Eureka加算(研究点をお題達成で加速)、研究ツリー本体の他ノード微調整。
- 次: P3 特殊制限(政策カードショップ/レアリティ/研究スロット開放/効果) / (大)眷属化→地上4X / 見た目③SPUMキャラ(fable5推奨)。

## 「強さの幅・種類・段階」拡張計画（2026-07-12 資料読了・設計）
ユーザー要望: 魔物/冒険者/魔法/武器防具に種類・段階・強さの幅を持たせたい(assetを活かす)。参考資料=n4282fq「小説設定資料」(Twilight)を9章WebFetchで読了。抽出システムと実装フェーズの詳細はClaudeメモリ[strength-variety-systems]。要点:
- 資料抽出: 魔法5階級(最下級→最上級)＋7+9属性、魔物の進化/適応進化＋職ツリー(基本→上位→最上位)＋ランクS-G、レアリティ14段階/魔物8分類、冒険者職カテゴリ多数、装備素材ラダー(鉄→ミスリル→オリハルコン)＋防具段階。
- **重要**: MinionEvolutionは既に段階(Depth)＋分岐(1親→複数子)＋研究ゲート(進化Ⅰ/Ⅱ/Ⅲ)対応済=**魔物ツリー拡張はMinionCatalogのデータ追加が中心**で着手容易。
- 実装フェーズ: PM魔物ツリー(基本→上位分岐→最上位＋rank＋SPUM/DungeonTaleビジュアル)→PA冒険者ランク(F-Sラダーをfame/threat連動＋職追加)→PA2/PE装備グレード(素材ラダーで攻防、誘導のgearLevel/装備両刃と接続、CDO2装備層完成)→PG魔法(属性＋魔法ランク、罠状態異常を統一)→PM2適応進化(属性副軸)。推奨順=PM→PA→装備→PG→PM2。装備/魔法の込み入った設計はfable5候補。
- ★次セッション着手候補: PM(魔物ツリー拡張)から。既存インフラ流用でデータ追加中心。
- 注: Unity MCPは一時切断→再接続済で以降は通常フロー(refresh_unity→read_console→Play検証)。スプライト割当はSerializedObjectでシーンに保存済(ビルドでも有効)。

## PM 魔物ツリー拡張（2026-07-12）✅
配下ロスターを16→34種、4段階(基本→進化Ⅰ→上位Ⅱ→最上位Ⅲ)×分岐に拡張。既存インフラ(MinionEvolution.EvoFrom＋研究m_evo1/2/3二段ゲート)を流用しデータ追加中心で実現。
- MinionCatalog: Rank{G..S}追加＋IndexOf/RankName、3ファミリー完成(不死/獣/魔族=ゴブリン職ツリー)。最上位=death_knight/elder_lich(不死), behemoth/fenrir(獣), goblin_general/goblin_wizard(魔族)。
- MinionEvolution.EvoFrom: 分岐追加(1親→複数子)。depth分布 基本7/Ⅰ11/Ⅱ10/Ⅲ6。
- GameUIManager: 図鑑にランクバッジ(RankHex)表示。34種を自動列挙。
- 検証: 親解禁＋研究段階の二段ゲートを全4段チェーンで決定的テスト(goblin→shaman→mage→wizard)、コンパイルエラー0。
- NEXT: PA(冒険者F〜Sランク＋職追加, fame/threat連動)。装備が重いならfable5推奨。見た目(SPUM個別スプライト割当)は後段。

## NEXT: UI-1 図鑑/研究の全画面リデザイン（2026-07-12 計画・実装は次回）
PM(配下34種)後、図鑑が固定620×520・スクロール無しで見切れる問題をユーザー指摘。参照=CDO2魔物召喚画面／Civ社会制度ツリー。
- 決定: 今回UI-1のみ(レイアウト刷新)。個体Lvシステムは UI-2 に分離。プラン制限が近く本回は記録のみ・実装は回復後。
- UI-1: 図鑑=全画面化＋左家系タブ＋段階(基本/Ⅰ/Ⅱ/Ⅲ)グループのカードグリッド＋縦スクロール(CDO2風)。研究=全画面＋前提を直交線でつなぐCivツリー。🔒絵文字フォント欠落警告も潰す。
- UI-2: 個体ごとLv(使うと上がる)・タブ管理・隊=種類選択/配置=個体選択。コスト概念は実装不要(ユーザー明言)。
- 詳細計画・実装メモ(該当行/データ準備状況)は memory: codex-research-ui-plan.md に記録。

## UI-1 図鑑/研究の全画面化（2026-07-12 実装／Unity未接続でコンパイル未検証）
GameUIManager.cs:
- 図鑑=全画面(1820×1020)＋左家系タブ(全体/不死/獣/魔族)＋段階(基本/Ⅰ/Ⅱ/Ⅲ)グループのカードグリッド＋縦スクロール。新規MakeVScroll(ScrollRect+RectMask2D)。AddCodexカード(名前/役割/ランク/ステータス/進化ロック/＋隊or進化)。下部に部隊トレイ固定フッタ。
- 研究=全画面＋分野バンド。ResearchDepthで横位置、前提を直交線ResearchConnector/LineRectで親右→子左に接続(Civ風)。AddResearchCell。
- 🔒🔬🔓絵文字を◆◇―に置換(フォント欠落警告対策)。図鑑/研究トグルでSetAsLastSibling最前面化。
- ★未検証: Unity MCP切断中。再接続後 refresh_unity(scripts)→read_console(error)でコンパイル確認＋Play目視(全画面/スクロール/接続線/見切れ解消)。

## UI-2 個体システム（2026-07-12 実装・検証済み）
CDO2方式の個体ロスター。図鑑で種類選択→「召喚」でDP消費しLv1個体を追加(ランク高いほど高DP)、マップ配置は無償、同種を何体でも保持、配置時に個体を選択。育成=+1Lv/戦闘投入・+4%/Lv・上限50。
- MinionRoster.cs(新規): Individual{id,catalogIndex,level}、SummonCost(tier×15×創造)、TrySummon(未解禁/DP不足null)、LevelMult(50→×2.96)、LevelUp(cap50)。
- DungeonFeatureManager: Feature/FeatureRecordにindividualId(永続化)、TryPlaceSquadMember無償化＋個体選択(自動割当FirstUnplaced)、IsIndividualPlaced重複防止、Squadスポーンで×LevelMult＆出撃個体LevelUp、Squad返金0。
- GameUIManager: 図鑑カードに個体情報＋[＋隊][召喚-DP]、部隊ストリップ2段化(種類→個体Lv、配置済は淡色)、罠ストリップy110→150。
- 検証: コンパイルerror0、決定的テスト(召喚75/300・未解禁gate・LvMult・個体別育成・配置bind・export永続)＋Play目視(召喚カードUI・2段ストリップLv9/4/1)全OK。
- NEXT: PA(冒険者F〜Sランク＋職追加, fame/threat連動)。

## PA 冒険者ランクラダー（2026-07-12 実装・検証済み）
AdventurerAI: 3段(新人/PRO/BOSS)を F〜S(8段) ラダーに置換。
- worldTier = fame/250 + (脅威度-1)×0.8 + turn×0.12 → rankIdx。序盤G72%/F27%→終盤A37%/S59%(決定的テスト確認)。「だんだん強くなる」＋誘導経済(泳がせるほど強敵)連動。
- ランクでHP/ATK/速度＋色ラダー、攻撃=脅威度×ランク倍率。
- 職=4アーキタイプ(挙動/リグ不変)のまま表示名を階級ラダー化(基本→上位→最上位, 5段×4=20職名): 見習い戦士→戦士→剣士→騎士→英雄 / こそ泥→…→アサシン / 祈祷師→…→大司教 / 術見習い→…→大賢者。
- コンパイルerror0、ランク分布＋階級名ラダー決定的テストOK。
- NEXT: PA2/PE(装備グレード 鉄→ミスリル→オリハルコン, gearLevel/装備両刃と接続)。ランク→装備/魔法連動もここで。

## UI-2 調整3点（2026-07-12 実装・検証済み）
1. ボス連携: 「ボス」を召喚個体から各階層1体任命(TryPlaceBoss, GridInputHandler mode8)。bossHp/AtkMult×個体LvMult＋大型化scale1.7(SpawnDefenderにscale引数)＋出撃でLvUp。1フロア1体・無償。検証: guardian/scale1.36/hp2.84/atk2.20。
2. 個体重複配置バグ修正: IsIndividualPlacedを全フロア横断化(DungeonFloorManager.IsIndividualPlacedOnOtherFloors, current除外)＋Squad/Boss対象。1階配置の個体は2階に置けない。
3. 編成ゲート: 個体0体の種類は隊不可(SquadAddがCountOfType<=0で拒否＋図鑑＋隊ボタンcnt>0のみ)。
- Squad/Boss返金0。コンパイルerror0、Play決定的テスト全OK。

## ボス任命UI明示化＋冒険者成長ペース1/4（2026-07-12）
- ボス任命ストリップ新設(GameUIManager.BuildBossStrip/RefreshBossStrip): 「ボス」ツールで召喚全個体を「種類Lv」チップ列挙(未配置選択可/配置済淡色)＋現ボス状態、選択→マスクリックでTryPlaceBoss。featureMgrにFloorHasBoss/CurrentBossIndividualId。
- 配置ストリップ一元化(ShowStripFor): 部隊/ボス/罠は選択ツールで1つだけ表示。👑→◆ボス任命(フォント欠落対策)。
- 冒険者成長ペース約1/4: Lv式のturn/fame寄与を1/4(turn/4,fame/120,turn*3/4,fame/40)、ランクworldTier=fame/1000+(脅威度-1)*0.8+turn*0.03。検証: turn20/fame300 旧Lv~62→新Lv~16。脅威度(誘導経済)は据え置き。
- コンパイルerror0、Play目視(ボスストリップ)＋決定的テスト(ペース)OK。

## PA2/PE 装備グレード（2026-07-12 実装・検証済み）
EquipmentCatalog.cs(新規): 素材7段(銅→鉄→鋼→銀→ミスリル→アダマンタイト→オリハルコン)、武器atk(0.9→2.05)/防具hp(0.95→2.0)/色。GradeFromWorld(rank,gearLevel)で等級選択。
- PA2 冒険者: ランク＋gearLevelで武器/防具グレード決定→武器=atk倍率、防具=実効HP倍率、突入ログに武器/防具素材。LureEconomyのHero倍率からgearLevel項を除去し二重計上回避(gearの効果を装備に移管=逃がすほど高グレードの具体化)。
- PE 魔物個体スロット準備: MinionRoster.Individualにweapon/armorGradeスロット＋EquipAtk/HpMult/Equip()、SpawnDefenderにextraHp/AtkMult(非対称)追加し隊/ボス適用(現-1=素手×1.0)。装着UIを足せば即効く。
- 検証: グレードラダー/GradeFromWorld分布(序盤銅93%→終盤オリハルコン)/個体装備(ミスリル武器銀防具→atk1.50/hp1.25)全OK。コンパイルerror0。
- NEXT: PEのスロット装着UI(図鑑カードに武器/防具スロット)、PG魔法。

## PE 個体スロット装着UI（2026-07-12 実装・検証済み）
図鑑に「個体」タブ(codexFamilyTab==4)を追加。召喚した各個体を行表示し、武器/防具スロットをDP鍛造で1段ずつ強化。
- EquipmentCatalog.ForgeCost(grade)=(grade+1)*150(銅150→オリハルコン1050)。
- MinionRoster: GradeOf/Unequip/TryForge(次グレードへ+1段, DP消費)。
- GameUIManager: RefreshCodexIndividuals/AddIndividualEquipRow/AddEquipSlot。各行=種類#id/Lv/合計効果(攻×/硬×)/配置状態＋武器/防具スロット(色付きグレード＋「強化＋ -DP」＋「外す」)。
- スポーン適用は既存(extraHp/AtkMult)＝装備した個体は隊/ボスで強くなる。
- 検証: 鍛造(4段→銀武器/銅防具 atk1.28/hp0.95)・解除・コスト・Play目視(個体タブ:ゴブリンLv9ミスリル武器/銀防具)全OK。コンパイルerror0。
- NEXT: PG魔法(属性＋ランク)。装備入手を冒険者ドロップと連携する案も。

## fable5用 見た目刷新 作業指示書（2026-07-12 Opus作成）
fable5(今日まで)に見た目総入替を任せるため、事前調査＋詳細指示書 fable5-visual-brief.md を作成。
- cyan原因特定: SPUM/Core/Basic_Resources/Materials/SpriteDiffuse.mat = Sprites/Diffuse(ビルトイン fileID10753)→URP非互換。修正=Sprites/Default or URP2D Sprite-Lit-Default。
- 差し替え点: ZombieAI.cs:138 / AdventurerAI.cs:111(RigOf)。保持必須API: CharacterVisual.Init/SetHP/FaceTowards/Facing/MuzzlePos/PlayAttack/PlayHurt/PlayHeal/SetDowned/Die。
- SPUM在庫: Human16/Elf9/Devil13/Skelton8。獣はSPUM対象外→Dungeon Tale(Assets/Tileset/Dungeon Tale: ゴースト/スライム/悪魔ボス/髑髏王)/据え置き。
- 指示書に割当マッピング/検証手順/ガードレール収録。fable5は §2 cyan修正から着手。

## 見た目刷新: SPUMキャラ統合（2026-07-12 fable5実装・検証済み）
fable5-visual-brief.md に沿い実装。cyanは現環境で非発生と実測確認(SpriteDiffuse.mat=Sprites/Default解決済み)→修正不要。
- SpumMap.cs(新規): 配下25種(不死12/魔族13)をSkelton/Devil prefabに武器実測で割当(剣/弓/両盾/杖/斧/二刀)、ghost/wraith=半透明骸骨術者、獣9種=null→手続きリグ自動フォールバック。冒険者=職4×ランク3帯で装備良化。
- CharacterVisual.InitSpum: 既存API維持のSPUMバックエンド。SPUM左向き素体をx=-1正規化、SpriteRenderer群をsrs登録=被弾/ダウン/死亡演出が既存コード動作、IDLE/MOVE/ATTACK/DAMAGED/DEATHブリッジ。
- ソート: SPUMのUnitRootはSortingGroup内蔵→グループorder60、配置マーカー50→30に下げキャラ前面化。HPバー120/王冠118。
- ZombieAI/AdventurerAI呼び出し差替(フォールバック内蔵で安全)。
- 検証: error0/例外0、Play目視=骸骨剣士/ゴブリン/半透明ゴースト/弓/獣フォールバック/ボス大型+王冠/冒険者Human戦士の戦闘・反転・攻撃・HPバー全OK。
- 残: 獣の見た目(Dungeon Tale等)、魔王SPUM化、装備グレード色差し。

## 魔王SPUM化（2026-07-12 fable5実装・検証済み）
- SpumMap.DemonLordPath(Race): 人種/鬼/悪魔/エルフ/ドワーフ/ヴァンパイア→未使用SPUM prefab優先で割当、Slime=null→手続き粘体を意図的に維持。
- DemonLordVisual.BuildStageにSPUM分岐(FIT1.7・SortingGroup order62)。オーラ/翼/王冠/HPバー/討伐/反撃は手続き装飾を共用、反撃=ATTACK・討伐=DEATHブリッジ、SetHPはy位置保持化。
- 既存バグ修正: DemonLord.PlaceAtのSetPresent(true)が旧紫マーカーを毎回復活→sr.enabled=false追加(スライム粘体が紫正方形に隠れていた真因)。
- 検証: 人種(盾の君主+王冠+オーラ)/悪魔(二刀+翼+王冠)/スライム(粘体FB)目視OK、error0。
- 見た目刷新はこれで一区切り。残: 獣9種(素材無し)、装備グレード色差し。

## 獣9種の見た目: Enemy Galore統合（2026-07-13）
ユーザーが Enemy Galore(ADMURIN)/Dark Fantasy RPG Icons/GDD Character Pack をimport(My Assets確認は録画をUnity VideoPlayerでフレーム化して読取)。
- Enemy Galore=敵8種(Rat/Bat/Crab/Golem/GolemReinforced/Pebble/Skull/SpikedSlime)、Animator Controller統一(Run(Bool)/Attack/Hit/Death/Ability(Trigger))。
- Assets/Resources/EnemyGalore/*.prefab を8個生成(SpriteRenderer＋Animator＋Controller)。BeastMap.cs(id→prefab/scale/faceLeft)＋CharacterVisual.InitBeast(Animator駆動・既存API維持)。ZombieAIで獣はInitBeast。
- 狼系代用: wolf→SpikedSlime/dire_wolf→Crab/fenrir→Golem大(ユーザー合意)。検証: 全8クリーチャー描画・自然発色・影・HPバー・サイズ調整OK、error0。
- 次: Turbo Diskアイコン→PE装備スロット＋罠/魔法UI、GDD→特殊エネミーUI。

## 装備/罠アイコン: Turbo Disk統合（2026-07-13 StageA）
- Turbo Diskアイコン12種をAssets/Resources/Iconsへ（Sprite形式）。GameUIManagerにIcon/IconImgヘルパ。
- PE個体タブの武器/防具スロットに剣/盾アイコン（素材グレード色で着色）。罠ストリップに通常=棘/炎=火球/針=槍アイコン＋🔒→×。
- 検証: 個体タブ・罠ストリップ目視OK、error0。次: GDD→特殊敵6種/スポナー4種。

## 特殊敵/スポナーの見た目: GDD統合（2026-07-13 StageB/C）
- GDD 10体を色バリアント選択でResources/GDD/*.prefab化(SR＋Animator＋Controller)。Controllerはparam無し=状態名Play。
- CharacterVisual.InitGdd(Play(state)橋渡し)＋GddMap.cs(特殊敵6/スポナー4)。
- 特殊敵6種(Koboiled/Phantom/Puppeteer/Rattles/Speckle/Valkyrie): 特殊敵ツールに種類選択ストリップ(SpecialStrip)、selectedSpecialType→Feature.trapKind→z.gddVisualPath。
- スポナー敵4種(Addergul/Deton/Frank/Goop): TickSpawnersでランダム割当。
- ZombieAI.gddVisualPath/Scale(GDD上書き＞獣＞SPUM)。GDD高解像度のためscale~0.5。
- 検証: 特殊敵6種目視OK(適正サイズ/自然発色/HPバー)、error0。スポナーは同一機構でコード検証。

## 隊の個体化/階層別化＋個体進化＋通路バグ修正（2026-07-28）
ユーザー要望4点。設計判断: 1個体=1隊のみ / 個体進化はLv維持・DPのみ / 手動タイル配置は完全無効化。
- 隊を「個体ID」ベース＋階層ごとに(squadByFloor)。SquadAdd(individualId)は他階編成済みなら拒否。CompMultは個体→種類→roleで算出。TryPlaceSquadMemberはスロット=個体を直接配置＋次の未配置へ自動送り。→同一種2枠で同じ個体を二重配置する不具合を解消。
- ボス選択を隊と分離(bossPickIndividualId)。
- 個体進化: MinionRoster.TryEvolveIndividual(直系の子＋研究段階＋DP)でLv・装備を維持したまま上位形態へ。到達形態は図鑑も解禁。従来の召喚型進化も併存。
- 図鑑: 種類カードの＋隊を廃止し「個体」タブに集約(＋隊/外す/進化分岐/装備/所属階)。部隊ストリップ1段化、トレイに「BnFの隊」表示。
- 通路バグ: 初期ツールNone化＋EventSystemでUI越しクリック遮断＋通路/部屋/宝箱ツールを無効化(SetToolMode拒否・else分岐削除)、Escで解除。
- 検証: 階層別編成/二重編成拒否/個体進化(Lv10・銀武器維持)/二重配置拒否/通路ツール拒否/UI目視すべてOK、error0。

## 特殊敵/スポナー敵が動かないバグ修正（2026-07-28）
原因: GDD同梱のAnimatorControllerは全stateのmotionがnull、かつ同梱.animはキーフレームのSprite参照が全てNULL(ベンダー側の破損)。→静止画のまま。
対策: スプライトシート(スライス済み)から**AnimationClipを自前生成**(12fps, idle/run/walkはループ)し、独自Controller(Assets/Resources/GDD/*_Ctrl.controller)を生成してプレハブに割当。10体×5状態(Idle/Run/Walk/Hit/Death)。
検証: 実行時にsprite=Koboiled_run_full-Sheet_1等でアニメ駆動を確認、normalizedTime進行、目視でも歩行動作OK。EnemyGalore側はmotion設定済みで元から正常。

## 魔法/魔物スキル/研究ツリー拡張（2026-07-28 Opus5）
- A 魔法(MagicCatalog): 属性6(火氷雷土光闇)×階級5(最下級〜最上級,威力0.7〜2.8)。状態異常はTrapKindに統一。相性=不死(光1.7/闇0.4)獣(火1.35/雷1.25)魔族(光1.5/火0.75/闇0.55)。眷属術者は研究で属性解禁＋階級上限、冒険者はランクで階級上昇。ZombieAI/AdventurerAI双方に統合。
- B 魔物スキル(MinionSkill): 12種を34形態すべてに1-2個割当。Tier2(威圧/不屈/自爆/石化/治癒/咆哮)は研究m_skill2で解禁。再生/群れ/棘/毒身/俊敏/吸命/自爆/石化/治癒/咆哮/不屈/威圧をZombieAIで実挙動化。
- C 研究ツリー: ResearchField.Magic新設(9ノード)＋m_skill2＋装備鍛造上限(r_grade_mithril/orichal)。18→30ノード。
- D UI: 図鑑カードにスキル/魔法表示、選択中ツールのハイライト＋ホバーツールチップ。
- 検証: 研究ゲート/階級/相性/Tier2解禁/実戦(ゴースト=呪詛+威圧0.8)/図鑑・研究パネル目視すべてOK、error0。
- 次: D武器種別、Eゴエティア72柱。

## D武器種別／Eゴエティア72柱（2026-07-28）
- D: EquipmentCatalog.WeaponType 7種(剣/斧/槍/弓/杖/双剣/鎚)＝攻×・間隔×・射程+。個体に weaponType(召喚時は役割別既定)、無償で巡回切替。ZombieAI.weaponIntervalMult/RangeBonusでStartに反映。UIは種別アイコン＋「種別▶次」＋ツールチップ。
- E: GoetiaCatalog にソロモン72柱を全実装(階級=王/公爵/侯爵/伯爵/君主/総裁/騎士)。個体IDから決定的に割当、ボス任命で名と加護(HP/攻/速)を継承、ログと個体行◈表示、ボスストリップにツールチップ。
- 検証: 弓=射程3.70/間隔1.14、鎚ボス=間隔1.74/攻35.8、72柱・個体#1ベレト〈王〉固定、UI目視OK、error0。

## 魔王の大改修＋感情ツリー刷新（2026-07-28 Opus5）
- ① 魔王の装備: EquipmentCatalog流用(グレード7段×武器種7種)。防具→HP、武器→攻撃、射程は反撃レンジに加算。錬成ランクで鍛造割引＋上限UP。武器種の切替可。
- ② 3段階16種族の進化ツリー(DemonLordRaceTree): 人種→第1(鬼/魔族/エルフ/ドワーフ/スライム/獣)→第2(羅刹/龍/堕天/吸血/妖精/ハイエルフ/巨人/変幻/獣王)。原作準拠の条件(ステ/Lv/配下の使用実績)。各種族に魔法属性＋魔王スキルを付与し、反撃が属性魔法化、再生/棘/不屈を実装。DemonLordVisualに新9種族の見た目。
- ③ 知識/創造/錬成の意味づけ: 知識→研究費-5%/ランク、創造→配下-6%・領域-5%/ランク、錬成→鍛造-8%/ランク＋上限+・戦利品+。魔王パネルに効果を実数表示。
- ④ 感情ツリー刷新: 4ルート×4段(16)＋複合4種(両ルートから半額ずつ)＋研究連携(最終段→RP+1、研究k_emotion→感情+35%、恐怖支配→脅威度上昇×0.7)。UIを全画面ツリー化(研究と統一)。
- 検証: 進化分岐/条件/最終形態、ステ効果の実数、魔王の鍛造・種別切替、感情の解禁・複合・研究連携、UI目視すべてOK、error0。

## セッション区切り（2026-07-28 Opus5）— /compact 対策の記録
このセッションで実装した内容（すべてpush済み・コンパイルerror0）:
1. c4f0ee6 特殊敵/スポナー敵が動かないバグ修正（GDD同梱アニメが破損→自前生成）
2. b449cfc 隊を個体ベース＋階層別に刷新／育てた個体の進化／通路誤配置バグ修正
3. 83dc5ce 魔法システム／魔物スキル／研究ツリー拡張＋ツールUI改善
4. 2450857 D武器種別＋Eゴエティア72柱
5. 266f82c 魔王の大改修（装備／3段階16種族進化／知識・創造・錬成の意味づけ）＋感情ツリー刷新
6. 追跡漏れの.meta追加

★次セッションの着手候補（優先順）:
- (A) 眷属化→地上4X ＝原作最重要の未実装。配下に真名を与えて外に出し、領域を広げる。
- (B) 特殊制限P3（政策カード・スロット0→3・CDO2ショップ）＝内政の残り。
- (C) 魔王スキルの残り（威圧/咆哮/群れ）の実挙動化、アビリティ(冒険者スキル)、伝説武器、等級/危険度表示。
- (D) 通しプレイでのバランス調整（1ゲーム最後まで回して数値を見る）。
※詳細設計と現状は memory: dangeon-3-current-code / demon-lord-emotion-overhaul / magic-skill-systems / codex-research-ui-plan / handoff-status に記録済み。

## 魔王/感情パネルが押せないバグ修正＋個体の経験値制（2026-07-28 Sonnet5）
- **バグ原因**: GameUIManager.Update() が毎フレーム RefreshDemonPanel/RefreshEmotionPanel を呼び、中の子(装備行・進化カード・感情セル)を毎フレーム Destroy→再生成していた。押下中にButtonが破棄されるためクリックが成立しない（見た目だけ正常）。
- **修正**: 「表示が変わる条件」だけを拾った署名(DemonPanelSig / EmotionPanelSig)を比較し、変化した時だけ再構築。感情の所持数は再構築せず RefreshEmotionPools で軽量更新（ルート見出しをキャッシュ）。パネル開閉時は署名をnull化して必ず1回描き直す。※遺物パネルは子を破棄しないので元から無問題。
- **個体の経験値制**: Individual に exp を追加。ExpPerLevel=100 / BattleExp=100（冒険者と戦った階層＝従来どおり1戦+1Lv）/ GarrisonExp=25（冒険者が到達しなかった階層で待機＝1/4）。LevelUp→AddExp に置換。
- DungeonFloorManager に deepestReached を追加し、EndDescent で「到達しなかった階層」のSquad/Bossレコードに待機経験を付与。図鑑の個体行に exp n/100（Lv50はMAX）を表示。
- 検証: Update4連打で子のハッシュ・数が不変（=再構築されない）、進化カード押下で人種→鬼種(第1形態)、感情ノード押下で解禁数1・所持300→280、実戦#1=Lv2/exp0、待機#2=25→50→75→Lv2/exp0。error0。

## 階層拡張の意味づけ＋トーテム13種＋遺物の作り直し（2026-07-28 Opus5）
### ① 領域(Domain)＝階層拡張の見返り
- **診断**: 踏破者は階段へ直行、探索者は「全マップの最大attraction」へ直行するため、広くしても道が長くなるだけ。配置数も無制限で「狭い階に詰め込む」が最適解だった。
- **深さ** → `DungeonFloorManager.DepthRewardMult(i) = 1 + 0.15*i` を撃破DP・素材に乗算。深部で倒すほど旨い＝浅い階で皆殺しにせず深く誘い込む（原作の泳がせ）。
- **広さ** → ①`PlacementCap = 8 + (size-10)/10*4`（10×10=8枠 → 50×50=24枠）＝防衛の器。全TryPlace系に上限判定を追加。②`DomainRenown = Σ(size/10)` で名声。拡張段数に応じてウェーブ増員(+1/2段)と冒険者ランクの上振れ(+0.06/段)。
- **探索AI**: 目標選択を `attraction ÷ (1 + 距離×0.08)` に変更＝近い順に食う。広いほど巡回が長くなり滞在時間が伸びる。
- UI: 領域パネルに各階の「配置枠」「報酬倍率」「拡張で枠+4」と名声サマリ、トップバーに `配置枠 n/m` チップ。

### ② トーテム 13種（TotemCatalog.cs 新規）
- 基礎3(誘惑の灯/戦棍の柱/巌の碑) ＋ 呪詛系3(呪詛の像=冒険者攻-20%/泥濘の碑=移動-25%/恐慌の面=満足+60%で早く帰す) ＋ 家系特化3(屍の祭壇/獣牙の柱/魔導の尖塔=その家系のみ+40%) ＋ 連携4(疾風の風車=攻撃間隔-15%/業火の炉=罠+50%/血の香炉=撃破時の感情+50%/生命の樹=毎秒2%回復)。
- 種類は Feature.trapKind に格納（階層退避/復元にそのまま乗る）。研究 `d_totem_curse`/`d_totem_blood`/`d_totem_ritual` で解禁。トーテム選択ストリップを新設。

### ③ 遺物の作り直し（4種→16種・実績制）
- **診断**: 4種すべて単純な+%、しかも最初から自由に着脱可能＝獲得の喜びも選択の悩みも無い。
- **実績で解放**（配下5体/罠で10体撃破/累計撃破DP3000/家系8体/3層構築/B3F防衛/Aランク撃破/無失点ウェーブ/研究10ノード/鋼以上の鍛造/絶望3段/感情ルート完走/ボス任命）。**スロットは1→2→3**（研究 d_relic2/d_relic3）。
- 他システムと絡む効果へ: 家系特化3種・深淵の鏡(最下層+40%)・深度の王冠(深度倍率+0.1/階)・英雄の首飾り(撃破DP+60%だが脅威度上昇+30%)・静寂の鈴(集客-20%・冒険者HP-15%)・賢者の石(毎ターンRP+2)・錬金の坩堝(鍛造-30%)・呪縛の鎖(状態異常+50%)・収穫の鎌(感情+30%)・魔王の心臓(魔王HP+30%・反撃階級+1)。遺物パネルを全画面4×4グリッド化し、未獲得は淡色＋条件表示。
- 検証: 枠8で9個目を拒否→拡張でsize20/枠12、名声3/ランク+0.06、B2F報酬×1.15、トーテム13種解禁・範囲内0.20/0.40・範囲外0.00、遺物16種/実績で+3解放/スロット1→3/装備効果(攻×1.25・撃破DP×1.60・脅威度成長×1.30)。error0。

## 難易度カーブの根本修正：成長オーダーを揃える（2026-07-28 Opus5）
### 原因（係数の1/4では直らなかった理由）
1. **fame自体が O(turn^2)**。fameは「逃がした人数×35」の累積で、ウェーブ人数が turn に比例して増えるため、fameの増加量そのものが毎ターン増える。これを `fame/40` のように**線形**に使っていたので冒険者Lvが O(turn^2)。
2. **掛け算の軸が多すぎた**。HP = ランク(0.70〜3.30) × Lv倍率 × **脅威度そのもの(最大×6)** × 装備グレード(最大×2.0)。「逃がす」という1つの操作がこの4つ全部を同時に押し上げる＝実質 O(turn^3)。`LureEconomy.HeroHpMult => threat` が最悪の犯人。
3. **人数のオーダー不一致**。攻撃側は `3+turn*2`（T11で25体）で増え続けるのに、防衛側は**配置枠で頭打ち**（10×10で8枠、うち戦力は5-6）。4倍の物量で、個々が弱くても押し切られる。→「急に瞬殺される」の主因はこれ。
4. ランクは `RoundToInt` の**階段関数**なので、閾値を跨いだ瞬間に +28%（0.70→3.30の8段）が一気に乗る。

### 修正
- **fameを対数化**: `renownLog = Log(1 + fame/50)`。fame 120→1.22 / 250→1.79 / 1800→3.64。**fameが倍になるたび一定量だけ増える**＝崖が消え、限界コストが一定になる。
- Lv = `1 + turn*0.8 + renownLog*4`（turn線形＋fame対数）。振れ幅を基準値比（0.70〜1.15倍）にして分散の爆発も止めた。
- 世界水準 = `turn*0.10 + renownLog*0.9 + (脅威度-1)*0.5 + 領域名声`。Lvと同じオーダー。
- **脅威度の直接倍率を大幅に削減**: HP倍率 `=脅威度(最大×6)` → `1+(脅威度-1)*0.15`、攻撃 0.5→0.20。脅威度の役割は「人数・ランク・報酬」に集約。リスクを下げた分 **RevenuePerThreat 0.5→0.7** で旨味は上げた。脅威度上昇量も 0.05→0.03。
- **ランク倍率を圧縮** 0.70〜3.30(4.7倍差) → 0.80〜2.25(2.8倍差)。装備グレードも `rank*0.55+gear/22` → `rank*0.45+gear/35`。高Lvの自動回復も (1+Lv*0.1)*0.5 → (1+Lv*0.04)*0.4。
- **人数** `3+turn*2` → `min(20, 3+turn)`。脅威度の追加も (th-1)*3 → *2。**配置枠の基礎を 8→12** に（罠/トーテムも枠を食うため戦力が残る数に）。

### 結果（総圧力＝人数×HP×攻撃、T1比）
| turn | 旧 | 新 | 防衛の素の伸び(個体Lv) |
|---|---|---|---|
| T5 | ×3.7 | ×5.2 | ×1.16 |
| T11(f250) | ×19.6 | ×18.6 | ×1.40 |
| T20 | ×630 | ×186 | ×1.76 |
| T30 | ×14074 | ×499 | ×2.16 |
序盤はむしろ少し厳しく（余裕すぎる問題の解消）、終盤の爆発を28分の1に。人数は T11 25→14、T30 63→20。
- **UIに「世界水準」チップを追加**（次に来る冒険者のランクと目安Lvを常時表示）＝強くなる前に読めるようにした。
- 検証: T11/fame250 で 世界水準2.71(D)・目安Lv16 → 実際に湧いた冒険者「E級 戦士 Lv.15」。ウェーブ人数 T1=4/T5=8/T11=14/T20=20/T30=20。error0。

## ボス/隊の排他・罠の強化ツリー・マーカーの見た目刷新（2026-07-28 Opus5）
### ① ボスに任命した個体を隊に編成できてしまうバグ
個体の実体は1つなので役割も1つ。双方向で塞いだ。
- `DungeonFeatureManager.BossFloorOfIndividual/IsIndividualBoss` を追加（アクティブ層＋退避済みの他フロアを横断）。`DungeonFloorManager.BossFloorOfIndividual` で他フロア分を検索。
- `SquadAdd` → ボス任命済みなら拒否。`TryPlaceBoss` → 隊に編成済みなら拒否（自動割当も `FirstBossEligibleIndividual` に変更）。
- UI: 図鑑の個体行はボス任命中なら『＋隊』ボタンを出さず「◆ B3F のボス（隊には編成できません）」を表示。ボスストリップは隊所属の個体を淡色＋「B2F隊」表記＋理由ツールチップ。

### ② 罠のバランス（固定罠が腐る／毒が強すぎる）
- **全罠に「対象の最大HP比」成分を追加**（`hpFrac`/`dotHpFrac`）。固定値だけだと冒険者HPの伸びに置いていかれて死に要素だった。HP800の相手でも通常罠が 2.5% → **9%** に。
- **毒の突出を解消**: DoTでは「持続倍率＝総ダメージ倍率」なので、感情「呪縛」×遺物「呪縛の鎖」の掛け算(最大2.25倍)がそのまま効いていた。**加算合成＋上限1.8倍**に変更し、DoTの基礎dpsも下げて瞬間ダメージ側へ重心を移した。→ HP100で 通常30% / 毒28% / 炎39% / 針31% とほぼ横並びに。
- **研究3ノードを新設**（領域研究）: `d_trap_pow1`(+35%) → `d_trap_pow2`(+35%) → `d_trap_pow3`(+40%＋HP比成分1.8倍)。フル investment で ×2.55。HP800相手に通常罠が 35% まで伸び、罠特化ビルドが成立する。

### ③ 配置マーカーの見た目（`MarkerArt.cs` 新規・手続き生成）
64×64のテクスチャを実行時に描いてキャッシュ（外部アセット不要・2×2スーパーサンプリングでアンチエイリアス）。
- **隊/ボス＝四隅のかぎ括弧**（中央を空けるのでキャラを隠さない＝主張控えめ）。ボスのみ小さな王冠を追加。
- **トーテム＝石柱**で、種類ごとの色＋Turbo Diskアイコンを重ねる（13種が一目で区別できる）。
- **スポナー＝渦**（切れ目のある二重リング）、**特殊敵＝菱形の輪**、**階段＝3段＋下向き矢印**（旧: 塗り潰し四角＋「▼」）。
- **隊/ボスのマーカーに個体ラベル**を追加＝「スケルトン #1 Lv8」、ボスは継いだゴエティア名も「◈ベレト」と表示。どの個体を置いたか一目で分かる。
- 色/文字の対応表(`ColorOf`/`LetterOf`)は不要になったので削除。
検証: ボス個体の隊編成/隊員のボス任命を双方向で拒否、罠のダメージ表、5種のトーテムを並べて色とアイコンの差を目視、ボス/隊/階段のマーカーを目視。error0。

## 眷属化 → 地上4X（原作最重要の未実装層）＋ グリフ欠落の根治（2026-07-28 Opus5）
### ① 眷属化（`KinRoster.cs` 新規）
原作の「支配領域を増やすポイントは眷属化」を実装。配下はダンジョンから出られないが、**真名を与えた眷属は配下を率いて地上へ出られる**。
- 条件: **Lv10以上＋進化Ⅰ以上**。DP消費（ティア×45×創造補正）。真名は候補から選択、`↻`で引き直し（個体IDとroll回数から決定的に決まる）。
- **LP(統率力)** = `8 + Lv*0.6 + ランク*2`。配下1体のコストは tierCP。
- **トレードオフ**: 眷属とその配下は**隊にもボスにも置けない**（SquadAdd/TryPlaceBoss/FirstBossEligible の全経路で拒否）＝防衛を削って地上に投資する判断。
- 戦力 = `(14 + tier*9) × Lv倍率 × 装備`、眷属本人は真名の力で×1.6。

### ② 地上マップ（`SurfaceMap.cs` 新規・16領域のノードグラフ）
- 迷宮前(id0)を起点に、**支配領域に隣接した先だけが見える**（探索）。集落/森/鉱山/町/砦/都市で防衛力60→1250。
- 支配すると**毎ターン DP/素材/RP/名声を産出**＝ダンジョン内とは別の収入源。
- **両刃**: 支配が広がるほど `WorldTierBias = min(1.2, log(1+支配数)*0.5)` で世界水準が上がる＝来る冒険者が強くなる。難易度カーブを壊さないよう**対数＋上限**にした（→ difficulty-curve-orders）。

### ③ 侵攻の解決（ターン終了時に自動）
戦力比で4段階: **1.25以上=完勝**（無損害で支配）／**1.0以上=辛勝**（支配・配下1体ロスト）／**0.7以上=敗走**（配下半数ロスト・2ターン負傷）／**0.7未満=壊滅**（配下全ロスト・4ターン負傷）。
**失った配下個体は `MinionRoster.Remove` でロスターから完全に消える**＝育てたものを賭ける重み。負傷中は進軍指示不可。

### ④ UI
- 上部バーに「地上」ボタン → 全画面パネル。左=眷属（真名/統率LP/戦力/状態/配下チップ/＋連れて行く/進軍中止/真名を返上）、右=領域（防衛力・産出・前回戦果・**完勝圏/辛勝圏/敗走の恐れ/壊滅の恐れ**の事前表示＋進軍ボタン）。
- 図鑑の個体行に「眷属化：〈候補名〉」＋`↻`。眷属/配下は所属表示が変わり、隊の操作は出さない。

### ⑤ グリフ欠落の根治（UIの□問題）
`HasCharacter` で実測したところ、**UIフォントに存在するのは ◆ □ → ・ … ― ＋ × 『 』 程度**しかなく、**◇ ◈ ○ ● △ ▲ ▽ ▼ ■ ☆ ★ ※ ← ↑ ↓ ▶ 【】「」 は全て欠落**していた（memoryの「◆◇―▲◈★は使える」という記述は誤りだった）。
- `GameUIManager.Fix()` を追加し、`Text()` 生成と `SetTxt()` の一箇所でサニタイズ（置換表＋フォントに無い記号帯/絵文字は除去）。既存の `.text =` も `SetTxt` に置換。
- 併せてソース全体の記号を一括置換（547箇所）。
- **教訓**: この一括置換で `GlyphMap` の**キー自身が書き換わってキー重複→UI生成が丸ごと落ちた**。置換表は `\uXXXX` エスケープで書くようコメント付きで修正済み。

検証: 眷属化の条件判定（Lv1は拒否/Lv16で可）、LP編成、隊/ボスとの相互排他、完勝→支配→隣接解禁、壊滅→配下4体ロスト＆ロスター5→1＆4ターン負傷、産出の反映(+1000DP/+75素材/+25RP)、WorldTierBiasの上限1.2、地上パネルと図鑑の目視。error0。

## 操作性：スクロールが効かない問題の根治＋ボスストリップの横スクロール（2026-07-28 Opus5）
### 原因（1つだった）
`AddTooltip` が `UnityEngine.EventSystems.EventTrigger` を使っていた。**EventTrigger は IPointerEnter/Exit だけでなく IScrollHandler / IBeginDragHandler / IDragHandler / IEndDragHandler も実装している**。uGUIのイベントは「その interface を実装した最初の祖先」で止まるので、**ツールチップを付けた要素の上ではホイールもドラッグも EventTrigger に吸われ、親の ScrollRect に一切届かなくなっていた**。
＝「選択できるところにマウスがあるとスライドできない」の正体。図鑑の個体タブだけでなく、遺物・研究・感情・領域・地上など**ツールチップを付けた全パネルで同じ症状**が出ていた。

### 修正
- **`UITooltipTrigger.cs` を新設**（IPointerEnterHandler / IPointerExitHandler **のみ**実装）。`AddTooltip` はこれを使う。スクロールもドラッグも実装していないので、そのまま親の ScrollRect へバブリングする。
- 総点検: スクロール領域内の**当たり判定つき要素 1760個すべて**でスクロールが ScrollRect に到達することを確認（塞いでいる要素0）。シーン内の EventTrigger も0。

### ボスストリップの横スクロール
所持個体が増えると画面外に見切れて選べなくなっていた（唯一、項目数が所持数で伸びるストリップ）。
- `MakeHScroll()`（横スクロール領域のヘルパー。`MakeVScroll` と対称）を追加し、ボスストリップを **固定の見出し＋横スクロールする個体リスト** に作り替えた。見出しに所持数と「横にスクロールできます」を表示。
- ついでに**地上に出ている個体（眷属/その配下）も淡色＋『地上』表記**にして、ボスに任命できない理由が分かるようにした。
- 他のストリップ（部隊5/罠6/トーテム13/特殊敵6）は項目数が固定で、実測でキャンバス幅1920に収まることを確認したのでそのまま。

検証: ボスストリップ 個体20体で content幅2688 > viewport幅1184 で横スクロール可、カード上のホイール/ドラッグの受け手が Viewport(ScrollRect)、図鑑の個体行のボタン121個すべてでスクロールが到達、ツールチップは従来どおり表示/非表示。error0。

## 他魔王領（eXterminate）＋領域の逆襲（2026-07-28 Opus5）
### ① 他魔王 3人（`RivalLords.cs` 新規）
原作『1都市に約60人の魔王が居て互いに真核を奪い合う』を3人に凝縮。
- **鬼種のカンタ**(力240/成長20)・**妖精種のアリサ**(400/28)・**龍種のヴェルグ**(680/38)。それぞれ**真核のある本拠地**を持つ（領域を16-18に追加：紅蓮の坑洞 / 常夜の樹海 / 凍てつく王座、防衛700/980/1400）。
- 毎ターン成長し、**隣接する一番手薄な領域**を取る。プレイヤー領は×0.85で評価＝**優先的に狙ってくる**。
- **本拠地を落とすと真核を奪える**＝その魔王を排除。保有領域は中立に戻り、戦利品（DP=力×3／素材／RP）が入る。遺物**『簒奪の真核』**（全防衛体+30%）も解放。
- 原作準拠：真核を奪えるのは魔王だけ。人間側は領域を**奪還**するだけ。

### ② 領域の逆襲
- `SurfaceMap.Region` に**所有者**（中立/自分/他魔王）を導入（`owned` は `owner==Self` の派生に変更）。
- **人間側の奪還軍**：`90×世界水準 + log(1+fame/50)×60`。中立に隣接する自領のうち一番手薄なところを毎ターン狙う。序盤は発生しない。
- **守る手段は2つ**：🏯 **砦化**（Lv1-3で防衛+120/300/560、DPで購入。奪われるとリセット）と 🛡️ **眷属の駐留**（部隊戦力×1.25。地の利で守りのほうが有利）。
- 自領の守り = `素の防衛×0.35 + 砦 + 駐留`。**領域を奪われると駐留していた眷属は敗走**（配下半数ロスト・2ターン負傷・迷宮前まで後退）。
- ターン解決順：**①自軍の侵攻 → ②他魔王の行動 → ③人間の奪還軍 → ④産出**（奪われた領域の産出は入らない）。

### ③ バランス調整（重要）
初期値（成長42-80・aggression最大2）だと**3ターンで盤面を7領域食い尽くし**、プレイヤーの拡張先が消えた。→
- **猶予4ターン**（原作の『擬似的平和』）は他魔王が動かない。
- **5領域持ったら固めに入る**（それ以上は広げない）＝盤面を食い尽くさせない。
- 成長を半減、侵攻後の消耗を×0.9→×0.75。
- 結果：T8で 3領/5領/1領 に落ち着き、**中立9領域がプレイヤーの取り分として残る**（20ターン放置しても変わらない）。

### ④ UI
地上パネルに**他魔王の状況行**（存命数・各魔王の軍事力と領域数・排除済み表示）。領域行に**所有者タグ**（色分け）・**◆真核**マーク・現在の守り（砦Lv/駐留数）・**砦化ボタン**・**守るボタン**（選択中の眷属を駐留させる）。

検証: 領域19/他魔王3、砦化で守り21→141→321、駐留で321→1228、他魔王領への進軍→本拠地陥落で真核奪取（カンタ排除・+780DP+30素材+8RP・遺物2種解放）、他魔王の伸長と**自領強奪**、人間の奪還軍が駐留ありでは撃退され駐留を外すと奪還、奪われた眷属の敗走（配下5→3・2ターン負傷・迷宮前へ後退）、20ターン放置での盤面推移。error0。

## 地上をCiv化：ヘクス盤＋地形/資源＋施設(隣接ボーナス)＋天啓(Eureka)（2026-07-28 Opus5）
### 調査（Civ VI / VII）
- **Civ VI**: 地区は1ヘクスを占有（都市のアンスタック）。**隣接ボーナス major+2 / standard+1 / minor+0.5（合計後に切り捨て）**。キャンパス=山+1・森/他地区+0.5、聖地=自然遺産+2・山+1、商業ハブ=川+2・港+2。技術(科学)＋社会制度(文化)の2本立てで、各ノードに **Eureka/霊感**（テーマに沿った行動でコストの約40%が即入る＝learn-by-doing）。地区コストは建造数で上昇し**一番建てていない地区は40%引き**。
- **Civ VII**: 3つの時代／**Triumph**（6属性に紐づく任意の挑戦・小=即時報酬/大=次の時代へのDedication）／時代替わりで首都以外は町に戻る／**司令官はレベルと属性を時代を越えて引き継ぐ**。
- 出典: civilization.fandom.com（Age/District/Adjacency）, civfanatics.com, gamerant, 2k公式。

### ① ヘクス盤（`SurfaceMap` を全面改修）
- **axial座標の半径2ヘクス＝1+6+12＝ちょうど19タイル**で、既存19領域と一致。1領域=1ヘクスに乗せ替えた。
- **隣接(links)はヘクスの6方向から自動導出**（手書きの隣接表を廃止）。盤を組み替えても追従する。
- 各ヘクスに**地形**(荒地/平地/森/丘陵/山岳/湿地)・**川**・**自然の驚異**・**資源**(鉄/魔石/穀物/家畜/宝石/良材)。
- UIは全画面パネル内に**六角形スプライトをグリッド配置**（`MarkerArt.Hexagon()/HexRing()` を手続き生成）。地形色で塗り、**所有者の色で縁取り**、選択中は金枠。未到達は「?」。名前/所有者/守り/資源/川/驚異/砦/施設/駐留/進軍先を1ヘクスに集約。

### ② 施設＝Civの地区（`DistrictCatalog.cs` 新規）
| 施設 | 産出 | 主な隣接源 |
|---|---|---|
| 魔泉 | 研究点 | 山岳+2 / 魔石+2 / 森+1 |
| 祭壇 | 感情 | 自然の驚異+2 / 森+1 / 湿地+1 |
| 交易所 | DP | 川+2 / 宝石+2 / 穀物・家畜+1 |
| 鉱錬所 | 素材 | 山岳+2 / 鉄+2 / 丘陵+1 / 良材+1 |
| 兵舎 | 領域防衛 | 丘陵+2 / 山岳+1 / 砦Lv |
- **1ヘクス1施設**。**隣の施設は minor(+0.5)**＝まとめて置く動機（Civと同じ）。**一番建てていない種類は40%引き**。
- **産出は全部ダンジョン側の資源に流れ込む**（DP/素材/研究点/感情/防衛）＝地上を耕すことがそのまま迷宮の強化になる。
- UIで**建てる前に「ここに建てると+N」と内訳**を出す（Civの配置レンズ相当）。

### ③ 天啓＝Eureka（`EurekaTracker.cs` 新規）
- 研究に進捗の概念が無いので、**条件達成でそのノードが40%引き**という形で実装。**全46ノードに条件**を付けた。
- **ダンジョンでの行動が地上/研究を進める**：罠で倒す→罠研究、魔法で倒す→魔法研究、個体を育てる→魔物研究、鍛造→錬成研究、感情消費→魔王研究、領域支配/施設建設→地上研究。これが2層を噛み合わせる要。
- 研究ツリーのノードに「天啓: 罠で5体倒す」を常時表示し、達成すると金色で「◆天啓達成 40%引き」。

### ④ 地上研究（新分野・7ノード）
開拓の礎(交易所/鉱錬所) → 祈りと探求(魔泉/祭壇) / 軍事拠点(兵舎) / 斥候(2つ先まで見える) / 兵站(全眷属のLP+6) / 拠点化(領域産出+25%) / 簒奪の作法(他魔王領への侵攻+20%)。研究は31→**46ノード**に。

### ⑤ バランス調整（実測して2回直した）
- 当初は**自タイルも隣接に数えていた**ため値が跳ね上がり（鉱錬所+8）、施設が一瞬で元を取った → **Civ同様に隣接6タイルのみ**に修正。
- 交易所が**平地+1**を拾って**どこに建てても+8**になり「置く場所を選ぶ」というCivの肝が消えていた → ありふれた地形を数えるのをやめ、川/宝石/穀物・家畜のみに。
- 換算レートを RP=ceil(v/2) / 感情=v×2 / DP=v×14 / 素材=ceil(v/2) / 防衛=v×35 にして、1施設が4-6ターンで元を取る水準に。

### ⑥ 眷属化UIの明示化（要望）
条件を全部満たすまでボタン自体が現れず「どうすれば出るのか」が分からなかった → **常に欄を出し、条件をチェックリスト表示**（満たした項目は緑の◆、未達は灰の・）。`KinRoster.NameRequirements()` が Lv/進化段階/隊・ボスの就任/DP を1つずつ返す。ボタンは未達なら押せないが**存在は見える**。

検証: 19タイル(環1=6/環2=12)・中心の隣接6・隣接がヘクスから導出、施設の隣接が場所で変わる(魔泉 祈りの丘+7 vs 灰かぶり+1、鉱錬所 麦守りの里+12)、40%引きの表示、施設産出、天啓の達成と40%引き(素6RP→4RP)、眷属化チェックリストの4パターン、ヘクス盤の目視。error0。

## 迷宮タイプ／空間タイプに実効果＋宝箱を面積基準に（2026-07-28 Opus5）
### ① 迷宮タイプ・空間タイプ（`DungeonTheme.cs` 新規）
これまで**BSPの分割パラメータと色味しか変えておらず、選ぶ理由が無かった**。Civの地形選択のように「得と損がセット」になるよう実効果を割り当て、各システムはこのクラスのgetterを掛けるだけにした。
| 迷宮タイプ | 得 | 損 |
|---|---|---|
| 標準 | 配置枠+2 | ― |
| 迷路 | 冒険者の満足閾値+35%（＝長居する＝罠が効く） | 宝箱-25% |
| 大空洞 | 部隊バフ+10%・防衛体の徘徊+1 | 集客-15% |
| 蟻の巣 | 宝箱+50%・集客+20% | トーテム半径-1 |

| 空間タイプ | 効果 |
|---|---|
| 洞窟 | 不死系+15% ／ 冒険者の与ダメ-5% |
| 遺跡 | 宝箱の価値+30%（集客も上がる） ／ 罠の再作動が遅い |
| 城砦 | 防衛体HP+15% ／ 集客-10% |
| 溶岩 | 火+25% ／ 氷-20% |
| 氷雪 | 冒険者の移動-15% ／ 獣系-10% |
配線先: 配置枠(DungeonFloorManager) / 満足閾値・与ダメ・移動(AdventurerAI) / ウェーブ人数(Spawner) / 部隊コンプ・徘徊・トーテム半径・家系倍率(DungeonFeatureManager) / 宝箱の価値(RoomData) / 宝箱数(DungeonGenerator)。

### ② 宝箱の密度を面積基準に
**数を「部屋数(BSPの葉)×比率」で決めていた**ため、階層を広げても部屋が大きくなるだけで数が増えず、広い階層ほどスカスカに見えていた。→ **`coef × size`（少0.28/中0.52/多0.80）** に変更。候補はRoomセル単位なので大部屋には自然に複数入る。
| 広さ | 少 | 中 | 多 |
|---|---|---|---|
| 10×10 | 3 | 5 | 8 |
| 30×30 | 8 | 16 | 24 |
| 50×50 | 14 | 26 | 40 |
（旧方式では50×50・中でも4-8個程度だった）

### ③ 生成パネルの表示
迷宮タイプのカードを「形の説明」から**「得と損」**に差し替え、空間タイプは選択中の効果を1行で明示＋各チップにツールチップ。宝箱ラベルも「階層の広さに比例して増えます」に。

### ④ 地上の大改修は設計モックを作成済み
ユーザー要望でCiv 6/7を追加調査し、**触れるモック**をArtifactで作成（厚みのあるヘクス盤・遺産・人口/働くタイル・地上ツリー・迷宮タイプ表）。方針が承認済み: 盤の見た目はモックのまま／人口と働くタイルも入れる／実装順は「迷宮タイプ+宝箱 → 盤+遺産+ツリー」。**次はこの続き（盤の刷新）**。

## 地上をCiv化 第2弾：厚みのある37タイル盤／遺産／人口／地上ツリー（2026-07-28 Opus5）
承認済みモック（Artifact）どおりに実装。

### ① 盤を半径3（37タイル）へ
`1+6+12+18=37`。既存19（環0-2）はそのまま、**環3の18タイルを追加**（塩の平原〜星降りの丘）。人間側の本国が外周に並ぶ。隣接はヘクスから自動導出なのでリンクの手当ては不要。

### ② 厚みのあるヘクス（2Dのまま奥行き）
各ヘクスを**天面＋側面の2枚**で描く（同じ六角形スプライトを下にずらして暗く塗る）。**縦を0.76に圧縮**して俯瞰にし、**地形ごとに高さ**（山岳22 / 丘陵12 / 森8 / 平地3 / 湿地1）。奥（rが小さい）から描く画家のアルゴリズムで前後関係が出る。

### ③ 地上モードで迷宮を畳む
「地上」ボタンで**カメラのcullingMaskをUIレイヤーのみに落とし**、下部ツールバーとフロアタブを隠す。「× 迷宮へ戻る」で復帰。
**ハマった点**: 最初 `cullingMask = 0` にしたら**画面が真っ黒**になった。Canvasが Screen Space-Camera の場合はUIごと消える（今回はOverlayだったが、カメラ描画のスクリーンショットでは何も映らなくなる）。**UIレイヤーだけは残す**のが正解。

### ④ 遺産（`WonderCatalog.cs` 新規・8種）
盤の生成時に**外周寄り（環2以降）へ2〜4個をランダム配置**。種類も重複しない。遺産タイルは防衛が固くなる（+200〜320）。効果はすべて迷宮側に返る：竜骨の尖塔(全眷属の統率+10) / 星詠みの環(毎ターンRP+4) / 嘆きの大樹(感情+25%) / 不落の城壁(自領すべての守り+120) / 黄金の秤(領域DP+40%) / 巨人の鉄床(毎ターン素材+6) / 囁きの迷路(罠+35%) / 賢者の炉(鍛造費-35%)。

### ⑤ 人口と働くタイル（Civの都市成長）
- 領域に**人口**(1-6)と**食料**。食料＝耕作タイルの合計−人口。**人口のぶんだけ隣接タイルを「使う」**（食料の高い順に自動選択）＝Civの市民配置。
- **統治力** = 2 + 砦Lv + (兵舎なら+2) + (研究『統治の理』+2)。**人口が統治力を超えると不穏＝産出半減**。
- **住居上限**: 人口は統治力+1で頭打ち。→ 際限なく増えて永久に不穏になる事故を防ぎ、砦/兵舎/研究で統治力を上げる動機になる（Civの住居に相当）。
- 人口は領域の産出と施設の産出の**両方に倍率**として掛かる。

### ⑥ 地上専用ツリータブ
Civの技術/社会制度の二本立てに倣い、**地上研究を研究パネルから外して「地上」パネル内のタブへ**（盤／地上ツリー）。8ノード（開拓の礎・祈りと探求・軍事拠点・斥候・兵站・拠点化・**統治の理**・簒奪の作法）。各ノードに天啓を表示。

検証: 37タイル(環1=6/環2=12/環3=18)、遺産3個がランダム生成され防衛が上がる、人口が食料で増え統治力+1で頭打ち・超過で産出0.65倍、耕作タイルが食料順に選ばれる、施設産出に人口倍率、地上モードで迷宮が消えツールバーが隠れる、ツリータブの切替。error0。

## Civ VII 公式資料の読み込みと適用計画（2026-07-28 Opus5）
### 読んだもの
公式ゲームガイド（map-generation / developing-settlements / improved-naval-combat / victories / triumphs / time-tested-civs / glossary）、パッチノート 1.4.0「Test of Time」と 1.2.5、7 Things to Know。薄い箇所は well-of-souls の解析ページと各種ガイドで補完。※YouTube 2本は音声/映像のため内容取得不可。

### 要点
- **マップ**: Tiny 60×38=**2,280**/Small 74×46=3,404/Standard 84×54=**4,536**タイル。1.2.5から**ボロノイ図でプレートを模擬**（点を撒く→プレート成長→解像度を上げて陸塊→島・浸食・山・火山→ヘクス割当）。直線的な海岸線を排し**95%は"普通"**になるよう調整。1.4.0で Fractal Continents 追加、島の定義 15→**30タイル**。
- **Triumph（1.4.0でLegacy Pathを完全に置換）**: 小＝即時報酬、大＝次代への**Dedication**（3枚選択）。**100種以上**＋プリセット。**災厄中のみ出現するTriumph**あり。
- **勝利**: 4種すべてスコア制。**2位の6→4→3→2→1.5→1.25倍**と閾値が下がり**5ターン保持**で勝利。科学のみ**革新100＋発射台**。決着しなければ総合スコア。
- **拠点/都市**: 新設はTown（生産キュー無し・生産は自動でGold化）→Goldで昇格。**Town Focus 9種**。**支配上限超過で全設定に-5 Happiness**。
- **不満**: 1.4.0で**1点につき産出-5%（最大-80%）**、上限超過分は青天井。
- **人口**: Builder廃止。**人口を割り当てたタイルだけが産出**。**専門家は隣接ボーナスの100%**を追加（1.4.0で50%→100%）、維持費は時代で2/4/6。**市街に建物2つでQuarter**。**倉庫**＝同種改良の数だけボーナス。
- 他: Influence(外交通貨)、独立勢力→City-State従属、**Commanderは昇進が時代を越えて持ち越す**、Distant Lands(別半球・古代は到達不可)、Crisis＋**全員が負の政策を選ぶ**Crisis Policy、1000超の**2-3択の物語事件**、**Memento**(周回持ち越し2枠)。

### 適用計画（C1〜C7）
| | フェーズ | 内容 |
|---|---|---|
| **C1** | **広大な盤と手続き生成** | 半径5(91)/7(169)/9(271)の選択制。プレート→陸海→浸食→山→バイオーム→資源の手続き生成。スクロール＋ズーム。海/沿岸/**外洋**（渡航研究が要る）。自然の驚異を固有名＋効果に格上げ |
| **C2** | 拠点と都市・人口 | Town/City昇格、特化9種、支配上限、不満(-5%/点・最大-80%)、祝祭、市街/田園、街区、倉庫、専門家(隣接100%) |
| **C3** | 時代・偉業・誓約・災厄 | 3時代＋進行度、偉業(小/大)、誓約3枚、時代末の災厄＝全員が負の政策 |
| **C4** | 勝利条件 | 制圧/恐怖/経済/革新のスコア制、2位比＋5ターン保持、総合スコア |
| **C5** | 外交・独立勢力・交易 | 威名(Influence)、独立勢力の従属、交易路、戦争支持/厭戦 |
| **C6** | 眷属＝指揮官 | 昇進ツリー、ZoC/側面/包囲/壁、遠き地への渡航 |
| **C7** | 物語事件・形見 | 2-3択の事件、周回持ち越しの形見 |

**承認**: C1から着手／盤は**小91・中169・大271の選択制**。
**全フェーズ共通の制約**: 強化パラメータを足すたびに掛け算の軸が増えるので、追加のたびに実測で確認する（Civ自身も不満に-80%の上限を設けている）。→ [[difficulty-curve-orders]]

## C1: 地上盤の手続き生成と大型化（2026-07-29 Opus5）
### ① `SurfaceGen.cs` 新規 ― Civ VII 1.2.5 のボロノイ/プレート方式を模した生成
Civの手順どおり **①点を撒いてプレートを模擬 → ②BFSでプレートを成長（＝ボロノイ） → ③プレート単位で陸/海を決める → ④浸食（出っ張りを削り内海を埋める＝直線的な海岸線を消す） → ⑤プレート境界に山脈・火山 → ⑥バイオーム/川/資源 → ⑦自然の驚異** の順で作る。
- **盤の大きさは選択制**：小 半径5=**91** / 中 半径7=**169** / 大 半径9=**271** タイル（`3R(R+1)+1`）。生成パネルにボタンを追加。
- 中心(0,0)は必ず陸＝迷宮入口。その隣接6タイルも必ず陸にして初手で詰まないようにした。外周は75%の確率で海に寄せ、盤が海で閉じるようにした。
- **川は山から海へ下る筋**として引く。資源は地形に合ったものだけ（山＝魔石/宝石、丘＝鉄、森＝良材、平地＝穀物/家畜）。
- **自然の驚異7種を固有名＋効果に格上げ**（虚ろの大穴/燃える湖/千年樹/霜の女王像/囁く石柱群/血染めの滝/天泣の谷）。盤の奥まったところに2〜7個。
- 地名は接頭辞38種×地形別の接尾辞から生成するので、毎回違う地名になる。

### ② 海と『遠き地』
`Terrain.Ocean` を追加。海は**支配できず陸路も通らない**ので、海で隔てられた陸は到達不能になる＝Civの Distant Lands。地上研究 **`s_voyage` 渡航術**（天啓＝海に面した領域を2つ支配）で**海を1マス越えた先へ進軍できる**ようになる。

### ③ パン／ズーム（`HexMapPanZoom.cs` 新規）
169〜271タイルは1画面に収まらないので、**掴んで動かす＋ホイールで寄る**を実装。`IDragHandler`/`IScrollHandler` **だけ**を実装しているのでタイルのボタンからイベントが上がってくる（EventTriggerを使わない理由は既知）。**ドラッグした指を離したときはタイルを選択しない**ようにガードした。タイルの大きさは盤の半径から逆算して自動で決まる。

### ④ 他魔王の本拠地も手続き配置
中心から遠く、かつ互いに離れた**陸**タイル3箇所を選ぶ。
**ハマった点**: `RivalLords.Build()` が旧仕様の固定ID `{16,17,18}` で `AssignRivalHome` を呼んでおり、**生成された盤の海タイルに本拠地が乗った**（防0の海溝が本拠地になった）。本拠地の決定は盤の生成側に一本化し、`RivalLords.HomeOf(i)` は `SurfaceMap` から引くようにした。

検証: 小91/中169/大271がタイル数どおり生成、陸/海/沿岸/山/川/驚異/遺産/資源がすべて分布、3種のseedで他魔王の本拠地が全部陸、中心の大陸から歩ける陸の数を確認（海で隔てられた分が『遠き地』になる）、盤の目視。error0。

### 次（C2）
拠点と都市（Town/City昇格・特化9種）、支配上限、不満(-5%/点・最大-80%)、祝祭、市街/田園の分離、街区、倉庫、専門家(隣接100%)。

## セッション記録（2026-07-29・/compact 前）
### このセッションで入ったもの（コミット順）
1. **他魔王領(eXterminate)と領域の逆襲** — 他魔王3人・真核の奪取・人間の奪還軍・砦化・駐留
2. **UIのスクロールが効かない問題を根治** — 原因は `AddTooltip` の `EventTrigger`（IScroll/IDragも実装しており親のScrollRectに届かない）。`UITooltipTrigger` を新設。ボスストリップを横スクロール化
3. **地上をCiv化** — ヘクス盤／施設5種と隣接ボーナス／天啓(Eureka)全ノード／地上研究／眷属化UIの明示化
4. **迷宮タイプ/空間タイプに実効果** ＋ **宝箱を面積基準に**（50×50・中で4-8個→26個）
5. **地上をCiv化 第2弾** — 37タイル／厚みのあるヘクス／地上モードで迷宮を畳む／遺産8種／人口と耕作タイル／地上ツリータブ
6. **C1: 地上盤を手続き生成にして大型化** — 小91/中169/大271、ボロノイ/プレート生成、海と遠き地、パン/ズーム、自然の驚異7種

### 次にやること
**[[civ7-roadmap]] の C2**：拠点と都市（Town/City昇格・Town Focus 9種）、支配上限、不満(-5%/点・最大-80%)、祝祭、市街/田園の分離、街区(Quarter)、倉庫、専門家(隣接ボーナス100%)。
その後 C3(時代・偉業・誓約・災厄) → C4(勝利条件) → C5(外交・独立勢力・交易) → C6(眷属＝指揮官) → C7(物語事件・形見)。

### 会話にしか無かった重要事項（memoryに転記済み）
- **EventTrigger はスクロールを食う**（IScrollHandler/IDragHandlerを実装しているため）。ホバー用途は `UITooltipTrigger`（Pointer系のみ）を使う。
- **`cam.cullingMask = 0` にすると画面が真っ黒**になる（UIレイヤーごと落ちる）。UIレイヤーは残す。
- **`MakeVScroll` の Content は横ストレッチ**なので `rect.width` を幅計算に使ってはいけない。ビルド時の実効幅をフィールドに保持する。
- **盤を手続き生成にしたら、盤の要素を指す固定IDは全部消す**。`RivalLords` の固定ID `{16,17,18}` が残っていて他魔王の本拠地が海タイルに乗った。
- **UIフォントに無い記号**は `GameUIManager.Fix()`/`SetTxt()` でサニタイズ済み。置換表 `GlyphMap` のキーは **`\uXXXX` エスケープ**で書く（生の記号だと一括置換で表自身が壊れる）。
- **Play直後はフレームが進まない**ことがある → `manage_camera screenshot` を挟む。**静的Instanceがstale** → stop→play し直す。
- 設計モック(Artifact): https://claude.ai/code/artifact/bfc23a24-32c2-42df-8218-7b752c0571ba

## C2: 拠点と都市（Civ VII の Settlement 系）（2026-07-29 Opus5）
`SettlementSystem.cs` 新規。C1までは**支配したヘクスが全部「人口を持つ都市」**で、271タイル盤だと拠点が数十個になり、Civの「少数の拠点が周囲を耕す」形になっていなかった。ここを作り直した。

### ① 拠点(Town) / 都市(City) / 版図 の3層
- 支配した領域は既定で**版図**（最寄りの拠点の領土）。人口も施設も持たない。
- DPで**拠点(Town)** を築く（220+120×拠点数）。拠点は生産キューを持たず、代わりに**特化を1つ**選ぶ。
- さらにDPで**都市(City)** へ昇格（480+320×都市数・『都市法』で-25%／人口2以上が要る）。**都市だけが施設を建てられ、専門家を置け、版図が広い**。
- 版図は全拠点から同時にBFS。半径は 拠点1／都市2／人口4以上で+1（最大3）＝**人口が育つと国境が広がる**。
- 迷宮前の中心タイルは最初から**首都(City)**。
- **どの拠点からも届かない自領は『未編入の辺境』**で、DP/素材/RPを産まない（名声だけ入る）。拠点を築く動機そのもの。

### ② 拠点の特化 9種（Town Focus）
成長(食料+50%)／農耕(耕作タイルの食料+1)／鉱山(素材+2＋丘陵山岳ごと+1)／交易前哨(DP+18・幸福+2)／中枢(RP+2＋版図の施設ごと+1)／砦(守り+120・統治力+1)／供犠(感情+6＋祭壇で+4)／中継(隣接自領ごとに名声+1)／工廠(素材+3・鍛造費-5%／最大-25%)。

### ③ 幸福と不満 ― **C1までの「不穏＝産出×0.5」の崖を撤去**
**純不満1点につき産出-5%、最大-80%**（Civ VII 1.4.0そのまま）。不満＝人口過密／**支配上限の超過**／専門家の維持／敵魔王領に接する。幸福＝施設・遺産・資源(最大3)・砦・都市・交易前哨・拠点化。段階関数が消えたので[[difficulty-curve-orders]]の方針と整合する。

### ④ 支配上限・祝祭・街区・倉庫・専門家
- **支配上限**＝3＋拠点化＋統治の理＋都市法(+2)。超過1つにつき**全拠点に不満+1**。
- **祝祭**＝幸福の余剰が貯まるとNターン産出+15%。
- **街区(Quarter)**＝『都市法』解禁。同じタイルに2つ目の施設を重ねると**両方に+2**（費用1.5倍）。
- **倉庫**＝新施設。隣接ではなく**所属する都市の版図にある資源タイルの数**で伸び、素材と食料を産む。
- **専門家**＝都市の施設タイルに1人。**その施設の隣接ボーナスが2倍**（Civ VII 1.4.0の100%）。維持費 食料2＋不満1、枠は人口÷2。
- 地上研究に3ノード追加（**都市法／倉庫術／専門家の登用**）＝地上ツリーは9→**12ノード**。

### 実測して直したこと
- **人口が14ターン増えなかった**：首都が荒地(食料0)に立ち、pop1では自タイルしか耕さないので食料が永久にマイナス。→ **拠点タイルは基礎食料3**（Civの都市中心に相当）。
- **祝祭が4/6ターン＝ほぼ常時**（実質「産出+25%の常時バフ」）。→ 必要量を`20+4×人口`、倍率を1.15に。逆に`16+10×人口`にすると幸福の余剰は人口では増えないので**一度も起きなくなった**。7〜9ターンに1回に落ち着かせた。
- **首都の版図が1タイルまで削られた**：同距離のタイルは種の並び順で決まるので、あとから近くに拠点を建てるだけで首都が負ける。→ 種を**都市→人口の多い順**に並べ、最小拠点間距離を2→**3**に。
- **ヘクスの中の文字が下のタイルに落ちる**：ヘクスの幅は実測**約57px**しかない。人口をバッジに畳んで「都14 都2」の形にし、行間を12/11/11に詰め、名前は**折り返しを残したまま自動縮小**（`wrapping=false`＋autoSizeだと縮まずに横へはみ出す）。
- `RegionType.Gate` の表示名を「拠点」→**「迷宮前」**に改名（『拠点』がTownを指す語になったため）。

検証: 版図のBFS／未編入の産出0／拠点を築く・都市へ昇格・上限超過で不満+2／Townでは施設不可・都市の版図では可／街区で隣接10→12／専門家で隣接2倍／倉庫が版図の資源数で伸びる／特化9種すべての産出／不満27で倍率0.20にクランプ／領域を奪われると拠点が消える／天啓3種／盤とパネルの目視。error0。

### 次（C3）
時代・偉業(Triumph)・誓約(Dedication)・災厄(Crisis)。

## W1: 地上をCiv規模へ ― 土台（座標系・O(1)・モードの完全畳み）（2026-07-29 Opus5）
「271タイルでもCivに比べたら小さい。1万タイル級にしたい」という要望を受けて計測したところ、**詰まっていたのは描画方式だけ**だった。

### 実測（ボトルネックの特定）
`SurfaceGen.Size` は半径をそのまま持つenumだったので `(Size)58` にキャストして1万タイル盤を実際に作って測った。

| | 271 | 4,447 | 10,267 |
|---|---|---|---|
| 盤の生成 | 5ms | 386ms | **1,908ms** |
| 産出の集計 | 0ms | 5ms | 0ms |
| ターン処理(他魔王＋奪還軍) | 0ms | – | 17ms |
| 全タイルの視界判定 | 0ms | – | 0ms |
| **盤の描画(uGUI)** | **61ms** | ≈1,000ms | **≈2,300ms** |
| **1タイルあたりGameObject** | **16個** | – | **≈16万個** |

→ **データ層は既に1万タイルに耐える**。uGUIが1タイル16GameObject(Graphic13/TMP3.5)を作り、しかもクリックのたびに全部Destroyして作り直すのが唯一の壁。

### ① 盤をCiv式（幅W×高さHの長方形＋東西ループ）へ ― `HexGrid.cs` 新規
- **odd-r offset（pointy-top）**＝Unityの Hexagonal Point Top Tilemap がそのまま使う座標系。W2でTilemapへ移すとき変換が要らない。
- **東の端と西の端がつながる**（南北の端だけ極地で閉じる）＝Civと同じ。距離は東回り/そのまま/西回りの3通りから最短を採る。
- 盤の大きさは **試作 20×14=280 / 小 60×38=2,280 / 中 84×54=4,536 / 大 106×66=6,996**（Civ VII Tiny〜Civ VI Huge の実寸）。
- **⚠ 防衛/産出は「入口からの距離」で伸ばすが、盤が広いと距離の最大値が変わって青天井になる** → `depth`(0〜9に正規化)を経由させ、盤の大きさによらず1タイルあたりの強さを一定にした。
- バイオームを**緯度**で変える（極寄り=荒地/丘、赤道寄り=森/湿地）。

### ② 近傍の導出を O(n²) → O(1)
`BuildLinksFromHex()` が「全タイル×6方向×全タイル線形走査」だった。`(col,row)→id` の配列インデックスにしたら **盤の生成が 1,908ms → 4ms**（6,996タイル）。

### ③ 陸の割合を目標に寄せる
プレートの当たり外れ任せだと実測で **陸27%〜55%** とばらつき、盤ごとに遊びが別物になっていた。プレート単位で陸/海を足し引きして **0.42（小さい盤は0.50）** に寄せる → 3seed×4サイズで **33〜42%** に収束。

### ④ 地上モードで迷宮UIを丸ごと畳む
以前は下部ツールバーとフロアタブだけを隠していたので、**盤の縁から迷宮のパネルが覗いて雰囲気を壊していた**。Canvas直下の兄弟を全部畳む方式にしたので、パネルが増えても勝手に追従する（もともと閉じているものは触らない）。地上パネル自体も画面いっぱいに敷いた。実測で迷宮UI6枚が畳まれ、戻すと6枚が復帰する。

### ⑤ W1のあいだの暫定描画
uGUIのままなので、**選択中のタイルを中心に 30×22 の窓を切って**その中だけ描く。大(6,996)でも 199ms / 13,106 GameObject に収まる。**W2でTilemapに移して窓を撤廃する。**

### 次（W2/W3）
- **W2 描画**: Hexagonal Tilemap へ移行（1タイル=GameObject 0個・チャンク単位で自動カリング）。地形/所有色/国境/霧をレイヤーで重ね、**文字は画面内かつ一定ズーム以上のときだけ**プールしたTMPを付ける（Civと同じ）。クリックは `HexGrid.CellAt` の逆変換1回で解決（Buttonが不要になる）。パン/ズームはカメラ操作へ。**冒頭で状態の保存(Capture/Restore)を入れる**（迷宮シーンを実際に畳むため）。
- **W3 遊び**: 眷属の移動力と同時進軍、国境の自動拡張、盤の大きさに比例する支配上限。※これが無いと「広いのに何も起きない盤」になる（今の速度では1万タイルの1割を埋めるのに1,000ターン）。

## W2: 地上をUnityのシーンで描く（2026-07-29 Opus5）
`SurfaceView.cs` / `HexTileArt.cs` 新規。uGUIのヘクス盤を捨てて、**ワールド空間の1枚メッシュ**に置き換えた。

### なぜ Tilemap ではなく自前メッシュか
- Unityのヘクス Tilemap は `cellSwizzle` と point-top/flat-top の対応が紛らわしく、[[HexGrid]] の座標をそのまま置けない。自前なら `HexGrid`/`SurfaceView.PosOf` をそのまま使える。
- **厚み（側面）の重なり順**を三角形の並び順で確実に制御できる（奥の行から積む＝画家のアルゴリズム）。
- 1タイル4頂点なので、全部見えても1メッシュに収まる。

### ① `HexTileArt` ― タイルの絵を1枚のアトラスに焼く
地形7種＋未探索の8セル（1セル 128×136px）。各セルに「天面のヘクス＋下に伸びる側面＋地形のモチーフ」を描く。天面は0.76に潰して俯瞰に（C1からの見た目を踏襲）。モチーフは山＝尖り3つ／丘＝こぶ／森＝木立／湿地・海＝波／荒地＝斑。

### ② `SurfaceView` ― 見えているところだけ詰める
カメラの矩形から `row0..row1 / col0..col1` を出して、その範囲のタイルだけ4頂点ずつ積む。**東西のループは、列をラップさせずに置くことで継ぎ目でも途切れない**。所有者・選択は頂点カラーで塗る。専用のオルソカメラを持ち、パン（ドラッグ）／ズーム（ホイール）／クリック（`CellAt` の逆変換1回）を担当する。**Buttonが1つも要らなくなった**。

### ③ 迷宮は「壊さずに」畳む
迷宮側のカメラを `enabled = false` にするだけ。**GameObjectは消さないので、階層・配置・個体・進行はメモリにそのまま残り、戻れば完全に元通り**。地上パネルは透明な器にして、UIは必要なところにだけ不透明な板を敷く（そうしないと盤の上にUIの背景がかぶって世界が見えない）。

### 実測（W1比）
| | 271タイル(旧uGUI) | 6,996タイル(新) |
|---|---|---|
| 盤の GameObject | 4,441個 | **0個**（シーン全体でも390） |
| メッシュ再構築 | 61ms（クリックのたび全Destroy） | **2.6ms**（全部見える zoom46・12,870タイル描画） |
| zoom7（通常の寄り） | – | **0.2ms / 1,085タイル** |
| ワールド座標→セルの往復 | – | 400/400 一致 |

**26倍のタイル数を、23倍速く描けるようになった。**

### 実測して直したこと
- **ラベルが全部□** ― ワールド空間のTMPは既定フォントに日本語が無い。UIと同じ `uiFont` を渡す。
- **地名が重なって読めない** ― 全タイルに名前を出すとタイル幅を超えて隣に被る。**Civと同じ密度**にして、出すのは「拠点/都市・遺産・真核・驚異」だけ、資源はうんと寄ったときだけに絞った。文字は**折り返しを残したまま自動縮小**（C2と同じ罠：wrappingを切ると縮まずに横へはみ出す）。
- **未探索タイルが背景に沈んで盤に見えない** ― 霧の色を少し明るい石板色にした。
- 初期ズームを7に。**注目タイルが右の詳細パネルに隠れない**よう、カメラを画面幅の26%だけ右にずらす。

### 残っている掃除
`GameUIManager.RefreshHexMap()` と `HexMapPanZoom.cs`、uGUIのヘクス盤パネルは**もう呼ばれていない**（死にコード）。次の機会に削除する。

### 次（W3）
広さに見合う進行：眷属の移動力と同時進軍／国境の自動拡張／盤の大きさに比例する支配上限。※これが無いと「広いのに何も起きない盤」になる。

### W2の手直し：世界が横に何個も並ぶ／迷宮が映り込む（2026-07-29 Opus5）
ユーザーの画面録画をUnityのVideoPlayerで14フレーム抜き出して確認した。**同じ大陸が横一列に5個並んでいた**のが「横並びすぎる」の正体だった。

1. **東西ループが世界を何周ぶんも描いていた**。実測で初期ズームでも1.4周、引き切ると**9.4周ぶん**が画面に入っていた。
   → 視界が世界1周より広くなったら**カメラを中心に1周ぶんへ丸める**（同じタイルを2度描かない）。あわせて**引ける上限を盤から算出**し、引き切ると世界がちょうど1つ収まるようにした。実測：試作/中/大とも引き切って**世界1.00個ぶん**。
2. **世界1つ分そのものが細長い帯だった**。天面の縦の潰し `Squash=0.76` のせいで、試作盤が **2.17:1**。
   → Squash を **0.90** に緩め、盤の寸法を **世界1つが16:9** になる W/H=1.386 で組み直した。
   試作 19×14=266 / 小 57×41=2337 / 中 79×57=4503 / 大 98×71=6958 → 実測比 **1.74〜1.78:1**。
3. **「地上ツリー」タブで有効なカメラが0台**になっていた（地上カメラを止め、迷宮カメラも止まったまま）。前のフレーム＝迷宮が残って見えるのはこれ。
   → 地上モードのあいだは**タブに関係なく地上カメラを常に有効**にし、ツリーは板で隠す。畳むほうも `Camera.main` 1台ではなく**有効なカメラを全部**畳んで、戻すときに復帰させる（`Camera.main` は地上モード中 null になるので当てにできない）。

検証: 4サイズの比が1.74〜1.78、引き切って世界1.00個ぶん、地上/地上ツリー/迷宮復帰でカメラが常に1台だけ有効。error0。

### W2の手直し②：既定が試作盤のままだった／地上カメラが迷宮まで描いていた（2026-07-29 Opus5）
1. **世界が小さく感じたのは、既定の盤が「試作 19×14＝266タイル」のままだったから**。試作サイズは W1 で uGUI が重かった頃の暫定で、W2（1万タイルでも2.6ms）で不要になっていたのに既定に残っていた。266タイルなら横に少し動かすだけで一周するのは当然だった。
   → **試作を廃止**して `Tiny 40×29=1160 / Small 57×41=2337 / Medium 79×57=4503 / Large 98×71=6958` に。**既定を「中」(Civ Standard相当・4,503)** へ。あわせて初期ズームを 7→5.5（zoom7だと中の盤でも画面2.7枚ぶんで一周してしまうため／5.5で3.5枚ぶん）。
   ※ループ自体は正しく1周ぶんで打ち止めになっている（引き切って世界1.00個ぶん・実測済み）。動かすと初期地点に戻るのはCivと同じ挙動で、盤が266タイルだったのが原因。
2. **地上カメラの `cullingMask` が -1（全レイヤー）**で、**迷宮のGameObjectを172個描いていた**。盤と迷宮が同じ座標帯に重なっているので、地上へ移った直後だけ迷宮が見え、パンして離れると消える＝「最初だけ映る」。
   → **`Surface` レイヤー(8) を新設**し、盤・ラベル・カメラをそこへ。`cullingMask = 1<<8`。実測で漏れ **172個 → 0個**。
   **ハマった点**: `manage_editor(add_layer)` は "added successfully" を返すが **TagManager.asset に永続化されない**ことがある（実際スロット8は空のままで `NameToLayer` が -1 を返した）。`ProjectSettings/TagManager.asset` を直接編集して解決。

検証: Surfaceレイヤー=8／cullingMask=256／地上カメラが描く迷宮の物0個／既定盤 中4,503／4サイズとも一周に画面1.4〜3.4枚ぶん。error0。

## 地上UIをCiv式のメニュー方式へ（2026-07-30 Opus5）
盤がシーンそのものになった以上、パネルを敷きっぱなしにすると世界が見えない。**常時出すのは上の帯だけ**にして、あとは**左のメニューから開く**形に作り替えた。既定では**何も開いていない**。

### ① 左端のメニュー＋開閉できる窓
- メニュー4つ：**領域 / 勢力 / 眷属 / ツリー**。押すと窓が開き、もう一度押すか窓の×で閉じる。同時に開くのは1つ。
- 窓は 620×約800。開いていても画面の34%、閉じれば0%。以前は右の詳細列だけで**幅の47%**を占めていた。
- **ツリーはタブではなくメニューの1項目**に（Civの技術ツリーと同じ扱い）。盤／ツリーのタブは廃止。
- **勢力**は新設。自分の拠点と他の魔王の一覧で、**押すとその場所へカメラが飛ぶ**。広い盤で迷子にならないための導線。
- 窓を開いているあいだは左が埋まるので、注目タイルを**右寄りに置く**（`SurfaceView.FocusOffsetX`）。

### ② 選択中タイルの小さな帯
窓を開かなくても「いま何を選んでいるか」が分かるよう、下端に 620×76 の帯を置いた。所有者・地名・地形・守り・資源・遺産・人口・幸福/不満を2行で。`詳細` ボタンで領域の窓が開く。

### ③ Canvasを3枚に分けた ― **迷宮UIの取りこぼしを根治**
以前は「Canvas直下の兄弟を1枚ずつ畳む」方式だったので、**地上モード中にあとから開くパネル（生成パネル）を取りこぼして盤の上に居座っていた**（実測で発生）。
→ **迷宮UI(100) / 地上UI(110) / ツールチップ(200)** の3枚に分け、地上モードでは **`dungeonCanvas.enabled = false`** で丸ごと止める。増えたパネルも自動的に付いてくる。ツールチップは独立Canvasなので迷宮でも地上でも出る。

検証: `SurfaceUICanvas=True(110) / GameUICanvas=False(100) / TooltipCanvas=True(200)`。生成パネルの残留なし。領域・勢力の窓が620px幅で崩れず表示。error0。

### 掃除が残っている
`RefreshHexMap()` / `HexMapPanZoom.cs` / `surfaceTab` / `surfaceTabBtns` / `boardOnlyLabels` / `surfaceRightBg`など、uGUI盤とタブ方式の残骸。

## W3: 広さに見合う進行（2026-07-30 Opus5）
盤が4,500〜7,000タイルになったのに、進行は「眷属が1ターンに隣の1領域を取る」ままだった。**広いのに何も起きない盤**にしないための3点。

**前提の確認**: Civでも Standard 4,536タイルで1文明が持つのは250〜350タイル程度。残りは他civ・都市国家・未開拓地。だから「自分で1万タイルを埋める」のではなく、**拠点が自動で国境を広げ、他魔王と土地を取り合う**のが正しい形。実測でも支配率は1〜7%に落ち着いた。

### ① 国境の自動拡張（Civの文化圏）
拠点が毎ターン拡張ポイントを貯め、貯まると**版図の半径の内側にある中立タイル**を1つ併合する。食料・資源・川・遺産のあるタイルから優先して伸びる。他魔王領は取れない（そこは眷属が戦って奪う）。
- 得点 = 3＋人口×2＋都市4＋版図の施設＋拠点化3＋祝祭3、**不満のぶんだけ減る**（不満だと広がらない）
- 必要量 = 10＋4×(版図-1)（Civと同じく取ったぶんだけ高くなる）
- **実測メモ**: 12+6×n だと30ターンで自領13タイルにしかならず止まって見えた。10+4×n で1タイル3〜5ターン。

### ② 拠点を未支配の土地にも築けるように（Civの開拓者）
自領限定にしていたら、国境が広がるのを待つしかなく**40ターンで拠点3つ**しか建たなかった。見えている中立の陸なら築けるようにした（築いた瞬間そこが自領になる）。

### ③ 眷属の移動力と多段進軍
1ターンに `MovementOf` タイルぶん進み、**隣に着いてから戦う**（Civのユニットと同じ）。移動力＝2＋兵站1＋斥候1＋身軽1。経路はBFSで、**敵領は素通りできない**（Civの支配地域）。UIに「あとNターンで到着・移動力M」を表示。
- 実測: 7マス先の目標へ 移動力3 で T1に3マス・T2に3マス進んで交戦。

### ④ 支配上限を盤に比例
固定3では4,500タイルの盤で身動きが取れない。**4＋タイル数/700**＋研究。極小5／小7／中10／大13。拠点13×版図37 ≒ 480タイルでCivと同じ密度になる。

### ⑤ **産出の青天井を塞いだ（最重要）**
①を入れた途端、40ターンで **+4,806DP／+558名声** まで膨れた。原因は**支配タイル全部が産出していた**こと。Civでは**人口が割り当てたタイルだけ**が産出する。
- `YieldSummary` を「拠点ごとに `WorkedTiles`（人口ぶん）だけ集計」に変更。**名声も版図限定**に（以前は未編入でも入っていた）。
- `PopMult` から**人口の項を外した**。人口は「働くタイルの数」として既に効いているので、倍率にも入れると二重になる。施設だけは `SettlementSystem.PopBonus`（1+0.12×(pop-1)）で都市の大きさを反映（施設の数は都市数で頭打ちなので膨らまない）。
- 結果: 同じ40ターンで **働くタイル42／+1,444DP／+255名声**。働くタイルは人口の合計で頭打ちになるので、支配を広げても青天井にならない。

検証: 4サイズ50ターンで 支配率1〜7%・働くタイル50〜59・+838〜1,413DP・+186〜315名声。移動力3で7マス先へ2ターン。error0。

### 次
[[civ7-roadmap]] の **C3**（時代・偉業(Triumph)・誓約(Dedication)・災厄(Crisis)）。

## C3: 時代・偉業・誓約・災厄（2026-07-30 Opus5）
`EraSystem.cs` 新規。Civ VII 1.4.0 の Age / Triumph / Dedication / Crisis をそのまま持ち込む。

### ① 時代（3つ）
**胎動の時代 → 伸長の時代 → 終焉の時代**。時代は**ターン数ではなく偉業の達成**で進む（進行0〜100）。
時代が上がると **世界水準 +0 / +0.6 / +1.2** ＝ 来る冒険者が強くなる（諸刃）。この作品では時代＝**魔王がどれだけ世に知られたか**。

### ② 偉業（Triumph）18種
時代ごとに小4＋大2。**小＝即時報酬（進行+12）／大＝誓約が1枚解禁（進行+26）**。小4＋大2でちょうど100。
条件はダンジョンと地上の両方から取る（撃破数・階層・罠・拠点・眷属・版図・施設・遺産・魔法・都市・他魔王排除・配下Lv・遺物・魔王Lv）。撃破数のために `EurekaTracker.OnAdventurerDefeated()` を新設して `AdventurerAI` の撃破処理から呼ぶ。

### ③ 誓約（Dedication）10種・3枚まで
大偉業で1枚ずつ解禁。**3枚だけ**選べる（Civ VIIと同じ）。全部が等価な強さになるよう配分：
叡智(RP+5/T)／熱狂(感情+8/T)／豊穣(食料+2)／城塞(守り+80)／簒奪(他魔王への侵攻+25%)／静謐(不満-2)／軍旅(移動力+1)／黄金(領域DP+20%)／開墾(国境の拡張+40%)／**秘匿(名声-20%＝世に知られる速さを抑える)**。

### ④ 災厄（Crisis）5種
進行が **75** を超えると発生し、**負の政策を1枚必ず選ぶ**まで時代が進まない。
飢饉(食料-2)／叛乱(不満+2)／枯渇(領域DP-25%)／侵攻(他魔王の力+30%)／停滞(国境の拡張-50%)。政策は時代の変わり目で消える。

### ⑤ UI
地上メニューに **「時代」** を追加（5つ目）。進行バー・災厄の選択・誓約の選択・偉業の一覧を1枚の窓に。ヘッダにも時代と誓約を1行で。

検証: 偉業の発火（拠点3つ→+12、版図30→+26、撃破20/罠15/眷属→計88）／災厄が75で始まり**政策を選ぶまで時代が進まない**／進行100で時代が進み進行リセット・災厄クリア・誓約は残る／世界水準 0→0.6→1.2／偉業リストが時代ごとに入れ替わる／終焉で打ち止め／誓約の効果（RP+5・不満+2など）が実際に効いている。error0。

### 次
[[civ7-roadmap]] の **C4**（勝利条件：制圧/恐怖/経済/革新のスコア制、2位比＋5ターン保持、総合スコア）。

## C4: 勝利条件（2026-07-30 Opus5）
`VictorySystem.cs` 新規。Civ VII の「4本すべてスコア制／閾値は2位の倍数／5ターン保持」をそのまま持ち込む。

### ① 4本の勝ち筋 × 5勢力
競うのは **自分／他の魔王3人／人間側**。**他の勢力が勝ち切るとこちらの敗北**になる。
- **制圧** 領土＋拠点/都市＋他魔王の排除
- **恐怖** 名声＋感情＋撃破数（原作の「畏怖で世界を染める」）
- **経済** DP/素材の産出＋施設＋遺産
- **革新** 研究の到達点＋魔王Lv＋遺物

### ② 閾値は「2位のスコア × 倍率」、倍率は時代で下がる
**胎動6倍 → 伸長3倍 → 終焉1.5倍**。届いてから **5ターン保持**で決着（相手に反撃の窓が空く）。
決着しないまま終焉の時代が終われば **総合スコア**（4本の合計）で決まる ＝ [[EraSystem]] に終着点ができた。

### ③ 実測して直したこと ― **人間側のスコアが桁違いだった**
最初は人間側を「未支配の土地の広さ」で測っていた。盤の9割は最初から中立なので、
**制圧289／経済1674** と他勢力（20〜40）より2桁大きく、**開始5ターンで人間側が勝って即敗北**していた。
→ 人間側は領土ではなく **「こちらへ向けてくる圧力」＝ターン・時代・世界水準** で伸びる時計として組み直した。
実測 T60 で 自分199／ヴェルグ143／人間114 と competitive な並びになった。

### ④ UI
地上メニューに **「勝利」**（6つ目）。4本それぞれに 自分のスコア／必要値／進捗バー／**全勢力の並び**／保持ターン。下に総合スコア表。ヘッダにも「誰の何が何ターン保持中か」を1行で。

検証: 5勢力4本のスコアが同じ桁に収まる／倍率が時代で 6→3→1.5 に下がる／**終焉の倍率1.5で人間側の『革新』が5ターン保持して勝利＝こちらの敗北**（研究を止めたら取られる、が実際に起きた）／総合スコアの集計／勝利パネルの目視。error0。

### 次
[[civ7-roadmap]] の **C5**（外交・独立勢力・交易：威名(Influence)、独立勢力の従属、交易路、戦争支持/厭戦）。

## C5: 外交・独立勢力・交易（2026-07-30 Opus5）
`DiplomacySystem.cs` 新規。Civ VII の Influence / Independent Powers / Trade Routes / War Support を持ち込む。

### ① 威名（Influence）
**名声とは別物**。名声は「世に知られた度合い＝冒険者が強くなる諸刃」だが、威名は**他勢力を動かす力**で難易度には効かない。
毎ターン入る（拠点＋都市＋中継の町＋遺産＋時代＋研究『威名の術』）。

### ② 独立勢力（自治都市）
盤の「町/都市」型の中立タイルから **4〜10箇所**を選んで置く（互いに6マス以上離す・自治都市なので防衛が硬い）。
6種：傭兵都市(眷属+15%)／交易都市(DP+120)／学都(RP+5)／聖堂都市(感情+10)／鍛冶都市(素材+6)／隠れ里(威名+3)。
**威名を注いで好意100で従属**。費用は既に従えている数だけ高くなる。**他の魔王も同じ相手に注いでくる＝取り合い**になる。

### ③ 交易路
自分の拠点どうしを結ぶ（10マスまで・上限＝1＋都市数＋研究）。**遠いほど旨い**（30＋距離×6 DP）＋両端に食料+1。

### ④ 他魔王との関係
**不可侵**（威名を払うと8ターン攻めてこない・向こうの成長も半減）／**讒言**（力を12%＋20削る）／
**厭戦**＝同時に2人以上と交戦していると全拠点に不満が乗る（1人までは無償）。

### ⑤ 研究3ノード追加（地上12→15）
威名の術（威名+4）／交易の道（交易路上限+2）／盟約（働きかけ-30%）。天啓もそれぞれ設定。

### ⑥ UI
地上メニューに **「外交」**（5つ目・全7項目に）。威名／独立勢力9件（種類・効果・好意・働きかけ・位置へ）／交易路（開く・閉じる）／他魔王（不可侵・讒言・厭戦）。ヘッダにも威名・従属・交易・厭戦を1行で。

検証: 独立勢力9件が距離18〜43に分散生成／働きかけ6回で従属し傭兵都市の眷属×1.15が効く／不可侵で AtWar=false・厭戦2→1／讒言で力400→332／交易路2本で+108DP／パネルの目視。error0。

### 次
[[civ7-roadmap]] の **C6**（眷属＝指揮官：昇進ツリー、ZoC/側面/包囲、遠き地への渡航運用）。

## C6: 眷属＝指揮官（2026-07-30 Opus5）
`KinPromotion.cs` 新規。Civ VII の Commander（昇進が時代を越えて残る）＋ ZoC / 側面 / 攻城 を持ち込む。

### ① 昇進ツリー 4系統×3段＝12
眷属は戦うたびに **武勲(Merit)** を貯め、昇進を選ぶ。**同じ系統の1つ下の段が前提**。費用は 5＋4×(取得数)。
- **進撃** 疾駆(移動+1) → 強襲(中立への侵攻+20%) → 電撃戦(移動+2)
- **攻城** 破城槌(砦の防衛を50%無視) → 城塞破り(遺産・自治都市の硬さを無視) → 総攻め(側面の効果2倍)
- **統率** 号令(LP+8) → 鼓舞(配下のロスト半減) → 軍旗(戦力+15%)
- **渡航** 沿岸航行(海1マス) → 遠洋(海2マス＝遠き地へ) → 不屈(負傷ターン半減)

武勲は 完勝+3(他魔王なら+6)／辛勝+2(同+5)／敗走でも+1／**時代を越えると+3**。

### ② 指揮官は時代を越える（Civ VIIと同じ）
`KinRoster.OnEraChanged()` を `EraSystem.Advance()` から呼ぶ。**昇進はそのまま残り、負傷は癒え、武勲が入る**。育てた指揮官が時代をまたぐ資産になる。

### ③ 支配地域(ZoC)
**敵の拠点・本拠地に隣接するタイルに踏み込んだら、そのターンはそこで足が止まる**。素通りして奥を突けない＝Civの Zone of Control。

### ④ 側面(Flanking)
目標に隣接している**味方の眷属1体につき戦力+12%**（最大3体・『総攻め』で倍）。単騎で殴るより寄ってたかるほうが強い。

### ⑤ 攻城(Siege)
砦や遺産・自治都市の**硬さの加算ぶん**を、攻城の昇進を持つ者だけが無視できる。実測で砦Lv3(防衛560)に対し**280軽減**。

### ⑥ 渡航
`SurfaceMap.IsDiscovered` が、研究『渡航術』だけでなく **昇進『沿岸航行/遠洋』を持つ眷属がいるか**も見るようにした。

### ⑦ UI
眷属パネルの選択中の行に **昇進の12マス**（系統＋段＋名前・修得済みは色付き・前提未達はツールチップで理由）。ステータス行に 移動力・武勲・次の昇進費用を追加。

検証: 段2をいきなり取れない（前提の表示）／疾駆で移動3→4・電撃戦で6／号令でLP+8／砦Lv3への攻城軽減280／渡航で海2マス（AnySeaCross=2）／側面が僚友1体で×1.24（総攻めあり）／他魔王本拠地の隣でZoC=True／時代を越えて負傷0・昇進12個が残る／パネルの目視。error0。

### 次
[[civ7-roadmap]] の **C7**（物語事件・形見：2-3択の事件、周回持ち越しの形見）。これでC1〜C7が完結する。

## C7: 物語事件・形見（2026-07-30 Opus5）── **これで C1〜C7 が完結**
`NarrativeSystem.cs` 新規。Civ VII の Narrative Events / Memento を持ち込む。

### ① 物語事件 12種（各2〜3択）
状況（拠点数・眷属の有無・時代・階層・撃破数・独立勢力の有無）に応じて起き、**選ぶまで次は起きない**。一度起きた事件は二度と起きない。
迷い込んだ子供／商人の申し出／裏切りの噂／他魔王の使者／古い石碑／飢えた眷属／勇者の噂／鉱脈の発見／疫病／自治都市の使者／地下の声／裏切り者の冒険者。

**どれも一長一短**で、この作品の「名声を稼ぐ＝冒険者が強くなる」両刃と噛み合わせてある。
例：『迷い込んだ子供』＝帰す(名声-20)／喰わせる(感情+40・名声+60)／配下にする(無償で1体)。
　　『勇者の噂』＝迎え撃つ支度(DP-1200・全自領の砦が1段)／潜む(**名声-90**)／挑発する(名声+150・DP+1800)。

### ② 形見（Memento）8種・2枠 ― **周回を越えて持ち越す**
実績で解禁され、**`PlayerPrefs` に永続保存**される。この作品にはまだ保存機能が無いので、**形見だけはディスクに残す**（Civの Memento と同じ役割）。
折れた真名の刻印(眷属化-30%)／初代の鍵(開始DP+2500)／血染めの首飾り(撃破DP+12%)／灰の懐中時計(**名声-15%**)／竜骨の欠片(地上の配下+10%)／賢者の遺稿(開始RP+40)／商人の割符(開始威名+80)／旗手の遺品(武勲+50%)。

### ③ UI
地上メニューに **「物語」**（8つ目・これで全項目）。事件の本文と選択肢カード、形見の2枠と一覧（解禁条件つき）。ヘッダにも「事件が選択を待っています」を出す。

検証: 4ターンごとに状況に合った事件が発火し3択が出る／選ぶと効果が入り次の事件まで間が空く／形見が撃破100・研究20で解禁され `PlayerPrefs` に "2,5" として残る／装備で撃破DP×1.12が効く／パネルの目視。error0。

---
## 🎉 C1〜C7 完了サマリ
| | 内容 |
|---|---|
| C1 | 広大な盤と手続き生成（ボロノイ/プレート・海と遠き地） |
| C2 | 拠点と都市（Town/City/版図・特化9種・不満-5%/点・祝祭・街区・倉庫・専門家） |
| C3 | 時代・偉業18・誓約10（3枚）・災厄5 |
| C4 | 勝利条件4本×5勢力（2位比の閾値・5ターン保持・総合スコア） |
| C5 | 外交（威名・独立勢力6種・交易路・不可侵/讒言/厭戦） |
| C6 | 眷属＝指揮官（昇進12・ZoC・側面・攻城・渡航） |
| C7 | 物語事件12・形見8（周回持ち越し） |
| W1〜W3 | 盤をCiv式16:9の長方形＋東西ループへ／ワールド空間の1枚メッシュ描画／国境の自動拡張と移動力 |

### 次の候補
通しプレイのバランス調整（C1〜C7で軸が大幅に増えたので実測が要る）／死にコードの掃除（`RefreshHexMap` / `HexMapPanZoom`）／セーブ機能／特殊制限P3。

## 上位階層のレベル問題（2026-08-02 Opus5）── ①適性深度 ②魔素濃度
「1階層の隊以外がレベルを上げにくく、上の階層ほど弱い」問題の根治。

### 問題の構造
**経験値は「戦った回数」に比例するのに、必要な強さは「そこまで来た冒険者の質」に比例する。**
回数は浅いほど多く、質は深いほど高いので、投資と需要が正反対を向いていた。
実測（旧仕様・20ウェーブ後）: **B1F Lv21(×1.80) / B3F Lv6(×1.20)** ＝ 深いほど弱い。
さらに B1F が抜かれた瞬間、「B1Fでも止められなかった相手」が Lv6 の配下に当たる＝**落差が最大のところに最弱がいる**。

### アーキテクチャ上の制約（ユーザー指摘）
`DungeonGridSystem` は1つだけで `ActivateFloor(i)` が階層を差し替える＝**2階層は同時に存在できない**。
`Descend()` は「退却中でない全員」を次フロアへ移し、前フロアの防衛体は撤収する。
→ **「弱い者がB1Fに残って戦い続ける」という状態は表現できない。**

### 採った解 ― 「残す」のではなく「帰す」
欲しいのは「弱い者がB1Fに居続けること」ではなく「**B1Fが弱い者を相手にし、B2F以深には強い者しか来ないこと**」。
それなら残す必要はなく、**階段の前で引き返させれば同じ結果**になる。複数階層の同時進行なしで成立する。
- **強者**：踏破目的で、相手が門番でなく `CombatPower` が2.2倍以上あれば**足を止めずに素通り**（殴られはするのでコストは残る）
- **弱者**：`Descend()` で `WillDescendTo(next)` を満たさない者は**引き返して清算**
- 必要Lvは期待Lvに対し **B2F 85% / B3F 110% / B4F 135% / B5F 160%**（実測 T15なら 21/28/34/40）
- **副次効果**：門番は道を塞ぐので強者も倒さざるを得ない＝各階のボスは強者と戦って育つ

### ② 魔素濃度 ― 経験値の基礎を深度で決める
「迷宮の核に近いほど魔素が濃い」。`MinionRoster.ExpForFloor(floor, fought)` = `(25 + 30×階層) ×(戦えば2)`。

| | B1F | B2F | B3F | B4F | B5F |
|---|---|---|---|---|---|
| 未到達 | 25 | 55 | 85 | 115 | 145 |
| 戦った | 50 | 110 | 170 | 230 | 290 |

実測（20ウェーブ後）: **B1F(毎回戦う) Lv11(×1.40) / B3F(未到達) Lv18(×1.68)** → **深いほうが強い**に反転。

### ③ 降下の「湧き待ち」を解消
降下は `spawner.IsSpawning` の間ずっと止まる設計だったが、湧く間隔は最短1.5秒で **T15なら湧き切るのに約27秒**。
階層を早く片付けると**何も起きない時間**ができていた。
→ 階段に到達した時点で `FlushRemaining()` で**控えを一斉に突入させて**から降りる。待ちが消え、「全員がその階層を通る」形は保たれる。

検証: 経験値表の反転／必要Lvの絞り込み（T5〜T25）／FlushRemaining の存在。error0。

### 次（この一連の残り）
③兵舎の派遣（占領地に訓練所）→ ④素材→経験値（未到達階層限定）。その後、他の9項目へ。

## ③訓練所・④実戦の反芻（2026-08-02 Opus5）── 上位階層のレベル問題の続き
`TrainingSystem.cs` 新規。①②で「深いほど育つ」向きは直したので、**プレイヤーが能動的に下層を仕上げる手段**を足す。

### ③ 訓練所（地上の施設）
占領した土地に建て、配下を送り込むと毎ターン鍛えられて帰ってくる。
- 施設 **『訓練所』**（研究 `s_training` 練兵の地・天啓「配下を8体そろえる」）。隣接は **丘陵+2／山岳+1／隣の兵舎+2**
- **3体まで・4ターン**。毎ターン `40 + 15×隣接` exp（実測: 隣接1で+55／4ターンで+220＝約2Lv）
- **訓練中は隊にもボスにも使えない**＝防衛を削って将来に投資する判断になる
- 訓練所は**産出しない**（育てるのが役目）。領域を奪われたり施設が消えると訓練は中断
- ※「配下は迷宮を出られない」という原作の縛りは、**自陣にした土地なら自由**というユーザーの整理で通した

### ④ 実戦の反芻（素材を注ぐ）
**冒険者が到達しなかった階層に置いてある個体にだけ**使える。近道ではなく「戦えなかったぶんを埋める」手段。
- 費用 `4 + Lv/3` 素材 → **+90exp**
- 判定に `DungeonFloorManager.LastDeepestReached`（直近ウェーブの最深到達）を新設
- 図鑑の個体行に「反芻 素材N」ボタン。使えないときは理由をツールチップに出す

### ついでに直したもの
図鑑の個体行で **ステータス倍率がボス任命名に被っていた**（幅236に収まらず折り返していた）→ 幅244＋自動縮小で1行に固定。

検証: 研究で解禁→建設→HasCamp／毎ターン+55exp／4ターンでLv1→Lv3／訓練中は編成不可／反芻は未配置の個体を正しく弾く／パネルの目視。error0。

### 次
残りのバックログ（タイトル画面・タブ自動close・ターン頭の物語ガイド・配置の即時反映・部隊枠6・装備グレードの強化幅・隊から外したらマップも解除・地上の初期カメラと塔UI）。→ [[deep-floor-leveling]]

## 操作性の4件（2026-08-02 Opus5）

### ② タブの自動close
各ボタンが自分のパネルをトグルするだけだったので、**裏に開きっぱなしのパネルが積もり**、いちいち元のタブへ戻って閉じる必要があった。
→ `OpenExclusive(panel)` を新設し、**全画面パネルは1枚だけ開く**ように（魔王/感情/遺物/研究/拡張/図鑑）。地上へ入るときも全部畳む。

### ④ 配置の即時反映
配置や階層切替がストリップに反映されず、何かボタンを押すまで暗くならなかった。
→ `Update()` で **署名（階層・配置数・隊の中身・配置済み個体・隊の上限）を比べて、変わったときだけ**ストリップと隊トレイを作り直す。
⚠ 毎フレーム作り直すと押下中にButtonが破棄されてクリックが成立しない（既知の罠）ので、必ず署名方式にする。実測で「変化時のみ更新／無変化なら据え置き」を確認。

### ⑤ 部隊枠5→6が効かない ― 原因は `const`
`public const int SquadMaxSlots = 5;` だったため、**研究『部隊枠 +1』(m_slot) が一生反映されていなかった**（constはコンパイル時に焼き込まれる）。
→ `public static int SquadMaxSlots => 5 + (研究済み ? 1 : 0)` に。実測で研究後に**6体編成できる**ことを確認。
**教訓: 研究や状態で変わる値を const にしない。**

### ⑦a 隊から外したらマップの配置も解除
`SquadRemoveIndividual` はリストから消すだけで、マップに置いた実体が残っていた。→ `RemovePlacedOfIndividual` を新設して同時に撤去。

### ⑦b 地上に入ると未発見の隅が映る ＋ 迷宮タイルの目印
`selectedRegionId` の初期値が **0＝盤の左上の隅（未発見）** だったのが原因。
→ 初期値を -1 にし、入場時に「未選択／範囲外／未発見」なら**迷宮のタイルへ寄せる**。実測で 迷宮前の荒れ地 が中心に来ることを確認。
あわせて、迷宮の入口タイルは**ズームに関係なく常に「迷宮」と表示**するようにした（自分の本拠が一目で分かる）。

## セッション記録（2026-08-02・/compact 前）
### このセッションで入ったもの（コミット順・14件）
W1 → W2 → W2手直し×2 → 地上UIのメニュー化 → W3 → C3 → C4 → C5 → C6 → C7 →
上位階層のレベル問題(①②) → ③④訓練所と反芻 → 操作性4件。

**[[civ7-roadmap]] の C1〜C7 と W1〜W3 はすべて完了。**

### 現在のシステム一覧（地上・メタ層）
`HexGrid`(odd-r offset・東西ループ) / `SurfaceGen`(手続き生成) / `SurfaceMap`(盤) /
`SurfaceView`+`HexTileArt`(ワールド空間の1枚メッシュ描画) / `SettlementSystem`(拠点・都市・版図・不満・祝祭) /
`DistrictCatalog`(施設7種) / `EraSystem`(時代・偉業・誓約・災厄) / `VictorySystem`(勝利4本×5勢力) /
`DiplomacySystem`(威名・独立勢力・交易・不可侵) / `KinRoster`+`KinPromotion`(眷属＝指揮官) /
`NarrativeSystem`(物語事件12・形見8) / `TrainingSystem`(訓練所・実戦の反芻) / `RivalLords`(他魔王)。

地上UIは**左端メニュー8項目**（領域/勢力/眷属/ツリー/外交/時代/勝利/物語）＋開閉できる620px幅の窓。
Canvasは**迷宮100 / 地上110 / ツールチップ200**の3枚で、地上モードでは迷宮Canvasごと `enabled=false`。

### 残っているバックログ（ユーザー提示・優先順）
1. **タイトル画面・設定画面** … 開始時に「地上の広さ／宝箱の量／初期階層数／迷宮タイプ」を選ぶ。
   **迷宮タイプによって初期DPも変わる**（例：宝箱量が中なら200DP）。
2. **ターン頭の物語風ガイド** … CDO2のように、ターンの始めに推奨行動やシステムの説明を物語調で出す。
3. **装備グレードの強化幅** … 1段階の強化が体感できない。レベルの伸びや冒険者の伸びとマッチしていないのが原因。
   **コストを上げてよい**（消費DPを上げる／高グレードは素材も消費させる）ので、1段階1段階を明確な強化にする。

### 会話にしか無かった重要事項（memoryへ転記済み）
- **⚠ 研究や状態で変わる値を `const` にしない**。`SquadMaxSlots = 5` が const だったため、研究『部隊枠+1』が
  **一生反映されていなかった**（constはコンパイル時に焼き込まれる）。同種の「研究したのに効かない」を疑うときは**まず const を探す**。
- **上位階層のレベル問題の構造**：経験値は「戦った回数」に比例するのに、必要な強さは「そこまで来た冒険者の質」に比例する。
  回数は浅いほど多く質は深いほど高いので、**投資と需要が正反対**を向いていた。→ [[deep-floor-leveling]]
- **⚠ 階層は同時に存在できない**（`DungeonGridSystem` は1つで `ActivateFloor` が差し替える）。
  だから「弱い者をB1Fに残す」は表現できない。**「残す」のではなく「帰す」**で同じ結果を得た。
- **UIを毎フレーム作り直すとボタンが死ぬ**（押下中にButtonが破棄されてクリックが成立しない）。**必ず署名方式**で差分更新する。
- **長いC#をシェル経由で書かない**。`python -c "..."` に130行のC#を埋め込んだらbashの引用符解析が壊れて実行されなかった。
  → **Write/Editツール**を使うか、`cat > file << 'PYEOF'`（**クォート付き**ヒアドキュメント）にする。
- Unity MCP の `add_layer` は成功を返しても `ProjectSettings/TagManager.asset` に**永続化されないことがある**。必ず確認する。

## タイトル画面・世界設定（2026-08-02 Opus5）

残りバックログの1件目。起動したらいきなり迷宮が建っていたのを、**タイトルで止めて世界を選んでから作る**ようにした。

### 構成
- `GameSetup.cs`（新規・static）… 選択内容と**初期DPの算出**だけを持つ。UIから切り離したので値の検証がしやすい。
- タイトルUIは `GameUIManager` 内（既存のPanel/Text/Card/Chipヘルパーとスキンをそのまま使うため）。
  Canvasは **TitleCanvas(order 300)** の1枚に3ページ：**0タイトル / 1世界設定 / 2遊び方**。
- 起動時は `GameSetup.WaitForTitle` を **`GameUIManager.Awake` で立てる**。
  ⚠ Awakeでないと間に合わない（**Awakeは全オブジェクトのStartより前**に走る。`DungeonGenerator.Start` が先に迷宮を作ってしまう）。
- 『この世界で始める』で 迷宮タイプ/空間/宝箱/階層 を generator と floorMgr に流し、`SurfaceMap.Regenerate(広さ, 種)`、
  `res.SetDP(初期DP)` の順に適用してから `GenerateAndBuild()`。**建造費は初期DPに織り込み済みなので生成は無料**で行う。

### 初期DP＝開始予算 − 初期迷宮の建造費
```
予算   = 1000 + 400 ×(階層-1)                    → 1000 / 1400 / 1800
建造費 = (300 + 宝箱[少100 中300 多600]) × 階層 + タイプ[標準200 迷路0 大空洞100 蟻の巣250]
初期DP = max(100, 予算 - 建造費)
```
**1層・宝箱中・標準 → 1000 -(300+300) -200 = 200 DP**（ユーザー提示の例と一致）。

実測（迷路・タイプ費0のとき）:
| | 少 | 中 | 多 |
|---|---|---|---|
|1層|600|400|100|
|2層|600|200|100|
|3層|600|100|100|

**予算の伸び(+400/層)を建造費の伸び(+400〜900/層)より小さくしてある**のが肝。
おかげで「宝箱『少』なら深く始めても手元は減らない／『中』以上で深くすると軍資金が尽きる」という
**深さと豊かさのトレードオフ**になる。宝箱『多』は常に下限100＝「全部を宝箱に注ぎ込む」極端な入り。
地上の広さと空間タイプは**無料**（初期DPに影響しない）。

### 検証
コンパイルerror0。タイトル→世界設定→開始 を通しで実測：
`DP=600 / floors=2 / type=Labyrinth / space=Lava / chest=Small / surface=2337タイル(seed 12345)`、
タイトルCanvasは非表示、生成パネルの選択状態も同期、地上へ入ると迷宮タイルが中央に来ることを目視。
タイトル待ちの間は `BuiltFloorCount=0`＝**本当に何も生成していない**ことも確認。

### ハマったところ
- **全角のマイナス `−` はUIフォントに無く、サニタイズで消える**（「開始予算 1,000　建造費 800」になる）。半角 `-` を使う。
- 新しい `[SerializeField]` を既存コンポーネントに足しても、**YAMLに無いフィールドは初期化子の値が残る**（`= true` が効く）。

## ターン頭の物語ガイド＋地上の進軍まわり（2026-08-03 Opus5）

### 📖 腹心の報告（`GuideSystem.cs` 新規）
準備フェーズに入るたびに **①情勢（物語調）②進言（最大3件）③初出システムの説明（一度きり）** を組み立てて中央に出す。
設計の芯は「**盤面から機械的に読み取れる事実だけを根拠にする**」こと。
余っているDP・空いている配置枠・眠っているBP・待機したままの眷属＝**取りこぼしている選択肢**を拾って重みで並べる。
- 進言は重み順に3件（例: 何も置いていない=99／魔王の傷=90／眷属ゼロで条件を満たす個体がいる=95）
- 説明は `taught` で一度きり（基本・研究・眷属・地上・階層・素材・勝利条件）
- 上部HUDに『報告』ボタンを追加（読み返せる）。『今後は出さない』も可
- 呼び出しは `DungeonTurnManager.EndBattlePhase` の末尾と、開始時（第1ターン）

### 🐛 進軍が「何も起こらない／25ターン後」だった原因 ― `Kin.regionId = 0`
**id 0 は盤の左上の隅＝海**だった（W1で盤が手続き生成のW×Hになったときの取り残し。旧仕様では0＝迷宮前）。
眷属は生まれた瞬間から迷宮の**53タイル離れた海の上**に立っていたので、
- `StepsTo` が 99（到達不能の番兵）→ ETA表示が `ceil(98/移動力4)＝25ターン`（＝ユーザーの見た「25ターン後」）
- `ResolveTurn` で `NextStep<0` → 「道が無く進軍を取り消した」→ **毎ターン進軍が解除されていた**

直し：
- `regionId` の既定を -1 にし、`TryName` で `HomeRegion`（＝`SurfaceMap.IndexOfCenter()`＝迷宮のタイル）に置く
- 敗走の戻り先も `HomeRegion` に（`= 0` を潰した）
- `FixStrayPositions()` を `ResolveTurn` の頭で呼び、盤を作り直しても海や範囲外に取り残さない
- `SetMarchTarget` は **届かない先を受け付けない**（受け付けると「指示は通ったのに毎ターン取り消される」になる）
- `StateText` は 99 のとき「道が塞がれています」と出す

### ⚔️ タイルの帯から直接進軍できるように
帯（選択タイルの概要）を 76→108px にして操作ボタンを載せた。窓を開かなくても
**進軍（眷属名・ETA・到達不能）／ここを守らせる／拠点を築く**（築けないときは理由）が押せる。
動かす眷属は「眷属メニューで選択中のもの→動ける1体」を自動で選ぶ。結果は帯の2行目に出す。

### 👑 盤に眷属を表示
`SurfaceView` に `CollectUnits()` を足し、タイルの下寄りに小さく `◆<真名の頭文字>` を出す。
**緑=待機 / 金=進軍中 / 灰=負傷**。`MarkDirty()` を `RefreshSurfacePanel` から呼んで位置の変化を描き直す。

検証: error0。眷属を作って隣の未支配タイルへ進軍 → `steps=1 / 今ターン交戦 → 完勝 → 自領`、
盤に `◆エ` が出ることを目視。第1ターンの報告（進言3・説明1）も目視。

### 🕹️ U1：ユニットを自分で動かす＋視界（2026-08-03）
これまで地上は「行き先を指定→ターン終了時に自動解決」の抽象モデルだった。Civのように**その場で動かせる**ようにした。

- **移動力の財布** `Kin.mp`（-1＝満タン）。手動移動も自動進軍も**同じ財布**から引く。
  ターン解決の最後に `mp = MovementOf(k)` で配り直す。
- `PathTo` / `CanMoveNow` / `TryMoveTo`（移動力を消費してその場で歩く・自動進軍は取り消し）
- `CanAttackNow` / `TryAttack`（隣接＋移動力1で即時交戦）
- 戦闘判定は `ResolveAttack(k, r, turn)` に**切り出して1箇所に**（自動と手動で仕様がずれないように）
- 帯のボタン: `ここへ移動（-N）` `攻撃する` `進軍（ETA）` `ここを守らせる` `拠点を築く`＋
  選択中ユニットの `◆真名 移動力 n/N・現在地` 行。**自動進軍は steps>1 のときだけ出す**（隣なら攻撃で足りる）
- 盤のタイルを押すと、そこに立っているユニットが**選択される**（Civと同じ操作感）

**👁️ 視界（追加方式）**
- `SurfaceMap.seen[]` と `MarkSeen(center, radius)` / `IsSeen(id)` を新設。**一度見た土地は覚える**。
- ⚠ 「見えている(IsSeen)＝霧を剥がして描く」と「手が届く(IsDiscovered)＝進軍先に選べる」は**別物**。
  海は見えても支配できないので、`IsDiscovered` は海を弾いたまま `seen` を条件に足した。
- `VisionOf(k)` = 2（研究『斥候』で3）。歩くたび／ターン解決のたびに更新。盤の生成時は迷宮の周り2タイル。

検証: 2マス先へ手動移動＝コスト2・mp 3→1、視界 19→28タイル。隣を手動攻撃＝完勝・自領化・mp0、
mp0では攻撃不可、ターンを回すと mp が戻る。error0。

## S1 政体と政策スロット（2026-08-03 Opus5）

civ7wiki 精読の差分（[[civ7-gap-plan]]）の1件目。**Civらしさで最大の欠落**だった「付け替えるビルド」を入れた。

### 既存との役割分担（ここを間違えると二重になる）
`EraSystem` の **誓約(Dedication)** が既に3枠のスロット制だったので、**作り直さず別レイヤー**にした。
- **誓約** … 大偉業で解禁／時代の変わり目にだけ選ぶ**長期**の枠
- **政策** … 研究と時代で解禁／**準備フェーズならいつでも無料で差し替える短期**の枠

### 政体（4種）
`恐怖政治(戦2民1)` / `収奪王政(戦1富2)` / `秘儀結社(秘2民1)` / `群狼同盟(戦1富1秘1)`。
それぞれ **常時効果＋色つきスロット構成＋祝祭中の2択**。時代の変わり目は無料、途中の乗り換えは `400 + 200×時代` DP。

### 政策カード（4系統×5枚＝20枚）
**スロットに色があり、同じ色のカードしか差せない**（Civ VI式）。効果は**加算を主**にして乗算軸を増やさない。
- ■戦 罠の刻印/肉の壁/略奪の作法/城塞化/総動員　■富 徴発/撒き餌/隊商路/遺物市場/黄金律
- ■秘 写本の蒐集/天啓の記録/魔素の精製/秘儀の伝授/進化の秘術　■民 慰撫/開墾/版図の拡張/祝祭の準備/万民の帰依
- **迷宮にも地上にも効かせた**（罠・防衛体HP・部隊枠・魔法・経験値・召喚コスト … と 領域DP・不満・食料・国境・LP）

### スロットの伸び方と陳腐化
```
政体の色つき枠 ＋ 自由枠（時代 胎動0/伸長+1/終焉+2 ＋ 研究『統治の刷新』+1 ＋ 祝祭中+1）
```
**祝祭が「産出×1.15」だけだったのが、ここでスロット+1に繋がった**（Civ VII の祝宴）。
カードには時代があり、**古い時代のカードは効果が半減**（Civ VII の建造物陳腐化を政策で先に導入。S5で建造物へ流用する）。

### 研究2ノード追加
`p_slot`「統治の刷新」＝自由枠+1（天啓：祝祭を1度起こす）／`p_edict`「布告の権」＝**戦闘中でも差し替え可**（天啓：政策を3枚同時に差す）。

### ⚠ また const の罠
`EurekaTracker.Discount` が `const float = 0.6f` だったため、政策『天啓の記録』が**一生反映されない**ところだった。
プロパティに変えて政策を参照させた。**状態で変わる値を const にしない**（[[deep-floor-leveling]] の教訓が再発）。

検証（error0）: 色違いは弾く／未解禁は弾く／政体を変えると差せなくなったカードが押し出される／
祝祭中はスロット+1で自由枠に富カードが入る／時代が進むと `罠倍率 1.15→1.08`（陳腐化で半減）・伸長カードが解禁・
天啓の割引が 0.60→0.52（陳腐化した『天啓の記録』）。UIは地上メニュー『政策』（政体4枚＋祝祭2択＋スロット＋手札20枚）。

## S2＋S3 属性ツリーとレガシーの道（2026-08-03 Opus5）

Civ VII の「**レガシーの道を達成する → その軸の属性ポイントが入る → 属性ツリーで恒久強化**」を一体で入れた。
S2とS3は同じループの表裏なので、片方だけ作ると宙に浮く（＝ポイントの出所が無い／達成しても行き先が無い）。

### レガシーの道＝既存の偉業に軸を付けた
`EraSystem.TriumphDef` に `axis` を追加し、18の偉業を6軸に振り分けた。
達成で **小偉業=1点／大偉業=2点** がその軸に入る。合計 **24点**、ツリーも **6軸×4段＝24ノード**。
ただし軸ごとに偏るので**全部は取れない**（＝通った道のぶんだけ強くなる）。

### 属性ツリー（`AttributeSystem.cs` 新規）
- 軍事: 防衛体HP+5% → 侵攻+10% → 損耗-20% → 部隊枠+1
- 拡張: 拠点上限+1 → 国境+20% → 拠点の食料+1 → 拠点上限+1
- 経済: 領域DP+10% → 素材+15% → 召喚-10% → 交易路+1
- 科学: RP+2/T → 研究コスト-10% → 天啓の割引+10% → 経験値+15%
- 文化: 感情+6/T → 祝祭の必要量-15% → 不満-1 → 名声+20%
- 外交: 威名+3/T → 独立勢力の費用-25% → 眷属LP+4 → 他魔王の力-10%

段は前段を取ってから。**点は軸ごとに別**なので、軍事の偉業では軍事しか伸びない。**時代をまたいで残る**。

### 損耗の軽減は1箇所に寄せた
`KinRoster.LoseFollowers` の入口で `政策『略奪の作法』×属性『練度』` を掛ける。
勝敗の3分岐に散らすと片方だけ直して食い違うため。

検証（error0）: 大偉業『眷属に真名を与える』(文化)で **文化+2**／段飛ばし不可／点切れで取れない／
取得で `防衛体HP×1.05`・`侵攻×1.10`・`感情+6/T`・`祝祭の必要量×0.85` が実際に効く。
UIは地上メニュー『属性』（6軸×4段・取得済/取得可/理由つき）。腹心の報告に「未使用の点がある」進言と初出説明も追加。

## S4 探索 ― 地形の重み・発見・斥候（2026-08-03 Opus5）

U1で手動移動を入れたので、**「どこを通るか」に意味**を与える段。3点セットで入れた。

### 🐾 地形の踏破コスト（`SurfaceMap.MoveCost`）
`平地1 ／ 荒地2 ／ 森2 ／ 丘2 ／ 湿地3 ／ 山岳=進入不可 ／ 海=不可`。
**自領は常に1**（道が整っている扱い）＝版図を広げると軍が速くなる。
- ⚠ Civ VII の森・荒地は「残り移動力を全部消費」だが、うちの移動力は2〜5と小さいので**2〜3の重み**にした。
- `KinRoster.PathTo` を BFS から**ダイクストラ**に変更。`StepsTo` の意味を「歩数」→**「総移動コスト」**に変え、
  タイル数が要る所は `TilesTo` を新設して分けた。
- Civと同じ **「移動力が1でも残っていれば隣へは必ず入れる」** を入れた（重い地形で詰まないため）。
- 自動進軍も1歩ごとにコストを引く（重い地形は入れるだけの移動力が要る）。

### 🔦 発見（`DiscoverySystem.cs` 新規）
**未踏の地に初めて入った瞬間**に30%で発生。8種、それぞれ**選択肢2つ**（石塚・焚き火・崩れた祠・獣の骨・
涸れた井戸・野営地・道標・澱んだ泉）。報酬はCiv VIIと同じく小刻み（DP+60〜180／素材+6〜12／研究点+4〜10／
感情／名声／**周囲が見える**）。同じタイルでは二度と起きず、誰かの土地（自領・敵領・海）では起きない。
UIは**ツールチップCanvas(order200)のモーダル**＝迷宮でも地上でも出る（自動進軍はターン終了時＝迷宮側で起きるため）。

### 🔭 斥候（`ScoutSystem.cs` 新規）
**安い・速い・地形を無視・戦えない**専門職。DP150／移動力4／視界3／上限2（研究『斥候』で+2）。
- **地形の重みを無視**して動き、**通り道の1マスずつ**視界を開けて発見も拾う（Civの斥候と同じ「歩いた線が見える」）。
- 敵領に入れず、取り残されると失われる。盤には □ で表示（眷属は ◆）。
- 帯から「斥候を出す（150DP）」「斥候をここへ（-N）」。

検証（error0）: 迷宮の隣は `森2/山岳=不可/荒地2`・自領1。斥候を3マス先へ→コスト3・残1、**視界37→58タイル**。
発見『打ち捨てられた野営地』→選択Aで **DP+60・素材+9**、モーダルが閉じる。

### ⚠ またこの罠
Pythonのヒアドキュメント経由でC#を書いたとき、文字列中の `\n` が**実際の改行**になって CS1010。
（[[handoff-status]] に既出。長い文字列を含む編集は Edit ツールで直すのが速い）

## S5 陳腐化と改築＋資源の割り当て（2026-08-03 Opus5）

### ⏳ 陳腐化と改築（Civ VII の Obsolete / Overbuild）
施設に**建てた時代**を刻み（`Region.districtEra / district2Era`）、時代が変わると**隣接ボーナスを失う**。
`DistrictCatalog.EffAdjacency(regionId, slot)` を新設し、産出・倉庫の食料・兵舎の防衛はすべてこれを見る。
- **専門家の出力も道連れ**（専門家は隣接ボーナスの2倍なので、0になれば0）＝Civ VII と同じ挙動が自動で出る
- **改築**＝建て直しの半額（`Cost×0.5`・下限50）で今の時代の建て方に直すと、隣接ボーナスが戻る
- ⚠ 基礎産出（1）は落とさず**隣接ボーナスだけ**失う形にした。Civ VIIは産出も大幅減だが、
  こちらは施設が少ないので全部落とすと時代移行が罰ゲームになる。隣接は3〜13あるので十分痛い。

実測: 交易所の隣接13 →（時代が進む）→ **実効0**・改築費285DP → 改築で **13に戻る**。

### 💎 資源の割り当て（Civ VII の Resource Assignment）
資源タイルは**版図にあるだけでは効かない**。拠点の**資源枠**に入って初めて、食料・幸福・倉庫の隣接に乗る。
- 枠 ＝ 町1／都市2 ＋ 研究『倉庫術』+1 ＋『交易の道』+1
- 毎ターン `SettlementSystem.ReassignResources()` が**価値の高い順**（魔石5>宝石4>鉄3>穀物/家畜2>良材1）に自動で詰める
- 拠点の行に `資源 3/3（版図に5・枠外2）` と表示。タイルの帯にも `[割当]/[枠外]`

これで **「都市に昇格させる」「研究を進める」ことが資源を活かす鍵**になり、
版図をただ広げるだけでは資源が死ぬ（＝Civの資源管理の判断が生まれる）。

実測: 資源5個・枠3 → 割当3（良材2つが枠外）。『倉庫術』で枠2→3。

検証 error0。UIは領域パネルに『陳腐化：隣接ボーナスを失った』＋『改築 NDP』、拠点行に資源の使用状況。

## S6 独立勢力の段階化と危機の対抗策（2026-08-03 Opus5）

### 🏛️ 独立勢力を3段階に（Civ VII の「友好関係を築く→都市国家化→宗主国」）
これまでは好意が満ちた瞬間に従属で、恵みが一気に入っていた。Civ VII と同じく**間**を作った。
- **独立(0) → 友好(1) → 宗主国(2)**。好意100で友好、そこから **4ターン保つ**と宗主国。
- **友好の間は恵みが半分**（`KindPower()` が 0.5／1.0 を返し、各恵みはこれを掛ける）＝「あと一押し」の期間ができる。

### 宗主国だけができること（Civ VII の宗主国限定外交／価格も寄せた）
| | 費用 | 効果 |
|---|---|---|
| 成長の促進 | 威名15 | 一番小さい拠点に人と糧（人口が1つ育つ） |
| 軍備の増強 | 威名30 | DP+400・素材+12 |
| 併合 | 威名120 | その土地を自分の**拠点(Town)**として取り込む（恵みは失う） |

### 💥 粉砕（Destroy）
眷属がその土地を落とすと粉砕。**軍事の属性+1**と素材+20（Civ VIIの「独立勢力粉砕で軍事属性」）。
`KinRoster.AfterConquer` から `DiplomacySystem.OnRegionConquered` を呼ぶ。

### 🛡️ 危機の対抗策
災厄は「必ず負の政策を1枚選ぶ」ままだが、**DPを払えば影響を半分にできる**ようにした
（`800 + 600×時代`）。凌ぎ切って時代を越えると**文化の属性+1**（Civ VIIの「全員が危機に対処すると次時代に恩恵」）。
実測: 叛乱の不満 +2 → 対抗策で **+1**。

### 🐛 見つけた本命のバグ：盤を作り直しても独立勢力が作り直されていなかった
`SurfaceMap.Regenerate` が `RivalLords.Reset()` しか呼んでおらず、**独立勢力は前の盤の id を握ったまま**だった。
実測で **独立勢力が海タイルを指していて『働きかけ』が永久に失敗**していた（`IsDiscovered` は海を常に false にするため）。
→ Regenerate で `DiplomacySystem / ScoutSystem / DiscoverySystem` も作り直し、`KinRoster.FixStrayPositions()` も呼ぶ。
**[[surface-units-u1]] の `regionId=0` と同型の事故**。盤の id を握っている側は全部作り直す。

検証 error0: 陸に立つ／好意100→友好(恵み0.5)→4ターン→宗主国(1.0)／軍備の増強でDP+400素材+12／粉砕で軍事属性0→1／
対抗策で不満+2→+1。

## U2 敵ユニットの実体化（2026-08-04 Opus5）

### なぜ必要だったか
U2以前は、他魔王も人間の奪還軍も **「一番手薄な自領を遠隔から一撃で奪う」** 数値処理だった。
盤の上に何も現れないので、**防ぎようも読みようも無かった**（気づいたら領域が減っている）。

### `EnemyForce.cs`（新規）― 敵も盤の上を歩く
- **湧く**：他魔王は本拠地から `power×0.45` を切り出して軍にする（**出したぶん本体は減る**ので際限なく湧かない・同時2体まで）。
  人間の奪還軍は**自領に接した中立の土地**に湧く（どこから来るかが見える・同時2体まで）。
- **歩く**：毎ターン移動力2ぶん、目標へ近づく。**地形の踏破コスト**（S4）をそのまま使う。
- **🚧 支配地域(ZoC)**：**こちらの眷属に隣接したら足が止まる**＝眷属が壁になる。実測で足止めを確認。
- **攻城**：目標に隣り合ってから `atk vs DefenseOf` で殴る。勝てば占領してそこへ入り、負ければ削れて目標を選び直す。
  🏯 **迷宮の入口だけは地上の軍では落とせない**（そこは迷宮側の防衛戦で決着する）。
- **引き上げ**：道が塞がって3ターン動けない／壊滅寸前になると退き、他魔王の軍は本体に力が還る。

### 迎撃（こちらから叩ける）
敵軍のいるタイルを選ぶと帯が **『迎撃する』** に変わる（移動力1消費）。
勝てば**軍は消えて戦利品**（DP＝戦力×1.2・素材6・武勲3）、負ければ眷属が**2ターン負傷**。
※ 敵軍がいるタイルでは「攻撃する（占領）」は出さない。**まず野戦で軍を退けてから土地を獲る**という順番になる。

### 表示
盤に **×＝他魔王の軍（魔王の色）／＋＝人間の奪還軍**（眷属は◆・斥候は□）。
タイルの帯にも `カンタの軍 戦力279` と出る。

### 検証（error0）
軍が5体まで盤に出て歩く／眷属の隣で**足止め**（位置が変わらない）／攻城で領域が落ちる／
迎撃で `322 vs 279` → 勝ち・軍が消えて **DP+335**／敵軍タイルでは『攻撃する』が『迎撃する』に置き換わる。

### ⚠ 作業上の失敗（記録）
Pythonの一括置換スクリプトで **2つ目の置換が失敗した瞬間に例外**が飛び、
**ファイル書き込み（末尾の `io.open(...,'w')`）が実行されなかった**。1つ目の置換も含めて丸ごと失われたのに
「ok」が出ていないことを見落として先に進み、UIにボタンが出ない原因を探すはめになった。
→ **複数置換のスクリプトは、失敗しても書き込みが走る形にするか、1置換ずつ Edit で当てる。**

## 装備グレードの強化幅（2026-08-04 Opus5）

### 何が問題だったか（数字で）
旧テーブルは1段階が **+10〜15%** しかなく、レベルの伸び（`PerLevel = +4%/Lv`）に直すと **2〜3レベル分**。
数百DPを払ってレベル2つぶん、では**体感できないのが当たり前**だった（ユーザー指摘のとおり）。

### 直し：1段階を +22% に広げ、そのぶん高くした
| グレード | 武器 | 1段の伸び | 防具 | DP | 素材 |
|---|---|---|---|---|---|
|銅|×0.85|―|×0.88|140|0|
|鉄|×1.00|+18%|×1.00|300|0|
|鋼|×1.22|+22%|×1.18|560|0|
|銀|×1.50|+23%|×1.42|950|2|
|ミスリル|×1.85|+23%|×1.72|1,600|8|
|アダマンタイト|×2.30|+24%|×2.10|2,600|18|
|オリハルコン|×2.85|+24%|×2.55|4,000|32|

**1段階 ≒ 5.5レベルぶん**（実測）。最高位は 銅比 ×3.35（旧は×2.28）。
**銀以上は素材も要る**ので、DPだけでは最上位に届かない＝素材の使い道が1つ増えた。魔王の武具は素材も1.5倍。
UIの『強化＋』に**素材の必要量**を出し、ツールチップに `銀 → ミスリル ×1.50 → ×1.85` と**1段で何が変わるか**を明示。

### ⚠ 冒険者も同じ表を使う（両刃なので釣り合いを取った）
グレードの倍率を広げると、**逃がして装備を奪われるほど強くなる冒険者**も一緒に強くなる。
`GradeFromWorld` の傾きを下げて（`rank×0.45 + gear/35` → `rank×0.40 + gear/42`）**平均で1段下げた**。
実測の差（同じランク・装備水準での攻撃倍率）:

| | 装備水準10 | 50 | 100 |
|---|---|---|---|
|ランク1|+0%|+9%|+17%|
|ランク4|+9%|+17%|**+6%**|
|ランク7|+17%|**+6%**|**+12%**|

正直に書くと **冒険者も +6〜17% 強くなっている**（丸めの都合で凸凹する）。
ただしこちらは**任意のタイミングで投資して**同じ幅を取り返せるのに対し、あちらは世界装備水準まかせなので、
「鍛えれば追い越せる」関係になる。ここは通しプレイで様子を見る。

検証 error0。個体の武器を4段（なし→銀）まで鍛造し `×1.00 → ×1.50`、素材が2消費されることを確認。

## 調整4件（2026-08-09 Opus5）

### ① 迷宮生成パネルを撤去
生成の設定（タイプ/空間/宝箱/階層/地上の広さ）は**タイトルの『世界設定』で開始前に決める**形にしたので、
ゲーム中に作り直す口は不要になった。`BuildGenPanel(root)` の呼び出しを外した
（メソッド自体は残す＝デバッグで作り直したくなったとき用）。

### ② 拠点を築いたら周囲も自領になる（Civと同じ）
**原因**: `ReassignTerritory` は「**既に自領のタイル**」しか歩かない設計だった。
未支配の土地に拠点を築いても、自領はそのタイル1枚だけ。国境がじわじわ広がるのを待つしかなかった。
→ `SettlementSystem.ClaimAround(settlementId, radius)` を新設し、**中立の陸だけ**を取り込む
（他魔王の土地は勝手に取らない＝そこは軍で獲る）。
- 拠点を築く → 半径1／都市へ昇格 → 半径2／**盤の生成時の首都も半径2**
- 実測: 首都の版図が **1タイル → 15タイル**に。

### ③ 地上の眷属が育つようにした
**送り出した瞬間に成長が止まる**ので、格上の敵に一生勝てず「鍛えてから挑む」もできなかった。
経験値は**眷属本人と連れている配下（半分）**に入る。
| 何をしたか | exp |
|---|---|
| 進軍中（毎ターン） | +12 |
| 駐留（毎ターン） | +6 |
| 完勝 | `20 + 相手の防衛×0.08` |
| 辛勝 | 上の1.2倍（きわどい戦いほど糧になる） |
| 敗走 | 上の0.4倍（負けても少しは糧） |
| 野戦（迎撃）の勝敗 | 同上 |
| **鍛錬**（自領で腰を据える） | **+120**／`200+Lv×30` DP＋`4+Lv/5` 素材・**その turn は動けない** |

⚠ 毎ターンの自動分は**わざと微量**にした。ここを厚くすると「送り出して放置」が最適手になり、
迷宮を疎かにできてしまう（迷宮と地上のどちらも見る、という芯が崩れる）。
実測: 鍛錬1回で Lv10→11／5ターン駐留で +30exp／辛勝で +32exp。

### ④ 初手から地上で動けるように（初期眷属）
眷属化には Lv10＋進化Ⅰが要るので、**10ターンほど地上で何もできない**のに他魔王だけが版図を広げていた。
→ `KinRoster.GrantStarterKin()` で開始時に**真名を持つ配下を1体だけ**配る（Civの初期ユニット相当）。
`MinionRoster.TrySummonFree` を新設し、Lv10相当まで底上げして本拠に置く。以降の眷属は従来どおり条件を満たして作る。
実測: 開始時に『エルザ』Lv10・戦力89（隣の中立の守りが88〜146なので、**いきなり無双はしない**）。

## Phase A ― 「伝わる」層（2026-08-09 Opus5）

計画は [[game-polish-plan]]。**一番の問題は見た目ではなく「起きたことが伝わらない」こと**だった
（実測：`Debug.Log` 296件＝出来事の大半がコンソールにしか出ていない／音0件／セーブ無し）。

### A-1 通知トースト（`NotifySystem.cs` 新規）
- 右上に最大5件・7秒で消える。**Kindで色分け**（金=得た／赤=失った／橙=来ている／紫=物語／灰=ログのみ）
- **押すとその場所へ飛ぶ**（`JumpToRegion`＝地上モードに入って盤をそこへ寄せ、選択する）
- 直近50件を**ログウィンドウ**（上部HUDの『記録』）で遡れる。行を押しても飛べる
- ⚠ 何でも流すと「何も伝わらない」に戻るので、**Info はトーストに出さずログだけ**にした
- 差し込んだ出来事：制圧/辛勝/敗走/壊滅・レベルアップ・領域を奪われた/守り切った・敵軍の進発・奪還軍の出現・
  迎撃の成否・偉業・時代・災厄・研究完了・祝祭・宗主国・属性ポイント・発見・真核の奪取
- ⚠ トーストは**署名方式**で作り直す（毎フレーム作り直すと押下中にボタンが死ぬ既知の罠）

### A-2 ターン間レポート（『腹心の報告』に統合）
`EndBattlePhase` は地上の全処理を**1フレームで終える**ので、これまで誰も何が起きたか見ていなかった。
報告の先頭に **「前のターンに起きたこと」** を足した：
- **資源の増減**（DP/素材/研究点/名声を前ターン終わりとの差分で）
- 前ターンの出来事を最大8件（**押すとその場所へ飛ぶ**）
これで「前ターンの結果 → 今の情勢 → 進言」の1枚になった。

### A-3 盤のフローティングテキスト（`SurfaceView.PopText`）
移動 `-2`／攻撃 `完勝`／迎撃 `撃破！`／鍛錬 `+120 exp`／築城 `拠点を築いた` をその場に浮かせる。
⚠ `unscaledDeltaTime` で動かす（戦闘の倍速/一時停止に引きずられないため）。

### A-5 戦闘の速度制御（‖ / 1x / 2x / 4x）
3分の防衛戦をただ見ている時間が長すぎた。下部バーに4ボタン。
- `Time.timeScale` を切り替える。**準備フェーズでは常に等速**（止めても意味がないので）
- 戦闘開始時に選んでいた速度を適用し、内政に戻ったら等速へ（選択自体は覚えておく）
- ⚠ UIの演出（トースト・フロートテキスト・descentフェード）は**すべて unscaled** で動かしてある

検証 error0: トーストが色分けで積まれる／敵軍の進発が実際に通知される／速度ボタンが準備中は timeScale を変えない／
ターン2の報告に `DP +1,240 素材 +9 研究点 +5` と前ターンの6件が並ぶことを目視。

## Phase B ― UIの基礎工事＋見切れの修正（2026-08-09 Opus5）

### 🔧 上下バーの見切れ（原因を数えて特定）
思い込みで直さず、`SizeElem` の幅を合計して測った：
- **上部バー：必要 2,236px（画面1920）→ 316px はみ出し**。右端の資源チップが切れていた
- **下部バー：必要 2,084px → 164px はみ出し**。『侵略開始』が切れていた

直し（**構造から**）：
| 対策 | 節約 |
|---|---|
| 上部から**作品名を撤去**（ゲーム中に要らない。タイトル画面にだけ置く） | -300 |
| 資源チップを **118→86px**（アイコン＋縦2段に組み直し） | -192 |
| メニューボタン 66→58／ターンピル 250→228 | -86 |
| ツールボタン 108→92／**デバッグ用『冒険者(検証)』を撤去** | -236 |
| 間隔 14→10・10→8、『戦闘時間+1分』→『時間+1分』104px | -160 |

さらに **`FitBarWidth()` という安全網**を入れた。バーを組み終えたあとに必要幅を測り、
はみ出していたら各要素を比例で詰める。**今後ボタンを足しても見切れない**。
実測: 上部 1,580px ／ 下部 1,678px（どちらも1920に収まる）。

### 🎨 `UITheme.cs`（新規）― 規則を1箇所に
UIが素人っぽく見える原因は装飾ではなく**規則の不在**だった。
- **面を3段の明度**に（背景 #0b0910 / 窓 #1a1726 / カード #12101b）＝奥行きが出る
- **余白は8pxグリッド**（S1..S5）。7,9,12,14,26 のような半端を使わない
- **文字は4段**（H1 22 / H2 16 / Body 13.5 / Small 11.5）
- **意味の色**（DP=金・素材=青緑・研究=青・感情=紅・名声=赤・威名=紫・食料=緑・警告=橙）を定義

### 🖼️ `UIIcons.cs`（新規）― アイコンを手続き生成
資源アイコンが無くHUDが文字だけだった。**素材を待たずに埋める**ため、
コイン／インゴット／本／ハート／旗／警告三角／グリッド／星／葉／二重丸／人／足あと を
64pxで**手続き生成**（3×3スーパーサンプリングで縁を滑らかに）。白で描き、`Image.color` で意味の色に着色。
⚠ 記号（◆＋×…）で代用すると**フォントに無い字が□になる**問題を何度も踏んできたが、**絵にすれば根治**する。

### ✨ トランジション
- **パネルのフェードイン** 0.14秒（開閉が一瞬で切り替わると安っぽく見える）
- **数値のカウントアップ** 0.45秒（DP/名声/素材）。初回だけ即決め（開幕に0から数え上げない）
- ⚠ どちらも **`unscaledDeltaTime`**（戦闘の倍速・一時停止に引きずられないため）

### まだ残っているB
- `UIKit.cs` への**部品の切り出し**（`GameUIManager` 4,700行の分割）
- 日本語フォントの**アセット化**（今はOS動的フォント）
- Bloodlinesスキンの**全パネル適用**（今は16箇所）

## Phase C ― 盤の絵（2026-08-09 Opus5）

地上の盤が「フラットな色面＋小さい文字」だったので、**一目で読める絵**に変えた。
実装はすべて既存の1枚メッシュに乗せた（[[SurfaceView]]／`HexTileArt` のアトラスを 8→13セルに拡張）。

### C-11 ユニットを文字から絵へ
`◆□×＋` の記号で描いていたが、**フォントに無い字は□になる**うえ小さくて読めなかった。
アトラスに**盾（眷属）／矢（斥候）／角のある菱形（敵軍）**を焼き、白で描いて色で意味を出す：
- 眷属 … 緑=待機／金=進軍中／灰=負傷
- 斥候 … 青（戦えない）
- 敵軍 … その魔王の色／人間の奪還軍は白
同じタイルに複数いるときは横に並べる（最大3体）。

### C-12 国境線（Civの国境）
**面の色だけでは版図の形が読めなかった**。所有者が違う隣を持つタイル＝国境なので、
そこに**ヘクスの縁の帯**を所有者の色で重ねる。これで「自分の領地がどこまでか」が一目で分かる。

### C-13 移動範囲のプレビュー
`KinRoster.ReachableNow(k)` を新設。**地形の重み**で幅優先に広げ、**敵領は通れない**のでそこから先へは伸びない。
選択中の眷属が今ターン行ける範囲を薄い枠で出す。選択タイルは金の枠。
⚠ 最初はアルファ150で出したら**盤が白く埋まって逆に読めなくなった**ので70まで落とした。

### まだ残っているC
- **C-14 敵軍の移動アニメ**（いまはターン解決後に瞬間移動する）
- **C-15 迷宮側の絵**（オートタイル・影・ヒットストップ・ダメージ数字）

### C-14 敵軍の動きを見せる（2026-08-09）
ターン解決は一瞬で終わるので、**敵軍は瞬間移動しているようにしか見えなかった**
＝「じわじわ近づいてくる」怖さも、迎え撃つ判断の余地も伝わっていなかった。
- `Army.prevRegionId` にターン開始位置を覚えておく
- **地上を開いたときに、前ターンの移動を1.1秒かけて再生**（`SurfaceView.PlayEnemyReplay`）
- 再生中の軍はタイルに紐づけず、出発地→現在地を補間した位置に描く（`DrawMovingArmies`）
- ⚠ **見えていない場所の動きは見せない**（`IsSeen` で両端を確認）
- 補間は smoothstep で最初と最後をゆるめる

### C-15 ダメージ数字（`FloatText.cs` 新規）
これまで冒険者は `💥HP:1234`（＝**残りHP**）を1つの TextMesh で出しているだけだった：
- **与えたダメージが分からない**（罠が効いているのか読めない）
- 1体に1つしか出せず、**連続で殴ると前の表示が消える**
- 絵文字はフォントに無いと □ になる
- **防衛体側は何も出ていなかった**ので、戦闘が棒立ちに見えていた

→ プール式のワールド空間フロートテキストを新設。
- **ダメージが大きいほど文字も大きい**（`2.2 + log10(1+v)*0.9`、上限5.0）＝効いているのが一目で分かる
- 罠/会心は金、通常は赤、防衛体の被弾は橙
- 出る瞬間に少し弾ませ、消え際に薄くする
- ⚠ こちらは **`deltaTime`**（戦闘の一部なので倍速なら速く、停止なら止まるのが正しい）。
  UIのトーストが `unscaledDeltaTime` なのと**逆**。

※ 影は `CharacterVisual` に既にあった。**迷宮の壁のオートタイル**だけ未着手（16変種の絵が要るので別枠）。

## Phase D ― 戦闘フェーズの能動性（2026-08-09 Opus5）

**『侵略開始』を押したあと、実質何もできなかった**（実測で14箇所が「準備フェーズのみ」で禁止）。
3分間ただ眺めるだけでは、どれだけ内政を作り込んでも**ゲームとしては薄い**。

### 📯 魔王の号令（`CommandSystem.cs` 新規）
**DPを払い、クールダウンで待つ**4つの手。連打できないので「いつ切るか」が判断になる。

| 号令 | DP | CD | 効果 |
|---|---|---|---|
| 治癒の号令 | 300 | 45s | 全防衛体のHPを30%回復 |
| 落石 | 350 | 35s | **冒険者が最も密集している所**に (魔力+1)×70 ダメージ |
| 魔王の一撃 | 500 | 70s | **最も強い冒険者**に (魔力+1)×180 ダメージ |
| 恐慌の波 | 450 | 60s | 侵入中の冒険者を**全員帰らせる**（感情は清算＝原作の泳がせと噛み合う） |

- 効果は既存の仕組みに乗せた（`HealFromAlly`／`TakeDamage`／`ForceRetreat`）＝新しい戦闘ルールを増やさない
- **魔力ランクが効く**ので、魔王のステ振りが戦闘中の手札に直結する
- ⚠ クールダウンは **`Time.deltaTime`**（**倍速なら早く回復する**のが直感に合う。UI演出が unscaled なのとは逆）
- ウェーブごとにリセット（溜め込んで次のターンに持ち越せない）
- UIは画面下中央に4枚。⚠ **中身は作り直さず値だけ更新**（作り直すと押下中にボタンが死ぬ既知の罠）

### ⚠️ 危険の可視化（D-18）
上部バーに戦闘中だけ `侵入 5　最強 Lv12　B2F` を出す。準備中は空。

### 🐛 フェードが進まずパネルが透明のまま残った
`Time.unscaledDeltaTime` が **0 を返す状況があり**（エディタが描画を進めていないとき等）、
`PlayFadeIn` した号令バーが **alpha=0 のまま画面に出なかった**。
→ ①開始アルファを 0 ではなく **0.25**（最悪でも「薄いが見えている」）
   ②1フレームの進みを `Mathf.Max(unscaledDeltaTime, 1/120)` で**必ず前へ進める**
**「演出が止まると機能が消える」作りにしない**、という一般則として記録。

### 残り
- D-17 緊急配置（戦闘中に罠を1つ置く）は**『落石』が同じ役割**（DPを払って戦況に介入する）を果たすので保留。
  タイルを指定して置く形が要るなら別途。

---

# セッション記録（2026-08-09・/compact 前）

## このセッションで入ったもの（コミット順・16件）
```
458467d タイトル画面・世界設定（初期DP＝予算−建造費）
a746301 ターン頭の物語ガイド＋進軍バグ修正・帯からの進軍・盤に眷属を表示
92b5c31 U1: 地上ユニットの手動移動・即時攻撃・視界
79b2f89 S1: 政体と政策スロット
fd88df1 S2+S3: 属性ツリーとレガシーの道
5f23656 S4: 探索（地形コスト・発見イベント・斥候）
a349d99 S5: 施設の陳腐化と改築＋資源の割り当て
a20eaef S6: 独立勢力の段階化・宗主国外交・粉砕＋危機の対抗策
96c4f7f U2: 敵ユニットの実体化（進軍・ZoC・攻城・迎撃）
04a0f6f 装備グレードの強化幅を1段+22%に
b21c6dd 調整4件: 生成パネル撤去／拠点の版図/眷属の成長/初期眷属
10766fc Phase A: 通知トースト・ターン間レポート・盤のフロートテキスト・戦闘速度
5673417 Phase B: 上下バーの見切れ修正＋UITheme/UIIcons/トランジション
f10e22b Phase C: 盤の絵（ユニットのスプライト化・国境線・移動範囲）
7277543 Phase C 仕上げ: 敵軍の移動再生とダメージ数字
dc4a9cf Phase D: 魔王の号令（戦闘中の手）と危険の可視化
```

## 新設したファイル（このセッション）
`GameSetup` `GuideSystem` `PolicySystem` `AttributeSystem` `DiscoverySystem` `ScoutSystem`
`EnemyForce` `NotifySystem` `UITheme` `UIIcons` `FloatText` `CommandSystem`

## ⚠ 会話にしか無かった重要事項（全部ここに転記）

### 1. 時間の使い分け（**間違えると壊れる**）
| 対象 | 使うもの | 理由 |
|---|---|---|
| UIの演出（トースト・パネルのフェード・盤のフロートテキスト・敵軍の移動再生） | **`unscaledDeltaTime`** | 戦闘の倍速/一時停止に引きずられてはいけない |
| 戦闘の一部（ダメージ数字・号令のクールダウン） | **`deltaTime`** | 倍速なら速く、停止なら止まるのが正しい |

### 2. 🐛 演出が止まると機能が消える作りにしない
`Time.unscaledDeltaTime` が **0 を返す状況がある**（エディタが描画を進めていないとき等）。
`PlayFadeIn` した号令バーが **alpha=0 のまま画面に出なかった**。
→ 開始アルファを **0.25**（最悪でも薄く見える）、1フレームの進みに **下限 1/120秒**。

### 3. 📏 バーの見切れは「数えて」直す
`SizeElem` の幅を合計したら 上部 **2,236px** / 下部 **2,084px**（画面1920）だった。
個々を詰めたうえで **`FitBarWidth()`**（組み終わりに必要幅を測って比例で詰める）を安全網に入れてある。
**今後ボタンを足しても見切れない**。

### 4. 🖼️ 記号で代用しない
`◆ □ × ＋ −` などは **UIフォントに無いと □ になる**（何度も踏んだ）。
→ `UIIcons`（UI用）と `HexTileArt` のオーバーレイセル（盤用）で**絵にして根治**した。
全角マイナス `−` も消えるので **半角 `-`** を使う。

### 5. 🧰 UIの作り直しは「署名方式」
毎フレーム作り直すと**押下中に Button が破棄されてクリックが成立しない**。
トースト＝`NotifySystem.Signature`／号令バー＝**中身は作らず値だけ更新**／ストリップ＝配置の署名。

### 6. 🗺️ 盤を作り直すときのチェックリスト（2度やらかした）
`SurfaceMap.Regenerate` は **盤の id を握っている側を全部作り直す**こと。
いま呼ぶもの: `RivalLords / DiplomacySystem / ScoutSystem / DiscoverySystem / EnemyForce` ＋ `KinRoster.FixStrayPositions()`。
- 1度目: `Kin.regionId = 0`（＝盤の左上の海）で進軍が毎ターン取り消されていた
- 2度目: 独立勢力が**海タイルに立って『働きかけ』が永久に失敗**していた

### 7. 🔧 ツール運用
- **複数置換のスクリプトは1件ずつ書き込む**。まとめて最後に書くと、途中で例外が出た瞬間に**全部消える**（実際に踏んで、UIにボタンが出ない原因を探す羽目になった）
- Pythonヒアドキュメント経由でC#を書くと **`\n` が実際の改行になって CS1010**。長い文字列を含む編集は **Edit ツール**で当てる

### 8. ⚠ `const` の罠（2度）
`SquadMaxSlots`／`EurekaTracker.Discount` が const で、研究や政策が**一生反映されなかった**。
**状態で変わる値を const にしない**。「効かない」を疑うときは**まず const を探す**。

## 次にやること ― Phase E（[[game-polish-plan]]）
**19 セーブ/ロード ／ 20 音 ／ 21 設定画面**。中でもセーブが最優先
（1プレイ数時間なのに中断できないのは製品として致命的）。

### セーブの設計メモ（着手時の指針）
- **全systemが static**なので、各systemに `ToJson()/FromJson()` を足して1ファイルにまとめるのが素直
- 保存が要る static: `SurfaceMap`(盤＋seen) `SettlementSystem` `DistrictCatalog`(タイル側に保持) `KinRoster`
  `ScoutSystem` `EnemyForce` `DiplomacySystem` `RivalLords` `EraSystem` `PolicySystem` `AttributeSystem`
  `ResearchState` `EurekaTracker` `MinionRoster` `MinionEvolution` `TrainingSystem` `NarrativeSystem`
  `DiscoverySystem` `GuideSystem` `NotifySystem` `LureEconomy` `GameSetup`
  ＋ MonoBehaviour側: `DungeonResourceManager` `DungeonTurnManager` `DemonLord` `DungeonFloorManager`(各階のFloorData＋配置)
- **地上4,500タイルは差分だけ保存**すれば軽い（生成は seed で再現できるので、`owner/settle/pop/district/resource割当/seen` などの
  「生成後に変わった値」だけを持てばよい）
- 迷宮は `FloorData`（map/entrance/boss/size）＋ `features`（配置物）＋ 個体IDの対応

---

# Phase E ─ 製品として必須（2026-08-09）

**19 セーブ/ロード ／ 20 音 ／ 21 設定画面**。面白さは増やさないが、
**1プレイ数時間なのに中断できない・無音**は製品として通らないので避けて通れない層。

## 💾 E-19 セーブ/ロード（`SaveSystem.cs` 新規）

### なぜ「各systemに ToJson/FromJson」にしなかったか
保存が要る状態は **20以上のクラス**に散っている。手書きすると 1,500行を超えるうえ、
**フィールドを1つ足すたびに書き忘れて壊れる**。
→ **静的フィールドをリフレクションで丸ごと写し取る**方式にした。

### 保存する / しないの決まり（⚠ 新しい state を足すときはここを守る）
| 書き方 | 扱い |
|---|---|
| `private static List<Kin> all;` | **保存される**（ふつうの状態） |
| `private static readonly PolicyDef[] policies = {...}` | **保存されない** ＝ **カタログの目印** |
| `const` / `[NonSerialized]` / `UnityEngine.Object` 由来の型 | 保存されない |

`readonly` をカタログの印にしたのは、**古いセーブが新しいバランス調整を上書きする事故**を
1語で防げるから。既存コードのカタログはほぼ全部 `static readonly` だったので、追加の記述がほぼ要らなかった。

### ファイルの形（自己記述型）
先頭に**型表**（フィールド名と型の一覧）を書く。読む側は**ファイル自身の情報だけで復号**するので、
- 後からフィールドを足しても**古いセーブが読める**（無い物は捨て、増えた物は既定値のまま）
- システムを丸ごと足しても・消しても読める

本体は GZip。**4,503タイルの世界で 121KB／保存 94ms・読込 172ms**。

### 復元の順番（⚠ ここを間違えると魔王が消える）
1. 静的システム → 2. シーンの管理者（魔王を除く） → 3. **迷宮を組み直す**（`RebuildAfterLoad`）
→ 4. **そのあとで魔王**（組み直しで作り直されるため） → 5. `ISaveHook.OnAfterLoad` → 6. 盤に汚れ印

### 🪝 ISaveHook（単純な写し取りで足りないとき）
`EmotionTreeManager` はノードの中に **`Func<bool>`（保存できない）と解放フラグが同居**していた。
ノードごと保存すると Func が null になって落ちるし、カタログの文言も古いまま復活する。
→ ノードは `[NonSerialized]`（＝生きている中身をそのまま使う）、**解放フラグだけ**別の入れ物に移す。

### 🐛 `readonly` の罠（この方式の弱点を最初に踏んだ）
`DungeonFloorManager.floors` が `private readonly List<FloorData>` だったせいで、
**迷宮そのものが保存されず、タイトルから読み込むと0層**になった。
→ 🛎️ **安全網**：`実体の readonly なコレクション`を見つけたら警告を出す
（カタログはほぼ `static readonly` なので誤検知しない。意図して保存しない物は `[NonSerialized]`）。

### UI
- 3スロット＋**オートセーブ**（ターンの頭で自動）。**保存は準備フェーズのみ**
  （戦闘中の場を保存すると「戦いの途中から再開」を作り込むことになる。Civも1手ごと）
- タイトルに『続きから』、上部バーに『保存』
- ⚠ 『続きから』は**押した先で**セーブの有無を見せる。ここで灰色にすると、
  あとから保存してタイトルへ戻ったとき押せないままになる

## 🔊 E-20 音（`SoundSystem.cs` 新規）
**AudioClip 0件**＝BGMもSEも一切無かった。[[UIIcons]] と同じ判断で**計算で作る**
（素材の調達を待つと、いつまでも無音のまま）。

- **効果音17種**をその場で焼いてキャッシュ。実測 peak 0.18〜0.49・**割れなし**
- **BGMは `PCMReaderCallback` で流しながら合成**。準備62／戦闘116／地上74 BPM、
  Aマイナーの4和音進行（Am-F-C-G）＋アルペジオ、戦闘だけ鼓動。
  16秒ループを何百回も繰り返すと耳が死ぬので、**鳴らし続ける**方式にした。メモリも食わない
  - ⚠ コールバックは**音のスレッド**で走る。中で `new` しない・Unity API を触らない
  - 実測 peak 0.55/0.84/0.59・**clipped 0**・直流成分ほぼ0
- 鳴らす場所は**元から一本化されている所**に挿した＝**1箇所で広く効く**
  `NotifySystem.Push`（種類別）／`PrimaryButton`（全ボタン）／`FloatText.Damage`（打撃）
  ＋ 配置・撤去・号令・侵略開始・ターン頭・発見・保存
- 同じ音の連打で割れないよう、**種類ごとに最短間隔**（打撃45ms・撃破70ms・クリック30ms）

## ⚙️ E-21 設定画面
音量3つ（全体／BGM／効果音・`PlayerPrefs`）＋腹心の報告の表示＋タイトルへ戻る／終了。
- ⚠ 設定は**専用のCanvas**（order 320）に置く。タイトル画面からもゲーム中からも開くので、
  迷宮のCanvasに置くとタイトル表示中に消え、タイトルのCanvasに置くとゲーム中に消える
- uGUI の Slider は部品（背景／伸びる面／つまみ）を自前で組む必要がある

## 📏 上部バー
`保存`『設定』を足しても収まる（Spacerに余裕が残り、右端の資源チップは見切れない）。
⚠ バーの余裕を測るときは **Spacer（伸縮）を固定幅と一緒に足さない**。合計だけ見ると
はみ出していないのに「はみ出した」と読み違える。

## 残り
- **F**（22 難易度 23 統計/戦績 24 実績 25 周回ボーナス 26 デイリーシード）
- Bの残り（`UIKit` 分割・日本語フォントのアセット化・スキン全適用）、Cの残り（迷宮の壁のオートタイル）

---

# Phase F ─ リプレイ性（2026-08-09）

**22 難易度 ／ 23 統計・戦績 ／ 24 実績 ／ 25 周回ボーナス拡張 ／ 26 デイリーシード**。
「もう1周やりたい」を作る層。**A〜F の計画はこれで完走**。

## ⚖️ F-22 難易度（`Difficulty.cs` 新規）
安寧／標準／苛烈／絶望の4段。**仕組みそのものは変えない**。動かすのは掛け算4本だけ：
冒険者の**伸び**と**人数**、他魔王の**伸び**、こちらの**取り分**。
研究の値段・建造費・配置枠は据え置き＝**同じ攻略が同じように通じる**。
（[[difficulty-curve-orders]] の「掛け算の軸を増やさない」に従った）

| | 安寧 | 標準 | 苛烈 | 絶望 |
|---|---|---|---|---|
| 敵Lv（T30/名声5000） | 35 | 43 | 51 | 60 |
| 人数 | ×0.80 | ×1.00 | ×1.15 | ×1.30 |
| 他魔王 | ×0.70 | ×1.00 | ×1.25 | ×1.55 |
| 取り分 | ×1.15 | ×1.00 | ×1.00 | ×1.12 |
| スコア | ×0.6 | ×1.0 | ×1.5 | ×2.2 |

⚠ **伸びにだけ掛ける**（初期値の1は動かさない）。序盤から別ゲームにしないため。

## 📊 F-23 戦績とリザルト（`RunStats.cs` 新規）
**以前はゲームが終わると `GAME OVER` の4文字が出るだけで、ボタンが1つも無かった**
（＝閉じることすらできない）。何をどこまでやったのかも残らない。

- **周の記録**：数えないと分からないものだけ数える（撃破・逃走・波・最深・最大版図・号令・稼いだDP）。
  領地/研究/眷属/スコアは**持ち主から読む**＝二重に数えない
- **通算**：`PlayerPrefs`。⚠ セーブ([[SaveSystem]])は1周の中身しか持たない。層が違う
- **スコア＝素点 × 難易度 × 早さ（× 勝利1.5）**
  ⚠ **早さの係数**（25T以内で満点／100Tで0.6倍）を入れないと「粘るほど高い」になり、**勝ち急ぐ理由が消える**
- リザルトは勝敗どちらでも同じ画面。『もう一度』『タイトルへ』を置いた

## 🏅 F-24 実績（`Achievements.cs` 新規）
29種（うち隠し2つ）。条件は `Func<bool>` を**ターンの頭と周の終わりにだけ**見る（常時監視しない＝安い）。
解除は `PlayerPrefs`。**解除12個で形見の枠が2→3**に増える＝周回の見返り。

## 🕯️ F-25 形見 8→16種
追加分の条件は**実績と揃えた**（実績を取れば形見も付いてくる＝目標が二重にならない）。
効果は既存の掛け算に挿しただけ（来訪・素材・移動力・研究費・他魔王の伸び・開始ターン・感情・開始資源）。

## 📅 F-26 日替わりの世界
日付から種を決め、**広さ/難易度/迷宮タイプまで固定**する（記録を比べるため）。最高スコアを日付ごとに残す。

## ⚠ 踏んだ罠
- **`const` の罠3度目**：`NarrativeSystem.Slots` が `const int = 2`。枠を実績で増やすので**プロパティ化**。
  （1度目 `SquadMaxSlots` / 2度目 `EurekaTracker.Discount`）
- **形見をセーブに入れてはいけない**：周を越える持ち物なので、別の周のセーブを読むと解禁が上書きされる。
  さらに枠2→3の後に**古いセーブの長さ2の配列**が入り込んで範囲外になる。→ `[NonSerialized]`＋長さの補正。
  同じ理由で `Achievements` もセーブ登録しない（`RunStats` は1周のものなので登録する）
- **リザルトは専用Canvas(330)**。迷宮のCanvasに置いたら、あとから開く『腹心の報告』が上に重なって
  **結果が読めなかった**。`SetAsLastSibling` は、その後で誰かが同じことをすれば負ける
- `BackToTitle` でリザルトを畳む（タイトルより上のCanvasなので、残るとタイトルに触れない）
- 🧪 テストで `CanvasGroup` の alpha を全部1にすると、隠れている全画面の暗転板(`FloorFade`)まで出てくる。
  **画面が真っ黒／変な色になったらまずこれを疑う**（2回引っかかった）

## Phase A〜F 完走後に残っているもの
- Bの残り：`UIKit` への部品切り出し（`GameUIManager` は5,000行超）・日本語フォントのアセット化・スキン全適用
- Cの残り：迷宮の壁のオートタイル（16変種の絵が要る）
- **通しプレイでのバランス確認**（難易度4段 × 装備グレード拡張 × U2の敵軍）

---

# 迷宮の見た目を作り直す（2026-08-09）

## 🔍 診断：**壁が1枚も無かった**
床タイルだけを置き、床でない所には何も描いていなかった。つまり**カメラの青がそのまま見えていた**。
「本格的なダンジョンに見えない」原因は、色でも解像度でもなく **壁という物体が存在しないこと**だった。

## 使った素材
`Dungeon Tale`（16px・185スプライト）。**Wall が RuleTile（18ルール）付き**だったので、
Phase C の宿題「迷宮の壁のオートタイル（16変種の絵が要る）」が**絵を1枚も描かずに終わった**。
実行時に読むため `Assets/Resources/DungeonTale/` へ `AssetDatabase.MoveAsset`（GUIDごと動くので参照は無事）。

- `DungeonTale.cs` … 名前でスプライトを引く入口
- `DungeonTilemapView.cs` … 床／血糊／壁／小物の4層を Tilemap で描く。外周14マスまで岩で埋める
- カメラ背景を **Unityの青 → ほぼ黒**（`CameraController.Awake`）
- 既存の入力・当たり判定は**配列の計算**なので、見た目だけ差し替えれば済んだ。
  マスのGameObject（`RoomData`の器）は残し、宝箱/罠だけアトラスの絵に差し替え

## ⚠ 落とし穴（全部実際に踏んだ）
1. **RuleTile は色をロックしていて `tilemap.color` が無視される**。
   マスごとに `SetTileFlags(pos, TileFlags.None)` → `SetColor(pos, c)` が要る。
   自前の `Tile` は `tilemap.color` で効くので、**片方だけ効かない**という分かりにくい形で出た
2. **掛け算では色を足せない**。`Floor_A..D` は青緑なので何を掛けても青緑のまま。
   **灰色の素材（`Floor_Metal`）を選ぶ**と掛けた色がそのまま出る
3. `Decal_*` は床の汚れではなく**血糊と赤いマーカー**（X・矢印・魔法陣）。22%で撒いたら記号だらけ → 5%。
   `Decal_Shade*` は壁の影ではなく**キャラの足元の影**（黒い塊が並んだ正体）
4. **素材は名前から想像せず、並べて目視してから選ぶ**。上の3つは全部それで外した。
   一時オブジェクトでスプライトを格子に並べてスクショするのが速い

## 決まった色
壁 `(0.30,0.26,0.42)` ／ 床 `(0.56,0.50,0.68)` ／ 小物 `(0.80,0.72,0.86)` ／ 血糊 alpha 0.55

## HUD
迷宮をピクセルアートに寄せた以上、HUDだけ手続き生成の図形だと**絵の言語がちぐはぐ**になるので、
`UIIcons` は Dungeon Tale の Item スプライト（宝石/槌/本/心臓/剣/盾）を優先し、無ければ手続き生成に落とす。

## まだ残っているもの
- **Phase B の残り**：`UIKit` への部品切り出し（`GameUIManager` 5,000行超）／日本語フォントのアセット化／
  Bloodlinesスキンの全パネル適用
- 迷宮側のUIの作り込み（枠・ボタンの質感）／配下と冒険者の見た目の統一（今はSPUM/GDD/EnemyGaloreが混在）

---

# セッション記録（2026-08-09 その2・/compact 前）

## このセッションで入ったもの（コミット順）
```
b2df0ca 迷宮の見た目を Dungeon Tale で作り直す（Cの残り・壁のオートタイル）
1bcc17a HUDのアイコンを同じ素材のピクセルアイコンへ
2501f47 日本語フォントの同梱と、Bloodlinesスキンの実適用（Bの残り）
de497cc docs: 配下スプライトの発注書
1eef3bf 配下スプライトをPixelLabで作り直す（不死12＋魔族4）
2040b7b 配下34種すべてに固有の姿を割り当てる
f071a9c 配下のコマ送りアニメを再生できるようにする（骸骨8状態で実証）
c5f06de アニメ：ゾンビとゴブリンに idle/walk/hit/death を追加（3/34種）
```

## 🩸 Bloodlinesスキン：**呼ばれていたのに素通りしていた**
`SkinPanel`/`SkinButton` は18箇所で呼ばれていたのに、スプライトが `[SerializeField]` の
**未割当**で全部 `null` チェックを抜けていた。ボタンは特にフラット色のまま。
→ 他の素材と同じく **Resources から自分で読む**方式に変更（`LoadSkin()` を `Start` の先頭で）。
⚠ ボタンのpngは**スプライトモードが Multiple** なので `Resources.Load<Sprite>` は null。
   `LoadAll<Sprite>` の先頭を取ること。

## 🈶 日本語フォント
OSのフォント（Yu Gothic UI 等）から動的生成していた＝**配布先で別の字になる／無い**。
Noto Sans JP（SIL OFL・`Assets/Fonts/OFL.txt` 同梱）を `Resources/Fonts/` に置いて読む。
⚠ 日本語は7,000字超なので**静的アトラスにしない**。`AtlasPopulationMode.Dynamic`。

## 🎨 PixelLab（MCP）で配下34種を作り直した

### 導入でつまずいた点
- `claude mcp add` は既定で**実行したフォルダ**に紐づく。管理者コンソール（`C:\WINDOWS\System32`）で
  実行すると、プロジェクトから見えない所に登録される。**必ず `--scope user`**
- 登録しても**セッション開始時にしか読まれない**ので、Claude Code の**再起動が必要**
- ⚠ APIトークンが `~/.claude.json` に平文で入る

### 費用の構造（ここを外すと破産する）
| モード | 1体 | スタイル一致 |
|---|---|---|
| standard | **1** | ❌（テンプレート生成。chibiプリセットでも5頭身のまま＝別の絵の言語） |
| v3 | 2〜9 | 参照画像で回転のみ |
| pro | **20〜40** | ✔ `style_character_id` |
| アニメ（テンプレート） | **1/方向** | — |
| アニメ（pro） | 20〜40/方向 | — |

**並列実行は8ジョブまで**。超えると rate limit。

### 🔑 既存の絵柄を持ち込む2段構え（これが肝）
1. `Char_Skeletone`(14x21) を **v3 の reference** にして8方向キャラ化（= STYLE BASE）
2. その ID を `style_character_id` にして pro モードで各種を生成
→ 太い暗色の輪郭・少ない色数・ずんぐりした頭身が受け継がれる。
⚠ v3 の reference は**出力32px以上が必須**（14x21をそのまま渡すと弾かれる。`size=32` を明示）

### ⚠ URLの構造（往復を減らす鍵）
- 立ち絵 `.../<character_id>/rotations/<dir>.png?t=1`
  **`?t=` は署名ではなくキャッシュ避け**。値は何でもよい＝**対応表のIDだけで一括ダウンロードできる**
  （`get_character` を34回呼ばずに済む）
- アニメ `.../animations/<anim_uuid>/east/<n>.png`
  ⚠ **`anim_uuid` は group_id とは別物**で対応表から組み立てられない。
  各キャラで `get_character` を呼んで拾う必要がある＝ここだけ往復が減らせない

### ⚠ 実寸の正規化
生成物は**キャンバスが36〜60pxとバラバラ**。そのまま置くと種類ごとに3〜4タイル分の背丈になる。
`CharacterVisual.InitDungeonTale` で**絵の高さを基準に正規化**して常に1.35ユニットに収める。

### 四足
`body_type=quadruped` ＋テンプレート（bear/cat/dog/horse/lion）も `style_character_id` と併用可。
狼=dog／鼠=cat／大獣・ベヒーモス=bear／ダイアウルフ・フェンリル=lion。
蝙蝠・ハーピー・セイレーンは翼持ちなので humanoid の方が近い。

## 🎬 アニメの再生側（Animatorを使わない理由）
34種×8状態＝272個の `AnimatorController` を管理することになるのに対し、やりたいのは
**「PNGを順に差し替える」**だけ。`Resources` から連番を読んで自前で回す方が軽い。
- `MinionAnim`：`Anim/<id>/<state>/<n>.png` を**連番が途切れるまで**読む（コマ数は状態ごとに違う）
- `CharacterVisual`：移動量から待機/歩き/走りを自動選択。被弾と死亡にも接続
  ⚠ **1回きりの再生（被弾/跳躍/振り向き）の最中は移動判定に横取りさせない**
  ⚠ 進めるのは **`deltaTime`**（戦闘の一部＝倍速なら速く動くのが正しい）
- 絵が無い種・状態は**1枚絵のまま**（作りかけでも壊れない）

実測コマ数: idle 4／walk 6／run 6／hit 6／death 7／crouch 5／air 9／turn 7

## 📋 残っている作業（次セッションはここから）
**アニメ 31種 × idle/walk/hit/death**（3/34完了）。手順:
1. 2体ぶんの4アニメを投入（8ジョブ＝並列上限）
2. `get_character` で `anim_uuid` を拾う
3. `bash docs/fetch-anim.sh <種id> <キャラid> idle:<uuid>:4 walk:<uuid>:6 hit:<uuid>:6 death:<uuid>:7`
4. Unityで取り込み設定（Point / PPU16 / 非圧縮）を当てる
対応表と残り一覧: `docs/sprite-manifest.json`

その後: run/crouch/air/turn（第2段）／Phase B の `UIKit` 分割／通しプレイのバランス確認

---

# 2026-08-10 ｜ 配下34種のアニメを完走（idle/walk/hit/death・859枚）

前セッションの残り31種を片付けた。2体ずつ（8ジョブ＝並列上限）投入 → `get_character` で
`anim_uuid` を拾う → `docs/fetch-anim.sh` で回収、を16回まわした。
コミット `265255a`。生成の消費は約124（テンプレート1／方向・v3も60x60なら1）。

## ⚠ 四足で判明したこと（humanoid とはまるで別物）
| | humanoid | quadruped |
|---|---|---|
| 使えるテンプレート | 48種（breathing-idle, taking-punch, falling-back-death …） | **10〜20種のみ**。humanoid のものは**1つも使えない** |
| hit / death | `taking-punch` / `falling-back-death` | **どのテンプレートにも無い** → `mode="v3"` の `action_description` |
| idle の名前 | `breathing-idle` | `idle`。ただし **bear だけ `idle` が無く `idle-long`** |
| idle のコマ数 | 4 | dog=8 ／ cat=8 ／ lion=9 ／ **bear=17** |

- v3 の hit/death は 60x60・`frame_count=6` で **1生成/方向**。`keep_first_frame` が既定 true なので
  **出力は7コマ**（0コマ目は立ち絵＝そこから動き出すので都合がよい）
- テンプレート名を間違えると**即エラーで課金されない**ので、当てずっぽうに投げて確かめてよい
- 蝙蝠・ハーピー・セイレーンは **humanoid で作ってあった**ことを確認（humanoidテンプレートが通った）

## 🔢 回収時のコマ数（テンプレートは固定なので毎回同じ）
- humanoid: `idle:4 walk:6 hit:6 death:7`
- quadruped: `idle:8|9|17 walk:6 hit:7 death:7`

## 🧩 Unityへの取り込み
859枚へ Point / PPU16 / 非圧縮 / mipmap無し / Clamp を一括適用（`execute_code`）。
⚠ 859枚の再インポート中は **Unity が MCP の ping に答えない**。`execute_code` が
`success:false` を返しても**実際には走っている**ので、投げ直さず数分待って結果を確認すること
（投げ直すと二重に再インポートが走る）。

## ✅ 動作確認
再生側は前セッションの `MinionAnim`（連番が切れるまで読む・`MaxFrames=24`）で**無改修**。
再生開始 0.43 秒の時点で idle が 6fps どおり**コマ2**を表示、skeleton は移動して `run` に遷移。
34種×4状態すべてが `Resources.Load` で引けることも確認済み。

## 📋 残っている作業
- **第2段：run / crouch / air / turn を全34種へ**。四足は `running-6-frames` は使えるが
  crouch/air/turn はテンプレートが無く v3 が要る
- Phase B の `UIKit` 分割（`GameUIManager` が5,000行超）
- 通しプレイでのバランス確認（難易度4段 × 装備グレード × U2の敵軍）

---

# 2026-08-10 ｜ GameUIManager を割る（Phase B-6 完了）

5,973行の神クラスを、**行の中身を変えない機械的な分割**と、**部品の切り出し**の2段でほどいた。
コミット `21b461e`（分割）と `5e45261`（UIKit）。

## ① partial class として9ファイルへ（21b461e）
`partial` なので**同じクラスのまま**＝参照もインスペクタの割当も壊れない。

| ファイル | 行 | 中身 |
|---|---|---|
| GameUIManager.cs | 295 | 参照/パレット/Awake/Start/BuildUI |
| GameUIManager.Kit.cs | 249 | 土台と [[UIKit]] への転送 |
| GameUIManager.DemonLord.cs | 435 | 魔王/感情ツリー/階層タブ/遺物 |
| GameUIManager.Codex.cs | 791 | 配下図鑑/配置ストリップ群/個体装備 |
| GameUIManager.Research.cs | 149 | 研究ツリー |
| GameUIManager.Surface.cs | 1978 | 地上4X（さらに割るならここ） |
| GameUIManager.Overlay.cs | 867 | 拡張/降下/リザルト/腹心/号令/トースト/ログ/セーブ/設定/発見 |
| GameUIManager.Title.cs | 473 | タイトルと新規開始 |
| GameUIManager.Hud.cs | 596 | 上下バー/生成パネル/ツールボタン/Update |

**やり方**：波括弧の深さからメンバ境界を機械的に求め、割り当てた行範囲が本体を
**過不足なく1回ずつ覆う**ことを検証してから書き出した。分割前後で深さ1のメンバ数は **363 で一致**。

## ② UIKit.cs（5e45261）
画面に依存しない部品を `static class UIKit`（380行）へ。
これまで `GameUIManager` の private だったので、**他のスクリプトが同じ見た目を作れなかった**。

⚠ **呼び出し側（200箇所超）は1行も変えていない**。`GameUIManager.Kit.cs` に1行の転送を置いた。
移動と書き換えを混ぜると、どちらが原因の事故なのか見えなくなる。新しく書くコードは `UIKit.` を直接呼ぶ。

⚠ フォント/スキン/パレットは `Start` の `ConfigureKit()` で1回渡す。**UIを組む前**に呼ぶこと。
パレットは既存の値をそのまま渡すので**見た目は不変**（`UITheme` への統合は別途）。

⚠ `Outline`→`AddOutline`、`Text`→`Label`、`Card`→`CardBox`、`Chip`→`ChipBox` に改名した
（`UnityEngine.UI` の型と同名だと、同じクラスの中で `AddComponent<Outline>()` を書いたとき読み手が迷う）。

## 🐛 検証で踏んだ罠：**エディタが止まっているとスクリーンショットは嘘をつく**
地上パネルを開いたら真っ黒で、一瞬「壊した」と思った。実際は
`Time.frameCount` が **1 から進んでいなかった**（エディタが非フォーカスでゲームループがティックしない。
`EditorApplication.QueuePlayerLoopUpdate()` も効かない）。
**後から開いたUIはCanvasの再構築がフレーム内で走るので、止まっていると何も描かれない**。
→ 画面を疑う前に **`Time.frameCount` を見る**。描画に頼らない検証として、
組み上がったパネルの文字要素数とフォント割当を数えた（図鑑207/207・研究164/164 ほか全11パネルで100%）。

## 📋 Phase B は完了
残りは **通しプレイでのバランス確認**（難易度4段 × 装備グレード × U2の敵軍）。
`GameUIManager.Surface.cs` が1,978行あるので、地上をさらに割るならそこ。

---

# 2026-08-10 ｜ レベル感の是正（こちらの伸びが遅い／深い階が弱い／地上が毎ターン削られる）

ユーザー報告「10ターン目で冒険者Lv14なのにこちらは1階層Lv5」「2階層以降のキャラは1〜2Lv」
「進化の恩恵を感じない」「地上が2ターン目から毎ターン奪られる」への対処。コミット `2ed061c`。

## 📊 まず測った（推測で触らない）
| | T3 | T10 | T20 | T30 | 倍率 |
|---|---|---|---|---|---|
| 冒険者HP | 127 | 255 | 644 | 901 | ×7.1 |
| 冒険者ATK | 1.10 | 1.73 | 3.52 | 4.16 | ×3.8 |
| **冒険者の総圧力** | | | | | **×27** |
| B1F配下の倍率 | ×1.04 | ×1.20 | ×1.40 | ×1.60 | ×1.54 |
| **配下の総戦力** | | | | | **×2.4** |

**11倍の開き**。しかも配下Lvは上限50でも×2.96が天井なので、**レベルだけでは構造上追いつけない**。

## 🔍 本当の原因は「軸の数」
冒険者は **ランク×Lv×武器×防具×脅威度** の5軸が**ターンとfameで勝手に**伸びる。
こちらは**ターンで自動的に伸びるのが個体Lvの1軸だけ**で、装備・進化は
**個体ごとに**DPを払う必要がある（1個体を最上位装備にすると両スロットで20,100DP＝配置枠12なら24万DP）。
＝ **投資が"数"に効かない**。

### 「深い階ほど弱い」の真因
魔素濃度(`ExpForFloor`)は正しく効いていた（B2F 55/波・B3F 85/波）。
本当の原因は **新規召喚が必ずLv1** だったこと。2階層は解禁が遅いので、そこに置くのは常に新兵。
冒険者がLv16の世界にLv1が出てくる。**魔素濃度で直したはずの現象がここから再発していた。**

### 反芻が二重に塞がっていた
`floor <= deepest` で一律禁止。しかし到達されるまでは反芻でしか埋められず、
**到達された瞬間に禁止**される。そしてその階の実戦経験は1波0.8Lvぶんしかない。

## 🛠️ 入れたもの（8件）
1. **新規召喚を世界水準のLvで出す** `MinionRoster.SummonLevel()`＝目安Lv×0.5。強さは召喚コストで払う（Lv1つ+10%）
2. **経験値の底上げ＋追いつき補正** `(25+30F)→(40+35F)`。目標Lvから遅れているぶんだけ最大2.5倍
3. **魔王Lvが全配下を底上げ** `1+min(Lv,40)*0.03`（払わなくても効く2本目の軸）
4. **進化段階そのものに倍率** `1+depth*0.12`＋盾役のatk底上げ（1.05→1.25 / 1.30→1.50）
5. **反芻を個体単位の判定に**（`Individual.foughtLastWave`）
6. **奪還軍に集結2ターン＋撃退後3ターンCD＋同時進発の禁止＋閾値100→160**
7. **`ManaSurge.cs`（魔素の奔流）**＝6ターンに1回・そのターン限り。覚醒(全配下+1〜3Lv)／奔流(深い階ほど経験値+75%/階)
8. **冒険者側の軸を削る**：自己回復をLvから切り離す／`GradeFromWorld` 0.40/42→0.34/50

## ⚠ 設計上の判断（次に触るとき用）
- **経験値を相手Lvに"比例"させてはいけない**。相手Lvはターンに線形なので、比例させると
  積算が二次になり一方的に追い越す。**遅れ幅に応じた補正**なら、追いついた瞬間に1.0へ戻るので
  オーバーシュートしない（→ [[difficulty-curve-orders]] の「入力のオーダーを揃える」）。
- **魔王Lvと個体Lvは同じターン駆動**なので、両方に大きな係数を持たせると二次になる。
  係数を小さく（0.03）し、上限（Lv40）も付けた。
- 進化段階の倍率は**プレイヤーの投資で駆動する軸**＝冒険者の装備グレードの対になるもの。
  ターン駆動の軸とは入力が違うので二重計上にはならない。
- ⚠ **常時効いているならそれは倍率であってイベントではない**。魔素の奔流は6ターンに1回・
  そのターン限りに固定した（実測 2/12ターン）。

## ✅ 結果（同じ実測）
- 冒険者の総圧力 **×27 → ×23.4**
- 配下の総戦力 **×2.4 → ×10.0**
- 差 **11倍 → 2.3倍**。残りは進化(×1.85)・装備(最大×7)・遺物/トーテムで埋まる範囲＝
  **既定は少し不利／投資すれば上回る**という形になった。

## 📋 次
通しプレイでの検証（この8件は全部カーブに効くので、**実際に遊んで**どこが行き過ぎ/不足かを見る）。

---

# 2026-08-10 ｜ Civ VII 精読 → ペース是正・偉業90件・ツリー土台・軍団システム

コミット `0a856e0`(G-1) `19121c4`(G-2) `7a408a1`(G-3a) `fa8823a`(U-1) `9d97bc5`(U-2)。

## 📚 資料から取れた「Civのツリーが深く見える理由」
Claudeのリサーチmd／Geminiのpdf／civ7wiki（ユニット・司令官・古代建造物・属性）を突き合わせた。

| 仕組み | Civ VII の実際 |
|---|---|
| **時代ごとに別のツリー** | 時代が変わると前のツリーは完結し、**まったく新しいツリー**が開く |
| **習熟(Mastery)** | 各ノードに第2段階。**習熟は後続ノードの前提にならない**＝「先へ急ぐ」か「深く掘る」かの選択 |
| **AND合流** | 法典＝規律 **かつ** 神秘主義／文字＝航海+土器。樹形ではなく**格子**になる |
| **複数の根** | 古代技術は 農業/航海/土器/畜産 の4起点 |
| **未来研究** | 各時代の末端に反復可能ノード（時代進行+10・属性+1・Innovation） |
| **排他イデオロギー** | 政治理論の後に1つ選び、**他2つは永久ロック** |
| ノード数 | 古代技術15・古代社会制度14程度。**深さは数ではなく習熟と合流で作る** |
| 1時代の長さ | **120〜160ターン**（全体400超） |
| 偉業 | **1時代30個・全100超。全部やる必要はない** |
| 勝利 | 閾値に届くのは**探検（2番目の時代）の半ば**から。倍率は 6倍→**1.25倍**へ連続的に |

⚠ **PDFは内蔵リーダーが『password-protected』と誤判定した**が、`/Encrypt` は無かった。
`docs/tools/pdftext.py`（ライブラリ無しのToUnicode CMap復号）で読めた。同じことが起きたらこれを使う。

### 資源はすでにCiv VIIと1対1で対応していた
DP=Gold ／ **素材=Production** ／ 研究点=Science ／ 感情=Culture ／ 威名=Influence ／ 食料=Food ／ 祝祭・不満=Happiness。
**新しい資源を足す必要はない**。軍団の生産に素材を使うのはこの対応に沿っている。

### civ7wiki のユニット数値（U-3以降の目安）
6分類（歩兵/騎兵/遠隔/攻囲/海洋/航空）。戦闘力 20→65（約3倍）、コスト 30→460（約15倍）、
移動2〜4、遠隔の射程15〜55、維持費0〜6。司令官は陸/海/空の3種で**昇進4系統**（稜堡・突撃・兵站・機動戦）。
建造物は産出6種・コスト55〜500・**倉庫系**（隣接改善+1）・**隣接ボーナス**（川/沿岸/山岳/資源/遺産/街区）。

## G-1 ペース（T18 → T64以上）
原因は3つとも構造。①時代の進行が「その時代の偉業を全部やる」設計（配点が小12×4+大26×2＝ちょうどNeed100）
②倍率が6/3/1.5の**階段**で終焉に入った瞬間に1.5 ③HoldNeed 5。
→ Need 210＋**自然進行+5/T**、倍率を**連続**（胎動6.0／伸長6.0→3.0／終焉3.0→1.25）、
**VictoryOpen**（伸長の半ばまで勝敗を止める）、HoldNeed 8。
「何もしない」条件の実測で胎動T1-35／伸長T36-／判定解禁T57／**T64に人間が経済勝利**。

## G-2 偉業 18→90件（1時代30・大6）
6軸に各15件ずつ。判定を**データ駆動**（`Cond`列挙30種＋閾値、`Value()`1箇所）に。
報酬も時代と大小から自動算出。⚠ **`TriumphProgressCap`（Needの60%＝126）**を新設し、
偉業を全部埋めても**1時代は最低17ターン**かかるようにした（偉業は早める手段で、飛ばす手段ではない）。

## G-3a 研究ツリーの土台
`ResearchNode` に **era / gate+gateNeed / tier / effect+amount** を追加。
`ResEffect` 15種＋`ResearchState.Sum()/Mult()` で効果を集約。
⚠ **1ノードずつ手配線すると150件で必ず漏れる**（押せるのに何も起きないノードができる）。
解放条件は偉業と**同じ `EraSystem.Cond` を共有**するので判定が二重にならない。`GateText` で「あと何が要るか」も出せる。

## U-1 軍団（Legion）
地上の駒が眷属3〜4体しかなく戦線にならなかった。眷属＝Civの**司令官**なので、足りないのは中身。
- 軍団は `MinionCatalog` 34種から作る＝**迷宮の進化ツリーがそのまま地上の強さになる**
- ⚠ **個体を消費しない**。消費すると20体並べた時点でロスターが空になり迷宮に置く駒が無くなる
- 兵科は `Role` から導く（Tank→前衛/Melee→突撃/Ranged→射手/Buff・Debuff→術者）。
  **射手と術者だけ射程1**＝前衛の後ろから撃てる＝並べる意味が出る
- 軍団もZoCを張る。⚠ ここを眷属だけにすると「並べても敵が素通り」になる
- `HexTileArt` に2枚追加（近接=隊列ブロック／射手=山形）。**色だけだと敵軍の菱形と紛れる**ので形で分ける
- 🐛 **自領には山岳のような通行不能タイルも含まれる**。そこに編成できてしまい永久に動けない軍団ができた

## U-2 生産キュー・維持費・上限・『軍団』タブ
- 生産力 = 3 + 人口×2（+都市3/兵舎2）。⚠ **面積ではなく人口**に紐づける
- 生産コスト 20+tierCP×8／着工DP tierCP×12。実測 スケルトンが人口4の拠点で4ターン
- 即時購入は残したが `着工DP + 生産力×25` と割高に。**即時が安いと「時間」という判断が消える**
- 維持費 1+tierCP/8。払えないときは**即解散にせず12ずつ損耗**（即解散だと事故で全滅）
- 上限 3+拠点×2+都市（+兵站2/簒奪2）。拠点を増やす理由にもなる
- ⚠ タブを1つ差し込むと**3番以降のindexが全部ずれる**。表示の出し分け・窓のタイトル・switch の3箇所を揃える

## 📋 残っている計画
**ユニット**: U-3（兵科差の戦闘・司令官の指揮半径・パック移動）→ U-4（昇進4系統）
**建造物**: B-1（倉庫系と隣接ボーナス）→ B-2（街区・陳腐化・Overbuild）
**ツリー**: G-3b（**習熟**＋**危険度** 三級→特級）→ G-3c/d（迷宮48＋地上52の計100ノード、**覇道**の排他分岐）→ G-4（地上ツリーUIをCiv型グラフへ）→ G-5（シンクレティズム・時代を越える系統）
順序の理由：**ツリーのノードの半分は「何を解禁するか」で価値が決まる**ので、ユニットと建造物を先に作る。

---

## 2026-08-10（続き）｜ G-3c 195ノード ／ G-3b 習熟と危険度

### G-3c 研究ツリー 57 → 195ノード（`8e239f8`）
G-3a で作った土台（時代 `era` ／解放条件 `gate` ／効果 `ResEffect`）に中身を載せた。

| | |
|---|---|
| 分野 | 魔物30・領域28・錬成14・魔王16・魔法37・地上40・**業の研究30（新設）** |
| 時代 | 胎動22・伸長83・終焉72 |
| 構造 | 解放条件つき69・**合流（前提2つ以上）17**・効果つき127 |
| 整合 | ID重複0・前提の欠落0 |

魔法は原作（n4282fq）の「基本7属性＋派生＋融合＋階級5段」に沿わせた。
合流の例：蒸気＝火+水／溶岩＝火+土／八熱地獄＝蒸気+溶岩+火炎嵐。

### G-3b 習熟（Mastery）と危険度（`9fd9d3e`）

**習熟**＝研究済みノードの第2段階。同コスト。
- **後続の前提には決してしない**。前提にすると「全部取る」が最適になって選択が消える。
- 基礎＝解禁／習熟＝数値。数値ノードは同じ効果がもう一度乗り、解禁型は分野の既定効果
  （`0.04 + tier*0.01`）を返す。⚠「押せるのに何も起きない習熟」を作らないため、
  195ノード全部で効果が返ることを実測（0件）。
- 深い段の習熟には危険度が要る。**`tier` から導く**（4→二級／5→準一級／6+→一級）。
  実測の内訳：不問147・二級29・準一級13・一級3・特級3。

**危険度**（`DangerRank.cs` 新設）＝原作の迷宮等級 三級→二級→準一級→一級→特級。
`名声(対数≤30)＋脅威度(≤20)＋階層(≤25)＋撃破(対数≤15)＋版図(≤10)＝100点`／閾値 `0/20/42/64/88`。

| | T1 | T10 | T20 | T35 | T50 | T70 |
|---|---|---|---|---|---|---|
| 点 | 6 | 36 | 56 | 78 | 86 | 96 |
| 等級 | 三級 | 二級 | 準一級 | 一級 | 一級 | **特級** |

⚠ **5つの入力すべてが飽和する**ので1軸を伸ばしただけでは上がらない。
実測：名声3万でも他が序盤なら準一級止まり（45点）。
⚠ **倍率としては使わない。鍵としてだけ使う**（掛け算の軸を増やさない → 難易度カーブの原則）。
⚠ 閾値の上を80にすると T50 で振り切れ、後半で等級が動かなくなる。88 にした。

### UI
- 研究セルに習熟行と、**開かない理由を1つだけ**（時代→前提→解放条件の順）。
- 分野の見出しに時代の内訳と「修了 3/30・習熟 1」。
- 上部バーに危険度チップ（`UIIcons` に頭蓋を追加。脅威度の「！」と**形で**区別）。
- ⚠ ツリーは実測 **2,848×4,010px** で窓は 1,768px。縦だけのスクロールに入れていたので
  **tier5以降の列が丸ごと掴めなかった**。`UIKit.MakeScroll2D` を新設して2軸に。
  2軸の Content はストレッチしないので **`sizeDelta` に幅も入れる**。

### ついでに直した2件（どちらも実測で見つけた）
1. **`UIKit.Fix` が `HasCharacter(ch)` の1引数版を使っていた**。同梱フォントは動的アトラスなので、
   まだ焼かれていないだけの字にも false が返る。→ `→` `―` `◆` が**フォントにあるのに全部消えていた**
   （『基本形→進化形』が『基本形進化形』、『配下進化Ⅰ/Ⅱ/Ⅲ 開放』が3つとも同名に見えていた）。
   `HasCharacter(ch, true, true)` に変更。ローマ数字は保険で `GlyphMap` から ASCII に固定。
2. **`SoundSystem.EnsureRoot` が再生外で `DontDestroyOnLoad` を呼んでいた**。
   エディタからゲームロジックを叩く検証が `NotifySystem.Push` 経由で必ず落ちる。`isPlaying` で止めた。

### 次
G-4（地上ツリーUIのCiv型グラフ化）／U-3（兵科差の戦闘・司令官の指揮半径・パック移動）／
B-1（倉庫系と隣接ボーナス）。**T60以降のカーブは通しプレイで要確認**。

### G-4 地上ツリーをCiv型のグラフに（`8ee9230`）
地上ツリーは **620px の窓に3列のカードを並べるだけ**で、前提のつながりが一切見えなかった
（＝ツリーではなかった）。70ノードのグラフは幅2,000pxを超えるので、窓ではなく全画面にする。

- `BuildTreeGraph(container, width, fields, onChanged)` を切り出し、
  **迷宮ツリーと地上ツリーの両方がここを呼ぶ**。片方だけ見た目が古くなることがない。
- 接続線を色分け：前提済み=緑／未達=灰／**合流（前提2つ以上）=金で太く**。
- ⚠ 迷宮の `researchPanel` は**迷宮Canvas(order100)**にあり、地上モードではCanvasごと切っている。
  だから地上ツリーは**地上Canvasに別で建てる**（中身は共通）。
- 左メニューの『ツリー』は入口（修了数＋「ツリーを開く」）に。開くと窓は畳む。
  地上⇄迷宮を往復してもツリーは持ち越さない。
- ついでに：`**強調**` が画面に生で出ていた（『支配上限 +2／**街区**（…）』）。
  コメントで `**` を使う癖が文字列に混ざるので、`UIKit.Fix` の出口で `<b>` に変換。閉じ忘れは自分で閉じる。

実測：迷宮125セル/2,848×4,010px・地上70セル/1,984×2,504px。入口タブ・往復・`**`の消滅も確認。

### U-3 兵科の相性・司令官の指揮・パック移動（`654c735`）
U-1/U-2 の軍団は ZoC で足を止め駐留で守りに足されるだけで、**敵と撃ち合わなかった**。

**三すくみ**：突撃→後衛 ×1.5 ／ 前衛→突撃 ×1.4 ／ 射手・術者→前衛 ×1.3。それ以外は等倍。
⚠ 細かく分けない。3本の矢印だけなら盤を見た瞬間に判断できる。敵軍にも兵科を持たせた
（片側だけだと「どれを当てるか」が生まれない）。

**会戦**：射程内の敵軍と自動で撃ち合う。**敵が動いたあと**に解決する。
前衛・突撃（射程0）は隣接で殴り合い＝反撃を食う。射手・術者（射程1）は**距離2から一方的に**削れる。
損耗＝`26 × (戦力比)^0.7`。⚠ 上限は **50**。60だと格上に触れた瞬間に6割溶け、
退く判断をする前に壊滅する（実測）。

**指揮**：眷属の周囲（半径1／昇進『号令』で2）に ×1.12（『軍旗』で ×1.20）。
⚠ **重ねない**（一番強い司令官のぶんだけ）。重ねると司令官を固める作業になるうえ掛け算の軸が増える。

**パック移動**：麾下に入れると、行き先を指示していないターンは司令官に付いて動く。
指揮が届いた時点で止める（司令官のタイルまで詰めると1タイル1軍団の制限で団子になる）。

#### 実測で見つけて直した2件
1. **敵の攻城が通ると、敵が軍団の上に乗って共存していた**。
   `OnTileOverrun` で半壊＋隣の自領へ後退、退路が無ければ壊滅。
2. **経路が貪欲法（距離が減る隣だけ）で回り込めなかった**。
   司令官のタイルの隣6面のうち4面が山岳で、麾下が何ターン経っても距離2から動かなかった。
   幅優先に置換（目標までの距離+3の範囲だけ探索）。**4,503タイル盤で4ターン1ms**、距離1に収束。

⚠ 射手は全種が**進化Ⅰ(`m_evo1`)以降**の解禁。序盤に射程1を試すなら術者（ゴースト・インプ）。

### U-4 軍団の攻勢・補給・歴戦（`4f94c2f`）
計画では『昇進4系統』だったが、U-3 を入れた時点で**もっと重い欠落**が3つ見えたので差し替えた。
軍団が ①土地を取れない ②傷が治らない ③育たない。
①が無いと「陣地の取り合い」にならず、②が無いと数ターンで盤の駒が全部使いものにならない。

**攻城**：隣の敵領・中立領を攻める（1ターン1回・移動力を使い切る）。
攻め手 = 戦力 × 攻城適性 × 指揮 × 側面支援。

| 兵科 | 前衛 | 突撃 | 射手 | 術者 |
|---|---|---|---|---|
| 攻城適性 | 1.00 | 1.25 | **0.70** | 0.85 |

⚠ 射手を城攻めに強くすると「射手だけ並べれば片づく」になり、前衛を作る理由が消える。
側面支援は隣に並べた味方1体につき +8%（3体まで）＝**横に並べるほど通る**。
1.15倍で制圧(-15%)／0.9倍で辛勝(-35%)／届かなければ -40〜60%。
⚠ 占領の後始末は `KinRoster.OnRegionConquered`（新設の公開口）を通す。
眷属と軍団で別々に書くと、真核の奪取や独立勢力の粉砕が片方だけ漏れる。

**補給**：自領で休んだターンだけ戻る（自領8／拠点15／都市20、兵舎+5）。
**戦ったターンは戻らない**。維持費を払えなかったターンも戻らない。

**歴戦**：会戦と攻城で `exp`、`ExpNeed = 60 + Lv×22`。
⚠ 与えたダメージに比例させない。強い相手ほど削れないので、格上と戦うほど育たなくなる。
「戦った回数」と「相手の格（対数）」で入れ、負けても半分は入る。

⚠ ボタンのラベルは短く。44pxに「攻める 23→88」を入れたら2行に折れて潰れた（実測）→「攻 52/88」。

### B-1 施設を時代つきに・16種へ・沿岸と遺産の隣接（`db89654`）
B-1/B-2 の予定だった「倉庫系・隣接ボーナス・街区・陳腐化・改築・専門家」は**既に入っていた**ので、
実際に足りていなかった **施設の種類と時代** を埋めた。

**時代**：`Def.era` を追加し、その時代に入るまで建てられない。
建てられない理由は「時代 → 研究 → 地形」の順に1つだけ出す。UIは時代順に並べる。

#### ⚠⚠ カタログの並び順は変えない
`SurfaceMap.Region.district` は**この配列のindexを保存している**。
一度時代順に並べ替えたが、それだと**既存セーブで交易所が魔泉に化ける**。
旧7種を 0..6 に固定し、新規は末尾に足す形に戻した。並べ替えは表示側（`SortedForUI`）だけ。

**施設 7 → 16 種**
| 時代 | 施設 |
|---|---|
| 胎動 | 交易所・鉱錬所・魔泉・祭壇・兵舎・倉庫・**農場** |
| 伸長 | 訓練所・**港**・**大市場**・**祝祭堂**・**石工場**・**使節館** |
| 終焉 | **造兵廠**・**学院**・**隠れ家** |

解禁はすべて G-3c で足した地上研究ノードに紐づけた＝**数値だけだったノードが解禁を持つ**。

**隣接**：**沿岸**（隣が海）と**遺産**（隣に世界遺産）を導入。
⚠ 分岐を **産出ではなく施設のid** で切るように変えた。産出で切ると交易所と大市場、
魔泉と学院がまったく同じ隣接条件になり、置く場所を選び直す意味が消える。
⚓ 港は沿岸だけ（緩めると内陸に港が並んで沿岸ボーナスが無意味になる）。

**新産出3種**：食料→`FoodIncome`／威名→`AddInfluence`／生産力→`LegionRoster.ProductionAt`。
⚠ 食料と生産力は `TotalYields`（全体集計）に入れない。所属する拠点が要る値なので、
全体に足すと産まない拠点でも軍団が早く出る。

実測：旧7種のindex固定・研究IDの欠落0・沿岸424/4503タイル・遺産3タイル・
隣接の幅（大市場+2〜+14、農場+4〜+12＝置く場所で変わる）・食料2→7／威名20→22／生産力8→10。

### ⏳ ターンを前半（迷宮）と後半（地上）に分割（`6836130`）
以前は迷宮の準備中も戦闘中も地上を触れたので、どちらにも集中できず、
地上の操作そのものを忘れる／面倒に感じる状態だった（ユーザー報告）。

`Phase` に `Surface` を追加：**Prepare（迷宮の準備）→ Battle（防衛戦）→ Surface（地上）→ 次のPrepare**。
- 防衛戦の終わりは**前半の締め**だけ（魔王の成長・RP・無失点判定・戦績）。地上の解決はやらない。
  ここでやると「地上を操作する前に地上のターンが済んでいる」ことになる。
- 地上パネルの『× 迷宮へ戻る』→ **『ターンを終える ▶』**。押すと地上の解決一式＋ターン加算。
- 上部バーの**『地上』ボタンを廃止**（フェーズで自動的に切り替わるので要らない）。
- ⚠ `IsPreparePhase` は「戦闘中でない」の意味のまま（`!= Battle`）。地上フェーズを外すと
  **地上フェーズ中に施設が建てられなくなる**（全systemのガードがこれを見ている）。
  切り分けは「どちらの画面を出すか」で担保。新設 `IsDungeonPhase`/`IsSurfacePhase`/`PhaseLabel`。
- ⚠ 保存は**前半のみ**。ロードは必ず前半から再開するので、後半で保存できると防衛戦が二重に起きる。
- ⚠ 見出しを「地上　第3ターン 後半」に伸ばしたのに支配サマリの開始位置を直さず、文字が重なっていた。

### 🧬 隊編成の階層表示と、除名時の配置解除（同コミット）
3階から見たとき「編成済み」としか出ず、**別の階の個体を誤って外す事故**が起きていた。
- 所属を **「B1F隊 (他階)」／「B2F隊 (この階)」** と階層名で出す（他階は橙）。
- ボタンも「隊から外す」→ **「B1F の隊から外す」**（押すもの自体に階を書く）。

配置が残る不具合を2か所直した。
1. ⚠ `SquadRemoveAt`（編成トレイから抜く経路）が `RemoveAt` するだけで配置解除を通していなかった。
2. ⚠ `RemovePlacedOfIndividual` が**いま開いている階しか見ていなかった**。
   他階の配置は `DungeonFloorManager` のスナップショットにあるので `RemoveIndividualFromOtherFloors` を新設。
   これが「1階に置いた個体を外しても盤に残り、さらに2階の隊にも入れられる」の正体。

### G-3d 覇道（終焉の排他分岐）と未来研究 ― ツリー206ノードで完成（`1c61f1e`）
Civ VII の「政治理論のあと1つ選び、他2つは永久ロック」を、原作の**大罪之刻印**で表した。
ここが1周で見られる終盤を変える＝周回する理由になる。

`ResearchNode` に2つ足した。
- `exclusive`：同じグループは**1つしか取れない**。取った瞬間、他は永久に閉じる。
- `repeatable`：何度でも取れ、**取るたびに効果が乗りコストが45%重くなる**。
  ⚠ 効果は取った回数ぶん積む（1回ぶんしか乗らないと「重ねる意味」が消える）。
  ⚠ 「研究済」にならず、習熟も出さない（重ねるのが伸ばし方）。

**覇道13ノード**（業の研究・終焉）：`大罪之刻印`（tier6・危険度 一級・前提＝闘神術＋死神の瞳）から
暴食（軍事）／強欲（産出）／憤怒（恐怖）の3本。各3段、末端は危険度 特級。
⚠ 入口3件には危険度 一級を**明示**した。書かないと `tier>=4 && gateNeed<=0` の自動付与で
特級になり、**選ぶこと自体ができなくなる**。

**未来研究『果ての探究』**：反復可能。配下HP +4%/回、コスト 70→102→133→164（実測）。

UI：封印は取り消し線＋暗い枠＋「封印 ― 『暴食の刻印』を選んだ」。
未選択の分岐には**押す前に**「◆選ぶと他の刻印は永久に閉じる」を赤で。反復は名前に「×4」。

実測：総206ノード・ID重複0・前提欠落0・排他3・反復1。
暴食を取ると強欲と憤怒が封印され研究不可、配下HP 1.550→1.710（反復×4）。

### G-5 習合（時代の変わり目に他の魔王の系統を継ぐ）（`c5a0fd7`）
Civ VII は時代が変わるとき文明を乗り換える。魔王は変えられないので、
**他の魔王の血脈を継ぐ**形にした（原作の「真核を奪う」＝相手の在りようを自分のものにする筋）。

- 継げるのは**時代が変わった直後の1回だけ**。見送ってもよい。同じ系統は一度きり。
  ＝1周で最大2つ。何を継ぐかで終盤の色が変わる。
- 対価は**威名**。⚠ **排除した魔王の系統は半額**。倒した相手のものを継ぐほうが安い＝
  「排除」と「習合」が同じ盤の上で繋がる。

| 系統 | 効果 |
|---|---|
| 鬼種の血 | 配下すべて +8%／軍団の攻城 +10% |
| 妖精種の理 | 毎ターンRP +25%／天啓 40%→52%引き |
| 龍種の威 | 威名 +5/T／脅威度の上がり方 -20% |

⚠ 効果は**既にある軸に薄く乗せる**だけ（`DemonLord.MinionPowerMult`・`SiegePowerOf`・
`ResearchState.OnTurnEnd`・`EurekaTracker.Discount`・`LureEconomy` の噂の伸び）。
新しい掛け算の軸を作らない。
UIは『時代』タブ。**選べるときだけ**3枚のカードを出し、ふだんは継いだ血の1行だけ。

### 🎬 wolf と rat の待機を作り直し（同コミット・各1生成）
⚠ **アニメの差分は『キャンバス全体』ではなく『体の不透明画素』に対して測る**。

| | 体の画素 | コマ間の変化 |
|---|---|---|
| wolf 旧 | 307 | 36.1% |
| wolf 新 | 403 | 34.9%（動く量は +27%） |
| rat 旧 | 332 | 21.9% |
| rat 新 | 432 | 26.1% |
| goblin | 190 | 61.7% |

全体比だと wolf 2%・rat 1% に見えて「止まっている」と読めたが、体比では動いていた（最初の見立ての訂正）。
⚠ コマを増やすと `8.png.meta` が既定設定（PPU100・Bilinear）で入り、
**そのコマだけ大きさが変わりぼやける**。`0.png.meta` の設定を写して揃える（guidは維持）。

### KinPromotion を Civ VII の4系統に（`f5d63ab`）
進撃/攻城/統率/渡航 → **稜堡/突撃/兵站/機動戦**。
⚠⚠ **defs の並び順（index）は変えていない**。`Kin.promotions` は index を保存しているので、
入れ替えると既存セーブで別の昇進に化ける。変えたのは line と tier と系統名だけ。

| 系統 | 段 |
|---|---|
| 稜堡（城を攻め、城で耐える） | 破城槌 → 城塞破り → 鼓舞 |
| 突撃（前へ出て打ち破る） | 強襲 → 総攻め → 軍旗 |
| 兵站（率い、届かせる） | 号令 → 沿岸航行 → 遠洋 |
| 機動戦（速く動き、止まらない） | 疾駆 → 不屈 → 電撃戦 |

司令官が麾下の軍団を強くする効果を2つ足した（既に号令＝指揮半径+1／軍旗＝指揮×1.20 はあった）。
稜堡『鼓舞』→ 麾下の軍団の被害 -15%／機動戦『電撃戦』→ 麾下の軍団の移動力 +1。

### 🖼️ 地上を絵で見せる（`94eddfe`）
Civ のように**盤を見ただけで何が建っていて誰が立っているか分かる**ようにした。

**アトラスをグリッドに**（HexTileArt）。盤は1枚メッシュなので絵を増やすにはセルを足すしかない。
⚠ 横1列のままだと 128px×68 = 8,704px で**テクスチャ上限(8192)を超える**ので8列に。
実測 1024×1413 ＝ 5.5MB・生成61ms・盤の再構築 0ms/回。

⚠⚠ `AddQuad`/`AddOverlay` が **UVの縦を 0/1 で決め打ち**していた（1行アトラス前提）。
そのままグリッドにしたら**全タイルがアトラス全体を貼って盤が壊れた**。`uv.yMin/yMax` を使う。

**施設16種＋町/都市/砦**を PixelLab で生成（21生成／うち `training` と `arsenal` は
1回目が点だけ・金床だけになったので description を具体化して振り直し）。
拠点は中央に大きく、施設は手前に小さく（街区で2つなら左右に）。陳腐化した施設は暗く。
⚠ 絵に所有者の色を掛けない。施設の絵は**それ自体が色で種類を示している**ため。

**軍団と眷属は「種の姿」で**（生成ゼロ）。迷宮の1枚絵34種をそのままアトラスへ焼いて流用。
1マーク＝「台座（兵科の記号）＋その上に載る種の姿」。
⚠ 別マークにすると横に並んで対応が分からない／姿だけだと兵科が読めない／
姿を大きくすると同じタイルの施設を覆い隠す（0.34 に落とした）。

---

## 2026-08-11 ｜ アップグレード計画とN-1（幹に繋ぐ）

### 立てた計画
PixelLab の制限が緩いので素材は増やせる、という前提で全体を見直した。
順序の根拠は **幹の歪みを直してから枝を足す**。アイテムもショップも「何に装備するか／何を売るか」が
34種の幹に乗っている必要があり、割れたまま足すと二重管理になる。

| | 中身 |
|---|---|
| **N. 幹に繋ぐ** | N-1 特殊敵とスポナーを幹へ／N-2 入手経路の整理 |
| I. 持ち物 | 装飾品スロット（効果は遺物の `Effect` 型を再利用）／ドロップ・宝箱・ショップ |
| S. ショップとガチャ | 限定ショップ（CDO2）／召喚のガチャ化（原作） |
| L. 魔王の役 | **奥で待つ／動かす の2スタイルを選べる**（ユーザー決定）／種族固有の権能 |
| V. 見た目と操作感 | 地上の地形・資源の絵／UI素材／ホットキーとドラッグ配置 |

### N-1 特殊敵をユニーク魔物に（`d649c84`）
#### 何が切れていたか（実測）
「特殊敵」は `GddMap.Special` の6種を**見た目だけ差し替えて置くだけ**で、
34種カタログ・個体Lv・装備・進化・図鑑・研究のどれにも繋がっていなかった。
配置だけ素材払い（隊員は無償）という不揃いもあった。

#### 👾 ユニーク魔物（`UniqueCatalog`）
別カタログにしつつ、**個体としては配下とまったく同じ扱い**にした（ガチャ産・個体識別・Lvあり）。
⚠ 幹への繋ぎ方が肝：**`MinionCatalog.Get(index)` に `index >= 1000` の分岐を1箇所だけ**入れ、
同じ `MinionDef` 型に変換して返す。これで呼ぶ側を**1行も変えずに**Lv・装備・図鑑・盤の絵が効いた。
一覧（`All`/`Count`/`ByFamily`）には含めない（召喚できる種に混ざるため）。
⚠⚠ `UniqueBase = 1000` と並び順はセーブに載る。**変えない・末尾に足す**。

#### 🎰 召喚の儀（`SummonGacha`）
ユニークはここでしか出ない。外れても通常種が必ず1体（空引きにしない）。
天井は 6% + 外し回数×2%（上限50%）。実測50回でユニーク6／通常44。
⚠ 未解禁の種は出さない。出すと進化ツリーで解禁する意味が消える。
⚠ 通常召喚より割高。安いと一覧から選ぶ意味が消える。

絵は PixelLab で6種（盤のアトラスも配下34＋ユニーク6＝40セル、総74セル）。

### I-1 装飾品スロットと行商人（`d50dcbb`）
#### CDO2 を調べた結果
個別の効果一覧は namu.wiki(403)/Fandom(402) が読めず取れなかった。取れたのは**構造**。

| CDO2 | 数 | 単位 |
|---|---|---|
| 装備 | 70〜80種 | 魔物1体ごと |
| トーテム | 30種以上 | 部屋ごと・種族バフ |
| 遺物 | 80〜90種 | ダンジョン全体 |

入手は**行商人から購入**と**ターンクリア報酬から選択**の2経路。
こちらは3層とも既にあったので、足りない「**どれを誰に着けるかで編成が変わる装備**」を装飾品として足した。

#### 💍 装飾品（14種）
⚠ 効果は**既に実装済みの魔物スキル12種を1つ付与する**形にした。
新しい効果の仕組みを作ると「押せるのに何も起きない装飾品」ができる（習熟で立てた原則）。
`ZombieAI.ApplySkillsOnSpawn` が既に全部解釈するので、配線は付与の1本で済む。
⚠ グレードは持たせない（武器防具と二重になる）。**種類で選ばせる**。
⚠ スキル無しの「素直に強い」枠も置いた。全部トリッキーだと「とりあえず硬くしたい」に応えられない。
⚠ 倍率は `EquipAtkMult/EquipHpMult` に**含めた**。呼ぶ側は既にこの2つを見ているので別の口を作らない。

#### 🛒 行商人
3枠・ターンの頭に引き直し・買った枠は**売り切れのまま**。
⚠ 埋め直すと「今買うべきか」の判断が消える。⚠ 引き直しはターン頭に1回だけ。
手持ちは種類ごとの個数。⚠ 着けたぶんは減らす（でないと1つの指輪を全員に着けられる）。

#### 配線
`ZombieAI.accessoryOwnerId` を足し、隊員・ボス・ユニークの3箇所で個体IDを渡す。
⚠ `ApplySkillsOnSpawn` は **Start** なので Instantiate 直後の代入で間に合う（Awake だと間に合わない）。
⚠ エディタが tick しないと Start が走らないので、検証は `ApplySkillsOnSpawn` を直接呼んで行った。

---

## 2026-08-11 L（魔王）― 二つの構えと捕食、種族の権能

**動機**：魔王は「ステを振って進化する」だけで、**戦闘中に立っているだけ**だった。
育てる対象なのに、戦場での判断が一つも無い。ユーザーの決定は
「今のシステムと、魔王も動かせるスタイルを**選択できる**ようになるのがいい」。
CDO2 の**捕食**（魔王が自分の配下を喰って永久ステを得る）を土台に据えた。

### 👑 L-1 二つの構え（`LordStance`）
| | 鎮座 | 親征 |
|---|---|---|
| 立つ場所 | 最下層から動かない | **階層を選べる** |
| 侵攻 | 従来どおり最下層まで降りてくる | **魔王が立った階で止まる**（彼が壁） |
| 糧 | 配下を喰らう（2体/ターン） | 在陣する階で倒れた冒険者の魂 |
| 見返り | ウェーブを凌ぐと BP +2 | 前に出るほど魂が多い |
| 危険 | 低い | **浅い階で立つと深度報酬を捨てる**／討たれれば即敗北 |

⚠ 魔王が実在する階の判断は `LordStance.LordFloorIndex()` **1箇所に集約**した。
`fd.isDeepest` を直接見ている所を残すと、構えを変えても盤が付いてこない。
⚠ 親征で立つ階では**下り階段を隠す**（降りられないので）。フロアタブの『魔』印も魔王に付いて動く。
⚠ 構えの変更は**準備フェーズのみ**。戦いが始まってから後ろへ下がれてはいけない。
⚠ 構えを変えたとき `ActivateFloor` を呼び直してはいけない。あれは退避済みスナップショットで
上書きするので**このターンに置いたばかりの配置が消える**。→ `RefreshLordPresence()` を足した。

### 🍽️ L-2 捕食
捕食値 → **喰らいの段**（費用 150+段×110／1段ごとに**基礎**最大HP+70・**基礎**攻撃+2.5）。
⚠⚠ 見返りは**基礎値への加算だけ**。倍率に乗せると CDO2 の
「捕食ビルドで魔王が単騎で全滅させる」（向こうで一番壊れている型）がそのまま再現される。
⚠ 歯止めは2つだけ：**鎮座のときだけ**・**1ターン2体まで**。
⚠ ユニーク魔物は喰えない（引き当てた1体が資産なので誤操作で消えると取り返しがつかない）。
盤・隊・地上に出ている個体も喰えない。**ガチャの外れで増える通常種の使い道**になる。

### 🜲 L-3 種族の権能
号令の**5枠目**を種族で切り替える。16種族ぶん書かず、
`DemonLordRaceTree` が既に持つ `skill`(`MinionSkillKind`) で引く（9通り＝鬨の声/疾風の令/血の饗宴/
生命の泉/大地の棘/満ちる潮/不滅の誓い/畏怖の眼/群狼の令）。**人種のうちは使えない＝進化する理由**。
⚠ 効果は既にある動詞だけ（ダメージ／回復／状態異常／攻撃倍率）で組んだ。
⚠ 攻撃強化は `LordAuthority.RallyAtkMult` 1本に集約し、`ZombieAI` の与ダメ計算に**1箇所だけ**掛ける。

### 検証（Play・決定的）
- 親征B1F：`isLordFloor(0)=True` / 階段マーカー非表示 / 冒険者を階段に立たせて `Update` → **降りない**。
  魔王を不在にした対照では 0→1→2 と降りた。
- 捕食：4回試して成功2（1ターン2体）／親征中は不可／地上の眷属は不可／段位で maxHP 600→**670**（＝+70）。
- 魂：不在 +0 ／ 在陣 Lv20 撃破 +13（3+20/2）。
- 権能：15種族すべて発動して**例外0件**。人種は `使えない（種族進化が必要）`。
- セーブ往復：構え・立つ階・捕食値・段位が復元、魔王 maxHP=670 を維持。

### ついでに直した既存の穴
`MerchantShop` と `AccessoryInventory` が `SaveSystem.StaticTypes` に**載っていなかった**
（I-1 の配線漏れ＝買った装飾品と品揃えがセーブされていなかった）。両方登録した。
`StartNewGame` の初期化列にも `LordStance.Reset()` を追加（周を越えて持ち越さない）。

---

## 2026-08-11 M（変異）― 後半の難易度を「対策の要求」で作る

**動機**：うちの後半は「冒険者が強くなる／増える」しか無い。ところが
`difficulty-curve-orders` のとおり**5つの入力はすべて飽和させてある**ので T60 を過ぎると平坦になる。
数を増やせば重く、倍率を増やせば二次曲線。**そのどちらでもない軸**が要る。

`Dungeon Defense: IoH` の**変異**がその答えだった（公式Guide/FAQ v1.92.3 で裏取り）。
向こうの終盤は「敵が強い」のではなく **`-75%物理` `-75%魔法` `敵防御+` といった
“いま組んでいるビルドを無効化する条件”** が積み上がる。対抗値 MGI は `効果 = 100% ÷ (1 + MGI%)`。

### 🧬 世界の変異（`MutationSystem`・10種）
物理の守り／魔法の守り／鉄化／呪詛／群れ／看破／蝕み／静寂／韋駄天／不屈。
- **T16 から1つずつ現れ、8ターンごとに増える**（全10種）。
- 各変異は**段**を持ち、10ターンごとに 1→5 と濃くなる。
- 効き＝`1段あたり × 段 ÷ (1 + 抑制)`。

⚠⚠ **割り算にしたのは、抑制をいくら積んでも 0 にならないから。**
引き算だと抑制を積むだけで変異が消え、**編成を組み替えるという本命の対策が要らなくなる**。
⚠ **新しい倍率の軸を増やしていない**。変異は既存の値を削る方向にしか働かず、段で上限が付く。
⚠ **魔王は変異の影響を受けない**。物理も魔法も封じられたときの逃げ道が『親征』になる
（L と噛み合わせた。→ 2026-08-11 L の項）。
⚠ 難易度で新しい掛け算を作らない。**段が上がる速さだけ** `Difficulty.AdvPowerMult` に相乗り。
⚠ 出る順は `GameSetup.Seed` とターンから決定的に選ぶ（毎周同じ順だと対策が定型化する／
セーブとロードで変わらない）。

### 🛡️ 抑制（MGI 相当）
領域研究3つ：`d_adapt1 順応`(+40%) → `d_adapt2 異相の解剖`(+60%) → `d_adapt3 変異抑制`(**反復可**・+35%/回)。
`ResEffect.MutationSuppress` を**enumの末尾に**追加（既存の並びを動かさない）。

### 配線（9箇所・すべて既存の式に1つ掛けるだけ）
`ZombieAI` 与ダメ（`hasSpell` で物理/魔法の守りを出し分け）／`ZombieAI` 最大HP・回復2箇所／
`AdventurerAI` 最大HP・移動速度・状態異常の持続／`TrapCatalog.PowerMult`／
`CommandSystem` クールダウン／`DungeonAdventurerSpawner` 人数。

### 🖥️ 見せ方（見えない難易度は理不尽になる）
上部HUDに**『変異』チップ**（数と抑制率／ホバーで**出ている変異・段・実際の%・対策**の一覧）。
`GuideSystem` に見出し「世界が形を変えた」＋初出の説明＋抑制が遅れているときの進言。

### 検証（Play・決定的）
```
T16 静寂1段15%
T40 静寂45% 物理24% 看破14% 呪詛7%（4種）
T72 静寂75% 物理60% 看破70% 呪詛28% 蝕み30% 不屈28% 群れ8% 魔法12%（8種）
T88 10種すべて／T100 で全部が上限
抑制 0% → +100%(順応+解剖) → +205%(＋反復×3) で 物理 60% → 30% → 20%
```
窓口の値も実測（物理×0.803 魔法×0.961 罠×0.770 配下HP×0.908 回復×0.902 号令CD×1.246）。
`TrapCatalog.PowerMult()` が 0.770 を返すことも確認＝**式に届いている**。
同ターンを3回呼んでも増えない／セーブ往復で10種と段が復元／
T16 で報告の見出しが「世界が形を変えた」に変わり、抑制0%・変異2種で『順応』の進言が出る。
HUDバーは 1920px に収まっている。

---

## 2026-08-11 V-1 見た目と操作の修繕（ユーザー報告3件）

### 💍 装飾品の枠が『強化＋』に丸かぶりしていた
装飾品スロットを `x=430` に置いていたが、武器/防具の**『強化＋』ボタンが x484〜616**。
チップは 430〜620・y25〜55 なので、両方のボタンの上に完全に乗っていた（押せない・読めない）。
装備列は x262〜796（種別→の右端）まで使い切っているので、**その右の空き**へ移動。
⚠ 縦に積むと今度は下段『眷属化』のチェックリスト(y59〜)に食い込むので、
**ラベル＋チップ＋効果文を横1本**に収めた（y8〜34）。
実測：チップ x864〜1096・y8〜34 ／ 強化＋ x484〜616 ／ 眷属化 x1412〜1564・y74〜98 ＝ 重なり無し。
ついでに**効果文をその場に出す**ようにした（1枠しかないので、名前だけでは選べない）。

### 🖱️ UIをスクロールすると盤まで拡大縮小されていた
`SurfaceView.HandleInput` の**ホイールの分岐にだけ**「UIの上か」の番が無かった
（掴んで動かす `down` には元からあった）。`CameraController.HandleZoom` も同じ穴。

⚠⚠ **`EventSystem.IsPointerOverGameObject()` だけでは足りなかった。**
`GraphicRaycaster` は **`Graphic.depth == -1`**（まだ描画バッチに乗っていない）を飛ばすので、
**開いた直後のパネルは「UIの上」と判定されない**。実測でツリーを開いても中央のヒット数が 0 だった
（エディタが tick しないと `depth` が -1 のまま＝この状態が固定される）。
→ **矩形で直接見る** `GameUIManager.PointerOverSurfaceUI(screenPos)` を併用。
`SurfaceInner` の直下の子のうち**背景が見える板（alpha>0.05）だけ**を走査するので、
帯・左メニュー・開いている全画面パネルが自動で対象になる（表を持たなくてよい）。
実測：パネル閉→中央False/左メニューTrue/上の帯True ／ ツリー開→中央True ／ 閉じ直後→中央False。

### ⚔️ 地上の敵軍が「色違いの菱形」1種だった
人間の奪還軍と他魔王の軍が盤の上で見分けられず、集結中か攻めて来ているのかも読めなかった。
PixelLab で**4種**（`foe_knight` `foe_archer` `foe_demon` `foe_warlock`）を作り `Resources/Surface/` へ。
味方の軍団と**同じ作り**にする＝**兵科の台座**（形＝近接/遠隔、色＝陣営）＋ その上に**姿**。
⚠ 兵科は台座で示すので、姿は**陣営×近接/遠隔の4種でよい**（0.55倍で載るのでこれ以上は絵として読めない）。
⚠ `DrawMovingArmies`（移動の再生）も同じ見た目に揃えた。片方だけだと動いた瞬間に化ける。
⚠ 新しいPNGは既定で `isReadable=0` で入る＝`BlitSprite` が**黙って何もしない**。
`TextureImporter` で Point/PPU16/非圧縮/mipmap無し/**isReadable=true** に揃えてから焼く。
実測：アトラス 1024×1570・78セル、4種とも中央に不透明画素あり（393/242/423/412）。

---

## 2026-08-11 V-2 地上の地形と資源の絵

### 🌄 地形7種
`Resources/Surface/terr_<地形>.png`（荒地/平野/森/丘/山/湿地/海）を PixelLab で作り、
**天面のヘクスの内側にだけ**焼き込む `HexTileArt.BlitMotif` を追加。

⚠ 普通の `BlitSprite` はセルいっぱいに貼るので、**天面からはみ出して側面や隣のセルに滲む**。
`InHex` で1画素ずつ切り、下地（天面の色）は残して**不透明な画素だけ**を `Lerp` で乗せる。
⚠ ヘクスは角が細いので、四角い絵は **82%** より大きくすると必ず角で切れる。
⚠ ヘクスの形と厚みは**手続き生成のまま**（盤の当たり判定と継ぎ目がそこで決まる）。
⚠ 絵があるときは従来の手続きモチーフを**描かない**（三角形と本物の木が混ざって汚くなる）。
絵が無ければ手続きモチーフに落ちるので、**PNGを消しても壊れない**。

**生成の罠**：`高top-down` の地形モチーフは既定で**地面の円盤ごと**返ってくる。
山・森は物体が覆うので問題ないが、丘・平野・荒地は**円盤が主役になって「土のパッチ」に見えた**。
`isolated objects on fully transparent background with no ground patch underneath` を
付けて振り直したら、物体だけが散らばった絵になった（3体ぶん振り直し）。

### 💎 資源6種
`res_iron` `res_manastone` `res_grain` `res_livestock` `res_gem` `res_timber` を追加し、
**タイルの右上に小さく常時**出す（`HexTileArt.ResourceIndex`）。
⚠ 以前は「うんと寄ったときだけ**文字**」だったので、引くと資源が盤から消えていた。
**どこを取れば旨いかは引いた状態でこそ読みたい**ので、絵は常に出す。
⚠ 資源名の表示しきい値を `zoom<=7` → `zoom<=4.5` に締めた。
絵を入れたまま 7 のままだと、**絵と名前が二重に出て盤が文字だらけ**になる（実測）。
名前は詳細パネルにも出ているので、覚えるまでの補助として最寄りのズームにだけ残す。

### 検証（Play・決定的）
アトラス 1024×1727・84セル。地形7種すべて天面に色が11〜35種（＝絵が乗っている）。
資源6種すべて中央が不透明（254〜361/361）。盤の見た目も確認（森・雪の山・丘・湿地・波・岩）。
PixelLab 残 **1,149 / 2,000**（この回で 20 使用：敵軍4・地形7＋振り直し3・資源6）。

---

## 2026-08-11 V-3 UI素材（HUDのアイコン13種）

**何を作って、何を作らなかったか**：パネル枠とボタンは Bloodlines の素材が生きている
（`Resources/UI/Frame_*.png` `Btn_*.png`）ので触らない。弱かったのは**アイコン**：
手続き生成の白いシルエットか、DungeonTale の汎用アイテム素材の流用で、
DP＝ダイヤ・素材＝ハンマー・脅威度＝剣・名声＝盾と**意味がずれていた**。

`Resources/UI/icon_<id>.png` に13種を作った：
DP＝紫水晶のコイン／素材＝インゴットと槌／研究＝光る本／感情＝紫煙の心臓／
名声＝月桂冠／脅威度＝鳴る鐘／世界水準＝星／食料＝林檎とパン／影響力＝封蝋の巻物／
人口＝二人の村人／移動＝翼のブーツ／危険度＝角のある髑髏／**変異＝罅割れた紫水晶と触手**。

⚠ **専用の絵は着色してはいけない。** 手続き生成と汎用素材は「白で描いて意味の色を掛ける」
前提だが、専用の絵は**それ自体が色を持っている**ので同じように掛けると全部その色に染まる。
→ `UIIcons.IsArt(id)` を足し、`ResChip` が **絵なら白／それ以外は意味の色**を掛けるようにした。
チップ左端の色帯は残るので、**色分けは失われない**。

⚠ 読み込みは **専用の絵 → 汎用素材 → 手続き生成** の3段。
最後の砦を残してあるので、**PNGを消しても壊れない**し、作らなかったidも従来どおり動く。

⚠ `slot`（配置枠）だけは**作らなかった**。2回振ったが、2×2のタイル格子は
18pxのチップでは暗くて読めず、振り直すと十字になった。
**手続き生成の白い格子の方が小さくて読める**ので、そのまま残した（フォールバックがそのために在る）。
`mutation` は今まで `threat` の鐘を流用していたので、専用の絵に差し替えた。

検証：14idのうち13が『絵』、`slot` だけ『手続き』を確認。HUDの見た目も確認。
PixelLab 残 **1,134 / 2,000**（この回で15使用＝13採用＋slotの空振り2）。

---

## 2026-08-11 V-4 操作性（マウス＋タッチ、ホットキー）

ユーザーの希望「PCでもスマホでもプレイできる感じ」。**入力の受け口を作る所まで**をやった。
⚠ **レイアウトのスマホ対応はここには含まれない**（後述）。

### 🖱️📱 `PointerInput` ― マウスとタッチを1つの窓口に
盤の操作が `Mouse.current` を直に読んでいたので、**タッチでは何も動かなかった**。
触る側が2種類の入力を場所ごとに書き分けると必ず片方を忘れるので、1本にまとめた。
- 押す/離す/位置は「マウス左ボタン」と「1本目の指」を同じものとして扱う。
- **ホイールとピンチを `ZoomStep` に正規化**（＋で寄る／だいたい ±0.4）。
  ⚠ ホイールの生値は環境で ±1 だったり ±120 だったりする。単位の違いを呼ぶ側に吸わせない。
- **指が2本のときは「押している」と言わない**（ピンチ中に盤が掴まれて飛ぶのを防ぐ）。
- 状態を持つので**1フレームに1回だけ**計算する。`Tick()` を誰かに呼ばせると忘れるので、
  **読まれたときに自分で1回だけ**更新する。

**踏んだ穴2つ（どちらもタッチを実際に注入して見つけた）**
1. **指を離したフレームでマウス処理へ落ちて `Released` が False に上書きされていた。**
   `touchCount > 0` だけで打ち切っていたのが原因（離した瞬間は 0 になる）。
2. ⚠⚠ **`phase == Ended` を見て「離した」と判定してはいけない。**
   Ended は**次に指が触れるまで残り続ける**ので、毎フレーム「離した」が立ち、
   しかもマウス処理へ行かなくなって**一度タッチするとマウスが永久に効かなくなった**。
   → 「指が下りていた→下りていない」の**変わり目**で取る（`prevDown`）。

### 📱 盤の操作
- 地上盤：1本指で掴んで動かす／2本指ピンチで寄る・引く／タップで選ぶ（既存のドラッグ判定を流用）。
- 迷宮盤：`CameraController.HandleTouchPan` を足した（PCの WASD にあたる操作）。
- 配置は**タッチだけ「指を離した瞬間」**に置く（押した瞬間だと、盤を掴んで動かす操作が
  そのまま配置になる）。マウスは押した瞬間のままにした（手応えが良いので変えない）。
- ⚠ タッチには右クリックが無い。撤去は下部バーの『消去』ツールで行う。

### ⌨️ ホットキー（`Hotkeys`）
`1`〜`8`＝下部バーの配置ツール（左から順）／`Esc`＝開いているパネルを閉じる→無ければツール解除／
`Space`＝前半は『侵略開始』・後半は『ターンを終える』（**戦闘中は何もしない**＝事故防止）／
`Z X C R T`＝図鑑・研究・魔王・遺物・拡張。
⚠ パネルは**上部メニューのボタンをそのまま押す**（`onClick.Invoke`）。開き方を二重に書くと
作法（Refresh・排他・音）が必ずずれる。
⚠ `GridInputHandler` に残っていた旧デバッグの `4/5/6` を**外した**。あれのせいで
1〜8 を素直に配れず `1,2,3,7,8,9,0` という覚えられない並びになっていた。
ツールチップに `[1]` `[Z]` `[Space]` を添えた（覚えてもらわないとホットキーは無いのと同じ）。

### 📐 小さい画面のUI（応急処置）
`UIKit.ReferenceRes()`：短辺 ≤560 → 1280×720 ／ ≤820 → 1600×900 ／ それ以外 1920×1080。
基準を下げると全体が拡大されるので、**組み直さずに**指で押せる大きさへ寄せられる。
縦長画面では `matchWidthOrHeight = 1`（高さ合わせ）にした。

⚠⚠ **これは応急処置で、スマホ対応の完了ではない。**
このUIは 1920×1080 前提で 24〜42px のボタンと 1820px の全画面パネルで組んである。
**横に長いバーは縦長画面で必ず溢れる**ので、本当のスマホ対応にはパネルごとの組み直しが要る。

### 検証（Play・決定的／タッチは InputSystem に注入して実測）
押す→動かす→離す の3段が正しく立つ／離した判定は**1フレームだけ**／
そのあとマウスのホイールが効く（zoom 8→6.46）／
2本指で `Held=False`・広げて `ZoomStep=+0.400`・縮めて `-0.400`／
地上盤にピンチを流して zoom 8→4.8（寄った）／
ホットキー 1〜8 が下部バーの並び順（トーテム/罠/スポナー/ボス/特殊敵/宝箱/部隊/消去）と全一致／
Z で図鑑が開き Esc で閉じ、2回目の Esc は false（閉じるものが無い）。

---

## 2026-08-11 G-3b の穴埋め ①：死にノード10件を配線

**発端**：G-3b の表（研究ツリー拡張）に対して「実装してないのはない？」と問われ、
`ResEffect.None`（＝解禁専用）のノード68件について
**そのidが Research.cs の外から一度でも読まれているか**を機械的に照合したところ、**10件が誰にも読まれていなかった**。
＝ 説明を読んでRPを払っても**本当に何も起きない**。プレイヤーには気づきようがない。

| id | 説明の約束 | 直し方 |
|---|---|---|
| `d_floor6` `d_floor7` | 第6/7層の追加 | `DungeonFloorManager.MaxFloors` を新設（5固定→研究で7まで） |
| `d_slot1` `d_slot2` | 配置枠 +2／+2 | `PlacementCap` に加算 |
| `m_slot2` | 部隊枠 +1 | `SquadMaxSlots` に加算 |
| `d_relic4` | 遺物スロット4つ | `SlotCount` の上限 3→4 |
| `m_evo4` | （王種への進化） | **形態が無いので効果を作り直した**：任命ボスの HP+0.6／攻撃+0.4 |
| `m_evo5` | （古代種への進化） | 同上：**個体Lv上限 50→60** |
| `s_road` | 眷属と斥候の移動力 +1 | `KinRoster.MovementOf` と `ScoutSystem.Movement` に加算 |
| `s_influence2` | 毎ターン威名 +10 | `DiplomacySystem.IncomePerTurn` に加算 |

⚠ `m_evo4/5` だけは「配線」では済まなかった。**5段階目の形態そのものが存在しない**ので、
名前に合う実効果を既存の軸で与え、**説明文の方を実際に合わせた**（王種＝ボスの格／古代種＝Lv上限）。
嘘の説明を残すより、できることを正しく書く方を選んだ。

### ⚠ 途中で踏んだ罠2つ
- **`ScoutSystem.Movement` が `const`** だった。研究で伸びる値を const にすると
  コンパイル時に焼き込まれて**一生反映されない**（`SquadMaxSlots` で一度踏んだのと同じ）。
  `MinionRoster.MaxLevel` も同じ理由で const → プロパティにした。
- **`RelicManager.slots` は長さ3の配列で、しかもセーブに載る。**
  上限を4に増やすと、**3個で保存された古いセーブを読んだ瞬間 `slots[3]` で添字外**になる。
  → `EnsureSlots()`（足りなければ伸ばす）を読む前に必ず通す。

### 検証（Play・決定的）
研究を入れる前後で値が変わることを1件ずつ実測：
配置枠 14→16（`d_slot1`／`d_slot2` それぞれ）／部隊枠 5→6／遺物スロット 1→2／
個体Lv上限 50→60／斥候の移動 4→5・眷属の移動 3→4／威名 4→14／MaxFloors 5→6→7。
**実際に7層まで追加できること**も確認（4回足せて5回目は `CanAddFloor=false`）。
最後に**最初と同じ走査をやり直して、死にノード 0 件**を確認。

---

## 2026-08-11 王種・古代種を本物にする（m_evo4 / m_evo5）

直前の①では「5段階目の形態が無い」ので **m_evo4/m_evo5 に代用の効果**を与えて凌いでいた。
ユーザーの指示で**形態そのものを作った**ので、代用は取り消して**本来のゲートに戻した**。

### 🧬 追加した12形態（34種 → **46種**）
最上位Ⅲの6形態それぞれに、**王種(depth4) → 古代種(depth5)** を1本ずつ繋いだ。

| 最上位Ⅲ | 👑 王種(depth4) | 🦴 古代種(depth5) |
|---|---|---|
| デスナイト | 破軍王 | 太古の亡霊王 |
| エルダーリッチ | 骸骨王 | 太古の骸神 |
| ベヒーモス | 巨獣王 | 太古の巨獣 |
| フェンリル | 狼王 | 太古の魔狼 |
| ゴブリンジェネラル | 覇王 | 太古の征王 |
| ゴブリンウィザード | 大呪王 | 太古の織手 |

⚠⚠ **`MinionCatalog` への追加は必ず末尾。** `Individual.catalogIndex` はセーブに載るので、
途中に挿すと既存のセーブで別の魔物に化ける。
⚠ **`MinionEvolution.TierResearchId` の `Clamp(depth,1,3)` を 1..5 に広げた。**
これを忘れると、王種も古代種も **m_evo3 で開いてしまう**（段を足したら必ずここも直す）。
⚠ `EvoFrom` は**子→親が1対1**なので、複数の王種から1つの古代種へ**合流はできない**。
今回は1:1で通した（合流させたいなら型から変える必要がある）。
⚠ 強さは `DepthMult`（depth4=×1.48／depth5=×1.60）が別途乗るので、
カタログ側の hp/atk は**depth3から素直に一段ぶん**だけにした。掛け算を二重に効かせない。
⚠ スキルは**2つまで**（3つ持たせると盤が読めなくなる）。親のを1つ継いで1つ足す形にした。

### 🎨 絵（PixelLab 12生成）
`create_map_object` 64×64・side で12体。ユニーク魔物と同じ手順・同じ置き場
（`Resources/DungeonTale/Chars/char_<id>.png`）。取り込み設定は既存の `char_koboild` に揃えた
（Point / PPU16 / 非圧縮 / **isReadable=true**）。
⚠ **アニメは無い**（1枚絵）。`MinionAnim` は絵が無い状態を許すので壊れないが、
34種が持っている idle/walk/hit/death は持っていない。付けるなら別作業（12種×4状態 ≒ 60〜100生成）。

### 検証（Play・決定的）
配下 **46種／絵あり46**（全部に絵が付いた）。深度と研究ゲートが
depth3→m_evo3 / depth4→m_evo4 / depth5→m_evo5 に正しく割れている。
デスナイトの個体で**実際に進化を通した**：m_evo4 前は `CanIndividualEvolveTo=False` で
`TryEvolveIndividual` も False → 研究後に True で **破軍王**（Lvは保持）→ m_evo5 後に **太古の亡霊王**、
そこから先の進化先は 0 件（＝最果て）。アトラスにも4体ぶん焼けていることを画素で確認（406〜441/441）。
PixelLab 残 **1,122 / 2,000**。

---

## 2026-08-11 新12種にアニメを付ける（idle/walk/hit/death・336枚）

### 🔑 `animate_image` を見つけたのが全部
最初は詰みかけた：**アニメAPI (`animate_character` / `animate_object`) は
「PixelLabが作ったidを持つもの」にしか効かない**。新12種は `create_map_object` の産物なので、
素直にやるなら `create_character`(pro＋`style_character_id`) で**作り直し**＝ 240〜480生成。

`animate_image` は **「loose sprite（ただの1枚絵）」を動かせる**唯一の口だった。
- 64×64・8コマ ＝ **1生成**（コストは総画素数で決まる）
- 出力は `frame_count + 1` 枚（index 0 は入力そのまま）＝ `MinionAnim` の連番規則にそのまま合う
- 入力は **`first_frame_url` を使う**（base64はMCP経由で切られることがあると明記されている）。
  元の map object の download URL をそのまま渡せた（**8時間は生きている**）。

結果 **12種 × 4状態 = 48生成**で済んだ（作り直し案の 1/5〜1/10）。

### 📐 コマ数
idle 6→7枚／walk 8→9枚／hit 4→5枚／death 6→7枚（`frame_count` は**偶数**指定・出力は+1枚）。
既存34種（idle4/walk6/hit6/death7）と揃ってはいないが、`MinionAnim` は
**連番が切れるまで読む**ので揃える必要は無い。

### ⚠ 踏んだ／避けた罠
- 取り込み設定は既存の `skeleton/idle/0.png` に**揃えた**（Point / PPU16 / 非圧縮 / mipmap無し）。
  ⚠ 既存アニメは `isReadable=False` なので、**画素差分での「本当に動いているか」検証はできない**
  （`GetPixels` が例外）。今回は `get_image` のインライン画像で動きを目視し、
  コマ数と `MinionAnim.Has` で配線を確認した。
- 308枚の再インポートは `StartAssetEditing`/`StopAssetEditing` で囲んだ（1枚ずつだと固まる）。
- ⚠ 四足（巨獣・狼）と幽体（亡霊王・織手）は**動きの指示を変える**。
  humanoid の "walking forward" をそのまま四足に投げると二足歩行しようとする。
  → 四足は "on four legs, gallop/lumbering cycle"、幽体は "gliding/drifting, hem trailing"。

### 検証（Play・決定的）
12種すべて idle=7 / walk=9 / hit=5 / death=7、**読めた総コマ数 336**、空の状態 0。
`MinionAnim.Has(id, walk/idle)` が全種で true。
実際に**隊に入れて盤に置き、戦闘に入れて4体をスポーン**させ、
破軍王・狼王・太古の巨獣・太古の織手が新しい姿で描かれることを画面で確認。
PixelLab 残 **1,074 / 2,000**（この回で48）。

---

## 2026-08-11 図鑑に王種/古代種が出ない・隊の6枠目が『クリア』に潜る（ユーザー報告2件）

どちらも**「段/枠が増えたのに、それを描く側が古い数のまま」**という同じ形の見落とし。

### 🧬 図鑑に王種(depth4)・古代種(depth5)が一度も出てこなかった
`RefreshMinionCodex` の段ループが `for (stage = 0; stage < 4; stage++)` で、
見出しの表 `stageNames` も4つしか無かった。**カタログには居るのに、描く側が3段までしか見ていない。**
→ 段数を**カタログから数えて出す**（`maxStage`）ようにし、`stageNames` に「王種Ⅳ」「古代種Ⅴ」を追加。
表に無い段が来ても「第N段」で出るので、**次に段が増えても勝手に載る**。

### 🧩 隊の6枠目が『クリア』の下に潜って読めなかった
トレイの幅も『クリア』の位置も **`5 * 100` のべた書き**だった。
枠は `i * 108` で置かれるので、6枠目(540〜642)が『クリア』(512〜632)に丸かぶりする。
研究『部隊枠+1/+2』や政策・属性で枠は 5→7以上に増えるのに、**5枠のときしか正しくなかった**。
→ 枠数から**毎回引き直す**：`slotW = min(108, (幅-クリア分) / 枠数)` で縮め、
『クリア』は `左 + 枠数×slotW + 12` に置く。`RefreshSquadTray` の中でやるので、
**研究で枠が増えたその場で追従する**（`BuildMinionCodex` は起動時に1回しか走らない）。

### ⚠ 検証で引っかかった罠（3度目）
図鑑のカードを数えたら 0 に見え、段の見出しが**6回ずつ**出た。
`Destroy` は遅延するので、**同じフレームでは古い子が混ざる**。
`activeSelf` で絞ったら 46枚ちょうど・見出しは家系ごとに1回ずつだった。

### 検証（Play・決定的）
図鑑：カード **46枚**（＝全種）、王種Ⅳ/古代種Ⅴ の見出しが3家系ぶん。画面でも確認。
隊トレイ：枠数7（5＋m_slot＋m_slot2）で、枠の右端 922 < クリアの左 940 ＝**重なり無し**。

---

## 2026-08-11 図鑑を進化ツリーにする（段＝列・進化元と線で接続）

46種になって**カードのグリッドでは系統が読めなくなった**ので、研究ツリーと同じ絵の言語に揃えた。

### 🌳 作り
- **段＝列**（基本／進化Ⅰ／上位Ⅱ／最上位Ⅲ／王種Ⅳ／古代種Ⅴ）。列の頭に見出しを1度だけ置く。
- **進化元→進化先を線で結ぶ**。線は `ResearchConnector` を**そのまま再利用**したので、
  研究ツリーと見た目が揃う（進化元が解禁済みなら緑＝この道は通れる）。
  ⚠ 進化は1親→複数子なので、合流（金の線）は使わない。
- 縦位置は **葉から詰めて、親は子の平均**に置く（`AssignCodexRows`）。
  これで枝が交差せず、どの子がどの親から出ているかが目で追える。
- ⚠ `MakeVScroll` → **`MakeScroll2D`** に変えた。6段×224px＝1,600px超で、
  縦だけだと右端（古代種）が掴めない。**Content には幅も入れる**（研究ツリーで一度踏んだ話）。

### ⚠ 「透けて読めない」は**バグではなかった**
最初のスクショでパネル越しに迷宮が透けていて、背景を締めようとした。
確かめたら `CanvasGroup.alpha = 0.39` ＝ **`PlayFadeIn` の途中で止まっていた**だけで、
パネルの色自体は不透明（alpha 1.000）。**エディタが tick しないとフェードが完了しない**
（→ [[tooling-traps]]）。手で alpha=1 にしたら普通に読めた。**直さなくてよかった**。

### 検証（Play・決定的）
全体46枚／不死16・獣13・魔族17（＝46）。線の本数は各家系「種類数−根の数」と一致
（不死13・獣11・魔族15）。カードの右端 1,564 < 内容幅 1,622 で横にも収まっている。
段の列位置が x=0/268/536/804/1072/1340 と等間隔に揃っていることも実測。

---

## 2026-08-15 T60以降のカーブを測った（ずっとの宿題）

⚠ **実時間の通しプレイではない。** 1戦3分×100ターンは回せないので、
**実際のゲーム関数を呼んで**各ターンの両陣営の力（HP×攻撃×体数）を出した。
`AdventurerAI.WorldTier/LevelBase`・`EquipmentCatalog`・`MinionEvolution.DepthMult`・
`MinionRoster.LevelMult`・`DemonLord.MinionPowerMult`・`MutationSystem`・`PlacementCap` は
**すべて本物を呼んでいる**（模型で作り直すと、答えが式の写し間違いに化けるため）。

### 📊 結果：**心配していた方向とは逆だった**
| T | 攻撃側 | 防衛側 | 防衛÷攻撃 |
|---|---|---|---|
| 10 | 65k | 52k | **0.80** |
| 20 | 397k | 338k | **0.85** |
| 30 | 838k | 1,722k | 2.06 |
| 50 | 1,628k | 9,738k | 5.98 |
| 70 | 3,365k | 33,055k | 9.82 |
| 90 | 5,381k | 84,671k | **15.73** |

序盤（T10〜20）の拮抗は良い。**T30で逆転し、T90で16倍**＝中盤以降が緩くなる。
⚠ しかもこれは**防衛側の下限**（感情・遺物・トーテム・部隊バフ・ゴエティアを入れていない）。

### 🔍 原因＝**掛け算の軸の本数**
こちら：種の素(HP×2.25/攻×2.64)・段DepthMult(×1.43)・個体Lv(×2.24)・
装備(HP×2.90/攻×3.35)・魔王Lv(×1.69)・体数(×2.25) ＝ **総合 ×3,816**
冒険者：ランク(×1.67・**T30で上限7に飽和**)・脅威度(**上限6で飽和**)・
レベル(×3.06＝**実質これだけ**)・装備・人数(×2.3) ＝ **総合 ×129**

**`difficulty-curve-orders` の「掛け算の軸を減らす」を冒険者側にだけ適用して、
自分側に適用していなかった。** これが正体。
⚠ **先日足した王種・古代種がこの跳ねの主因の一つ**（T40→50で×3.5、T60→70で×2）。
段を足すこと自体は良いが、**既存の4軸に6本目を積んだ**形になっていた。

### 💰 コストは制約になっていない
18体を最果てまで＋全装備 ＝ **44万DP**／累計DPは **T60で57万** ＝ **払えてしまう**。
（1体：召喚194＋進化 175/375/700/1150/1550 ＝ 4,144DP／鍛造は武具で 20,300DP）

### 🎯 直し方（3案・ユーザー判断待ち・**未着手**）
1. **こちらの軸を飽和させる（推奨）**：装備グレードか個体Lvを逓減／上限つきに。
   装備を等比(1段+22%)から逓減にすると ×2.9 → ×1.8 程度に落ちる。
2. **段と装備を二者択一に近づける**：`DepthMult` を廃し、段の強さはカタログ値だけで表す。
3. ❌ **相手の飽和を外す**（`WorldTier` の上限7を上げる）は**原則に反する**＝先送り。

### 🧬 変異は効いていたが足りない
T100で与ダメ ×0.74（抑制135%まで買われた状態）。方向は正しいが**16倍差を埋める規模ではない**。

---

## 2026-08-15 ⚖️ カーブの手当て（①軸の飽和 ＋ ②段と装備の二者択一）

ユーザー決定：**①と②の組み合わせ**（③＝相手の上限を上げるは原則違反なので不採用）。
触ったのは**3式だけ**。冒険者側のコードは**1行も触っていない**。

### 手当ての中身
| # | 場所 | 旧 | 新 |
|---|---|---|---|
| ① | `MinionRoster.LevelMult` | `1+(lv-1)*0.04` の直線（Lv50=×2.96） | **Lv20までは+4%/Lv据え置き、以降+1.5%/Lv**（Lv50=×2.21） |
| ① | `MinionEvolution.DepthMult` | `1+depth*0.12` の直線（段5=×1.60） | **飽和表** 1.00/1.12/1.24/1.32/1.38/**1.42** |
| ② | `MinionRoster.EquipAtkMult/HpMult` | グレード倍率をそのまま | **段が深いほど装備の"上乗せ"が痩せる**（1段-11%・下限4割） |

⚠ `DepthMult` は**消さずに飽和させた**。消すと「タンク進化は攻撃がほぼ動かず進化の実感が無い」という
元の問題がそのまま戻る。**一段ごとの手応えは残し、積み上がりだけを削る**。
⚠ ②は `EquipmentCatalog.grades` **そのものは触っていない**。あの表は冒険者と魔王も引いているので、
表を弄ると相手まで弱くなる。痩せさせるのは `MinionRoster` の中＝**配下だけ**。
⚠ 上乗せ分(1.0超)にだけ掛ける。銅(0.85)のような1未満を救うと「深い段ほど貧弱な装備が有利」が生まれる。
⚠ 装飾品には掛けない（種類で選ぶ層で、グレードのように積み上がらない）。

### 結果（同じ前提・同じ実関数で再測）
| T | 旧 防衛÷攻撃 | **新** | 効き |
|---|---|---|---|
| 10 | 0.80 | **0.80** | ×1.000（**序盤は完全に据え置き**） |
| 20 | 0.85 | **0.85** | ×1.000 |
| 30 | 2.06 | **1.73** | ×0.841 |
| 50 | 5.98 | **3.02** | ×0.505 |
| 70 | 9.82 | **2.88** | ×0.293 |
| 90 | **15.73** | **3.00** | ×0.191 |

**16倍まで走っていたものが3倍で頭打ちになった。** 序盤（T10-20）の拮抗は1ミリも動いていない
＝**削ったのは終盤の積み上がりだけ**。

### 副作用の確認（3つとも健全）
- **進化はまだ割に合う**：オリハルコン・Lv50で段を1つ上げるごとに **×1.41〜×2.01**。
- **装備を1段上げる意味も残る**：段5でも **+10〜16%**（段0は+22〜24%）。「払う意味が無い」域ではない。
- **Lvは単調増加のまま**：Lv20まで旧と同一、Lv30で×1.91、Lv50で×2.21。**上げ損はどこにも無い**。

### ⚠ まだ実時間の通しプレイはしていない
これは実関数を呼んだ計算。**体感・所要時間・実際に詰まる場所は未確認**のまま。

---

## 2026-08-15 📋 G-3b の穴埋め②③①（表と実装の差を全部潰した）

`research-dead-nodes` に残っていた未実装3件を全部片付けた。**3件とも「説明は書いてあるが何も起きない」型**。

### ③ 大罪之刻印 3種 → 7種（`e573547`）
覇道の排他分岐に **怠惰/嫉妬/傲慢/色欲** を追加（各3ノード＝12ノード）。
- 😴 怠惰＝研究点と守り（鎮座と噛み合う）／😖 嫉妬＝耐性と変異抑制（相手の伸びを止める唯一の道）
- 😤 傲慢＝魔王自身（親征と噛み合う）／😍 色欲＝感情と育ち（誘導経済と噛み合う）
- ⚠ **排他なので1周で取れるのは3ノードのまま**＝カーブは太らない。だから各分岐は既存3本と横並びに揃えた。
  1本だけ強いと「実質そこしか選べない」＝排他にした意味が消える。
- ⚠ 魔王ツリーの `k_sin_*` が同じ7つの大罪名で丸かぶりだったので **『〜の兆し』に改名**（idは据え置き）。
  兆しが出る(魔王ツリー) → 刻む(覇道) の順に読める。

### ② 錬成の等級 7段 → 14段（`e573547`）
**旧仕様の正体**：`r_grade_epic`〜`genesis` の7ノードは「配下の攻撃+X%」という**無条件の全体倍率**で、
説明の『叙事詩級を鍛えられる』は**嘘**だった（鍛造上限を読むのは mithril/orichal の2つだけ）。

- 叙事詩/伝説/究極/幻想/世界/神/創世 を `grades` の**末尾に追加**（索引はセーブに載る）
- **等級段は1段 +6%**（素材段は+22%）。全体倍率で配っていた 攻+44%/HP+20% を**個体ごとの鍛造へ移しただけ**で、
  段5換算の力は旧比 **×1.05 ＝ほぼ据え置き**。**強さの追加ではなく支払い方の変更**。
  → 投資しない人は以前より弱くなる（タダで貰えていた分が消えた）
- 1スロットを創世まで **71,150DP / 757素材**（武具2枠で14万DP）＝**全員には配れない沼**。誰に持たせるかを選ぶ
- 上限の式を `EquipmentCatalog.ResearchGradeCap()` に**集約**（`MinionRoster` と `DemonLord` に同じ式が2つあった）
- ⚠⚠ **冒険者は `HeroMaxGrade`(=6) で締めた。** `grades.Length-1` のままだと、等級を足すたびに
  相手の上限も黙って上がる＝直したカーブが戻る。実測でも rank7/gear100 で最大5(アダマンタイト)止まり
- 図鑑の鍛造ボタンが研究上限を見るようにした（見ないと「押せるのに警告が出るだけ」が7つ増える）

### ① 魔法の属性 6種 → 16種（`5ebb966`）
**旧仕様の正体**：`g_elem_water/wind/void` と `g_der_*` の10ノードが「魔法威力+X%」で、
`MagicElement` は6種のまま＝**属性は1つも増えていなかった**。

- 基本9（火/氷/雷/土/光/闇/**水/風/無**）＋派生7（**影/血/木/神聖/空間/時間/重力**）
- 🕳️ **虚無は相性表を読む前に 1.0 を返す**＝あらゆる属性耐性を無視。属性を増やす一番の見返りをここに置いた
- 神聖は不死に **×2.0**（聖光の1.7を超える）／影蝕は聖職者にも通る（呪詛と同じ0.5だが不死耐性を抜く）
- `ElementResearchId` を switch から**配列**に。switch のままだと属性を足したとき `default` の呪詛に落ちて
  「研究したのに使えない／していないのに使える」が同時に起きる
- 王種/古代種の術者に派生属性（骸骨王=影蝕・大呪王=虚無・骸神=重力・織手=時間）
- ⚠⚠ **冒険者には派生と虚無を配らない。** 相手に配ると軸が1本増えて終盤だけ跳ねる。上位でも水流まで

### 検査
- 研究221ノード：id重複0・前提切れ0・表示名かぶり0
- **解禁専用85ノードのうち読み手が無いもの＝0件**（`m_evo4/5` は `"m_evo"+n` で組み立てるためgrepに出ないだけ）
- 研究の上限が1段ずつ開くこと・冒険者が等級段と派生属性に届かないことを**実関数で確認**

---

## 2026-08-15 🎮 実時間の通しプレイ（T1〜T12）― 初回

**ずっと「一度もやっていない」と書き続けてきた実時間プレイをついに実施した。**
標準難易度・既定設定（標準/洞窟/宝箱中/1層/地上中）。攻略サイトを書くつもりで最善手を選びながら進めた。

### ⚙️ 前提（道具の話）
- ⚠⚠ **`Application.runInBackground = true` が要る。** エディタが非フォーカスだと `Time.frameCount` が
  進まず、戦闘が完全に止まる（残り時間180sのまま冒険者が入口から動かない）。
  30分ほど「戦闘が始まらない」と誤認した。**MCPからプレイするときは最初にこれを入れる。**
- ⚠ Canvasが全部 ScreenSpaceOverlay なので **MCPのスクショに映らない**。
  撮影時だけ ScreenSpaceCamera＋手前カメラ＋レイヤーUIに載せ替える必要がある。
  さらに **SurfaceCamera の cullingMask がレイヤー8だけ**なので、地上フェーズでは別途載せ替えが要る。

### 📈 実測した進行（T1→T12）
| T | DP | 名声 | 時代 | 支配 | 地上産出 | 世界水準 | 研究 | 魔王Lv |
|---|---|---|---|---|---|---|---|---|
| 1 | 200 | 0 | 0/210 | 18 | 17DP | F Lv2 | 0 | 1 |
| 2 | 1,461 | 54 | 29 | 18 | 17DP | E Lv5 | 6 | 2 |
| 3 | 2,492 | 58 | 70 | 18 | 18DP | E Lv7 | 11 | 3 |
| 5 | 1,665 | 276 | 118 | 18 | 39DP | C Lv12 | 20 | 5 |
| 8 | 1,727 | 408 | 157 | 25 | 60DP | Lv15 | 32 | 8 |
| 10 | 4,915 | 436 | 171 | 30 | 60DP | Lv17 | 35 | 10 |
| 12 | **6,845** | 503 | 181 | 32 | 83DP | Lv19 | **37（取れる研究0）** | 12 |

### 🚨 いちばん大きい問題：**T12以降、やることが無くなる**
- **T12時点で「いま取れる研究＝0件」。** 胎動の研究を全部取り切ってしまい、RPは貯まるだけ。
  （`m_evo2` 以降は**時代ゲート**で成長の時代まで開かない）
- **DPが6,845余る。** 配置枠は階ごと14で埋まり、隊は6枠上限、買うものが無い。
- 偉業も20/30達成済みで、残り10件は「感情ツリー」「祝祭」「拠点2つ」など**別systemの初回タスク**。
- 時代は 181/210 で、あとは**自然進行+5/ターンを6ターン待つだけ**。
- ⇒ **T12〜T18の6ターンは、押すボタンが「侵略開始」と「ターンを終える」しかない。**
  ユーザーの懸念「ただターンの経過を待つだけのターンはないか」への答えは **YES**。

### 🚨 2番目：戦闘が20秒で終わり、リアルタイム要素が死んでいる
- 実測：T1=16秒、T2=22秒、T3〜=20秒前後。**制限時間180秒は一度も使われない。**
- **号令4種（治癒300/落石350/魔王の一撃500/恐慌の波450）を使う場面が来ない。**
  押そうとすると既にウェーブが終わっている。一時停止すれば押せるが、
  **落石=350DPで70ダメージ**＝スケルトン1体42DPと比べて効率が一桁悪く、そもそも使う理由が無い。
- 侵入人数は T12でも2〜4人。**「波を捌く」感覚が無い。**

### 🚨 3番目：地上4Xが経済的に無意味
- **支配タイルを18→32に倍近く増やしても、産出はほぼ動かない。**
  産出は「人口が耕すタイル」だけなので、**人口2では2タイル分しか出ない**。
- **施設5つを約3,400DP掛けて建てたら、産出表示が1も動かなかった。**
  人口が届かない位置に建ったため。**どこにも説明が無い。**
- 序盤は「勝てる進軍先がゼロ」で完全に手詰まりになる（下記）。

### ⚔️ バランスの具体的な問題
1. **⚠⚠ 表示している数字と判定に使う数字が違う**（地上）
   UIは眷属の「力56」を出すが、**戦闘判定は軍力92**。辺境の守りは88なので、
   プレイヤーは「勝てない」と誤解して手が止まる。**実際は最初から勝てた。**
2. **⚠⚠ 鍛錬（500DP＋6素材）が力+1しか上げない。** しかもそのターンの移動力を全部消費する。
   守り88に届かせるには単純計算で1.6万DP。**完全に罠の選択肢。**
   対して**随行（配下を1体つける）は無料で軍力+41**。強さの差が80倍ある。
3. **罠が一度もとどめを刺さなかった**（偉業「罠でとどめを10回」が10ターンで 0/10）。
   毒沼を6つ以上置いたが、削るだけで殺し切らない。
4. **ゴブリン(75DP)がスケルトン(45DP)の完全下位互換**（hp0.9/atk1.0 vs 1.0/1.0）。買う理由が無い。
5. **ゾンビ→グールの進化が罠**：Tank→Meleeで役割が減り、HPも1.45→**1.2に下がる**。
6. **T1終了時に偉業が4件まとめて達成され、DPが200→1,461に跳ねる。**
   開始時点で条件を満たしている偉業（版図8＝開始18／発見2＝開始8／真名1＝開始1）があるため。
   **T1で作った「200DPしかない」緊張が、T2で完全に消える。**

### 🎯 逆に良かったところ
- ◎ **世界設定画面**：「開始予算1,000 − 建造費800 ＝ 初期DP200」の式が明示され、取捨選択が即わかる。
  難易度も「仕組みは変わらない。世の本気度と取り分だけが動く」＋倍率表記で誠実。
- ◎ **腹心の報告**：ターン頭に情勢＋進言3つ＋初出説明。**これが無かったら何をすればいいか分からない。**
- ◎ **隊の役割コンプ**（+10%/種・満員+15%）が良い設計。
  「5役割そろえて×1.55」を目指す組み立てが楽しい。**満員ボーナスが最安のラット14DPで取れる**のも良い。
- ◎ **トーテムが半径4**。密集配置なら攻+20%/HP+25%を360DPで全員に配れる。配置を考える理由になる。
- ◎ **地形が良い**。10×10のB1Fに1マス幅の隘路が2つ、B2Fには唯一の隘路が1つ。
  「どこで受けるか」の判断が成立している。
- ◎ ボスに**ゴエティアの名**（＋シトリー等）が付き、1.7倍に大型化する。手触りが良い。
- ◎ 冒険者のラベルが「E級 聖職者![探索] Lv.6」と読める。魔法詠唱も「中級 火炎!(MP:3)」と出る。

### 🖥️ UIの問題（実際に困った順）
1. **⚠⚠ ラベルが重なって読めない。** 配下を隣接させると名前が団子になる
   （「スケルトンアーデ<重なり>ケルトンソルジャー #3」）。トーテム名・スキル名・ダメージ数字も混ざる。
2. **⚠⚠ 腹心の報告が閉じない。** 開いたまま他のパネルを開くと二重に重なる。
   `侵略開始` を押しても残り続け、**戦闘中ずっと盤の中央を隠す**。
3. **⚠ 号令バーは買えないときだけ文字が重なる**（「DPが足りない」と「（要300）」が衝突）。
4. **⚠ 地上の上部バーが3段びっしりの数字**（支配18/4502・産出4種・時代・政体・他の魔王3人・威名・
   勝利条件・形見）。優先順位が無い。「4502」は意味が伝わらない。
5. **⚠ 地上の左メニューが11個の2文字ボタン**（個域/勢力/眷属/軍団/ツリー/政策/属性/外交/時代/勝利/物語）。
6. **⚠ ヘクス盤が画面の1/4しかなく、周りは紫の空白。** UIは窮屈なのに盤は小さい。
7. **⚠ 遊び方の記述が古い**：「上部の『地上』で世界地図に出られます」→ **地上ボタンは存在しない**
   （ターン後半に自動で移る作りに変わった）。魔王・研究・感情・進化への言及も無い。
8. **⚠ 準備中は配下が名前ラベルだけ**（実体は戦闘開始でスポーン）。宝箱の絵と紛らわしい。
9. **⚠ 魔王パネルに閉じるボタンが無い**（同じタブをもう一度押す＝気づきにくい）。

### 🔧 コード上で気づいた点
- `CostOf(FeatureType.Boss)=376` は**どこからも使われない死んだ値**（ボスは配置無償・返金対象外）。
- 撤去の返金はコメントが「50%返金」だが、**実際は100%返金**（`RemoveFeature`）。
  階層拡張で壊すときの `RefundRecords` だけが50%。**2つの経路で率が違う。**
- `CanDrill` は `mp < MovementOf` を「もう動いた」と判定するので、
  研究で最大移動力が上がった直後は**動いていないのに**「今ターンはもう動いている」と出る（嘘になる）。

### 🛠️ 改善・調整の計画（優先度順）

**P0：胎動の中身を埋める（「待つだけのターン」を消す）**
- ① **研究が尽きないようにする。** T12で0件は早すぎる。対策は3つのどれか：
  - 胎動の研究ノードを増やす（今回16属性・14等級・大罪7を足したので、**そのうち胎動に配れるものを回す**）
  - `m_evo2` の時代ゲートを外し、**別の条件**（進化させた数・配下Lv）で開く
  - **反復研究（`F(...)`）を胎動にも1本置く**（RPの行き先を常に残す）
- ② **DPの行き先を作る。** 配置枠14×階層が埋まったら買うものが無い。
  - 個体への投資（鍛造・装飾品）を**もっと早く開ける**。今回 T12まで「武具を2つ鍛える」偉業が 0/2 だった
  - 階層の横拡張（`ExpandFloor`）をもっと安く・分かりやすく
- ③ **開始時点で満たしている偉業を無くす。** 版図8→**25**、発見2→**6**、真名1→**2** など、
  「開始状態＋1手」で取れる値に上げる。T1の緊張を2〜3ターン持たせる。

**P1：戦闘を「捌く」ものにする**
- ④ **侵入人数と波を増やす。** いまT12でも2〜4人・20秒。制限時間180秒に対して1割しか使っていない。
  人数を増やすか、**ウェーブを複数回に分ける**（1ターンに3波など）。
- ⑤ **号令を実用圏に。** 落石350DPで70ダメージは弱すぎる。
  威力を上げるか、**DPではなく専用リソース（感情など）**にして「使うのが当たり前」にする。
- ⑥ **罠がとどめを刺せるようにする。** 10ターンで撃破0は死に要素。

**P2：地上を「育つ」ものにする**
- ⑦ **産出を支配タイルにも少し乗せる。** いまは人口が耕すタイルのみ＝**支配を倍にしても+0**。
  Civの原則（面積に比例させない）は正しいが、**0はやりすぎ**。支配タイルに小さな定数を置く。
- ⑧ **施設を建てる前に「効くかどうか」を見せる。** 3,400DP使って産出+0は事故。
  建設UIに「この施設は人口が届いていないので産出しません」を出す。
- ⑨ **序盤の詰まりを解く。** T1〜T3は**勝てる進軍先がゼロ**。
  最寄りの辺境の守りを 88 → **40程度**にするか、初期眷属の軍力を上げる。
- ⑩ **鍛錬の効果を実用値に**（+1 → 最低でも +10〜15）。または随行と役割を分ける。

**P3：UIの読みやすさ**
- ⑪ **ラベルの重なり解消**（最優先）。隣接した配下の名前が団子になる。
  縦にずらす／ホバー時のみ出す／アイコン＋Lvだけにする、のどれか。
- ⑫ **腹心の報告をモーダルにする**（閉じるまで他を触れない／侵略開始で自動的に閉じる）。
- ⑬ **地上の上部バーを3段→1段＋詳細タブに**。「支配18/4502」は「18タイル」に。
- ⑭ **眷属の表示を「軍力」に統一**（力56ではなく軍力92を出す）。これは**誤解を生む最悪の表示**。
- ⑮ 遊び方ページの更新（地上ボタンの記述が古い）＋レイアウト（右2/3が空白）。

**P4：まだ触れてもいない層がある**
今回の12ターンで**一度も触らなかった／触る理由が無かった**もの：
**感情ツリー・遺物・装飾品・鍛造・ガチャ・眷属の増員・政策・属性ツリー・外交・軍団**。
偉業の未達10件のうち4件がこれら（感情3つ／祝祭／拠点2つ／独立勢力）。
⇒ **腹心の進言が「まだ触っていない系統」を優先して出す**ようにするだけで、体験がかなり変わるはず。

---

## 2026-08-15 🗺️ 通しプレイを踏まえた改善計画

### 診断：問題は3層に分かれる
プレイして分かったのは、**「足りない」ものより「見せていない」ものの方が多い**ということ。

| 層 | 中身 | 直す難度 |
|---|---|---|
| **① 緊張が一度も無い** | 12ターン通して**魔王HPは1.00のまま**。配下を1体も失わなかった。戦闘は毎回20秒で片付く。**危なくないので、乗り切った解放感も無い。** | 中 |
| **② 持っているのに見せていない** | ガチャ・行商人・鍛造・装飾品・感情ツリー・遺物・政策・属性——**全部実装済みで、全部『図鑑』パネルの中**。12ターン誰も案内してくれなかった。DPが6,845余ったのは**使い道が無いのではなく、使い道を知らなかった**から。 | **低** |
| **③ 本当に足りない** | 胎動の研究の数（T12で0件）・戦闘の密度・地上の産出 | 高 |

**②が一番安く、一番効く。** ここから手を付ける。

---

### P0：嘘と事故を消す（最優先・小さい）
「間違った情報を出している」ものだけを潰す。仕様変更ではないので迷いが無い。

| # | 場所 | いま | 直し方 |
|---|---|---|---|
| 1 | 眷属のUI | **「力56」と出すが判定は「軍力92」** | **軍力を表示する。**「勝てないと思って手が止まる」最悪の表示 |
| 2 | 盤のラベル | 隣接した配下の名前が団子になり読めない | 縦にずらす／アイコン＋Lvだけ／ホバー時のみ全文 |
| 3 | 腹心の報告 | 閉じずに残り、**戦闘中も盤の中央を隠す** | モーダルにする＋『侵略開始』で自動的に閉じる |
| 4 | 号令バー | **買えないときだけ**文字が重なる | 不足時のセルを2行にする |
| 5 | 遊び方 | 「上部の『地上』で世界地図に出られます」＝**そのボタンは無い** | 記述を現行のフェーズ制に更新。魔王・研究・感情・進化も追記 |
| 6 | 鍛錬 | **500DP+6素材で力+1**（随行は無料で+41） | 効果を+10〜15にするか、廃止して随行に一本化 |
| 7 | ゴブリン | スケルトン(45DP)の完全下位互換で75DP | 役割かCPを変えて存在理由を作る |
| 8 | ゾンビ→グール | Tank→Meleeで役割が減り**HPも1.45→1.2に下がる** | 進化で下がるステータスを作らない |
| 9 | 返金 | `RemoveFeature`は100%、`RefundRecords`は50%。コメントは「50%」 | どちらかに揃える |
| 10 | `CostOf(Boss)=376` | どこからも使われない死んだ値 | 消すか、ボスにも費用を課す |

---

### P1：導線 ―「持っているのに見せていない」を見せる（安くて効果が大きい）
1. **腹心の進言を『まだ触っていない系統』優先にする。**
   感情ツリー／鍛造／装飾品／ガチャ／行商人／遺物／政策／属性を、**未使用なら進言の先頭に出す**。
   いまの進言は「罠を置け」「進軍しろ」「BPを振れ」の3つで、**12ターン一度も他系統を指さなかった**。
2. **図鑑を『工房』として外に出す。** ガチャ・行商人・鍛造・装飾品が全部図鑑パネルの中に埋まっている。
   上部バーに独立したタブを作るか、少なくとも**進言から直接開けるボタン**を置く。
3. **DPが余ったら進言に出す。**「DPが3,000以上余っています。鍛造か召喚の儀に回せます」。
4. **施設の建設UIに「人口が届いていないので産出しません」を出す。** 3,400DP溶かした事故の再発防止。

---

### P2：緊張を作る（本丸）
**12ターン、一度も危なくなかった。** ここを直さないと他を直しても面白くならない。
- **侵入人数／波を増やす。** いまT12でも2〜4人・20秒で、**180秒の制限時間の1割しか使っていない**。
  1ターンを複数波に分けて、**回復と再配置の判断**が要る形にする。
- **罠がとどめを刺せるようにする。** 10ターンで撃破0（偉業0/10）は死に要素。
- **号令を実用圏に。** 落石350DPで70ダメージ＝スケルトン42DPより一桁効率が悪い。
  **DPではなく感情を消費**にして「毎戦使うもの」に変えるのが筋（DPは建設と競合して使われない）。
- **失う体験を作る。** いま配下は死んでもロスターに残る。深い階の全滅など、条件付きで失う形を検討。

---

### P3：胎動の中身を埋める（T12〜T18の空白を消す）
- **時代ゲートを行動ゲートに置き換える。** `m_evo2` を「時代=成長」ではなく
  **`Cond.Evolved 4`（4体進化させる）** で開く。時間で待たせず、**行動で開ける**形に。
- **胎動に反復研究を1本置く**（`F(...)`）。RPの行き先が常に残る。
- **開始時点で達成済みの偉業を潰す。** 版図8→**25**／発見2→**6**／真名1→**2**。
  いまはT1終了時に4件同時達成で**DPが200→1,461に跳ね、初手の緊張が消える**。
- **今回足した属性16・等級14のうち、胎動に配れるものを前倒しする。**

---

### P4：地上に手応えを
- **支配タイルに小さな定数産出を置く**（例：+1DP/タイル）。
  いまは人口が耕すタイルのみ＝**支配を18→32に倍増しても産出+0**。
  「面積に比例させない」原則は正しいが、**0はやりすぎ**。
- **初期人口か食料の伸びを上げる。** T7でまだ人口2。施設が効き始めるのが遅すぎる。
- **序盤の詰まりを解く。** T1〜T3は勝てる進軍先がゼロ。本拠の周りの辺境だけ守りを下げる。
- 正解手順（**まず前線の自領へ移動 → 次ターンに攻撃**）を進言かツールチップで示す。

---

### 効果の測り方（次の通しプレイで確認する）
| 指標 | いま | 目標 |
|---|---|---|
| 空ターン率（研究も買い物も無いターン） | **T12〜T18＝6/18ターン** | **0** |
| 戦闘の平均秒数 | 20秒 | 60〜90秒 |
| 12ターンで触ったシステム数 | 5（研究・進化・配置・トーテム・地上侵攻） | 10以上 |
| 魔王HPが0.7を下回った回数 | **0** | 1回以上 |
| DP余剰の最大 | 6,845 | 2,000以下 |

---

## 2026-08-15 ⚔️ P2：戦闘の密度（点滴 → 波）

### 真因は「人数」ではなく「届き方」だった
通しプレイの症状は「戦闘が20秒で終わる／号令を使う場面が来ない／捌く感覚が無い」。
人数を増やす前に実際の湧きを読んだら、**T12でも15体はちゃんと湧いていた**。
問題は `max(4.0 - turn*0.2, 1.5)` 秒おきに**1体ずつ**送っていたこと。
湧くそばから溶けるので、**画面に居るのは常に2〜4体**。群れにならず、圧力にもならなかった。

### やったこと（`ddede62`）
- 同じ人数を**3つ前後の塊**に分け、**塊の中は0.35秒おき＝ほぼ同時**に着弾させる
- 塊と塊のあいだ＝ `max(5, 9 - turn*0.2)` 秒
- `FlushRemaining`（階層が抜かれたとき）は「残り全部」→**「その塊のぶんだけ」**

⚠⚠ **総人数も個々の強さも1ミリも変えていない。変えたのは届き方だけ。**
相手の飽和（上限20体）を外すのは `difficulty-curve-orders` の原則に反するので触っていない。

⚠ **息継ぎを16秒にしたら失敗した。** 塊が5秒で溶けたあと**11秒だれも居ない**時間ができ、
密度が上がるどころか「待ち」が増えた。実測して 5〜9秒に詰めた。
**息継ぎは戦闘より短くする**——前の塊を捌いている最中に次が着く長さ。

### 実測（T12相当・防衛3体の条件）
| | 前 | 後 |
|---|---|---|
| 同時侵入 | 2〜4体 | **最大8体** |
| 戦闘時間 | 約20秒 | **74秒時点でまだ継続** |
| 魔王HP | 12ターン通して 1.00 | **0.75 まで削られた** |
| 落石(350DP)の与ダメ | **70** | **420** |

**落石の数値は1つも触っていない。** 密集そのものが価値を作った
（範囲攻撃なのに1体にしか当たっていなかった）。
＝「号令が弱い」という当初の診断は**半分間違い**で、正しくは「号令が当たる状況が無かった」。

### やらなかったこと（意図的）
- **罠の強化は見送った。** 「罠でとどめ0/10」は事実だが、罠を強くするのは**こちらを強くする**方向で、
  直したばかりのカーブに逆行する。真因は威力ではなく**置き場所**
  （隘路に置くと満タンの相手に当たるので、とどめは取れない。**奥に置けば仕留め役になる**）。
  → 威力ではなく**教え方**の問題として P1/導線側で扱う。
- **号令の通貨をDPから感情へ移す案も見送った。** 密度の改善だけで価値が6倍になったので、
  通貨を変える必要がなくなった。**変えずに済むなら変えない。**

---

## 2026-08-15 🧭 P3の方針決定（測定を踏まえて修正）

### 再プレイ T1〜T6 の測定（P0/P1/P2 適用後）
| | 前 | 後 |
|---|---|---|
| 戦闘の長さ | 毎ターン約20秒 | **19/21/42/21/21/67秒超** |
| DP余剰 | T12で **6,845** | **91〜913**（鍛造が吸った：T3で7本・T4で6本） |
| 災厄 | 気づかず | **未発火**（進行117・発火は160から） |

**DPの余剰はP1だけで解決した**（使い道が無かったのではなく、知らなかった）。

### ⚠ 診断の訂正：「胎動の中身が足りない」は本当だった
T6時点の内訳：**胎動で取れるノードは40本**（25本取得済・15本がRP待ち）、
**時代ゲートで止まっているものが41本**。
40本 ÷ 18ターン ＝ **2.2本/ターン**。取り切るとT11〜12で枯れる（前回の観測と一致）。

⚠⚠ **当初「待機中の41本を胎動へ流す」案を出したが、これは悪手だった。**
あの41本は成長・終焉のために書かれたもの（等級14段・大罪・古代種…）で、
胎動に引くと**胎動が胎動でなくなる**。1つ目の時代で「神級の鍛造」が見えるのはおかしい。
→ **胎動には胎動のための新しい中身を書く。**

### 決定：①の中身を差し替え
- **(a) 行動ゲート化は進化段だけに絞る**（`m_evo2` を「時代=成長」→「4体進化させたら」）。
  時代を待つより「育てたから開く」方が正しいゲートだから。
- **(b) ①の本体は新コンテンツの制作**。

### 新コンテンツの順（ユーザー決定）
**C → A → B、そのあと D と E。実装の重さは考慮しない（面白さ優先）。**

| # | 中身 | 状態 |
|---|---|---|
| **C** | **次の波の偵察**（先触れ）— 準備フェーズに「今回の敵はこう来る」を出す | **着手** |
| **A** | **落とし穴の罠** — 踏んだ冒険者を1階層下へ落とす | 次 |
| **B** | **配下の個性** — 召喚時に性格が付き、1体ずつ違う戦い方をする | その次 |
| **D** | **迷宮の掘削** — 通路を掘る/塞ぐ。迷宮ものの核。単独フェーズ規模 | 後で必ずやる |
| **E** | **事件（ランダムイベント）** — 選択肢つきの小事件 | 後で |

### Cを最初に置く理由
毎ターンが同じに感じる最大の原因は、**準備フェーズに「今回はどうするか」の入力が無い**こと。
毎回おなじ最適解を置き直しているだけになっている。
相手の編成が事前に分かれば、**毎ターン盤を組み替える理由**が生まれる。
置き場所（腹心の報告）も既にある。

### Cの設計
1. **ウェーブを前もって決める（pre-roll）**。
   いまは戦闘開始時に人数を決め、各冒険者が湧いた瞬間に職とランクを自分で引いている。
   予告するには**準備フェーズの頭で名簿を確定**させ、スポナーはそれを順に出すだけにする。
   ⚠ この「名簿を先に作る」土台は A（落とし穴で経路を変える）や D（掘削）でも効く。
2. **偵察の深さを研究で伸ばす**（＝胎動固有の新しい研究枝になる）
   人数 → 職の内訳 → 属性と魔法 → 個体の弱点、と段階的に見えるようにする。
   **これがそのまま胎動の密度不足への回答にもなる**（新しい枝が生える）。
3. 情報に対して**打つ手がある**ようにする（聖職者が多い＝呪詛が効かない／魔術師が多い＝遠距離で先に潰す 等）。

---

## 2026-08-15　新コンテンツ C：先触れと備え（実装完了）

「胎動が薄い」への回答その1。**準備フェーズに『今回はどうするか』という入力を作る。**

### 何が無かったか
毎ターンが同じに感じていたのは、盤の作り方に**その回だけの理由**が無かったから。
相手が誰か分からないので、毎回おなじ最適解を置き直すだけになっていた。

### 作ったもの

**① ウェーブの pre-roll（`WaveRoster`）**
旧：戦闘開始時に人数を決め、**各冒険者が湧いた瞬間に自分で職とランクを引いていた**。
　　これでは予告のしようがない（引く前だから誰も知らない）。
新：**準備フェーズの頭で名簿を確定**させ、スポナーはそれを順に出すだけにした。
　　人数の式は `DungeonAdventurerSpawner` から**そのまま**移しただけで、数も強さも変えていない。
⚠ 名簿は準備の頭で固まる。準備中に階層を足しても、その噂が届くのは次のターン
　（その場で敵が増えると「建てたら即罰される」ことになる）。
⚠ この土台は **A（落とし穴で経路を変える）** と **D（掘削）** でも要る。

**② 先触れ（読む）— 研究で深くなる**

| 研究 | RP | 読めるもの |
|---|---|---|
| （無研究） | ― | 「およそ 6〜10 体」という幅だけ |
| `d_omen1` 耳を澄ます | 4 | 正確な人数・最高ランク・平均Lv |
| `d_omen2` 斥候の目 | 8 | 職の内訳と目的（探索/踏破） |
| `d_omen3` 魔力の読み | 12 | 敵の属性と、**こちらのどの属性が通るか** |
| `d_omen4` 看破 | 18 | 1人ずつの名簿（ランク・職・Lv・魔法） |

**③ 備え（打つ）— `WardSystem`・そのターン限り・1つだけ**
研究 `d_ward`（6RP）で解禁。読めても打つ手が無ければ情報は飾りになる。

| 備え | DP | 対 | 効果 |
|---|---|---|---|
| 魔封じの結界 | 200 | 魔術師・聖職者 | 冒険者の魔法が半減 |
| 静謐の霧 | 240 | 聖職者 | 広域ヒール不発＋自己回復 1/4 |
| 軋む床 | 180 | 戦士 | 重装の移動 -40%（罠と配下の間合いに長く留まる） |
| 見張りの目 | 160 | 盗賊 | 略奪がほぼ止まる（装備水準の上昇を抑える） |
| 狭き門 | 260 | 大人数 | 一度に雪崩れ込む塊が半分 |
| 偽りの気配 | 300 | 踏破目的 | 踏破者が階段を見失い、探索者のように彷徨う |

⚠ **どれも「相手の得意を1つ潰す」だけ**にした。数値を盛る道具にすると掛け算の軸が1本増える
（→ カーブの手当てが台無しになる）。選び間違えれば効果はほぼ無い。
張り替えは**全額戻る**（読んで間違えた1ターンが丸ごと死ぬのは理不尽）。

**④ 天啓の輪**：`d_omen1`＝冒険者20体撃破／`d_ward`＝30体／
`d_omen2/3/4`＝**備えを2回・5回・10回張る**。**張るほど読みが安くなる**（read→act→read more）。

### 実測（確認したこと）
- 予告 `E/Thief Lv4, F/Warrior Lv5, E/Mage Lv5, F/Warrior Lv4, F/Thief Lv4, …`
  → 実際に湧いた5体が**そっくり一致**。pre-roll は効いている。
- セーブ→名簿を汚す→ロード で `ThiefLv4 / 軋む床 / 床x0.6` が完全復元。
- 胎動で取れるノード **40 → 45**。研究総数 221 → 226。

### 踏んだ罠（3つとも既知のものを踏み直した）
1. **`MakeVScroll` の Content は横ストレッチ。** `sizeDelta.x` に実幅を入れたら幅が2倍になり、
   pivotが中央なので**左右に半分ずつはみ出して見切れた**。→ 幅はビューポート幅、`sizeDelta.x` は 0。
2. **UI文字列に絵文字を書かない。** ⚔️🎭🌿🔮🜁🌫️ が全部 □ になった。→ 色帯と色つき文字に置き換え。
3. **`readonly` はセーブに乗らない。** 名簿を readonly List で持つとロード後に引き直され、
   **予告した波と違う波が来る**（＝予告が嘘になる／やり直しで引き直せる）。→ readonly を外して登録。

### 「多い」の線を測ってから決めた
最初 `職の数×4 >= 全体` （＝25%以上）で「多い」と言わせたら、**4体の波で聖職者1体でも警告**が出た。
**2体以上かつ35%以上**（戦士だけ45%）に直した。読みは当たらないと意味がない。

### 次
**A（落とし穴の罠）**。踏んだ冒険者を1階層下へ落とす＝**経路を操作する初めての手**。
①の名簿の土台がそのまま効く。

---

## 2026-08-15　新コンテンツ A：落とし穴（実装完了）

**倒すための罠ではなく、運ぶための罠。** 経路を操作する初めての手。

### ⚠⚠ 先に確かめたこと：いまの「階層が進む仕組み」
実装前に `DungeonFloorManager` を読み直した。分かったのは：

- **階層は同時に1つしか存在しない。** `ActivateFloor` が盤ごと作り直し、要素も防衛体も入れ替える。
- 降下は**パーティ単位の事件**。踏破目的の1人が階段セル（`grid.BossCell`）に乗った瞬間に `Descend()` が走り、
  `WillDescendTo(next)` を満たす全員が一緒に降り、満たさない者は**その場で退場**する。
- 門番が生きている間／魔王が立っている階では降下しない。

つまり **「落とし穴で1人だけ下の階へ移す」は、そのままでは成立しない**（下の階が存在しない）。
ここを踏まえて設計を組み直した。

### 作ったもの

**罠『落とし穴』（`TrapKind.Pit`・研究 `d_trap_pit` 5RP）**
置いたあと、**もう1クリックで行き先を決める**（2段階の配置）。ダメージは飾りで、価値は行き先。

| 行き先 | どうなるか | 使い道 |
|---|---|---|
| **同じ階のマス（縦穴）** | 踏んだ相手をそこへ運ぶ | 入口近くの穴で**殺し部屋へ直送**／階段の手前の穴で**入口へ戻して時間を奪う** |
| **▼下の階（奈落）**<br>研究 `d_trap_abyss` 9RP | その階から消え、**降下が起きたとき下で目を覚ます** | 手に負えない1体を今の戦線から外す |

**⚠ 奈落は削除ボタンではない。** 降りられないまま波が終われば、落とした者は**這い上がって逃げる**
（＝名声↑・略奪装備の持ち逃げ）。**「落とすこと」は「倒すこと」ではない**という線を残した。
しかも落とし穴で踏破目的の者を全部落とすと**誰も階段に乗らない＝降下が起きない**ので、
「邪魔者を消したつもりが全員逃げていた」が成立する。ここが判断になる。

### 実装で効いた設計
- **1階層しか無い**問題は、落ちた者を `SetActive(false)` で**眠らせて控えに置く**形で解いた。
  `Descend()` で起こし、`EndDescent()`（＝波の終わり）で這い上がらせる。
- 着地点は**穴の真下**。各階は別々に生成されるので真下は壁のことが多く、そのときは
  **いちばん近い床**へ寄せる（入口に戻すと「下に落ちた」意味が消える）。
- `FallTo` は `RelocateTo` と違い **`startPos`（退却先＝入口）を書き換えない**。
  書き換えると穴の底が「家」になり、落ちた相手が帰らなくなる。
- 未完成の穴（行き先を決めていない穴）は**踏んでも何も起きない黙った罠**になるので、
  『侵略開始』で自動的に取り消して全額返す。Esc・右クリックでも取り消せる。
- 盤の上では**黒い穴＋行き先までの線**で描く。罠タイルの絵は全種類で共通（緑の棘）なので、
  これが無いと「ただの緑の罠」に見えて運ぶ罠だと分からない。

### 実測（3つの道すべて）
- **縦穴**：(2,2) を踏んだ聖職者が (9,5) へ移動。✅
- **奈落**：踏んだ瞬間に盤から消え、控え1。降下すると**穴の真下に最も近い床 (3,1)**（入口は (3,2)）で復帰。✅
- **這い上がり**：落としたまま波を終えると 名声 105→140／脅威度 1.091→1.122。✅
- セーブ→穴を消す→ロードで **(7,7) と『下の階』の両方が復元**（`FeatureRecord.link` を追加。
  セーブはフィールド名で突き合わせるので追加は安全）。✅
- 胎動で取れるノード **45 → 47**（研究総数 228）。罠は6種→**7種**。

### 罠のデバッグで踏んだこと
`CheckRoomEffectAt` を手で呼んでも発火しない、と2回悩んだ。原因は**冒険者を湧かせた同じ呼び出しの中で
踏ませていた**こと。`Start()` がまだ走っておらず `gridSystem` が null で、冒頭の早期 return に落ちていた。
（コードは正しかった。→ [[tooling-traps]] の「エディタが tick しない」と同じ種類の罠）

### 次
**B（配下の個性）** — 召喚した個体に性格が付き、1体ずつ違う戦い方をする。

---

## 2026-08-15　新コンテンツ B：配下の気性（実装完了）

**盤の上に人格を置く。** 同じ種類・同じLvの配下がこれまで完全に同じ動きをしていたので、
「どの個体を置くか」は Lv と装備を見るだけの作業だった。

### 気性12種（召喚時に1つ決まる）
いちばん大きいのは**誰を狙うか**。ここが1体ずつ違うだけで、盤の意味が変わる。

| 気性 | 戦い方 | 取引 |
|---|---|---|
| 勇猛 | いちばん<b>強い</b>相手へ突っ込む | 攻+10 / HP-8 |
| 臆病 | いちばん<b>弱った</b>相手にとどめ | 速+15 / 攻-8 |
| 執念 | 一度狙った相手を<b>倒すまで変えない</b> | HP+12 / 速-10 |
| 狡猾 | <b>術者</b>を先に潰す | 攻+8 / HP-6 |
| 忠実 | 置いたマスから離れない（leash 1） | HP+15 / 速-15 |
| 奔放 | どこまでも追う（leash 7） | 速+20 / HP-10 |
| 獰猛 | 手数で押す | 間隔-18 / 攻-10 |
| 鈍重 | 遅いが重い | 攻+28 / 間隔+22・速-8 |
| 不屈 | 瀕死で攻撃+35% | 素の攻-12 |
| 狂騒 | 瀕死で速度+50% | HP-10 |
| 静謐 | 毎秒 最大HPの0.6%回復 | 攻-10 |
| 貪婪 | 撃破DP+35% | HP-8 |

⚠ **強さの軸を増やしていない。** 12種の平均は実測で **HP×0.988／攻×1.005／速×1.002／間隔×1.003**、
戦力(HP×攻÷間隔)の幅は **1.31倍**。ここを崩すと「当たりの気性が出るまで召喚し直す」ゲームになる。

### 選べるようにした（研究）
- `m_temper1`**見極め**（6RP）＝召喚が**2択**になる
- `m_temper2`**調教**（11RP）＝既にいる個体を**2択で振り直す**（450DP・いまの気性は出さない）

⚠ **引き直しではなく選択**にしたのが肝。引き直せると「当たりが出るまで回す」になり、
平均1.0で釣り合わせた意味が消える。

### 前2つ（C・A）と噛み合う
先触れで「聖職者が多い」と読めたら**狡猾**を前へ、「重装が主体」なら**鈍重**を、
落とし穴の落下先には**忠実**を置く——**読む→備える→組み替える**が1本に繋がった。
`d_omen2` を取ったのに『見極め』が無い人には、腹心が進言する。

### ⚠ 狙いの実装をやり直した（重要）
最初は「距離に重みを掛けて最小を選ぶ」形で書いた。実測すると
**どの気性も『近い順』と同じ相手を選んでいた**。距離の比（2.0 と 9.0 なら4.5倍）に対して
重みが小さすぎたからで、重みを上げれば今度は盤の端まで歩いて何も殴らなくなる。

→ **「いちばん近い相手＋4.5マス以内」を候補にし、その中で誰を選ぶかを気性で変える**形にした。
狙いは変わるが、遠くの相手を無理に追いはしない。実測（同じ盤面で気性だけ差し替え）：

```
候補  戦士 距離2.0 力11 HP100% ／ 戦士 距離9.0 力122 ／ 魔術師 距離4.0 力17
      戦士 距離5.5 力19 HP15% ／ 盗賊 距離3.0 力49 ／ 盗賊 距離8.0 力19
忠実(近い)   → 戦士 距離2.0
勇猛(強い)   → 盗賊 距離3.0 力49   ← 力122は遠すぎるので正しく無視
臆病(弱った) → 戦士 距離5.5 HP15%
狡猾(術者)   → 魔術師 距離4.0
執念(固執)   → 戦士 距離2.0（隣に別の相手を置いても乗り換えない＝確認済み）
```

### もう1つの衝突：速度を書く場所が2つあった
獣の加速（`AddFrenzy`）と気性『狂騒』が**どちらも `moveSpeed` に代入**していて、
獣＋狂騒の個体では毎フレーム上書き合戦になる。`RecomputeSpeed()` に一本化して
**掛ける場所を1つ**にした。

### 見せ方
- **盤のラベルに気性を出す**（種類名6文字＋Lv＋気性）。置く前に読めないと判断材料にならない。
- **図鑑の個体行に色つきバッジ**。押すと調教の2択が開く（ホバーで効果と狙いが出る）。
- 2択の窓には「狙い： いちばん強い相手」など**行動の1行**を必ず添える。数値よりこれが選ぶ理由になる。

### 実測
- 気性はセーブ/ロードで完全復元（`Individual.temper`）
- 配置した体に正しく乗る（鈍重＝間隔×1.220・速×0.920／狡猾＝狙いCaster、HP倍率比 1.064＝1.00/0.94 と一致）
- 胎動で取れるノード **47 → 49**（研究総数 230）

### 次
**D（迷宮の掘削）** — 通路を掘る/塞ぐ。迷宮ものの核で、単独フェーズ規模。

---

## 2026-08-15　新コンテンツ D：迷宮の掘削（実装完了）

### ⚠⚠ 設計の第一条件：**タイルを1枚ずつ描かせない**
この作品はもともと手動タイル配置だったが、**作業感が強すぎて遊んでいて楽しくなかった**ので
自動生成に切り替えた経緯がある。掘削で同じ失敗を繰り返さないために、次の3つを守った。

1. **クリック数＝判断の数。**
   - 『塞ぐ』＝1クリックで**通路の区間まるごと**（分岐に当たるまで）
   - 『掘る』＝2クリックで**その間をL字にまっすぐ掘り抜く**（線は game が引く）
   プレイヤーは**意図だけ**を言う。
2. **1ターンの回数を絞る**（3回・研究『大工事』で5回）。無制限なら「盤を描き直す作業」に戻る。
3. **結果が数字で返る。** 新しく `道のり`（入口→階段の最短）を上部バーに常設し、
   1操作ごとに「道のり 47 → 49」と出す。

### ⚠ そして**クリックする前に結果が見える**
カーソルを合わせると、**対象のマスが盤の上で色づき**、下の帯に1行出る：

```
塞ぐ 1 マス　-60DP　道のり 47 → 49        （緑＝伸びる）
塞ぐと階段まで辿り着けなくなる            （赤＝できない）
掘る 3 マス　-330DP　道のり 49 → 49       （青＝掘る先）
```

これが無ければ掘削はただの落書きになる。**見えるから、盤を読む遊びになる。**
先読みは**盤を触らずに**計算する（`PathLengthWith` に「壁とみなすマス／床とみなすマス」を渡す）。
実際にタイルを置いて戻すとカーソルを動かすたびに盤がちらつくため。

### 実装でやり直したこと（2つとも実測で分かった）

**① 掘る経路を「壁が最小の道」にしていたのは間違いだった。**
ダイクストラで壁の枚数が最小の道を探していたので、**どこか遠回りで繋がっていると壁0枚の道が
見つかり、掘っても何も起きなかった**（実測：`既に道が通っている` としか出ない）。
掘るのは**新しい近道を作る**行為で、遠回りが在るかどうかとは無関係。
→ **L字にまっすぐ貫く**（横→縦／縦→横の、壁が少ない方）。上限14マス。結果が目で読める。

**② 初期の 10×10 では掘削がほぼ機能しない。**
実測：10×10 の生成マップは床51マスで、**塞いで道のりが伸びるマスは0**、
10マスは「塞ぐと到達不能」（＝拒否）だった。ほぼ1本道で、岩盤もほとんど無い。
**30×30 に広げると、床413／岩盤487、塞いで伸びるマスは 33** に増える。
→ 研究 `d_excavate` の天啓を **「階層をひとつ20マス以上に広げる」** にし、
説明にも「階層が広いほど岩盤が多く、できることが増える」と書いた。
狭いまま渡すと「できない」しか言わない道具になる。

### 遊び方（腹心が最初の1回だけ教える）
**1本道は塞げない**（階段に届かなくなるため拒否される）。
→ **先に『掘る』で迂回路を作り、それから近道を『塞ぐ』。** これで道のりが伸びる。
実測でこの流れが成立することを確認（迂回路を1マス掘る → それまで拒否されていた区間が塞げるようになる）。

### 実測
- 30×30 で `塞ぐ (19,14) → 道のり 47→49`、残り工事 5→4
- `掘る (3,18)→(6,18)` で3マス開通、**道のりは 49→49**（袋小路は近道にならない＝正しい）
- **階を往復しても工事が残る**（`FloorData.map` への書き戻し。忘れると `ActivateFloor` が元の形に戻す）
- 先読みの色つきは12マス、`sortingOrder 40`（タイルマップは -40〜-25 なので上に出る）
- 胎動で取れるノード **49 → 51**

### 次
**E（事件）** — 選択肢つきのランダムイベント。

---

## 2026-08-15　新コンテンツ E：迷宮の異変（実装完了）

### ⚠ 先に確かめたこと：選択肢つきの事件は**既に2つあった**
そのまま3つ目を作れば重複になるので、住み分けを決めてから書いた。

| | いつ出る | 中身 |
|---|---|---|
| `NarrativeSystem` 物語事件 | 状況に応じて・**一度きり** | 世界と方針。報酬は資源 |
| `DiscoverySystem` 発見 | **未踏の地上タイル**を踏んだとき | 歩いた褒美。報酬は小刻み |
| `ManaSurge` 奔流 | 6ターンに1回 | **選択の無い**跳ね |
| **異変（今回）** | **4ターンに1回・準備フェーズ** | **毎回選ぶ／そのターンの戦い方が変わる／自分の行動から生まれる** |

### 芯は「自分が積んだものが跳ね返ってくる」
だから出現条件を**プレイヤーの行動**から引いた。ここで C/A/B/D が一本に繋がる。

| 事件 | 出る条件 |
|---|---|
| 落盤 | **掘削で工事をした**ことがある |
| 穴の底の声 | **落とし穴を1つ完成させた**ことがある |
| ギルドの密偵 | **先触れ**（`d_omen1`）を研究した |
| 罠師の亡霊 | 罠を8基以上置いた |
| 配下の諍い | 配下が4体以上いる |
| 玉座の夢 | **前の波でB2F以降まで攻め込まれた** |
| 迷宮の飢え | DPが900以上ある |

全10種・各2〜3択。⚠ **どの選択肢も一長一短**にした（片方が明らかに得なら、それは選択ではなく作業）。
効果は**そのターン限り**——常時効いているならそれは倍率であって事件ではない。

### 効果の届き先（全部1行ずつ実装）
配下の攻撃/HP → `SpawnDefender` ／ 罠の威力 → `TrapCatalog.PowerMult` ／
罠の不発 → 戦闘開始時 ／ 次の波の人数 → `WaveRoster.RollCount` ／
先触れの深さ → `WaveRoster.ScoutLevel` ／ 工事の回数 → `Excavation.OpsPerTurn` ／
冒険者の足 → `AdventurerAI` ／ 配下1体をベンチ → `SpawnDefendersForActiveFloor` ／
通路がふさがる → `Excavation` の先読みを使って**道が切れない場所だけ**。

### 実装で直した2つ

**① 魔王のHP回復は見返りにならなかった。**
`DemonLord.PlaceAt` が階を組み直すたびに `currentHP = maxHP` にするので、
準備フェーズの魔王は**必ず全快**している。「HPが30%戻る」は何もしないのと同じで、
`HPRatio < 0.7` という出現条件も永久に成立しない。
→ `LordHeal` を丸ごと捨て、条件は **`LastDeepestReached >= 1`（前の波でどこまで来られたか）** に変えた。

**② 罠の不発が3基のはずが2基しか止まらなかった。**
`FindObjectsByType<RoomData>` で拾っていたのが原因。直前の `ImportFeatures` がタイルを敷き直しており、
**古いタイルは破棄予約されているだけでまだ場に居る**ので、死にかけのオブジェクトを止めていた。
→ `grid.GetGridObject(x,y)` で**盤に今出ているもの**だけを引く。実測 5基中3基が正しく不発に。

### 実測
- 配下の倍率： 異変あり HP×1.618／攻×1.689、異変なし HP×1.798／攻×1.407
  → 比 **HP 0.900・攻 1.200**（『焚きつける』の +20%/-10% と一致）
- 「次の波 +4」は**次のターンに移って効き、その次で0に戻る**（＝そのターン限り）
- 罠の不発 5基中3基
- **答えないまま『侵略開始』は押せない**（フェーズは Prepare のまま）
- セーブ→答え待ちを消す→ロード で答え待ちの事件が復元

### 次
C/A/B/D/E がすべて揃った。次は**この5つが入った状態での通しプレイ**で、
胎動の密度が実際に埋まったかを測るのが筋。

---

## 2026-08-16　通しプレイ T1〜T5（C/A/B/D/E を入れた後）

実時間で T1〜T5 を「攻略サイトを書くつもりで」プレイした記録。

### 数字の推移

| | T1 | T2 | T3 | T4 | T5 |
|---|---|---|---|---|---|
| 開始DP | 100 | 1,741 | 2,307 | 2,220 | 2,960 |
| 施設の産出/turn | 0 | 0 | 476 | 1,064 | **1,190** |
| 領域の産出/turn | 0 | 18 | 18 | 18 | 38 |
| 戦闘の長さ | 18秒 | 29秒 | 23秒 | 20秒 | 20秒 |
| 魔王HP | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 |

制限時間は180秒。**5回とも 11〜16% しか使っていない。魔王は一度も削られていない。**

---

## 🔴 いちばん大きい問題：交易所が壊れている

**実測**：交易所1つ 380DP → **DP +168〜266/turn**。**1.4〜2.3ターンで元を取る。**
`DistrictCatalog.TotalYields` のコメントには「⚖️ 換算レート：施設1つが**4-6ターンで元を取る**くらい」とある。
**設計意図の3倍**出ている。

**原因が特定できた。** 隣接ボーナス `adj` が **14〜18**（設計は 3〜6 を想定）。内訳の実測：

```
轟きの採石場 交易所： 川+2 ／ 川+2 ／ 川+2 ／ 穀物+1 ／ 川+2 ／ 川+2 ／ 川+2 ／ 家畜+1 ／ 隣の施設×1
→ adj=14  v=15  DP+210/turn
```

⚠ **川は連なる。** 盤全体では川は15%（1,740タイル中258）しかないのに、
**川沿いに建てれば隣6タイルのうち5〜6が必ず川**になる。だから adj が上限（6×3=18）に張り付く。
`(1 + adj) * 14` なので adj が3倍なら産出も3倍。

**T5時点で施設5つ・DP +1,190/turn。T1の100DPから5ターンでこれ。**

### 直し方の案（推し：②）
1. adj に上限を入れる（例：6で頭打ち）――手軽だが「置く場所を選ぶ」味が消える
2. **同じ種類の隣接は逓減させる**（1つ目+2、2つ目+1、3つ目以降+0.5）
   ―― 川が連なっても伸びなくなる。**種類の多様性を評価する**形になり、Civの隣接の思想に近い。**これを推す**
3. `v * 14` の係数を下げる ―― 対症療法。他の施設まで巻き添え
4. 川の生成密度を下げる ―― 盤の見た目が変わる。副作用が読めない

---

## 🔴 人口・版図（Civの層）が死んでいる

**実測**：19タイル支配して 領域の産出は **DP+18**。施設5つで DP+1,190。**施設が31〜66倍。**

理由：**働けるタイルは人口ぶんだけ**（Civと同じ設計）で、人口1なら1タイル。
- 人口1→2 に必要な食料8、収入+2/turn → **4ターン**
- 人口1→8（上限）まで積むと 224食料 ÷ 2 = **112ターン**

⚠ **胎動が18ターンで終わる設計なのに、人口システムは100ターン規模。尺度が合っていない。**
「19タイル支配しているのにDP+18しか入らない」＝前回のプレイで感じた「地上が産出しない」の正体。

### 直し方の案
- 人口の必要食料 `8 * pop` を緩める（例：`4 + 2*pop`）→ 1→8 が 112 → 40ターン
- 食料の基礎収入を上げる（研究『農法』で+2は取ったが、それでも遅い）
- または**支配タイルそのものに小さな産出**を持たせ、「働けるタイル」は上乗せにする

---

## 🔴 DPとRPの非対称（400倍）

T5時点： **DP +1,200/turn ／ RP +3/turn**（基礎1＋知識ランク2）。
研究は17件修めて「いま取れるものが4件」まで減ったが、**ノードが足りないのではなくRPが足りない**。
（C/Dで胎動のノードを29→51本に増やした効果は出ている。前回の「T11-12でノードが枯れる」は解消。）

⚠ **DPは余って捨て場所を探し、RPは常に足りない。** この非対称が「やることが無い」の正体に変わった。

### 実際に起きたこと
DPの捨て場所を探した結果、**配下を28体召喚した。隊の枠は6。22体は一度も盤に出ない。**
盤に出ないと経験値も入らないので、**22体は永久にLv1のまま**の死蔵資産。

### 直し方の案
- 施設に **RP産出**をもっと持たせる（魔泉/学院を早く解禁する）
- **DP→RPの交換**を作る（「研究に金を積む」＝Civの購入に近い）
- 配下の召喚コストを**所持数で逓増**させる（28体積む意味を消す）

---

## 🟠 T1が窮乏、T2が大盤振る舞い

- T1： DP **100**。罠は150DPなので**1つも買えない**。なのに腹心は「まず罠を1つ置く」と進言する
- T2： DP **1,741**（+1,641）。開始状態で既に満たしている偉業が **5つ同時に達成**された
  （階層を2つ作る／版図を8タイルにする／眷属に真名を与える／発見を2つ得る／属性ポイントを3得る）

⚠ **プレイヤーが何もしていないのに5つ達成される。** 序盤の経済曲線が階段状になっている。

### 直し方
- 初期DPを 250〜300 に（罠1つ＋配下1体は置ける最低ライン）
- 開始状態で満たしている偉業は**達成済み扱いにして報酬を出さない**、または条件を上げる

---

## 🟠 進言の誤報：「魔王の傷が深い」がT1に必ず出る

準備フェーズでは魔王がまだ配置されておらず `HPRatio = 0`。`GuideSystem` の `hp < 0.4f` が常に真になる。
T1で「最下層の守りを厚くする ― 魔王の傷が深い」が出るが、魔王は無傷。
→ 魔王が未配置(`IsPresent == false`)のときはこの進言を出さない。

---

## 🟠 部隊の「置き忘れ」に警告が無い（自分で踏んだ）

T2で隊に5体入れたが、**盤に置いたのは2体**。残り3体は戦闘に参加せず、**exp 0**のまま
（置いた2体は Lv1→Lv3）。気づいたのは自分で数えたから。

⚠ 「隊に入れる」と「盤に置く」が別操作で、置き忘れても**どこにも警告が出ない**。
→ 『侵略開始』の前に「隊員3体が未配置です」と出す（先触れパネルか報告に1行）。

---

## 🟠 役割を散らす価値が隠れている（初心者が必ず損をする）

**実測**：スケルトン2体（近接のみ）＝ 部隊バフ **1.00** → ゾンビ(盾)/ゴースト(妨害)/インプ(支援)を足して **1.45**。
**+45%がタダで手に入る。**

T1で私は「近接を並べればいい」と思ってスケルトン2体を買い、損をした。
⚠ 召喚のカードに書いてあるのは種の倍率だけで、「役割を散らすと部隊バフが上がる」ことは分からない。
→ 召喚ボタンに「いまの隊に足すと 部隊バフ 1.00→1.20」と出す。

⚠ おまけ：研究『部隊枠+1』を取ったら **バフが 1.45→1.30 に下がった**（満員ボーナス+15%を失うため）。
研究したのに弱くなる。説明が要る。

---

## 🟢 良かったところ（C/A/B/D/E は効いている）

- **先触れ→備えが本当に判断になった。** T4で「7体・聖職者4」と読めて『静謐の霧』を張った。
  T5は「8体・聖職者3」でまた霧。**毎ターン相手を見て選ぶ**という手触りが生まれている。
- **異変**がT4で初めて出た（『迷い込んだ獣』3択）。どれも一長一短で、選ぶのが楽しい。
- **気性**が盤のラベルに出るので、同じスケルトンでも個体が違って見える。
- **研究の密度は解消**。T4時点で「取れる研究23件」。前回の「40本しかない」状態からは明確に改善。

---

## 次にやること（優先順）

| # | 中身 | なぜ |
|---|---|---|
| **1** | 隣接ボーナスの**同種逓減**（川+2, +1, +0.5…） | 経済の崩壊の根。ここを直さないと他の調整が全部無意味になる |
| **2** | **DP→RPの交換**と施設のRP産出 | DP1,200 : RP3 の非対称を埋める。「やることが無い」の新しい正体 |
| **3** | 人口の必要食料を緩める（`4+2*pop`） | 版図を広げる意味を作る。100ターン規模を40ターン規模へ |
| **4** | 初期DP 100→250／開始時に満たす偉業の扱い | T1の窮乏とT2の跳ねを均す |
| **5** | 未配置の隊員の警告／役割の効果の可視化／魔王HP進言の誤報 | どれも1行で直る体験の穴 |
| **6** | 召喚コストの逓増 | 28体死蔵を止める |

⚠ **戦闘が20秒**なのは、上の1〜3の結果（DPが余る→防衛が過剰）なので、
**戦闘側の数字を触る前に経済を直す**。前回P2で密度を上げた工夫は生きている（T2は29秒出た）。

---

## 2026-08-16　戦闘が短い理由の検証（前回の結論を訂正）＋ 改善①：隣接ボーナスの同種逓減

### ⚠ 前回「戦闘が20秒なのは経済が壊れて防衛が過剰だから」と書いたが、**半分しか合っていなかった**

ユーザーの指摘（「敵のHP/マナ/耐性とこちらの攻撃の関係で、勝っている側が一方的になるのでは」）を
実測で確かめた。**指摘の方が正しい。**

#### 実測：T1相当の場面（配下は Lv1〜2 のスケルトン3体・装備なし）
```
冒険者4体： 総HP 365   総DPS 64  → 配下を全滅させるのに 19.0秒
配下3体　： 総HP 1,218 総DPS 51  → 冒険者を全滅させるのに  7.2秒
```
**HPが3.3倍違うのに、DPSはほぼ同じ。** 装備も強化も無いT1の配下ですらこの差なので、
**「防衛が過剰だから」では説明がつかない。構造の問題。**

#### 原因1：ダメージ軽減が**どこにも無い**
`ZombieAI.TakeDamageFromAdventurer` も `AdventurerAI.TakeDamage` も `currentHP -= damage;` だけ。
防具グレード(`armorGrade`)は**HP倍率**にしか効いていない。
軽減が無い＝戦闘は純粋な「HP ÷ 相手DPS」の競争になり、**優劣が決まった瞬間に終わる**。

#### 原因2：HPの掛け算の層が非対称（4層 vs 11層）
- 冒険者： 基礎100 × ランク × Lv(+3%/Lv) × 脅威度 × 防具 ＝ **4層** → 82〜103 HP
- 配下　： 基礎120 × 部隊バフ × 遺物 × トーテム × 種族 × 魔王の格 × 進化段 × 個体Lv × 空間 × 政策 × 属性 ＝ **11層** → 371〜458 HP

#### 原因3：術者が5秒で無力化される（ご指摘どおり）
マナ100・回復0.75/秒。
- 魔術師：1撃20マナ・間隔1.0秒 → **5秒で枯れる**。次の1発まで **26.7秒**。以降は素手0.3倍
- 聖職者：広域ヒール30マナ・間隔2.0秒 → **6秒で枯れる**

**戦闘開始5〜6秒で敵の脅威が半減する。** これが「急に一方的になる」の直接の原因。

### 提案（次に相談したい）
1. **ダメージ軽減を導入**し、防具グレードをHP倍率から軽減へ移す（⚠ 上限を必ず入れる。50%など）
2. マナ回復を上げる／最大マナを上げる
3. **宝箱にマナ回復を持たせ、マナが枯れたら踏破目的でも宝箱へ寄り道する**（ご提案）
   → 戦闘が伸びるだけでなく、**誘導宝箱に「敵を回復させてしまう」という裏の意味**が生まれる。
     「マナを枯らす」がプレイヤーの戦術になる
4. 冒険者側のHP倍率の層を増やす（or 配下側の層を減らす）
⚠ どれもカーブに直接触るので、[[curve-measurement-t100]] の手当て（T90で16倍→3倍）を壊さないか要確認。

---

## 改善①：隣接ボーナスの**同種逓減**（実装完了）

同じ理由の n 個目の重みを **1.0 → 0.5 → 0.25 → 0.1…** にした（`DistrictCatalog.SameReasonWeight`）。

**なぜ上限で頭打ちにしなかったか**：それだと「どこに建てても同じ」になり、
Civの隣接の肝である**置く場所を選ぶ**が消える。逓減なら
「川6つ(3.7)より 川4つ+穀物(4.7)の方が強い」＝**種類の多様性**が効く形で残る。

### 実測（同じタイルで前後を比較）
| タイル | adj | DP/turn | 回収ターン |
|---|---|---|---|
| 川4本 陽炎の里 | 9 → **4** | 140 → **70** | 2.7 → **5.4** |
| 川4本 朽ちた街道 | 8 → **3** | 126 → **56** | 3.0 → **6.8** |
| 盤の最良(adj7) | ― | ― | **3.4** |

**設計意図の「4〜6ターンで元を取る」に着地した。**

### 盤全体の分布（健全になった）
交易所： 平均adj **1.7**／最大 **7**／adj0が32%　分布 0:32% 1:22% 2:14% 3:14% 4:8% 5:6% 6:2% 7:1%
→ **良い立地を探す意味がある幅**になった。

### 総産出
同じ盤・交易所5つで **DP+1,190 → DP+280（24%）**。

### ⚠ 残った懸念（次に相談したい）
- **下振れが重い**：adj0 のタイルは回収27ターンで実質無価値。開始領域19タイルに良い立地が
  ほとんど無い世界もある（今回がそう）。**シード運で経済が3倍変わる**。
- 「隣の施設+0.5」も逓減の対象にした。元のコメントは「まとめて置く動機」だったが、
  逓減しないと **n個密集で n(n-1)/2 の二次成長**になり川より危険なので、逓減させた。

---

## 2026-08-16　改善②：戦闘の芯（軽減＋テンポ＋マナ）

ご指摘（「敵のHP/マナ/耐性とこちらの攻撃の関係で、勝っている側が一方的になるのでは」）を
実測で確かめたうえで手当てした。**指摘は正しかった。**

### 何が起きていたか（実測・T1／配下はLv1〜2のスケルトン3体・装備なし）
```
冒険者4体： 総HP   365   総DPS 64 → 配下を全滅させるのに 19.0秒
配下3体  ： 総HP 1,218   総DPS 51 → 冒険者を全滅させるのに  7.2秒
```
強化も装備も無いT1の配下ですらHPが3.3倍。**「経済が壊れて防衛が過剰」では説明がつかない。**

原因は3つ：
1. **ダメージ軽減がどこにも無かった**（両者とも `currentHP -= damage;`／防具はHP倍率にしか効かない）
2. **HPの掛け算の層が非対称**（冒険者4層 vs 配下11層）
3. **術者が5〜6秒でマナ切れ**し、以降は素手0.3倍。次の1発まで26.7秒

### 手当て

**① `CombatMath` を新設し、両陣営に同じ式の軽減を入れた**
```
軽減 = 防御 / (防御 + 42)   上限 50%
防御 = 職/役割の厚み(12/8/5/4) + Lv × 0.45
```
⚠ **装備グレードを入れていない。** 装備は既にHP倍率として効いていて、しかも配下側が高くなりやすい。
入れると軽減が防衛側に偏って**比が動く**。入力をLvと職/役割だけにすることで両陣営が同じ規模になる。

⚠ 最初 上限0.45・K=34 にしたら **Lv35で全職が上限に張り付き、職の差が消えた**（実測）。
上限は「差が残る高さ」に置くこと → 上限0.50・K=42。

**比が動いていないことの確認（同じ個体で軽減あり/なしを比較）**
```
軽減なし： 配下全滅 25.7秒／冒険者全滅 6.0秒　比 4.276
軽減あり： 配下全滅 30.9秒／冒険者全滅 7.1秒　比 4.337   ← +1.4%
比の変化 = (1-冒険者軽減)/(1-配下軽減) = 1.014倍
```
カーブの手当て（T10 0.80／T90 3.00 → [[curve-measurement-t100]]）は保たれている。

**② テンポのノブ（`CombatMath.TempoScale = 1.6`）**
⚠⚠ **軽減だけでは足りない。** 戦闘の長さは「冒険者が全滅するまで」で決まり、
比は「配下の生存時間 ÷ 冒険者の生存時間」。**比を保ったまま長さを3倍にするには両方3倍にするしかなく、
軽減でそれをやると67%が必要で上限を超える**（実測：軽減で伸びるのは1.2倍まで）。
だから「長さ」は専用のノブに分けた。両陣営の攻撃間隔に同じ値を掛けるので比は動かない。

**③ マナの手当て（ご提案）**
- マナ回復 **0.75 → 1.7**／秒
- マナ切れの素手 **0.3 → 0.55** 倍
- **宝箱にマナ回復を持たせた**（通常45／誘導宝箱70）
- **マナが尽きた術者は、踏破目的でも宝箱へ寄り道する**（`NeedsMana()` が真なら核より宝箱が魅力的になる）

→ 宝箱を置くことが「**敵の術者を回復させてしまう**」両刃になり、
  **『マナを枯らす』がプレイヤーの戦術として立ち上がる**。

### 実測（戦闘の長さ・制限180秒）
| 波の規模 | 前 | 後 |
|---|---|---|
| 4体（T1） | 18秒 | **16秒** |
| 8体（T5相当） | 20秒 | **33秒** |
| 13体（T10相当） | ― | **60秒** |

⚠⚠ **少人数のターンでは、戦闘の長さは「湧きの間隔」で決まる。**
T1は `batchSize=3`／`batchGap=8.8秒` なので、3体出す→8.8秒待つ→1体、で**最短9.5秒が湧き待ち**。
戦闘そのものの手当ては効いているが、T1では見えない。
**バランスを測るときは8体以上の波で測ること。**

### 残っている宿題
- HPの層の非対称（冒険者4層 vs 配下11層）はそのまま。比は手当て済みなので急がないが、
  「配下側だけ層が増え続ける」構造自体は残っている
- `TempoScale` は 1.6。もっと伸ばしたければここ1つで調整できる（両陣営に等しく効く）

---

## 2026-08-16　補足：今回入れた「耐性」の正確な中身（属性は入っていない）

### 入れたのは **防御による一律の減衰だけ**
```
軽減 = min(0.50, 防御 ÷ (防御 + 42))
実ダメージ = ダメージ × (1 − 軽減)

冒険者の防御 = 職の厚み(戦士12 / 聖職者8 / 盗賊5 / 魔術師4) + Lv × 0.45
配下の防御   = 役割の厚み(盾12 / 近接8 / 支援5 / 遠隔・妨害4) + Lv × 0.45
```
通るのは **2箇所だけ** ―― `AdventurerAI.TakeDamage` と `ZombieAI.TakeDamageFromAdventurer`。

### 属性相性は**1行も触っていない**
`MagicCatalog.ResistMultVsMinion / ResistMultVsHero`（不死は聖光×1.7・呪詛×0.4／獣は火炎×1.35／
魔族は聖光×1.8／虚無は耐性を無視…）は**前からあるものがそのまま**動いている。

**掛かる場所が違う。**
| | どこで | 何を見る |
|---|---|---|
| 属性相性（既存） | **攻撃側** `ZombieAI:470` / `AdventurerAI.SpellMultVs` | 撃つ属性 × 相手の種族/職 |
| 防御の減衰（今回） | **受け側** `TakeDamage` 系 | 受け手の Lv と 職/役割 |

```
魔法ダメージ = 攻撃力 × 呪文の階級 × 属性相性 × 政策 …
              ↓ 相手へ
受け側で     × (1 − 防御の減衰)
```
**二段。二重計上ではない。**

### ⚠ 穴が2つある（意図的だが、期待とズレやすい）
1. **属性相性は魔法にしか効かない。** 斬撃・バックスタブ・配下の近接には属性の概念が無い。
   **戦闘の大半は物理**なので、属性の出番は術者がいるときだけ。
2. **今回の減衰は属性を見ない。** 「火に強い/氷に弱い」は防御側に反映されていない。
   比を動かさないために入力を職/役割とLvに絞った結果。

### 拡張案（未着手）
⚠ 単純に減衰側へ属性相性を足すと **二重になる**（攻撃側で既に掛かっているため）。
筋が良いのは **ダメージの入口を `CombatMath` に一本化**すること：
1. 攻撃側の `ResistMultVsHero/VsMinion` の掛け算を外す
2. `CombatMath.Apply(damage, defense, element)` にして**受け側で属性も掛ける**
3. 物理に属性を持たせたくなったときも1箇所で済む

動かす呼び出しは4箇所：`ZombieAI:470` `AdventurerAI:572` `DemonLord:374` `WaveRoster:208`。

---

## 📌 2026-08-16 時点の到達点と次にやること（圧縮対策のまとめ）

memory の `session-state-2026-08-16` と同じ内容をここにも残す。

### 到達点
```
C/A/B/D/E（新コンテンツ5本） 完了
  → 通しプレイ T1〜T5 で経済と戦闘の穴を発見
    → 改善① 隣接ボーナスの同種逓減 完了（d3f286c）
    → 改善② 戦闘の芯（軽減・テンポ・マナ） 完了（2c8d193）
      → 次： 改善③ DP:RP の非対称（1,200 : 3）
```

| commit | 中身 |
|---|---|
| `d5fa58a` | C 先触れ＋備え（波の pre-roll／読み4段／備え6種） |
| `1becf78` | A 落とし穴（運ぶ罠。縦穴／奈落） |
| `6014cf1` | B 配下の気性（12種・狙いが変わる） |
| `0202336` | D 迷宮の掘削（塞ぐ／掘る） |
| `63aa8b9` | E 迷宮の異変（10種・自分の行動から生まれる） |
| `c8c3369` | 通しプレイ T1〜T5 の記録 |
| `d3f286c` | 改善① 隣接の同種逓減 |
| `2c8d193` | 改善② 戦闘の芯 |

### 次にやること（優先順）
1. ~~隣接ボーナスの同種逓減~~ 済
2. **DP→RPの交換＋施設のRP産出**（DP+1,200/turn : RP+3/turn ＝ 400倍）
3. **人口の必要食料 `8*pop` → `4+2*pop`**（19タイル支配で領域の産出DP+18／人口1→8に112ターン）
4. 初期DP 100→250／開始時に既に満たしている偉業5つの扱い
5. 1行で直る体験の穴（下記5件）
6. 召喚コストの所持数逓増（28体召喚して22体死蔵した）

### 5 の中身
- T1： DP100で罠150が買えないのに「まず罠を1つ置く」と進言する
- T1： 「魔王の傷が深い」が誤報（準備フェーズは魔王未配置で `HPRatio=0`）
- 隊に入れても盤に置き忘れると警告が無い（置き忘れた個体は exp 0）
- 役割を散らすと部隊バフ 1.00→1.45（+45%）なのにどこにも書いていない
- 研究『部隊枠+1』を取ると満員ボーナスを失ってバフが下がる（1.45→1.30）

### バランスを触るときの制約
- 比（防衛÷攻撃）は手当て済み： T10 0.80 ／ T90 3.00
- 戦闘の長さと比は**別のノブ**（`CombatMath.TempoScale`）
- ⚠ **少人数の波では戦闘の長さは湧きの間隔で決まる。測るときは8体以上の波で。**
  実測 8体 20→33秒／13体 60秒（制限180秒）

---

## 2026-08-16　新コンテンツ G＋L：因縁（名のある冒険者）と囚牢（`32f6d43`）

### なぜ既存改良ではなく新規制作にしたか
改善③は「DP→RPの交換レートを1本引く」だったが、**それは数字の付け替えであって遊びではない**。
同じ穴（DP+1,200 : RP+3＝400倍）を、**倒し方そのものを経済にする**ことで塞ぐ。
新規案5本（F縦の迷宮／G名のある冒険者／L捕虜と改宗／地上の民／序列と派閥）を面白さで比べ、
**G＋Lを1本**として先に作った（土台がC・A・W・KinRosterに既にあり、カーブの制約にも当たらないため）。

### G 因縁（`Nemesis.cs`）
- **名が生まれるのは「半分以上削ってなお生還した者」と「奈落を這い上がった者」だけ。**
  逃げた全員に名を付けるとただの騒音になり、名前の価値が消える。条件は `Nemesis` 側が持つ
  （呼ぶ側の `AdventurerAI.GrantReturnReward` に条件を書くと、規則が2箇所に散る）
- 逃走のたびに HP×0.15／攻×0.12／ランク＋／Lv＋ で強くなるが **上限あり（`GrowthCap=6`）**。
  伸びるのは**プレイヤーが取り逃がしたぶんだけ**＝勝手に難しくなるのではなく「自分が育てた敵」
- 名簿には**末尾から差し替える**（人数は増やさない・1波に最大3体）。
  先頭に置くと開幕でいきなり出て波の山が消える。人数を増やすとカーブの上に軸が1本乗る
- 討伐すると DP＋素材＋**脅威度が下がる**（「あそこは還れない」に噂が変わる）

⚠ 踏んだ罠：**名前がかぶった**（2人生んで2人とも「石橋の…」）。家名・名の両方で既出を避けて
seed を引き直すようにした。名がかぶると名前が記号に戻ってしまう。

### L 囚牢（`Prison.cs`）
- 方針を**生け捕り**にすると、倒れた冒険者が牢へ。⚠ **捕らえると撃破DPも素材も入らない**
  （＝天秤の支点。「今日はDPが要るのか、知識が要るのか」を毎波選ばせる）
- 処遇4つ：**尋問**（RP）／**調伏**（転向者として配下に）／**供物**（魔王が喰らう）／**解放**（身代金・必ず恨んで戻る）
- 維持費を払えないと**一番手強い者から脱走**し、名を得て戻る（放置のコストを金でなく敵で払わせる）
- 転向者4種は `UniqueCatalog` の**末尾に weight 0 で追加**。ガチャには出ないが、
  Lv・装備・図鑑・盤の絵は幹にそのまま乗る（別カタログを作ると配線を全部書き直しになる）

### ⚠ バランスで3回踏み直したこと（全部実測で発覚）
| 症状 | 原因 | 手当て |
|---|---|---|
| 捕虜1人が5ターンで **60RP**（基礎+3/turnの20ターンぶん） | 「名あり×2」「搾る×2」の倍率を重ねていた | 倍率を ×1.4／×1.5 に。基礎も圧縮 |
| 枠7×毎ターンで **+175RP/周期** | **枠が「同時に絞れる人数」まで増やしていた** | **尋問は1ターン1人**（深牢で2人）。枠＝誰を残すかの幅／尋問回数＝今日は誰から聞くかの選択、と役割を分けた |
| 調伏まで **24ターン**（胎動18ターンより長い＝机上の存在） | 反抗心の初期値が高く減りが遅い | 初期値を圧縮・毎ターンの摩耗を 5→8＋体格×2 に。**6〜13ターン**へ |

⚠ もう1つ：**4回目の尋問で捕虜が黙って死んでいた**（ボタンの見た目は3回目までと同じ）。
→ `WillBreak()` を足し、**押す前に「搾る」に変わり実りが1.5倍になる**形にした。事故ではなく選択にする。

### 実測（最大効率で回したとき）
```
石牢(枠4/尋問1)  20ターンで 尋問12回 = 88RP  → +4.4 RP/turn（維持費の累計 2,220DP）
深牢(枠7/尋問2)  20ターンで 尋問21回 = 154RP → +7.7 RP/turn（維持費の累計 3,256DP）
基礎の研究点は実測 +3/turn
```
コンパイルエラー0／先触れ・因縁の両パネルをスクリーンショットで確認（見切れなし）。

---

## 2026-08-16　F-1：縦の迷宮の土台（`d42cbb1`）

ユーザー判断で **F-フル（階層が本当に同時に生きる）** を採用。その前提工事。**挙動は変えていない。**

### なぜ先にこれが要るのか（実測）
この作品は長く**「盤は1枚」**で書かれてきた。各システムは盤を
`FindFirstObjectByType<DungeonGridSystem>()` で掴んでいる ―― **20箇所以上**（盤のAPI呼び出しは **126**）。
1枚のうちは正しく動くが、**2枚目を置いた瞬間に「どちらを掴むか不定」になる**。
掘削・落とし穴・気性・異変は全部この盤を触るので、**動作確認済みのものが最初に壊れる**。

### やったこと
- `DungeonGridSystem` に **登録簿**（`Active` / `Boards` / `Of(floor)`）を追加
- `FindXObjectByType<DungeonGridSystem>` を **全廃**（12ファイル・29箇所 → `Active`）
- `GridInputHandler` の `[SerializeField]` 参照を廃止。
  シーンで割り当てた1枚を握り続けると「**B2Fを見ているのにB1Fに置ける**」ことになる
- `floorIndex` と `FloorOrigin`（`floorIndex × FloorSpacing(200)`）を追加。
  **`GridToWorld` / `WorldToGrid` だけがオフセットを知る**形にした。
  AI・配置・カメラは全部ここを通ってワールド座標を出しているので、ここに足すだけで全体に伝わる
  → 階層は**ワールド座標をずらして同時に存在**できる
  ⚠ 同じ座標に重ねてはいけない（当たり判定も `WorldToGrid` も階をまたいで混ざる）

⚠ 踏んだ罠：`Active` を自動プロパティにしたら、**再コンパイルのドメインリロードで静的が飛び**、
`Awake` は再実行されないので null のままになった。→ 空なら数え直す仕掛けを入れた。
**そのとき `FindFirstObjectByType` をそのまま返さず、いちばん浅い階を選ぶ**（不定を避けるため）。

### 検証
コンパイルエラー **0**／B1F の座標は変化なし（origin 0・`WorldToGrid(GridToWorld(3,4))=(3,4)`）／
**波を1周実行してエラー0**（湧き→戦闘→清算→地上フェーズ、DP 200→662・名声70）。

---

## 2026-08-16　F-2：階層が同時に生きるようになった（`49302e8`）

**縦の迷宮の本体。** 盤・配置・防衛体を階層ごとの実体にし、降下しても上の階が消えないようにした。

### 実測（3階・turn30・名簿20体）
```
B1F : 冒険者 0  配下 6
B2F : 冒険者10  配下 6      ← 3階が同時に戦っている
B3F : 冒険者 8  配下 6
階と座標の食い違い = 0 ／ 戦闘中の階切替も動作
```
旧仕様ではこれは**原理的に不可能**だった（降りると上の階は盤ごと消えていた）。

### 盤（`DungeonFloorManager`）
- `GenerateAllFloors` で B1F の `GridManager` を**複製**して階層ぶん用意（`EnsureBoards`）
- `ActivateFloor` は**盤を作り直さない**。`Active` の付け替えとカメラ移動だけ
- ⚠ **複製の罠**：複製元の階の座標に生えたタイルとガイドが子として付いてくる。
  しかも `Awake` は複製の瞬間に走る＝`SetFloorIndex` より先なので、ガイドは B1F の原点に作られる。
  → `ClearAllTilesAndGuides()` で子を全部消してから作り直す

### 配置（`DungeonFeatureManager`）
- `features` を**階層ごとの辞書**に。⚠ このファイルに既にある `squadByFloor` / `SquadOf(floor)` と
  **同じ流儀に揃えた**ので、30箇所ある `features` の呼び出しは**1行も変えずに**階層対応になった
- 防衛体も階層ごと。⚠ `DespawnDefenders` は**降下では呼ばない**（波の終わりだけ）
- `SpawnDefendersForAllFloors`：侵略開始で全階の守りを一度に立てる
- ⚠ `SpawnDefender` の呼び出しが多い（10箇所以上）ので、引数を増やさず
  **生成中だけ `spawnFloor` を立てる**形にした

### 冒険者（`AdventurerAI` / `Descend`）
- ⚠⚠ **降りられない者を退場させるのをやめた。** その階に残って戦い続ける。
  ＝「1階を捨て階にして消耗させ、下で仕留める」が成立する中心
- `BindFloor` で階を注入。⚠ **`RelocateTo` より前**に呼ぶこと
  （後だと座標だけ下の階へ飛んで、経路は前の階の盤で引き続ける）
- 冒険者は必ず **B1F の入口**から湧く。⚠ `Active` を使うと、戦闘中に B2F を見ているあいだ
  **下の階の入口に直接湧く**
- 門番と階段の判定を階で絞った（`GetLivingGuardianOnFloor`）。
  絞らないと**どこか1階でも門番が生きていれば全階の突破が止まる**

### ⚠ 途中で見つかった「既存」バグ2件
守りを置かずに波を回すと出ないので、F-1の検証（エラー0）をすり抜けていた。
1. **`CharacterVisual.InitDungeonTale` がモードフラグを立てていなかった。**
   `flip`/`bob` しか作らないのに `Update` が手続きリグ側の分岐へ落ち、`torso` が null で
   **毎フレーム NullReferenceException**。→ `useDT` を追加
2. **`ZombieAI` で配下が倒れたとき `hpTextMesh` の null ガードが無かった**
   （スポナー湧き／蘇生体は持っていないことがある）

さらにF-2で作り込む前からあった階の取り違え2件も潰した：
- `RaiseUndead` が**表示中の階**に蘇生していた（B2Fで倒れた不死がB3Fに湧く）
- `TickSpawners` が表示中の階しか回していなかった（＝見ている間しか働かない設備）

### 検証
コンパイルエラー **0**／実行時エラー **0**／B2Fの描画をスクリーンショットで確認
（自前の宝箱・下り階段・配置マーカーが正しくその階の座標に出ている）。

⚠ **道具の罠（新規）**：プレイモード中にスクリプトを編集すると**ドメインリロードで
`DungeonFloorManager.floors` が空になる**（`FloorData` は非シリアライズのplain class）。
`BuiltFloorCount=0` になり `SwitchTo` が黙って何もしなくなる。
**編集したら必ず一度プレイモードを抜けて入り直してから測ること。**

---

## 2026-08-16　F-4：階ごとの状況をタブに出し、経験を「戦った階」だけに配る（`4f2050f`）

縦の迷宮は**盤を一度に1つしか見られない**ので、タブが他の階を知る唯一の窓になる。

### フロアタブ（戦闘中）
`B2F 10/9` ＝ **敵/味方** の数を1行で。敵が居る階は枠と文字が赤、居ない階は沈める。
- ⚠ **1行に収める**（タブは高さ26px。改行すると見切れる）。幅は 70→96px に広げた
- 数字は `DungeonFloorManager` が **4回/秒**で数え直したもの。
  ⚠ **UIと経験の判定が同じ数字を見る**（別々に数えると必ず食い違う）

### ⚠⚠ 経験の前提が F-2 で壊れていたのを直した（いちばん大事な修正）
| | 旧 | F-2直後（バグ） | F-4（修正後） |
|---|---|---|---|
| 配るタイミング | 降りた先の守りを立てる瞬間 | 同じ（＝開幕に全階） | **波の終わり** |
| 満額の条件 | 立った＝戦った | **立っただけで満額** | **その階に冒険者が入ったか** |

F-2で「全階の守りを開幕に立てる」ようにしたので、旧来の場所のままだと
**冒険者が一度も来ないB3Fの配下まで満額の実戦経験を貰う**（置くだけでタダ）状態だった。

**実測（B1Fだけ戦った波）**
```
B1F +80（実戦）   B2F +75（待機）   ← 修正前は B2F/B3F も満額だった
参考： 実戦/待機 = B1F 80/40  B2F 150/75  B3F 220/110
```

### 波の終わりの報告
`ReportBreaches`：どの階で戦いが起きたかを残す。
⚠ 縦の迷宮では**同時に複数階が破られる**ので、「最深部まで何F」だけでは何が起きたか読めない。

### 検証
コンパイルエラー **0**／実行時エラー **0**／タブをスクリーンショットで確認
（`B1F 0/7` 灰 ／ `B2F 10/9` 赤 ／ `B3F魔 1/7`・見切れなし）。

---

## 2026-08-16　F-2/F-4 の副作用つぶし（ユーザー報告5件・`e9fc677` `563a999` `b02e697`）

通しで触ってもらって出た報告。**5件中4件が「盤が1枚である前提」で書かれた既存コードの取りこぼし**。

### 共通の教訓
F-2で「盤を階層ぶんに増やし、ActivateFloor は作り直さなくなった」。
このとき**「1枚である前提」に依存していた箇所**を洗い切れていなかった。典型的な形は3つ：
1. **`CurrentFloorIndex`（表示中の階）を"その対象の階"のつもりで渡している**
2. **`ActivateFloor` が盤を組み直す前提で、組み直しを頼んでいる**
3. **1つしか無い前提のグローバル状態**（魔王の `present`、`current` の二役）

### ① 1階も2階も同じ形に見える（`e9fc677`）
`DungeonGridSystem.RepaintTilemap` が **表示中の階**を `Paint` に渡していた。
階層ぶんの盤を順に組むと**全部が同じ帯に描かれ、最後の階の形が全階に見える**。
→ **その盤自身の `floorIndex`** を渡す。`DungeonTilemapView` はタイルマップを1枚しか持たないので、
階ごとに `FloorSpacing` ぶんセルをずらし、消すのも**その階の帯だけ**にした。
⚠ 消す範囲は常に最大(50)＋壁の余白（今の size で消すと拡張前の外周が残る）。

⚠ **私は前回この兆候を見て流していた**：B2Fのスクショで床が真っ暗だったのを
「テーマの色だろう」と判断した。**説明のつかない見た目は確かめる。**

### ② 階層の拡張(10→20)が効かない／階層追加で盤が増えない（`e9fc677`）
`TryExpandFloor` が `ActivateFloor` を呼んでいたが、F-2でそれは「見る階を変えるだけ」になった。
→ `BuildBoard(i)` を呼ぶ。`TryAddFloor` も `EnsureBoards()` が要る
（無いと `DungeonGridSystem.Of(新しい階)` が null＝降りた先が空）。

### ③ 魔王が表示されない／階段の前で立ち尽くす（`563a999`）※同根
`DemonLord.present` が「**表示中の階が魔王の階か**」という単一フラグだった。F-2以降これで3つ壊れる：
・別の階を見ている間 魔王がどこにも居ない
・`IsPresent` を見ている降下の判定が**全階で止まる**（階段の前で立ち尽くす）
・別の階を見ている間 **魔王が無敵**

→ `present` の意味を「**盤に置かれているか**」に変え、どの階かは `MyFloor` で持つ。
降下の判定は `IsLordFloor(current)`。`AdventurerAI` の門番/核の判定も自分の階で見る（`LordIsHere`）。

⚠ さらに、F-2で `ActivateFloor` に `RefreshLordPresence()` を足したせいで
**階のタブを押すたび魔王が全回復**していた（`PlaceAt` がHPを満タンに戻す）。
→ HPを保つ `MoveTo` を新設。**「置く」と「移す」を同じ関数にしない。**

### ④ 階段が入口から一番遠くない（特に拡張後）（`563a999`）
これはF-2と無関係の**元からの弱点**。`DecideEntranceAndBoss` が**部屋の中心どうしの直線距離**で
選んでいたので、盤が広いほど「壁を挟んで近いだけの部屋」が当たっていた。
```
修正前（20×20）  入口(6,7)→階段(16,4)  道のり13 / 最大28 ＝ 46%
修正後           生成直後 81〜91% ／ 拡張後 75〜80%
```
→ 実際に掘れた通路を**BFSで測った道のり**で選ぶ。到達できない部屋は選ばない。
（100%にならないのは階段を**部屋の中心**に置く設計のため。意図どおり）

### ⑤ 1階のタブが2階に戻される／波が終わらない（`b02e697`）※これが一番深い
**`current` が「表示している階」と「侵攻の最前線」の二役**を兼ねていた。
- B1Fタブを押す→`current=0`→降下ロジックが「最前線はB1F」と解釈し**その場でまた降ろす**
  （「2階層に侵入」が再表示され、押しても押しても戻される）
- B2Fを見ている間は**B1Fの降下判定が一度も走らない**→階段の前で固まり波が終わらない

→ `current` は**表示専用**。降下は `TryDescendFrom(f)` を**全階について毎tick**判定する。
`Descend(from)` は `current` を書き換えず、**「いま降りた階を見ていた」ときだけ**視点を連れていく。
奈落の控えにも**どの階から落ちたか**を持たせた（無いと1つ浅い階に湧く）。

⚠⚠ **別原因で同じ症状になる穴がもう1つあった**
F-2で「降下必要Lvに届かない者はその階に残る」ようにしたが、
**踏破目的の彼らには階段以外の目的が無い**ので階段の上で永久に立ち尽くす。
その階の守りを倒し切っていると誰にも倒されず、波が永遠に終わらない。
→ 手が届かないと悟った者は**諦めて引き返す**（歩いて帰り感情DPを清算＝取り逃がし扱い）。
⚠ **階層ボスを倒さないと次へ進めない設計は維持**（`GetLivingGuardianOnFloor`）。

### 検証
- 階段：生成直後 81〜91%／拡張後 75〜80%（修正前46%）
- 拡張：B2F 10→20（床50→153）、非表示の階でも表示中の階でも成功・他階は不変
- 追加：3層→4層、B4Fの盤が作られ魔王が移動
- 魔王：4層追加後もB4Fに居続け、どの階を表示してもHPが変わらない
- タブ：B1Fを3回連続で押してもB1Fのまま
- 波：B2Fを見たまま戦っても取り残し0、**制限時間を65秒残して自然終了**
- コンパイルエラー0／ゲーム側の実行時エラー0

⚠ 検証中に一度ゲームが完全停止したが、`Time.timeScale=0` の正体は
**魔王討伐によるゲームオーバー**だった（`DemonLord.Die`）。バグではなく、
③の修正で魔王が正しく攻撃対象になった結果。**止まった＝バグ、と決めつけない。**

---

## ⭐ 次にやること（ここから再開）

**F-フルは F-1／F-2／F-4 まで完了**（F-3はF-2に含めて実施済み）。縦の迷宮は動いている。

### ✅ 「盤が1枚である前提」の洗い直し ― 実施した（`b080a6f`）

**きっかけの報告：2階以降で床なのに『壁には配置できません』と言われて置けない。**
原因は `DungeonFeatureManager.grid` を **`Start` で1回だけ掴んでキャッシュ**していたこと。
**ずっとB1Fの盤を握り続ける**ので、B2F以降の配置可否がB1Fの地形で判定されていた
（ユーザーの推測「1階層の座標で判断してる？」がそのまま当たり）。
→ `grid` をプロパティ（表示中の階の盤）に。キャッシュ廃止。

洗い直して、**型が2つ増えた**（当初の3つでは足りなかった）：

#### 型④【新】セル座標が階をまたいで衝突する
`(5,5)` は**全ての階に存在する**。階を見ない座標比較は別の階と誤マッチする。
- `ZombieAI.IsDeadZombieAt` … B1Fの屍のせいで**B3Fの同じマスに置けなくなる** → 階を引数に
- `AdventurerAI` 聖職者の範囲回復 … **B1Fの聖職者がB3Fの仲間を回復していた** → 階で絞る

#### 型⑤【新】「全体に効く」効果が全階に効く
⚠ **距離で絞る処理は安全**（階が `FloorSpacing=200` 離れているので自然に除外される）。
接敵・範囲攻撃・配下スキル（咆哮/治癒/自爆…）は全部 `Vector3.Distance` なので触らなくてよい。
**危ないのは距離を見ない「全体」系。** DPあたりの効果が階数ぶん跳ね上がっていた。

| 対象 | 直し方 |
|---|---|
| 号令4種（治癒/落石/魔王の一撃/恐慌） | `DungeonGridSystem.CommandFloor`（＝**見ている階**）に限定 |
| 権能（Splash/Freeze/Poison/HealAll/CountDefenders） | **魔王が立っている階**に限定 |
| 異変『罠が不発』 | 逆に**全階から集める**（`Active` だと必ずB1Fで走るため、「迷宮の異変」なのに**B1Fの罠しか止まっていなかった**） |

⚠ **号令と権能はバランスにも効く**（これまで階数ぶん強すぎた）。
次のバランス測定は**この変更を前提**にすること。

#### 型③の残り／型②の後始末
- `ExcavationPreview.grid` も同じキャッシュ（掘削プレビューが別の階の座標で描かれる）。
  ⚠ プレビューの署名に**階を混ぜる**（同じセルだと階を変えても描き直さない）
- `DemonLord.grid` は `PlaceAt`/`MoveTo` が入れるので、`Start` の保険を未配置時のみに
- 「階層は同時に1つしか存在しない」と書かれた**古い注意書き3箇所**を実態に合わせた
  （`DungeonFeatureManager` `DungeonFloorManager` `Excavation`）。読む人を誤らせるため

#### 洗い方（次に盤を増やしたときも同じ手順で）
```
型① grep "CurrentFloorIndex"        … 表示中の階を「対象の階」のつもりで渡していないか
型② grep "ActivateFloor"            … 盤の組み直しを期待していないか
型③ grep "DungeonGridSystem.Active" … 掴んでキャッシュしていないか
型④ grep で座標比較                 … 階を見ずに Vector2Int を比べていないか
型⑤ grep "ObjectsByType<AdventurerAI>|<ZombieAI>" … 距離で絞っていない走査は階で絞る
```

**検証**：3階すべてで隊員5体・罠3基を配置成功（失敗0）／3階同時の波を65秒走らせて
階と座標の食い違い0／コンパイル・実行時ともエラー0。

### ⚠ 積み残しがあった（`9687e5d`）― 前回は目についたものを直しただけだった
「洗い直した」と書いたが、**型④⑤の系統的な grep をしていなかった**。やり直して3件発見：

**① 落とし穴の「行き先待ち」が階を持っていない（型④）**
`pendingPit` はセルだけを覚えていた。B1Fで穴を置いてから階を切り替えると
**B2Fの同じ座標**を見に行く。`CancelPendingPit` に至っては**別の階の要素を消して返金**していた
（`StartBattlePhase` も呼ぶ経路なので、侵略開始で他階の配置が消える可能性があった）。
→ `pendingPitFloor` ＋ `RemoveFeature(floor, cell)`。
→ **階を切り替えたら未完了の設置は畳む**（`ActivateFloor`）。続きをする前提の状態だから。

**② 掘削の「掘りかけ」も同じ（型④）**
`Excavation.pendingDig` もセルだけ。B1Fで始点を置いて階を移ると B2F の盤で経路を引く。

**③ 異変『通路がふさがる』の効く階が"開いていたタブ"で決まっていた（型⑤の裏）**
`SealOneCorridor` が `Active` を使っており、**結果がUIの状態で変わっていた**。
→ 階を無作為に選び、`Excavation.UseGrid/EndUseGrid` でその階の盤で計算させる。
→ `WriteBackMap(floor)` を追加（`WriteBackCurrentMap` は表示中の階しか書き戻せない）。

⚠ **教訓：「洗った」と言う前に、型ごとに grep を実際に流す。**
目視で拾えるのは自分が既に知っているものだけ。

### ⚠⚠ さらに積み残していた（`f00d2bc`）― grepでは出ない種類
grepは流し切ったが、**F-2が触ったセーブ/ロードを一度も動かしていなかった**。
実際に往復させたら重いものが2件出た。**grepで見つかるのは"書き方"だけで、"通り道"は動かさないと出ない。**

**① ロードすると全階の配置が表示中の1階に積まれる**
`AddFeature` が `features`（＝表示中の階の辞書）に書いていた。
`ImportFeatures(floor, ...)` は `spawnFloor` を立てているのに、**書き込み先がそれを見ていない**。
→ `FeaturesOf(SpawnFloorIndex)[cell] = f`。
実測：保存前 B1F=1/B2F=0/B3F=3 → ロード後 **B1F=3/B3F=0**。

**② 魔王を置かない階の盤を組むと魔王が消える（型③）**
`BuildFromMap` に `else DemonLord.Instance.SetPresent(false);` が残っていた。
盤が1枚の頃は「最下層以外を構築＝魔王不在」で正しかったが、F-2で盤を階層ぶん組むので
**魔王が居ない階を組むたびに消える**。
実測：**B2Fを拡張しただけで魔王が居なくなり、ロード後も不在のまま**。
→ 削除。在・不在は `RefreshLordPresence` に一本化。

**③ 魔王の無敵判定が階を見ていなかった（型⑤の取りこぼし）**
`shielded` と `TakeDamage` が `GetLivingGuardian()`（シーン全体）を見ていたため、
**B1Fにボスを置くだけで最下層の魔王が永久に無敵**だった。
→ `GetLivingGuardianOnFloor(MyFloor)`。階を絞らない版の呼び出しは0件になった。

**検証**：B2F拡張直後に魔王 在=True／セーブ→1層に作り直し→ロードで
`B1F 10/1・B2F 20/0・B3F 10/3・魔王B3F在` が完全一致。

⚠ **教訓その2：grepの次は"通り道を実際に通す"。**
セーブ/ロード・拡張・階層追加のような**節目の操作**は、grepでは異常が見えない。

### ⚠ 安全と確認したもの（もう一度洗わなくてよい）
- 配下のスキル（咆哮/治癒/群れ/威圧/自爆）＝ すべて `Vector3.Distance` で絞っている
- `ZombieData` ＝ 未使用コード
- `DungeonGenerator` ＝ FloorManager があれば `GenerateAllFloors` に委譲している
- `GameUIManager` の `CurrentFloorIndex` 参照 ＝ すべて**表示用のラベル**（正しい用法）
- `AdventurerAI` の自分の座標比較（目的地・入口・階段）＝ 同一個体なので階は一致
- `CheckRoomEffectAt`（罠・部屋の発火）＝ 自分の階の盤を使っている
- `stairsMarker` ＝ 1個だが表示中の階に追従する（同時に2階は見られないので問題なし）
- `WardSystem` / `MinionTemperament` ＝ 盤を参照しない

### ⚠ 最優先：バランスの測り直し（F の宿題）
3階が同時に戦うようになったので、**1波あたりの総戦闘量が増えている**。
- 実測：3階＋各階に守り6体で **1波が48秒を超えた**（単層は約16秒）
- 測る対象は [[combat-math]] と同じ **比（防衛÷攻撃）と長さ**。⚠ **8体以上の波で測る**
- ⚠ **階数で強さを増やさない**という縛りは守れている（階数は時間と手数だけを増やす設計）。
  ただし「同時に戦う階が増える＝守りの総数が増える」ので、**実質の防衛力は上がっている**。
  ここを測らずに強化を足さないこと → [[difficulty-curve-orders]]
- 深い階の魔素濃度が強い（B3F 実戦220／待機110＋追いつき補正×2.0）。
  **待機でもLvが上がる**ので、階を増やすほど育成が速い。カーブと合わせて見ること

### そのほか（軽い・G＋L由来）
- 腹心の報告に牢の状況（`Prison.Summary`）を出す
- 戦績に `RunStats.NemesisSlain` `Captured` `Converted` を出す（数えているが未表示）
- `GameSetup` の初期階層数：いまタイトルの世界設定で1層になることがある。
  縦の迷宮を作ったので、**既定を2〜3層**にしないと新機能に触れないまま終わる

~~**F-2 階層ぶんの盤を作る**~~ ✅ 完了（`49302e8`）
~~**F-3 アクターを階に紐づける**~~ ✅ F-2に含めて実施（`BindFloor`／降下で退場させない／守りを撤収しない）

**F-4　見せ方と終了条件（残り・ここから）**
- **戦闘中の階切替のUI**。仕組みは動く（`SwitchTo` は戦闘中も通る＝実測済み）が、
  **フロアタブが準備フェーズ用のまま**。戦闘中に押せる導線と、各階の状況
  （冒険者◯体／守り◯体）を出すこと。`fm.LivingDefenderCount(floor)` は用意してある
- **ウェーブ終了条件**：`DungeonTurnManager.CheckWaveEndCondition` はシーン全体の
  `AdventurerAI` を数えているので**結果としては正しく動く**。ただし
  「どの階で終わったか」「どの階が破られたか」が取れないので、階ごとに数えて報告に出す
- **`deepestReached` と待機経験の見直し**：`GrantGarrisonExp` は「到達しなかった階」に
  1/4経験を配る作りだが、いまは**全階が戦っている**ので前提が崩れている。
  実戦した階＝冒険者が来た階、で数え直す
- ⚠ **階数で強さを増やさない**という縛りを守ること（階数は「時間と手数」だけを増やす）。
  ここを破ると [[curve-measurement-t100]] の手当て（T10 0.80／T90 3.00）がやり直しになる
- ⚠ **バランスは未測定**。3階同時になったぶん、1波あたりの総戦闘量が増えている。
  比（防衛÷攻撃）と戦闘の長さを測り直すこと（8体以上の波で）

### そのほか（G＋L由来・軽い）
- 腹心の報告に牢の状況（`Prison.Summary`）を出す
- 実績／戦績に `RunStats.NemesisSlain` `Captured` `Converted` を出す（数えてはいるが未表示）

## 通しプレイ T1-T30 の全記録

| T | 戦闘s | 波 | 撃破計 | 到達 | DP | RP | 名声 | 時代 | 層 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 15.5 | 4体Lv1 | 4 | ○ | 255 | 1 | 0 | 0 | 1 |
| 2 | 28.8 | 5体Lv5 | 9 | ○× | 453 | 14 | 54 | 29 | 2 |
| 3 | 29.3 | 6体Lv6 | 15 | ○× | 409 | 23 | 58 | 52 | 2 |
| 4 | 29.3 | 7体Lv6 | 22 | ○× | 662 | 0 | 62 | 75 | 3 |
| 5 | 30.4 | 8体Lv7 | 30 | ○× | 694 | 3 | 66 | 92 | 3 |
| 6 | 39.8 | 9体Lv8 | 39 | ○×× | 748 | 0 | 75 | 117 | 3 |
| 7 | 31.6 | 10体Lv9 | 49 | ○×× | 1035 | 1 | 84 | 122 | 3 |
| 8 | 27.2 | 11体Lv10 | 60 | ○×× | 1080 | 2 | 93 | 127 | 3 |
| 9 | 32.1 | 12体Lv11 | 72 | ○×× | 1044 | 0 | 102 | 146 | 3 |
| 10 | 39.1 | 13体Lv12 | 85 | ○×× | 1404 | 4 | 115 | 156 | 3 |
| 11 | 30.1 | 14体Lv14 | 99 | ○×× | 1558 | 8 | 128 | 161 | 3 |
| 12 | 27.0 | 15体Lv14 | 114 | ○×× | 1327 | 12 | 141 | 166 | 3 |
| 13 | 33.2 | 16体Lv14 | 130 | ○×× | 1842 | 16 | 159 | 171 | 3 |
| 14 | 30.7 | 17体Lv17 | 147 | ○×× | 2457 | 0 | 172 | 182 | 4 |
| 15 | **57.2** | 22体Lv18 | 169 | ○××× | 2408 | 0 | 189 | 187 | 4 |
| 16 | 43.2 | 19体Lv18 | 188 | ○××× | 2225 | 2 | 206 | 206 | 4 |
| 17 | 46.1 | 20体Lv19 | 209 | ○××× | 2267 | 2 | 224 | **210/210** | 4 |
| 18 | 36.5 | 20体Lv22 | 231 | ○××× | 2711 | 2 | 246 | **210/210** | 4 |
| 19 | 42.6 | 20体Lv21 | 251 | ○××× | 1974 | 2 | 268 | **210/210** | 4 |
| 20 | 65.6 | 20体Lv22A | 271 | **○○××** | 2841 | 5 | 290 | 0（伸長へ） | 5 |
| 21 | 51.0 | 20体Lv26S | 284 | ○×××× | 8109 | 0 | 584 | 93 | 5 |
| 22 | 49.5 | 20体Lv26S | 304 | ○○××× | 4502 | 5 | 597 | 126 | 5 |
| 23 | 58.8 | 20体Lv29S | 324 | ○×××× | 3142 | 6 | 610 | 131 | 5 |
| 24 | 54.9 | 20体Lv28S | 344 | ○×××× | 2926 | 1 | 623 | 142 | 5 |
| 25 | 59.6 | 20体Lv29S | 367 | ○×××× | 3435 | 6 | 636 | 147 | 5 |
| 26 | 49.9 | 20体Lv29S | 388 | ○○××× | 3308 | 1 | 649 | 152 | 5 |
| 27 | 42.5 | 20体Lv31S | 408 | ○×××× | 2988 | 6 | 662 | 157 | 5 |
| 28 | 50.4 | 20体Lv29S | 428 | ○×××× | 3142 | 1 | 675 | 162 | 5 |
| 29 | 54.9 | 20体Lv34S | 448 | ○○××× | 3468 | 6 | 688 | 167 | 5 |
| 30 | - | 20体Lv33S | 468 | - | 3142 | 11 | 701 | 172 | 5 |

### T30 最終状態
```
撃破468 / 逃走0 / 凌いだ波30 / 稼いだDP 104,205
DP3,142  素材1,133  名声701  RP11   脅威度1.00  装備水準0.0
時代=伸長 172/210    階層5    魔王Lv31 全ステA BP15余り
研究 済94/235（取れる18・時代待ち98）
召喚できる種 7種（T1と同じ）  配下31体 Lv13〜42
囚牢 7/7満杯・転向0   因縁 0人   地上 5タイル（開始12から減少）
```

## 🎮 プレイして感じたこと ―― 攻略記事を書く視点からの評価

### ✅ 効いているもの（触らないでほしい）
- **盤を読む楽しさが本物**。入口→魔王の経路をBFSで見ると、必ず「隣接2以下の1マス」＝関所がある。
  そこを固めれば勝てる、という**明快な定石**が成立している。T1で盤を見て考える時間はとても良い
- **役割を散らす部隊コンプ**（近接だけ1.25 → 4役散らして1.45）。編成に意味がある
- **階層追加が「配置枠+6」でもある**という二重の意味づけ。DPの使い道として一番腑に落ちた
- **異変の3択**はどれも一長一短で、毎回考える価値がある

### 🔴 いちばん大きい問題：**"上手く守るほどゲームが痩せる"**
30ターン**逃走0**で完封できてしまう。その結果：

| 眠ったシステム | なぜ眠るか |
|---|---|
| **脅威度／装備水準（誘導経済）** | 1.00 / 0.0 のまま**一度も動かない**。逃がさないと動かない |
| **因縁（Nemesis）** | 0人。半分削って逃がした者にしか名が付かない＝完封だと存在しない |
| **B3F〜B5F** | 5層作って**一度も到達されない**。縦の迷宮の"縦"が使われない |
| **囚牢** | 7/7で満杯のまま。捕らえたが**使い道を毎ターン開く動機が無い** |
| **地上** | 12→5タイルに減った。**触らなくても負けない**ので触る理由が無い |

**根っこは1つ**：「関所を1つ固める」が強すぎて、**深さ・広さ・リスクを取る理由が消える**。
`泳がせる`（逃がして脅威度を上げ、見返りを増やす）が**純粋な損**になっているのが決定的。

### 🔴 2番目：**時代の進み方が"二相"で、中盤が空白**
```
胎動 T1-T19（19ターン）
  T1-T6   偉業が一斉達成 → +25/turn（DP+1,288, RP+17 が T2 に一気に入る）
  T7-T16  +5/turn の単調な消化 ← **ここが「ただ待つ」区間**
  T17-T19 210/210 で足踏み（災厄の政策を選ぶまで進まない）
伸長 T20-  同じ形（T20で研究43件・鍛造62件・時代+93 が一気に開く）
```
- **時代の変わり目だけが山**で、その間の10ターンは同じことの反復
- **210/210で3ターン止まった**のは、災厄が「政策を選べ」と待っているのにヘッダの1行しか主張しないから

### 🔴 3番目：**育つ実感が「数字だけ」**
- **召喚できる種が T1 も T30 も 7種**。研究を94本取っても**手札が増えない**
- 配下はLv13〜42まで育つが、やることは「同じ7種を関所に並べる」のまま
- 魔王は T30 で**全ステA・BP15余り**＝**育て切って手が余る**
- DPは**累計104,205稼いで手元3,142**。素材1,133が死蔵。**使い道が枯れている**

### 🟠 UI／操作で引っかかったところ
1. **異変が『侵略開始』を黙って止める**。押しても何も起きない（トーストのみ）
   → 侵略開始ボタン自体を「まず異変に答える」に化けさせるべき
2. **災厄も同じ**。時代が止まっているのにヘッダの1行しか出ない
3. **スターターのLv10配下は眷属なので隊に入らない**。初見で必ず「なぜ置けない？」となる
4. **囚牢が解禁されても『方針＝殲滅』のまま**。切り替えを意識しないと存在しないのと同じ
5. **T1のDP200に対し罠150・トーテム150**。T1は実質「罠1つ置いて終わり」か「配下だけ」の二択

## 📋 改善案（効きが大きい順）

### A. 「逃がす」に価値を作る ―― 最優先
いまは逃走＝損。**完封が最適解である限り、経済も因縁も牢も眠り続ける**。
- 撃破DPを下げ、**生還した冒険者が落とす"土産"**（素材・装備・情報）を上げる
- 脅威度が上がると**撃破DPだけでなくRPも増える**ようにする（RPが唯一の欠乏資源なので効く）
- 「無傷で完封」より「**8割削って泳がせる**」が得になる帯を作る

### B. 深い階に行かせる ―― 縦の迷宮を実際に使わせる
- **冒険者が階段を優先する**強い動機（例：浅い階の宝を無視して降りる"踏破特化"の割合を増やす）
- または**浅い階の守りに上限**（配置枠を階ごとに変え、B1Fは薄くしか置けない）
- いまは B1F に全戦力を置けてしまうので、**縦に配らせる制約**が要る

### C. 時代の中盤に山を作る
- 偉業を**時代の頭に固めず**、進行度に応じて段階的に開く
- 中盤に**中ボス的な大波**（20体上限を外した特別な波）や、時代固有の目標を置く
- **210/210 の足踏みを無くす**：災厄を答えるまで進行が止まるなら、**準備フェーズを開けない**ようにして強制的に気づかせる

### D. 手札を増やす
- 研究94本取っても**召喚できる種が増えない**のは致命的。
  進化ツリーの解禁を**召喚候補の増加**に直結させる（いまは既存個体の進化のみ）
- DP/素材の捨て場所：**装備グレードの上限開放**、**トーテムの多重化**、**階層の広さ拡張**を後半に開く

### E. 小さいが効く修正
- T1の初期DPを 200→300 程度に（罠1つ＋配下数体が置ける）
- 異変・災厄は**それを答えるまで次に進めない**ことを、ボタンの表示自体で伝える
- 囚牢の解禁時に**方針を自動でONにするか、初回だけ確認を出す**
- スターターの眷属を**「地上要員」と明示**する（図鑑で別枠に）

---

## D-1『一括布陣』／D-2『大招集』（通しプレイ T1-T30 への回答・第1弾）

### なぜこの2つを最初に作ったか
T1-T30 の実プレイで出た中心的な問題は **「上手く守るほどゲームが痩せる」**。
逃走0で30ターン完封できてしまい、脅威度・装備水準・因縁・B3F〜B5F・囚牢・地上の
**6つが丸ごと眠った**。根っこは「関所を1つ固めれば勝てる」＝**リスクを取る理由が無い**こと。
- **D-1** は「もう判断が残っていない作業」を1ボタンに畳む（時間を判断に返す）
- **D-2** は **能動のリスク**を渡す（受け身のリスク＝逃走は、上手いほど避けられてしまう）

### D-1 一括布陣（`AutoDeploy.cs` 新規）
- `PathToGoal` 入口→階段/魔王の最短経路をBFSで引く（**冒険者と同じ「壁でない」基準**）
- `ChokePoints` 経路上で歩ける隣が2以下＝関所。奥から順
- `SuggestedSpots` 経路の**終盤から**接する床を拾う（入口寄りに置かない＝手前で削り切らない）
- `DeployCurrentFloor` 表示中の階の**未配置メンバーだけ**を置く。手で置いたものは動かさない
- 下部バーに「布陣」ボタン（ツール群の末尾）。⚠ 手で置く道は残してある

### D-2 大招集（`FeverSystem.cs` 新規）
準備フェーズに**自分から**大波を呼ぶ。**取り消せない**（見てから決められると賭けにならない）。
| ノブ | 値 | 意図 |
|---|---|---|
| `WaveMult` | 2.5 | 人数。⚠ **このときだけ20体の上限を外す** |
| `LootMult` | 2.0 | 撃破DPと素材 |
| `EraMult` | 2.0 | 時代の進み（中盤の「ただ待つ区間」を自分で飛ばせる） |
| `KillsPerRp` | 3体で+1 | **本命の報酬**。RPが唯一の欠乏資源だから |

⚠ **常時倍率にしない**。押した**そのターンだけ**効く（`OnTurnStart` で解除。名簿を引く前）。
常時の倍率は難易度カーブの軸を1本増やすのと同じで、手当て済みの比（T10 0.80／T90 3.00）を崩す。

配線：`WaveRoster.RollCount`／`AdventurerAI`(撃破報酬)／`DungeonTurnManager`(OnWaveEnd・OnTurnStart)／
`SaveSystem.StaticTypes`／`GameUIManager.Title.StartNewGame`(Reset)。
ボタンは**侵略開始の隣**（「今から何を迎えるか」を決める同じ場面の手だから）。
押す前の見込みをツールチップに出す ―― **賭けは「見えている」から賭けになる**。

### 併せて直した既存バグ：**第1ターンだけ名簿が空**
`WaveRoster.Roll` は**ターンの切り替わりでしか呼ばれない**ため、開幕の T1 だけ名簿が空だった。
影響は4つ ―― ①先触れが空 ②報告が人数を語れない ③スポナーが `Max(1, Count)` で
**1体しか湧かない** ④大招集の見込みが「0→0体」。
`StartNewGame` に `WaveRoster.Roll(1)` を入れて解消（実測：T1 の名簿 0体 → **4体**）。

### 検証（実機・プレイモード）
- コンパイル：**エラー0**（警告は既存の UAC1009 3件のみ）
- T1 名簿 **4体**／`Forecast` = 「およそ **4 → 10 体**」
- 布陣：4体を一括配置 → 2度目は「全員もう置いてある」で 0体（既存を壊さない）
- 大招集：宣言で名簿 4→**10体**、波の後 **撃破9体 → RP+3・時代+5**、`Active` が自動で解除
- 下部バー：21要素で **1,797px / 1,920px**（`FitBarWidth` の網に掛からない）
- ⚠ 絵文字（🔥）は NotoSansJP SDF に無く□になるので、ラベルは実績のある **◆** に置換した

### 次にやること
- **D-3** 10連召喚＋召喚演出（魔法陣・レア枠・光。PixelLab で素材生成）
- **D-4** 撃破の手応え（画面揺れ・巨大ダメージ表示・連鎖・ドロップの吸い寄せ）
- **Phase E** 眠っている5systemを起こす（逃がす価値＝脅威度→RP／階ごとの配置枠上限で深い階へ／研究→召喚可能種の増加／時代の中盤に山）
- **Phase F** DPS/EHP の基準表（⚠ 式を写して模型を作らない。実測で取る）

---

## D-3『召喚の演出＋10連』／D-4『撃破の手応え』

### なぜ
通しプレイ T1-T30 で **DP は 104,205 動いた**のに、撃破の瞬間に起きることは
「静かに数字が増える」だけだった。ガチャも「行の文字が書き換わる」だけで、
**払った瞬間と得た瞬間が同じ**なので記憶に残らない。
手応えは「盤面が良くなること」ではなく「**押した指に返ってくること**」で出る。

### D-4 撃破の手応え（`KillFeedback.cs` 新規）
| 要素 | 中身 |
|---|---|
| 画面の揺れ | `ScreenShake`。ランク×連撃で強くなるが **0.34 で頭打ち**（乱戦で酔わせない） |
| 弾ける絵 | `FxSprite`（プール）。PixelLab生成の `impact_burst` |
| 実り | 撃破DPと素材を**別の高さに**浮かせる（重なると読めない） |
| 連撃 | 窓 2.6秒。3本目から「N 連」が出て、伸びるほど大きく・音が高くなる |
| HUD | 資源チップが増えているあいだ **1.07倍に膨らむ**（盤の数字とHUDを1本の線でつなぐ） |

⚠ **強さには一切触っていない。** 報酬の値は `AdventurerAI` が決め、ここは決まった値を見せるだけ。

⚠ `ScreenShake` は `CameraController` が `Update` で `transform.position` に**足し込む**ので、
**LateUpdate の頭で前フレームぶんを引いてから**新しい offset を足す。
実測：波の前後でカメラ座標 `(6.14, 4.50, -10.00)` が**ぴったり戻る**（residue なし）。

### D-3 召喚の演出と10連（`GameUIManager.Gacha.cs` 新規／`SummonGacha` 拡張）
- **盤に現れる瞬間**に魔法陣（PixelLab生成）。段が高い個体は光条つき → `TryPlaceSquadMember`
- **結果画面**：5列×2段のカードが1枚ずつめくれる。ユニークだけ**一拍おいて**開く（そこが山）
- **10連**：⚠ **割引はしない**。安くすると「まとめて引くのが常に得」になり、
  配下の値段という軸をこっそりずらす。値打ちは手数が減ることと天井が一気に進むことだけ。
  DPが尽きたらそこまでで止める（払い損にしない）
- `TryRoll` / `TryRollTen` は `RollOnce` を共有（支払い・天井・LastResult の更新は1箇所だけ）

### 素材（PixelLab・3枚／3 generations）
`Assets/Resources/Fx/` に `summon_circle` `burst_rays` `impact_burst`。
⚠ 取り込み設定は **Point フィルタ・非圧縮・mipmapなし**（ドット絵をにじませない）。

### 検証（実機・プレイモード）
- コンパイル：**エラー0**
- 布陣5体 → 大招集（名簿 4→10体）→ 波：**撃破10・最高連撃 5 連・RP+3**、カメラ座標に残留なし
- 10連：10枚とも表示、ユニーク2体で金枠＋光条、等級と役割が読める（スクショで確認）
- 下部バーは 21要素で **1,797px / 1,920px**

### 途中で踏んだ罠（次回のため）
1. **`MinionDef` は構造体**なので `cond ? Get(i) : null` は CS0173。有無は個体の側で持つ
2. **透明度で薄めても色は沈まない**（下地が暗いので）。暗い色との**混色**で作る
3. 等級を face と**同じ色**で書いたら読めなかった。抜く色は必ず変える
4. ⚠⚠ **`Application.runInBackground = false` だとエディタが裏で1フレームも進まない**。
   `Time.time` が 0 のまま止まり、コルーチンの演出が一切動かない ―― 検証前に必ず立てる

### 次にやること
- **Phase E** 眠っている5systemを起こす（逃がす価値＝脅威度→RP／階ごとの配置枠上限で深い階へ／研究→召喚可能種の増加／時代の中盤に山）
- **Phase F** DPS/EHP の基準表（⚠ 式を写して模型を作らない。実測で取る）

---

## E-1『泳がせの構え』＋ 眠っている系統を進言が指すようにした

### なぜ
通しプレイ T1-T30 は **逃走0** で終わり、その結果 **脅威度 1.00 のまま・装備水準 0.0 のまま・因縁 0人**。
原作の核である「泳がせて世界を育てる」層が丸ごと眠っていた。
根っこは「逃がすのは失敗」だったこと ―― **受け身のリスクは、上手いプレイヤーほど避けられる**。
だから **能動の構え**にして「今日は狩る／今日は泳がせる」をターンごとに選ばせる（大招集と対になる手）。

### 中身（`LureStance.cs` 新規）
- 準備フェーズに切り替える。⚠ **そのターン限り**（`OnTurnStart` で解除）
- HP **35%** より下まで削った相手はそれ以上叩かず、**退却させる**
  （⚠ 入口は `AdventurerAI.TakeDamage` 1箇所だけ。配下・罠・魔王のどれから来ても同じ扱い）
- 見逃して**生きて還った**1人につき **研究点 +1**。⚠ **1ターン上限3**
  （後半は波が20体になるので、栓が無いと 20RP/turn 入って牢の尋問が意味を失う）
- ⚠ **倍率をひとつも足していない**。変わるのは「逃げ切る人数」＝頻度だけ。
  脅威度・装備水準・因縁の式は既存の `LureEconomy` / `Nemesis` をそのまま通る
- 引き換えに、その波は**撃破DP・素材・時代の進みがゼロ**。無傷で奥まで来た者は魔王に届く

### 実測（プレイモード）
| | 殲滅（従来） | 泳がせ | 泳がせ＋大招集 |
|---|---|---|---|
| 撃破 | 3 | 0 | 0 |
| 見逃し | 0 | 4 | **10** |
| 脅威度 | 1.000 | 1.122 | **1.305** |
| 装備水準 | 0.0 | 5.00 | 3.8 |
| 因縁 | 0人 | **4人** | **8人**（上限） |
| 研究点 | ― | +5 | **+4（上限が効いた）** |

**30ターン一度も動かなかった3つが、1波で全部動いた。** 上限も効いている（10人見逃して RP は +3 で頭打ち）。
翌ターンに構えが自動で解除されること、泳がせOFFなら従来どおり倒せること（撃破3・見逃し0）も確認。

### 併せて：**進言が眠った系統を指すようにした**（`GuideSystem`）
通しプレイの本当の詰まりは「難しい」ではなく **「一度も指さされない」** ことだった。
研究を94節も進めたのに、召喚できる種類は **T1 も T30 も 7 のまま**。
進化は段×25DPと安いので、値段ではなく導線の問題。

| 進言 | 条件 | weight |
|---|---|---|
| 配下を進化させて手札を広げる | 解禁できる種類が1つでもある | 88 |
| 一度『泳がせて』みる | T4〜20・脅威度 <1.02・因縁0（＝**世界が一度も動いていない**） | 92 |
| 『◆ 大招集』で自分から波を呼ぶ | T5〜20・一度も呼んでいない | 90 |

⚠ 最初 weight 80 で入れたら **3枠に一度も入らなかった**（88台が渋滞していた）。実測して上げた。
⚠ 使わないままだと条件が永久に真なので、**T20 で窓を閉じる**（居座らせない）。

実測：m_evo1 を取った瞬間に `EvolvableCount` が 0→**11**、進言の上位3つが新しい3件に入れ替わった。

### 次にやること
- **E-2** 深い階へ行かせる（B3F〜B5F が未到達のまま）
- **E-4** 時代の中盤に山をつくる（+25/turn → +5/turn → 210/210 で3ターン待ち）
- **Phase F** DPS/EHP の基準表（⚠ 式を写して模型を作らない。実測で取る）

---

## E-4『時代が黙って止まる』の修正 ＋ E-2『深さの見返りを見せる』

### E-4 時代が黙って止まっていた（実測の穴）
通しプレイで **210/210 のまま3ターン**動かなかった。原因は
`if (CrisisActive && crisisPolicy < 0) return;` ―― 災厄の政策を選ぶまで足止めなのに、
告知は**災厄の始まり(160)に1回だけ**。上限に着くころには忘れている。
その3ターンは文字どおり「ただ待つだけのターン」だった。

- 止まっているあいだは**毎ターン** `NotifySystem` で言う
- `EraSystem.BlockedOnCrisisPolicy` を足し、腹心の報告が **weight 100**（罠の99より上）で指す
  ―― 罠が無いのは「弱い」だけだが、これは**ゲームが進んでいない**

実測：210/210 で `blocked=True` が2ターン続き、進言の1位が「災厄の政策を選ぶ」になる。
政策を選んだ次のtickで 胎動→伸長 に進んだ。

### E-2 深さの見返りをタブに出した
B3F〜B5F に一度も到達しなかった件。**深度倍率は実装されていた**（1階ごと +0.15）が、
**画面のどこにも出ていなかった**ので「下へ運ぶと旨い」という判断材料が無かった。
階層タブ（96px・1行で満杯）は触らず、**ツールチップ**に載せた。

実測（3層）：B1F ×1.00／B2F ×1.15／B3F ×1.30（魔王が立つ階の注記つき）。

⚠ **測って分かったが、まだ直していないこと**：+15%/階は、
「奥まで通す＝魔王に近づける」リスクの対価としては**弱い**可能性が高い。
ただし倍率を上げるのは掛け算の軸を太らせる話なので、**Phase F の実測（DPS/EHP基準表）の後**に回す。
ここで勘で上げない → [[difficulty-curve-orders]]

### 補足：B3F〜B5F 未到達は「バグではなく設計どおり」
冒険者が下へ進むには**階層ボスを倒す**必要がある（設計で確定済み）。
つまり守り切れているかぎり深い階に敵は来ない。深い階を使う道は
**落とし穴で自分から運ぶ**こと。だから E-2 は「運ぶ理由を見せる」ことに絞った。

### 次にやること
- **Phase F** DPS/EHP の基準表を実測で取る（⚠ 式を写して模型を作らない）
- その表を見てから：深度倍率／泳がせの見返り／大招集の倍率を**まとめて1回で**調整する

---

## Phase F：新しい3つの選択を**実測**した（＋見つかった2件を直した）

### 測り方
T100のカーブは既に測って手当て済み（T10 0.80／T90 3.00 → [[curve-measurement-t100]]）なので、
今回測るのは **D-2/E-1 で足した「選択」に値打ちがあるか**。模型は作らず、**同じ盤で本物の波を3回**回した。

- T8相当（`currentTurn=8`／fame=240＝基準プレイヤーの 30×T）・**3層・各階に隊5体（布陣）**
- 名簿は 11体（⚠ 少人数だと湧きの間隔が長さを決めるので8体以上で測る → [[combat-math]]）
- **セーブ/ロードで初期状態を完全に揃えた**（slot3）。dp/mat/fame/rp/threat/nem すべて同値から3回

### 結果（1波あたりの増分）
| | A 殲滅 | B 泳がせ | C 大招集 |
|---|---|---|---|
| 名簿 | 11 | 11 | **28** |
| 撃破 | 10 | **0** | **18** |
| DP | +937 | +744 | **+4,436** |
| 素材 | +53 | **0** | +197 |
| 名声 | +35 | **+385** | +385 |
| 研究点 | +1 | +4 | +7 |
| 時代 | 0 | 0 | **+5** |
| 脅威度 | +0.034 | **+0.379** | +0.375 |
| 装備水準 | 0 | **+21.3** | +18.8 |
| 因縁 | +1 | **+8（上限）** | +4 |
| 到達 | B2F | B2F | **B3F** |
| 魔王HP | 1.00 | 1.00 | **0.42** |

### 読めたこと
1. **泳がせは「DPを捨てる手」ではなかった。** DPは −21% で済む。撃破DPを失う代わりに
   **感情DP（喜び＋恐怖）は帰り着いた者からしか入らない**ので、ほぼ相殺される。
   本当に捨てているのは**素材（−100%）**で、得ているのは名声11倍・研究点4倍・因縁8人。
   代金は**あとで払う**（脅威度と装備水準が上がって世界が硬くなる）。**良い取引になっている。**
2. **B3F〜B5F を使わせるのは D-2 だった。** 大招集の波だけが B3F まで届いた。
   新しい仕掛けは要らず、**波を重くすれば縦の迷宮は自然に使われる**。
3. ⚠ **大招集は「勝てるうちは毎ターン押す」だけの手になっていた。**
   DP 4.7倍・素材3.7倍・RP+7・時代+5 に対して、代償は魔王HP −58%。
   ところが `DemonLord.OnWaveDefended` が波の終わりに **HPを満タンに戻す**ので、
   **死ななければ痛みが1ターンも残らない**＝判断のいらない手だった。

### 直したこと① 大招集に**休み**を入れた（`CooldownTurns = 2`）
⚠ 倍率では直さない（軸が太る → [[difficulty-curve-orders]]）。
牢の尋問（1回/turn）や泳がせのRP上限と同じ **回数の制限**で効かせる ―― 「いつ切るか」を選ばせる手に変える。
実測：T9に呼ぶ → T10「あと2ターン」／T11「あと1ターン」／**T12で解禁**。
ボタンも `◆ 休 2` に変わり、押す前に残りが読める（押してから断られるのは、見えているのと同じではない）。

### 直したこと② **階を下りた冒険者が退却できず突っ立つ**（実測中に発覚）
`CalculatePathTo` は `currentGridPos == target` なら**何もせずに返る**ので経路が張られず、
`OnReachedDestination` も呼ばれない ―― **永久にその場に立つ**。
縦の迷宮で階を下りた直後（`RelocateTo` が `startPos` を新しい階の入口に書き換え、本人はそこに立っている）に
退却を決めると必ずこれに嵌り、**波が制限時間いっぱいまで終わらなかった**。

`RetreatHome()` を作り、退却の入口を全部そこに通した（すでに入口の上なら、その場で清算して退場）。
実測：同じ波が **48秒で綺麗に終わる**（以前は最後の1体が立ったまま安全網の時間切れ待ち）。
⚠ `bestTarget` が無いときの分岐だけは `currentGridPos != startPos` が前提なので触っていない。

### 次にやること
- 深度倍率（+15%/階）は**まだ触っていない**。大招集で B3F が使われることが分かったので、
  「運ぶ」動機は D-2 が担っている。倍率を上げるかは**通しプレイをもう一度してから**判断する
- 通しプレイ（T1〜T30）をやり直して、D/E/F の全部が入った状態でカーブと手触りを測り直す

---

## G-1『略奪の可視化と奪還』（Geminiフィードバックへの回答・第1弾）

### まず事実確認したこと
もらったフィードバックは**数コミット古い版**が前提だった。突き合わせた結果：

| 指摘 | 実際 |
|---|---|
| 撃破時の派手な演出・画面揺れ | **D-4 で実装済** |
| 溜めと解放のハイテンションウェーブ | **D-2 大招集で実装済** |
| 泳がせの緊張感 | **E-1 で実装済** |
| 魔王の必殺技 | **既にある**（号令4種＋種族権能の5枠目） |
| 10×10でトーテム半径4は効かない | **正しい**（全13種が `radius = 4`。10×10だと直径9マス＝ほぼ全域） |
| **持ち逃げ予定の可視化** | **未実装。しかも値は全部あった** |

⚠ 案2「キルゾーン（隣接ボーナス）」は**採らない**。同じ資料が「配下側は11層で掛け算が多すぎる」と
書いた直後に**12層目を足す**提案になっている。狙っている快感は**既存の層を見せる**ことで出せる（G-4）。
⚠「ジャックポット冒険者」も採らない。**特別な冒険者の枠は因縁がすでに担っている**。二つ目を足すと両方薄まる。

### 直したこと（表示だけ・バランスは1ミリも動かしていない）
この迷宮の「持ち逃げ」は**DPではなく戦利品(`carriedGear`)**だった、というのが実装を読んで分かったこと。
- 逃げ切られる → `LureEconomy.OnGearEscaped` で**世界の装備水準**が上がる（次から敵が良い装備で来る）
- 仕留める → `GearRecoverMaterials` で**素材**として戻る

つまり「見逃すか、入口の手前で狩るか」が毎波の勝負どころなのに、**その賭けが一度も見えていなかった**。

| | 出したもの |
|---|---|
| 頭上 | `戦利品 N`（金）。逃走に入ると **赤・大きく `逃走 戦利品 N`** に変わる |
| 仕留めた | `奪還！ 素材 +N`（金・光条・画面揺れ・音）。⚠ **逃走中に仕留めたときだけ大きく出す**（そこが山だから） |
| 逃げ切られた | 「<b>持ち逃げされた</b> ― 戦利品 18（世界の装備水準 0.0 → <b>8.8</b>）」 |

⚠ `KillFeedback.OnRecover` は**素材を1つも足していない**。素材は既に `droppedMaterials` に入っている。
演出のついでに報酬を足すと軸が1本増える → [[difficulty-curve-orders]]

### 検証（実機・T8・3層・名簿11）
- コンパイル**エラー0**／実行時エラー0
- 頭上ラベルが金で出る（スクショ確認。1体は**戦利品10**を抱えていた）
- 逃走中を仕留める → 素材 298→306（**うち5が奪還ぶん**＝`GearRecoverMaterials(5.0)` と一致）
- 逃げ切られる → 通知2件。**1体が戦利品18を持ち出し、世界の装備水準が 0.0→8.8** に跳ねた
  ―― この振れ幅がいままで**完全に見えていなかった**

### 測定台で踏んだ罠（次回のため）
`SaveSystem.Load` を**直に呼ぶと** UI側の後始末（`DoLoad → OnGameLoaded → SetSurfaceMode(false)`）が
走らないので、**タイトルが被り、迷宮カメラが `enabled=false` のまま**になる（`Camera.main` が null）。
製品の導線は正しいので**バグではない**。テストからは `DoLoad` を呼ぶこと。

### 次
- **G-2** 因縁＝大物化（多くを抱えて来る／倒すと大きく返る。⚠ 倍率は足さない）
- **G-3** トーテム半径の手当て（10×10で半径4が全域＝置く場所の判断が消えている）
- **G-4** 範囲の可視化（キルゾーンの快感を、新しい倍率なしで出す）

---

## G-2『因縁＝大物化』（ジャックポットは新設せず、既にある系統に寄せた）

### 設計の判断
Gemini の「ジャックポット冒険者を新設」は**採らなかった**。
特別な冒険者の枠は **因縁がすでに担っている**ので、二つ目を足すと両方薄まる。
代わりに因縁を大物にした。⚠ **倍率は1つも足していない**（過去に因縁で倍率を重ねて失敗した記録あり）。

### 中身
| | 何を |
|---|---|
| 🎁 抱えて来る | 逃げ切ったときの戦利品を `Hero.hoard` に覚え、**次に現れるときそれを抱えている**。G-1 の頭上表示がそのまま効いて「戦利品 12」が最初から出る＝**大物だと一目で分かる** |
| 🎁 取り返す | 討ち取ると `LureEconomy.RecoverGear` で**世界の装備水準が下がる**。撒いたときと同じ係数(`GearSpreadFrac`)で戻すので、撒いた量より多くは回収しない |
| 💥 決着を盤に出す | `Nemesis.OnSlain` の見返り（Lv16・逃走1回で **632DP**）は**画面右の通知にしか出ていなかった**。`KillFeedback.OnNemesisSlain` で撃破の場に出す |

⚠⚠ **`gearLevel` はこれまで上がる一方だった**（`OnGearEscaped` と `Reset` しか無い）。
取り逃がしが積み上がるだけで**取り返す道が1本も無かった**。
「自分が育てた敵を討ち取ると、そいつが世界に撒いた装備が戻る」という形で**唯一の下げ道**を通した。

⚠ 因縁のときは `OnRecover`（奪還の帯）を出さない。決着の帯と**二重になる**。

### 検証（実機・T8→T10）
- 泳がせ1波で因縁8人が誕生し、**hoard が個別に記録された**（12.5／7.5／5／2.5／0…）
- T10 に2人が復帰。**hoard 12.5 の個体だけ頭上に「戦利品 12」**、hoard 0 の個体は何も出ない
  （＝全部が大物ではない。だから信号として意味がある）
- 討伐の実測：**+632DP／+5素材／世界の装備水準 -6.3**（12.5 × 0.5 = 6.25 と一致）
- hoard を持たない因縁の討伐は **-0.0**（装備水準は動かない）。正しい
- コンパイル**エラー0**／実行時エラー0

### 実測で見えた副産物（数字は変えていない）
守りが薄い状態で3ターン回したら、**世界の装備水準 0 → 80／世界水準 F Lv14 → A Lv20／危険度 三級 → 準一級**まで走った。
逃がすほど世界が硬くなるのは設計どおりだが、**この速さがいままで完全に不可視だった**。
（通しプレイ T1-T30 では逃走0だったので 0.0 のまま動かなかった＝同じ仕掛けの裏表）

### 次
- **G-3** トーテム半径の手当て（10×10で `radius = 4` が全域＝置く場所の判断が消えている）
- **G-4** 範囲の可視化（キルゾーンの快感を、新しい倍率なしで出す）

---

## G-3『トーテムの半径を盤の広さに追随させる』

### 実測（まず測った）
半径を **4 で固定**していたので、トーテム1つが「入口→最深部の経路」を覆う割合が
盤の広さで滅茶苦茶に振れていた。⚠ 盤は拡張のたびに作り直されるので、
**同じ盤で新旧の半径を比べて**マップ差を消してある。

| size | 経路長 | 旧（半径4） | 新（追随） | 実効半径 |
|---|---|---|---|---|
| 10 | 10 | **90%** | **50%** | 2 |
| 20 | 24 | 38% | **38%** | 4 |
| 30 | 42 | 21% | 31% | 6 |
| 40 | 61 | 15% | 28% | 8 |
| 50 | 82 | **11%** | **26%** | 10 |

**両端が壊れていた。** 10×10 では1つで経路の9割を覆う＝**どこに置いても同じ**。
50×50 では1割しか覆えず、経路を覆うのに9個要るのに枠は全部で30＝**実質使えない**。
原因は同じで、**盤が5倍になるのに半径が動かない**こと。

### 手当て（`TotemCatalog.EffectiveRadius`）
`半径 = 基準半径 × 盤の広さ / 20`（＋空間タイプの補正）。

⚠⚠ **20×20 を基準にした**。実測のとおり **20×20 の値は 38% → 38% でまったく変わらない**。
＝最初の拡張後＝いちばん長く遊ぶ中盤のバランスには**触っていない**。直したのは両端だけ。
⚠ `DungeonTheme.TotemRadiusBonus`（蟻の巣は狭い）は**掛けたあとに足す**。
先に足すと、空間タイプの差まで盤の広さで伸び縮みしてしまう。
⚠ 図鑑のツールチップも「この階では**半径N**」と実効値を出すようにした（固定値を書いたままだと嘘になる）。

### 分かったが直せないこと（正直に記録）
**10×10 では、どう半径をいじっても配置は判断になりきらない。** 経路そのものが 6〜12 マスしかないので、
半径1でも半分は覆う。これは半径の問題ではなく**盤が小さいこと**そのもの。
最初の盤なのでそれで良い、と判断した。⚠ ここを直そうとして初期サイズを上げると、
建造費と初期DPの式（→ [[title-and-start-settings]]）まで動く。

⚠ 10×10 は 90% → 50% の**弱体化**にはなる。T10 の比は元々 0.80（攻撃側有利）なので、
ここは**次の通しプレイで見る**。数字での埋め合わせはしない（勘で軸を足さない）。

### 次
- **G-4** 範囲の可視化（キルゾーンの快感を、新しい倍率なしで出す）
  ―― 半径が盤ごとに変わるようになったので、**見せないと余計に分からない**。優先度が上がった

---

## G-4『トーテムの効き目を盤に描く』（キルゾーンの快感を、倍率なしで）

### なぜ
トーテムは「範囲内の配下を強くする」仕掛けなのに、**その範囲が盤のどこにも描かれていなかった**。
置いた本人が「いま誰に効いているのか」を確かめられないので、置き場所が判断にならない。
G-3 で**半径が盤の広さで変わる**ようになったので、なおさら見せる必要があった。

⚠⚠ Gemini の案2「殺戮部屋に隣接ボーナスを足す」は**採らない**。
配下側の掛け算の層は既に多く（冒険者4 vs 配下11）、そこに12本目を積むと難易度カーブが崩れる。
狙っている快感（自分の作った濃いところを見てニヤリとする）は、**可視化だけで出る**。
**倍率はひとつも足していない。**

### 中身（`TotemRangeView.cs` 新規・`ExcavationPreview` と同じ作り）
| ツール | 何を描くか |
|---|---|
| 🗿 トーテム | **選んでいる種類**の効き目（薄い＝1つ／濃い＝2つ重なり）＋ カーソル位置に置いた場合の範囲を**白**で。白は「**新しく届くようになるマス**」だけ |
| 🛡️ 部隊 | どこが厚いかだけ（種類は問わない）。隊員はトーテムの中に立たせてこそ効くので、置く前に濃いところが見えている必要がある |

下部の帯に1行：
`🗿 誘惑の灯（半径 4）― ここに置くと 10 マスに新しく届く／既に効いている 35 マス`

⚠ **重ねがけは2つまで**（`totemBuffMaxStack`）なので、**濃さも2段で止める**。
3段目を描くと「重ねるほど強い」という嘘になる。
⚠ 種類で絞るのは、重ねがけが**同じ種類どうしでしか起きない**から（`TotemSum` の実装どおり）。
⚠ `FeaturesOf` は公開せず `CollectTotems(floor, cells, kinds)` を足した（毎フレーム new しない形）。
⚠ 描画順は掘削のプレビュー(40)より下の 38。掘っている最中はそちらを主役にする。
⚠ 盤は**キャッシュしない**（縦の迷宮で B1F を握り続けて別の階に描く事故を避ける）。

### 検証（実機・30×30・同種2つを経路上に設置）
- トーテムツール：**35マスに着色**、重なりが濃く出る。帯は
  「ここに置くと **10** マスに新しく届く／既に効いている **35** マス」
- 部隊ツール：「トーテムが効いている範囲（濃い＝2つ重なっている）― 35 マス」
- 本物の導線（ツール選択 → `GridInputHandler.Update`）で動くことを確認。スクショで色の段も確認
- コンパイル**エラー0**／実行時エラー0

### G-1〜G-4 まとめ
Gemini のフィードバックへの回答は以上で完了。採ったのは3件（略奪の可視化／因縁の大物化／盤の狭さ）、
**採らなかったのは2件**（キルゾーンの隣接ボーナス＝軸が増える／ジャックポット冒険者＝因縁と役割が被る）。

### 次
**D/E/F/G 全部入りで通しプレイ T1〜T30 をやり直す。** 見るべきもの：
- G-3 の 10×10 弱体化（経路カバー 90%→50%）が序盤の手触りをどう変えたか
- 泳がせ／大招集／奪還が「毎ターン選ぶ手」として機能しているか
- 深度倍率(+15%/階)を上げるべきか（Phase F で保留にした唯一の数字）

---

## J-1『ドロップのジャラジャラ』（撃破の場とHUDを線で繋ぐ）

### なぜ
D-4 で「撃破の場に数字を出す」と「チップが膨らむ」は入れたが、**その2つが繋がっていなかった**。
盤で起きたことと、上の数字が増えたことが**別々の出来事**に見える。あいだを**物が飛ぶ**と1つになる。

### 中身（`LootBurst.cs` 新規）
硬貨（DP）と塊（素材）が倒れた場所から弾け、**上部HUDのチップへ吸い寄せられて**消える。
着弾のたびに短い音（最短間隔つき・音程はランダムに高め＝ジャラジャラ）。

⚠⚠ **絵はHUDのチップと同じもの**（`UIIcons` の硬貨と塊）を使う。
ここで PixelLab で別のきれいな絵を描くと、**飛んでいく物と着く場所が別物に見えて**、
「繋ぐ」という狙いそのものが消える。目が追えることが全て。**だから PixelLab は使っていない。**

⚠ 行き先は毎フレーム引き直す（カメラが動くとズレる）。チップはスクリーン空間のUIなので
`Camera.main.ScreenToWorldPoint` を通す（→ `GameUIManager.ChipWorldTarget`）。
実測：DPチップ = screen(1652, 1400) / 素材 = (1865, 1400)（2560×1440）。
⚠ カメラが無いとき（地上フェーズ）は**上へ抜ける**フォールバック。0,0 に吸い込むと妙な絵になる。
⚠ 1回の上限（硬貨5・塊4）と**同時の上限90**を両方置く。28体の波で1体6個だと170個になる。
⚠ 報酬には触っていない。DPと素材はもう `AdventurerAI` が渡している。

### ⚠⚠ 途中で私が誤診して、直して、戻した記録
スクショに **「810028」** という読めない塊が出ていたので「乱戦で数字が重なっている」と判断し、
**連撃中は個別の数字を出さない**という手当てを入れた。**これは誤診だった。**

あの数字は重なりではなく、**私がテストで撃った一撃（999,999）の軽減後の実値そのもの**。
検算：999,999 → Lv10戦士 717,948 ／ Lv15 691,357 ／ Lv20 666,666。
本物の戦闘の数字は2〜3桁で（スクショで「18」）、重なりは起きていない。

→ **手当ては全部戻した**（残したのは無害な「少しばらす」だけ）。
**教訓：見えた症状を疑う前に、その数字がどこから出たのかを確かめる。**
自分のテスト器具が作った異常値を、製品の不具合と読み違えた。

### 検証（実機・T8・3層）
- コンパイル**エラー0**／実行時エラー0
- 撃破で硬貨と塊が弾け、**同時に17個**が飛んでいる状態をスクショで確認
- 行き先の座標がHUDのチップと一致することを `WorldToScreenPoint` で確認
- 本物の戦闘（デバッグ一撃なし）でスクショ ―― 数字は「18」など2桁で読める

### 見つけたが直していないこと
戦闘中、**盤の上の文字ラベルが重なる**（`中級 火炎!(MP...` ＋ `素材...` ＋ `スケルトン Lv6` が同じ帯に）。
これは今回の作業とは無関係の既存の重なりで、優先度は低い。通しプレイで気になるようなら手当てする。

### 次（地上の方針・S-1〜S-3）
Gemini の地上フィードバックを実装と突き合わせた結果、**前提が1つ事実と違っていた**：
「地上の発展が冒険者の質・量に跳ね返る構造」は**今のコードには無い**。
- `WaveRoster.RollCount` の入力に**地上由来はゼロ**
- 職は `Random.Range(0, 4)` の**完全な乱数**
- 地上研究18ノードは**全部が地上のことしか解禁しない**

→ **S-1** 常設の「次に起きること」（敵軍の進発カウントダウンが通知で流れて消えている）
→ **S-2** 工作 → 次の波の職を寄せる（`Random.Range(0,4)` という一行のフックがある）
→ **S-3** 地上を放置した代償を迷宮で受ける（実測された「地上は放置しても負けない」への回答）
→ S-4 地上研究→迷宮の玩具は**保留**（層をまたぐ解禁は掛け算の軸を増やす。通しプレイの後で）

---

## S-1『次に起きること』（流れて消えていた期限を、迷宮の画面に出しっぱなしにする）

### なぜ
期限のある出来事は**既にいくつもあった**のに、知らせ方が**ターン頭の通知1回だけ**だった。
`EnemyForce` は「カンタの軍が集まりつつある（2ターン後に進発）」を `NotifySystem` に流すが、
**右の通知は流れて消える**。だから「あと2ターン」という圧が、その場で消えていた。

⚠⚠ **地上のサイドバーではなく迷宮の画面に出した。** 狙っている感覚は
「この波さえ凌げば」「あの軍が来る前に関所を厚くしないと」 ―― つまり
**迷宮の判断をしている最中に見えていないと圧にならない**。

### 中身（`Foretell.cs` 新規＋HUDの小パネル）
資源チップの真下に4行まで、近い順に。0ターン＝「今」（未来に見せない）。

| 出どころ | 出る行 |
|---|---|
| `IncidentSystem.HasPending` | 今 異変に答えていない ― 答えるまで侵略を始められない（危険） |
| `EraSystem.BlockedOnCrisisPolicy` | 今 時代が止まっている ― 災厄の政策を選ぶ（危険） |
| `EnemyForce.Army.musterTurns` | 2T ○○の軍が △△ から進発（戦力 117）（危険） |
| `Prison.Captive.defiance` | 6T ○○ が膝を折る（好機） |
| `TrainingSystem.turnsLeft` | 3T 訓練が終わる（2体）（好機） |
| `FeverSystem.ReadyTurn` | 3T 大招集が使えるようになる（好機） |
| 時代の残り | 12T 胎動の時代が終わる（20ターン以内のときだけ） |

⚠ `Foretell` は**読むだけ**（ターンを進めない・値を変えない）。
⚠ 残り1ターンは赤くする。**次のターンに起きるものだけは色で分かる**必要がある。
⚠ `Prison.DefianceDecayOf(c)` を足した（残りターンの計算に要る。private だった）。

### ⚠⚠ 実測で分かった、もっと大事なこと
**T8 の普通の状態で、予定されている出来事が「0件」だった。**
軍は集結しておらず、捕虜も訓練も無く、時代は42ターン先。
つまり **Civ の「あと1ターン」が生まれないのは、表示の問題ではなく
「序盤には予定された出来事がそもそも無い」から**だった。

パネルは正しく動く（仕込めば3行出るのをスクショで確認）が、**中身を作るのは S-3 の仕事**。
地上に牙を持たせて軍が実際に集結するようになれば、この枠は自然に埋まる。
＝ S-1 は S-3 の**受け皿**であって、単体では効かない。順番は間違えていないが、
**S-1 だけ入れて「あと1ターンができた」とは言えない。**

### 検証（実機）
- コンパイル**エラー0**／実行時エラー0
- 仕込み（大招集を呼ぶ＋捕虜2人）で
  `3T 大招集が使えるようになる` `6T E級の魔術師 が膝を折る` `7T D級の戦士 が膝を折る` の3行
- スクショで、資源チップの下・盤の右上に収まっていることを確認（盤の邪魔をしない）
- 地上フェーズでは隠れる

### 次
- **S-2** 工作 → 次の波の職を寄せる（`e.job = Random.Range(0,4)` という一行のフック）
- **S-3** 地上を放置した代償を迷宮で受ける ―― **S-1 の枠を埋めるのはこれ**

---

## S-3『地上を放置した代償を迷宮で受ける』（「放置しても負けない」の正体を潰した）

### ⚠⚠ 原因は数字ではなく、**1行の分岐**だった
通しプレイの実測「地上は放置しても負けない」（版図 12→5 に減っても平気）の正体を、
コードを追って特定した。

```
EnemyForce.PickTarget : if (r.type == RegionType.Gate) continue;   // 入口は狙わない
EnemyForce.Assault    : if (tgt.type == Gate) { a.targetId = -1; return; }  // 入口は落とせない
EnemyForce.ResolveTurn: if (a.targetId < 0) { Retreat("狙う先が無くなった"); }
```

つまり **こちらの地上の領域が無くなると、奪還軍は狙う先を失って帰っていた**。
＝**版図を全部失ったほうが安全**だった。`Assault` のコメントには
「そこは迷宮側の防衛戦で決着する」と書いてあったのに、**その配線が存在しなかった**。

### 手当て
土地を奪い返せなくなった**人間の奪還軍は帰らず、坑道そのものを目指す**。
入口に着いたら1ターン数え、次のターンに**雪崩れ込んで次の波に加わる**。

| | 中身 |
|---|---|
| `Army.toDungeon` / `gateTurns` | 迷宮を狙っている／入口で数えている残り |
| `NextStepToGate` | ⚠ **幅優先**。通常の `NextStep` は貪欲法で**窪みに嵌まって往復した**（実測：石造りの牧草地 ↔ 廃里 を3ターン往復して引き上げた） |
| 引き上げの除外 | ⚠ 迷宮へ向かう軍は `idleTurns` で引き上げない（引き上げたら元の木阿弥） |
| `WaveRoster.MixInDungeonAssault` | 戦力 ÷ **60** 体（2〜12）を名簿に足す。踏破目的・満足して帰らない |
| `Foretell` | 「2T 奪還軍 が坑道へ雪崩れ込む（戦力 300）」→「今 討伐隊がこの波に加わる」 |

⚠ **人数だけを足す。** レベルや強さに係数は掛けない（既存の `lvBase`／`worldTier` をそのまま使う）。
⚠ 人数の上限20は超える（大招集と同じ「自分の選択の結果」）。ただし**大招集と違って防げる** ――
地上で軍を潰せばよく、入口でも1ターン止まって見えている。
⚠ 他の魔王の軍（`owner >= 0`）は土地が欲しいだけなので、これまでどおり引き上げる。変えたのは人間の奪還軍だけ。

### 検証（実機・自領を全部失わせて追跡）
```
T9  1T 奪還軍 が 綻びの木立 から進発（戦力 300）
T11 2T 奪還軍 が坑道へ雪崩れ込む
T12 1T 奪還軍 が坑道へ雪崩れ込む      ← ここが「あと1ターン」
T13 今 奪還軍 が坑道へ雪崩れ込む
T14 今 討伐隊がこの波に加わる（戦力 300）
名簿 11 → 16（＋5体・全員が踏破目的）
```
コンパイル**エラー0**／実行時エラー0。`EnemyForce` は既に `SaveSystem.StaticTypes` にあるので保存も通る。

### これで S-1 の枠が埋まった
S-1 で「T8 の普通の状態では予定が0件」と書いたとおり、**受け皿だけでは効かなかった**。
S-3 が入ったことで、地上を失うほど **S-1 のパネルが埋まり、迷宮の判断に効く**。
2つで1つの機能になっている。

### 次
- **S-2** 工作 → 次の波の職を寄せる（`e.job = Random.Range(0,4)` のフック）
- そのあと **D/E/F/G/J/S 全部入りで通しプレイ T1〜T30**

---

## S-2『流言』（地上と迷宮のあいだの、最初の直通路）

### なぜ
実装を読んで分かったこと ―― **地上と迷宮は繋がっていなかった**。
- `WaveRoster.RollCount` の入力に**地上由来のものは1つも無い**
- 職は `e.job = (Job)Random.Range(0, 4)` の**完全な乱数**
- 地上研究18ノードは**全部が地上のことしか解禁しない**

＝ 地上を耕しても、**迷宮の盤の上では何も変わらなかった**。

### 中身（`RumorSystem.cs` 新規）
**威名 20** を払って噂を撒くと、次に降りてくる**顔ぶれが寄る**（名簿のおよそ 60%）。

| 噂 | 呼ぶもの | うまみと厄介さ |
|---|---|---|
| 魔物が暴れている | 戦士 | 硬いが、宝には目もくれない |
| 財宝が眠っている | **盗賊** | 宝箱を漁る ― **戦利品を多く抱えて帰ろうとする（奪還の的）** |
| 呪いが広がっている | 僧侶 | 仲間を癒す ― 長引くが、術が尽きれば脆い |
| 秘術の遺構がある | 魔術師 | 遠くから焼く ― 脆いが、魔力が続くかぎり削られる |

⚠⚠ **強さには触らない。** 変えるのは**顔ぶれ（職の比率）だけ**で、人数もレベルもランクも既存の式のまま。
⚠ 対価は**威名**（地上の資源）。地上を耕した者だけが撒ける、という形で「地上をやる理由」が迷宮側に生まれる。
  相場は交易路25／和平40なので 20 は少し安め ―― 毎ターン撒けるが、他の外交は諦める、くらいの位置。
⚠ 撒くと**その場で名簿を引き直す**（大招集と同じ）。そのターン限り。

### ⚠ 置き場所は『先触れ』の中
ここは「何が来るか」を見る場所なので、その隣に「**何を来させるか**」があると
**見る → 仕込む → 備える** が1画面で繋がる。地上メニューに置くと、波を見ながら決められない。

実際、盗賊を呼ぶと要約が自動で「**盗人が多い。宝を持ち逃げされる。**」に変わる。
G-1（略奪の可視化）と G-2（奪還）が**そのまま噛み合っている**
―― 自分で鴨を呼び、入口の手前で仕留めて素材にする、という一連の手になった。

### 検証（実機）
- コンパイル**エラー0**／実行時エラー0
- 撒く前 戦士3 盗賊2 僧侶2 魔術師4 → 撒いた後 **戦士2 盗賊6** 僧侶1 魔術師2（計11のまま）
- 2回目は「この波にはもう噂を撒いてある」で弾かれる。威名も正しく減る
- 別の試行では 盗賊 **8/11**（Bias 0.6 まわりのばらつき）

### スクショで見つけて直した2つ
1. 「🗣️」が**□**になった（NotoSansJP SDF に無い）→ 実績のある **◆** に
2. 『次に起きること』が**0件でも枠だけ出ていた** → 0件なら枠ごと隠す
   （見出しだけの空箱は「予定が無い」ではなく「壊れている」に見える）

### 次
**D/E/F/G/J/S 全部入りで通しプレイ T1〜T30。** 見るもの：
- G-3 の 10×10 弱体化（経路カバー 90%→50%）が序盤の手触りをどう変えたか
- 泳がせ／大招集／流言／奪還が「毎ターン選ぶ手」になっているか
- S-3 の討伐隊が、地上を放置したときにちゃんと効いてくるか
- 深度倍率(+15%/階)を上げるべきか（Phase F で保留にした唯一の数字）

---

## 通しプレイ（D/E/F/G/J/S 全部入り）― T30に届かず、**T11〜T13で壁**

⚠ **結論を先に**：30ターンまで行けなかった。**2周とも T11〜T13 で崩れた。**
そしてこれは「難しすぎる」話ではなく、**資源が戦力に変わる導線が細い**という話だった。

### 2周の一致した崩れ方
| | 1周目（配置枠を空けた） | 4周目（配置枠を毎ターン埋めた） |
|---|---|---|
| 崩れ始め | T12（撃破1） | T9〜T10（撃破3→0） |
| 決着 | **T13 魔王が討たれた** | T11 で撃破0（詰み） |
| そのとき余っていた物 | **DP 40,210・素材 636** | **DP 17,120・素材 281・魔王BP 76** |
| 配置 | 10/22（半分空き） | **30/30 × 2階（満杯）** |
| 脅威／装備／世界水準 | 2.96／100／7.0 | 3.54／100／7.0 |

**枠を埋めても寿命は1〜2ターンしか伸びない。** 本質は別のところにある。

### ⚠⚠ 一番大事な発見：**量は飽和し、質に投資する導線が無い**
4周目は配置枠を **30/30 × 2階** まで埋めた。それでも T10・T11 は**撃破0**。
私の配下は **Lv14の素のスケルトン**で、
**装備を鍛えず（素材281が眠ったまま）／進化させず（召喚できる種は7のまま）／
感情ツリー0個／魔王BP 76が未使用**だった。

つまり —— **数の軸（配置枠・体数）はすぐ飽和し、
質の軸（進化・鍛造・感情・魔王BP）に投資しないと T10 前後で壁に当たる。**
そして**ゲームはそれを壁の手前で教えない**。進言は「遺物」「感情ツリー」を出すが、
**「素材が281眠っている」「BPが76眠っている」を強く指さない**。

### 新しい層は、ちゃんと働いた
- 🔥 **T7の流言＋大招集**（1周目）が最大の山：盗賊18人を呼んで25人の嵐にし、**DP +7,563・素材 +170**。
  波の途中で「抱えている戦利品の合計40」＝**仕込む→嵐にする→入口の手前で奪還する**が1ターンに収まった
- ✅ **G-2の奪還**：因縁を討ち取って**装備水準が 31.3→30.0 に下がる**のを確認（下げ道は本当に効く）
- ✅ **S-1**：異変未回答で侵略が始まらないのを、パネルが先に教えた（無かったら原因不明で止まっていた）
- ✅ **連撃10**（大招集の波）／**役割5種で部隊バフ ×1.55**
- ✅ **大招集は無限の金ではなかった**：T7/T10は無傷、T13で死んだ。世界が追いつくと本当に殺しに来る
- ✅ **拡張は諸刃**：盤が広がると同じ人数では守れない（経路11→21・関所2→6）

### 見つけた実バグ（**修正してコミット済** `6b15aef`）
1. **新しい周でターン番号とフェーズが戻らない** ―― T13で敗北→再開すると**T13の防衛戦のまま**始まる
2. **`Reset()` を持っているのに呼ばれていない系統が7つ** ―― `LureEconomy`（脅威度・装備水準）／
   `EraSystem`／`ResearchState`／`MinionEvolution`／`TrainingSystem`／`DiplomacySystem`／
   `RelicManager`／資源（素材・名声）。**周回の層が成立していなかった**

### まだ直していない（次の候補）
| # | 中身 | なぜ |
|---|---|---|
| A | **進言に「余っている資源」を最優先で出す** | 2周とも資源を余らせて負けた。`素材>200` `BP>20` などで weight 95 |
| B | **侵略開始で「この階に何も置いていない」を止める** | 拡張は配置を全消しする。そのまま突入すると即死する（実際に起きた） |
| C | **鍛造と進化への導線** | 「装備1段で+22%」を壁の手前で見せる。素材の使い道が図鑑の奥にしか無い |
| D | `RunStats.Escapes` が常に0 | 逃走が戦績に数えられていない |
| E | 拡張が安い（RP3+DP400で枠+4・半径×2・経路×2） | 効果に対して値段が軽い |
| F | 地上の産出が大きすぎる（T2で+1,217DP） | ただし4周目では枠を埋めたら足りなくなった。**Aとセットで見る** |

⚠ **数字を触る前にAとBとCをやるべき**。2周とも「資源が余ったまま負けた」＝
いま足りないのは強さではなく、**強くなる道を指すこと**。

---

## A/B/C：通しプレイで負けた原因への手当て（数字ではなく導線）

### A 余っている資源の進言を、**余っている量で重くする**
2周とも資源を余らせたまま壁に当たった。**進言そのものは前からあった**が、
weight が 45〜70 の固定で、88〜92（進化・遺物・感情）に**一度も勝てなかった**。

⚠ **一律に上げない。** 少量のときまで叫ぶと、今度はこれが他を全部潰す。
「40くらい貯まっている」は小声、「300貯まっている」は**画面で一番大きい声**にする。

| 進言 | 重み |
|---|---|
| 装備を鍛える | `45 + min(50, 素材/6)` → 素材200で78・**300で95** |
| 配置枠を埋める | `66 + min(30, 空き×3)` → **10空きで96** |
| 魔王のBPを振る | `70 + min(26, BP)` → BP20で90・**26以上で96** |

文言も具体にした：「**1段でおよそ +22%**（レベル5〜6ぶん）」「DPは足りています。足りないのは置いた物です」。

**実測（1周目の敗北時の状態を再現）**：DP 39,975・素材 636・配置 5/14 で
```
95 : 装備を鍛える（『図鑑』→ 個体の武器・防具）
93 : 配置枠を埋める（罠・スポナー・トーテム）
92 : 一度『泳がせて』みる
```
**私が実際にやらなかった2つが、そのまま1位と2位になった。**
逆に 素材45・DP50・枠8/14 のときは**この2つは出てこない**（静かなまま）ことも確認。

### B 何も置いていない階があるまま突入させない
階層の拡張は**配置を全部消す**（返金あり）。置き直さずに侵略開始を押すと、
無防備の階に波が入って**その1ターンで魔王が死ぬ**（2周目で実際に起きた）。

`StartBattlePhase` に全階の点検を足した。⚠ **一度断るだけ**で、もう一度押せば通す。
毎回止めると「置かない」という選択ができなくなる ―― **事故は止めるが、判断は奪わない**。
⚠ `PlacedCount` は表示中の階しか見ないので、`PlacedCountOf(floor)` を足して全階を見る。

**実測**：配置を全部外して押す → 1回目は `前半・迷宮` のまま（止まる）／2回目で `前半・防衛戦`（通る）。

### C 鍛造への導線
A の素材の進言に畳んだ（題を「装備を鍛える（『図鑑』→ 個体の武器・防具）」にし、
効果量と、素材200超では「数を増やすより、いま居る配下を鍛えるほうが効きます」を出す）。

### 次
この3つを入れた状態で**通しプレイをやり直す**。見るのは
「T10前後の壁を、進言に従うだけで越えられるか」。
越えられないなら、そのとき初めて**数字**（深度倍率・拡張の値段・地上の産出）を触る。

### A/B/C の効きの確認（5周目・T5まで）
進言を**読んで、その通りに動く**プレイで回した。**進言は狙いどおりに切り替わった**：

| T | 進言の1位 | 私がやったこと |
|---|---|---|
| 2 | **[96] 配置枠を埋める** | 罠で 3/14 → 満杯へ |
| 3 | **[92] 魔王のBPを振る** | BP 5点を振った（魔王Lv3） |
| 4 | [88] 遺物 | （鍛造の窓口は素材56で未点灯＝小声のまま。正しい） |
| 5 | **[90] 大招集** | 呼んだ（名簿25） |

**A の狙い（少量なら小声、余っていれば最大の声）は live でも成立している。**
⚠ ただし **T30 までは回せていない**（1ターンが実時間で1〜2分かかり、
自動の周回ループが途中で位相ずれを起こした）。**壁を越えられたかは未確認。**

### いま確実に言えること／言えないこと
- ✅ **言える**：1周目の敗北時の状態を再現すると、進言の1位2位が
  「装備を鍛える(95)」「配置枠を埋める(93)」＝**私が実際にやらなかった2つ**になる。
  素材45・DP50 のときは出てこない（静かなまま）。B のガードも1回目で止まり2回目で通る。
- ❌ **言えない**：「進言に従えば T10 の壁を越えられる」。これは**通しで回して初めて言える**。

### 次
**T30 までの通しを、位相ずれの起きない形で1回やる**（1ターンずつ手で送る）。
それで越えられなければ、そのとき初めて数字（深度倍率・拡張の値段・地上の産出）を触る。

---

## 通し（進言に従うプレイ）― T9で**Aが効かない理由**が特定できた

位相ずれの起きない形（フェーズを見て、その場でできることだけをする1手）で回し直した。
**T9 まで進んで、そこで止まった。** ただし今回は**止まった理由が正確に分かった**。

### T9の状態
```
配下 6体・最高Lv11・**武器グレード0／防具グレード0（＝素手のまま）**
素材 182 を持っている（＝一度も鍛えていない）
配置 30/30（満杯）／DP 9,362／脅威 2.38／装備水準 100
累計撃破 54（T7から**一度も増えていない**）
```

### ⚠⚠ なぜ「装備を鍛える」が出なかったか
`45 + 182/6 = **75**`。そして上には**常設の 88（遺物）/87（研究）/86（感情）**が居た。
この3つは**押さないかぎり条件が消えない**ので、無視され続けると**永久に3枠を占める**。
＝ **A で重みを可変にしたのに、その上に「動かない天井」があった。**

### 手当て（2つ）
1. **素材の傾きを立てた**：`45 + min(52, 素材/3)`（素材100で78／**156で97**）。
   素材は撃破からしか出ないので、100も貯まっていれば「使っていない」証拠。
2. **「まだ一度も触っていない」系の進言に窓を切った**（`turn <= 18`）。
   遺物・感情・初手の鍛造・装飾品。**18ターン見せて触らないなら、知らないのではなく選んでいる。**

### 検証（T9の詰まった状態を再現）
```
97 : 装備を鍛える（『図鑑』→ 個体の武器・防具）   ← 1位に出た
92 : 一度『泳がせて』みる
90 : 『◆ 大招集』で自分から波を呼ぶ
```
副作用も確認：**T3（素材30）では初出の進言がちゃんと出る**（進化88/遺物88/研究87）。
**T20 では遺物が引っ込み**、毎ターンの判断（泳がせ・大招集）に入れ替わる。

### いま言えること
- ✅ **壁の原因は特定できた**：素手のまま T9 を迎えていた。素材182が眠っていた。
- ✅ **その素材を指す導線は、いま1位に出る**（97）。
- ❌ **「これで壁を越えられる」はまだ言えない**。次の通しで確かめる。

⚠ 数字（深度倍率・拡張の値段・地上の産出）は**まだ一つも触っていない**。
導線が正しく点灯した状態で越えられなければ、そのとき初めて数字の番。

---

# 🎯 次にやること：**「ドパれる」体験の設計**（①〜⑥・順番も確定）

⚠ **この節は会話にしか無かった内容の記録。** 実装はまだ**1つも入っていない**。

## 通しプレイで実際に「ドパった」のは4つだけだった
1. **T7の流言＋大招集** ― 盗賊18人を呼び、25人の嵐にして、全員が戦利品を抱えて関所に飛び込んだ
2. **役割5種を揃えた瞬間の ×1.55**
3. **奪還！ 素材+12**
4. **連撃10**

共通点：**①自分で仕込んだ ②一拍待った ③一気に来た ④まとまって返ってきた**。
4拍そろっているのは1番だけで、だから1番が突出していた。

## 診断：このゲームは報酬を「集計して、静かに、あとで」払っている
地上の産出も研究点も名声も時代も、**ターンの終わりに数字が変わっているだけ**。
プレイヤーの行為と、その報いが返る瞬間が切れている。
D-4（撃破の演出）と J-1（ジャラジャラ）で**撃破だけがその外に出た**。残り全部がまだ中にいる。
→ 方針は「演出を足す」ではなく **払い方を変える**。

## ①〜⑥（実装順：**② → ④ → ③ → ① → ⑤ → ⑥**）

| # | 何を | 側 | 中身 |
|---|---|---|---|
| **②** | **波の呼吸を見せる** | 迷宮 | **3波構成は既に実装済み**（`batchSize = 総数/3`、`batchGap = 9 - ターン×0.2` 秒）。**だが画面に出ていない**。「第2波 来る」を出して間の数秒に緊張を置く＝**新しい仕掛けを作らずに溜めが手に入る** |
| **④** | **地上の収穫を見せる** | 地上 | 地上はDPの2/3を出しているのに**払い出しが完全に不可視**。ターン終わりに産出タイルが順に光り、数字がHUDへ飛ぶ（J-1を地上に適用） |
| **③** | **波の決算** | 迷宮 | 波が終わると即・地上で**余韻がゼロ**。⚠ **決算パネルは存在しない**（右のトーストが流れるだけ）。撃破N・最高連撃M・奪還K・因縁の生死を数え上がる形で1画面 |
| **①** | **戦闘中に押す物** | 迷宮 | 下のHOWツリー。**最優先だが実装が重いので4番目** |
| **⑤** | **版図が増える瞬間** | 地上 | タイル取得がいま真偽値。色が塗り変わるひと呼吸＋版図カウンタの跳ね |
| **⑥** | **時限つきの出来事を増やす** | 地上 | S-1の枠はあるのに**T8の普通の状態で予定0件**だった。施設の建設に数ターン／眷属の昇進に期限／交易路の完成を待たせる＝**地上を「待つ場所」にする** |

## ⚠ ひとつの警告（順番の理由）
「ドパれる」を目的にすると演出を足す方向に流れる。だが通しプレイで一番つらかったのは
演出の地味さではなく **T9以降、何をしても撃破が0になって打つ手が分からなくなったこと**。
**ドパミンは選択が効いている実感の上にしか乗らない。**
だから①は演出ではなく**手の追加**として扱う。②④は「既にあるものを見せるだけ」なので安くて効くから先。

---

# 🌳 ①のHOWツリー：戦闘中に押す物を作る

## 調べて分かった、使える材料
- **配下の狙いは「一番近い相手＋`AimWindow` の窓の中から気性で選ぶ」**（`ZombieAI` 436-440）
  → **窓の概念が既にある**ので、プレイヤーの指定を差し込む余地が空いている
- **`currentJoy` / `currentFear` は冒険者1人ずつに溜まり、`GrantReturnReward` の帰還時にしか清算されない**
  → **盤の上に、まだ誰も取っていない報酬が転がっている**
- 号令は5枠（治癒/落石/魔王の一撃/恐慌の波/種族の権能）DP300〜500・CD35〜70秒
  → **1波に1回撃てるかどうか**。20〜60秒の波でプレイヤーの操作は実質ゼロ

## 分解の軸＝「押すと何が変わるか」

### 分岐1 溜まっているものを使う
- **A-1 号令ゲージ**（既存回収・軽い）撃破でCDが縮む／専用ゲージが溜まる。
  ⚠ 倒す→撃てる→もっと倒せる の正のループ。**上限が要る**
- **A-2 感情の刈り取り**（既存回収・軽い・**推し**）盤の上の喜び／恐怖を「いま刈る」。
  **早く刈れば少ない、待てば増えるが逃げられる**＝押すタイミングが賭けになる。泳がせ・奪還と噛み合う。
  ⚠ 倍率を1つも足さない（既にある値を、取る瞬間だけ渡す）
- A-3 `ManaSurge`（6ターンに1回）を戦闘中の手にする

### 分岐2 狙いを変える
- **B-1「狙え」指定**（ほぼ既存・**費用対効果が最も高い**）冒険者をクリック＝範囲内の配下が集中。
  `AimWindow` の窓の中なら指定を優先、とするだけ。⚠ **窓の外は指定できない**ことが自然な制限になる
- B-2 気性の一時切替（既存12種を号令で塗り替える）中くらい

### 分岐3 盤を変える
- C-1 落とし穴の行き先をライブで差し替え（既存の「運ぶ罠」）⚠ 階層ボスの設計に触れる
- C-2 1波1回だけその場に置ける ⚠⚠ **準備フェーズの意味が薄まる。最も警戒。採らない**

### 分岐4 魔王が動く
- D-1 親征をライブに（`LordStance` の鎮座/親征）演出は一番派手。
  ⚠ **魔王HPは波の終わりに全快するので代償が残らない**（Phase Fで確認済み）。先にそこを直す必要がある

### 分岐5 時間に賭ける
- E-1 波の途中で「もう一波」を呼ぶ ⚠ 大招集は既に強い。**戦闘中版は実り据え置きでリスクだけ**にする

### 分岐6 一瞬に反応する
- F-1 **奪還の窓**（新規・G-1と直結）戦利品持ちが入口に着く直前の数秒だけ印が出て、押すと配下が殺到。
  **間に合うか／間に合わないか**が一番強い刺激。⚠ 反射ゲームにしないよう窓は数秒・回数制限

## ✅ 採用（ユーザー合意済みの方向）：**B-1 ＋ A-2 ＋ A-1 を1本で**
役割が被らず、波の時間を端から端まで埋める。**3つとも既存の値の再配分で、倍率の軸を1本も増やさない。**

| | 波のどこを埋めるか |
|---|---|
| **B-1 狙え** | 常時。数秒ごとの小さな判断 |
| **A-2 感情の刈り取り** | 中盤。待つか取るかの賭け |
| **A-1 号令ゲージ** | 終盤。溜めた物を解き放つ |

D-1(親征) と F-1(奪還の窓) は魅力的だが、**この3つが効いたのを見てから**。


---

# 2026-08-30（続き）　②波の呼吸を見せる／④地上の収穫を見せる ― 完了

ドパミン設計の①〜⑥のうち、順番どおり **②と④を1本で**入れた。
どちらも **既にある仕組みを見せるだけ**で、報酬にも人数にも倍率にも触っていない。

## ② 波の呼吸を見せる（迷宮）

### 何が問題だったか
`DungeonAdventurerSpawner` は1ターンの人数を **3つの塊**に割って送り、
塊のあいだに「息継ぎ」（`batchGap = max(5, 9 - ターン×0.2)` 秒）を空けている。
**そこが号令と立て直しの窓**として意図的に作られている ―― のに、
**画面には1文字も出ていなかった**。遊ぶ側には「なんとなく途切れる時間」でしかなく、
溜めにも合図にもなっていなかった。

### 入れたもの
- `DungeonAdventurerSpawner` に**読み取り専用**の窓口を追加
  （`BatchCount` / `BatchIndex` / `Breathing` / `NextBatchIn` / `BreathRatio` / `SpawnedThisTurn`）。
  ⚠ 値を1つも変えていない。**式には一切触れていない。**
- `GameUIManager.Wave.cs`（新規）＝ 戦闘中だけ上部中央に出る帯
  - ◆点：塊の数だけ並び、送り終えた塊は赤、次の塊は**息継ぎの進みぶんだけ**赤くなる
  - ゲージ：**満ちる方向**（減る方向にすると「猶予」に見えて、圧にならない）
  - 3つの状態を言葉で書き分ける
    - 息継ぎ中 → 「息継ぎ ― 第2波まで 3.3　**いま立て直す**」
    - 突入中 → 「第1波 突入中　1/4 人」
    - 送り終えた → 「最終波を送り終えた　あとは掃討」
  - 塊が着いた瞬間 → 帯が膨らむ＋縁が光る＋`Sfx.Wave`＋小さめの画面揺れ＋**中央に「第 2 波」**
    最後の塊だけ「最 終 波」（金）にして、**まだ来るのか終わりなのか**を必ず言う

### 実測で踏んだ2つ
1. **見出しと注記を同じ行に置けない。** 右揃えで載せたら
   「息継ぎ ― 第2波まで 1.5　いま立て直す」が「第1波 / 全2波」と**重なって両方読めなくなった**。
   → ゲージの下に1行を割いた（帯の高さ 52→66）。
2. **一声が腹心の報告の裏に隠れていた。** この帯は `BuildUI` の途中で作られるので、
   あとから作られるパネルが兄弟として上に乗る。
   → 撃つたびに `SetAsLastSibling()`（触れない文字なので手前でも操作の邪魔にならない）。

## ④ 地上の収穫を見せる（地上）

### ⚠⚠ まず判明した事実：**地上の画面には資源が1つも出ていなかった**
地上モードでは**迷宮Canvasごと `enabled=false`** にしているので、
上部バーの DP/素材/名声チップは**まるごと消えている**。
つまり地上で何をしても、持ち物がいくつ増えたのか**その場では確かめようがなかった**。
（ヘッダの「産出 +17DP」は**見込み**であって、手持ちでも着地でもない。）

### 入れたもの
- **地上の資源チップ4枚**（DP／素材／研究点／名声）。迷宮のチップと同じ `ResChip`・同じ絵。
  ⚠ **右上の角は使えない。** そこはトーストが積む場所で、トーストは order 200 の別Canvasなので
  **必ず上に被る**（実測：4枚中3枚が隠れた）。しかも収穫の瞬間は通知が一番多い瞬間。
  → トーストの帯（右端から約 420px）を避けて、その左に置いた。
- `HarvestBurst.cs`（新規）＝ J-1 のジャラジャラを地上に。産んだ領域から硬貨・塊・書・旗が弾け、
  **チップへ吸い寄せられる**。出所は**領域ごとのDP産出の重み**で散らす。
- `SurfaceMap.CollectYields` / `DistrictCatalog.Collect` / `WonderCatalog.Collect` から
  `HarvestBurst.Add(...)` で**数えるだけ**（⚠ 加算はしない。したら産出が二重になる）。
- ターンの締めで **画面の切り替えだけを 1.52 秒待たせる**（`OnPhaseChangedAfterHarvest`）。
  ⚠⚠ 待たせるのは**画面だけ**。ターンの解決は全部済んでいる（`currentPhase` はもう Prepare）。
  ここで解決を遅らせると、セーブや報告と順番が入れ替わって壊れる。
- チップの下に一行：「収穫　+1,217 DP　+34 素材　+12 研究点　+8 名声」

### ⚠⚠ 実測で踏んだ罠（同じ轍を踏まないこと）
**URP の 2D レンダラでは `SpriteRenderer` の既定マテリアルが `Sprite-Lit-Default`。**
光が当たらないと**真っ黒**に描かれる。地上カメラは Surface レイヤーしか映さないので
迷宮側の光が届かず、**収穫が黒い塊になって盤を覆った**（スクショで確認）。
→ 盤のメッシュと同じ `Sprites/Default` を明示的に張る。

もう1つ、**絵ごとに解像度が違う**（手続き生成は 64px/unit、描き起こしの絵は別）。
同じ倍率で出すと片方だけ巨大になるので、`sprite.bounds.size.y` から**実寸で割り出す**。
ヘクスの外接円は 0.5 なので、盤の上では 0.22 → 0.10 まで縮めながら飛ばす。

### ⚠ 検証中の誤読（記録しておく）
T1→T2 で DP が **+1,217** 増え、収穫の行は「+17 DP」としか言わなかったので
「取りこぼしている」と読んだ。**違った** ―― 検証のために `HardEndWave` で波を打ち切ったせいで、
**盤に残った冒険者が地上フェーズの最中も稼ぎ続けていた**（地上に居るあいだ DP 200→582 と動いた）。
T2→T3 は **+18 = +18** でぴったり一致する。収穫の行は正しい。
**普通に遊べば戦闘は地上に入る前に終わっている。**

### 承知のうえで出していないもの
施設が生む **感情** と **威名** は収穫の行にもチップにも出していない（チップが6枚になると帯が破綻する）。
必要になったら、行にだけ足す。

## 別件で見つけたこと（未対応）
`NarrativeSystem.GrantStartingBonuses()`（形見の開始ボーナス：初代の鍵 +2500DP など）が
**どこからも呼ばれていない**。装備していても入らない。→ 直すなら `StartNewGame` から。

## 次
**③ 波の決算**（決算パネルは今も存在しない）→ そのあと **①**（B-1狙え＋A-2感情の刈り取り＋A-1号令ゲージ）。

---

# 2026-08-30（続き2）　③ 波の決算 ― 完了

ドパミン設計の③。**②④と同じく、報酬は1つも足していない**（既に払われた物を数え直して見せるだけ）。

## 何が問題だったか
波が終わると画面はそのまま地上へ切り替わり、起きたことは**右のトーストが数枚流れて消えるだけ**。
何体倒したのか、何を持ち逃げされたのか、**押した手が効いたのか** ―― どれも残らない。
＝余韻がゼロで、次の波に持っていける学びが無い。

## 入れたもの

### `WaveReport.cs`（新規）＝ 1波ぶんの集計
- 数え始め＝`StartBattlePhase`／締め＝`EndBattlePhase` の**いちばん最後**。
  ⚠ 大招集の見返り・研究点・魔王の成長は**波の終わりに払われる**ので、そこまで数える。
- 資源は**入りと出を別々に**数える。⚠ 号令は戦闘中にDPを払うので、差だけ見せると
  「稼ぎが少ない」＝**押した手が損に見える**。実測で 実り+1,070／号令-800／差引+270 と出た。
- 集計の口はすべて**既にある関所**に1行ずつ足しただけ
  （`DungeonResourceManager` の Add/TrySpend／`AdventurerAI` の撃破と生還／
  `Prison`／`RelicManager.ReportDefenderLost`／`CommandSystem.TryUse`）。

### `GameUIManager.Report.cs`（新規）＝ 決算パネル
地上へ渡す前に1枚だけ挟む（④の収穫と同じく、**遅らせるのは画面だけ**）。
- 見出し：`第 N 波 ― 無傷で凌いだ` ／ 来襲・決着までの時間・最深
- 一言（`Verdict`）：⚠ **数字の言い換えにしない**。次の波で何を変えるかに繋がる言葉にする
- 大きい4つ（倒した／逃した／捕えた／最高連撃）＝**0から数え上がる**
- 実り／号令の支払いと差引／代償（持ち逃げ・装備水準・防衛体・魔王HP・奪還）
- **この波で選んだ手** ← ここが本体。大招集・泳がせ・流言・備え・親征・号令が並ぶ。
  1つも押していない波は空欄にせず「この波では何も選ばなかった（…）」と書く
- 出口は3つ：ボタン／`Space`／`Esc`

## ⚠⚠ 実測で踏んだ罠：**TMPは枠が足りないと「1文字も描かない」**
`12.5pt` の行を **高さ18px** の枠に入れたら（必要 18.11px）、代償の行が**丸ごと消えた**。
はみ出すのでも見切れるのでもなく、**そこだけ実装されていないように見える**。
→ 1行の枠は必ず「文字の大きさ × 1.5」より高くする。この panel は 22px / 21px に直した。

ついでに**画面に出ている文字を全部走査**して同じ症状が他に無いか確かめた（`characterCount==0` を探す）。
⚠ 走査は **`activeInHierarchy` のものだけ**にすること。非アクティブなTMPは組まれないので
必ず0文字を返し、978件の偽陽性になった。生きている文字に限れば **0件**。

## ⚠ ホットキーの横取り（これが無いと壊れる）
決算が出ているあいだ、フェーズは**もう Surface**。`Space` を素通しにすると
`EndSurfacePhase` に届いて**地上フェーズを丸ごと飛ばして**ターンが終わる。
→ `AdvancePhaseByHotkey` の頭で決算と収穫の hold を横取りする。実測で確認済み。

## ついでに直した積み残し
**`RunStats.Escapes` が常に0**だった（前回までの「まだ触っていない数字」の1つ）。
`EmotionTreeManager.CountEscape()` は書いてあったのに**どこからも呼ばれていなかった**。
生還の唯一の通り道（`AdventurerAI.GrantReturnReward`）から呼ぶようにして、実測で 4 が入った。

## 次
**① 戦闘中に押す物**（B-1 狙え ＋ A-2 感情の刈り取り ＋ A-1 号令ゲージ）→ ⑤ → ⑥。

---

# 2026-08-30（続き3）　① 戦闘中に押す物 ― 完了（3本）

Gemini の5案と突き合わせて**採る3つを決め直した**うえで実装。
（Gemini案 5「罠の誘引」＋3「OVERLOAD」を1本に融合、4「見せしめ」は恐怖の伝播だけ採用、
1「緊急封鎖」は不採用、2「Bullet Time」は後回し。理由は下）

## 何が問題だったか（実測）
- 号令は DP300〜500・CD35〜70秒。**1波（20〜60秒）に1回撃てるかどうか**。5枠あるのに実質1枠。
- しかも **DPは余る**（2周とも資源を余らせて負けた）＝値段は制限として働いていない。
  効いている制限は**クールダウンだけ**。
- `RoomData.IsTargetable()` は**罠を目的地から外している**ので、作った殺戮部屋は素通りされる。
- `currentJoy`/`currentFear` は **`GrantReturnReward`（生きて帰ったとき）にしか清算されない**
  ＝倒すと丸ごと消える。盤の上に「まだ誰の物でもない報酬」が歩いていた。

## 1本目：`Decoy.cs` ― 誘引（おとり）→ 過負荷
戦闘中、**盤の罠をクリックする**。
- **誘引**：範囲8マスの冒険者が 5.5 秒そこへ向かう。⚠ **踏破目的の直行も上書きする**
  （魔王への一直線から引き剥がせる＝時間を買える）。⚠ 退却中には効かない。
- **過負荷**：その罠をいま爆発させる。半径2.4マスに罠ダメージ ×3 と状態異常。
  ⚠ 撃った罠は**その波のあいだ黙る**（`DisableTrapTemporarily`）。**これが対価**。
- 回数は **波あたり 誘引2・過負荷2**、共有クールダウン4秒。⚠ **DPは取らない**。
- 右クリックで誘引を挟まず直に過負荷。

実測：おとりで (6,6) の一団が (1,x) まで歩いてきて、過負荷1発で **4人が 100% → 31〜61%**。
既存の号令『落石』(350DP) と同じ役目を、**DPではなく「置いた罠」と「波あたりの回数」で払う**形。

## 2本目：`EmotionHarvest.cs` ― 感情の刈り取り
冒険者をクリックすると、溜まった `joy+fear` を**いま**清算して DP と感情に変える（溜めは0に戻す）。
- **早く刈る＝確実だが少ない／待つ＝増えるが、帰られても倒しても取り逃がす**という賭け。
- 見せしめ：刈った相手の**恐怖の半分**が周囲3.2マスに散る。
  ⚠⚠ Gemini案の「パニックで敵の火力が上がる」は**採らない**（押すと相手が強くなる＝掛け算の軸が増える）。
  散らすのは**既にある恐怖の値**だけで、増えた恐怖はそのまま次の実りになる ―― 新しい数字は0本。
- 回数は波あたり2、クールダウン4秒。

実測：同じ波の4人が 溜め 0 / 40 / 70 / 120 とばらけた（＝「誰をいつ」が本物の選択になっている）。
溜め120 で **+127 DP**。

## 3本目：`CommandCharge.cs` ― 号令ゲージ
撃破でゲージが溜まり、満ちたら **号令のクールダウンが全部戻る**（`ClearCooldowns`）。
- ⚠⚠ **1波に1回だけ。** 「倒す→撃てる→もっと倒せる」は正のループなので上限が要る。
  解放したその波では**もう溜まらない**。
- ⚠ 溜まりは**等級で重み**（頭数だけだと後半は勝手に満ちて、ただの時限ボーナスになる）。
- ⚠ 増えるのは**撃てる回数だけ**。威力にも値段にも実りにも触らない。

実測：落石(CD35秒)を撃った直後に解放 → CD 35 → 0。

## UI：⚔️ 戦闘中の手（号令バーのすぐ上）
`◆誘引 2　◆過負荷 2　◆刈り取り 2　[号令ゲージ 37%]　[解き放つ]`
⚠ **押す物は押す物の隣にまとめる**（散らすと結局どれも見つけてもらえない）。[Q] でも解放できる。

盤の上には
- **使える罠に菱形の印**（⚠ これが無いと「押す物がある」ことに気づけない ―― ②で学んだこと）
- おとりの輪（脈打つ）と**過負荷の巻き込み範囲**（見えないと「引きつける」判断ができない）
- 罠／冒険者に乗せると下部の帯に「クリックで何が起きるか」

## ⚠ 実測で踏んだもの
- **波が終わっても盤の印が消えなかった。** 描き直しは `Tick` の中でしか起きず、`Tick` は
  戦闘中しか回らないので、最後に描いた印が**準備フェーズに残り続けた**。→ `Decoy.EndWave()` を新設。
- 罠でないマスを押したときに理由を返すと、**盤のどこを押しても赤い通知が出る**（画面が「押すな」と
  言い続ける）。→ 押せる物が無いときは**黙って無視**。
- ゲージの上に文字を重ねたら、鮮やかな塗りで1文字目が読めなくなった。→ 塗りを沈んだ色に。

## Gemini案の判断（記録）
| | 判断 | 理由 |
|---|---|---|
| 5 罠の誘引 | 採用 | 私の3案に無かった穴を正確に突いていた |
| 3 OVERLOAD | 採用 | 「引きつけて…今だ！」の**待つ時間**が作れる。5とセットで1本に |
| 4 見せしめ | 半分採用 | 恐怖の伝播だけ。**パニックで敵の火力↑は掛け算の軸**なので落とす |
| 2 Bullet Time | 後回し | 良い案だが**解く問題が違う**（「押す時間が無い」ではなく「押す物が無い」）。①の枠として後で |
| 1 緊急封鎖 | 不採用 | `Excavation` は**戦闘中は掘れない**と明示的に拒否している（C-2を落とした判断と同じ）。
さらに実測で**10×10 の盤では経路操作が機能しない**。経路再計算で立ち往生する危険もある |

**B-1「狙え」は取り下げた。** 気性12種が既に `AimWindow` の中で狙いを決めており、そこに
プレイヤーの指定を差し込むと**自分で作った仕組みと喧嘩する**。配下15〜30体では集中射撃も見えにくい。
誘引の方が「群れが向きを変える」という**大きくて読める絵**になる。

## 次
⑤ 版図が増える瞬間 → ⑥ 時限つきの出来事。
そのあとで **通しプレイ T1〜T30**（②③④①が入った状態で、T10前後の壁を越えられるか）。

---

# 2026-08-30（続き4）　⑤ 版図が増える瞬間／⑥ 時限つきの出来事 ― 完了

これで**ドパミン設計の①〜⑥がすべて入った**。

## ⑤ 版図が増える瞬間 ― `ClaimFx.cs`（新規）

### 何が問題だったか
`SurfaceMap.SetOwner` は **真偽値を書き換えるだけ**だった。眷属を進軍させ、軍団を戦わせ、勝った ――
その結果が「次に盤を見たら色が変わっている」でしか伝わらない。
4Xで一番気持ちのいい瞬間（**版図が広がる**）が、丸ごと無音だった。

### 入れたもの
- `SetOwner`（**持ち主が変わる唯一の関所**）から `ClaimFx.Note` を1行。
- 盤の上に **1マスずつ 0.13秒ずらして**：
  - 取った → 緑の輪が広がり「**+23 DP/T**」（⚠ **その土地が何を産むか**を添える。
    数が増えただけでは値打ちが伝わらない）
  - 失った → 赤の輪と「奪われた」
  - ⚠ 同時に10マス光らせても「10増えた」とは読めないので、必ずずらす（見せる上限14／数えるのは全部）
- ④の**収穫の行に「版図 +3」を先に置く**。「何マス増えたか」は額より先に知りたい。
- ⚠ ターンの締めで奪ったぶんは `EndSurfacePhase` の解決中に起きるので、
  **④の収穫の間にそのまま流れる**（意図してそう並べた）。収穫が0でも版図が動いたなら画面を待たせる。

実測：4マス取得で 3マスに `+23 DP/T` が並び、`ClaimFx.Line()` が「版図 +4」を返した。

⚠ `SurfaceView.Flash` も **URPの罠**（既定マテリアルが `Sprite-Lit-Default` ＝真っ黒）を踏むので、
専用の不変色マテリアルを張ってある。⚠ 盤メッシュの `mat` は使い回さない（`mainTexture` を持っている）。

## ⑥ 時限つきの出来事 ― `Proclamation.cs`（新規）

### ⚠⚠ 何が問題だったか（S-1 のときの実測）
『次に起きること』の枠は作ってあるのに、**T8 の普通の状態で予定が0件**だった。
読み込める予定が **敵軍の集結・時代の終わり・牢・訓練・大招集の休み** ――
どれも**中盤以降にしか存在しない**ため。
Civ の「あと1ターン」が無いのは**表示の問題ではなく、序盤に予定された出来事が無いから**。

### 入れたもの：ギルドの布告
ギルドが「**何ターン後に何をするか**」を先に言う（T2から、2〜4ターン前に予告、一度に1件だけ）。
| 布告 | 効き目 | 軸 |
|---|---|---|
| 総力戦 | その波の人数 ×1.6 | 頻度 |
| ◯◯の隊 | その波の職がひとつに寄る（**備えが刺さる日**） | 構成 |
| 賞金首 | 野に在る『名のある者』が必ず出る（`Nemesis` の休みを無視） | 抽選 |
| 静穏 | 人数 ×0.55、ただし**世界の装備水準が上がる**（ギルドの支度） | 頻度＋既存の値 |

⚠⚠ **強さの掛け算は1本も足していない。** 変えるのは人数と顔ぶれだけで、
レベルにも装備にも係数を掛けない。すべて既にある操作口（`WaveRoster` の人数、
`RumorSystem` と同じ職の寄せ方、`Nemesis` の抽選、`LureEconomy` の装備水準）を使う。
⚠ **必ず先に言う。** 言わずに起きるのはただの理不尽で、言うから
**備え・流言・大招集・掘削が「その日に向けた準備」になる**。

さらに **`MutationSystem` の次の変異（T16／以後8ターンごと）** も『次に起きること』に出した ――
日付が決まっているのに、現れるまでどこにも出ていなかった。

実測：T2 に「戦士の隊が編まれる（3ターン後）」が出て、Foretell に `3T 布告『戦士の隊』` と並んだ。
効き目も確認 ―― 一色の隊で **7人全員が僧侶**／総力戦 7→11人／静穏 7→4人＋装備水準 18.75→21.75。

### ⚠ 踏みかけた罠
`WaveRoster.Roll` は **1ターンに何度も呼ばれる**（流言を撒くと引き直す）。
静穏の代償をそのまま置くと、**撒くたびに装備水準が上がる**＝押すほど損をする意味不明な罰になる。
→ ターン番号で1回だけに絞った。

## 次
**通しプレイ T1〜T30。** ①〜⑥が全部入った状態で、
- T10前後の壁を進言に従うだけで越えられるか（[[playthrough-wall-t11]] の宿題）
- 戦闘中に押す物（誘引/過負荷・刈り取り・号令ゲージ）が**実際に押したくなるか**
- 布告が「その日に向けた準備」を生むか

---

# 2026-08-30（続き5）　通しプレイ T1〜T14（①〜⑥入り）― 壁の正体が判明

生ログは `docs/playlog_full.md`（コミット `1978c99`）。

## 結果：**T14 で魔王が討たれた**（前2周は T11 / T13）
魔王HPは **T13 まで 100%**。壁は3ターンぶん後ろへ動いたが、越えてはいない。

| | 1周目 | 4周目 | **今回** |
|---|---|---|---|
| 決着 | T13 | T11 | **T14** |
| 余っていた物 | DP 40,210・素材 636 | DP 17,120・素材 281 | DP 552・**素材 341** |
| 撃破 | ― | ― | 106（逃した 64・凌いだ波 13） |

## ✅ ①〜⑥は効いた
- **戦闘中の手が T3 以降ほぼ毎波使われた**（誘引／過負荷／刈り取り／号令／号令ゲージ）。
  「戦闘中に押す物が無い」は解消。
- **T6・T16 の『総力戦』布告で来襲が 8〜10 → 14〜18 に跳ねた**＝⑥が実際に波を作った。
- **DPが毎波プラスとは限らなくなった**（T6 -49／T7 -202）。号令を撃つ判断が対価を持った。
  前回は「DPが余りっぱなし」だったので、これは狙いどおり。
- 決算の一言は毎回**正しい指摘**を出していた（「持ち逃げが多い」「配下の質に投資する頃合い」
  「もっと呼び込んでよい頃合い（大招集・流言）」）。

## ⚠⚠ 壁の正体：**時代が装備の上限を閉じている**
- 鍛造の上限を 3→4 に上げる唯一の研究 `r_grade_mithril` は **時代『伸長』のノード**。
- T14 時点の時代は **胎動 171/210・+5/ターン → あと 8 ターン（T22頃）**。
- つまり **T14〜T22 は、質を上げる口が閉じたまま敵だけが伸びる区間**。
  敵は想定 Lv26（T22には Lv40前後）・世界の装備水準 85。
  こちらは Lv14・**装備 w3/a3（上限）**・素材 341 と DP 552 が**使い道なく余る**。
- **前回の「資源を余らせて負けた」の正体はこれ。** 導線が細いのではなく、**時代で閉じている**。

## ⚠ このプレイの穴（自動プレイの限界）
`FeverSystem.CalledTurn = -1` ―― **大招集を一度も使わなかった**。
大招集は**時代の進みを×2**にするので、使っていれば『伸長』に4ターンほど早く着いた。
＝ **大招集は「人数を増やす手」であると同時に「時代を進めて装備の上限を開ける手」**。
決算が何度も「もっと呼び込んでよい頃合い」と言っていたのに押さなかった。
**進言も決算も正しいことを言っていた。押さなかったのはこちらの自動プレイ。**

## 次に考えること（数字を触る前に、まずここ）
1. **時代の進みが唯一の鍵になっているのは危うい。** 装備の上限を開ける道が
   「時代を待つ」だけなら、T14〜T22 は**何をしても変わらない8ターン**になる。
   別の口（素材を大量に払う／魔王の錬成ランク／遺物）を1つ足すか、
   `r_grade_mithril` を『胎動』に降ろすかの判断が要る。
2. **進言が上限に達したあと黙る。** 「装備を鍛える」と言い続けた末に上限へ着いたら、
   次は「時代を進めろ（大招集・偉業）」と言うべき。いまは何も言わない。
3. 地上を守らないと **版図 12 → 3** まで食われる（S-3 の討伐隊は設計どおり効いている）。
   ただし自動プレイが地上をほぼ操作していないので、これは実測として弱い。

## ⚠ 道具の罠（今回踏んだ）
`execute_code` に**上限の無い `while`** を書いて **Unity を固めた**（強制終了で復帰）。
`SquadAdd` は地上に出ている個体を拒否するので、`continue` で無限ループになった。
→ ループは必ず回数上限つき／失敗した候補は必ず外して進める。

---

# 2026-08-30（続き6）　鍛造の上限：**第2の口は既にあった**（＝情報の問題だった）

## ⚠⚠ 前回の私の診断を1つ訂正する
「装備の上限を開ける道が時代待ちだけなのは危うい → 第2の口を足す」と書いたが、
**第2の口は既に実装されていた** ―― `DemonLord.ForgeGradeBonus`（**錬成 B で +1／S で +2**）。
`MinionRoster.TryForge` も `EquipmentCatalog` の上限にこれを足している。

通しプレイでそこに気づけなかったのは、自動プレイが BP を `i % 4` で振っていて
**`Stat.Refine`（index 4）を一度も触らなかった**から。つまり**私の実験の穴**でもある。

ただし**もっと大事な事実**が出た ―― **どこにも書いていなかった**。
鍛造の枠は上限のとき「研究『ミスリル鍛造』」とだけ言い、
その研究が**時代『伸長』のノード**であることも、**錬成でも開く**ことも言わない。
＝ プレイヤーからは「時代を待つ以外に何もできない8ターン」に見える。**情報の問題だった。**

## 実測（測ってから直した）
| | 上限3（銀） | 上限4（ミスリル） | 上限5（アダマンタイト） |
|---|---|---|---|
| 力（攻×HP） | 2.13 | 3.18 | 4.83 |
| 上限3比 | ― | **1.49倍** | **2.27倍** |

錬成 B（rank3）は **BP 17**（2+5+10）＝ **3波ぶん**（+6BP/波）。
実際に上げたら上限が 3→4 に開き、全員 w4/a4 になって **素材 380 → 300（80消費）**。
＝ **余っていた素材がそのまま力に変わった。**

## 入れたもの（新しい仕掛けは足していない。**掛け算の軸は0本増**）
1. **`EquipmentCatalog.CapExplain()`** ―― 上限に当たっている理由と**開ける道を2本とも**1文に。
   実際の出力：
   > 魔王の『錬成』を B まで（+1段）　または　研究『ミスリル鍛造』**（時代『伸長の時代』が要る）**
   ⚠ 時代で閉じていることを**赤字で明示**する。待つしかないのか、いま動けるのかが分かれ目。
2. **`EraSystem.HasReached(era)`** ―― 研究ノードの時代が来ているかを1行で聞ける。
3. **鍛造の枠の表示**を「研究『◯◯』」から「**上限 ― 開き方あり**」に変え、
   ツールチップに `CapExplain()` を出す。
4. **進言に『魔王の錬成を上げて、鍛造の上限を開く』**（重み92）。
   置いた配下が**全員上限**かつ**素材60以上**のときだけ出す。必要BPと所持BPを数字で書く。
5. ⚠⚠ **「装備を鍛える」を上限のときは黙らせた。**
   実測で、全員が上限なのに **97 で1位に居座り続けていた** ―― つまり
   **できないことを1位で指し続けていた**。ここは黙るのではなく、上の④が引き継ぐ。

## 残る判断（数字の話。まだ触っていない）
上限5（錬成S・BP65）の先は `r_grade_orichal` で、それは**時代『終焉』**。
錬成を極めても cap 5 で止まる。T30 以降にそれで足りるかは、次の通しプレイで測る。

---

# 2026-08-30（続き7）　🪺 巣と 🌿 環境（生態系）― スポナーを置き換えた

## きっかけ：測ったらスポナーが強かった
同じT1・同じ大招集の波（来襲10）で比べた実測：

| | 撃破 | 逃した | DP差引 | 防衛体の損失 | 決着 |
|---|---|---|---|---|---|
| 罠14 ＋ 隊5〜6 | 9 | 1 | +637 | あり | 26秒 |
| **スポナー9 ＋ 隊5** | **15** | **0** | **+1,632** | **0** | **23.7秒** |

実り2.6倍。**なのに私も進言も一度も置かなかった。**
進言は「配置枠を埋める（罠・スポナー・トーテム）」と3つ並べるだけで、どれが効くか言わない。
＝ このセッションで何度も出た病気（**強い手が既にあるのに見えていない**）の4例目。

## なぜスポナーは浮いていたか
- 湧いた個体は**ロスターに載らない**＝育たない。鍛造も進化も気性も乗らない。
  この作品の芯（配下を育てる）と**1本も繋がっていなかった**。
- 置いた瞬間から**永久に5体/波**。判断は「置くか置かないか」の一度きり。**強いが浅い。**

## 入れたもの：強さを足さず、**素を下げて置き方で戻す**
### 🪺 巣（`FeatureType.Spawner` の意味を変えた。enum は触っていない）
- **素は 2体/波**（旧 5体/波）。
- **波をまたいで育つ** ―― 湧かせた子のうち**波末に生き残った数**が養分になり、
  閾値（6→14→…）で巣レベル +1（最大3・レベルごとに上限 +1）。
  ⚠⚠ これが「置いて終わり」を壊す1手。**湧かせた子が生き延びる盤**を作る動機になる。

### 🌿 環境（`HabitatCatalog` 新設・`FeatureType.Habitat` を末尾に追加）
巣の **2マス以内**に置くと効く。重ねがけは **2つまで**（トーテムと揃えた）。

| | 効き目 | 動かす既存の数 |
|---|---|---|
| 苔床 120DP | 湧きの間隔 −25% | `spawnerInterval` |
| 水源 160DP | 波あたりの上限 +2 | 波あたりの上限 |
| 餌場 180DP | 湧く個体のレベル +25% | `MinionRoster.LevelMult` に掛ける係数 |

⚠⚠ **環境も配置枠を食う**（罠・トーテム・隊とゼロサム）。実測：

| | 湧き | 間隔 | 使った枠 |
|---|---|---|---|
| 素の巣 | **2体/波** | 6.0秒 | 1 |
| ＋水源2 | **6体/波** | 6.0秒 | 3 |
| ＋苔床2 | 6体/波 | **3.4秒** | 5 |

旧スポナーは1枠で5体/波。**枠あたりの効率はむしろ下がっている。**
得をするのは**盤を広げて巣を環境で囲めた者だけ** ―― これが「広さの報酬」。
⚠ 掛け算の軸は増やしていない。動かすのは既にある3つの数だけ。

実測：波を1回凌いだだけで巣が Lv1→Lv2（6体/波 → **7体/波**）に育った。

### 進言（同じ病気を繰り返さないため）
- 「**巣を置く**」（枠を使っているのに巣が0・重み94）
- 「**巣の隣に環境を置く**」（巣はあるが環境が0・重み90）
⚠ 順番に意味がある。巣を置いた人にだけ環境の話をする。

## ⚠⚠ 踏んだ罠：`[SerializeField]` は**シーンに焼かれた古い値に負ける**
素を 5→2 に下げたのに、実測すると **5体/波のまま**だった。
`spawnerMaxPerWave` が `[SerializeField]` で、シーンに保存済みの 5 がコードの既定値を上書きしていた。
→ **バランスの決めごとは `const` にする**（インスペクタで触るノブではない）。
古い値が飛ばないよう、旧フィールドは読まずに残してある。

## ⚠ 罠（2回目）：ヒアドキュメントで `\n` が実際の改行に化ける
C# の文字列に `\n` を書くときは **Edit ツールを使う**（→ [[tooling-traps]]）。今回また2箇所で踏んだ。

## 次
通しプレイ3周目。見るのは「巣と環境に枠を割く判断が生まれるか」「T13の壁が動くか」。

---

# 2026-08-30（続き8）　Gemini の8案の採否と、今後の計画（W → X → Y → Z）

⚠ この節は**会話にしか無かった設計判断**を残すためのもの。実装はまだ W-3（生態系）まで。

## 判断の土台になった実測（これを外すと全部ずれる）
### ① 同一階層を1段（+10マス）広げると何が起きるか
| 増える | 増えない |
|---|---|
| 配置枠 **+4**（罠・トーテム・スポナー・環境） | **`SquadMaxSlots`（＝恒久で育つ頭数）** |
| 経路の長さ（滞在時間） | ― |
| **敵の人数**（`RenownBonusAdventurers`：2段ごと +1人） | ― |
| **敵の質**（`RenownHeroRankBias`：段 × 0.06） | ― |

`SquadMaxSlots` は **5＋研究(m_slot/m_slot2)＋政策＋属性** のみ。**面積にも階層の広さにも連動しない。**
＝ 広げて増えるのは**使い捨ての頭数（巣）と罠・環境の枠**だけ。拡張すると**配置は全部クリア**（50%返金）。

### ② 通しプレイ2周（①〜⑥入り）
| | 1周目 | 2周目（錬成優先＋大招集） |
|---|---|---|
| 決着 | T14 | **T13** |
| 撃破 | 106 | **175** |
| 鍛造の上限 | 3（最後まで） | **4（T4から）** |
| 余った素材／DP | 341／552 | **868／7,284** |
| 死んだ瞬間 | ― | 来襲**42人** 対 **配下6体**・階層1 |

⚠ **どちらも自動プレイの穴が混じっている**（巣を1つも置かず・階層を拡張せず）。
ただし「呼ぶ人数は青天井／捌く用意は自分で作る必要がある」という形は盤の数字がそのまま示している。

## Gemini の8案 ― 採否
| 案 | 判断 | 理由 |
|---|---|---|
| 面積3 **生態系** | ✅ **採用（実装済み）** | 「自動資源生産」は軸が増えるので不採用。だが「**魔物が湧く**」だけは足りない側（頭数）そのもの。→ 🪺巣と🌿環境 |
| 面積1 複数ルート＋維持コスト | ⭐採る（形を変えて） | 維持コストは**足さない**（DP 7,284 が余る＝コストは制限にならない）。足すのは逆側 |
| 面積2 巨大施設（5x5） | ⭐採る（W の後） | 「広げる理由」として素直。⚠ 10×10 では置けない＝**広げた者にだけ見える報酬**として正しい |
| 視点3 冒険者のメタ編成 | ○ 採る（Y） | `RumorSystem`/`Proclamation`/`WardSystem` の鉄道がある。安い。⚠ いま入れると死ぬのが早まるだけ |
| 視点2 地上⇄地下の地形同期 | △ 保留（Y） | 良い案だが **10×10 では成立しない**（溶岩で階層が丸ごと消える）。面積が意味を持ってから |
| 視点1 不可逆ビルド（カルマ） | △ 保留（Z） | 「ビルドの平坦化」は**終盤の問題**で、まだ終盤に行っていない。`DemonLordRaceTree`(16種族) と研究の**排他グループ**が既に部分実装 |
| 面積4 迷子・補給切れ（SAN/松明） | △ 保留（X の後） | 冒険者に新しいステータスを1本足す。**10×10 では迷いようがない**。面積が20以上になってから再検討 |
| 視点4 最終決戦フェーズ | △ 保留（Z） | **T13 で終わるゲームに終幕は作れない**。T25 に届いてから |

⚠ Gemini の事実誤認：迷宮を「50×50」と書いているが、**開始は 10×10**（上限50）。
これが 視点2 と 面積2 の成否を分ける。

## 計画
| 段 | やること | 条件 |
|---|---|---|
| **W-0** | ✅ 測る（スポナーの実力） | 済 ― 罠より圧倒的に強かった |
| **W-3** | ✅ **生態系**（🪺巣と🌿環境） | 済（`c5a8323`） |
| **W-1** | 拡張の**取引を見せる** ―「枠+4／経路×2 ／ **敵+1人・質+6%**」を両方書く | 次 |
| **W-2** | 進言と決算が「**捌く用意**」を言う（大招集の見込みに「いまの守りで捌けるか」） | 次 |
| **W-4** | 通しプレイ3周目 ― 巣と環境に枠を割く判断が生まれるか／T13 の壁が動くか | |
| **X** | 巨大施設・複数ルート | 広い盤が普通になってから |
| **Y** | メタ編成・地形同期 | 同上 |
| **Z** | 最終決戦・不可逆ビルド | **T25 に届いてから** |

## ⚠⚠ このセッションで私が外した診断（4回）― 同じ轍を踏まない
1. 「壁は**時代**が装備の上限を閉じているから」→ **第2の口（魔王の錬成）が最初からあった**。
   気づけなかったのは自動プレイが BP を `i % 4` で振って `Stat.Refine`(index 4) を触らなかったから。
2. 「上限を開ければ壁が動く」→ 開けても **T13 で落ちた**。素材868・DP7,284 が余った。
3. 「捌く頭数は階層数で固定」→ **スポナーが面積で伸びる口だった**。
4. （その前）「進言が上限のあと黙る」→ 黙るどころか**できないことを97で1位に指し続けていた**。

**共通の原因はいつも同じ：強い手が既にあるのに、画面のどこにも書いていない。**
②〜⑥／鍛造の上限／大招集／スポナー ―― 4例とも同じ病気だった。
**「足りない」と思ったら、まず「既にあるが見えていないだけではないか」を疑う。**

---

## 2026-08-31　W-1 / W-2（どちらも「見せる」だけ・実装0本の新しい数字）

計画 W→X→Y→Z の 2件（→ `roadmap-wxyz`）。**新しい強さも倍率も足していない。**
どちらも「既にゲームの中で起きていることを、判断する場所に書く」だけ。

### W-1　階層拡張の**取引**を両側に書いた
これまで拡張ボタンの横にあったのは **「(枠+4)」と値段だけ**。実際にはこの1段で
**名声が上がり、来る冒険者の人数（2段ごと+1人）と質（+6%/段）が増え**、さらに
**その階の配置が全部クリアされる**（50%返金）。＝ 払う側が3つあって1つも書いていなかった。

- `DungeonFloorManager.ExpandGainLine(i)` / `ExpandCostLine(i)` を新設。
  ⚠ 数字は作っていない。`RenownBonusAdventurers` / `RenownHeroRankBias` / `TryExpandFloor` の
  返金処理を**そのまま言葉にしただけ**。
- 行を2段に（上＝得と値段／下＝赤で代償）。人数が増えない段では「（次の段で人数+1人）」と書く。
- パネルは**中身に合わせて畳む**（1〜2層のとき下が大きく空いて「作りかけ」に見えた）。
- 見出しも「客が増える」→「**来る冒険者の人数と質が増える**」に。

### W-2　進言と決算が「**捌く用意**」を言う
`◆ 大招集` は「呼べる」と「旨い」は出ていたが、**「いまの守りで捌けるか」がどこにも無かった**。

⚠⚠ **強さを式で予想していない。** 攻撃力を足し合わせた「防衛力」を作るのは
掛け算の軸を1本増やすのと同じで、しかも当たらない。代わりに
**プレイヤー自身の戦績**（`RunStats.BestWaveHeld` ＝ **一人も通さず**凌いだ波の最大来襲人数）を
覚えておき、見込み人数と並べる。予想ではなく事実なので外れない。

- `RunStats` に `BestWaveHeld` / `BiggestWaveSurvived` / `LastWave*` と `NoteWaveOutcome`。
  積むのは `WaveReport.EndWave` の1か所（`Flawless` が確定した後でないと正しくない）。
- `FeverSystem.ReadinessOf/ReadinessLine` ＝ 三段階（緑 内側／橙 超える／赤 危ない・1.5倍が境）。
- 出る場所は3つ：**大招集ボタン**（危ないときは `!` と暗い色＋ツールチップ）／
  **進言**（危ないときは見出しを「守りを厚くしてから」に変え、重みも 90→62 に落とす）／
  **決算の『次の備え』**（最後の行＝次の準備フェーズへの唯一の持ち帰り）。
- ⚠ **禁止はしない。** 危なくても押せる（賭けを取り上げない）。見せるだけ。
- ⚠ 休み中は「切れば」と書かない（押せない手を勧めない）。「あと N ターン休み」に切り替える。

### 途中で直した細かい2件
- 進言の見出しが「配置枠を埋める（罠・**スポナー**・トーテム）」のままだった。
  画面のツールは 🪺巣 / 🌿環境 なので、**進言と画面で名が違って**いた → 「罠・巣・トーテム」に。
- 「無傷で捌いた最大」と書いたら、決算の見出し「**無傷**で凌いだ」と画面上で矛盾した
  （見出しの無傷＝魔王と防衛体、こちらは**逃走0**まで含む別条件）。→ 「**一人も通さず**凌いだ最大」に統一。

### 検証（エディタ実機）
- コンパイルエラー 0（警告は既存の UAC1009 3件のみ）。
- 拡張パネル：全TMPの描画文字数を実測、**0文字の行なし**。見出しは 594/676px で1行に収まる。
- 決算パネル：`次の備え` 2行を含め **0文字の行なし**。行数に合わせて畳む位置も追従。
- 三段階の出し分けを `BestWaveHeld` = 0 / 3 / 9 / 14（見込み10体）で確認 →
  Risky（`◆ 大招集 !`）／Risky／Tight／Fine。
- 実際に1波回して `RunStats` が積まれることを確認（来襲4／撃破4／逃走1 → 逃走ありなので `BestWaveHeld` は据え置き＝設計どおり）。

### 次
**W-4 通しプレイ3周目**。見るのは2つ ―― ①巣と環境に配置枠を割く判断が生まれるか
②T13の壁が動くか。今回のW-1/W-2は**選択の材料を増やしただけ**なので、
壁が動かなければ原因は材料不足ではない（＝次はXへ）。

---

## 2026-08-31（続き）　W-4 通しプレイ3周目 ― **壁は動かなかった（T14）**

生ログ：`docs/playlog_run3.md`。今回は記録どおり**進言に従わせた**
（自動運転は `Assets/Scripts/_AutoPlayHarness.cs`。毎ターン進言を重み順に実行し、
**実行できなかった進言も全部記録する**＝ゲーム側の穴とボット側の穴を分けるため）。

### 効いたこと（W-1/W-2 は設計どおり動いた）
- **巣と環境が初めて置かれた**（T2）。前2周は最後まで0個だった。進言 94/90 が効いた。
- **大招集が1度も出なかった。** 守りが薄い（12/14枠・巣1・配下2）ので readiness が Risky、
  進言の重みが 90→62 に落ちて**上位3件から押し出された**。W-2 は無謀な賭けを止めた。

### ⚠⚠ それでも同じターンで死んだ ―― **止めた手は経済の本体でもあった**
大招集は撃破・DP・RP・**時代の進行**の主エンジン。切らなくなった結果、
時代は **Dawn のまま**・RP 65・撃破 71（2周目の 40%）。
一方で逃走 55 が積み上がり**世界の装備水準が 57.3**まで走り、配下2体では捌けなくなった。
＝ **無謀な死を、貧しい死に置き換えただけ。** 決着は T13/T14 → **T14**。

### ⚠⚠⚠ 3周に共通する本体：**T4 以降、盤が一度も大きくならない**
階層は3周とも **1のまま**（追加も拡張も0回）。配置枠は **T4 で 12/14 に達したあと不動**。
配下の数は **2〜6体**のまま、波は 4人 → 19人。

原因は**進言の側**にある（実測）：
- **拡張・階層追加の進言が存在しない**（見出し34件に1つも無い）。
- **「配下を召喚して数を増やす」は weight 55 ＝ 最下位**。しかも進言は
  **上位3件しか出ない**（`GuideSystem.Build` が3件で打ち切る）。競合は 76〜99 なので、
  **14ターン通して一度も画面に出なかった**（出現0回）。
- ＝ 2周目の診断「**呼ぶ人数は青天井、捌く頭数は誰も勧めない**」が**そのまま残っている**。
  W-1/W-2 は**選ぶ材料**を足したが、**選択肢そのもの**は足していない。

### ⚠ W-2 の指標の弱点（自分で作った物の穴）
`RunStats.BestWaveHeld` は **T4 の 7 から最後まで動かなかった**。
逃走が常態になると「一人も通さず」が二度と成立せず、readiness が**永久に Risky に貼り付く**。
＝ 一度荒れると「もう大招集はするな」と言い続ける**片道の指標**。要見直し。

### 途中で踏んだ罠
- **未回答の『異変』があると `StartBattlePhase` が黙って return する。** 8分間ずっと
  同じターンを回し続けた。→ ハーネスに異変への回答と、**3分固まったら理由を書いて止まる見張り**を入れた。
- **進言は上位3件しか出ない。** その3件が全部「今できない手」だと、DP 519 を持ったまま
  1ターン何もしないことが起きる（実測 T3）。

### 次にやること（W は終わり。ここからは**選択肢を足す**側）
1. **進言に「盤を大きくする」を入れる** ― 拡張／階層追加／召喚。
   ⚠ ただし weight を上げるだけでは 3件枠の押し合いになる。**枠の数か、出し方**を変える必要がある。
2. **大招集を止めたら経済が止まる**という結合を解く（時代・RP の口が大招集しか無い）。
3. `BestWaveHeld` を片道でなくする（例：直近N波の実績で見る）。

---

## 2026-08-31（続き2）　①進言の成長枠／②大招集と経済の分離／③片道の物差しの是正

生ログ：`docs/playlog_run4.md` `run5.md` `run6.md`（3周走らせた）。

### 先に W-3（生態系）の実動作を確認した
素の巣 **2体/波・6.0秒** → 水源2で **6体/波** → 苔床2で **3.4秒**。
1波回して **6体湧いて5体生存 → 養分 5/6**（次の波で Lv2）。**設計どおり動いている。**
3周目で効かなかったのは仕組みではなく、**巣1・環境1（種類はランダム）しか置かれなかった**から。

### ③ 片道の物差しを直した（`RunStats.HeldRecently` / `FeverSystem.Held`）
`BestWaveHeld`（一人も通さず凌いだ最大）は逃走が常態化すると二度と更新されず、
3周目は T4 の 7 から T14 まで動かなかった。→ **直近5波で実際に捌いた最大人数**に差し替え。
完璧さを要求せず、窓で見るので**下がりもする**（実測 13→9→14）。

### ② 研究点を大招集から切り離した（`FeverSystem.BaseKillsPerRp = 6`）
⚠ 当初「時代が Dawn のまま＝結合」と書いたのは誤り。時代は 210/(+5/turn) ≒ 42ターンかかる設計で、
T14 で Dawn は正常。**本物の結合は研究点**だった ―― RPは大招集を切ったときだけ入る作りで、
3周目は撃破71に対し**戦闘由来のRPが0**。RPは唯一の欠乏資源なので、これは経済が止まるのと同じ。
→ 撃破からの基礎RPを大招集の外に出し、大招集側を 3→6 に下げた。
**大招集を切った波の合計は従来と同額**（1/6＋1/6＝1/3）。倍率の軸は増やしていない。

### ① 進言に「成長枠」を作った（`Advice.grow` ＋ 3枠のうち1枠を予約）
重みを上げるだけでは 76〜99 の渋滞に負けるので、**枠の割り当て**で解いた。
召喚は weight 55 固定 → **数負けしている人数**で 62〜92 に。拡張・階層追加の進言も新設。

### ⚠⚠⚠ 測った結論：**いまの作りでは「広げる」ほど早く死ぬ**
| 周 | 入れた物 | 拡張の進言 | 決着 | 撃破 |
|---|---|---|---|---|
| 3 | W-1/W-2 | 存在しない | T14 | 71 |
| **4** | ＋成長枠・基礎RP・直近の物差し | **出なかった**（重みで負けた） | **T15** | **113** |
| 5 | ＋拡張の声を大きく | 毎回通した | T12 | 69 |
| 6 | ＋「器を満たしてから」の門 | 門つきで通した | **T11** | 74 |

理由は3つとも実測できている：
1. **拡張はその階の配置を全部消す**（50%返金）。5周目 T12 は **枠1/54・巣0**、
   6周目 T6 は 14/14 → **7/18** になり次の波で魔王HP 32%。
2. **階を足すと空の階ができ、素通りされる。** 5周目 T7 は「**来襲10・撃破0・逃10**」。
3. 広げるDPは**置く物に使えたDP**。10×10 では器より**中身**が足りない。

→ **3周目の診断は「観察としては正しく、原因としては誤り」だった。**
  「T4以降、盤が大きくならない」は事実だが、**大きくすれば良くなるわけではなかった**。
  広げられなかったのではなく、**いまの値段と仕様では広げるのが損**。

→ 拡張・階層追加の進言は**声を小さいまま残した**（本当に満杯で豊かなときだけ拾われる）。
  ⚠ **大きくしたくなったら、先に「拡張が配置を消す」仕様を直すこと。** これが X の前提条件。

### いまの到達点
**4周目の構成（③＋②＋成長枠、拡張は静か）が最良：T15・撃破113・RP81・配下4・装備水準37.5。**
コードはその構成に戻してある。

### 次
1. **拡張が配置を全部消すのをやめる**（X の前提）。置き直しの手間ではなく、
   広げた瞬間に盤が空になることが問題。返金ではなく**そのまま残す**か、自動で置き直す。
2. 空の階ができる問題（階を足した直後）も同じ根。
3. そのあとで初めて、拡張の進言を上げてよい。

---

## 2026-08-31（続き3）　X-1 巨大施設（5×5）― 広さを「頭数」に変える

### 先に前提を直した：拡張が配置を全部消すのをやめた（`2ef0ffc`）
`DungeonFeatureManager.RestoreAfterResize` を新設。`TryExpandFloor` は
「返金して捨てる」→「**退避 → 地形を作り直す → 戻す**」になった。
同じマスが壁になったら**近い床へずらす**（6マスまで）。戻せなかった物だけ返金。
⚠ **順番が意味を持つ**：巣とトーテム（範囲で効く錨）を先に据え、環境はそのあと。
環境の元の場所が使えないときは**仕えていた巣のそば**へ寄せる。
実測：巣1(水源×2)＋罠4 の7件で 10×10→20×20 → **7/7 引き継ぎ**（以前は 0/7）。

### ⚠⚠ 一辺は 5×5。**4×4 は門にならなかった**
各サイズで迷宮を **40回生成**して、空いた正方形が1か所でも取れる確率を測った：

| 盤 | 3×3 | 4×4 | **5×5** | 6×6 |
|---|---|---|---|---|
| 10×10 | 100% | **42%** | **0%** | 0% |
| 20×20 | 100% | 100% | **57%** | 30% |
| 30×30 | 100% | 100% | **95%** | 62% |

最初 4×4 にしたのは**1回しか生成を見ていなかったから**（たまたま0か所の盤を引いた）。
⚠ **確率は複数回まわして測る。**
20×20 でも 57%＝取れないことがあるのは意図的で、そのときは『掘る』で自分で空間を作る
（面積・地形工事・巨大施設が1本に繋がる）。

### 中身：2種（末尾に足せる）
- **練兵場**（900DP）：その階の**隊の枠 +1**。
  ⚠⚠ **面積が頭数に繋がる唯一の道。** `SquadMaxSlots` は研究・政策・属性でしか伸びず、
  **面積にも階層数にも一切連動していなかった**。一方、7周の通しプレイで壁を動かしたのは
  恒久的な頭数だけ（配下 2→7 で T14→T17）。＝広げても勝ちに繋がらないから誰も広げなかった。
- **大巣**（1200DP）：素 **4体/波**（ふつうの巣は2）・環境が **3マス**先まで届く。
  実測：水源2つで **8体/波**。

⚠ 倍率の軸は**1本も増えていない**（枠の数・湧く数・届く距離だけ）。

### 作りの決めごと
- 実体は**左下の1マス**にだけ登録し、残り24マスは `GreatWorkCovers` で塞ぐ。
  16〜25個の Feature を作ると配置枠も返金も撤去も全部その倍数になっておかしくなる。
- **配置枠は1つしか食わない**（敷地は広いが判断は1つ）。
- ⚠ 環境との距離は**敷地のいちばん近い辺から**測る。左下セルから測ると 5×5 では
  対角が8マス先になり、**角にしか環境を置けない**（盤の見た目と効きが食い違う）。
- 印は**敷地ぜんぶを塗る**。1マスの印だと残り24マスが見えない壁になる。
- 置けない階では、ストリップに**理由をその場に書く**（「反応しないツール」に見せない）。

### 検証（エディタ実機）
- 10×10：建てられない（0%）→ 1段拡張で建てられる。
- 練兵場を建てて **隊枠 5 → 6**。敷地内には他の物を置けない。枠は 1/18 しか食わない。
- 大巣 **4体/波** → 辺から3マス以内に水源2つで **8体/波**、環境側も「巣 1 に効いている」。
- 拡張しても巨大施設は残り、隊枠 6 のまま。
- 進言に「B1F に『練兵場』を建てる」が weight 93 の成長枠として出た。
- ⚠ ツールが1つ増えて帯が溢れ「トーテム」が2行に折れたので、幅と余白を詰めて折り返しを禁止した。

### 次
**通しプレイで「広げる → 練兵場 → 隊枠」の道が実際に通るか**を測る。
7周目までは拡張が一度も起きなかったので、そこが動くかが焦点。

---

## 2026-08-31（続き4）　🎧 音の土台（ファイル差し替え・声・曲の淡い入れ替え）

ElevenLabs を MCP で繋いだので、**音が届いたら鳴る土台**を先に入れた。
⚠ MCP は**起動時にだけ読み込まれる**ので、生成そのものは Claude Code の再起動後。

### 作り
- **`AudioAssets`（新規）＝音の目録。** 効果音17・BGM3・声6を、
  「id／いつ鳴るか／生成の指示／目安の長さ」で1枚に並べた。ここは名前と説明だけを持つ。
- **`SoundSystem` はファイルを先に探し、無ければ手続き生成に落ちる。**
  ⚠⚠ これが肝。目録が埋まっていなくても**無音にならない**し、1本置くごとに
  **その音だけ**が本物に差し替わる（全部揃うまで待たなくてよい）。
  ⚠ 見つからなかった id も覚える（毎回 `Resources.Load` を叩くと、
  置いていない音ほど重くなる＝いちばん多い経路がいちばん遅い）。
- **🗣️ 声は専用の口**（`voiceSrc` ＋ `VoiceVolume`）。効果音に混ざって切られない。
  ⚠ 前の台詞は止める（重なると何を言っているか分からない）。
  ⚠ 無ければ**黙る**（手続き生成に落とさない。合成音声の代わりはビープでは務まらない）。
  ⚠⚠ **動く文章は喋らせない。** 進言の本文はターンごとに変わるので読み上げが終わらない。
  何度も来る決まった場面（侵略開始／大招集／敗北 …）だけを置く。
- **🎵 曲は2本のAudioSourceで淡く入れ替える**（`SoundSystemTicker`）。
  ⚠ 準備→戦闘→地上は1ターンに3回切り替わるので、ぶつ切りだと
  **曲が付いた瞬間に前より安っぽくなる**。⚠ `unscaledDeltaTime`（4倍速で曲が飛ばない）。
- 設定画面に「声」のスライダーを追加（声だけ切りたい人が必ずいる）。
- 置き場所：`Assets/Resources/Audio/{Sfx,Bgm,Voice}/<id>.mp3`（README 同梱）。
  ⚠ `Resources` の外に置くと**ビルドに入らない**。

### 検証（エディタ実機）
AudioSource 5本（SE/BGM/声/曲A/曲B）＋Ticker が立ち、
ファイル0本の状態で 効果音・手続きBGM は鳴り、声は黙る。目録 17/3/6 を認識。

### 次
Claude Code を再起動して ElevenLabs の MCP を読み込み、目録の順に生成して置く。
効果音 → BGM3本 → 声、の順（効果音は1本ずつ差し替えても壊れない）。

---

## 2026-08-31（続き5）　🎧 音を実際に入れた ＋ ✨ 買ったVFXを繋いだ

### 音（ElevenLabs）
- **効果音 17本**（`click` 〜 `save`）を生成し、目録の id にリネームして `Resources/Audio/Sfx/` へ。
- **声 6本**（腹心の一言）を `eleven_multilingual_v2` / 日本語 / Matilda で生成 → `Voice/`。
- ⚠⚠ **曲は作れなかった。** Music API は **有料プラン専用**（`402 paid_plan_required`）。
  → 代わりに**環境音のベッド**を効果音APIの**5秒ループ**で3本作り、`Audio/Amb/` に置いた。
  ⚠ これは曲の**置き換えではなく別の層**。曲＝気分／ベッド＝その場所に居る感じ。
  手続き生成BGMの**下に敷く**専用 AudioSource を足した（本物の曲が来ても外さなくてよい）。
- 消費：**声の 175 文字だけ**（効果音は文字数枠を消費しない）。残 9,825/10,000。

### VFX（Eric VFX Studio・5種）
- URP の `Particles/Unlit` を使っていたので**そのまま出せた**（真っ黒・マゼンタにならない良い方）。
- `Resources/Fx/` に `fx_hit` `fx_explosion` `fx_burst` `fx_circle` `fx_slash` としてコピー。
- **`FxPrefabs`（新規）＝音と同じ「あれば使う」型**。無ければ何もしない＝
  既存の手続き演出（`FxSprite` / `BattleVfx`）がそのまま残る。段は3つ：
  **買ったパーティクル → 自作スプライト → 手続きの円**。
- 繋いだ場所：撃破（`fx_explosion`／因縁は `fx_burst`）・召喚（`fx_circle`）・
  号令（`fx_slash`・魔王の位置から）・過負荷（`fx_burst`）。

### ⚠ 2Dの盤に3Dのパーティクルを出すときの決まり（`FxPrefabs` に実装）
1. **`sortingOrder` を必ず設定する**（420＝配下より手前・UIより奥）。やらないとタイルの裏に隠れる。
2. z を少し手前に出す。
3. **使い終わったら消す**（`ParticleSystem` は放っておくと盤に残り続ける）。
4. ⚠ プールしない（`Clear()` の抜けで前の粒が残る事故の方が高くつく）。
5. ⚠⚠ **倍率は盤の1マス基準で決める。** 素の大きさだと**2マス分を覆って**何が起きたか読めない
   （スクリーンショットで確認して 1.3→0.85／0.85→0.5 などに縮めた）。

### 検証（エディタ実機）
効果音 17/17・環境音 3/3・声 6/6・VFX 5/5 を読み込み。
再生して AudioSource 6本（SE/手続きBGM/声/曲A/曲B/環境音）が期待どおり動作。
VFX は URP シェーダで `sortingOrder=420`、盤の手前に正しく描画されることを画面で確認。

### 次
- 曲（本物のBGM 3本）は有料プランが要る。ベッドだけでも空気は出るが、曲は別途。
- 通しプレイで「広げる → 練兵場 → 隊枠」の道が通るかの検証（X-1 の積み残し）。

---

## 2026-08-31（続き6）　⚔️ 攻撃のエフェクト（種類ごとに描き分ける）

### なぜ要ったか
演出は**倒したときだけ**だった。1体倒すまでに十数回殴り合うので、
**戦闘時間の大半に手応えが無い**。しかも盗賊の刺突も戦士のなぎ払いも術者の魔法も、
画面上では**まったく同じ「何も起きない」**だった。

### 分け方は2軸（`AttackFx` 新規）
① **形**＝何をしたか　② **色**＝誰の何か。
⚠⚠ 形は少数でよい。**16属性ぶんの絵を作るのではなく、1つの形を属性色で染める。**

| 攻撃 | 形 | 色 |
|---|---|---|
| なぎ払い（戦士） | 三日月 | 鋼 |
| 刺突（盗賊） | 細い菱形・速い | 紅 |
| 打撃（鈍器・素手） | 太い輪・遅い | 鈍い金／灰 |
| 爪（配下の物理） | 3本の裂傷 | 紅 |
| 魔法（術者・配下・僧侶） | 弾ける輪 | **属性16色** |

繋いだ場所：冒険者4職の攻撃・**魔王への一撃**（職ごとに形を変える）・配下の攻撃。

### ⚠⚠ 買ったパーティクルは「色が意味を持つ演出」には使えない
`Color over Lifetime` を持っているので、`startColor` を差し替えても**元の色で出る**
（実測：火炎も氷結も同じ金色になった）。
さらに `startColor` が**グラデーション設定**だと `.color` への代入が**黙って無視される**
（`mode = ParticleSystemGradientMode.Color` を先に立てる必要がある）。

→ **決まり：色が固定でよい演出だけ買った素材、色が意味そのものの演出は手続きで描く。**
　　斬撃＝買ったパーティクル（鋼の武器で固定）／魔法＝手続き（属性16色）。

### 検証（エディタ実機）
5種を盤に並べて撮影し、**刺突＝桃の菱形／打撃＝輪／爪＝赤い3本線／斬撃＝金の三日月**が
別物として読めることを確認。属性色は `startColor` を読み返して
火炎 `E8622E`／氷結 `7FD3E6`／鋼 `D1E0FF` が乗ることを確認。

### 次
PixelLab で「刺突」「爪」のスプライトシートを作れば、手続きの形をそのまま差し替えられる
（`PrefabFor` に1行足すだけ）。⚠ ただし**色が意味を持つ魔法は手続きのまま**にすること。

---

## 2026-08-31（続き7）　🗡️🐾 刺突と爪をスプライトシート化（PixelLab・計4生成）

### 手順（`pixellab-pipeline` の記録どおり）
`create_map_object`（1生成・64×64・透明背景）→ `animate_image`（1生成・8コマ指定＝**出力9コマ**）
→ `.../images/<job_id>/download?index=N` で連番を落とす。**2種で計4生成**。

### ⚠⚠ 踏んだ罠（次も踏む）
1. **1回目は空っぽの絵が返った。** 「白・無彩色」と指定したうえに形の指示が抽象的で、
   モデルに描くものが無くなった。→ **太く・画面いっぱい・高コントラスト**＋色を指定して振り直し。
2. **PPU16 は配下の小さな絵（14×21）用の設定。** 64px の演出に使うと **1枚が4マス分**になる。
   演出は **PPU64（64px＝1マス）**。
3. ⚠⚠ **生成物は濃紺だった。** 暗い色に `SpriteRenderer.color`（乗算）を掛けると
   **さらに暗くなって盤に溶ける**。→ **輝度をアルファに移して白マスクに変換**した
   （`a = a × 輝度 × 1.55`、rgb は白）。これで陣営色にも属性色にも染まる。
4. **PixelLab の絵は斜め（右上向き）に描かれてくる。** そのまま攻撃方向へ回すと45度ずれるので
   `FrameAngleOffset` で打ち消す。
5. ⚠ **短い演出はスクリーンショットでは捕まらない。** 撮影は呼び出しから 0.2〜1秒遅れて走るので、
   0.16〜0.26秒の演出は写らない（長寿命のパーティクルだけ写る）。
   **見た目は「アニメを止めた検分用オブジェクト」で確かめ、動作は数を数えて確かめる。**

### 作り
`AttackFx` に**コマ送りの層**を足した（`Resources/Fx/atk_<kind>/<n>.png`・0始まり）。
⚠ `MinionAnim` と同じ「読めるところまで読む」方式なので、**作りかけでも壊れない**。
⚠ コマ送りがあるときは**形をいじらない**（絵が動きを持っているので、伸縮させると二重に動く）。

### 検証（エディタ実機）
- コマ数 9/9、大きさ 刺突1.25マス・爪1.15マス、向き 315度（＝0度の攻撃に −45度補正が乗った値）。
- 拡大して静止させた検分では、刺突がコマごとに細り、爪が薄れていくのを確認。
- **フックが実際に動くことを確認**：冒険者(Mage)の攻撃を直接呼ぶと `Magic` が1つ、
  配下を射程内に寄せて攻撃を呼ぶと `Claw` が1つ生成された。

### 残り
- 魔法は**手続きのまま**にすること（属性16色を色で表しているので、絵に焼き込むと表現できなくなる）。
- 打撃（Blunt）は手続きのまま。必要なら同じ手順で1種2生成。

---

## 2026-08-31（続き8）　🎮 体験の診断 ―― 「壁」より前に、**世界の9割に出会えていない**

⚠ ここまでの診断は**自動運転の成績**を見て立てていた。人が遊ぶ前提で数え直した。

### 測った事実（ボットの巧拙と無関係な、構造の数字）

| 作ってある物 | 総量 | 1周で触れる量 |
|---|---|---|
| 研究ノード | **235枚**（全部で 4,929 RP） | 1周のRPは **95** → 安い順でも **20枚＝8%** |
| 配下 | **46種** | 初期解禁 **7種＝15%** |
| 時代 | 1つ進むのに **42ターン**（自然進行 +5/T・Need 210） | 決着が T14〜17 ＝ **一度も変わらない** |
| 属性・トーテム・遺物・政策 | 16 / 13 / 16 / 20 | 大半が視界に入らない |

### 敵と味方の伸び方（これが壁の正体）

| | T1 | T15 | T20 | T40 |
|---|---|---|---|---|
| 冒険者の想定Lv | 2 | 12 | 16 | **31** |
| 波の人数 | 4 | 18 | 20 | 20（頭打ち） |

味方の**伸びしろ自体は十分ある**：鍛造は最大13段・配下Lv上限50・隊枠は最大12（練兵場込み）。
⚠⚠ **問題は伸びしろの大きさではなく、そこへ行く鍵が1つしかないこと。**

### 🎯 根っこ（1つ）
**成長の鍵が研究点(RP)に一極集中しているのに、RPの供給が中身の量の 1/50 しかない。**
- 敵は**RPを一切必要とせず**、ターン経過だけで毎ターン強くなる。
- 味方はほぼ全部RP経由（鍛造の上限・隊枠・進化・罠の種類・階層・魔法）。
- ＝ **時間が経つほど一方的に不利**になる。交差点が T14〜17。
- しかも **DPは 7,284 余る**。余る資源と足りない資源があるのに**交換できない**。

### 体験の言葉に直すと
1. **世界の9割に一生出会わない。** 46種の配下も16属性も、ほとんど画面に出ない。
2. **中盤が平坦。** T5〜T12 は新しい物が1つも解禁されず、同じ操作の反復になる。
3. **予告なく死ぬ。** T12まで魔王HP100% → 1波で即死。何を間違えたか分からず、学べない。
4. **余った資源で何もできない。** DPが山ほどあるのに、欲しい物（研究）には使えない。

⚠ 1〜4はすべて**人が上手く遊んでも起きる**。導線（進言）の問題ではない。

### 計画（次の3段）
- **G-1 交換路**：余るDP/素材を、足りないRPへ流す道を作る。
  ⚠ 単純な交換レートは「DPが余る＝実質無限RP」になるので、**回数の制限**で効かせる。
- **G-2 1周の長さを設計スケールに合わせる**：時代が**最低1つは変わる**（T40前後）まで生きられるようにする。
  ⚠ 敵を弱くするのではなく、**味方の伸びが敵の伸びに追随できる**ようにする（＝G-1の効果）。
- **G-3 死に方を直す**：装備水準のインフレに**前兆**と**手当て**を付ける。
  「T12まで無傷 → 1波で即死」をやめ、削られながら負ける形にする。

### ⚠ やらないこと
- 進言の重み調整（ボットの導線の話であって、人の体験の話ではない）
- 敵の弱体化（波の人数はT20で頭打ち済み。弱くすると手応えが消える）
- 新しい倍率の軸（→ [[difficulty-curve-orders]]）

---

## 続き9：診断のやり直し ― 「両替機」案の撤回と、その手前で見つけた穴

### まず撤回する

前回出した **G-1「余るDP・素材をRPに流す交換路」は取り下げる。**
指摘のとおり、通貨を科学力に直接両替するボタンは Civ にも CDO2 にもステラリスにも無い。
入れた瞬間に「いかにDPを稼いで突っ込むか」の1本道になり、資源ごとの意味とツリーの計画性が消える。
しかも自分の診断（成長がRPツリーに一極集中している）に対して、
**その一極集中を温存したままバイパスを掘る**という矛盾した処方だった。

### 撤回したうえで、コードを数え直した

| 測ったこと | 数字 |
|---|---|
| 研究ノード総数 | **233** |
| うち**どのシステムからもidを読まれていない** | **137（59%）** |
| うち「効果の種類と量」を持っているノード | 162 |
| その効果の種類（`ResEffect`）16種のうち**読み手がいるもの** | **1種**（`MutationSuppress` のみ） |
| ⇒ **数値効果が一切適用されていないノード** | **127** |

`ResearchState.Sum(kind)` という受け口は設計されている（`Research.cs:17` にその意図が書いてある）。
**受け口を作って、読み手を1つも書いていない。**
だから「配下HP +12%」「研究点 +40%」と書いてあるノードを買っても、**何も起きない**。

もう1つ：**RPの産出を伸ばす手段がゲーム内に1つも無い。**

```
ResearchState.OnTurnEnd(knowledgeRank)
  -> AddRP(1 + 知識ランク)     // これが全部
```

DPには10以上の入り口があるのに、RPは**魔王の知識ランクという1本の受動的な点滴だけ**。
建てる・配属する・工夫する、どれをやってもRPは増えない。
そして `RpYield`（研究点 +10%〜+40%）を約束している4ノードは、上のとおり**読み手がいない**。

天啓（Eureka）も同じ。仕組みは既にあるが **233ノード中71ノードにしか条件が無く**、
効果は「40%引き」だけ。1周のRPが95しかない以上、**200RPのノードが120RPになっても届かない**。
しかも達成は `Debug.Log` に出るだけで、**画面に一度も出ない**。

### 本当の因果

「RPが足りない」のではなかった。**RPを払っても半分以上は何も返ってこない**。
そのうえでRPの蛇口が1本しかなく、敵はRPを一切使わずターン経過だけで強くなる。
だから交差点が T14〜17 に来る。

---

### 新しい計画 H

**H-1 ツリーの配線を通す（最優先・設計変更ではなく未完成の完成）**
`ResEffect` 16種に読み手を1つずつ書く。**16か所書けば127ノードが一斉に生きる。**
新しい倍率の軸は増えない（ノードは既にあり、値も既に書いてある）。
⚠ これだけでバランスが大きく動く。実装後に必ず測り直す。

**H-2 研究の産出を「建てる／配属する」ものにする**（Civの区域購入・CDO2の研究室配属）
- 迷宮に研究の設置物（書庫・観測室）をDPで建てる。巨大施設の5×5の枠が既にある。
- **配属**：配下や捕虜を研究に回す。回した個体は**隊にもボスにも使えない**。
  防衛を削って未来を買う判断になる。`TrainingSystem`（訓練所）で既に実証済みのパターン。
- ⚠ **両替ではない。** DPで買えるのは「産出力」だけで、RPそのものは買えない。
  建てた翌ターンから効き、波を守れなければ止まる。盤を広げる動機（X-1）とも噛み合う。

**H-3 天啓を「割引」から「事件」に**
- 40%引き → 条件達成で**即時解禁**または大幅ブースト。
- 達成を**画面に出す**（今は `Debug.Log` だけ）。
- カバー率 71/233 → 主要な枝は全部に条件を付ける。
- ⚠ 即時解禁は「安いノードが全部タダ」になり得るので、1周の回数上限か段に応じた割合にする。**実測して決める。**

**H-4 戦力の成長をツリーから外して現場へ**
- 鍛造上限：`r_grade_mithril/orichal` → **鹵獲装備の解析**（倒した相手の等級を素材で分解する）。
- 配下進化：`m_evo1/2/3` → **個体Lvと素材**で現地進化。
- 隊枠：練兵場（巨大施設）で既に +1 できる。研究を唯一の道でなくす。
- ⚠ ツリーを弱くするのではなく**担当を変える**。RPは迷宮のルール・魔法・政策・地上へ寄せる。

### やらないこと
- DP→RP の交換レート（撤回済み）
- 進言の重み調整（導線であって体験ではない）
- 敵の弱体化・新しい倍率の軸

### 順番
**H-1 → H-3 → H-2 → H-4。**
H-1 は新しい設計を1つも足さずに127ノードを生かすので、
これを先にやらないと H-2 で蛇口を太くしても**バケツの穴が開いたまま**になる。

---

## 続き10：Civ VII の実画面（160枚）と突き合わせ ― 計画Hの H-1 を撤回

詳細は `docs/civ7-screenshots-notes.md`。ここには結論だけ。

### 読み違えが5件あった

1. **⚠⚠ ノードの中身。** Civ VII の技術ノードは**建造物・ユニット・カード・遺産**を配る。
   「全体の攻撃力 +12%」型のノードは**1つも無い**。数値は建造物に付き、**置く場所**（隣接）で変わる。
   政策カードの大きさは「**宮殿の**生産力+1」。この作品は 162/233 が無条件の全体倍率。
2. **ツリーは時代ごとに丸ごと入れ替わる。** 1時代＝20〜24ノード・2画面。
   この作品は233ノードの1本ツリーを時代ゲートで小出しにしている。**大きすぎるのではなく分けていない。**
3. **時代が変わらないと全部死ぬ。** 時代42ターン／決着T14〜17＝**一度も変わらない**。
   そのせいで レガシーの道→属性ポイント・政策カード(Growth 8/End 4)・時代ゲートのノード が全部眠っている。
4. **政策カードは研究から出る。** Civ VII は社会制度ノードのほぼ全部がカードを配る。
   この作品の `PolicySystem.IsUnlocked` は**時代しか見ていない**＝研究とカードが一度も繋がっていない。
   なおスロットは Civ VII では**ターン10で1枠**（カードは2枚）。希少さが選択を作っている。
5. **戦力は戦闘の通貨で伸びる。** 司令官は戦闘経験→昇進ポイント→5系統のツリー。科学力は使わない。
   この作品には `KinPromotion`（4系統×3段・武勲）が**既にある。ただし地上の眷属だけ。**

読み違えていなかったもの：習熟(II)／排他／反復研究／時代／レガシーの道／属性ツリー／
隣接ボーナス／生産の待ち行列／購入／独立勢力／発見の2択。

### ❌ H-1（配線を通す）を撤回する
「`ResEffect` 16種に読み手を書けば127ノードが生きる」は**やってはいけない**。
生かした瞬間、**Civ がやっていない物＝無条件の全体倍率を127本入れる**ことになる。
自分で書いた鉄則（`research-dead-nodes`「全体倍率を消して、同じ量を"払う場所"へ移す」）にも反する。
**死にノードは配線するのではなく、付け替える。**

---

## 計画H（改）

**H-3 まず時代の長さを直す（最も安く、最も効く）**
時代 42ターン → **12〜15ターン**。1周＝3時代＝**T40前後**。
これだけで レガシーの道・属性ポイント・政策カード16枚・時代ゲートのノードが**一斉に生き返る**。
新しいコードはほぼ要らない。⚠ 直後に必ず通しプレイで測る。

**H-1（改）「+X%」を「物」に付け替える**
127ノードの数値を、**建造物・罠・トーテム・装備・政策カード**へ移す。
- 産出系（DpYield / RpYield / MaterialYield）だけは読み手を書く。Civでも建物が産出を出すから。
- 戦闘系（DefenderHp/Atk/TrapDamage/MagicPower/ResistAll…）は**全部「物」へ**。
- ⇒ ノード数は減る。1時代あたり **20〜24ノード**を目標にツリーを3枚に割る。
⚠ これは16か所の配線ではなく、**カタログの作り直し**。H-3 の後、段階的にやる。

**H-2（改）研究は「迷宮に置ける物」を配り、その物がRPを産む**
研究ノード → 施設（書庫・観測室）を解禁 → **DPで建てて盤に置く** → 毎ターンRP。
＝ Civ の「技術 → 建造物 → 産出」の鎖そのまま。**両替ではない**（買えるのは物であって研究点ではない）。
配下や捕虜の**配属**も同じ枠でやる（訓練所と同じく、配属した個体は隊に使えない）。

**H-5 政策カードを研究から出す**
`PolicySystem.IsUnlocked` に研究条件を足し、社会制度側のノードがカードを配るようにする。
⚠ **スロットは増やさない。** Civ VII はターン10で1枠。希少だから選択になる。

**H-4（改）迷宮側にも昇進ツリーを**
`KinPromotion` と同じ形（系統×段・戦闘で貯まる武勲）を**配下／隊**に。
鍛造上限は研究から外し、**鹵獲装備の解析**へ。

### 順番
**H-3 →（測る）→ H-1改 → H-2改 → H-5 → H-4改。**
H-3 は1行に近い変更で、眠っている実装済みシステムを4つ起こす。**先に測ってから中身に手を入れる。**

### やらないこと（据え置き）
DP→RPの交換レート／進言の重み調整／敵の弱体化／新しい倍率の軸。

---

## 続き11：Civ VII 全スクショ精読 ―― 訂正1件と、計画Kへの作り直し

全文は `docs/civ7-screenshots-notes.md`。

### ⚠ まず訂正：「RPの蛇口は1本しかない」は誤りだった
`ResearchState.AddRP` の呼び出し元を数え直したら **20か所**あった。
地上タイル(`SurfaceMap.YieldSummary` の `rpYield`)・施設(`DistrictCatalog`)・拠点・政策カード・
属性・外交・囚牢・発見・時代・語り・熱狂 ―― **Civ と同じ「建てた物が産む」回路は既にある。**

本当の問題はそこではなかった：**その回路が動き出す前に1周が終わっている。**
産出するのは「拠点の人口が働いているタイルだけ」なので、T15時点では領域も人口も施設もほぼ無い。
⇒ 蛇口の数ではなく、**時代・拠点・人口という土台が育つ時間が無い**ことが原因。

### 新たに分かった Civ VII の作り（要点だけ）
- **研究の所要ターン＝コスト÷1ターンの科学力**。科学力を伸ばすことがそのままツリーの速度になる。
- **ツリーはその時代ぶんが全部見える**（技術14＋II、社会制度10＋II、＋文明固有3〜4）。
- ノードが配るのは **建造物・ユニット・政策カード・遺産・プロジェクト**。数値は建造物に付き、**置く場所**で変わる。
- 建物の型が5つ：素の産出／**隣接ボーナス**／**倉庫ボーナス**（この居住地の〈施設の種類〉を底上げ）／
  **配置制約**（沿岸・草原・荒地に置く必要がある）／**街区**（同じ区域に決まった2つを建てると成立）
- **政策の差し替えは、新しいカードを解禁したときだけできる**（Civ VIの無政府状態に代わる軽い縛り）。
- **習熟(II)がそのまま勝利ポイント**（イノベーション）になる。
- **都市の成長＝隣接タイルを1つ選ぶ**。選ぶ前に**6産出の差分が `+1` で見える**／候補タイルに産出チップが浮く。
- 産出は7本：食料・生産力・金・科学・文化・幸福・影響力。**居住地には上限がある**（1/3）。
- 生産は**待ち行列**＋**購入タブ**。⚠ **買えるのは物だけ。科学力は絶対に買えない。**

### 🔴 この作品に決定的に欠けていたもの＝**生産力**
Civ では建物もユニットも**ターンをかけて作る**。金は「割り込んで買う」ための別口。
この作品は**すべてDPで即時購入**なので、
- DPの使い道が**配置枠の数**でしか制限されず、**7,284 余る**
- 「何を先に作るか」という**順番の判断が一度も発生しない**
- 拠点・人口・施設が育つ**時間の実感**が無い

---

## 計画K（Civ VII の骨格を入れる）

**K-0 時代の長さ**：42ターン → **12〜15ターン**。1周＝3時代＝T40〜45。
眠っている レガシーの道／属性ポイント／政策カード16枚／時代ゲート が一斉に起きる。

**K-1 生産力と生産キュー**（最重要の新システム）
拠点ごとに**生産力**を持たせ、建造物とユニットは**ターンをかけて作る**。DPは**購入タブ**へ回す。
⇒ DPの余りが「割り込んで買う」という判断に変わり、余剰そのものが遊びになる。

**K-2 建造物を「置く」ものにする**
隣接ボーナス／倉庫ボーナス／配置制約／**街区**（2棟で成立）。既存の `DistrictCatalog` を拡張。

**K-3 ツリーを時代ごとに割り、ノードは「物」を配る**
233ノードの1本 → 3時代 × 約24。127本の「+X%」は建造物・罠・トーテム・装備・カードへ**付け替え**。

**K-4 政策カードを研究から出す**＋**差し替えは新カード解禁時のみ**。⚠ スロットは増やさない。

**K-5 ユニットの生産と昇進**
`KinPromotion`（4系統×3段・武勲）を**迷宮の配下／隊**にも。鍛造上限は鹵獲装備の解析へ。

**K-6 助言者とジャーナル**
`GuideSystem`（腹心の報告）を**「どの道を狙うか」の宣言**＋**達成すべき目標の一覧**に作り替える。

### 6本の柱の対応
| Civ VII | この作品 | 状態 |
|---|---|---|
| 生産力 | **無し** | 🔴 K-1 で新設 |
| 幸福度 | 不満(`PopMult`)として半分ある | 🟡 正式な産出に昇格 |
| 科学力 | 研究点RP | ✅ |
| 財政力 | 魔力点DP | ✅（K-1 で購入専用に役割変更） |
| 軍事力 | 迷宮の防衛・軍団 | ✅ |
| 文化力 | 名声／感情 | 🟡 どちらが文化かを決める |

---

## 続き12：K-0 完了 ―― 時代が初めて動いた（＋その過程で出た本物のバグ3件）

### 変えたもの（ゲーム側）
| ファイル | 変更 |
|---|---|
| `EraSystem.cs` | `Need` 210→**75**（自然進行だけで42T→**15T**）／`TriumphProgressCap` 0.6→**0.35**（偉業で早めた最短 17T→**10T**）／`CrisisAt` を `Need` から導出（56）／**`MaxTriumphsPerTurn = 2` を新設** |
| `DungeonAdventurerSpawner.cs` | **`AbortAndClear()`** を新設（湧きを止めて盤の冒険者を全部消す） |
| `GameUIManager.Title.cs` | `StartNewGame` の頭で **`AbortAndClear()`** と **`ClearAllRunFeatures()`** を呼ぶ |
| `DungeonFeatureManager.cs` | **`ClearAllRunFeatures()`** を新設（全階層の配置を空にする） |

### 露見した本物のバグ3件（時代が一度も変わらなかったので今まで見えなかった）
1. **時代交代で報酬が雪崩れ込む。** 伸長に入った瞬間、伸長の偉業30個のうち既に満たしている物が
   **一斉に発火**し、1ターンで **DP +15,370／RP +126／属性 +10**（DP 2,317→17,687）。
   → 1ターンに成立する偉業を **2件まで**に。報酬は減らさず速さだけ抑える。
2. **新しい周を始めても盤の冒険者が消えない。** `StartNewGame` が湧きも盤も片付けていなかった。
   同じセッションの2周目が **T1 の波から永久に抜けない**（実測：2〜4周目が全部停止）。
   ⚠ **人が「タイトルへ戻る→新しい世界を始める」を続けてやっても起きる。**
3. **置いた罠・巣・トーテムも残る。** `ResetRunCounters()` は `trapsEverPlaced` を0にするだけだった。
   実測：2周目が **T1 の時点で 7/14 枠が埋まった状態**で始まり、来襲4人に対し撃破10になった。

### 計測（自動運転・汚染なしを確認：4周とも T1 は 1/14枠・研究0・属性0・魔王HP100%）

| | K-0 前 | K-0 後 |
|---|---|---|
| 決着ターン | T11 / T12 / T14 / T15 / T17 | **T14 / T16 / T18 / T20** |
| 時代の交代 | **0回**（42Tかかるので一度も届かない） | **毎回 T12 に 胎動→伸長** |
| 到達した時代 | 胎動のみ | 伸長 34〜66/75 |

時代交代の瞬間に **政策枠 3→4** が開き、属性ポイント・RP・研究が伸び始める。

⚠ **4周は独立した標本ではない。** T14→T16→T18→T20 と**セッション内で単調に伸びている**
（撃破も 82→116→153→217）。実績・形見が `PlayerPrefs` で周を越えるのは設計どおりなので、
おそらくその効果。**独立した1周目どうしで比べると T20 / T20 / T14（K-0後）対 T11〜T17（K-0前）。**

### 結論
- ✅ **時代は動くようになった。**眠っていた政策枠・属性・時代ゲートが実際に開く。
- ✅ 周は概ね **4ターンほど伸びた**。
- ❌ **終焉には届かない。**3時代を回すには T30〜45 まで生きる必要があり、まだ遠い。
- ⇒ 束縛条件は時代の長さではなく **T14〜20 で魔王が討たれること**。次の K-1（生産力）と
  K-3（ノードを「物」に付け替える）が、そこへの回答になる。

### ⚠ ハーネス側の注意（次に測る人へ）
- **エディタ設定 `ScriptCompilationDuringPlay` が 0 だと、再生中の再コンパイルで static が全部消える**
  （`Instance` も `GameSetup.Started` も null になり、`Awake` は再実行されない＝画面だけ固まる）。
  計測中は **1（再生後に再コンパイル）** にすること。⚠ 計測後に 0 へ戻してある。
- 走行中に `execute_code` で様子を見に行くのも引き金になりうる。**ログファイルだけを外から見る。**

---

## 続き13：K-1（前半）― 生産力と待ち行列。画面01・02 を実装

アーティファクト（`https://claude.ai/code/artifact/b5d81242-5343-440a-951f-a5064a2307cd`）の画面01・02。

### 先に分かったこと：**生産は「無い」のではなく「Civ と違う形で半分あった」**
| | 実装前 | Civ VII | K-1 |
|---|---|---|---|
| 生産力 | `LegionRoster.ProductionAt`＝3＋人口×2 | ある | **式はそのまま使う** |
| 待ち行列 | **無い**（1拠点1件だけ） | ある | **足した** |
| 施設の建設 | **DPで即時購入** | 生産力でターンをかける | **生産に載せた** |
| 軍団 | **DPを取ったうえにターンもかかる**（二重取り） | どちらか一方 | **着工は無料に**、DPは購入だけに |

⚠ さらに **`LegionRoster` はセーブ対象に入っていなかった**（軍団も作りかけもロードで消えていた）。`SaveSystem` に登録した。

### 作ったもの
- **`ProductionSystem.cs`（新）** … 拠点ごとの待ち行列。施設と軍団を同じ列に載せる。
  - **1 生産力 = 8 DP**。⚠ 施設の旧DP価格が変わらないように決めた（交易所 228DP → 生産28 → 買値224DP）。
  - **購入は1ターンに1件まで。** ⚠ レートだけで縛ると、DPが余る後半に全部買えて「順番を選ぶ」遊びが消える。
  - 拠点を失うと生産も消える／置き場が無ければ完成を持ち越す／旧 `builds` は自動で列へ移す。
- **`DistrictCatalog.PlaceBuilt`（新）** … 費用を取らずに置くだけの口。`TryBuild` は**待ち行列に積む**意味に変わった。
- **`LegionRoster.SpawnBuilt` / `TakeLegacyBuilds`（新）** … 完成処理を切り出し、列から呼べるように。
- **画面01：上部バーを産出6本立てに** … 生産力・魔力点・素材・研究点・名声・幸福度＋拠点 n/m。
  仕切りを入れて左＝産出／右＝状態に分けた（⚠ Civ VII の上部バーは産出だけで、危険度のような状態は混ざらない）。
  増分 `(+N)` は**前ターンに実際に増えた量**（予測を出すと外れたとき嘘になる）。
- **画面02：地上メニューに『生産』タブを新設** … 拠点を選ぶ帯／その拠点の産出／生産・購入タブ／
  待ち行列（先頭が建造中・進捗バー・▲▼で並べ替え・×で取りやめ）／分類ごとの一覧（所要ターンつき）。

### 検証（エディタ実測）
```
拠点=迷宮前の荒れ地 生産力=8
交易所を積む → T4 で完成し施設として置かれた（28生産力）
スケルトン軍団 → T10 で完成し盤に現れた（44生産力）
購入：ゾンビ軍団 416DP → DP 2200→1784・購入枠 1→0（同ターンの2件目は拒否）
```
⚠ 列は**先頭の1件だけ**が伸びる（Civ と同じ）。

### 踏んだ罠
- **`🔨` は TMP のフォントに無く、黙って消える**（画面に出したら何も表示されなかった）。文字に置き換えた。
- **python の heredoc で `\n` が実際の改行に化けた**（3度目）。C#の文字列に `\n` を書くときは Edit を使う。
- 新規 `.cs` は `scope=scripts` の refresh では取り込まれない。**`scope=all`** が要る。

### 残り（K-1 後半）
- 地上フェーズの上部ヘッダーは**別の帯**で、まだ産出6本になっていない（迷宮側だけ差し替わっている）。
- 大工事／プロジェクトの分類はまだ空（アーティファクトには載っている）。
- 通しプレイでの計測はこれから。

---

## 続き14：K-1（後半）― 大工事とプロジェクト、地上ヘッダーの統一

### 大工事／プロジェクトに何を入れるか（考えたこと）

**分類の空きを埋めるだけにしない。**アーティファクトの画面02には4分類あったが、
既存の物を並べ替えるだけなら、生産力は**地上だけの話**に閉じたままになる。
この作品を実際に遊ぶ人は**迷宮**を見ているので、それでは生産力が他人事のまま終わる。

そこで2つの軸で決めた：

| 軸 | 出てきた答え |
|---|---|
| **Civ VII から輸入** | プロジェクト＝生産力を「一度きりの見返り」に変える品目（本家は科学プロジェクト→イノベーション）。<br>「完了すると祭壇を**無償で2回購入できる**」という**券**の形も本家にあった |
| **この作品ならでは** | **大工事＝生産力を迷宮に注ぐ唯一の道**。Civ には無い分類。地上と迷宮を噛み合わせる |

### 入れたもの

**🏗️ 大工事（迷宮に効く）**
| | 生産力 | 中身 |
|---|---|---|
| 縦坑の掘削（新しい階層） | 100 | 迷宮を1層深くする。道のりが伸び、配置枠が増える |
| 練兵場の建造許可 | 112 | 完成で練兵場(5×5)を**1つ無償で置ける**。この階の隊枠+1 |
| 大巣の建造許可 | 150 | 完成で大巣(5×5)を**1つ無償で置ける**。波あたり4体・射程3 |

⚠ 巨大施設は**盤のどこに置くかをプレイヤーが選ぶ**ので、完成しても勝手には建たない。
**建造許可（券）が1枚入り、迷宮の『巨大』から無償で置ける**形にした
（Civ VII の「完了すると祭壇を無償で2回購入できる」と同じ形）。券は2枚までしか持てない。

**🎯 プロジェクト**
| | 生産力 | 中身 |
|---|---|---|
| 徴募 | 40 | **配下を1体、無償で召喚**（解禁済みのいちばん上の段） |
| 祝祭の準備 | 30 | その拠点で**祝祭**（4ターン・産出が伸び、政策の自由枠が1つ開く） |

⚠ **産出への両替にはしない**（両替機を撤回した件と同じ理由）。渡すのは
**頭数**（＝そのまま捌ける数になる。壁を動かした唯一の軸）と**状態**（祝祭）で、どちらも通貨ではない。
⚠ 徴募は「生産力→頭数」の唯一の経路なので、安すぎないよう40（＝5ターン）に置いた。

### そのほか
- **上部バーの DP 表記を『魔力点』から『DP』に戻した**（ゲーム全体の呼び名に揃える）。
- **地上フェーズの帯も産出6本に揃えた**（生産力と幸福度が抜けていた）。
  `支配 17/4502　生産 8　DP 200 (+20)　素材 +1　研究 +0　名声 +4　幸福 +1　世界水準+1.20`

### 検証（エディタ実測）
```
生産力8の拠点で 徴募→祝祭の準備→練兵場の建造許可 を積む
  T5  徴募完成　配下 1→2（インプ）
  T9  祝祭の準備完成　祝祭4ターン開始
  T23 練兵場の建造許可完成　許可 1枚
以後『祝祭の準備』は「この拠点はいま祝祭のさなか」で積めなくなる（重複防止）
```

### 残り
- 通しプレイでの計測（K-0 のときと同じ4周）。
- 迷宮側の『巨大』ボタンに**許可の枚数**をまだ出していない（券を持っていても気づけない）。

---

## 続き15：K-1 の計測 ―― 機構は動いた。**狙いは外した。**

汚染なし4周（`docs/playlog_K1.md`）。比較の相手は K-0 の4周。

| | K-0 | K-1 |
|---|---|---|
| 決着ターン | T14 / T16 / T18 / T20 | **T15 / T19 / T22 / T22** |
| 1周目どうし | T14 | **T15** |
| 時代 | 毎回 T12 に胎動→伸長 | 同じ |
| 終焉 | 未到達 | 未到達 |

**周の長さは変わっていない**（1周目 T14→T15 は誤差）。

### ✅ 動いたもの
- 待ち行列は**常に3件**埋まり、完成して次が始まる流れが最後まで回った。
- **徴募が効いた。** 配下の頭数が **1 → 6 / 9 / 13 / 16**（周を追うごとに増える）。
  「生産力→頭数」の道は通った。
- 大工事・建造許可・祝祭も手で試して完成まで確認済み。

### ❌ 外したもの：**DPの余剰は解決していない。むしろ悪化した**
最終ターンのDP残高：**2,473 / 4,948 / 8,261 / 28,331**（K-0 は 7,284）。

理由は2つ：
1. **施設をDP即時購入から外した**ので、DPの出口が1つ減った。購入（1ターン1件）はそれを埋めていない。
2. ⚠⚠ **購入が1回も起きなかった。** 原因はボット側：準備フェーズの頭で
   `配置枠を埋める` と `装備を鍛える` にDPを使い切るので、購入の判定(DP≥1,200)に届かない。
   ログのDPは**戦闘後の値**で、判定が走る準備フェーズの頭ではほぼ0。
   ⚠ 購入の口そのものは手動で動作確認済み（ゾンビ軍団 416DP・枠1→0）。

### ⚠ もっと根の深い問題：**生産力の桁が足りない**
1周を通して **生産力 8 → 16〜20**。1生産力=8DP の換算だと毎ターン **160DP相当**。
一方DPの収入はそれを大きく超える（末期 28,331）。
**Civ の生産力は100ターンの尺で設計された数字**で、20ターンで終わるこの作品には小さすぎる。
生産を主役にするなら、生産力そのものを桁で上げるか、**DPの蛇口を絞る**しかない。

### ⚠ 死に方は1ミリも変わっていない
```
持ち逃げ 54 → 80 ／ 装備水準 26 → 53.8 → 59.3 → 99.3 ／ T15 に死亡
（3周目も 装備水準 86.8）
```
**持ち逃げが増える → 世界の装備水準が跳ねる → 1〜2波で死ぬ。** K-0 のときと同じ形。
生産力を入れても、ここには一切触れていないので当然ではある。

### 結論と次
- K-1 は**機構としては完成**（生産・待ち行列・購入・大工事・プロジェクト・地上ヘッダー）。
- ただし **DPの余剰も、周の長さも、死に方も動かなかった。**
- ⇒ DPの余剰の根は**出口ではなく蛇口**（撃破と大招集）。出口を足すより蛇口を見るべきだった。
- ⇒ そして**実際に殺しているのは装備水準のインフレ**で、これは K の計画のどこにも入っていない。
  次に手を入れるならここ（以前 G-3 として挙げた「死に方を直す」）。

---

## 装備水準の作り直し（案・実装はまだ）

### いまの仕組みと、なぜ壊れているか
```
宝箱を漁る → carriedGear += (1 + joyValue*0.05) * LootMult
逃げ切る   → gearLevel  += carriedGear * GearSpreadFrac
次の波     → EquipmentCatalog.GradeFromWorld(rank, gearLevel) で武具の等級が決まる
```
⚠⚠ **持ち出される物に「等級」が無い。** 量だけ。だからグレード1相当の宝箱でも、
たくさん漁られれば世界の装備水準はグレード10へ向かって上がる。

そして持ち逃げの量は**波の人数に比例**し、波の人数は**ターンに比例**する。
＝ **装備水準は実質ターン駆動のカーブで、「プレイヤーの選択の結果」という衣を着ているだけ。**
実測：装備水準 26 → 53.8 → 59.3 → **99.3**（3ターン）→ 死亡。
プレイヤーに残された手は「1人も逃がすな」だけで、波が大きくなると不可能になる。

### 案（ユーザー提案 ＋ 補強）

**① 上がる条件を「量」から「等級の差」へ。しかも一足飛びにはしない**

```
持ち逃げされた宝箱1つにつき
    ポイント += max(0, 宝箱の等級 − いまの装備水準)
ポイントが 10 たまるごとに
    装備水準 += 1   （ポイントは 10 引く。端数は持ち越す）
```
⚠ **10 にした理由：1ターンに宝箱を何度も開ける個体がいる。** 5だと1体の略奪者が
そのターンだけで2等級ぶん押し上げてしまう。

- 等級3の敵が**等級1の宝箱**を拾っても **0ポイント**（すでに上回っているので学ぶものが無い）
- 等級3の敵が**等級10の宝箱**を拾っても、入るのは **7ポイント**。10に届かないので**まだ上がらない**。

**⚠⚠ この式は自動的に頭打ちになる。** 装備水準が撒いた等級に近づくほど差分が減り、
追いついた瞬間に 0 になる ―― **撒いた等級が、そのまま世界の装備水準の上限**。
これが「こちらで敵の強さの上昇をコントロールできる」の正体。

飽和までの実測見積り（1つ持ち逃げされるごと）：

| 撒く等級 | 0→上限までに要る持ち逃げの数（10点/等級） |
|---|---|
| 等級3 | 4＋5＋10 ＝ **約19個** |
| 等級5 | 2＋3＋3＋5＋9 ＝ **約22個** |
| 等級10 | 序盤は **1個で+1等級**。一気に跳ねる＝意図的な賭け |

序盤は迷宮の等級が低いので、いくら漁られても敵は強くならない。
＝ **装備水準がターンではなく「こちらが何を撒いたか」で決まる。**

⚠ ポイントが入るのは **持ち逃げされたときだけ**（倒せば世界には届かない）。
いまの「逃がすと広まる／倒すと戻る」の骨格はそのまま残る。

**② 宝箱を等級1〜10に分ける（ツリーで段階解禁）**
中身の等級＝そのまま `EquipmentCatalog` の等級ラダー。
⚠ **見返りも等級に比例させる。** 等級1の宝箱はDPも感情もわずか、等級10は大きい。
これが無いと「等級1だけ撒く」が無条件の最適解になり、選択が消える。

**③ 階層ごとに「どの等級をどの割合で置くか」を設定できる**
例：B1F＝等級1が9割・等級2が1割／B3F＝等級3が5割・等級4が5割。
⚠ 10×階層数の表を触らせない。**「基準の等級」＋「ばらつき」の2つのつまみ**に畳む。

**④ 配置は生成時のまま。開けた瞬間に等級を決める（遅延解決）**
生成器に手を入れずに済み、設定を変えたら次の波から効く。

### ⚠ 補強（案のままだと壊れるところ）
- **世界が勝手に武装する下限が要る。** 等級1しか撒かなければ永久に敵が強くならないなら、
  「撒かない」が無条件の最適解になる。**名声と時代でゆっくり上がる下限**を別に持たせる
  （いまの速さの 1/5 程度）。撒くのは「その下限を自分から追い越す」行為にする。
- **『先触れ』に次の波の装備等級を出す。** 撒く等級を決めるのは事前の判断なので、
  相手の等級が見えないと賭けにならない。
- **`RecoverGear`（略奪者を倒すと装備水準が下がる）は残す。** 唯一の巻き戻しなので。
- ⚠ 階層ごとの設定表は**セーブに載る**。等級の enum は末尾追加のみ。

### なぜこれが良いか
- 装備水準が**ターンの関数から、プレイヤーの選択の関数**に変わる。
- 原作の誘導経済そのもの：**旨い餌を撒くほど儲かるが、敵が強くなる。**
- 「1人も逃がすな」という不可能な要求が、「**何を撒くか**」という毎ターンの判断に置き換わる。

---

## 続き16：K-2 ― 画面03「選ぶ前に、選んだ結果が見える」

### 先に確かめたこと：**システム側はほぼ揃っていた**
| Civ VII の要素 | この作品 |
|---|---|
| 隣接ボーナス | ✅ `Adjacency`（major+2 / standard+1 / minor+0.5） |
| 倉庫ボーナス | ✅ 倉庫（`Yield.Warehouse`） |
| 街区（2棟で成立） | ✅ `district2` / `asQuarter` |
| 配置制約 | 🟡 港＝沿岸のみ、だけ |
| **選ぶ前に結果が見える** | ❌ **無かった** |

⇒ K-2 は**最後の1行**に絞った。Civ VII の意思決定の気持ちよさの正体はここだったので。

### 作ったもの
- **`DistrictCatalog.PreviewYieldAt(施設, タイル)`** … そこに建てたら毎ターンいくつ増えるかを返す。
  ⚠⚠ **換算レートを2箇所に書かない。** `TotalYields` と同じ式をここから使う
  （別々に書くと「見せた差分」と「実際の増分」がずれて**嘘になる**）。
- **`SurfaceView.placementPreview`**（領域id → 隣接ボーナス）
  … 候補タイルを**良い場所ほど濃く**光らせ、タイルのラベルに **+N** を出す。
  ⚠ 比べているあいだは地名や資源より優先して出す（いま知りたいのはそれだけなので）。
- **生産パネルの施設の行に『盤で比べる』** … 押すと盤に出る。もう一度押すと消える。
- **差分パネル** … `交易所 をここに建てると／基礎1＋隣接3　川+2×2→3／DP +56`。
  0 のものは薄く「―」で出す（**何が増えないか**も読めるように）。

### 実測
```
交易所を選んで『盤で比べる』
  候補タイル 14
  廃樹海 +3（川+2×2→3）／星降りの集落 +3／硫黄の霊峰 +2／陽炎の池 +0
  差分パネル：DP +56
```

### 踏んだ罠
- **`\n` が実際の改行に化けた（4度目）。** python 経由でC#の文字列に `\n` を書こうとするたびに起きる。
  **C#文字列の `\n` は Edit ツールで書く**、を徹底する。
- **`ScreenCapture` を設定と同じ `execute_code` で呼ぶと、描き直す前のフレームが撮れる**（盤が真っ黒になった）。
  設定と撮影は**別の呼び出しに分ける**。
- 8要素の名前つきタプルは **CodeDom（検証用の `execute_code`）から読めない**。本体のコンパイルは通る。

---

## 続き17：K-3（前半）― ツリーの画面を作り直した

アーティファクト `https://claude.ai/code/artifact/092d51ec-1b2b-42d8-a69a-206ac0d2539d` のとおり。

### 技術ツリーのスクショ37枚を1枚ずつ見て分かった、いちばん効いている規則
> **Civ のノードカードには「+12%」のような数字が1つも書かれていない。**
> 載っているのは **名前・所要ターン・貰える物のアイコン** の3つだけ。説明も数値もホバーの中だけ。

だから24枚並べても読める。この作品の旧カードは 232×100px に
**名前＋状態＋コスト＋説明文＋排他の警告＋習熟＋天啓の条件**を詰めていて、並べた時点で読めなかった。

### 作ったもの
- **時代タブ**（胎動 0/54 ／ 伸長 0/83 ／ 終焉 0/98）。⚠ **233枚を1枚に積んでいたのをやめ、時代ごとに割って見せる。**
  既定はいまの時代。まだ来ていない時代も覗けるが研究はできない。分野の見出しも**その時代内の数**に変えた。
- **ノードカードの作り直し** … 名前／所要ターン（押せるときだけ）／貰える物のアイコン／
  印（天啓・排他・反復・条件）。説明文とコストはカードから外した。
- **ホバーの中身** … 状態を副題で言う（研究完了／いま研究できる／未解除の研究／封印された研究）、
  説明、前提の✔つき一覧、解放条件、**天啓（いままで `Debug.Log` にしか出ていなかった）**、末尾にコストと所要ターン。
- **所要ターン ＝ 残りコスト ÷ 前ターンに実際に入った研究点**（予測ではなく実測。上部バーの増分と同じ考え方）。
  足りているときは「今すぐ」と出す（「―」だと終わらないように見える）。

### ⚠ 踏んだ罠2つ（どちらも既知だったのに踏んだ）
1. **TMPは行の高さが枠より大きいと1文字も描かない。** 13.5pt を 18px の枠に入れたら
   `chars=0` で**ノード名が全部消えた**。22px に広げて解決。
2. **絵文字はこのプロジェクトのフォントに無く、黙って消える。** アイコンを 👹🪤⚒ で書いたら
   1つも出なかった（🔨 のときと同じ）。`UIIcons` の手続き生成アイコンに差し替えた。

### 残り（K-3 後半・まだ手を付けていない）
**162/233 のノードが「+X%」のままで、配る「物」を持っていない。**
いまのアイコンは**分野と効果の種類から引いた代用**。本番はノードを
「建造物・配下・罠・政策カード・遺物・大工事を配る」形に付け替える ―― これがカタログの作り直しで、K-3 の本体。

---

## 続き18：ホバーを専用パネルに作り直した（K-3の続き）

### 何が起きていたか
研究ノードのホバーに**汎用の `AddTooltip`** を使っていた。あれは
`BuildTooltip` が作る **画面下の 560×30 の固定箱**で、研究の説明
（説明文＋前提の一覧＋解放条件＋天啓＋コスト）を流し込めば**必ず枠からはみ出る**。
アーティファクトのツールチップとは別物だった。

### 作ったもの：`GameUIManager.ResearchTip.cs`（新）
Civ VII のツールチップと同じ作り：
- **見出し**：名前（中央・15.5pt）＋**状態の副題**（研究完了／いま研究できる／未解除の研究／
  封印された研究／◯◯の時代から）
- **1行＝1つの中身**：左に絵（`UIIcons`）、右に 見出し／産出の行（ラベル左・値右・下に細い罫）／
  箇条書き／効果／**配置制約は橙**
- 行は「分野と段＋説明」「排他」「時代／前提の✔つき一覧／解放条件」「**天啓（進捗つき）**」
- **末尾にコストの札**（`コスト 6 研究点　今すぐ`）＋所持

⚠⚠ **高さは中身から測って決める。** `TMP.GetPreferredValues(text, width, 0)` で
レイアウト待ちなしに測り、行を積みながら y を伸ばす。固定にすると長い説明で必ず溢れる。
⚠ 出る位置はカーソル基準で、**画面の外へ出さないよう内側へ寄せる**。
⚠ 押せないノードに「今すぐ」と出さない（時代で止まっているのに研究できるように見えた）。

### 実測
```
配下進化II 開放 ／ 伸長の時代から
  [魔物] 魔物研究・第1段　2段階目の進化(進化形→上位)を解禁。
  [★]   まだ来ていない時代　伸長の時代に入るまで研究できない。
  [研究] 天啓　個体をLv15まで育てる　達成するとコストが40%引きになる
        コスト 6 研究点 ／ 所持 120 研究点
パネル高さ 300.5px（中身から自動で決まった）
```

### ⚠ また踏んだ罠
**`ScreenCapture` は `Time.frameCount` が進んでいないと嘘をつく。**
再生に入った直後は `frame=1` のままで、撮っても中身が写らない。
`Application.runInBackground = true` にしてフレームを進めてから撮ること。

---

## 続き19：K-3 本体（第1段）― 業(Art) の30ノードを「作れるようになる」に付け替えた

### ⚠ 「物を配る」の意味を取り違えない
**貰えるのではなく、生産・配置できるようになる**（Civ の 畜産 → 投石兵を*訓練できる*ようになる）。
以下はその読み方で作った。

### まず数えた（ゲームの実体から）
| 時代 | 魔物 | 領域 | 錬成 | 魔王 | 魔法 | 地上 | 業 | 計 |
|---|---|---|---|---|---|---|---|---|
| 胎動 | 8(4) | 19(1) | 2(1) | 2(0) | 6(2) | 8(1) | **9(9)** | 54 |
| 伸長 | 14(11) | 14(5) | 4(2) | 4(2) | 14(6) | 22(12) | **11(11)** | 83 |
| 終焉 | 10(7) | 10(5) | 8(0) | 10(8) | 17(10) | 10(6) | **33(33)** | 98 |

（かっこ内は「+X%」型）。**業(Art) は53ノード全部が「+X%」**で、しかも1本も配線されていなかった。

### ⚠⚠ さらに悪いことに、業は属性ツリーと二重だった
業の30ノード（武術・耐性・魔眼）は「配下の攻撃 +5%」「配下HP +12%」型。
一方 `AttributeSystem` は **6軸×4段が全部配線済み**で、
`DefenderHpMult` `SquadSlotBonus` `RpPerTurn` `ExpMult` … と**同じ territory を既に担っていた**。
しかも属性は**レガシーの道で点が入る**＝ Civ VII の本来の仕組み。
つまり業は「動かないほうの重複」だった。

### 付け替えた先：**装飾品を作れるようにする**
装飾品（`AccessoryCatalog` 14種）は**配下に着けると魔物スキルを付与する**もので、行商人で買える。
ここに研究の縛りを付けた：

```
研究『棘皮の細工』 → 装飾品『棘の胴当て』が作れる／行商人に並ぶ → 着けた配下が〈棘の皮膚〉を使う
```

- `AccessoryCatalog.Def` に `research` を追加（+ `IsUnlocked` / `LockReason` / `ByResearch`）
- `MerchantShop.RollItem` が**解禁済みしか並べない**ように（棚が空にならない保険つき）
- 業の a_* 30ノード → **14ノード**（装飾品1つにつき1ノード）。胎動6・伸長5・終焉3
- ⚠ **ノード側は `ResEffect.None`。** 効果量は装飾品にあるので、ノードにも倍率を持たせると二重取り
- ツリーのカードとホバーに「作れるようになる物」を出す
  （名前・行商人での値段・付与するスキル・HP/攻撃/速度・「研究すると作れる／並ぶようになる」）

### 結果（実測）
| | 前 | 後 |
|---|---|---|
| 業(Art) | 9(9) / 11(11) / 33(33) | **6(0) / 5(0) / 26(23)** |
| ノード総数 | 235 | **219** |
| 装飾品 | 最初から全部買える | **14/14 が研究待ち** |

`a_craft_stone` を研究 → 『石守りの護符』が作れるようになるところまで確認済み。
残る 26(23) は**大罪の刻印（h_*）**で、これは第2段。

### 次（K-3 第2段）
- **刻印(h_*) 23ノード**：7つの大罪 × 3段の排他分岐。構造（1つ選ぶと他が永久に閉じる）は
  Civ VII の排他イデオロギーそのもので**残す価値がある**が、中身が全部「+X%」。
  各大罪が「何を作れるようにするか」に付け替える必要がある。
- そのあと 地上(22/13/10)・魔物(14/10)・魔法(14/17) の効果ノード。

---

## 続き20：K-3 本体（第2段）― 大罪の刻印を「作れるようになる物」に付け替えた

### 何が問題だったか
7つの大罪 × 3段 ＝ 21ノード＋根＋反復 ＝ 23ノード。
「**1つ選ぶと他が永久に閉じる**」という構造（Civ VII の排他イデオロギー）は良いのに、
**中身が全部「+X%」で、しかも1本も配線されていなかった** ―― つまり選ぶ意味が画面に一度も出ていない。

### やったこと
**3段を1段に畳み、7つとも別の「作れるようになる物」に付け替えた**（23 → 9ノード）。
段を積んでも数字が増えるだけで選択は増えないので、段はいらない。

| 大罪 | 解禁されるもの | 仕掛け |
|---|---|---|
| 暴食 | プロジェクト『**喰らいの宴**』 | 牢の捕虜を1人喰らい、配下を1体無償で得る |
| 強欲 | **行商人の棚 3→4** | `MerchantShop.Slots` |
| 憤怒 | プロジェクト『**焚刑**』 | **世界の装備水準を下げる**（`LureEconomy.RecoverGear`） |
| 怠惰 | **迷宮の配置枠 +2**（全階層） | `PlacementCap` |
| 嫉妬 | プロジェクト『**簒奪**』 | 属性ポイント +1 |
| 傲慢 | プロジェクト『**玉座の顕現**』 | 魔王に BP +3 |
| 色欲 | **牢の枠 +2** | `Prison.Capacity` |

⚠ **『未来の理』(h_future) も直した。** Civ VII の未来技術は**属性ポイント**を配る（割合ではない）ので、
`DefenderHp +4%` → **属性ポイント +1（反復）** に。

### ⚠⚠ 途中で踏みかけた大きな間違い
最初は 憤怒＝大巣／怠惰＝練兵場 と、**巨大施設を刻印のゲートにした**。
だがこれは**既にある入手経路を奪う**改悪だった ―― 刻印は終焉の時代の物で、
決着が T20 の周では巨大施設が**永久に建てられなくなる**。
→ **巨大施設のゲートは外し、刻印は「新しい物を足す」方向にだけ使う**と決めた。
`ProductionSystem.Works.Research` にその決まりをコメントで残してある。

### ⚠ ここでも `const` の罠（4度目）
`MerchantShop.Slots` が `public const int 3` だった。研究で伸ばすなら const にできない。
プロパティに変え、棚の配列も**中身を引き継いだまま伸ばす**ようにした（伸ばさないと添字外で落ちる）。

### 実測
```
刻印なし： 棚3　配置枠14　牢0　巨大施設＝作れる　刻印プロジェクト＝全部×
7つ全部： 棚4　配置枠16　牢+2　刻印プロジェクト＝全部○
『焚刑』1回 → 装備水準 20.0 → 14.0
```

### 数の変化（K-3 第1段＋第2段）
| | 着手前 | いま |
|---|---|---|
| ノード総数 | 235 | **205** |
| 「+X%」型 | 127 | **83** |
| 業(Art) | 9(9)/11(11)/33(33) | **6(0)/5(0)/12(0)** |

**業は「+X%」がゼロになった。** 残る 83 は 地上(12/6)・魔物(11/7)・魔法(6/10)・魔王(8) など。

---

## 続き21：K-3 本体（第3段）― 地上(Surface)

### 地上は業と性格が違った
数えたら **40ノード中、実は10件が既に施設を解禁していた**（`DistrictCatalog` の `research` から読まれている）。
つまり地上の問題は「死んでいる」ではなく **「説明と中身が食い違っている」** だった。

```
s_town_prod 「町を『生産』に特化できる」 ← 説明
            ResEffect.MaterialYield 0.2f  ← 中身（しかも誰も読んでいない）
```
**特化の仕組み（Town Focus 9種）は既にあって、研究とは繋がっていなかった。**

### やったこと
| 区分 | 件数 | 処置 |
|---|---|---|
| 既に施設を解禁しているのに「+X%」が付いていた | 7 | **「+X%」を外し、説明を実際の解禁だけにした** |
| 死んでいて、対応する仕組みが既にある | 9 | **本当に繋いだ**（下表） |
| 三重で要らない | 3 | **削除**（s_navy2 / s_navy3 / s_settle2） |

**繋いだもの**
| ノード | 実際に何ができるようになったか |
|---|---|
| 生産の町 / 農の町 / 保養の町 / 要塞の町 | **拠点の特化を選べるようになる**（9種のうち、成長の町だけ最初から） |
| 指揮官 | **眷属が昇進を修められるようになる**（`KinPromotion` 全体のゲート） |
| 大将軍 | 昇進の**第3段**が取れるようになる |
| 国境の理 | 国境の**自動拡張が速くなる**（+4/ターン） |
| 交易帝国 | 交易路の上限 **+3** |
| 帝国法 | 支配上限 **+4** |

⚠ **s_navy2/s_navy3 は削除。** 海越えは `s_voyage`（研究）と `KinPromotion`（沿岸航行・遠洋）が
既に担っていて**三重**だった。s_settle2 も上限系が s_charter2 と重複していた。

### 結果
| | 着手前 | 第1段後 | 第2段後 | **いま** |
|---|---|---|---|---|
| ノード総数 | 235 | 219 | 205 | **202** |
| 「+X%」型 | 127 | 108 | 83 | **64** |
| 業(Art) | 53(53) | 23(23) | 23(0) | 23(0) |
| 地上(Surface) | 40(19) | 40(19) | 40(19) | **37(0)** |

**業と地上の「+X%」はゼロになった。** 残る64は 魔物(22)・領域(11)・魔法(18)・魔王(10)・錬成(3)。

### 次
残りのうち大きいのは **魔法(18)** と **魔物(22)**。魔物は遊びの中心なので次はそこ。

---

## 続き22：地上ツリーのホバー修正 ＋ 配下の進化を全系統で古代種まで

### 🔍 地上ツリーでホバーが出なかった
専用ツールチップを**研究パネルの中**に作っていた。地上ツリーは別のパネル（`surfaceWindow`）なので、
親が非表示のまま＝**ホバーしても何も出ない**。
→ **`TooltipCanvas`（独立Canvas・sortingOrder 200）へ移した。**迷宮ツリーと地上ツリーの両方から使う物なので。

### 🧬 配下：行き止まりを全部なくした
**実測した偏り**
| 基本種 | 最大段 | 形態数 |
|---|---|---|
| スケルトン | 5 | 8 |
| **ゾンビ** | **1** | **2** |
| ゴースト | 5 | 6 |
| ラット | 5 | 10 |
| **バット** | **2** | **3** |
| ゴブリン | 5 | 15 |
| **インプ** | **1** | **2** |

ゾンビとインプは**1段で打ち止め**。買っても伸びないので、選ぶ意味が無い状態だった。
行き止まりは6箇所（グール/ボーンスナイパー/セイレーン/ゴブリンレンジャー/オーク/ダークエルフ）で、
すべてを古代種(段5)まで伸ばすのに **20体**要ると数えた。

**足した20体**（1段ずつ・分岐の先も古代種まで届く）
- グール系：喰屍鬼 → 屍肉喰らい → 飢餓王 → 太古の飢餓
- 骨の射手：骨弩兵 → 髑髏狙撃王 → 太古の骨箭
- セイレーン：惑いの歌姫 → 惑王 → 太古の歌
- ゴブリン狩人：ゴブリンハンター → 狩王 → 太古の狩人
- オーク：オークウォーロード → 豪腕王 → 太古の巨腕
- ダークエルフ：闇撃ち → 影の司祭 → 宵闇王 → 太古の宵闇

数値は既存の段ごとの階段に合わせた（段2 cp14-20 → 段3 cp26-32 → 段4 cp44-50 → 段5 cp60-66）。

**結果**
| | 前 | 後 |
|---|---|---|
| 全形態 | 46 | **66** |
| 行き止まり | 6 | **0** |
| 最大段が5未満の系統 | 3本 | **0本** |
| 段ごと | 7/11/10/6/6/6 | 7/11/12/12/12/12 |

### 🎨 絵
PixelLab の `create_map_object`（64×64・side・**1体1生成**）で20体。
⚠ 記憶にある安い経路を使った ―― `create_character`(pro) だと1体20〜40生成で、20体＝400〜800生成になる。
残量 1065 → 20 消費。⚠ 並列は4体ずつ（それ以上で rate limit が出る）。

⚠ **落としたPNGは `isReadable` を立てないと黙って無視される。**
Point / PPU64 / 非圧縮 / mipmap無し / Clamp / isReadable=true に揃えた。
⚠ Unity の外で置いたファイルは、**先に `AssetDatabase.Refresh` を通さないと `AssetImporter` が取れない**
（1回目は20件とも「見つからない」になった）。

### 残り
アニメ（idle/walk/hit/death）はまだ。⚠ 絵が無い状態でも1枚絵のまま動くので、壊れてはいない。
`animate_image` で 1体4生成 ＝ 20体で80生成の見込み。

---

## 続き23：K-3 本体（第4段）― 残りの63件を数え、17件を本当に繋いだ

### まず全部数えた：**残り63件は1つ残らず死んでいた**
魔法18・魔物22・領域11・魔王10・錬成3。参照走査で **63/63 が未参照**。

### 繋いだ17件（説明のとおりに動くようにした）
| ノード | いままで | いま |
|---|---|---|
| 威圧 / 不屈 / 自爆 / 石化 / 治癒 / 咆哮（6） | `DefenderAtk +3%` 等・未参照 | **その技が使えるようになる**（`MinionSkill.SkillUnlocked`） |
| 危険度 二級/準一級/一級/特級（4） | `DpYield +10%` 等・未参照 | **自分から危険度の点を積む**（20/22/22/24点） |
| 共鳴の彫像 | `DefenderHp +5%` | **トーテムの半径 +1** |
| 殲滅機構 | 未参照 | **罠のダメージ +45%** |
| 分解 / 錬金術 | 未参照 | **素材の取得 +15% / +25%** |
| 抽出 | 未参照 | **毎ターンの研究点 +10%** |
| 魔素の反芻 / 奔流 | 未参照 | **配下の経験値 +20% / +30%** |

⚠ **繋いだノードは `ResEffect.None` にした。** 効果量は繋いだ先（スキル・罠・素材・経験値・危険度）に
置いてあるので、ノードにも残すと**二重取り**になる。

⚠ **技の解禁は `m_skill2` の一括ゲートも残した。** 既存のセーブと進言を壊さないため
（`m_skill2` を取っていれば6つとも開く／個別ノードでも1つずつ開く）。

⚠ **危険度は「格上げする」とは書けなかった。** 等級は5つの入力から**導かれる値**なので、
「自分から点を積む＝わざと危険にする」形にした。実測：研究前 三級15点 → 4件中2件で **準一級57点**。

### 結果
| | 着手前 | 業 | 刻印 | 地上 | **いま** |
|---|---|---|---|---|---|
| 「+X%」型 | 127 | 108 | 83 | 64 | **47** |
| 業(Art) | 53 | 23 | 0 | 0 | 0 |
| 地上(Surface) | 19 | 19 | 19 | 0 | 0 |
| 錬成(Refine) | 3 | 3 | 3 | 3 | **0** |

### ⚠ 残り47件は「繋ぐ先が無い」もの ―― 設計の判断が要る
| 群 | 件数 | 何が問題か |
|---|---|---|
| **魔法の個別呪文**（蒸気/溶岩/転移/停止/黒の特異点…） | 12 | エンジンは**属性×階級**でしか呪文を持たない。**個別の呪文という単位が存在しない** |
| **格と位**（ハイ/グレーター/アーク/タイラント・ロード/キング/クイーン/エンペラー） | 8 | **接頭語の仕組みがコードに無い**（原作の要素だが未実装） |
| **魔王の兆し**（k_sin_* 7＋k_core/k_reprisal2/k_regen2） | 10 | 魔王側に受け口が薄い |
| **種族の理**（不死/獣/魔族 ×2） | 6 | `TraitOf(family)` はあるが数値を受ける口が無い |
| 詠唱/魔力の3段×2 | 6 | 発動速度・魔力量という軸がエンジンに無い |
| その他（連鎖の仕掛け・空間の深化 ほか） | 5 | 個別に要検討 |

**これらは「配線漏れ」ではなく「その仕組み自体がまだ無い」。**
勝手に12個の呪文システムや接頭語システムを作るのは K-3 の範囲を超えるので、
**どれを作り、どれを畳むかを決めてから**進めたい。

---

## 続き24：残47件のうち26件をどう作るか ―― 4つの設計案（2026-09-06・実装なし）

ユーザーの指示：**格と位は実装賛成、ただし「進化と差別化するために、実際に使うと上がる」形にする。**
上位（タイラント／ロード／キング／クイーン／エンペラー）は**開放条件**を設ける ――
「1度以上、地上で敵ユニットを倒した」「1度以上、敵の集落を滅ぼした」「1度以上、他ダンジョンを制覇した」。
併せて **地上の敵の動き**（虫食いをやめる・集落単位にする・湧いて出るのをやめる）と、
**盤の上のダンジョン**（野良／ボット／プレイヤー）、**魔法は属性×階級に畳みつつ蒸気・溶岩・転移・停止・黒の特異点を残す**。

📄 **設計案（アーティファクト）**：https://claude.ai/code/artifact/f613c217-0404-4ace-9b46-e26f1faf52e6

### 🔍 コードを読んで確かめたこと（設計の前提）
| 事実 | どこ |
|---|---|
| `EnemyForce.SpawnHuman` は「**自領に隣接する中立タイルからランダムに**」軍を湧かせている | `EnemyForce.cs:160` 付近 |
| `PickTarget` は「**一番守りの薄い1タイル**」を選ぶ ＝ 虫食いの正体 | `EnemyForce.cs:198` |
| `Assault` は**そのタイル1枚だけ**所有者を書き換える | `EnemyForce.cs:365` |
| **中立タイルに集落の仕組みが1つも無い**。`Settle` が付くのは自領だけ。`RegionType.Village/Town/City` は地形と深度から決まる**ただの呼び名** | `SurfaceGen.cs:316` |
| **`DiplomacySystem.Power`（独立勢力・4〜10個）が既にある** ―― regionId・name・kind・favor・suzerain・destroyed を持つ。**人類の集落の実体にする器として最適** | `DiplomacySystem.cs:53,335` |
| `MagicCatalog.Spell` は `element/rank/power/trapStatus/colorHex` のみ。**範囲も効果の種類も無い** | `MagicCatalog.cs:29` |
| `ZombieAI.AttackAdventurersInRange` は**既に範囲内の冒険者を全員ループしている**（1体で break していない）→ 範囲魔法は思ったより浅い | `ZombieAI.cs:490` |
| 盤の上のダンジョンは**こちらの1つだけ**（`RegionType.Gate`）。魔王の本拠地は `Domain` というただの硬いタイル。「入る」という行為が存在しない | `SurfaceMap.cs` |
| `.owned` は 121 箇所（意味は変わらないのでそのまま使える）／`owner == OwnerNeutral` は **22 箇所**（「無主の荒野」と「人類の領域」に割る必要がある） | 全体 |

### 🏛️ Civ VII の実仕様（今回あらためて調べた）
1. **タイルの所有は集落に属する。** 一度その集落のものになったタイルは、範囲内の別の集落に付け替えられない。国境は中心から**最大3ヘクス**。**タイルは1枚ずつ取り合うものではない。**
2. 集落は**城砦区画をすべて翻して初めて陥落**。中心は常に城砦区画。**町は中心にしか城壁を建てられない**ので落ちやすく、都市は区画の数だけ手数がかかる。
3. ユニットは**集落で生産・購入して湧く**。独立勢力は中心を**ほとんど動かない守備兵**で固め、**敵対しているものだけ**が軍を送る。

→ ユーザーの言う「そもそも civ ってそういうものだったはず」は**そのとおり**だった。

### 一｜格と位：三つの鍵で上がる
- **研究**＝天井（全体）／**開放条件**＝その個体の事績（上位のみ）／**武功**＝実際に使って貯める（個体）
- 七段：ハイ10 → グレーター30 → **アーク70（迷宮で冒険者10体）** → **タイラント140（地上で敵ユニット撃破）** → **ロード240（敵の集落を滅ぼす）** → **キング／クイーン380（他ダンジョン制覇・排他）** → **エンペラー560（他の魔王を討つ）**
- 武功は**待機では1点も入らない**（経験値との決定的な違い）
- 配るのは割合ではなく**物**：装飾品枠+1／種族技枠+1／段3で役割ごとに分岐（術者=階級上限+1・射手=射程+1・前衛=庇う・突撃=踏み込む）／段4で眷属化のLv要件が外れる／段5で軍団を率いる
- **進化との差別化**：進化＝「買う・姿が変わる・買い直せる」／格＝「使う・名前が変わる・**個体が死ぬと全部消える**」
- ⚠ セーブ：`MinionRoster.Individual` に `rank/crown/deed/deedFlags` を**末尾に**足す

### 二｜地上：人類は集落を中心に生きている
- **既存の独立勢力を人類の集落に格上げ**（村=半径1／町=2／都市=3、既存の `ClaimAround` がそのまま使える）
- **領域タイルは攻めても取れない**（略奪＝産出停止のみ）。**中心を落とすと領域が丸ごと移る**。都市は城砦区画が複数＝数ターンかかる
- ユニットは**集落の中心からしか湧かない**（上限＝村1／町2／都市4）。配置は**守備（動かない）／駐屯（領域内）／哨戒（外1〜2マス＝警戒圏）**
- 態度は集落ごと：**無関心／警戒／敵対**。敵対は**宣言されて表に出る**
- ⚠ S-3 の「地上を全部失うと迷宮を狙う」は残す（無いと「地上を捨てたほうが安全」に戻る）→ 敵対集落が0になったら最も近い集落が自動で敵対
- 他の魔王も同じ器に乗せる（`RivalLords` は今 float 1つしか持っていない）
- **4つの中で一番大きい**

### 三｜盤の上のダンジョン
- **野良の巣**（中サイズで6〜10・2〜4層・**地上に軍を出さない**）／**魔王の迷宮**（既存3体・5〜8層）／**他プレイヤー**（マルチ時）
- 攻め入る＝入口へ進軍 → 遠征を宣言（LPの範囲で軍団を選ぶ）→ **盤が相手の迷宮に切り替わる** → 最深部の魔王を討てば制覇 → **その間、留守は空いている**
- ⚠ **エンジンの向きが逆になるのが最大の難所。** 役を入れ替えるのではなく、`AdventurerAI` の「歩いて最深部を目指す」振る舞いをそのまま使い、見た目とステータスだけ配下から作る **`RaidRunner` を1本足す**。守り側は既存の `ZombieAI` がそのまま使える
- ⚠ **マルチの器をいま決める**：遠征の入力は「相手のダンジョンのスナップショット」1つだけ。ボット専用の近道を作ると後で全部やり直しになる

### 四｜呪法：属性 × 階級 × **形**
- `Spell` に **`shape`（範囲）** と **`kind`（効果の種類）** を足す。個別呪文は消えるのではなく**この軸の値になる**
- 11の形：単撃／貫き(直線3)／**熱波(3×3・継続)=蒸気**／**灼野(3×3・残る)=溶岩**／薙ぎ(扇5)=火炎嵐／泥沼(3×3・束縛)=泥濘・重圧／**縛鎖(3×3・停止)=停止**／**跳躍=転移**／壁=空間壁／**特異点(5×5・引き寄せ)=黒の特異点**／八熱・八寒(階層全体)
- 名前は自動で組む：〈階級〉〈属性〉・〈形〉＝「上級 火炎・熱波」「最上級 重力・特異点」→ **16×5×11。個別に書き足す必要がそもそも無くなる**
- **属性と形に相性**（乗れば×1.25／外れれば×0.8）→ **16属性を全部取る意味がここで初めて出る**
- 使える形の数は**役割と格**で決まる（術者は既定2つ・アークで+1・キング／クイーンで+1）
- **冒険者には形を配らない**（派生属性と同じ理由）。例外は**他のダンジョンの魔王だけ**＝遠征が難しい理由になる
- **詠唱3件・魔力3件もここで解ける**：詠唱時間（範囲が広い形ほど長い）と1波の詠唱回数（広い形ほど重い）＝**範囲魔法の値段**
- ⚠ **4つの中で一番安い**

### 作る順番（依存がある）
1. **呪法の形**（一番安い・18件が生き返る）
2. **格と位（段5まで）**（**段5で天井を打つ**。二・三が無くても完結する）
3. **地上の作り直し**（一番大きい。段4・5の開放条件がここで実る）
4. **盤の上のダンジョン**（三の上。段6・7とマルチの器）

⚠ 段6・7は三と四が無いと**永久に取れないノード**になる。大罪の刻印を終末時代のゲートに繋ぎかけて止めたのと同じ罠。

### 次にやること
**ユーザーの判断待ち**（アーティファクト末尾の「決めてほしいこと」5件）。
① キング／クイーンの排他を個体単位にするか魔王単位にするか ② 野良の巣の数 ③ 遠征中に迷宮が攻められたときの扱い ④ 格の盤上での見せ方 ⑤ 対象外にする残り（種族の理6・魔王の兆し10・その他5）

---

## 続き25：呪法①「形」を作った ―― 魔法研究の「+X%」が **18 → 0** に（2026-09-07）

ユーザーの判断が出たので設計を確定し（アーティファクト更新済み）、**作る順番の①**を実装した。

### 決まったこと（4件）
| 問い | 決定 |
|---|---|
| キング／クイーン | **排他**。個体単位なので、キングの個体とクイーンの個体を1体ずつ持つのは可 |
| 野良の巣の数 | 中サイズで6〜10。**盤の広さに追従**（独立勢力と同じく `SurfaceMap.Count` から） |
| 遠征中に迷宮が攻められたら | **遠征は総力戦・数に上限なし。その代わり連れて行った魔物は防衛に立てない。** 中断や自動撤退という特別扱いは作らない ―― 留守が薄いのは**プレイヤーが決めたこと**になる |
| 格の見せ方 | 接頭語＋縁の色（段1〜4）＋**冠の印**（段5〜7）。冠の印は **PixelLab で描き起こす** |

### 🌀 作ったもの：属性・階級に次ぐ**3本目の軸『形』**
- `SpellForm`（11種）＋ `MagicCatalog.FormDef`（半径・威力倍率・詠唱・魔力・状態異常・解禁研究・残るか）
- `Spell` に `form / radius / castTime / manaCost` を追加
- **名前は自動で組む**：〈階級〉〈属性〉・〈形〉→「上級 火炎・熱波」「最上級 重力・特異点」
- `MagicCatalog.CoversTarget`（純粋な幾何。**盤を動かさずに検算できる**ように static で切り出した）
- `SpellField.cs`（新規）＝灼野・泥沼の**その場に残る**器。⚠ `DungeonFeatureManager` には置かない（あちらは準備フェーズの構造物で、払い戻し・セーブ・配置枠を持つ）
- `ZombieAI`：範囲判定・魔力・詠唱・押し引き（特異点＝引き寄せ／隔壁＝押し返し）・跳躍

### 覆うマス数（実測）
| 形 | 単撃 | 貫き | 熱波 | 灼野 | 薙ぎ | 泥沼 | 縛鎖 | 隔壁 | 特異点 | 八熱 |
|---|---|---|---|---|---|---|---|---|---|---|
| マス | 1 | 4 | 9 | 9 | 10 | 9 | 9 | 9 | **25** | **49(全部)** |

### ⚠ 実装中に踏んで直した罠4つ（全部このセッションの実測）
1. **半径は「角まで届くか」で決める。** タイル数で書くと必ず足りない ―― 3×3の角は1.41、5×5の角は2.83。
   薙ぎ(2.2)が扇のはずなのに3×3より狭く、特異点(2.5)が5×5の角に届いていなかった。→ 3×3=1.5／5×5=**2.9**／扇=**3.0**
2. **`powerMult` は「1体あたり」で考える。** 熱波を0.70で置いたら相性(×1.25)と噛み合って
   **単撃の87%を3×3にばら撒く**形になり、相手が2体いる時点で単撃を撃つ理由が消えた（上級火炎 単撃2.00 / 熱波1.75）。
   → 広い形は**1体あたり半分以下**に。詠唱と魔力だけでは代償にならない（研究が進むと詠唱は0.35倍まで縮む）
3. **相性は「重み」ではなく「門」。** 点数で混ぜると `powerMult` の差が相性を押し切る。2度踏んだ：
   ①`相性×10+魔力` → 魔力の重さが勝ち、雷撃も影蝕も揃って八熱（相性外れ）を覚えた（20体中7体）
   ②`威力×範囲` → 貫き(0.70)が重く、呪詛の術者が**自分に乗る隔壁を捨てて**貫きを選んだ
   → **まず相性で候補を絞り、その中で扱える一番重い形を採る**
4. **16属性すべてが、少なくとも1つの形に乗ること。** 呪詛・影蝕・血魔・神聖が**どの形にも乗らず**、
   その属性の術者だけが何を撃っても常に×0.8という隠れた罰を受けていた（ゴースト「下級 呪詛・熱波」威力0.36）。
   → 神聖→貫き／血魔→泥沼／影蝕→縛鎖／呪詛→隔壁 を追加。**属性を足したらここも必ず足す**

### ⚠ ついでに直した見落とし
**続き23で足した20形態に得意属性を書いていなかった。** `default` に落ちて、
セイレーン系3体が雷撃・影系3体が火炎になっていた。→ 歌姫系=水流／宵闇系=影蝕 を追加。

### 形の散らばり（終盤・術者20体）
薙ぎ×2 隔壁×3 泥沼×4 灼野×2 貫き×1 八熱×1 縛鎖×5 特異点×2
（序盤＝蒸気まで は全員が熱波。**段が上がるほど重い形に移る**という意図どおりの弧）

### 研究ノードの付け替え（12＋6＋新1）
- 蒸気→熱波／溶岩→灼野／火炎嵐→薙ぎ／泥濘→泥沼／停止→縛鎖／転移→跳躍／空間壁→隔壁／黒の特異点→特異点／八熱地獄→八熱 の**形の解禁**に
- **形を重複させない**ため、八寒地獄・重圧は「**魔力の器 +6**」に回した
- 加速は形ではなく「**詠唱がさらに15%短くなる**」として詠唱の枝に合流
- 詠唱1/2/3 → **詠唱時間 ×0.8/0.65/0.5**（旧: 未参照の MagicPower +5/8/12%）
- 魔力1/2/3 → **魔力の器 +4/+8/+14**（既定6／旧: 同じく未参照）
- **新設 `g_pierce`「貫き」**（Dawn・前提 `g_elem_thunder`）
- ⚠ 効果量は全部 `MagicCatalog.forms` 側に置き、ノードは `ResEffect.None`（二重取りを避ける）
- ⚠ **跳躍は "持ち技" ではなく "囲まれたときの反射"** にした。`PickForm` の候補に入れると
  「跳ぶだけで何もしない術者」になる。研究があれば全術者が自動で使う＝`g_space_tele` が死なない

### 通算
| | 着手前 | K-3④まで | **いま** |
|---|---|---|---|
| ノード | 235 | 202 | **203** |
| 「+X%」型 | 127 | 47 | **29** |
| 魔法研究 | 18 | 18 | **0** |

**残り29件＝魔物研究14（格と位8＋種族の理6）／魔王研究10／領域研究5。**

### 次にやること
**② 格と位（段5まで）。** `MinionRoster.Individual` に `rank/crown/deed/deedFlags` を末尾追加し、
武功の入口を作る。⚠ **段5（ロード）で天井を打つ** ―― 段6・7は③④が無いと永久に取れないノードになる。
併せて `MagicCatalog.PickForm` に格の枠（`rankSlots`）を渡し、アークで形が1つ増えるようにする。

---

## 続き26：格②「格と位」を段5まで ―― 「+X%」が 29 → 24（2026-09-07）

作る順番の②。**格は個体ごと・使うと上がる**形にした（→ [[MinionRank]]）。

### 三つの鍵
**研究**＝天井（全体に1回）／**開放条件**＝その個体が果たした事績／**武功**＝実際に使って貯める。
⚠ **待機では武功は1点も入らない**（経験値との決定的な違い）。

| 段 | 呼び名 | 研究 | 開放条件 | 武功 | 得るもの |
|---|---|---|---|---|---|
| 1 | ハイ・◯◯ | m_rank_high | — | 10 | **装飾品スロット +1**（`accessory2`） |
| 2 | グレーター・◯◯ | m_rank_greater | — | 30 | **研究を待たずに第2段階の種族技が使える** |
| 3 | アーク・◯◯ | m_rank_arch | 迷宮で冒険者10体 | 70 | 術者=**魔法階級+1**／射手=**射程+1**／前衛=**不屈**／突撃=**吸命** |
| 4 | タイラント・◯◯ | m_rank_tyrant | **地上で敵ユニット撃破** | 140 | **眷属化のLv条件が外れる** |
| 5 | ◯◯・ロード | m_crown_lord | **敵の集落を滅ぼした** | 240 | **統率 +12** ＋ **冠の印** |

### ⚠ 段5で天井を打ってある（`MinionRank.Cap = 5`）
段6（キング／クイーン）は「他のダンジョンを制覇した」、段7（エンペラー）は「他の魔王を討ち取った」が門で、
**その仕組み自体がまだ無い**。先に開けると**永久に取れないノード**になる。
→ `m_crown_king / queen / emperor` の3件は**あえて死にノードのまま残してある**。
　 ④（盤の上のダンジョン）を作ったら `Cap` を 7 に上げて付け替える。
　 ただし **`FlagSlewLord` の印は今から立てている**（④で天井を開けたとき遡って数え直さずに済む）。

### 実測（エディタで通した）
- 研究前に50体倒しても **段0のまま**（研究＝天井が効いている）
- 研究3件を取った瞬間 `RecheckAll` で **段0→2 へ一気に**昇格
- 段4は武功144あっても**地上の事績が無いと止まる** → 地上で1体倒した瞬間に昇格
- 段4で **Lv1の個体が眷属化できる**（Lv条件の免除が効いている）
- 段5で **統率+12**、呼び名が『スケルトンアーチャー・ロード』（位は後置）
- 武功884でも **段5で止まる**（Cap が効いている）

### ⚠ 実装中に踏んで直した罠3つ
1. **研究した瞬間に既存個体が昇格しない。** `AddDeed` は `amount <= 0` で早期returnするので、
   研究を取っても**その配下が次に誰かを倒すまで何も起きなかった**（武功50・撃破50の個体が段0のまま）。
   → `MinionRank.RecheckAll()` を作り、`TryResearch` の `m_rank_*` / `m_crown_*` で呼ぶ。
2. **2枠目の装飾品の技が黙って消えた。** `AccessorySkill` が「得ている技を**1つ**返す」形だったため、
   2つ着けても1枠目しか返らなかった（棘の皮膚を着けたのに毒身しか出ない）。
   → **枠が増える以上、問いは「何を得ているか」ではなく「これを得ているか」**。
   `HasAccessorySkill(id, kind)` に置き換えた。
3. **`m_crown_lord` が Growth・tier2 のまま。** 前提を `m_rank_tyrant`（End・tier4）にしたので、
   そのままだと時代が来ても前提が開かず**永久に取れない**。→ End・tier5 に移した。

### 👑 冠の印（PixelLab・4枚）
`Assets/Resources/DungeonTale/Crowns/crown_{lord,king,queen,emperor}.png`（64×64／Point／PPU64／
非圧縮／mipmapなし／Clamp／isReadable）。ロードは1度目が**暗い輪に見えて小さいと潰れた**ので描き直した。
⚠ **冠は段5からしか出さない**（全段に印を付けると盤も一覧も記号だらけになり、「位に入った」段差が消える）。
段1〜4は**接頭語と縁の色**だけで見せる。

### セーブ
`MinionRoster.Individual` に **末尾追加**：`rank / crown / deed / deedFlags / kills / accessory2`。
既存セーブでは全部0（＝無印）になるだけで壊れない。

### 通算
| | 着手前 | 呪法①後 | **いま** |
|---|---|---|---|
| 「+X%」型 | 127 | 29 | **24** |
| 魔物研究 | 14 | 14 | **9** |

残り24件＝**魔物研究9**（位3＝④待ち／種族の理6）／**魔王研究10**／**領域研究5**。

### 次にやること
**③ 地上の作り直し**（4つの中で一番大きい）。独立勢力を人類の集落に格上げ →
領域（村1／町2／都市3）→ 集落からの生産と配置（守備／駐屯／哨戒）→ 態度（無関心／警戒／敵対）→
**集落単位の陥落**（領域タイルは攻めても取れない・中心を落とすと領域が丸ごと移る）。
⚠ 手を入れるのは `owner == OwnerNeutral` の**22箇所**（`.owned` の121箇所は意味が変わらない）。

---

## 続き27：③地上の作り直し ―― 虫食いと「湧いて出る敵」をやめた（2026-09-07）

### 作り直す前の姿（実測）
- **「人類側」という勢力がコードに存在しなかった。** あるのは中立タイルと、
  `EnemyForce.SpawnHuman` が**自領に隣接する中立タイルからランダムに湧かせる匿名の奪還軍**だけ。
- `PickTarget` は「一番守りの薄い**1タイル**」を選び、`Assault` は**そのタイル1枚**の持ち主を書き換えていた＝**虫食い**。
- `RegionType.Village/Town/City` は地形と深度から決まる**ただの呼び名**で、中身が無かった。

### いまの形（Civ VII の実仕様に合わせた）
1. **既存の独立勢力（4〜10）を人類の集落の実体に格上げ**した（`DiplomacySystem.Power` に相乗り）。
   村=半径1／町=2／**都市=3**（Civ VII の「中心から最大3ヘクス」）。
2. **版図タイルは攻めても取れない。** 攻撃＝**略奪**（産出が止まる・踏み越えて進める）。
   **中心を落とすと版図が丸ごと移る。**
3. **城砦区画**：村1／町2／都市3。全部破って初めて陥落＝**都市攻めは数ターンかかる**。
4. **兵は必ず集落の中心から湧く。** 配置は**守備（門番は動かない）／哨戒（警戒圏の中だけ）／進軍**。
5. **態度は集落ごと**：無関心／警戒／**敵対**。敵対は**宣言されて表に出る**。
6. **所有者の番号**：0=無主の荒野／1=自分／2..=他魔王／**100..=人類の集落**。
   ⚠ 100 から離したのは `IsRival`(=2以上) を壊さないため。5 などにすると人類の町が「他の魔王」扱いになる。

### 実測（3つの種で25ターン・全集落を敵対にした最悪ケース）
| 種 | 自領 開始→25T | 荒らされた最大 | 進軍 | 虫食い |
|---|---|---|---|---|
| 12345 | 19 → **19** | 0 | 3 | **なし** |
| 777 | 14 → **14** | 2 | 3 | **なし** |
| 4242 | 14 → **14** | 2 | 3 | **なし** |

**攻城の通し（都市・守りを1にして仕組みだけ見た）**
T1〜3 版図を踏み荒らして前進 → T4〜5 城砦を1つずつ破る → **T6 陥落。自領 19 → 56（+37 が丸ごと）**
格ごとの手数も設計どおり：**町=2撃／都市=3撃**。

### ⚠ 実装中に踏んで直した罠6つ（全部このセッションの実測）
1. **再入。** `DiplomacySystem.Reset()` は `BuildPowers` → `SurfaceMap.All` → `SurfaceMap.Build()` と
   **再入**してくる。`Build()` の中で版図を配ろうとしたら**まだ空のリストに配っていて**、
   集落9つすべて版図0・格も村のままだった。→ 配るのは `BuildPowers` の最後。
2. **こちらが完全に無敵になった。** 狙う先を「拠点だけ」にした途端、敵は自領の外周で足を止めて
   3ターン後に引き上げるようになった（9集落すべて敵対で25ターン、自領19タイルが1枚も減らない）。
   → **版図は通れる／奪えるのは拠点だけ**。Civ でも敵は国境の中を歩いて略奪する。
3. **通られた側に損が無かった。** 版図を通れるようにしただけでは素通りされるだけ。
   → `pillagedTurns` を足し、**荒らされたタイルは産出しない**。
4. **遠い集落が進軍の枠を独占した。** 迷宮から18ヘクスの集落が敵対しているのに、進軍していたのは
   48／31／36 ヘクスの3つ。→ `TickMuster` を**近い順**に回す。
5. **道の無い軍が枠を永久に潰した。** 海を挟んだ集落の軍が経路を見つけられず、停滞14ターンで居座り、
   3枠のうち1枠が死んでいた。→ 迷宮狙いの軍も**6ターン停滞したら畳む**。
6. **`pillaged` が二役していた。** 外れの畑を1枚荒らしただけで城壁が1つ破れ、城砦3の都市が**2撃で落ちた**。
   → `wallsBroken` を分けた。荒らすことと城を破ることは別。

### ⚠ 生成の調整
集落は**一番近い候補だけを先頭に**して置く。
⚠ depth で全部ソートしたら、9つ全部が「間隔6を満たす最小 depth」に貼りついて
**迷宮から18ヘクスちょうどの輪**になった（3種すべて 18,18,18…）。近いのは1つでよい。
いまの距離：18,19,23,25,35,37,39,47,49（seed 12345）。

### セーブ
`Power` に `grade / posture / muster / hostileSince / pillaged / wallsBroken` を**末尾追加**。
`Region` に `pillagedTurns`。③より前のセーブは `SaveSystem.Load` の最後で `HumanRealm.EnsureSeeded()` が移行する。

### 次にやること
**④ 盤の上のダンジョン**（野良の巣／魔王の迷宮／他プレイヤー）。
`RaidRunner` を1本足して `AdventurerAI` の「歩いて最深部を目指す」振る舞いを流用する。
これが入ったら `MinionRank.Cap` を 5 → 7 に上げ、`m_crown_king / queen / emperor` の3ノードを付け替える。
⚠ 遠征の入力は**相手のダンジョンのスナップショット1つ**に揃える（ボット専用の近道を作るとマルチで全部やり直し）。

---

## 続き28：④-a 盤の上にダンジョンを置いた（2026-09-07）

④の土台。**盤に「行く先」を撒き、遠征の入力を1つの型に絞った。**

### 作ったもの
- **`DungeonSnapshot.cs`（新規）** ＝ 他所のダンジョン1つぶんの写し。
  ⚠⚠ **これがマルチプレイの器そのもの。** 野良の巣もボットの魔王も他プレイヤーも、
  **渡すものはこれ1つだけ**にしてある。ここでボット専用の近道を作るとマルチで全部やり直しになる。
  ⚠ **地形は持たない**（`seed` と `floorSizes` から `DungeonGenerator` が同じ盤を組み直せる）。
  セーブが軽く、通信でも seed と数個の数字で済む。
- **`NestSystem.cs`（新規）** ＝ 盤への配置・自動生成・制覇の見返り。
- `SurfaceMap.RegionType` に **`Nest`** を末尾追加／`SaveSystem.StaticTypes` に `NestSystem`。

### 実測
| 盤の大きさ | タイル | 野良の巣 | 魔王 | 一番近い巣 |
|---|---|---|---|---|
| 極小 | 1,160 | 4 | 3 | 6 |
| 小 | 2,337 | 4 | 3 | 9 |
| 中 | 4,503 | **9** | 3 | 12 |
| 大 | 6,958 | 10 | 3 | 15 |

深いほど手強くなる（中・seed12345）：
`苔むした坑 2層 守り5 難度F 距離12` … `囁く窖 4層 守り22 難度C 距離45`
魔王の迷宮は `カンタ 5層/難度D` `アリサ 6層/難度B` `ヴェルグ 7層/難度S`。

- **自領・人類の版図に巣が乗った数＝0**（魔王の本拠地を除く）
- **同じ seed なら同じ巣になる**＝マルチの前提を満たす（実測 True）
- 制覇すると DP/素材/RP が入り、**`FlagRaidedNest` が立つ＝段6『キング／クイーン』の門**

### ⚠ 踏んだ罠2つ
1. **③と同じ再入。** `SurfaceMap.Regenerate` の中で `NestSystem.Reset()` を
   `DiplomacySystem.Reset()` の**後ろ**に置いたら、あちらが `BuildPowers` →
   `HumanRealm.EnsureSeeded` → `NestSystem.Build()` と再入してくるので、
   **撒いたばかりの巣を空にしていた**（どの盤の大きさでも巣が0個）。
   → **再入してくる Reset の前に、その相手を初期化する。**
2. **名前が重複した。** id のハッシュだけだと 10×6＝60通りしかなく、
   同じ盤に「淀んだ塚」「囁く洞」が2つずつ出た。盤の上で指して呼べない名前は名前ではない。
   → placedSoFar を見て空いている組み合わせへずらす（4種で重複0を確認）。

### ⚠⚠ 次（④-b／④-c）に入る前に見つけた危険
**遠征で盤を切り替えるとき、こちらの迷宮の配置を消してはいけない。**
`DungeonFloorManager.BuildBoard` → `DungeonGridSystem.BuildFromMap` は
**その階の配置を消す**（コード中に警告あり：「階層を1つ足しただけで既存の階の配置が全部消える」）。
なので遠征は **こちらの盤を組み替えてはならない**。
→ 階層が世界Yで `FloorSpacing=200` ずつ離れている仕組みをそのまま使い、
　 **遠征用の盤を離れた階層 index（例：100〜）に建てる**のが安全。こちらの盤と配置には一切触れない。

### 次にやること
- **④-b 遠征の編成**：誰を連れて行くか選ぶ（**上限なし・連れて行った個体は防衛に立てない**）。
- **④-c 盤の切り替えと `RaidRunner`**：`AdventurerAI` の「歩いて最深部を目指す」振る舞いを流用し、
  見た目とステータスだけ配下から作る。守り側は既存の `ZombieAI` がそのまま使える。
  ⚠ `ZombieAI` は `FindObjectsByType<AdventurerAI>` で狙いを探しているので、**狙う相手の型を1段抽象化する**必要がある。
- **④-d**：`MinionRank.Cap` を 5 → 7 にし、`m_crown_king / queen / emperor` の3ノードを付け替える。

---

## 続き29：④-b 遠征の編成 ―― 「連れて行く／守りに残す」が編成そのものになった（2026-09-07）

### ユーザーの決定をそのまま形にした
「遠征に行く魔物を選択制に。防衛用の魔物は遠征に生かせないという選択肢も生まれる。
　遠征は基本総力戦なので、遠征に生かせることのできる数に制限なし。」

→ **上限なし。ただし連れて行った個体は迷宮の防衛に立てない。**
　 だから「この個体は出す／この個体は残す」が編成そのものになり、
　 **中断や自動撤退という特別扱いが要らなくなる**（留守が薄いのはプレイヤーが決めたこと）。

### ⚠⚠ 実装の要：判定を増やさず、既存の1つに流し込んだ
`KinRoster.IsAwayFromDungeon` は **隊・ボス・在陣・反芻など11か所**が見ている唯一の問い。
遠征をここに乗せたので、**全部が自動的に正しくなった**。新しい判定を撒くと必ず片方だけ古くなる。

### 実測
```
入口から離れた場所で宣言        → False「入口まで進軍してください」
入口に立って宣言               → True
10体すべて追加                → 10体足せた（上限で弾かれない）
連れて行った個体を隊に入れる     → 0体（＝守りに立てない）
```

**難度の階段（遠征の通し）**
| 巣 | 難度 | 隊 | 結果 |
|---|---|---|---|
| 朽ちた窖 2層 | G | スケルトン×3 | 撤退 |
| 忘れられた洞 2層 | G | スケルトン×6 | **制覇** |
| 灰の窖 3層 | E | リッチ×10 | **制覇** |
| 苔むした坑 4層 | D | 破軍王×16 | **制覇** |
| ヴェルグの迷宮 7層 | **S** | 破軍王×16 | **制覇** |

＝ **進化して初めて奥の巣に手が届く**。巣の難度は距離に沿って G(12) G(19) G(22) F(25) E(34) D(45) と並ぶ。

**魔王の迷宮を制覇 → 真核を奪う**まで通った：`魔王は排除済み=True`、
率いた眷属の事績ビット＝12（巣制覇4＋魔王討伐8）。
⚠ 門は満たしているのに段は上がらない ―― `MinionRank.Cap=5` で止めてあるので**それが正しい状態**。

### ⚠ 踏んで直した罠2つ
1. **主が強すぎて一番易しい巣すら誰も落とせなかった。** `lordHpMult` は **HPの倍率**なのに
   強さの倍率としてそのまま掛けていて、難度F・2層の巣で**主だけが隊10体ぶんの6割**になっていた
   （実測 434 vs 647 → どの隊でも制覇0件）。
   → 比べ合いで解く形では**耐久は平方根で効く**（倍のHPは倍の手数ではない）。
   ⚠ `lordHpMult` の意味は変えていない ―― ④-c では本物のHPとしてそのまま使う。
2. **一番近い巣が難度Fから始まっていた。** 巣は depth 2 以上にしか置かないので
   `depth/1.6` だと最寄りでも tier 1 になり、**練習場が最初から練習にならなかった**。
   → `(depth-2)/1.8` にして最寄りを G に。

### ⚠ ここが ④-c の唯一の差し替え口
`Expedition.ResolveFloorAbstract` ―― **仮の解決**。数値で殴り合うだけで、凝った式は入れていない
（どうせ捨てるうえ、**強さを式で予想しない**という決まりに反する → [[readiness-and-trade]]）。
④-c では**離れた階層 index に遠征用の盤を建てて実際に戦わせる**。
⚠ 差し替えるのは**解決のしかただけ**。勝敗・損耗・制覇の扱いはいまの形を保つこと。

### 次にやること
- **④-c**：盤の切り替えと `RaidRunner`。
  ⚠ こちらの迷宮の配置を消さないため、**遠征用の盤は離れた階層 index（100〜）に建てる**。
  ⚠ `ZombieAI` は `FindObjectsByType<AdventurerAI>` で狙いを探しているので、狙う相手の型を1段抽象化する。
- **④-d**：`MinionRank.Cap` を 5 → 7、`m_crown_king / queen / emperor` の3ノードを付け替え。

---

## 続き30：④-c1 遠征先の盤を建てた ―― こちらの迷宮を消さずに（2026-09-07）

④-c の一番危ないところ（**遠征でプレイヤーの迷宮が消える**）を先に潰した。

### 作ったもの：`RaidBoard.cs`（新規）
- スナップショットから**遠征先の盤を建てる**。
- ⚠⚠ **こちらの盤には触らない。** 階層が世界Yで `FloorSpacing`(=200) ずつ離れている仕組みを
  そのまま使い、**階層 index 100 以降**に建てる（こちらは 0〜数枚なので決して重ならない）。
- ⚠ **敵の守りにこちらの強化を掛けない。** `DungeonFeatureManager.SpawnDefender` は
  トーテム・遺物・魔王の格・政策・興奮ツリーを全部掛けている（＝**こちらの迷宮の守り**用）。
  あれを使うと**敵がこちらの投資で強くなる**。ここは「種の倍率 × Lv × 主の硬さ」だけで組む。
- 地形は `seed` から組み直す（`DungeonGenerator.SetSeed` を追加）。

### 実測（再生中・本物の迷宮で）
```
遠征の前 B1F: 床52 入口(2,3) ボス(7,7) size10 配置0
遠征先 骨の洞 2層 建った=True 守り=6
   1層 index=100 床54 生存2 原点y=20000
   2層 index=101 床69 生存4 原点y=20200
遠征の後 B1F: 床52 入口(2,3) ボス(7,7) size10 配置0
   ★ 変わったマス=0 ／ こちらの迷宮は無傷か = True
```
**全100マスを1つずつ比べて0件**。狙いどおり、遠征はこちらの盤に一切触れない。

### ⚠ 途中で見つけて直した「元からあった」バグ2つ
1. **`DungeonGridSystem.Of()` が、盤があるのに null を返した。**
   `Boards` と `Active` は登録簿を数え直すのに、`Of()` だけしていなかったので、
   ドメインリロード直後に `Of(0)==null`（実測）。→ 空なら数え直す。
2. **再生中に再コンパイルすると `DungeonTilemapView` が落ちた。**
   フィールドだけ null に戻り GameObject は残るので、`Grid` は既に付いていて
   `AddComponent<Grid>()` が **null を返し**、次の行で NullReference。
   → **作り直す前に `GetComponent` で拾う**（層も同じ理由で拾う。作り直すと二重に積まれる）。
   ⚠ これは遠征とは無関係の元からある脆さで、**盤を建て直すたびに踏みうる**。

### ⚠ 道具の罠（新規）
**編集中は `Object.Destroy` が効かない**（次フレームまで遅延するのにフレームが進まない）。
実測：編集中に建てた遠征の盤が片付かず、**そのまま再生に持ち込まれた**（floor 100/101 が居座った）。
→ `RaidBoard.Kill` は再生中かどうかで `Destroy` / `DestroyImmediate` を分ける。

### 残り（④-c2 以降）
- **侵入側のAI**。こちらの配下を歩かせて相手の守りと戦わせる。
  ⚠ 27か所ある `FindObjectsByType<AdventurerAI>` の**ほとんどは既に階層で絞ってある**
  （`a.MyFloor != floor` / `OnCommandFloor`）ので、階層 index 100 以降は自然に除外される。
  絞っていないのは `DungeonAdventurerSpawner.AbortAndClear`（新しい周で全部消す＝問題なし）など少数。
  → **`AdventurerAI` を侵入側として使い回す道が現実的**（`ZombieAI` は既に `AdventurerAI` を敵として殴る）。
  ただし死亡処理（牢・因縁・撃破報酬）は遠征では通してはいけないので、そこだけ分岐が要る。
- 盤の表示切り替え（カメラ）とUI、`Expedition.ResolveFloorAbstract` の差し替え。
- **④-d**：`MinionRank.Cap` を 5 → 7、位の3ノードを付け替え。

---

## 続き31：④-c2 侵入側のAI ―― 盤の上で本当に戦うようになった（2026-09-07）

### 要：戦闘の中身を1行も書き換えていない
`ZombieAI` は既に `AdventurerAI` を敵として殴り、`AdventurerAI` は既に `ZombieAI` を殴る。
遠征は**役が入れ替わるだけ**なので、この2つを向かい合わせれば成立する。
→ `AdventurerAI` に**侵入者モード**を足した（`MakeRaider(individualId)`）。
⚠ 侵入者用のAIを別に書くと、狙い・射程・気性・魔法・罠…と**同じものを2セット**持つことになり必ず片方が古くなる。

### ⚠ 侵入者が通してはいけない道を2つ塞いだ
1. **`DetermineAdventurerStatus` を通さない。** あれは `WaveRoster` から**1件取り出す**ので、
   遠征に出すたびに**こちらの迷宮に来るはずだった冒険者が1人消える**＝『先触れ』で予告した波と食い違う。
   → `SetupAsRaider` で、配下個体から中身を作る。
2. **撃破処理を1つも通さない。** 死亡ブロックの中身は全部「こちらの迷宮で冒険者を倒した見返り」
   （生け捕り・因縁・撃破DP・素材・感情・実績・天啓・捕食・号令ゲージ・波の決算）。
   自分の配下が他所で倒れたのにこれが走ったら **殺されるほどこちらが儲かる**。
   → 侵入者は先頭で分岐して `Expedition.OnRaiderFell` へ。

⚠ 火力は `attackPower` ではなく **`threatAtkMult`** を通る（与傷＝`(10+Lv×0.5)×threatAtkMult`）。
Lv は基礎の項に入っているので、掛けるのは**種の強さと装備だけ**（二重に Lv を掛けない）。

### ⚠⚠ 「階を抜けた」の判定を作り直した
最初は**守りを全滅させたら**にしていたが、守りはアンカーで散らばっていて侵入者は最深部へ歩く。
実測で **隅に1体だけ残った守りに誰も近づかず、3,500フレーム経っても永久に終わらなかった**。
→ **侵入者の誰かが最深部（ボス地点）に着いたら抜けた**に変更。
こちらの迷宮でも「冒険者が階段に着いたら降りる」であって「守りを全滅させたら」ではない。
**守りは関所であって、消化すべき一覧ではない。**

### 実測（再生中・2層の巣を通しで）
```
1層: 守り2 → 0 ／ 侵入者7 生存 ／ 抜けた=True
2層: 守り4（主を含む）／ 侵入者7 ／ 最深部に到達=True
🏆 制覇=True ／ 事績ビット=4（段6の門）／ 武功=40
片付け後: 盤=1（こちらだけ）／遠征の階に残る守り=0／残る侵入者=0
B1F: 遠征の前後で床も入口も変わらない
```
**シーンの `AdventurerAI` は侵入者7・ふつうの冒険者0** ―― こちらの迷宮の勘定に漏れていない。

### ⚠ 途中で見つけて直した「元からあった」バグ
**不死がとどめを刺されると、骸が盤の外に湧いていた。**
`RaiseUndead` に渡すマスが `Start` で決めた `myGridPos` で、そのとき自分の階の盤でなかった場合
**世界座標がそのままマスに入る**。実測：原点 y=20000 の盤で倒れた不死からマス y≈20007 が渡り、
さらに原点を足されて **y≈40007＝どの盤にも無い場所**に骸が湧いた（そこで永久に生き続ける）。
→ ① 倒れた瞬間にマスを引き直す ② `RaiseUndead` は盤の外のマスを受け取らない。
⚠ これは遠征とは無関係で、**縦の迷宮でも起こりうる**元からの不具合。

### ⚠ 片付けは「一覧」ではなく「階」で掃く
盤の上には**こちらが立てた以外の駒も生まれる**（起き上がった骸／倒れて復活待ちの守り）。
控えた一覧だけ消したら **守りが9体、盤の無い階に残り続けた**。
→ 遠征の階（100以降）に居るものを全部掃く。

### 次にやること（④-c3）
- `Expedition.TickTurn` の `ResolveFloorAbstract` を**盤の解決に差し替える**。
  ⚠ 盤の戦闘は数秒かかるのでターン処理の中で即決できない。**波のフェーズに載せる**
  （こちらが攻められている裏で遠征も進む＝「留守が空く」がそのまま画になる）。
- 盤の表示切り替え（カメラ）と、遠征の編成・進行のUI。
- **④-d**：`MinionRank.Cap` を 5 → 7、位の3ノードを付け替え。

---

## 続き32：④-c3 遠征を波に載せた ―― 攻められている裏で、遠征も1層降りる（2026-09-07）

### 仮の解決を捨てて、盤の戦闘に差し替えた
`Expedition.ResolveFloorAbstract`（④-bの仮実装）を削除し、**波と同じ時間で進む**形にした。

⚠⚠ **盤の戦闘は数秒かかるので、ターンの解決の途中では即決できない。**
→ こちらの迷宮が攻められている**その同じ戦闘フェーズ**で、遠征先の戦いも進む。
　 **1つの波＝遠征の1層。**「留守が空く」がそのまま画になる。

- `StartBattlePhase` → `Expedition.OnBattleStart()`（盤を用意し、その階の守りと侵入者を立てる）
- `EndBattlePhase` → `Expedition.OnBattleEnd()`（抜けたか／全滅したかで進退を決める）
  ⚠ **`EndDescent` より前**に呼ぶ。あちらは盤の駒を撤収させるので、後に置くと
  **抜けたかどうかを数える前に侵入者が消える**。

### 実測（再生中・本物のターンループで）
```
戦闘フェーズ開始直後: 盤=3枚（floor 0 / 100 / 101）守り2 侵入者9
戦闘中(frame 23360): 1層 抜けた=True／侵入者は最深部(9,8)に到達
波を締めた後:        遠征 1/2層 → 2/2層（層が進んだ）／侵入者は撤収されて0
全滅させた場合:      遠征=終了／RaidBoard.Active=False（盤も片付く）
```

### ⚠ 足踏みの上限を入れた
抜けられなかった波は「もう一度その階へ挑む」が、**そのままだと永久に同じ階を叩き続ける**
（撤退も制覇もしないので遠征が終わらず、出した個体が二度と迷宮の守りに戻らない）。
→ **同じ階を3波抜けられなかったら引き上げる**（残り波数を通知に出す）。

### ⚠ 道具の罠（テスト時）
`StartBattlePhase` には**早期returnが複数ある**（異変に未回答／何も置いていない階がある／
既に戦闘中）。反射で叩くテストでは、これに当たると**何も起きずに戻る**ので
「遠征が動いていない」ように見える。実際には仕組みではなくゲート側で止まっていた。

### 残り
- 盤の表示切り替え（カメラ）と、遠征の編成・進行のUI。
  いまは遠征先の盤が階層 index 100 に建つだけで、**プレイヤーには見えていない**。
- **④-d**：`MinionRank.Cap` を 5 → 7、`m_crown_king / queen / emperor` の3ノードを付け替え。

---

## 続き33：④-c UI ―― 遠征を宣言し、編成し、覗けるようにした（2026-09-07）

📄 画面案（承認済み）：https://claude.ai/code/artifact/b610d783-58dd-42a2-a119-e8860c6ee691
ユーザーの判断：**3件とも提案どおり**。① 専用の窓（図鑑に間借りさせない）② 突入後は組み替えない
③ 覗いているあいだも時間は止めない。

### 作ったもの
- **地上の巣カード**（`GameUIManager.Surface.cs` の領域詳細に追加）
  巣タイルを選ぶと 種類／難度／層／守り／罠／最深部の主 を出し、`遠征を宣言` ボタン。
  眷属が入口に立っていなければ**理由を書いて止める**。
- **`GameUIManager.Expedition.cs`（新規）** ＝ 編成と進行の窓。
  左「迷宮に残す」／右「連れて行く」の二列。押すと反対側へ移る。
- **階層タブの右に『遠征』タブ**（続き32で入れたカメラ切替と対）。

### ⚠ この窓の要は「残る守り」を常に出すこと
連れて行く側だけ数えても判断できない。**残す側が空になったのが見えて初めて**
「守りを削って攻めている」という自覚が出る。0体のときは帯ごと赤くして
「この波、迷宮は無防備になる」と書く。

### ⚠ 出さないと決めたもの
**勝率・戦力比は出さない**（→ [[readiness-and-trade]] の「強さを式で予想しない」）。
出すのは相手の事実（層・守りの数・罠・主・難度）だけ。難度は**冒険者と同じ G〜S**で書く
――「強さ」の目盛りを2つ作らない。

### ⚠ 実装で踏んだ罠3つ
1. **`MakeVScroll` は `out` を取らない。** Content の `RectTransform` を**そのまま返す**。
2. **主要アクションの赤枠は第6引数 `red=true`。** 色を BLOOD にするだけでは
   Bloodlines のボタン枠にならず、押せるのに沈んで見えた（実機で確認）。
   タイトルの『この世界で始める』と同じ作法に揃えた。
3. **高さ20pxに2行は入らない。** 突入の注記が折り返して下の行が切れていた
   （→ [[ui-conventions]]）。縮められるようにして1行に収めた。

⚠ `OpenExclusive` に遠征の窓も足した。**閉じるときは `expeditionOpen` の印も下ろす**
――印だけ立ったままだと次の再描画で勝手に開き直す。

### 実機で確認したこと
`苔むした坑 野良の巣・2層・難度G` を宣言 → 窓が開き、
左「迷宮に残す 4体」／右「連れて行く 6体・上限なし」、率いる眷属は金枠で先頭、
`グレーター・レイス`『ハイ・レイス』の**接頭語が段の色で出る**、
帯に「連れて行く6体は迷宮の守りに立てない。残る守り4体」。

### 次にやること
**④-d**：`MinionRank.Cap` を 5 → 7、`m_crown_king / queen / emperor` の3ノードを付け替え。

---

## 続き34：④-d 格の階段を最上段まで通した ―― 「+X%」が 24 → 21（2026-09-07）

`MinionRank.Cap` を **5 → 7**。据え置いてあった位の3ノードを付け替えた。

⚠ 5で止めていたのは、段6・7の門「他のダンジョンを制覇」「他の魔王を討ち取る」を満たす仕組みが
無かったから（先に開けると**永久に取れないノード**になる）。④で遠征が入り、
`NestSystem.OnConquered` が `FlagRaidedNest` を、魔王の迷宮の制覇が `FlagSlewLord` を
**実際に立てるようになった**ので開けた。

### 👑 段6は「選ぶ」段にした
| | 継ぐと得るもの |
|---|---|
| **キング** | 麾下の軍団が**兵科で不利な当たりをしなくなる**（相性の**下限**が上がる） |
| **クイーン** | **統率 +20**／麾下の軍団が**自領の外でも損耗を癒せる** |
| **エンペラー**（段7） | **両方**を備え、**統率 さらに +30** |

- ⚠ **自動で決めない。** 「1つ選ぶともう片方は永久に閉じる」は**選ばせるから重い**のであって、
  役割から勝手に決めたら分岐しない分岐になる。→ 図鑑の個体行に
  『キングの位を継ぐ』『クイーンの位を継ぐ』のボタンを出し、**選ぶまで段6に上がらない**。
- ⚠ **研究は両方取れる。** 排他になるのは**個体の側**。ここを排他ノードにすると
  盤全体で片方しか存在できなくなり、「キングの個体とクイーンの個体を1体ずつ持つ」ができない。
- ⚠ 効果は**できることが1つ増える**形にした。クイーンを「毎ターン回復」にしかけたが、
  自領での回復は**元からある**ので何も増えない → 「**どこでも**癒える」に変えた。
- ⚠ キングは相性の**下限**だけ上げ、上限は動かさない。上限まで動かすと
  「有利な当たりを作る」という判断そのものが消える。

### 実測
```
段5まで進めて巣を制覇 → 段5のまま／「位を継げる ― キングかクイーンを選ぶ」
  キングを選べる=True クイーンを選べる=True
クイーンを継ぐ → スケルトン・クイーン（段6）
  もうキングを選べるか=False（永久に閉じた）／統率+32／自領の外でも癒える=True
魔王を討ち取る → スケルトン・エンペラー（段7）
  統率+62／兵科の下限×1.3／自領の外でも癒える=True
クイーンだけ研究した場合 → キング=False・クイーン=True（正しく片方だけ）
```

### 通算
| | 着手前 | 呪法① | 格② | ③地上 | **いま** |
|---|---|---|---|---|---|
| 「+X%」型 | 127 | 29 | 24 | 24 | **21** |
| 魔物研究 | 32中14 | 14 | 9 | 9 | **6** |

**格と位は 8/8 すべて配線完了。** 魔物研究に残る6件は**種族の理**（対象外と決めたもの）。
残り21件＝種族の理6／魔王研究10／領域研究5。

### 次にやること
- **通しプレイ**。①〜④が全部入ったので、一度まとめて回して壊れていないか見る。
  ⚠ 特に③地上（虫食いの停止）と④遠征（留守が空く）は**同じ波で同時に効く**ので、
  数字で確かめたいのは「地上を作り直したことで、いつ・どこから攻められるようになったか」。
- 残った「+X%」21件（種族の理6／魔王の兆し10／領域5）の扱い。

---

## 続き35：①〜④のあとの通しプレイ ―― 虫食いは消えたが、脅威も消えていた（2026-09-07）

記録：`docs/playlog_run10.md`（3周）／`docs/playlog_run12.md`（直したあと2周）

### ✅ 虫食いは止まった
**5周すべてで、自領タイルが最後まで1枚も減らなかった**（16/16、14/14、19/19…）。
③の狙いはそのまま効いている。

### ❌ ところが、脅威が一度も届いていなかった
| | 1周 | 2周 | 3周 |
|---|---|---|---|
| 終了 | T14 | T13 | T13 |
| 敵軍が湧いた | T5 | T5 | T5 |
| **荒らされたタイル** | **0** | **0** | **0** |

集落は敵対を宣言し兵も出すのに、**3周とも一度も自領に届かない**。
T14 時点で一番近い討伐隊がまだ7ヘクス手前 ＝ 到着は T17〜19 なのに、1周は T12〜14 で終わる。
**「虫食いは治ったが、地上が無害になった」** ―― 治療は成功したが患者が別の意味で死んでいた。

### 🔍 原因は3つ重なっていた（実機で盤を覗いて特定）
1. **一番近い集落が必ず18ヘクス。**
   `SurfaceGen` は **depth 3 以上でないと `Town` を作らない**のに、集落の候補を Town/City だけに
   絞っていた ＝ **村を候補から丸ごと外していた**。5つの種すべてで最寄りがちょうど18（depth3の等高線）。
   ⚠ ④-a で巣について同じ穴を潰したのに、**集落側で見落としていた。**
2. **敵対する集落が永久に1つのまま。** 態度は「略奪された」か「誰も敵対していないので担ぎ出された」
   でしか上がらず、自動運転は略奪をしないので **5周とも `敵対=1` で固定**。
3. **その1つが村なので、守備上限1・徴集6ターン。** ＝ **人類の兵が盤に1体だけ**。
   盤に出ていた敵軍7〜8体は、ほとんど**他の魔王の軍**だった。

### 🔧 直した3点
- **村も集落の候補に入れる**（`depth 1.6 ≒ 10ヘクス`）。5つの種すべてで最寄り10・
  首都の版図とは初手から接触0。**近いのは村（小さな脅威）、遠いのが都市**という並びになる。
- **名声が上がるほど敵対する集落が増える**（`1 + log(1+fame/40)`・上限4・近い順）。
  ⚠ 新しい軸は足していない ―― 名声は「世に知られた度合い」なので、
  **世界が動き出す**のがまさにその意味。対数なので終盤だけ跳ねない。
- **敵対中は守備上限が倍・徴集が半分の時間**。守っているときと戦争しているときで
  抱える兵の数も動員の速さも同じなのはおかしい。

### ✅ 直したあと（2周とも同じ形）
```
T2  敵対1                        ← 宣言が出る
T4  敵対3・敵軍2                  ← 名声で増え、兵が出る
T10 荒1  ← ★5周ずっと0だった数字が動いた
T11 荒2                          ← 版図を荒らされる
T13 荒1 → T14 荒0                 ← 去ったあと癒える
自領19タイル：最後まで1枚も減らない
```
**「奪われはしないが荒らされる」という Civ の形**が盤の上で成立した。押し寄せて→荒らして→引いて→癒える。

### ⚠ 「敵軍18」は杞憂だった（内訳を実測）
```
盤の敵軍14 ＝ 人類の進軍3（上限3で止まっている）／人類の守り5（版図と警戒圏から出ない）／他魔王6
無関心な集落は兵0。敵対した3つ（10/19/28ヘクス）だけが兵を抱えている。
```
**こちらへ向かっているのは3体だけ。** 進軍の枠は設計どおり効いている。

### 次にやること
- 1周が T12〜14 で終わる壁は**①〜④とは別の、前からある問題**（装備水準が持ち逃げで跳ねる）。
  ここは K-3 の範囲外なので、別に扱う。
- 残った「+X%」21件（種族の理6／魔王の兆し10／領域5）の扱いを決める。
  ⚠ 魔王の兆しのうち4件（暴食＝撃破DP／強欲＝素材／色欲＝感情／怠惰＝研究点）は
  **既に実在の通り道がある**（`AddMaterial` `AddEmotion` `AddRP`）。
  残る憤怒（配下の攻撃+12%）と傲慢（魔王の全能力+25%）は、以前 H-1 で撤回した
  **無条件の全体倍率**そのものなので、扱いを分ける必要がある。

---

## 続き36：装備水準の作り直し ―― 設計確定（2026-09-08・実装はこれから）

⚠ 続き（案・実装はまだ）の**改訂版**。以前の記録は「世界プール」で書いていたが、
ユーザーの原文は**個体ごと**だったので直した。**この節が最終版。**

### ユーザーの原文（チャット・原型）
> こちらの宝箱に配備している装備水準が、敵のもともと装備してダンジョンに突入してくる装備水準より
> 高いときのみ敵の装備水準が上がる。宝箱に入れる装備のグレードも意図的に高くしたり低くしたりできる
> （可変式）にすればこちらで敵の強さ上昇コントロールできる。グレード１〜１０の宝箱（ツリーで徐々に開放）。
> 宝箱のグレードのマップに配置する種類、割合も設定できる（低階層はグレード1が9割・2が1割…）。
> 配置は生成されるときのままで、開けたときにどのグレードの宝箱か処理させればいい。
> グレード３の敵がグレード１０の宝箱を獲得した瞬間グレード１０になるのはよくないから、
> 差分ぶん多く**装備水準上昇経験値**が得られる仕組み。**10たまってようやくその敵個体の装備水準が1上がる**。
> 逆に装備水準が3のやつがグレード1の宝箱を拾っても1ポイントも得られない。

### ⚠ 記録のずれを訂正した
以前の記録は `LureEconomy.gearLevel`（世界に1つの値）にポイントを積む形にしていたが、
原文は「**その敵個体の**装備水準」。**10 にした理由が「1ターンに何度も宝箱を開ける個体がいる」**
＝ 1体が溜め込むことへの手当てなので、**個体ごとでないと理由と式が噛み合わない**。
→ 個体ごとに確定。

### いまの式が跳ねる理由（構造の問題・実測つき）
```
OnGearEscaped: gearLevel += carriedGear * GearSpreadFrac(0.5)   ← 逃げた人数ぶんの「和」
GradeFromWorld: baseF = rank*0.34 + gearLevel/50                ← 50 gearLevel = 等級+1
MaxGear = 100 ／ 冒険者の等級上限 = 6（オリハルコン）
```
**和なので逃げた人数に比例する。** 人数はターンに比例するので終盤は1波で +24 動く
（実測 26 → 53.8 → 59.3 → **99.3** の3ターンで死亡／今回の通しプレイでも 88.0）。
⚠⚠ **「急に上がる」の原因は係数ではなく、和であること自体。** 係数を下げても人数が増えれば同じ。

### 確定した設計

**① 個体（原文どおり）**
```
突入時    ： その個体の等級 = GradeFromWorld(rank, 世界水準)      ← いまと同じ
宝箱を開く： points += max(0, 宝箱の等級 − その個体のいまの等級)
points>=10： その個体の等級 +1／points -= 10（端数は持ち越し）
```
⚠ 差が縮むほど入りが減り、追いついた瞬間に 0 ＝ **個体の等級は撒いた等級を超えられない。**

**② 世界＝「和」ではなく「逃げ切った装備を追いかける遅い値」**
```
波の終わりに：
  逃げ切った者の等級を昇順に並べ、**第3四分位数**を取る（nearest-rank：index = ceil(0.75*n)-1）
  目標 = その等級 × 50（世界水準の目盛り。1等級 = 50）
  速さ = 基準歩幅 × clamp(逃げた人数 / 4, 0.25, 1.0)
  世界水準 → 目標へ、その速さぶんだけ近づける（上げ下げ両方に上限）
```
⚠⚠ **最高値ではなく第3四分位数**（ユーザー判断）。最高だと**外れ値1人**に全部引きずられ
「1人も逃がすな」に逆戻りする。平均だと弱い逃走者が薄め、しかも**人数が効いて和の病気に戻る**。
第3四分位は**強い側を拾いつつ、1人の外れ値では動かない**。
⚠ 人数は**目標ではなく速さ**に効かせる。「何人逃がしたか」を意味あるままにしつつ、和にはしない。

これで3つ同時に成立する：
| | どう効くか |
|---|---|
| 急に上がらない | 1波で動ける幅に上限。歩幅8なら等級+1に**6波以上**（いまは1〜2波） |
| 上限がこちらの手にある | 個体は撒いた等級を超えられない → 目標も → **世界も超えられない** |
| 下がる道ができる | 逃がさなければ目標が下回り、**世界水準が自然に下がる** |

⚠ いまは `RecoverGear`（因縁を討つ）が**唯一の下げ道**だった。これで
「**逃がさなければ下がる**」という当たり前の道が通る。`RecoverGear` は即効の引き下げとして**残す**。

**③ 宝箱を等級1〜10に分ける（ツリーで段階解禁）**
中身の等級＝そのまま `EquipmentCatalog` の等級ラダー。
⚠ **見返りも等級に比例させる**（等級1はDPも感情もわずか／等級10は大きい）。
無いと「等級1だけ撒く」が無条件の最適解になり、選択が消える。

**④ 階層ごとに等級の配分を設定できる**
⚠ 10×階層数の表を触らせない。**「基準の等級」＋「ばらつき」の2つのつまみ**に畳む。

**⑤ 配置は生成時のまま。開けた瞬間に等級を決める（遅延解決）**
生成器に手を入れず、設定を変えたら次の波から効く。

**⑥ ⚠ 世界が勝手に武装する下限**
撒かなければ永久に敵が強くならないと「**撒かない**」が無条件の最適解になる。
名声と時代でゆっくり上がる**下限**を別に持ち、世界水準 = `max(下限, 追いかけている値)`。
下限の速さはいまの 1/5 程度。撒くのは「その下限を**自分から追い越す**」行為になる。

**⑦ 『先触れ』に次の波の装備等級を出す**
撒く等級を決めるのは事前の判断なので、相手の等級が見えないと賭けにならない。

### 触る場所
`LureEconomy`（gearLevel の更新を和→追従に）／`AdventurerAI`（個体の等級と points・
宝箱を開く処理）／`EquipmentCatalog`（等級ラダーは既存）／`DungeonFeatureManager`（宝箱の等級・
遅延解決）／`Research`（等級解禁ノード）／`Foretell`（先触れの表示）。
⚠ 階層ごとの設定と等級は**セーブに載る**。enum は末尾追加のみ。

### 承認された数値と、②の式の訂正（2026-09-08・UI承認済み）
UI案 https://claude.ai/code/artifact/d88d2a12-1b9d-47fb-baff-6b55dee8af86 （**承認済み**）

| 項 | 確定値 | 理由 |
|---|---|---|
| 歩幅（1波で動ける幅） | **0.12 等級／波**（= 6.0／100目盛り） | 等級+1 に約8波。いまは1〜2波で+1なので**5倍ゆっくり**。1周13波なら撒き続けても +1.5 が上限 |
| 速さの係数 | **逃げ切った人数 ÷ 入場した人数** | ⚠ 下の訂正を参照 |
| 目標 | 逃げ切った者の等級の**第3四分位数**（nearest-rank：`index = ceil(0.75*n)-1`） | 最高値だと外れ値1人に引きずられる（ユーザー判断） |
| 等級10の解禁研究 | **領域研究**（罠・宝箱と同じ枝） | 迷宮の設えの話。錬成（魔王の装備）とは分ける |
| ④のつまみの窓 | **全階を1枚**（案B） | ⚠ 下の訂正を参照 |

#### ⚠ 訂正1：速さの係数は「人数」ではなく「割合」（ユーザー提案・採用）
```
旧（私の案・誤り）： 速さ = 歩幅 × clamp(逃げた人数 / 4, 0.25, 1.0)
新（確定）      ： 速さ = 歩幅 × clamp(逃げ切った人数 ÷ 入場した人数, 0, 1)
```
旧は**絶対数**なので、波の人数がターンで増えると**同じ4人でも意味が変わり、終盤ほど自動的に全速**になる。
＝ また「ターンが上げている」状態に片足を突っ込んでいた。割合なら波の大小に左右されず、
意味は**「どれだけ捌けなかったか」**のまま。実例：5/20 なら旧は全速、新は 1/4 の速さ。

⚠⚠ **割合を掛けるのは上げるときだけ。** 下げにも掛けると、全滅させた波が `0 ÷ n = 0` で
**世界が凍り**、「逃がさなければ下がる」という唯一の自然な下げ道が死ぬ。
誰も持ち出していない波は**目標が下限になり、満速で下がる**。

#### ⚠ 訂正2：階層ごとのつまみは「全階を1枚」（案B・ユーザー判断）
階ごとの窓（＝表示中の階だけ設定できる形）を提案したが、却下。理由は2つとも構造的：
1. **その階を表示していないと設定できない**（階層タブを回らないと迷宮全体を組めない）
2. 「**どこかの階の最高等級 ＝ 世界水準の上限**」という、この system の最重要の1行が
   **階を回らないと分からない**。決めているのは1マスの中身ではなく**迷宮全体の勾配**なので、
   勾配が一望できる形でなければ判断材料にならない。

→ 1行 = 1階（基準の等級／ばらつき／撒く等級の帯／1つあたりの見返り）を縦に並べ、
最後に「**この迷宮全体の最高等級 = 世界の上限**」を1行で出す。

### 次の一手
**実装。**（UI承認済みなので、続き36 の①〜⑦＋上の確定値をそのまま作る）

---

## 続き37：装備水準の作り直し ―― 実装（2026-09-08）

UI承認済みの設計（続き36＋その追記）をそのまま実装した。**「和」を捨てて「追従」にした**のが全部。

### 入れたもの

**① `TreasureGrades`（新規）＝撒く等級**
階ごとに「基準の等級」と「ばらつき」の2つだけ。⚠ 10×階層数の表は作らない。
`RollCatalog(floor)` は**開けた瞬間**に呼ぶ（遅延解決）＝生成器に一切触らず、つまみが次の宝箱から効く。
- ⚠ 見返り `RewardMult` は**索引に比例させない**。等級段(7-13)は1段+6%しか強くならないので、
  索引で払うと「等級8〜10＝ほとんど武装させずに大金」という抜け道になる。
  **実際に相手へ渡る強さ**（攻撃倍率と硬さの平均）で払う。DP 17（等級1）〜 54（等級7）。

**② `LureEconomy` ＝ 和 → 追従**
```
目標 = max( 下限 ,  逃げ切った者の等級の第3四分位 × 50 )      ← 上限は「撒いた最高等級」
速さ = 歩幅0.12等級 ×（逃げ切り ÷ 入場）                      ← 上げのときだけ割合を掛ける
下げ = 歩幅そのまま（割合を掛けない）
```
`OnGearEscaped`（和）は**削除**。逃げ切りは `NoteEscapedGrade(等級)` を控えるだけで、
世界が動くのは `WaveReport.EndWave` の頭で1回呼ぶ `SettleWave()` のみ。

**③ `EquipmentCatalog.GradeFromWorld` の通貨を揃えた**
```
旧: rank*0.34 + gear/50   ← gear は「ランクに上乗せする下駄」で、目標と同じ通貨ではなかった
新: gear/50 + (rank-2)*0.34 ← gear/50 が**そのまま中央の等級**、ランクはその周りの ±
```
揃えないと「目標＝逃げ切った等級」がランクぶん**二重計上**になる。
上限も固定6をやめ、`LureEconomy.WorldGradeCap`（＝撒いた最高等級／ただし下限ぶんは常に届く）にした。

**④ 個体ごとの装備水準（原文どおり）**
`AdventurerAI.GainGearPoints`：`points += max(0, 宝箱の等級 − 自分の等級)`／10で+1段（端数持ち越し）。
⚠ 等級が動いたら硬さと攻撃を**比で掛け直す**（基準値を持ち回すと脅威度・因縁・変異を巻き戻す）。現在HPも同じ比で伸ばす。
⚠ 武器と防具で別々に引くのをやめた（「その個体の装備水準」は1つの数）。

**⑤ 研究3ノード（領域）** `d_chest_g6`(9RP/胎動) → `d_chest_g8`(20/伸長) → `d_chest_g10`(40/終焉)。
天啓は 宝箱60回／鍛造6回／ミスリル以上3回。`EurekaTracker.OnChestOpened` を新設。

**⑥ 『先触れ』に装備等級** `WaveRoster.Entry.gearGrade` を**名簿で引き終える**（湧いた瞬間に引き直すと予告が嘘になる）。
読みの深さに関係なく「来る者の装備／撒いているのは等級N〜M」を出す。⚠ 勝率は出さない。

**⑦ UI『撒く等級』（全階を1枚）** 下部バーの「等級」ボタン。1行＝1階（基準／ばらつき／撒く等級の帯／1つの見返り）、
最後に「この迷宮で撒く最高等級＝世界の上限」。表示は全部**等級**に統一した（0-200の生目盛りはプレイヤーの語彙ではない）。

### ⚠⚠ 通しプレイで見つけて直した穴2件（どちらも「下限が一度も効かない」）

**穴1：下限に永久に届かない。** 速さの割合を**下限に追いつく動きにまで掛けていた**ので、
1人も逃がさない波では速さが 0 になり、世界は下限に一生届かなかった。
→ **割合で絞るのは「逃げ切りが押し上げているとき」だけ**。下限はギルドが勝手に整えるものなので満速。

**穴2：`RecoverGear` が下限を割っていた。** 因縁を討つたびに 0 まで下げられるので、
**全員討ち取って因縁も討つと世界水準が永久に 0** ＝「撒かず・逃がさず」が無条件の最適解に戻っていた（実測で1周ぶん見えた）。
→ 回収できるのは「こちらの迷宮から出ていった物」だけ。床は `min(いまの値, 下限)`。

### 実測（自動運転2周・つまみは既定のまま＝撒く最高は等級1）

| | 旧 | 新（1周目 / 2周目） |
|---|---|---|
| 装備水準の動き | 26 → 53.8 → 59.3 → **99.3**（3ターン＝約1.5等級） | 0.00 → **0.81**（12ターン）／ 0.12 → **0.90**（17ターン） |
| 1波の最大の動き | 上限なし（人数ぶんの和） | **0.12 等級**（歩幅そのもの）を一度も超えない |
| 上下 | 上がる一方 | 単調に上がり、討ち取れば下がる |
| 終了 | T12 / T13 / T14 | **T13 / T18** |

✅ **跳ねは消えた。**「1波で何等級動いたか」が歩幅を超えることが一度も無い。

❌ ⚠⚠ **ただし壁は動いていない。** 装備水準を旧の 1/3 に抑えても **T13 で死ぬ**（2周目は T18 まで伸びたが散らばりの範囲）。
＝ **T12〜14 の壁の正体は装備水準の跳ねではなかった。** `gear-level-rework` に「壁の正体」と書いてあったのは**誤診**。
装備水準は「急に敵が強くなる」という**体感**の犯人ではあったが、死因ではない。次に測るならここから。

### 次の一手
1. **壁の本当の原因を測り直す**（装備水準は容疑者から外れた）。
2. つまみを実際に動かした場合の測定（自動運転は『等級』の窓を押さないので、撒く最高が等級1のままだった）。

---

## 続き38：壁の測り直し ―― 原因は「装備水準」ではなく**置ける物の総数**だった（2026-09-08）

続き37 で装備水準を旧の 1/3 に抑えても T13 で死んだので、容疑者を外して測り直した。

### 測り方
自動運転のログに**覗き穴を4つ**足した（表に出ない事実を見るため）。
`進言の全件＋重み` ／ `道のり`（入口→最深部のマス数）／ `道の上の守り` ／ `余ったDPで召喚した数`。
⚠ 「何が出たか」ではなく **「何が出なかったか」** を見ないと、DPを数千抱えて死ぬ理由は分からない。

### 外れた仮説2つ（先に潰した）
- ❌ **進言が召喚を勧めない。** 勧めていた（T5 83／T8 92／T11 92）。出ていたのに実行されなかっただけ。
- ❌ **道が短くて置き場が無い。** 道のり **43 マスに対して埋まっていたのは 11**。空きは32マスあった。

### ⚠⚠ 実測（1周・18ターン上限）
| | T1 | T13 | 伸び |
|---|---|---|---|
| 波の人数 | 4 | **23** | **×5.75** |
| 配置枠（置ける物の総数） | 14 | **22** | ×1.57 |
| 道の上の守り | 1 | **11**（T6から動かない） | ― |
| 配下 | 1 | **2** | ×2 |
| DP | 214 | **1237〜3425 を抱えたまま死亡** | ― |

**波は turn に比例して伸びるのに、守りの総量は 22 で止まる。** これが壁。

### なぜ 22 で止まるのか（コードで確認）
1. `DungeonFeatureManager.CheckPlacementCap` は **配下・罠・トーテム・巣・環境を1つの数で数える**。
   ＝ **頭数を増やしても、置く枠を罠と奪い合うだけ**。DPを頭数に換えても盤の上の総量は増えない。
   （A/B：余ったDPを召喚に回す腕を足しても T13 → T14 しか動かなかった）
2. 枠が増える道は `PlaceCapBase + (size-10)/10*PerStep + 研究+2×2 + 刻印+2` ＝ ほぼ**拡張だけ**。
3. ⚠⚠ その拡張は `RenownBonusAdventurers`（2段ごとに+1人）と `RenownHeroRankBias`（段×0.06）で
   **来る人数と質を同時に上げる**。＝ **広げるほど波も増える**（→ [[growth-is-a-trap]] の実測が再現された）。

**つまり守りだけを増やす道が1本も無い。** 資源が余るのは導線が細いからではなく、**行き場が無い**から。

### ⚠ まだ測っていない唯一の道
**階層追加**（枠がもう1階ぶん増える）。今回の2周はどちらも**1層のまま**終わった
（進言『階層をもう1つ増やす』は weight 74 で、成長枠を 90〜96 の面々に毎回取られる）。
枠 22 対 波 23 は「1層のときの数字」であって、多層で同じかは未測定。**次はここ。**

### 次の一手
1. **階層追加を強制した腕**で同じ測定（枠が階数に比例して伸びれば、壁は「導線」／伸びても死ぬなら「設計」）。
2. 併せて、`配置枠` が配下と罠を同じ数で数えていることの是非。
   守りの頭数と仕掛けを**別の枠**にすると「広げずに守りを厚くする」道が初めて生まれる。

---

## 続き39：壁の測り直し②ーー容疑者6つを全部潰した／残ったのは「敗北が二値」（2026-09-09）

⚠⚠ **続き38 の結論（壁＝配置枠22）は誤り。撤回する。** 腕を1本ずつ作って測り直した。
診断書 https://claude.ai/code/artifact/7ae37ca5-9d13-41c8-930a-cc5e7e23bcd0

### 潰した容疑者（腕を作って走らせた／判定＝T12〜14 の壁が動いたか）
| 容疑者 | 測ったこと | 結果 | 判定 |
|---|---|---|---|
| 装備水準 | 旧の1/3に抑えた（続き37） | 0.00→0.81（12T）。跳ねは消えた | ✕ T13で死ぬ |
| 配置枠 | 埋まっているか数えた | **19/22**。空きがある周も満杯の周も同じT13 | ✕ 死因でない |
| 頭数 | 余ったDPを召喚に回した | 配下2→**41**。撃破は **3/4 → 1/20 に悪化** | ✕ 逆効果 |
| 道のり | 道の長さと埋まり方 | **43マス中11**しか埋まっていない（空き32） | ✕ 場所はある |
| 脅威度 | 毎ターン記録 | T11まで **1.0〜1.34**、増員 **0人** | ✕ 効く前に死ぬ |
| 倒れたまま | 毎ターン全員起こした | 起こせた数 **0**（準備時に倒れている個体が無い） | ✕ 該当なし |

⚠ **配下41体で撃破が減った**のが決定的だった。41体を養う費用は鍛造から出るので**裸の41体**になる。
関所は「立っているか倒れているか」なので、弱い個体は立った瞬間に倒れ、後続はそのまま通る。
**数は質の代わりにならない。**

### ⚠⚠ 残った唯一の不変量：**記録した72波すべてで魔王HPが100%**
そして次の波で 100% → 0。**「少し破られた」という状態がゲームに存在しない。**
- 抜かれなかった波：失うもの無し／持ち越す痛み無し／合図無し
- 抜かれた波：その周のすべてを失う／合図は手遅れ

だから**閾値を上げる手当ては全部、閾値を少し動かすだけで崖は崖のまま**になる
（＝6本の腕がどれも効かなかった理由）。この形は既知の3症状を全部説明する：
「上手く守るほど痩せる」／「T12まで無傷→1波で即死」／「資源を余らせて負ける」。

### 案（崖を斜面にする）
- **D 捌ける数を見せる**（軽い・物差しになる）… 過去に捌けた最大数と先触れの人数を並べる。⚠ 勝率は出さない。
- **B 倒れるのをやめて退がる**（本命）… 瀕死の配下は死なず次の階へ。関所の二値が段階に変わる。
  ⚠ 全周とも最深到達=1＝**縦の迷宮が一度も使われていない**。これを初めて働かせる案でもある。
- **A 迷宮が傷つく** … 抜かれると荒廃が溜まり枠・産出・湧きが落ちる。地上の `pillagedTurns` と同じ発想。
- **C 玉座に段** … B と役割が重なる。B の実測を見てから。
- **E 全滅させなくてよくする** … 波の約半分が `Purpose.Conquer` で魔王直行、残りは満足して帰る。
  **捌くべき相手は最初から半分**なのに全員殺す形になっている。⚠ 方向転換なので単独で。
- ❌ **枠を分ける案は落とす。** 閾値を上げる手で、上の診断だと効かない。

### ⚠ 測定の穴（見つけたので直すこと）
**周をまたいで `MinionRoster` が残る。** 2周目が**配下53体で開始**していた。
過去の「2周目のほうが長く保った」記録はこれで説明がつく。**1周ずつ、素の状態から**測る。

### 触った所（計測のため）
`_AutoPlayHarness`：覗き穴（進言の全件＋重み／道のり／道の上の守り／余DP召喚／階／起こした／脅威度／最深）と
腕3本（`spendSurplusDp` `forceAddFloor` `ReviveDowned`）。`ZombieAI.ResurrectNow()` を公開（一括復活UIでも要る）。
`DungeonFeatureManager.HasFeatureAt(floor, cell)` を追加。

### 次の一手
**D → B の順**を薦める。⚠ D を先にやるのは、いま**効き目を測る物差しが無い**から
（無いまま案を積むと、また誤診する）。

---

## 続き40：⚠⚠ 測っていなかった軸があった ―― 巣（2026-09-09）

ユーザーの指摘「階層は増やしたの？巣や環境は配置した？」で、測定の穴が出た。**答えはどちらもノー。**

### 穴
| | 対照3周 | 腕①' |
|---|---|---|
| 階層 | **1層のまま** | 4層 |
| 巣 | **1個のまま** | 1個 |
| 環境 | **1個のまま** | 1個 |

⚠⚠ 理由は進言のゲート：
```
巣を置く   : if (fm.NestCount == 0 && dp >= 300)        ← 2個目を一生勧めない
環境を置く : if (fm.HabitatCount == 0 && ...)           ← 同上
```
＝ 前回までの全測定は、**守りの補充路を閉じたまま**走らせていた。

### 腕③：巣4つ＋環境8つ（他は素の設定・1周）
| | 対照 | **巣4つ** |
|---|---|---|
| 終了 | T12 / T13 / T14 / T15 | **T17** |
| 撃破／逃走 | 94/56・63/74 | **215/76** |
| 一人も通さず | 0〜8回 | **18回** |
| 配下（ロスター） | 2〜6 | **4〜6（変わらない）** |

例：T5 来襲18→撃破28／T6 来襲22→撃破34／T14 来襲19→撃破23。
**守りの本体がロスターの個体ではなく「巣からの湧き」になった。**

### ⚠⚠ 前回の結論のうち2つが誤りだった
1. **「頭数を増やしても効かない」** → 正しくは **ロスターの頭数は効かないが、巣の湧きは効く**。別物だった。
   41体召喚しても盤に立てるのは枠まで。**巣から湧いた個体は `features` に載らないので枠を食わない。**
2. **「守りだけを増やす道が1本も無い」** → **巣がそれ。**
   枠1つを配下に使うと **1体**。同じ枠を巣に使うと **5〜6体/波**（`NestPerWave = 2 + (level-1) + 環境`、
   巣は波をまたいで level 3 まで育つ）。**5〜6倍**。

### ⚠ ただし壁の形（二値）は残っている
魔王HPは腕③でも**全波100%**、最後はやはり1波で0。T10 に一度崩れた（来襲27→撃破2・逃走25）が、
**次の波で戻った**（15/15）―― 対照では崩れたら二度と戻らなかった。
＝ **巣は「崖を斜面にする」機能を既に部分的に持っている**（波をまたいで守りが再生するので）。
壁の位置が T13〜15 → T17 に動いただけで、死に方は変わっていない。

### すぐやるべき小さい直し（バランスではなく穴）
`GuideSystem` の巣／環境の条件を **`== 0` から「足りているか」** に変える。
いまは1個置いた瞬間に**この系統の進言が永久に消える**ので、いちばん効く軸が誰にも見えない。
→ [[nest-and-habitat]] の「スポナーは強いのに誰も置かなかった」が**まだ直っていなかった**。

### 次の一手
1. **進言のゲートを直す**（軽い・効果が大きい・バランスを変えない）。
2. そのうえで案 D（捌ける数を見せる）→ B（退がる）。⚠ 巣が入った状態を基準に測り直すこと。

---

## 続き41：魔王の第二形態（バーサーカー）―― 設計（2026-09-09・実装前）

ユーザー案。設計書 https://claude.ai/code/artifact/3948ab3b-1624-4b25-82b7-7efc3b20a46b

### 狙い
続き39 で残った唯一の不変量（**72波すべて魔王HP100% → 次の波で0**）に直接当てる。
守りが崩れた瞬間にはもう味方が全滅していて、逆転も再起も起こりようがない ―― **即死をやめ、
そこから始まる時間をつくる。**

### 形
- **ゲージ1＝いまの `maxHP` そのまま**（合計を増やさない）。⚠ こうすると
  **「これまで死んでいた瞬間」がそのまま突入点**になり、効いたかを実測と直接くらべられる。
- 割れると**第二形態**：魔王が動く／状態異常無効／反撃魔法が撃ち放題／硬直なし。
- 同時に**階層ボス全員が魔王の周りに蘇る**（HPと自動回復↑・攻撃↓＝屍）。
  癒し手がいれば魔王の回復役になる。⚠ 「ボスを倒さないと魔王を殴れない」権能は**付けない**（膠着する）。
  ◎ 副産物：ボスは各階1体なので、**深く掘った者ほど last stand が厚い＝縦の迷宮に初めて報酬が付く**。
- **喰らって配る**：魔王が倒した冒険者の強さを吸い、生きている味方と自分に配る。

### 波の結末は3つ（新しい値を作らない）
| 結末 | 条件 | 代償 |
|---|---|---|
| 軽傷 | バーサーカー中に**全滅させた** | ゲージ1が削れたまま。**噂は出ない**（生存者0＝脅威度は上がらない） |
| 重傷 | 魔王は生きたが**逃した者がいる** | ＋**ゲージ1の回復が数ターン止まる**。逃げた者は通常どおり噂を広める |
| 敗北 | ゲージ2も割られた | 従来どおり |

### 歯止め3つ
1. **逓減**：1回目100% → 2回目70% → 3回目40%…
   ⚠ **「n回で打ち切り」にしない。** 打ち切りだと3回目まで実質無敵で4回目に唐突に死ぬ＝**また二値**。
2. **ゲージ1は波ごとに25%しか戻らない**（4波無傷で満タン）。
   代償であると同時に、**「追い込まれている」が盤の外から見える唯一の警告**になる。
3. **持ち越しを分ける**：**次の1波に 40%／恒久に 10%（上限つき・加算のみ）**。
   ⚠ 当初は「その波かぎり」にしていたが、**「次のターンも絶対ピンチ」を崩すのが目的**なので
   立て直しの猶予は要る（ユーザー判断）。ただし**大半は次の1波だけ**にして貯め込めなくする。
   恒久ぶんは上限つき・**加算のみ**（倍率にすると捕食ビルドが暴れる）。
   逓減と「ゲージ1が4波かけてしか戻らない」のほうが常に重いので、**割りに行く動機は立たない**。
   ⚠ 既存 `LordStance` の捕食は**味方を喰う**（1ターン2体・加算のみ）。**別名にして混ぜない。**
- ❌ **DP消費は採らない。** 実測で**死ぬ瞬間まで DP を 1237〜3425 抱えている**（「DPは制限にならない」）。
- ❌ **突入で脅威度を上げない。** 脅威度は `OnHeroEscaped` ＝**逃げ延びた者だけ**が広める仕組みで、
  全滅させれば噂は出ない。**既存の因果がそのまま正しい**ので新しい倍率を足さない（ユーザー判断）。

### ⚠⚠ 実装で必ず踏む穴
`DemonLord.OnWaveDefended` は**毎波 `currentHP = maxHP`**、準備フェーズでも回復する。
**ここを直さないとこの案の半分が効かない**（割れたゲージが黙って満タンに戻り、警告が消える）。
`RecomputeCombatStats` の `if (currentHP > maxHP)` と準備フェーズの `currentHP <= 0f` リセットも2ゲージ対応にする。

### 触る所
`DemonLord`（2ゲージ・第二形態・回復規則）／`LordStance`（既存の捕食と分ける）／
`DungeonFeatureManager`（階層ボスの蘇生召喚）／`GameUIManager.Hud`（2本のゲージ・準備フェーズで見える所）／
`SaveSystem`（ゲージ1残量・突入回数・回復停止ターンは**状態**なので保存）。

### 次の一手
実装。効いたかは**「バーサーカーに入った波の数」と「そこから生還した数」**で測る。
⚠ 1周で判断しない／周をまたいで `MinionRoster` が残るので1周ずつ素の状態から。

---

## 続き42：魔王の第二形態 ―― 実装（2026-09-09）

設計書 https://claude.ai/code/artifact/3948ab3b-1624-4b25-82b7-7efc3b20a46b （続き41）のとおり実装。

### 入れたもの
- **`LordBerserk`（新規）**：殻の残量／突入回数／重傷の修復停止／持ち越した力。**セーブに載せた**。
- **`DemonLord`**：`phase`（1＝殻／2＝第二形態）。**ゲージ1＝いままでの `maxHP` そのまま**で合計は増やしていない。
  - `TakeDamage` の死亡分岐に **`if (phase == 1) { EnterBerserk(); return; }`** を挿した ＝ **これが「即死をやめる」1行**。
  - ⚠⚠ **`OnWaveDefended` の `currentHP = maxHP` を外した**（準備フェーズ・再配置・ロードも同様）。
    ここを直さないと割れた殻が黙って満タンに戻り、この system の半分が死ぬ。
  - `ChaseNearestHero()`＝第二形態のあいだ**追う**（いままで魔王は一歩も動かなかった）。反撃間隔は ×0.35。
    ⚠ **状態異常と硬直はもともと魔王に無い**（受ける口が1つも無い）ので、そこには何も足していない。
  - `Devour()`＝倒した冒険者の `CombatPower` を吸い、自分を癒し、同じ階の味方に配る（**加算と癒しのみ**）。
- **`DungeonFeatureManager.RaiseBossesAroundLord()`**：階層ボスを全員、魔王の傍に屍として起こす
  （HP×2.6／攻撃×0.55、`guardian: false`、**配置枠を食わない**）。
- **`ZombieAI.GraftPower()`**：配られた力を受ける（癒し＋攻撃の加算）。
- **HUD**：殻が減っていると見出しに「殻 47%」、第二形態のあいだはバーが橙。

### 実測（素の設定・1周ずつ）
| | 対照 | ①だけ | ①〜⑤ |
|---|---|---|---|
| 魔王HPの推移 | **72波すべて100%** | 100→94→100→91→100→54 | **100→79→100→91→47→36→0** |
| 第二形態 | ― | 入ったが同じ波で抜かれた | **4回入って4回とも生還** |
| 終了 | T12/13/14/15 **すべて「討たれた」** | T17 討たれた | **T22「勝敗が決した」**（討たれていない） |

✅ **即死が消えた。** 殻が割れても死なない。
✅ **警告が出るようになった。** 91 → 47 → 36 → 0 と3波かけて降りるので、事前に手が打てる。

### ⚠⚠ 実装中に踏んで直した穴2件
1. **戦闘中の自然回復が殻まで押し戻していた**（研究『自然回復』・種族『再生』）。
   0% だった殻が次の波で **61%** に戻り、唯一の警告が消えていた。
   → `NoteShell` は**下がる方にしか動かさない**。癒しはその波を戦うHPを戻すが、**傷跡は残る**。
2. **殻0%が吸い込み状態だった。** 0%だと毎波そこから第二形態に入り、毎波「逃した者がいる」＝
   重傷が再発して**永久に0%**（実測 T16〜T21 の6波すべて0%）。＝「何度も追い込まれたら意味がない」に逆戻り。
   → 止めるのは**通常の回復(25%)だけ**にして、重傷中でも **10%/波** は必ず戻す。

### ⚠ 次に確かめること（未決）
**救済が強すぎないか。** 逓減が底（15%）でも生還し、死因が「討たれた」でなくなった。
1周ずつなので断定はしない。⚠ 測るなら **`RaisedBossHpMult`(2.6) / `RaisedBossAtkMult`(0.55) /
捕食の配分(自分0.9・味方1.2)** を振って、**「燃えた回数」と「そこから生還した割合」**で見る。
生還率が10割のままなら弱める。

---

## 続き43：K-6 とUI刷新 ―― 承認済みの計画と、やる順番（2026-09-10・実装前）

承認済みの画面案3枚：
- K-6 の仕組みと画面 https://claude.ai/code/artifact/cc3ce9fb-81dc-44fc-9ce4-ced9a32cd508
- 「次の一手」が座った実画面 https://claude.ai/code/artifact/5b463545-348a-41e5-8d40-234448b3788a
- UI刷新（戦略と配置） https://claude.ai/code/artifact/aa480616-4134-475b-81be-1440f3c35efe

### なぜこの2つを同時に扱うのか
K-6 は「腹心が何を言うか」の話で、UI刷新は「それをどこで押すか」の話。
**大ボタンも進言の『そこへ開く』も、置き場所は同じ下部バー**なので、
別々にやると**同じ並びを2回組み直す**ことになる。だから1本の順番にまとめる。

### ⚠ 測り方の前提
- **UI刷新は自動運転では測れない**（ボットは画面を見ない）。人が触って判断する。
- **A-1 を入れるとボットの手が変わる**ので、**以後のバランス測定はすべて取り直し**になる。
  ＝ 第二形態の調整（D-1）は A-1 の前か、A-1 のあとに基準ごと取り直すかの二択。**あとで取り直す**を選ぶ。

### やる順番

**第1段 ― 測って効くと分かっているもの（軽い）**
1. **A-1 進言を直す**：条件を `NestCount == 0` → **「足りているか」**に。
   `why` に数えられる事実（次の波の人数／前の波で捌けた数／残り枠／費用）。**『▶ そこへ開く』**を付ける。
   ⚠ ここで「打てる手を数える」層を作る。A-2 の大ボタンは**その出力を使い回す**。
2. **C-2 波の決算を `Space`/`Enter`/外側クリックで閉じられるように**（Gemini③）。数行。
3. **C-1 ダメージ表示のメリハリ**（Gemini②）：通常／会心・スキルで大きさを変える。魔王の一撃はフラッシュ＋重いSE。

**第2段 ― 絵にする（既存を壊さない）**
4. **B-1 アイコン一式＋hover説明**。線画・24格子・線1.6・色は薄灰／選択で金の2状態。
   hoverは「名前・一行・費用/条件・ホットキー」。⚠ 畳む前にここを済ませる
   ―― 絵が弱いまま畳むと**ただ探しにくくなるだけ**。

**第3段 ― 畳む（下部バーを1回で組み直す）**
5. **B-2「戦略」「配置」に畳む ＋ A-2「次の一手」の大ボタン**を同時に。
   常時見えるのは**戦略・配置・魔物の3つ＋資源＋いま押すべき1つ**。
   トレイは**押したボタンの隣から生やす**（中央に窓を出さない）／**選んでも自動では閉じない**。
   ⚠ 『侵略開始』『ターンを終える』は**消さない・弱くしない・通せんぼしない**。

**第4段 ― 魔物**
6. **B-3 魔物＝絵のツリー**。『図鑑』→**『魔物』**に改名、大きい絵のボタン、位置は『配置』の隣。
   ノードに出すのは**ランクと費用の2つだけ**、残りはhoverと右の詳細。**召喚ボタンは詳細の中**。
   『個体』『隊』も同じ並びに揃える。
7. **B-4 掴んで置く（D&D）＋視点の長押し移動**。掴んでいるあいだ**絵が指に追従**、置ける所は緑。
   「選んで連続置き」も**残す**（右クリックで解除）。
   ⚠ 左ドラッグは配置に使うので、視点は**何もない所の左ドラッグか中ボタン**。ここは触って詰める。

**第5段 ― ジャーナル**
8. **A-3 ジャーナル本体**（4本の道・宣言・目標一覧）＋上部の進捗チップ。
   地上は**左の柱の『勝利』の中身を作り替える**（ボタンを増やさない）。
   目標は**新しい数字を作らず** `VictorySystem` の各スコアの内訳を並べるだけ。
9. **A-4 宣言した道の目標を進言に混ぜる**（1と8が繋がる）。

**第6段 ― 仕上げ**
10. **B-5 召喚の儀の演出**。排出物の**絵**を出す。演出は**3段だけ**（並／上位＝光条＋SE／最上位＝暗転して割れる）。
    ⚠ クリックと `Space` で**必ず飛ばせる**。

### 別枠（この順番の外・いつでも）
- **D-1 第二形態の調整**（実装済み・未検証）。逓減が底でも生還している。
  振るのは `RaisedBossHpMult`(2.6)／`RaisedBossAtkMult`(0.55)／捕食の配分。
  見るのは**「燃えた回数」と「そこから生還した割合」**。⚠ A-1 のあとに基準ごと測り直す。
- **D-2 K-3 の残り21ノード**（種族の理6／魔王の兆し10／領域5）
- **D-3 K-4 政策カードを研究から出す**（未着手。⚠ いま入れると**プレイヤーの手を減らす**方向なので後ろ）
- **D-4 K-5 配下の昇進**（`KinPromotion` は眷属にはあるが迷宮の配下に繋がっていない＝半分）

### 決めてある事（実装中に迷わないための一覧）
- 絵にしない物：**数字・固有名・『侵略開始』『ターンを終える』**（取り返しのつかない一手）
- アイコンごとに色を変えない（12個が別々に光ると、いまの文字バーと同じ「うるさい」に戻る）
- 大ボタンの選び方は**重み比べではなく、上から順に見て最初の1つ**
  （重みだと `装備を鍛える`(97) が居座って他が一生出てこない＝実測）
- 大ボタンの条件は**数えられる事実だけ**（空いている／足りていない／余っている）。強さを式で予想しない
- 宣言に報酬も罰も付けない（付けると「宣言＝縛り」になって選ぶのが怖くなる）
- 道具のアイコンは線画のまま、魔物のマスは PixelLab のスプライト（役割を混ぜない）

---

## 続き44：A-1 進言を直した ―― 「足りているか」と『▶ そこへ開く』（2026-09-10）

計画は続き43。承認済みの画面案どおり。

### 入れたもの
1. **`GuideSystem.Advice` に `go` / `goLabel`（文字列）**。
   ⚠⚠ **デリゲートにしてはいけない** ―― `GuideSystem` は `SaveSystem.StaticTypes` に載っていて
   静的フィールドを丸ごと写す方式なので、`System.Action` を持たせると保存で壊れる。
   キーの形は `tool:巣` / `panel:研究` / `floor:deepest` / `surface:生産`。
2. **`GameUIManager.GoToAdvice(key)`** ＝ キーを実際の画面/ツールに繋ぐ**唯一の場所**。
   押したら報告は畳む（開いたままだと行った先が下に隠れる）。
3. **進言カードに『▶ そこへ開く』**。⚠ 枠の高さも 56→84 に伸ばした
   （TMPは枠が足りないと1文字も描かない）。**25件の進言すべてに行き先を付けた**。
4. **巣／環境のゲートを `== 0` から「足りているか」へ**。
   - 巣：`必要 = ceil(次の波の人数 / 6)`（1つの巣が捌けるのは 5〜6体/波）
   - 環境：`必要 = 巣の数 × HabitatCatalog.MaxStack(2)`。3つ目は効かないのでそこで止まる
5. **`why` に数えられる事実**：次の波の人数／直近で捌けた数（`FeverSystem.Held`）／**残り枠**／費用。
   ⚠ 召喚の進言には**残り枠を必ず添える**（配下を41体にしても盤に立つのは枠までで撃破は減った）。
6. **『最下層の守りを厚くする』を殻で判定**（`hp` だと準備フェーズの一瞬の値に振り回される）。
7. **ハーネスも `go` キーで捌く**ように変更。
   ⚠⚠ 題名の部分一致は文言を直すたびに黙って壊れる
   （実測：「巣を置く」→「巣をもう1つ置く」にした瞬間 `Contains("巣を置く")` が外れた）。

### 実測（腕は全部オフ・1周）
| | 前 | A-1 後 |
|---|---|---|
| 盤の巣／環境 | **1／1 で固定**（全周） | **2／3** |
| 進言に巣が出た回数 | 1回きり（`== 0` なので） | **8回** |
| 終了 | T12〜15「討たれた」 | **T22「勝敗が決した」** |
| 殻の推移 | ― | 96 → 87 → 76 → 68 → 10（**読める勾配**） |

✅ **ゲートの直しは効いた。** 2個目以降が初めて勧められるようになった。

### ⚠ 残った問題（A-2 で解く）
**巣が3つ目まで行かない。** `装備を鍛える`(weight 97) が8巡の予算を食い切るため。
＝ 進言そのものは出ているのに、**重みの渋滞で後ろが実行されない**という前から知っている症状。
→ **A-2 の大ボタンは「重み比べではなく、上から順に見て最初の1つ」**にする決まりにしてある。
そこで解く。⚠ 人の目には巣の進言は毎ターン見えているので、**この穴はボット側の色が濃い**。

### 次
A-1 は完了。順番どおり **C-2（決算を Space/Enter で閉じる）→ C-1（ダメージ表示）** へ。

---

## 続き45：C-2 / C-1 ―― 閉じ方と、手応え（2026-09-10）

Gemini の指摘②③。⚠ どちらも**見た目の層**なので、自動運転では良し悪しを測れない
（落ちないことだけ7ターン走らせて確認した）。

### C-2 決算の閉じ方
- ⚠ **`Space` は既に効いていた**（`AdvancePhaseByHotkey` が決算を横取りしている）。足りなかったのは残り2つ。
- **暗幕（背景）を押しても閉じる**。⚠ 札は自分でクリックを受け止めるので、札の上を押してもここには届かない
  ―― 「外側を押したら閉じる」がそのまま成立する。決算は**見せるだけの窓**なので、外で閉じても失う物が無い。
- **`Enter` を足した**（`ConfirmByHotkey`）。⚠⚠ **フェーズは進めない。**
  Enter で進むと「決算を閉じたつもりで**戦闘が始まる**」事故になる。進めるのは `Space` の役目のまま。
- ついでに **収穫の演出を飛ばせるように**した（`Space`/`Enter`）。
  ⚠ 1回の `WaitForSecondsRealtime` を細かく刻んで毎回フラグを見る形に変えただけで、
  **入る資源は1つも変わらない**（演出を止めるだけ）。

### C-1 ダメージ表示のメリハリ
- **`FloatText.Damage(pos, amount, victimMaxHp, trap)`** に変えた。
  ⚠⚠ **会心の乱数は作らない。** `最大HPの 22% 以上を削った一撃`を「重い」と呼ぶ
  ―― 新しい軸を足さずに「効いた」を言い当てられる。
  - 重い一撃：文字 **×1.65**／**ばらさず真上**に出す／`hold 0.28秒`**止まってから**上がる／長く残る／低い音
  - 普通：これまでどおり左右にばらす（関所で5体が同時に殴るので、重なると読めない）
  - ⚠ **全部を大きくしない。** 全部が目立つのは何も目立たないのと同じ。
- **`FloatText.Spawn` に `hold`**（出た直後、大きいまま止まる秒数）を足した。
  ⚠ 止まっているあいだは**寿命も進めない**＝そのぶん長く読める。
- **こちら側の被弾も同じ道**に通した（`ZombieAI`）。片側だけメリハリが無いと、
  押されているのか押しているのかが読めない。
- **`ScreenFlash`（新規）**：画面が一瞬光る。`魔王の一撃` と `落石` に付けた。
  ⚠ 使うのは**1波に数回しか起きないことだけ**。毎回の殴りで光らせるとただのチカチカになり、
  本当に大きいことが埋もれる（＝いまと同じ問題に戻る）。
  ⚠ **報酬も判定も持たない**（見せるだけの層）。⚠ 時間は `unscaledDeltaTime`
  ―― 4倍速で4倍速く消えたら、速いときほど気づけないという逆のことが起きる。
  ⚠ 専用 Canvas（order 5000）。既存のUI Canvasに付けると地上/迷宮の切替で一緒に消える。

### 次
順番どおり **B-1（アイコン一式＋hover）** へ。

---

## 続き46：B-1 アイコン一式＋hover（2026-09-10）

承認済みの画面案どおり。⚠ **畳む（B-2）より先にここをやる**
―― 絵が弱いまま畳むと**ただ探しにくくなるだけ**なので。

### 入れたもの
- **`IconFactory`（新規）**：アイコンを**手続きで描く**（外部素材なし。この作品の既定のやり方）。
  - 24格子で定義し、96px に**距離場**でラスタライズ。線分と円だけを置けば、**角の丸みは勝手に付く**。
  - 線の太さ 1.6／**塗らない**／**色を持たせない**（白で描き `Image.color` で乗せる）。
    ⚠ アイコンごとに色を変えない ―― 12個が別々に光ると、いまの文字バーと同じ「うるさい」に戻る。
  - ⚠ **y を反転する**（定義はSVGと同じ下向き、テクスチャは上向き）。忘れると全部の絵が上下逆になる。
  - **35個**：戦略/配置／魔王・感情・遺物・研究・拡張・先触れ・因縁・報告・記録・保存・設定／
    トーテム・罠・巣・環境・巨大・ボス・特殊敵・宝箱・部隊・塞ぐ・掘る・消去・等級・布陣・魔物／
    資源7（DP・素材・研究点・名声・生産・枠・脅威）。
- **`IconCatalog`（新規）**：hover の文章。**名前・一行の説明・費用/条件・ホットキー**の4点を、
  **順番を変えずに**出す。⚠ **ここに数字を書かない**（呼び側が実際の値を差し込む）
  ―― 固定値を書くと、バランスを変えたときに**説明だけが古いまま残る**。
- **`GameUIManager.IconButton`**：絵1枚＋hoverで立つボタン。⚠ **絵が無い名前は文字に落ちる**
  （描き忘れても画面が壊れない）。
- **上部バー**：文字ボタン11個 → 絵に（幅 58〜68 → **34**）。
- **下部バー**：`ToolButton` を絵に（幅 84 → **40**）。
  ⚠ これで「トーテムが2行に折れる」問題そのものが消えた（文字を置かないので）。

### ⚠ 描いてみて直したもの（絵は描いてから読まないと分からない）
1枚のシートに書き出して見た（`docs/icons.png`）。**4つが誤読されそうだったので描き直した**：
| | 何に見えたか | 直し方 |
|---|---|---|
| 設定 | **太陽**（輪が細く棘が長い） | 輪を大きく（3.2→4.4）、棘を短く |
| 因縁 | 片方の刃しか見えない | 鍔をやめ、**交差＋結び目**に |
| 掘る | **V字**（弧が2本） | 頭は弧1本、柄はまっすぐ縦 |
| 名声 | **木**（茎と台座のせい） | 茎と台座をやめ、炎の外形＋芯だけに |

さらに実画面で **『報告』と『記録』が同じ絵・同じ名前**になっていたのを見つけて直した
（報告＝**吹き出し**＝人が話す／記録＝**書**＝書き留めた物）。

### 実測
起動して確認：**`Icon_` ボタン 23個・旧 `Tool_` の残り 0個・実行時エラー 0**。

### 次
**B-2「戦略」「配置」に畳む ＋ A-2 大ボタン**（同じ下部バーを1回で組み直す）。

---

## 続き47：地上のB-1 ＋ B-2「戦略/配置」に畳む ＋ A-2 次の一手（2026-09-10）

### 地上のB-1（やり残していた）
- **左の柱を絵に**（74×62 → **44×44**）。⚠ 並びも index も変えていない
  ―― `switch (surfaceMenuTab)` と `wt` がこの順に依存している（→ [[legion-system]] で index ずれを踏んでいる）。
- **アイコン14個追加**：領域・勢力・眷属・軍団・ツリー・政策・属性・外交・時代・勝利・物語・支配・幸福。
  ⚠ 描いてから見直して **外交** を描き直した ―― 握手は 96px で潰れて **M字**に見えた。
  「結ばれている」ことだけを描く＝**組んだ2つの輪**にした。
- **ヘッダーを絵＋数字のチップ列に**（支配・生産・DP・素材・研究・名声・幸福）。
  ⚠ 名前は hover が持つので、チップには数字だけ。増分は薄い括弧。
- 説明は `IconCatalog` に一本化した（`mTips` と2か所に置かない）。

### B-2 畳んだ
- **`MakeTray` / `GroupButton`**：押した入口の**隣から生える**トレイ。
  ⚠ 画面の真ん中に窓を出さない ―― 盤を見ながら選ぶ操作なので、隠れると「どこに置くか」を考えられない。
  ⚠ **選んでも自動では閉じない**（罠を10個置くのに毎回開き直すのは、いまより手数が増える）。
  ⚠ 2枚同時には開かない（両方開くと盤がほとんど見えない）。`Esc` で畳む。
- **上部**：魔王・感情・遺物・研究・拡張・報告・先触れ・因縁・記録 → **『戦略』**のトレイへ。
  ⚠ **保存と設定は畳まない**（探して開くものではなく、いつでも押せるべきもの）。
- **下部**：ツール12個 → **『配置』**のトレイへ。
- **『図鑑 →』を『魔物』の大ボタンに**（52px・紫の縁）。位置は『配置』の隣
  ―― 魔物を選んで置く、という手の流れがそのまま並びになる。⚠ ホットキーの名前は `図鑑` のまま（[Z]）。

### A-2 次の一手
- **`NextAction`（新規）**：⚠⚠ **重み比べにしない。上から順に見て最初の1つ**。
  重みで competing させると `装備を鍛える`(97) が居座って他が一生出てこない（実測）。
  条件は**数えられる事実だけ**（空いている／足りていない／余っている）。強さを式で予想しない。
  - 迷宮：罠が0 → 巣が足りない → 環境が足りない → 枠が空 → 殻50%割れ → RP余り → DP600超
  - 地上：生産の列が空 → 動いていない眷属 → 属性ポイント → 政策スロット
- **大ボタンは『侵略開始』『ターンを終える』の手前**に並ぶ。⚠ **通せんぼはしない**（横に並ぶだけ）。
  打てる手が尽きたら**隠す**（空のボタンを置かない）＝主要アクションが主役に戻る。
- 地上は**いまの紫のヒント板を押せるボタンに置き換えた**（新しい場所を作らない）。
- 押すと `GoToAdvice` に流す ―― **行き先の解釈は1か所**（A-1 で作った所）。

### ⚠ 踏んだ罠
`KinRoster.Kin.mp` は「今ターンに残っている移動力」で **-1 が満タン**（0ではない）。
`> 0` で見ると**まだ一歩も動いていない眷属を見落とす**。

### 実測（起動確認）
`BottomBar 子 28 → 17`／`TopBar 子 30 → 22`／`PlaceTray 子12`・`StrategyTray 子9` が畳まれている／
次の一手が実際に出る（迷宮「巣を置く／300 DP／いま0つ・要1」、地上「眷属を動かす」）／実行時エラー 0。

### 次
**B-3 魔物＝絵のツリー**（図鑑・個体・隊を同じ並びに）。

---

## 続き48：地上の大ボタンを作り直した（2026-09-11）

### ⚠⚠ 何を間違えたか
続き47 で地上の『次の一手』を**上の帯の中**（`w-360, 8, 176×30`）に入れてしまい、
**帯の中の小さなボタン**になって「大ボタン」の値打ちが丸ごと消えていた（指摘を受けた）。
Civ VII も、この作品の承認済み画面案も、**盤の上に浮く独立した塊**として右下に置いている。

### 直した形
- **右下に浮く独立した塊**（`SurfAction`）。⚠ 帯に縛られないので、**文字が読める大きさ**にできる
  ―― ここが帯との決定的な違い。
- 中身は縦に3つ：**「まだ打てる手がある」／▶大ボタン（268×62・文字19pt・2行）／ターンを終える（268×46）**。
  ⚠ 『ターンを終える』も一緒にここへ移した。**次の一手 → 締め**が縦に並ぶことで、
  「まだ手がある／もう無い」がそのまま上下の並びになる。
- **手が尽きたら塊ごと縮む**（154 → 62）。⚠ 『ターンを終える』は消さずに**上へ詰める**
  ―― 締めはいつでも押せる。空いた場所を残さない。
- 迷宮側の大ボタンも**文字を 14.5 → 15.5 に上げ、費用を小さく2行目**へ。

### 実測（起動確認）
`SurfAction 288×154`・右下（anchor 1,0／位置 -22,22）／子3つ（20・**62**・46）／
下の帯（`SurfaceBanner` x154〜1034）と**重ならない**（`SurfAction` x2080〜2464）／実行時エラー 0。

### ⚠ 道具の罠（また踏んだ）
ヒアドキュメント経由で `\n` を書くと**本物の改行に化ける**（→ [[tooling-traps]]）。
C#の文字列に `\n` を入れるときは **Edit ツールを使う**こと。今回もコンパイルエラーで気づいた。

---

## 続き49：B-3 魔物＝絵のツリー（2026-09-11）

承認済みの画面案どおり。⚠ **『図鑑』の中身を作り替えただけ**で、タブも家系の並びも変えていない。

### 入れたもの
- **ノードを絵に**：224×126 の文字カード → **84×84 の絵**。
  - 中身は **絵／ランク（左上）／費用（右下）／手持ちの数（左下・居るときだけ）** の4つ。
    ⚠ いままでの「スケルトン 近接 F T3 HP… 群れ 個体数 最高Lv」＝**文字7つ**をやめた
    ―― 全部載せると絵にした意味が消える。
  - ⚠ **未解禁も消さずに暗く出す**（α0.28）。先に何があるかが見えることが、育てる動機になる。
  - 絵は `MinionSprite.ByIndex`（PixelLab の1枚絵）。**66種すべて用意できている**ことを確認した。
    無い種は魔物の線画に落ちる（枠が空にならないように）。
- **`GameUIManager.CodexDetail`（新規）**：ツリーの**右に固定の柱**（260px）。
  選んだ1種の 絵・名前・ランク・役割・HP/攻/速・スキル・魔法・手持ち・説明を開き、
  **『召喚する』『進化させる』はここに置く**。⚠ 一覧の上に置くと、また文字が並ぶ。
  ⚠ 何も選んでいないときは**案内だけ出して枠は残す**（出たり消えたりすると一覧の幅が動く）。
- **hover が残りを引き受ける**（`CodexTip`）。ノードに出さなかったものは全部ここ。
- **隊のトレイも同じ並びに**：108×30 の文字 → **56×52 の絵＋隅にLv**。
  空きは「空」ではなく**点線と＋**（文字を減らすのが目的なので）。
  ⚠ 枠を伸ばしたぶんフッタも 116 → 140 に伸ばした（TMPは枠が足りないと1文字も描かない）。

### 実測（起動して確認）
ツリーのノードに**絵が乗る**／`CodexDetail 260×804`／選ぶと詳細が**Art＋6テキスト＋ボタン**に変わる
（スケルトンアーチャー＝未解禁なので『進化させる（150 DP）』が出た）／隊の枠10／実行時エラー 0。

### ⚠ 道具の罠（3度目）
ヒアドキュメント経由の `\n` がまた本物の改行に化けた。
今回は**直す道具を作った**：`scratchpad/fixnl.py`（行内の `"` が奇数＝リテラルが開いたままの行を繋ぎ直す）。
⚠ それでも**C#文字列に `\n` を書くときは Edit ツールを使う**のが本筋。

### 次
**B-4 掴んで置く（D&D）＋視点の長押し移動**。⚠ 掴む対象が絵になった（B-3）のが前提。

---

## 続き50：配置の帯も絵にした（B-3 の続き）＋『次の一手』が読めていなかった

### ① ボス任命・部隊の帯を絵の一覧に（ユーザー依頼）
「配置からボス任命とか部隊を開いた時も魔物の一覧をアイコンで並べるように。hoverしたら個体情報の詳細が出る感じで。」

- **`IndividualCell(parent, 個体id, x, y, size, dim, badge)`**（`GameUIManager.CodexDetail.cs`）を
  図鑑・隊・ボス任命の**3か所で共通**に使うようにした。
  中身は **絵／左上＝格／右上＝Lv／下＝いま何をしているか**の4つだけ。
  名前も装備も強さも**全部 hover**（`IndividualTip`）が持つ。
- **ボス任命**：130×22 の文字チップ → **52×52 の絵**。帯そのものを **46 → 80** に伸ばした
  （⚠ 伸ばし忘れると TMP は1文字も描かない）。
- **部隊**：同じ 52px の絵に。見出しは帯の高さ（44/60）から**真ん中に置き直す**。
- ⚠ **使えない理由を hover から消さない**。
  「B1F の隊に編成済み」「地上に出ています」は**個体の詳細の前**に足す
  ―― どれを外せば任命できるかが、そこで分からないと意味がない。
  任命できる個体には逆に**継ぐ魔神の名と加護を後ろに足す**（置き換えない）。

### ② 絵にしたら出た不具合（1回目のスクショで発覚）
- **`B1F隊` が2行に折り返して絵に重なっていた**。バッジ枠が 16px しかなかった。
  → **四隅を役割で分ける**（左上＝格／右上＝Lv／下＝バッジを幅いっぱい）＋折り返しを切る＋自動縮小。
  ⚠ 52px の枠では**同じ隅に2つ置くと必ずぶつかる**。

### ③ ついでに見つかった本物のバグ ―― 『次の一手』の大ボタンが読めない
同じスクショの右下で見つけた。**A-2 で完了と報告していた場所**。

- **文字が重なっていた**：ボタン 168px に「▶ 罠を置く」＋添え書きを**同じ行**に入れていたので、
  折り返して2行目が1行目に重なっていた。→ 幅 **168 → 236**、添え書きは**次の行**へ、折り返しを切る。
- **⚠⚠ 文字がほぼ見えていなかった**：`PrimaryButton(…, bg=#e3a94a, fg=#1a1206, red:true)` と書いていたが、
  `SkinButton` が Bloodlines の**赤い枠スプライトを被せる**ので、**渡した琥珀色の下地は出ない**。
  下地が暗いままなのに文字は黒（#1a1206）＝暗闇に黒を書いていた。
  → **下地が実際にどうなったかを見てから文字色を決める**（`BtnSkinned` / `NextFg` / `NextNoteHex`）。
  **地上の大ボタンも同じ色を渡していた**ので、同時に直った。

### 実測（起動して確認）
ボス任命＝6体ぶんの絵（`Ind_1`〜、52×52、間隔4、内容幅344）／地上に出ている個体は暗く＋「地上」／
隊に居る3体は「B1F隊」／選べる2体は緑のLv＋選択で金枠。
部隊＝3マス・見出しは中央・選択に金枠。
迷宮の大ボタン＝「▶ 罠を置く／まだ盤に何も無い」が2行で読める。
地上の大ボタン＝「▶ 生産を選ぶ／迷宮前の荒れ地 の列が空」＋「ターンを終える →」。
コンパイルエラー 0。

### ⚠ 道具の罠（4度目）
また `\n` が本物の改行に化けた（`GameUIManager.Hud.cs`）。**Edit ツールで直した**。
決まりを繰り返す：**C#文字列に `\n` を書くときは Edit ツールを使う**。

### 次
**B-4 掴んで置く（D&D）＋視点の長押し移動**。
⚠ 迷宮の『次の一手』は**まだ帯の中**にいる（地上だけ独立した大ボタンになっている）。
そろえるかどうかはユーザーの判断待ち。

---

## 続き51：地上で大ボタンに従うと、ターンを終えた瞬間に画面が消えるバグ

ユーザー報告：「地上で進言（大ボタン）に従って指示されることすべてこなした後、
ターンを終えるを選ぶと**絶対** display no camera になる」。

### 原因（再現して確定）
`SetSurfaceMode(true)` が、**畳んだカメラを覚える前に `foldedCameras.Clear()` していた**。
集め直すループは `if (!c.enabled) continue;` で「いま有効なカメラ」だけを拾うので、
**さっき自分で無効にした本カメラは拾えない**。＝2回目に地上へ入った瞬間に記録が空になる。

| 手順 | folded | Main Camera |
|---|---|---|
| 素の状態 | 0 | **ON** |
| 地上へ | 1 | off |
| もう一度 地上へ | **0** ← 記録が消える | off |
| ターンを終える | 0 | **off**（戻す相手が居ない）→ No cameras rendering |

⚠ この「2回目」は例外的な操作ではない。**地上の大ボタンは地上に居るまま押す**もので、
`GoToAdvice("surface:…")` が `SetSurfaceMode(true)` を無条件に呼んでいた。
つまり A-2 で大ボタンを入れた時点から、**大ボタンを1回でも押せば必ず踏む**道になっていた。

### 直したもの（2か所・どちらか片方でも直るが両方入れた）
1. **`GameUIManager.Surface.SetSurfaceMode`**：`Clear()` をやめ、`Contains` で重複を避けて**足すだけ**にした。
   毎回まわすので、あとから増えたカメラも拾える。
2. **`GameUIManager.Overlay.GoToAdvice`**（`surface` 分岐）：`if (!surfaceModeOn)` で守った
   （`JumpToRegion` と同じ形）。入り直すと曲が鳴り直し・ツリーが閉じ・盤が寄り直すのも止まる。

### 実測
大ボタン4種（生産／眷属／属性／政策）を順に押してから『ターンを終える』→ **Main Camera=ON**。
守りを外した最悪の形（`true` 3連発→`false` 2連発）でも**有効カメラが 0 になる瞬間は無い**。

### ⚠ 残っている別件（未対応・判断待ち）
コンソールの警告：`DungeonFloorManager.floorTouched は readonly なのでセーブに乗りません`。
中身は**波ごとにリセットされる一時状態**（波の頭で全 false → B1F だけ true）なので、
`[NonSerialized]` を付けて黙らせるのが筋に見える。⚠ セーブの形に触るので手は付けていない。

---

## 続き52：B-4 掴んで置く＋置ける所が緑＋盤を掴んで見回す

### ⓪ 先に片付けた警告
`DungeonFloorManager.floorTouched` / `advOnFloor` に `[System.NonSerialized]` を付けた。
どちらも**波の頭で全部消してから数え直す一時状態**（`RecountOccupancy` は4回/秒）なので、
セーブに乗せる物ではない。⚠ `readonly` を外して保存する方ではない ―― そこを間違えると
セーブの形が変わる。

### ① 置ける所の判定を1本にした（これが B-4 の土台）
⚠⚠ **同じ4行が6か所に写経されていた**（準備フェーズ・壁・既に何かある・配置枠）。
`DungeonFeatureManager.PlaceCellOk(cell, out why)` に集約し、6か所ともそこを呼ぶようにした。
そのうえで**見せるためだけの問い合わせ** `CanPlaceAt(toolMode, cell, out why)` を生やした。

- ⚠ ここを分けないと「緑に光ったのに置けない」が生まれる。**何も出さないより悪い**。
- ⚠ `CanPlaceAt` は**解禁やDPは見ない**。マスの話と持ち物の話を混ぜない
  ―― DPが無いだけで盤が真っ赤になっても、なぜ置けないかは伝わらない。そちらは通知が言う。
- 🧹 消去(10)だけは逆向き＝**何か在るマス**が対象。
- 🏛️ 巨大(17)は 4×4 の専用判定に流す。⚠ そちらは枠を見ないので、ここで足した。

**実測（10×10・枠0/14）**：罠/トーテム/巣/ボス/特殊敵/部隊/誘導宝箱/環境＝**47マス**、
消去＝0、巨大＝**0**（10×10 では 4×4 が1か所も取れない ―― 既知の実測と一致）。
罠を1つ置くと 47→46、消去 0→1、同じマスは「そのマスには既に何かある」。
壁は `CanPlaceAt` も `TryPlaceTrap` も揃って False。

### ② 置ける所を緑に（`PlacementOverlay` 新規）
⚠⚠ **塗り潰し → 枠に変えた**。ここが今回いちばん学んだところ。

- はじめ α0.20 の塗り潰しにしたら、**明るい床の上でほぼ消え**、タイルの隙間にだけ緑が残った。
- 濃くして α0.30 にしたら、今度は**迷宮が丸ごと緑になった**（置ける所＝床のほぼ全部なので当然）。
- ⚠ これは**濃さの問題ではなく形の問題**だった。→ `MarkerArt.CellRing()`（四角い枠）を足し、
  **枠＝置ける／塗り潰し＝いま指している1マス**に分けた。床の絵が残り、しかもはっきり読める。

### ③ 掴んで置く（`UIDragPlace` 新規）
ストリップのマスを**掴んで盤に落とす**。掴んだ絵が指に付いてくる。

- ⚠ 掴んだ瞬間に**押したときと同じ選択**を走らせる（`onGrab`）。選ぶ道を2本にしない。
- ⚠ 落とし先の解釈は `GridInputHandler.DropAtScreen` → `PlaceWithCurrentTool` に渡す。
  **配置の振り分けは1か所**（盤のクリックも掴んで落とすのも同じ道を通る）。
- ⚠ 影は `raycastTarget = false`。true にすると自分でポインタを遮り、**絶対に置けない**。
- ⚠ **UIの上で離したら置かない**（帯の中で手が滑っただけでDPが飛ぶ）。
- ⚠⚠ **帯は横スクロールする。**マスがドラッグを全部食うと指で帯を送れない
  （マウスはホイールがあるが、タッチには無い）。→ **横に払ったらスクロールへ渡す／
  上下に引いたら掴む**。帯は下、盤は上なので「上へ引く＝盤へ運ぶ」は素直。

**実測**：罠1/トーテム3/ボス1/特殊敵0/部隊3/環境3/巨大2 のマスが掴める（解禁と手持ちのとおり）。
影＝`char_skeleton`、指に追従、raycast False。
ScrollRect の中のマス（ボス任命）は**横に払うと渡す=True／上へ引くと渡す=False**。
`DropAtScreen` は 盤の上=置く（枠0→1）／同じマスに再度=増えない／盤の外=False。

### ④ 盤を掴んで見回す（左ドラッグ）
⚠⚠ **配置と競合する**。同じ左ボタンが「置く」でもある。
→ 置く側を **押した瞬間 → 動かさずに離した瞬間** に変えて解いた（タッチは元からこの形）。
`CameraController.HandleTouchPan` を `HandleDragPan` に広げてマウスでも掴めるようにした。

- ⚠ 動いたかどうかの判定は**置く側が持つ**（`tapMoved`）。2か所に置くと必ずずれる。
- ⚠ `tapFromBoard`＝**押し始めが盤だったか**。UIのボタンから盤へ指を滑らせて離しただけで
  置けてしまうのを防ぐ。
- ⚠⚠ **畳まれているカメラは動かさない**。地上に居るあいだ迷宮のカメラは `enabled=false`
  なので、素通りさせると**見えていない迷宮の視点が地上のドラッグでずれていく**。

### 次
**A-3 ジャーナル本体**（4本の道・宣言・目標一覧＋進捗チップ／地上は『勝利』の中身を作り替える）。
残り：A-4 宣言した道の目標を進言に混ぜる／B-5 召喚の儀の演出。
⚠ 迷宮の『次の一手』は**まだ帯の中**（地上だけ独立した大ボタン）。そろえるかは判断待ち。

---

## 続き53：掴んで置くが一度も動いていなかった ― 掴んだ瞬間に、掴んでいるマス自身を消していた

ユーザー報告：「掴んで置くができないけど？」。**そのとおりで、B-4 は動いていなかった。**

### 原因
`UIDragPlace.OnBeginDrag` が `onGrab()` を呼ぶ。その中身を
「押したときと同じ処理」＝ **選択 ＋ `Refresh*Strip()`** にしていた。

`Refresh*Strip()` は帯の中身を **`SetActive(false)` してから `Destroy`** する。
つまり**いま掴んだマス自身が、掴んだその瞬間に非アクティブになる**。
EventSystem は `pointerDrag` が非アクティブになるとドラッグを捨てるので、
`OnEndDrag` が**二度と来ない** ―― どこに落としても何も起きない。

```
掴んだ直後: activeInHierarchy = False   ← これ
```

⚠⚠ **なぜ実装時に気づかなかったか**（ここが今回の本当の学び）
検証で `OnBeginDrag` → `OnDrag` → `OnEndDrag` を**自分で順に呼んで**通ってしまった。
`Destroy` はフレーム末なので、同じフレームの中で全部呼べば最後まで動く。
**EventSystem が間に立ったときだけ落ちる**経路を、素通りして測っていた。
→ **部品を直接叩いて「動いた」と言わない。**間に立つ仕組み（EventSystem・入力モジュール）の
  振る舞いが本体なら、そこを含めて測る。今回は「掴んだ直後にマスが生きているか」が正しい指標だった。

### 直したもの
`UIDragPlace` を **`onGrab`（選ぶだけ）** と **`onDone`（落としたあとの作り直し）** に分けた。
7つのストリップすべてで、ボタンの `onClick` は従来どおり「選ぶ＋作り直す」、
掴んだときは「選ぶ」だけにして、作り直しは `OnEndDrag` の最後に回した。

### 実測（EventSystem 経由の形で測り直した）
- 罠：①掴んだ直後 **マスは生きている=True**（直す前は False）／②影が盤のマスまで追従／
  ③落とす → **枠 0→1**・影が消える・そのマスは「既に何かある」
- ボス任命（**横スクロールの中**のマス）：掴んでも生きている／盤に落として **枠1→2・ボス居る=True**
- **帯の中で離す** → UI当たり3 → **枠 3→3（置かれない）** ＝ 取り消せない出費を守れている

---

## 続き54：A-3 ジャーナル ＋ A-4 宣言した道を進言に混ぜる

画面案は承認済み https://claude.ai/code/artifact/6949ca9c-eb91-44c6-ac85-0356c4d5ce02

### なぜ作り替えたか
『勝利』は「自分 124 ／ 必要 342」までは見せるのに、
**その点をどう上げるのかがどこにも書いていなかった**。見ても次の手が決まらない表だった。

### A-3 入れたもの
1. **`VictorySystem.Breakdown(道)`** ―― 点の出どころ（`label` / `unit` / `amount` / `points` / `go`）。
   ⚠⚠ **新しい数字は1つも作っていない。**`Score(Self, …)` が足していたものを分解しただけ。
   逆に **自分のスコアは内訳の合計から作る**（`SelfScore`）。
   式を2か所に書くと「目標を達成したのに点が動かない」が生まれる。
   **実測：4本とも 内訳合計 == Score。**
2. **宣言**（`VictorySystem.Declare` / `DeclaredPath`）。同じ道をもう一度押すと取り消し。
   ⚠ `VictorySystem` は `SaveSystem.StaticTypes` に載っているので **int で持つ**（デリゲート禁止）。
   ⚠ `Reset()` に足すのを忘れない。
3. **ジャーナル本体**（地上『勝利』の中身）：宣言の帯 → 宣言した1本だけ開いて内訳 →
   残りは行のまま → 勢力ごとの点を1枚の表に畳んだ。⚠ **ボタンは増やしていない**（左の柱は12個のまま）。
4. **内訳の各行に『▶ そこへ開く』**。名声のように開く先が無いものがあったので、
   `GoToAdvice` に **`dungeon:`**（地上を畳んで盤に戻るだけ）を足した。
5. **迷宮の上部バーに『道・恐怖 124/342』のチップ**。hover で内訳、押すと『勝利』へ。
   ⚠ **宣言していないあいだは出さない** ―― 常時見える物を黙って太らせるのは畳む方針の逆。
   ⚠ **毎フレーム数えない（0.5秒に1回）**。`ThresholdFor` は5勢力ぶんを計算し、
     自分のぶんは `Breakdown`＝地上の全タイル走査を通る。

### A-4 入れたもの
`GuideSystem` に **宣言した道の進言を1件**。⚠ 出すのは数えられる事実だけ
（いまの点／要る点／**いちばん手を付けていない項目**）。
「この項目が効率いい」は予想であって事実ではないので言わない。

⚠⚠ **既存の3件を押しのけない。**`grow` と同じく重みでは 76〜99 の渋滞に負けるので枠で解くが、
`grow` のように**奪う**のではなく **席を1つ増やす**（3→4）。宣言していない人の画面は今までどおり3件。

⚠ ボタンの幅は 128px しかない。項目名をそのまま入れると『感情に注いだ数へ』が入りきらず、
TMPは枠が足りないと1文字も描かない。→ `ShortGo` でキーの後ろ半分だけ使う（「▶ 感情」）。

### 実測
- 内訳合計＝Score：制圧32/32・恐怖0/0・経済2/2・革新6/6 **4本とも一致**
- 宣言 → チップ「道・恐怖 124/342」、hover に3行の内訳＋閾値＋保持
- 同じ道をもう一度 → 宣言解除・チップが消える
- 進言：**宣言なし3件／宣言あり4件**（既存3件はそのまま、4件目に「恐怖の道を進める」）
- 道を変えると進言も追従（革新→「研究済みのノード」0点を指す／go=`panel:研究`）
- 『▶ 迷宮へ』で地上が畳まれ、有効カメラ1（→ 続き51 の直しが効いている）

### 次
**B-5 召喚の儀の演出**（絵を出す・3段・必ず飛ばせる）で10段の最後。
そのあとは別枠の **D-1 第二形態の調整**（⚠ A-1 以降で手が変わっているので基準ごと測り直す）。

---

## 続き55：B-5 召喚の儀と行商人 ―― 2つを個体タブから出して「場所」にした

画面案 https://claude.ai/code/artifact/30dced39-a936-4180-8fad-c387af03b68a （承認済み）
⚠ ユーザー追加指示：**値引き・再入荷はあってよい**（ただし連続で並ばない／一時的／最大3割）、
**売り物とガチャの排出物に絵を付ける**。

### 🎨 絵（PixelLab・16生成）
既存の型どおり `create_map_object` 48×48 `view=side` `outline=single color outline`（→ [[pixellab-pipeline]]）。
- **装飾品14種** → `Resources/Accessories/acc_<id>.png`（`AccessorySprite` で引く）
- **背景2枚** → `Resources/Scenes/bg_ritual.png` / `bg_merchant.png`（`create_image_pixflux` 400×240）
  ⚠ 生成の指示に **「中央／棚は空のまま」** を入れてある。品物も魔法陣もUI側で重ねるので、
    描き込ませると二重になる。
  ⚠ 露店の絵の**下端に生成物の署名文字**が入っていたので 12px 切り落とした（240→228）。
- 取り込みは Point / PPU16 / 非圧縮 / mipmap無し / Clamp / **isReadable=true**（既知の罠）。

### ✦ 排出物が見た目で分かるようになった
ガチャの結果カードは **等級の文字（F/E/D…）を大きく出すだけ**だった。
→ `MinionSprite.ByIndex` の**姿**に差し替え、等級は左上へ小さく。
**実測：10連で10枚とも絵つき**（スケルトン／ゴブリン／バット／ゴースト／インプ／ラット が一目で分かる）。
⚠ 絵が無い種は従来の等級の文字に落ちる（枠が空にならない）。配下66/66・ユニークは引ける6種すべて絵あり。

### 🛒 店の規則（ユーザー指示ぶん）
- **連続で並ばない**：`prevStock` を覚えて次の回の抽選から外す。同じターン内の重複も外す。
- **再入荷はある**：外すのは「前の回」だけなので、いつかまた並ぶ。
- **値引きは一時的**：品揃えと一緒に消える（引き直せば無くなる）ので、恒久的に安い品は生まれない。
  ⚠ 恒久にすると「いま買うべきか」の判断が「安くなるまで待つ」に化ける。
- **値引きは多くても1枠・最大3割**（10/20/30%、小さいほど出やすい・35%の確率）。
  ⚠ 毎回付けると「安い日」という出来事が消える。
- 支払いも表示も `MerchantShop.PriceOf` **1か所**を通す。

**実測（100ターン・300個）**：前回と同じ品が並んだ **0**／同じターンに重複 **0**／
値引きが出たターン **31/100**・2枠以上 **0**・最大 **30%**／解禁6種が各50回ずつ再入荷。

⚠⚠ **最初の計測は何も測っていなかった。** T1は装飾品の解禁が0なので棚が空で、
「連続0件」も「値引き0件」も**空の棚を数えていた**だけだった。解禁してから測り直した。
→ [[verify-through-the-real-path]] と同じ形の罠（**通ったのではなく、通る道が無かった**）。

### 🏛️ 画面2つ（`GameUIManager.Ritual.cs` 新規）
- **召喚の儀**：祭壇の背景＋**逆回しに重ねた2枚の陣**＋中央にユニーク確率と天井のゲージ＋大ボタン2つ。
- **行商人**：露店の背景＋棚に品のカード（絵・名前・希少度・説明・効果・値段）。
  売り切れた枠は**空にせず灰色で残す**（買えたのに買わなかったが見えないと限定の意味が無い）。
- 置き場所は**『戦略』の中**（常時見えるボタンは3つのまま＝B-2の約束）。
  ⚠ ただし**行商人だけ赤い印**を出す（そのターンに一度も開いていないあいだ）。
  畳んだせいで入荷を逃すなら畳んだ意味が無い。

### ⚠⚠ 踏んだ罠2つ
1. **陣が画面の外へ飛んでいった。** `Place` は左上原点でピボットも左上なので、
   置いた矩形をそのまま `Rotate` すると**角を軸に公転**する。
   → 入れ物を `Place` で置き、中の子を `StretchFull`（中心ピボット）にして**子を回す**。
2. **回る陣の上に直に文字を置くと読めない。** 線と字が噛み合う。暗い下敷きを1枚挟んだ
   （足元の2行も床の絵の上なので同じく帯を敷いた）。
⚠ スクショが**全体にオレンジがかって見えたら暗転の途中**。1フレーム置いて撮り直す。

### ⏭️ 演出を飛ばせるようにした（承認案の ⚠ 条件）
`RevealGachaCards` は飛ばせなかった。→ `SkipGachaReveal()` を足し、
**パネルのどこを押しても／Enter／Space** で抜けられるようにした。
⚠ `AdvancePhaseByHotkey` で**横取りする**。素通しにすると演出中の Space が
そのまま『侵略開始』に届く（決算パネルと同じ穴）。
**実測**：Space で 演出中 True→False・**準備フェーズのまま**（戦闘が始まらない）／
飛ばしたあと開きかけのカード 0 枚。

### 次
10段はこれで全部。残りは別枠の **D-1 第二形態の調整**（⚠ A-1 以降で手が変わっているので基準ごと測り直す）／
D-2 K-3残り21ノード／D-3 K-4政策カード／D-4 K-5配下の昇進。

---

## 続き56：D-1 第二形態 ―― 「救済が強すぎないか」を測ったら、**救済になっていなかった**

⚠ A-1 以降でボットの手が変わっているので、**基準ごと測り直した**（自動運転4周・`docs/playlog_d1_before.md`）。

### 測って分かったこと（直す前）

| | 最終T | 魔王HP | 燃えた回数 | 殻の最低 |
|---|---|---|---|---|
| 1周目 | T21 | 20% | 3 | 10% |
| 2周目 | T21 | 10% | 3 | 10% |

殻の推移（1周目・末尾）：`100 100 100 100 100 100 100 100 61 73 **10/燃1 10/燃2 10/燃3** 20`
2周目に至っては `100 ×11 → **いきなり 10/燃1** → 10/燃2 → 10/燃3`。

⚠⚠ **想定と逆だった。**「救済が強すぎて負けようがない」ではなく、
**第二形態は3ターンの猶予を足しただけで、再起の道が無かった**。
直そうとした「T12まで無傷 → 1波で即死」が、**一段ずらしてそのまま再現していた**
（無傷の期間が T12 → T17 に延びただけ）。

### 原因は2つとも `LordBerserk` の数字だった
1. **`ShellRecoverWhenGrave = 0.10`** ―― 10%の殻は次の波で即割れる＝**実質ゼロ**。
   「詰ませないための最低限」のつもりが、最低限になっていなかった。
2. **`GraveBlockTurns = 2`** ―― その実質ゼロが**2ターン続く**。
   燃えた波は逃す者が出やすい（＝`!wiped`）ので、毎回このルートに入り、
   **割れたら最後まで割れっぱなし**になる。

### 直したもの（数字だけ・仕組みは変えない）
| | 前 | 後 |
|---|---|---|
| `ShellRecoverPerWave`（凌いだ波） | 0.25 | **0.34** |
| `ShellRecoverWhenGrave`（燃えた波） | 0.10 | **0.22** |
| `GraveBlockTurns`（回復停止） | 2 | **1** |

割れた直後からの殻：**22% → 56% → 90% → 100%**（実測・机上計算とも一致）。
⚠ **「負けようがない」にはならない**：ゲージ2は入るたびに痩せる（100/70/40/25/15%）ので、
何度も燃えれば結局終わる。変えたのは「一度割れたら詰み」を「凌げば戻れる」にした点だけ。

### 🎨 第二形態の演出（PixelLab・6生成）
- **魔王の第二形態の姿**（80×80・7コマ）→ `Resources/DungeonTale/Anim/lord_berserk/idle/`
  ⚠⚠ **姿が変わらないと形態変化にならない。**いままでは殻が割れても見た目は同じで、
    変わるのは数字（HPバーが満タンに戻る）だけだった。
- **殻が割れるVFX**（64×64・9コマ）→ `Resources/Fx/shellbreak/`
- **捕食のVFX**（64×64・9コマ）→ `Resources/Fx/devour/`
- **`SpriteFx`（新規）**：連番を1回だけ再生する使い捨て。⚠ 絵が無ければ黙って何もしない。
  ⚠ 進めるのは `deltaTime`（戦闘の一部なので倍速に追従する。UIの演出とは逆）。
  ⚠ 絵の実寸がバラバラなので**高さを基準に正規化**する（配下と同じ手当て）。
- `DemonLordVisual.SetBerserk(bool)`：素体のパーツを畳んで第二形態の絵に差し替える。
  **実測：往復とも動く**（入ると素体38枚が畳まれ、戻ると38枚が復帰）。
- 殻が割れた瞬間は **画面フラッシュ＋低い音＋殻割れVFX**を重ねた。

### ⚠⚠ 私の誤読 ―― 終了行を読まずに死因を決めていた
途中まで「T21 で終わった＝魔王が討たれた」と報告した。**誤り。**
ログの終了行は5周とも

> **終了：T22 ― 勝敗が決した**（到達 伸長の時代 75/75）

で、ハーネスは魔王の死を**別の行**（`魔王が討たれた`）に書く。
**5周とも魔王は一度も討たれていない。**HPの数字だけを見て死因を推測し、終了行を読んでいなかった。

→ ハーネスの `Finish("勝敗が決した")` に**勝者と勝ち筋を書く**ようにした（同じ読み違えを繰り返さないため）。
測り直した1周の終了行：

> **終了：T22 ― 勝敗が決した ― 自分 の『革新』（こちらの勝ち）**　最終T21・**魔王HP 100%**・燃1

### ✅ D-1 の結論
1. **「救済が強すぎる」は誤り。**殻が10%に貼り付く形は**実在の欠陥**で、直した
   （燃3→2/0/0、割れたあと 22%→56%→90%→100%）。
2. ⚠ **ただし「救済が強すぎるか」はこの設定では測れない。**
   **勝負が T22 でこちらの勝ちに決してしまい、第二形態が繰り返し試される場面まで到達しない。**
3. ⚠⚠ **壁の形が変わっていた。**かつての「T11〜17 で魔王が討たれる」は消え、いまは
   **T22 でこちらが『革新』で勝って終わる**。`ThresholdFor = 2位のスコア × 倍率` なので、
   2位（人間側）が 24〜39 点しか無い一方こちらが 290 点あると、**閾値が形骸化する**。
   ＝次に触るべきは第二形態ではなく**勝利の閾値の作り**。→ [[wall-is-placement-cap]] の続き。

### 実測まとめ

| | 直す前 | 直した後 |
|---|---|---|
| 燃えた回数 | 3 / 3 | 2 / 0 / 0 / 1 |
| 殻の最低 | 10% / 10% | 22% / 80% / 37% / 22% |
| 終わり方 | T22 勝敗が決した ×2 | T22 勝敗が決した ×4（確認した1周は**こちらの勝ち・革新**） |
| 魔王が討たれた | **0回** | **0回** |

⚠ 計測が終わったので**ハーネスはシーンから外して保存した**（勝手に進まない状態に戻した）。

### 次
**勝利の閾値**（`VictorySystem.ThresholdFor`＝2位×倍率）。2位が小さいと閾値が意味を失う。
第二形態の調整はここが動いてから測り直す ―― いま何を変えても T22 で終わる。

---

## 続き57：敵も経営する（段①②）―― 2位は伸びたが、頭打ちだった

計画 https://claude.ai/code/artifact/a5ad14b1-152c-4013-9e88-00345829ee13 （承認済み）
診断 https://claude.ai/code/artifact/fab7d593-8ab3-44b8-8023-cec615711e11

### なぜやったか（出発点の実測）
決着は毎回 **T22**。しかもそれは「勝てる最速の時刻ちょうど」だった
（勝利判定の解禁 ≈T15 ＋ 保持8ターン）。
原因は `閾値 = 2位のスコア × 倍率` という**相対式**で、
⚠ ライバル魔王は `power` が毎ターン **+20/+28/+38 されるだけ**（21ターンで 660/20 = 33点）。
⚠ 攻め込む先も **T1 に seed から1回生成した固定物**で、bot が作った物ではなかった。

### 🏗️ 器はもう在った
`RaidBoard.Build(snapshot)` は**既に本物の盤を建てて守りを湧かせている**。
`DungeonSnapshot` も階層・守り・罠・主を持つ。
＝やることは**「スナップショットを籤から bot の積み上げに変える」だけ**だった。盤は作り直していない。

### 入れたもの
**`RivalBrain`（新規）** ―― 毎ターン「稼ぐ → **1手だけ**打つ」。
⚠⚠ **成長曲線を書かない。**`NextAction` と同じく**上から順に最初の1つ**：
守りが0の階を埋める → 破られた階を厚くする → 枠が空いていれば足す → 罠 → 階層を掘る → 主を鍛える。
⚠ **プレイヤーと同じ歯止め**（階ごとの守りの上限＝配置枠／DP／段の解禁）。
免除すると [[growth-is-a-trap]] の非対称が生まれる。

**遠征で抜いた階の守りが実際に減って残る**（`OnFloorFallen`）。
⚠ いままでは次に来ると元通りで、遠征は「同じ固定ダンジョンを何度も殴る」行為だった。

スコアの rival 分岐を**盤の実体**から出すようにした。

### 実測
40ターン経営させると 5層/25守り → 7層/49守り（カンタ）、7層/56守り → 9層/73守り（ヴェルグ）。
**2位のスコア 24〜39 → 76〜1014。閾値の形骸化は解消した。**

自動運転3周（maxTurns 90）：

| | 終わり方 | 燃 |
|---|---|---|
| 1周目 | **T18 魔王が討たれた** | 4 |
| 2周目 | T25 こちらの勝ち（革新） | 1 |
| 3周目 | T25 こちらの勝ち（恐怖） | 0 |

### ⚠⚠ 分かったこと2つ
**① T22の勝利が、古い壁を隠していた。**
閾値が本物になって勝利が遠のいた途端、**魔王が討たれる展開が戻った**（1周目 T18）。
[[wall-is-placement-cap]] の壁は解決していたのではなく、**勝利が先に来て見えなくなっていた**だけ。

**② bot のスコアは頭打ちで、プレイヤーは頭打ちでない。**

| | T24 の伸び |
|---|---|
| プレイヤーの革新 | 研究 77〜79 × 6 ＝ **462+**（233ノードあるのでまだ伸びる） |
| bot の革新 | 層9×8 ＋ 主Lv20×2 ＝ **112**（層は上限10・守りも階ごとに上限） |

閾値 112×3 = 336 < 462 → **やはりプレイヤーが超える**。決着は T22 → **T25**、3ターン伸びただけ。

⚠ 原因は**私が置いた上限**（`MaxFloors=10` / `MaxGuardsOn`）。
ただし上限を上げるだけだと**成長曲線の罠**に落ちる。必要なのは
**bot にもプレイヤーと同じ「伸び続ける軸」を持たせる**こと：
- **段(tier)を上げる**＝プレイヤーの研究にあたる。DPの行き先にもなる（いま40ターンで余り始める）
- **名声**＝人間側の波を凌ぐと増える（＝段③が必須。名声の源はそこにしか無い）

### ⚠ ついでに見えた、100ターンに届かない別の理由
**両方とも使い道が尽きている。**
- プレイヤー：T24 で **DP 16,708 が遊んでいる**／**最深 B1F のまま**
- bot：40ターンで DP が余り始める

＝時間を延ばす前に、**やることが足りない**。100ターン埋めるには使い道の側も要る。

### 次
段①b（bot の段を上げる）＋ 段③（人間側が bot を攻める＝名声の源と失う道）。
⚠ 計測が終わったのでハーネスはシーンから外して保存した。

---

## 続き58：敵も経営する（段①b②③）―― 世界は動くようになった。相対式はそれでも噛み合わない

### 入れたもの
**段①b 伸び続ける軸**：bot が**段(tier)を上げる**（＝プレイヤーの研究にあたる）／
**古い守りを鍛え直す**（枠が埋まったあとのDPの行き先）。
**段③ 人間側が bot も攻める**：⚠ 盤は建てず**抽象判定**。
使う式は**プレイヤーとまったく同じ**（`AdventurerAI.WorldTier` / `LevelBase`）で、
入れる名声だけが bot 自身のもの。凌げば名声、凌げなければ守りを失い、全階抜かれれば**討たれる**。

### ⚠ 自分で置いた値の誤りを3つ直した
1. **段が一度も上がらなかった** ―― 開始の段は `3 + i*2` ＝ **3/5/7** なのに、上限を **5** にしていた。
2. **bot が1ターン1手しか打てなかった** ―― これは**私が勝手に課した縛り**。
   自動運転のログを見ると**プレイヤーは1ターンに10手ほど**打っている。→ 4手/ターン。
3. **人間側に通されると1階まるごと全滅していた** ―― それは*こちらが遠征で殲滅したとき*の話。
   毎ターン適用したら bot が痩せ続けた（守り 39→2）。→ **通された階は半分だけ失う**。

さらに **貯める判断**を入れた。⚠ 4手/ターンにしたら今度は毎ターン安い守りで使い切り、
**階層(1,200)にも段(13,000)にも永久に届かなくなった**（80ターン経っても 5/6/7層）。
次の大きな買い物の45%まで貯まったら安い手を止める ―― これは**間違えうる判断**でもある
（貯めている最中に攻められれば薄いまま受ける）。

### 実測（机上80ターン）
| | T25 | T80 |
|---|---|---|
| カンタ | 6層/守30/手強さ1232 | 6層/守**4**/手強さ633 |
| アリサ | 8層/守53/手強さ3261 | 8層/守38/手強さ2487 |
| ヴェルグ | 9層/守69/手強さ6299 | **10層/守91/手強さ8778** |

**強い bot は育ち、弱い bot は人間側に押される。**名声も 776〜831 まで伸びる。

### 実測（自動運転3周・maxTurns 120）
| | 終わり方 | 燃 | 到達 |
|---|---|---|---|
| 1周目 | **T25 魔王が討たれた** | 4 | **終焉 36/75** |
| 2周目 | T25 こちらの勝ち（革新） | 3 | **終焉 34/75** |
| 3周目 | T24 こちらの勝ち（革新） | 1 | **終焉 17/75** |

✅ **初めて「終焉の時代」に到達した**（これまでは毎回「伸長」止まり）。
✅ **第二形態が実際に使われるようになった**（燃 1〜4）。
✅ 2位のスコア 24〜39 → **126**（革新）。

### ⚠⚠ それでも決着は T24〜25 ―― 相対式は構造的に噛み合わない
2/3 はまだ『革新』でこちらが勝つ。理由は2つあって、**どちらも数字いじりでは直らない**。

**① プレイヤーと bot でスコアの材料が違う。**
- プレイヤーの革新＝**研究済みノード ×6**（233ノードあるので青天井・T24 で 462〜474）
- bot の革新＝**層×8 ＋ 主Lv×2**（層は上限10）＝ T24 で **126**

**② 倍率は時代が進むほど下がる。**
終焉 36/75 の倍率は **2.16**（実測）。閾値 = 126 × 2.16 = **273** で、プレイヤーの 462 が超える。
＝**先へ進むほど勝ちやすくなる**。Civ VII は意図してそうしているが、
こちらは片方のスコアが青天井なので、下がる閾値に追いつかれる。

### 結論
⚠ **「2位が伸びれば相対式は本来の意味を取り戻す」という私の読みは、半分しか当たらなかった。**
2位は確かに伸びた（24→126）が、**伸び方の形が違うので追いつけない**。
同じ土俵の量で測っていない限り、相対式は機能しない。

→ **次は確定事項の「絶対条件＋仕上げの儀」**。測定がその判断を裏付けた形になった。
⚠ 併せて「魔王が T25 で討たれる」も残っている（1周目）。勝利条件を直すと、こちらが前面に出る。

⚠ 計測が終わったのでハーネスはシーンから外して保存した。

---

## 続き59：引き継ぎ（2026-09-27）―― 会話の圧縮に備えて、次の一手と決定事項を固定する

### いまどこか
**K-6＋UI刷新の10段は全部完了／D-1 完了／「敵も経営する」段①①b②③ 完了。**
**次は確定事項の『絶対条件＋仕上げの儀』の実装**（そのあと『時代を延ばす』）。

### ⚠⚠ ユーザーが会話で決めたこと（ここにしか無い）
- **『絶対条件＋仕上げの儀』と『時代を延ばす』は「Civの良さだから必ずやる」**。
  敵の経営がうまくいっても**やる**。順番が後ろだっただけで、**いまが次**。
- 地上が 1.3% しか使われない件は **「まず時間を作り、地上は次段」**。
- 敵の経営は **「botも本物の盤を持つ」**（`DungeonSnapshot` に積み、攻め込んだときだけ `RaidBoard` で実体化）。
- botが失う道は **「こちらの遠征＋人間側の攻撃」**。**bot同士の削り合いは今はやらない**。
- ショップは **値引き・再入荷OK／連続で並ばない／値引きは一時的／最大3割**。
- ガチャ・ショップは **『戦略』の中**、**行商人だけ新入荷の赤い印**。
- **ゲームスタートで勝手に進まないこと**（計測後はハーネスを必ずシーンから外して保存）。
- **新しい画面は実装前にアーティファクトで承認**（常設ルール）。

### ⭐ 次の一手：絶対条件＋仕上げの儀
承認済みの画面と数字 https://claude.ai/code/artifact/fab7d593-8ab3-44b8-8023-cec615711e11
- 道ごとに **4つの絶対条件**、**3つ満たすと『儀』が解禁**、儀は **生産の列で8ターン**、完成で勝利。
- **新しい数字を作らない**：条件は `VictorySystem.Breakdown` の項目。4つ目が無い道は既存の数字
  （恐怖＝`DangerRank`、革新＝解禁した種の数）。
- 初期の条件（⚠当てずっぽう・測って直す）：
  制圧＝自領120／拠点6／都市3／倒した魔王2
  恐怖＝名声6,000／感情40／撃破の天啓120／危険度 特級
  経済＝産出DP400／素材4,000／施設12／遺産3
  革新＝研究150／魔王Lv25／遺物12／解禁した種30
- 他勢力も同じ形（bot側は `RivalBrain` の実体から）。
- `閾値＝2位×倍率`＋`HoldNeed` は置き換える。**時代切れの総合スコア決着は残す**。
- ジャーナル（A-3）と上部の『道』チップを作り替える。
- そのあと `EraSystem.Need` 75→165（1時代33T／3時代99T）。⚠ 逆順だと時代が動かなくなる。

### 根拠になった実測（続き58）
相対式は**構造的に噛み合わない**：プレイヤーの革新＝研究×6（T24で462・233ノードで青天井）、
botの革新＝層×8＋主Lv×2（126）。しかも**倍率が時代とともに下がる**（終焉36/75で2.16 → 閾値273）。

### 残っている穴（次の段で前に出てくる）
- 魔王が **T25 前後で討たれる**（勝利が先に来なくなって、元の壁が表に出た）。
- **使い道が尽きている**：T24 で **DP 16,708 が遊ぶ**／**最深 B1F のまま**。
- 地上 **4,503タイル中58（1.3%）・自領22・拠点1**。

### ⚠ ユーザーにまだ答えをもらっていない問い
- **迷宮の『次の一手』はまだ下部バーの中**（地上だけ独立した大ボタン）。そろえるか？

### 引き継ぎの置き場所
メモリ `session-state-2026-09-27.md`（会話が圧縮されたらまずここ）＋ `century-plan.md`。
測り方の手順・道具の罠・新しいファイルの役割表もそこにまとめた。

---

## 続き60：百年の決着 柱A＋柱B ―― 勝利を「絶対条件＋仕上げの儀」に、時代を 75→165 に（2026-09-27）

### ユーザーの指示
- 「次の作業は『絶対条件＋仕上げの儀』の実装に進んで」（画面と数字は承認済み `fab7d593`）
- 未回答だった問いに回答：迷宮の『次の一手』も **どちらも大ボタンにする**

### やったこと
**① 勝利判定を作り替えた（`VictorySystem`）**
- `閾値＝2位×倍率`・`HoldNeed`・`VictoryOpen`・保持カウントを**捨てた**。
- 道ごとに **4つの絶対条件**（`Conditions(勢力, 道)`）。**3つ満たすと儀が開き、8ターンで完成＝勝ち**。
  条件を3つ未満に割ると儀は**止まる**（積んだぶんは残る）。
- こちらの条件は `Breakdown` の `amount` そのもの（数え方を2か所に書かない）。
  4つ目：恐怖＝`DangerRank`（等級で表示）、革新＝`MinionEvolution.UnlockedCount`。
- 他の魔王も同じ形（`RivalBrain` の実体：自領・守り・段・名声・主Lv・罠・階層・手強さ）。
  ⚠ **どの道も「早く満ちる条件」は2つまで**にした（3つ入れると強いbotがT25で儀を始める）。
- ⚠ **人間側は儀を持たない**（承認案からの変更点）。人間側の点は時間で伸びる合成値なので、
  条件を置くと「ただの時限装置」になる。人間側の勝ち方は「魔王を討つ」で既にある。
- 時代切れの総合スコア決着は**据え置き**。
- 儀の名前：制圧『覇王の宣布』／恐怖『畏怖の戴冠』／経済『黄金の契約』／革新『深淵の開扉』。

**② 儀は生産の列に載る（`ProductionSystem.Kind.Rite = 4`）**
- 生産力ではなく**1ターン1**で進む。**買えない**（時間は買えない）。同時に1つだけ。
- 積む先は**生産力のいちばん小さい拠点**（`RiteRegion`）の**列の先頭**＝そこは8ターン他の物を作れない。
- 止める手：こちらが遠征で bot の階を落とすと、その bot の儀が **3ターン押し戻る**（`SetBackRite`、`Expedition` から）。

**③ 画面**
- ジャーナル（地上『勝利』）を承認案の形に：条件4行（✓/−・いま/要る・▶）→ 満ちた数 → 儀の帯
  （『儀を始める』／進捗バー／止まっている警告）→ 他の勢力の同じ道。残り3本は●4つの行、
  勢力表は「条件n/4・儀・総合」。他の魔王の条件の中身は名前に hover。
  **他の魔王が儀を始めたら最上段に赤い帯**（あと何ターンで敗北・▶外交で見る）。
- 上部『道』チップ：条件 n/4、儀の最中は「儀 3/8」。
- 進言：他の魔王の儀を止める（weight95・宣言なしでも出る）／儀を始める（90）／宣言した道のいちばん遠い条件（80）。
- 次の一手（地上）の⓪に「儀を始める」。
- **外交**：他の魔王の欄を**最上段へ移し**、迷宮の中身（層・守り・段・主Lv・名声）・前のターンの手・進めている儀を出す
  （敵も経営する 段④。見えない所で強くなるのは理不尽）。
- **迷宮の『次の一手』を独立した大ボタンに**：下部バーから抜き、右下（バーのすぐ上）に
  地上と同じ寸法・同じ並び（ひと言 → 大ボタン → 侵略開始）で浮かせた。手が尽きたら塊ごと縮む。戦闘中・地上では隠す。

**④ 時代を延ばした（`EraSystem.Need` 75→165）** ＝1時代33ターン／3時代99ターン。
- ⚠ 勝利を直して周が伸びたことを確かめてから、の順で入れた（逆だと K-0 の「時代が変わらない」に戻る）。

### 検証
- コンパイル エラー0（警告は既存の4件のみ）。
- 実際の経路で確認：bot に条件を満たさせ `TickTurn` → 儀開始・通知・赤い帯・勢力表 → 遠征の押し戻し 4→1。
  こちらの儀を列に置いて `Tick` → 条件未達で止まる・買えない・帯に「止まっている」→ 完成で `Decided` 自分・恐怖。
- 自動運転2周（`docs/playlog_rite1.md`・最大130T）：
  - 1周目 **T27 魔王が討たれた**（伸長 67/165）。
  - 2周目 **T40 恐怖の儀で勝ち**（T32 開始 → T40 完成・伸長 138/165）。
  - ✅ **T22 の相対決着は消えた。**勝利は「儀を始めて8ターン」という形で実際に起きた。
  - ⚠ **恐怖の条件が甘すぎた**：名声6,000は T24〜31、撃破の天啓120は T3〜T15、危険度 特級は T30、
    魔王Lv25と遺物12は T5 で満ちていた。→ 伸び方の実測から置き直した（**再計測はまだ**）：
    恐怖＝名声40,000／感情40／撃破2,000／特級、経済＝素材6,000、革新＝魔王Lv120／遺物16。
    bot の名声 300→1,000（周によって T25 で 131 と 334、5倍違う＝複利で伸びる）。
  - bot は40ターンで**どの道も 2/4 まで**。段は1つも上がらなかった（ヴェルグ段7のまま）。
  - ⚠ 自動運転は感情に一度も注がない（感情0）＝感情の条件は測れていない。

### 次にやること
1. ⚠⚠ **魔王が T27 前後で討たれる壁**。勝利が T22 に来なくなったので、これがいちばん手前の終わり方になった
   （1周目）。100ターン規模にするには、これを動かさない限り時代を延ばしても届かない。
   → [[wall-is-placement-cap]] [[lord-second-phase]]
2. 直した条件で**再計測**（1周50分前後／今回の2周で約1時間半）。
3. 地上（1.3%）は「まず時間を作り、地上は次段」のまま。

---

## 続き61：タイトルと固有名を元作品から離す（2026-09-27）

### ユーザーの指示
note で開発記を公開するにあたり、元作品（小説『ダンジョンバトルロワイヤル』・Civilization・CDO2）を
直接想起させる表記を見直したい。特にタイトル。→ 新タイトルはユーザーが選んだ **『迷宮統魔録』**。

### 変えたもの（画面に出る文字だけ）
- タイトル：『ダンジョンバトルロワイヤル』→ **『迷宮統魔録』**。英字 `DUNGEON BATTLE ROYALE` → `CHRONICLE OF THE LABYRINTH LORD`
  （候補の英字 OVERLORD は同名の小説・アニメや同名のゲームがあるので避けた）。
- 他の魔王：原作の登場人物名と「〇〇種の魔王」の呼び方をやめた
  カンタ（鬼種）→ **ゴウラ（剛鬼の魔王）**／アリサ（妖精種）→ **フィリエ（翅妖の魔王）**／ヴェルグ（龍種）→ ヴェルグ（**古龍の魔王**）。
- 眷属の名前の候補：クロエ・カノン・リナ・シオン（原作の登場人物）→ コルネ・イサナ・ネリス・ソラス。
- 原作の造語「真核」→ **「迷宮核」**（文字列10行。コメントは据え置き）。
- 「支配領域の産出」→「版図の産出」／研究の説明の「原作の最上位接頭語。」を削除。
- 他作品名への言及を削除：「Civ準拠」×2・「Civの社会制度にあたる木」・「レガシーの道」×2（→「偉業」）。
- 確認：CDO2 の語（オーブ・マイレージ・統率力）や原作の世界観の語（カオス/ロウ・世界救済・擬似的平和）は画面に出ていなかった。

### 変えていないもの
- `ProjectSettings` の `productName`（dangeon_3）。変えると PlayerPrefs の保存先が変わり、**戦績・実績・設定が消える**。
- コード内のコメント・dev_log・メモリの「原作」「Civ」の記述（プレイヤーには見えない）。
- 既存のセーブに入っている他の魔王の名前（新しい周から新しい名前になる）。
- 魔王・眷属・配下・勇者・ダンジョンなどの一般語。

### 検証
コンパイル エラー0。タイトル画面を起動して表示を確認。

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

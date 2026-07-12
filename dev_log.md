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

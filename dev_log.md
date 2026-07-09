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

### 次（A案の続き）
- [ ] フェーズ②：感情ツリー(歓喜/興奮/絶望/殺戮)＋Eurekaブースト。
- [ ] フェーズ③：3層バフ(装備/トーテム/遺物)。眷属モンスターの種類拡充(種族相性を活かす)。

### その先
- Step 2B（複数フロア/階層）／研究ツリー画面／見た目仕上げ。

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

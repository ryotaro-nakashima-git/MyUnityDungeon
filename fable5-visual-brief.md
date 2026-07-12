# fable5 作業指示書：見た目の刷新（SPUM/Dungeon Tale スプライト統合＋URP cyan修正）

> Opus が事前調査して作成。fable5 は本書に沿って**実装・検証**に集中してください。
> ゴール：手続きリグ(CharacterVisual)で描いている配下34種＋冒険者を、**SPUM等の完成スプライトで描画**し、**URPのシアン化を解消**する。

---

## 0. 前提・作業ルール（このプロジェクト固有）
- Unity 6 (URP 2D), C#, 新Input System。日本語で応答。
- **提案→許可→実装**。commit/push は main 直（許可済み）。**各ステップで dev_log.md と Claude memory を更新**。デバッグ/調整報告は1-2行。
- Unity MCP (HTTP)：`refresh_unity(scope=all, mode=force, compile=request)` → `read_console(types=[error])` でコンパイル確認。**新規.csを認識させるにはscope=all**。`manage_editor(play/stop)`＋`manage_camera(screenshot, include_image=true)` で目視。
  - ⚠️ execute_code内での `Canvas.ForceUpdateCanvases()` は PlayerLoop再帰エラーを誘発することがある（環境要因・無害）。多用しない。
- MCP screenshotは反映が数フレーム遅れることがある。決定的テスト（execute_code）優先、必要ならstop→再screenshot。

## 1. スコープ（優先順）
1. **URP cyan の全体修正**（最優先・低コスト高効果）
2. **配下(モンスター)の見た目をSPUMへ**：spumHint→SPUM prefabの割当＋idle/attack
3. **冒険者の見た目をSPUMへ**：職(4)×ランクで prefab割当
4. **獣ファミリー/ゴースト系の穴埋め**（SPUMは人型のみ。Tiny Swords/Alebardium/Dungeon Tale等で補完 or 手続きのまま残す）

※ 1回で全部が重い場合は「cyan修正→配下→冒険者」の順に**段階commit**。獣は最後（後述の穴あり）。

---

## 2. 【最重要】URP cyan の原因と修正
- **原因（特定済み）**：`Assets/SPUM/Core/Basic_Resources/Materials/SpriteDiffuse.mat` が**ビルトインの `Sprites/Diffuse`**（`m_Shader: {fileID: 10753, guid: 0000000000000000f000000000000000}`）を参照。これはURPで非対応→シアン/破綻描画になる。SPUMのSpriteRendererがこのマテリアルを使っている箇所がシアン化。
- **確認手順**：
  1. `Assets/SPUM/Resources/Addons/BasicPack/2_Prefab/{Human,Elf,Devil,Skelton}/` の prefab を開き、各SpriteRendererの `m_Material` が `SpriteDiffuse` を指しているか、それとも既定(Sprites-Default)か確認。
  2. `EffectMat.mat` / `ColorPicker.mat` の参照シェーダも確認。
- **修正案（いずれか）**：
  - (A) `SpriteDiffuse.mat` のシェーダを **URP互換のスプライトシェーダ**に差し替え：`Universal Render Pipeline/2D/Sprite-Lit-Default`（2Dライト使用時）または `Sprites/Default`（アンリットで十分ならこれが最も安全）。マテリアルYAMLの `m_Shader` を該当シェーダのGUIDに変更、または manage_material / execute_code でShader.Findして再設定。
  - (B) SPUMのSpriteRendererが使う共有マテリアルを一括で `Sprites/Default` に置換（execute_codeでシーン/プレハブ横断も可）。
  - ※ 2Dライティングを使っていないなら **`Sprites/Default`（アンリット）が最短で確実**。まずこれで cyan が消えるか検証。
- **検証**：Play → SPUM prefabを1体シーンに出す or 既存スポーンをSPUM化した後、`manage_camera screenshot` で**シアンが消え正しい色**になっていること。`read_console` にmissing shader警告が無いこと。

---

## 3. 現在の描画アーキテクチャ（差し替え点）
- **`Assets/Scripts/CharacterVisual.cs`**：手続きリグ本体。`enum RigType{Warrior,Thief,Cleric,Mage,Undead,Beast,Demonkin}`、`enum AttackStyle{Swing,Stab,Cast,Punch,Claw}`。子GameObjectにプリミティブSpriteを大量生成し、Updateで歩行/待機/攻撃/被弾/回復/死亡/ダウンをコードアニメ。HPバー・影・tint・反転を内包。
- **生成箇所**：
  - `ZombieAI.cs:138` — 子GO `vgo` に `AddComponent<CharacterVisual>()` → `visual.Init(rt, isGuardian?1.4f:1f, isGuardian)`。rtは `species`(Undead/Beast/Demonkin)から。
  - `AdventurerAI.cs:111` — `visual.Init(RigOf(adventurerJob))`（Warrior/Thief/Cleric/Mage）。
- **⚠️ 保持必須の公開API**（ZombieAI/AdventurerAIが呼ぶ。壊すとAI動作破綻）：
  - `Init(RigType type, float scale=1f, bool crown=false)`
  - `SetHP(float r)` / `FaceTowards(float worldX)` / `Facing`(getter) / `MuzzlePos()`
  - `PlayAttack(AttackStyle)` / `PlayHurt()` / `PlayHeal()`
  - `SetDowned(bool)`（眷属のダウン/復活）/ `Die()`（冒険者の死亡自壊）
- **推奨アプローチ**：`CharacterVisual` を**そのままのAPIで**「SPUM描画バックエンド」に拡張 or 新クラス `SpumVisual`（同API）を作りAI側の `AddComponent<CharacterVisual>` を差し替え。
  - Init内で **手続き生成の代わりにSPUM prefabをInstantiate**（後述のマッピングで種別決定）。HPバー/影は従来通りコードで付与（SPUMには無い）。
  - `PlayAttack/Hurt/Heal` → SPUMのアニメ（`SPUM_Prefabs` の StateAnimationPairs／Animator）へブリッジ。`Facing`は localScale.x 反転で継続可。`MuzzlePos` は手の相対位置で近似。
  - `SetDowned/Die` → SPUMの死亡クリップ or フェード/回転で代替。
  - **段階案**：まず「静止スプライト＋既存コードアニメ流用」（SPUMの合成見た目を1枚絵/最小構成で表示）でもOK。完全なSPUMアニメ連携は余力で。

---

## 4. SPUM アセット在庫
- 場所：`Assets/SPUM/Resources/Addons/BasicPack/2_Prefab/` 配下に **Human(約16) / Elf(約9) / Devil(約13) / Skelton(約8)** のキャラprefab（計~50）。ファイル名は `SPUM_2024...prefab`（種別はフォルダ名で判別）。
- ランタイム：`SPUM_Prefabs.cs`(StateAnimationPairs=状態→AnimationClip群) と `PlayerObj.cs`。SPUM標準の再生機構あり（Idle/Move/Attack/Damaged/Death 等）。
- ⚠️ **獣(Beast)向けのSPUM prefabは無い**（SPUMは人型）。beastは spumHint="" のまま。下記Dungeon Tale/Tiny Swords、または手続きリグ据え置きで対応（後述）。
- **Dungeon Tale**：`Assets/Tileset/Dungeon Tale`（9png・Atlas/Main/Hero/Fx等）。**ピクセルのダークダンジョン**で、敵に**赤悪魔ボス／金冠髑髏(魔王候補)／スライム／ゴースト**あり＝**ghost/wraithの"DungeonTale_Ghost"はここのゴースト**。シェーダ/ノーマルで2Dライティング対応（URP設定要）。テーマ直撃なので盤面/ボス/ゴースト/スライムに最適。
- **Tiny Swords**：`Assets/Tiny Swords`（414png、明るいカートゥーン）。Warrior/Archer/Lancer/Monk×5色方向別アニメ＋汎用FX(回復/矢/パーティクル)。トーンが明るくダーク基調と衝突するので**主役非採用・FXのみ拝借**推奨。
- **Bloodlines UI**：`Assets/Alebardium/Bloodlines UI`（UI専用・キャラ非該当）。

---

## 5. 割当マッピング（spumHint / species / role / rank → prefab）
`MinionCatalog`（Assets/Scripts/MinionCatalog.cs）に各形態の `spumHint`・`family`・`role`・`rank` がある。以下方針で割当（具体prefabはfable5が在庫から選定）：

### 配下（34種）
- **不死 Undead（spumHint="SPUM_Skelton"）** → Skeltonフォルダの8体を段階/役割で割当。例：
  - skeleton(基本)→素朴な骸、zombie(盾)→重そうな骸、ghoul/skeleton_knight→装甲多め、lich/elder_lich→ローブ/杖持ち、bone_sniper→弓持ち。
  - **ghost/wraith（spumHint="DungeonTale_Ghost"）**：SPUMに幽霊が無ければ、半透明化した骸 or 別アセットのゴースト。
- **魔族 Demonkin（"SPUM_Devil"）** → Devilフォルダの13体をゴブリン職ツリーに割当。例：
  - goblin(基本)→小型、hobgoblin/kobold→戦士型、goblin_archer/ranger→弓、goblin_shaman/mage/wizard→杖/ローブ、orc/goblin_general→大型・装甲、dark_elf→細身術士。
- **獣 Beast（""）** → SPUM対象外。**手続きリグ据え置き**か、Tiny Swords等の獣スプライトがあれば割当（無ければPMの見た目は据え置きで可、要相談）。
- **rank/進化段階**：同系統でも上位ほど装飾の多いprefabを当てると「強そう」に見える（任意）。

### 冒険者（職4×ランク8）
- Warrior→Human(重装・剣)、Thief→Human/Elf(軽装)、Cleric→Human(ローブ)、Mage→Elf/Human(杖・とんがり帽)。
- **ランク(G〜S)**で prefab or 色/装備を差別化できると理想（AdventurerAI.AdventurerRank / WeaponGrade/ArmorGrade が使える）。最低限は職だけでもOK。

> マッピングは `CharacterVisual`/新Visual内に **id or spumHint → prefabパス** の辞書を持つのが素直。prefabは `Resources.Load` かシリアライズ参照（シーン保存 or ScriptableObject）。**ビルド安全性**：Resources配下なので `Resources.Load<GameObject>("Addons/BasicPack/2_Prefab/Skelton/SPUM_...")` が使える（パス確認）。

---

## 6. 検証チェックリスト（各段階でcommit前に）
1. `refresh_unity(scope=all, mode=force, compile=request)` → `read_console(error)` が**エラー0**。
2. Play → 迷宮生成(`DungeonGenerator.TryGenerateWithCost`、必要なら`res.AddDP(20000)`)→ 配下召喚(`MinionRoster.TrySummon`)→配置→侵略で冒険者出現。
3. `manage_camera screenshot` で：**シアンが無い**／不死=骸・魔族=悪魔・冒険者=人型 が正しく出る／攻撃/被弾でアニメが動く／HPバー・影・反転・ボス大型化(scale)が機能／死亡・ダウンが破綻しない。
4. 既存ゲーム挙動（AI/戦闘/配置/個体Lv/装備倍率）が壊れていないこと（`z.hpMult`等はSpawnDefender側なので視覚差し替えで壊れない想定だが要目視）。

## 7. ガードレール
- **公開API(§3)を維持**。Init/PlayAttack/SetHP/SetDowned/Die/Facing/MuzzlePos の呼び出し側を壊さない。
- 既存のスポーン倍率・個体/装備/ボス強化のロジックには触れない（見た目だけ）。
- SPUMの重い依存（大量AnimationClip）でビルド/実行が重くなりすぎないよう、必要な状態だけ使う。
- 迷って設計判断が要る所（獣の扱い、SPUMアニメ全対応 vs 静止絵MVP）は**ユーザーに提案して許可を取る**。

## 8. 参考ファイル索引
- 視覚：`Assets/Scripts/CharacterVisual.cs`（手続きリグ本体）
- 生成：`Assets/Scripts/ZombieAI.cs:138`（配下）, `Assets/Scripts/AdventurerAI.cs:111`（冒険者, `RigOf`）
- データ：`Assets/Scripts/MinionCatalog.cs`（spumHint/family/role/rank）, `Assets/Scripts/MinionRoster.cs`（個体/装備）
- 素材：`Assets/SPUM/...`（prefab/mat）, `Assets/Tiny Swords/`, `Assets/Alebardium/`, `Assets/Tileset/`
- cyan元凶：`Assets/SPUM/Core/Basic_Resources/Materials/SpriteDiffuse.mat`（Sprites/Diffuse→URP非互換）
- 進捗記録：`dev_log.md`, Claude memory（`asset-store-eval`, `strength-variety-systems`, `codex-research-ui-plan`）

---
**まず §2 の cyan 修正から。SPUM prefabを1体テスト表示してシアンが消えることを確認 → §3-5 の差し替えへ。**

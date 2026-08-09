# 配下スプライト発注書（AIドット絵ツール向け）

このファイルは、生成AIに**そのまま貼って使う**ための仕様とプロンプト集です。
目的は「**名前と見た目が一致していない**」状態の解消。

---

## 1. なぜ今ズレているか（実測）

`MinionCatalog` の配下は **34種**あるのに、割り当てられている絵は実質3つでした。

| 系統 | 種類数 | いまの見た目 |
|---|---|---|
| 不死 | 12 | **全部 `SPUM_Skelton`**（ゾンビもリッチもデスナイトも同じ骸骨） |
| 獣 | 10 | **割当なし**（手続き生成のリグ） |
| 魔族 | 12 | ゴブリン系に一部割当、残りは流用 |

つまり **34種のうち固有の姿を持つのは数種だけ**。ここが「妥協」の正体です。

---

## 2. 合わせるべき仕様（`Dungeon Tale` 実測値）

迷宮の見た目をこの素材に統一したので、**新しいキャラもこれに合わせます**。

| 項目 | 値 |
|---|---|
| キャラの大きさ | **幅 14〜22px / 高さ 21px**（16pxタイルの上に立ち、タイルより少し高い） |
| タイル | 16×16 px |
| 視点 | **真横に近い見下ろし（3/4 top-down）**。正面向き・左右対称 |
| 輪郭線 | 濃い色の輪郭あり（`#2C1E31` / `#10121C` 系） |
| 影 | 足元に楕円の落ち影（別スプライト） |
| 色数 | **アトラス全体で48色**。1キャラは概ね **4〜6色** |
| 背景 | 完全透過 |

### 使用パレット（48色・そのまま渡す）

```
#AC2847 #2C1E31 #DAB163 #6B2643 #4D3533 #A26D3F #10121C #6E4C30
#E98537 #62A477 #3E3B65 #CE9248 #FF0004 #E8D282 #EC273F #F7F3B7
#1E4044 #FFFFFF #F6E8E0 #B0A7B8 #F3A833 #DE5D3A #26854C #9DE64E
#5E5B8C #A6CB96 #006554 #5AB552 #C878AF #111111 #8C78A5 #000000
#94493A #FF0077 #3859B3 #FA6E79 #3388DE #DECEED #36C5F4 #D3EED3
#9A4D76 #6DEAD6 #FFD1D5 #FFA2AC #008B8B #CC99FF #FF006A #1F232E
```

### 参照画像として渡すもの
`Assets/Resources/DungeonTale/Atlas.png` から
`Char_Skeletone` `Char_Slime` `Char_Bat` `Char_Rat` `Char_Snake` `Char_Eye` `Hero`
を切り出して**参照画像**に入れてください（スタイル参照が一番効きます）。

---

## 3. 共通プロンプト（英語・毎回先頭に貼る）

```
16-bit pixel art sprite, single character, front-facing 3/4 top-down view,
21 pixels tall, centered, standing pose, idle frame.
Strict limited palette (max 6 colors from the provided palette).
Dark outline (#2C1E31), soft top-down lighting, no gradients, no anti-aliasing,
no dithering, crisp 1px pixels, transparent background, no shadow baked in,
no text, no border, no background scenery.
Style must match the reference sprites exactly (same proportions, same outline
weight, same chunky readable silhouette).
```

**否定プロンプト（negative）**
```
anti-aliasing, blur, gradient, 3d render, isometric, side-scroller profile,
full body illustration, high resolution, extra limbs, text, watermark,
white background, drop shadow
```

---

## 4. 個別プロンプト（34種）

先頭に共通プロンプトを付けて、`SUBJECT:` の行だけ差し替えてください。

### 🦴 不死（Undead・12種）
色は骨白 `#F6E8E0` / 布 `#5E5B8C` / 目の光 `#6DEAD6` を基調に。

| # | 名前 | SUBJECT |
|---|---|---|
| 1 | スケルトン | `a plain skeleton warrior, bare bones, small rusty shortsword, empty eye sockets` |
| 2 | ゾンビ | `a bloated rotting zombie, green-grey flesh, torn clothes, arms hanging forward, slouched heavy posture` |
| 3 | ゴースト | `a translucent floating ghost, wispy tail instead of legs, hollow glowing eyes, pale blue` |
| 4 | スケルトンアーチャー | `a skeleton archer holding a short bow, quiver of arrows on the back, light frame` |
| 5 | スケルトンソルジャー | `a skeleton soldier with a round wooden shield and helmet, sturdy stance` |
| 6 | グール | `a feral hunched ghoul, long claws, bloody mouth, sinewy grey body, crouching` |
| 7 | レイス | `a dark hooded wraith, black tattered robe, no visible face, two burning cyan eyes, floating` |
| 8 | スケルトンナイト | `a heavily armoured skeleton knight, full plate armour, closed helm, longsword` |
| 9 | ボーンスナイパー | `a slender skeleton sniper with a long crossbow, one glowing targeting eye, hooded` |
| 10 | リッチ | `a lich spellcaster skeleton, tall pointed hat, purple robe, floating green orb in hand` |
| 11 | デスナイト | `a death knight in blackened spiked armour, red glowing eyes, huge greatsword, cape` |
| 12 | エルダーリッチ | `an elder lich archmage, golden crown, long dark robe with runes, staff topped with a skull` |

### 🐺 獣（Beast・10種）
色は毛皮 `#6E4C30` / `#A26D3F`、牙と爪 `#F7F3B7`。

| # | 名前 | SUBJECT |
|---|---|---|
| 13 | ラット | `a large dirty rat, hunched on four legs, long pink tail, yellow teeth` |
| 14 | バット | `a small bat with wide spread wings, big ears, fangs, hovering` |
| 15 | ウルフ | `a grey wolf standing on four legs, bared fangs, bushy tail, alert posture` |
| 16 | ハーピー | `a harpy, bird-woman with feathered wings for arms, talons, wild hair` |
| 17 | 大獣 | `a massive shaggy beast, thick brown fur, small eyes, heavy shoulders, four legs` |
| 18 | ダイアウルフ | `a huge dire wolf with scars, spiked collar of bone, dark fur, snarling` |
| 19 | セイレーン | `a siren, winged bird-woman singing, elegant blue-green feathers, open beak` |
| 20 | ベヒーモス | `a colossal armoured beast, rocky plated hide, tiny eyes, enormous bulk, four thick legs` |
| 21 | フェンリル | `a legendary giant wolf, glowing blue eyes, frost breath, broken chain on one leg` |
| 22 | （予備） | — |

### 👹 魔族（Demonkin・12種）
色は緑肌 `#62A477` / 赤肌 `#AC2847`、装備は革 `#6B2643`。

| # | 名前 | SUBJECT |
|---|---|---|
| 23 | ゴブリン | `a small green goblin, big pointed ears, crude dagger, loincloth, mischievous grin` |
| 24 | インプ | `a tiny red imp, small bat wings, curled horns, forked tail, floating` |
| 25 | ゴブリンアーチャー | `a green goblin archer with a crude short bow, feathered cap` |
| 26 | ホブゴブリン | `a big muscular hobgoblin, dark green skin, heavy club, leather straps` |
| 27 | ゴブリンシャーマン | `a goblin shaman, bone necklace, skull staff, feathers in hair, hunched` |
| 28 | コボルト | `a small dog-snouted kobold, brown scales, short spear, wary posture` |
| 29 | ゴブリンレンジャー | `a lean goblin ranger, green hood and cloak, longbow, quiver` |
| 30 | ゴブリンソルジャー | `a goblin soldier with iron helmet, square shield and shortsword, disciplined stance` |
| 31 | ゴブリンメイジ | `a goblin mage in a blue robe, oversized hat, glowing purple orb` |
| 32 | オーク | `a large orc warrior, grey-green skin, tusks, heavy axe, armour scraps` |
| 33 | ダークエルフ | `a dark elf assassin, dark purple skin, white hair, twin daggers, slim silhouette` |
| 34 | ゴブリンジェネラル | `a goblin general in ornate armour, red plume on helmet, commanding pose, banner on back` |
| 35 | ゴブリンウィザード | `a goblin wizard, long beard, star-patterned robe, floating spellbook` |

---

## 5. 使うツール（2026年8月時点の調査）

| ツール | 向いている用途 | 備考 |
|---|---|---|
| **PixelLab**（第一候補） | **既存素材へのスタイル合わせ** | 参照画像からのスタイル一致機能あり。**Asepriteプラグイン**があるので手直しまで一本で回る。ドット絵専門で利用者も最大 |
| Sprixen | プロジェクト全体の統一 | 「Style Lock」でパレット・解像度・比率を固定できる |
| Sprite-AI | 安く数を出す | 月$5〜・エディタ内蔵 |
| Scenario | 独自スタイルの学習 | 34体を同一スタイルで量産するなら学習させる価値あり |
| Ludo.ai | ドット絵以外も含む万能 | 今回の用途では過剰 |

### 実際の回し方（2026年の定番）
1. AIで **1体につき20案** 出す
2. 良いものを選ぶ
3. **Aseprite で手直し**（輪郭の1px、パレットの丸め、足元の影）

**AIだけで完成させようとしないこと。** ドット絵は1px単位の整合が命で、そこは人の手が速いです。

---

## 6. 受け取ったあとの取り込み（私の作業）

- 命名規則：`Char_<id>.png`（例 `Char_death_knight.png`）
- 置き場：`Assets/Resources/DungeonTale/Chars/`
- サイズ：高さ21px前後・透過PNG・**Point フィルタ / PPU 16**
- `MinionCatalog` の `spumHint` を新しいIDに差し替えれば、そのまま表示に乗ります

この形式で頂ければ、34体の差し替えは私の側で一括でできます。

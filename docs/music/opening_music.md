# オープニングの曲を作るとき

いまは**ゲームの中で作る曲**（手続き生成の、遅く低い曲）が鳴っている。権利の心配がない既定の音。
下のどれかで作った曲を `Assets/Resources/Audio/Bgm/opening.mp3` に置けば、そちらに差し替わる（置けば鳴る・無ければ今のまま）。

## 無料で曲を作れる所（2026-10 時点）

| 所 | 無料でできること | ゲームに使うときの注意 |
|---|---|---|
| **Gemini アプリ**（Lyria 3.5） | 「ツール → 音楽を作成」。歌なし（インストゥルメンタル）を選べる。2〜3分の曲。MP3で保存できる | 作った曲は作った人のもの。ただし**仕事で使ってよいと明記されているのは有料プラン（AI Plus／Pro／Ultra）**。無料プランでの販売物への利用ははっきりしない。曲には見えない透かし（SynthID）が入る |
| Gemini API（Lyria RealTime） | いまは試験提供で無料（回数の上限あり）。API キーが要る | 試験提供のため条件が変わりうる |
| InsMelo などのゲーム曲向けサイト | 無料で作れて、商用可・クレジット不要をうたう所がある | サイトごとに規約が違う。使う前に規約を読む |

## Gemini に渡す指示（例）

> 暗いファンタジーのゲームのオープニング曲。歌なし。ゆっくりで不穏、低い弦楽器と遠くの合唱、ときどき鐘が一つ鳴る。少しずつ盛り上がり、最後は暗く荘厳に終わる。長さ45秒くらい。

英語の指示（`AudioAssets.cs` の `opening`）：
`instrumental dark fantasy opening theme, slow and ominous, deep low strings and distant choir, a single bell, building softly toward a majestic but dark ending, no vocals, about 45 seconds`

## 置き方
1. 作った曲を MP3 で保存する。
2. 名前を `opening.mp3` にして `Assets/Resources/Audio/Bgm/` に置く。
3. オープニングの長さは約44秒（6場面＋題字）。曲が長くても、オープニングが終わると止まる。

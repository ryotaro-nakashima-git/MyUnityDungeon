"""
資源の収支（数理設計 系2・形式仕様 §3）。標準ライブラリだけで動く。

使い方:
    python tools/analyze/economy.py docs/measure/p1_act [--resource DP] [--out docs/measure/report_p1_economy]

- economy.csv から、資源ごとの蛇口（source）と排水口（sink）を区分ごとに合計する。
- ターン帯ごとの入り・出・残高、貯まり具合 R = 残高 ÷ 収入の指数移動平均（半減期5ターン）を出す。
- 仕様の目標：R_DP ∈ [2, 6]。
"""
import csv, math, os, sys
from collections import defaultdict

LABEL = {
    'AdventurerAI.TakeDamage': '撃破の報酬', 'AdventurerAI.GrantReturnReward': '逃げた冒険者の清算',
    'AdventurerAI.ReapEmotion': '感情の刈り取り', 'Nemesis.OnSlain': '因縁の相手', 'EraSystem.TickTurn': '時代の偉業',
    'SurfaceMap.CollectYields': '地上の産出', 'DistrictCatalog.Collect': '施設の産出', 'SettlementSystem.Collect': '拠点の産出',
    'MinionRoster.TrySummon': '配下の召喚', 'CommandSystem.TryUse': '号令', 'DungeonFeatureManager.TryPlaceTrap': '罠を置く',
    'DungeonFeatureManager.TryPlaceFeature': '施設を置く', 'DungeonFeatureManager.TryPlaceHabitat': '環境を置く',
    'Run.Reset': '周の初期化', 'GameUIManager.Title.StartNewGame': '開始時のDP',
}


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    res = 'DP'; out = 'docs/measure/report_economy'
    if '--resource' in sys.argv:
        res = sys.argv[sys.argv.index('--resource') + 1]; args = [a for a in args if a != res]
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; args = [a for a in args if a != out]
    by_key = defaultdict(int)
    per_turn = defaultdict(lambda: [0, 0, [], 0])   # turn -> [in, out, stocks, n]
    runs = set()
    for d in args:
        with open(os.path.join(d, 'economy.csv'), encoding='utf-8') as f:
            for r in csv.DictReader(f):
                if r['resource'] != res:
                    continue
                t = int(r['turn']); runs.add((d, r['run']))
                v = int(r['amount'])
                if r['kind'] == 'stock':
                    per_turn[t][2].append(v)
                    continue
                if r['key'].startswith('Run.') or 'StartNewGame' in r['key'] or 'SetDP' in r['key']:
                    continue   # 周の初期化・開始時の DP は流れではない
                by_key[(r['kind'], r['key'])] += v
                if v > 0: per_turn[t][0] += v
                else: per_turn[t][1] += -v
    nrun = max(1, len(runs))
    lines = [f'# {res} の収支', '', f'周の数：{nrun}（1周あたりの平均で示す）', '', '## 蛇口と排水口', '',
             '| 区分 | 呼び出し元 | 1周あたり | 割合 |', '|---|---|---|---|']
    tin = sum(v for (k, _), v in by_key.items() if k == 'source')
    tout = -sum(v for (k, _), v in by_key.items() if k == 'sink')
    for (kind, key), v in sorted(by_key.items(), key=lambda kv: -abs(kv[1])):
        base = tin if kind == 'source' else tout
        lines.append(f'| {"入り" if kind == "source" else "出"} | {LABEL.get(key, key)}<br><span style="color:#888">{key}</span> | {abs(v)/nrun:,.0f} | {abs(v)/max(1,base):.0%} |')
    lines += ['', '## ターンごと（全周の平均）', '', '| T | 入り | 出 | 残高（中央値） | 貯まり具合 R |', '|---|---|---|---|---|']
    ema = None; hl = 5; a = 1 - 0.5 ** (1 / hl)
    for t in sorted(per_turn):
        i, o, stocks, _ = per_turn[t]
        n_t = max(1, len(stocks))
        inc = i / n_t
        ema = inc if ema is None else ema + a * (inc - ema)
        st = sorted(stocks)[len(stocks) // 2] if stocks else 0
        R = st / ema if ema and ema > 0 else float('nan')
        lines.append(f'| {t} | {inc:,.0f} | {o/n_t:,.0f} | {st:,} | {R:.1f} |')
    with open(out + '.md', 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')
    sys.stdout.buffer.write(('\n'.join(lines[:40]) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()

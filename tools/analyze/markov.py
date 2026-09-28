"""
魔王の殻（第二形態のゲージ σ）の状態遷移表（数理設計 系4・形式仕様 §5）。標準ライブラリだけで動く。

使い方:
    python tools/analyze/markov.py docs/measure/p1_act [--out docs/measure/report_p1_markov]

- 状態 = 殻を 10% 刻みにした値（100%＝満タン）。最後の波で討たれた周は、その波の後を DEAD とする。
- waves.csv の (sigma_start → sigma_end) を数え上げ、遷移確率 P(s'|s) を出す。
- 各状態について：脱出確率 ε(s) = P(σ_after ≥ 50% | s)、滞留の期待 τ(s) = 1 / (1 − P(s|s))。
- 仕様の受け入れ条件（P3）：σ ≤ 30% の状態から 10波以内に σ ≥ 50% へ戻る確率が 0 でないこと。
"""
import csv, os, sys
from collections import defaultdict


def bin_of(sig):
    return min(10, int(round(float(sig) * 100)) // 10)   # 0..10（10＝満タン）


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    out = 'docs/measure/report_markov'
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]
        args = [a for a in args if a != out]
    counts = defaultdict(lambda: defaultdict(int))
    for d in args:
        killed = {}
        with open(os.path.join(d, 'runs.csv'), encoding='utf-8') as f:
            for r in csv.DictReader(f):
                killed[r['run']] = (r['outcome'] == 'killed', int(r['end_turn']))
        waves = defaultdict(list)
        with open(os.path.join(d, 'waves.csv'), encoding='utf-8') as f:
            for w in csv.DictReader(f):
                waves[w['run']].append(w)
        for run, ws in waves.items():
            ws.sort(key=lambda w: int(w['turn']))
            for i, w in enumerate(ws):
                a = bin_of(w['sigma_start'])
                last = i == len(ws) - 1
                b = 'DEAD' if (last and killed.get(run, (False, 0))[0]) else bin_of(w['sigma_end'])
                counts[a][b] += 1
    states = sorted(counts.keys(), reverse=True)
    cols = list(range(10, -1, -1)) + ['DEAD']
    lines = ['# 殻の状態遷移表', '', '行＝波の前の殻（10%刻み）、列＝波の後。数字は回数。', '']
    lines.append('| 前＼後 | ' + ' | '.join(('%d%%' % (c * 10)) if c != 'DEAD' else '討たれた' for c in cols) + ' | 計 | ε(≥50%) | τ（滞留の期待・波） |')
    lines.append('|' + '---|' * (len(cols) + 4))
    for s in states:
        row = counts[s]; n = sum(row.values())
        up = sum(v for k, v in row.items() if k != 'DEAD' and k >= 5)
        stay = row.get(s, 0) / n if n else 0
        tau = (1 / (1 - stay)) if stay < 1 else float('inf')
        lines.append('| %d%% | ' % (s * 10) + ' | '.join(str(row.get(c, 0) or '') for c in cols)
                     + ' | %d | %.2f | %.1f |' % (n, up / n if n else 0, tau))
    low = [s for s in states if s <= 3]
    esc = sum(sum(v for k, v in counts[s].items() if k != 'DEAD' and k >= 5) for s in low)
    tot = sum(sum(counts[s].values()) for s in low)
    dead = sum(counts[s].get('DEAD', 0) for s in low)
    lines += ['', f'**殻 30% 以下からの1波の行き先**：50%以上へ戻る {esc}/{tot}（{esc/tot:.0%}）・討たれる {dead}/{tot}（{dead/tot:.0%}）' if tot else '殻30%以下の状態は観測されなかった']
    with open(out + '.md', 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')
    sys.stdout.buffer.write(('\n'.join(lines) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()

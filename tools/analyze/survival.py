"""
魔王の生存曲線（数理設計 P0/P1・形式仕様 §7）。標準ライブラリだけで動く。

使い方:
    python tools/analyze/survival.py docs/measure/eq16 [docs/measure/eq4 ...] [--out docs/measure/report_eq]

- runs.csv の end_turn / outcome から、カプラン・マイヤー法で生存曲線を出す。
  outcome=killed が「事象」、それ以外（勝ち・負け・上限・中断）は「打ち切り」。
- 95% 信頼区間はグリーンウッドの式（log(-log) 変換）。
- 2群ならログランク検定（自由度1のカイ二乗）。
- waves.csv から、ターン帯ごとの 撃破率 D/A・逃走率 E/A・魔王HPの損失 を並べる（速さの等価性の確認用）。
- 出力：<out>.md（表）と <out>.svg（重ねた生存曲線）。
"""
import csv, math, os, sys


def read_runs(d):
    rows = []
    with open(os.path.join(d, 'runs.csv'), encoding='utf-8') as f:
        for r in csv.DictReader(f):
            rows.append((int(r['end_turn']), r['outcome'] == 'killed', r))
    return rows


def read_waves(d):
    p = os.path.join(d, 'waves.csv')
    if not os.path.exists(p):
        return []
    with open(p, encoding='utf-8') as f:
        return list(csv.DictReader(f))


def km(rows):
    """[(t, S, lo, hi, n_at_risk, d)] を返す。"""
    times = sorted(set(t for t, _, _ in rows))
    n = len(rows)
    S, var_sum = 1.0, 0.0
    out = [(0, 1.0, 1.0, 1.0, n, 0)]
    for t in times:
        at_risk = sum(1 for tt, _, _ in rows if tt >= t)
        d = sum(1 for tt, e, _ in rows if tt == t and e)
        if d > 0 and at_risk > 0:
            S *= (1 - d / at_risk)
            if at_risk > d:
                var_sum += d / (at_risk * (at_risk - d))
        lo, hi = ci(S, var_sum)
        out.append((t, S, lo, hi, at_risk, d))
    return out


def ci(S, var_sum, z=1.96):
    """log(-log S) 変換のグリーンウッド区間。"""
    if S <= 0 or S >= 1 or var_sum <= 0:
        return (S, S)
    se = math.sqrt(var_sum) / abs(math.log(S))
    a = math.exp(-z * se); b = math.exp(z * se)
    return (S ** b, S ** a)


def S_at(curve, t):
    s = 1.0
    for tt, S, _, _, _, _ in curve:
        if tt <= t:
            s = S
    return s


def median(curve):
    for t, S, _, _, _, _ in curve:
        if S <= 0.5:
            return t
    return None


def median_ci(curve):
    lo_t = next((t for t, _, lo, _, _, _ in curve if lo <= 0.5), None)
    hi_t = next((t for t, _, _, hi, _, _ in curve if hi <= 0.5), None)
    return lo_t, hi_t


def logrank(a, b):
    times = sorted(set(t for t, e, _ in a + b if e))
    O1 = E1 = V = 0.0
    for t in times:
        n1 = sum(1 for tt, _, _ in a if tt >= t); n2 = sum(1 for tt, _, _ in b if tt >= t)
        d1 = sum(1 for tt, e, _ in a if tt == t and e); d2 = sum(1 for tt, e, _ in b if tt == t and e)
        n = n1 + n2; d = d1 + d2
        if n < 2:
            continue
        O1 += d1; E1 += d * n1 / n
        V += d * (n1 / n) * (n2 / n) * (n - d) / (n - 1)
    if V <= 0:
        return None, None
    chi2 = (O1 - E1) ** 2 / V
    p = math.erfc(math.sqrt(chi2 / 2))
    return chi2, p


def wave_table(waves):
    """ターン帯ごとの平均（撃破率・逃走率・魔王HPの損失・負荷率）。"""
    bands = [(1, 5), (6, 10), (11, 15), (16, 20), (21, 25), (26, 30), (31, 40), (41, 999)]
    out = []
    for lo, hi in bands:
        sel = [w for w in waves if lo <= int(w['turn']) <= hi]
        if not sel:
            continue
        def m(f):
            return sum(f(w) for w in sel) / len(sel)
        A = lambda w: max(1, int(w['A']))
        out.append(((lo, hi), len(sel),
                    m(lambda w: int(w['D']) / A(w)),
                    m(lambda w: int(w['E']) / A(w)),
                    m(lambda w: float(w['lord_hp_start']) - float(w['lord_hp_end'])),
                    m(lambda w: float(w['rho']) if float(w['rho']) >= 0 else 0.0)))
    return out


def svg(curves, labels, path, tmax=None):
    W, H, L, R, T, B = 680, 300, 56, 24, 16, 40
    if tmax is None:
        tmax = max(t for c in curves for t, *_ in c) + 5
    pw, ph = W - L - R, H - T - B
    x = lambda t: L + t * pw / tmax
    y = lambda s: T + ph * (1 - s)
    cols = ['#9b1c2a', '#276b91', '#2a7349', '#8d5a0f']
    g = []
    for v in [0, .25, .5, .75, 1]:
        g.append(f'<line x1="{L}" x2="{W-R}" y1="{y(v):.1f}" y2="{y(v):.1f}" stroke="#ddd"/>'
                 f'<text x="{L-8}" y="{y(v)+4:.1f}" font-size="11" text-anchor="end" fill="#777">{int(v*100)}%</text>')
    step = 10 if tmax > 40 else 5
    for t in range(0, int(tmax) + 1, step):
        g.append(f'<text x="{x(t):.1f}" y="{H-18}" font-size="11" text-anchor="middle" fill="#777">T{t}</text>')
    for i, c in enumerate(curves):
        pts = []
        prev = 1.0
        for t, S, lo, hi, _, _ in c:
            pts.append(f'{x(t):.1f},{y(prev):.1f}'); pts.append(f'{x(t):.1f},{y(S):.1f}'); prev = S
        pts.append(f'{x(tmax):.1f},{y(prev):.1f}')
        g.append(f'<polyline points="{" ".join(pts)}" fill="none" stroke="{cols[i%4]}" stroke-width="2.4"/>')
        g.append(f'<text x="{W-R-4}" y="{T+16+i*16}" font-size="12" text-anchor="end" fill="{cols[i%4]}">{labels[i]}</text>')
    with open(path, 'w', encoding='utf-8') as f:
        f.write(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" font-family="sans-serif">{"".join(g)}</svg>')


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    out = 'docs/measure/report'
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]
        args = [a for a in args if a != out]
    groups = [(os.path.basename(d.rstrip('/\\')), read_runs(d), read_waves(d)) for d in args]
    lines = ['# 生存曲線の報告', '']
    lines.append('| 群 | 周 | 討たれた | 打ち切り | 中央値 | 中央値の95%区間 | S(30) | S(60) | S(100) |')
    lines.append('|---|---|---|---|---|---|---|---|---|')
    curves = []
    for name, rows, _ in groups:
        c = km(rows); curves.append(c)
        ev = sum(1 for _, e, _ in rows if e)
        med = median(c); lo, hi = median_ci(c)
        lines.append(f'| {name} | {len(rows)} | {ev} | {len(rows)-ev} | {("T%d" % med) if med else "未到達"} | '
                     f'{("T%d" % lo) if lo else "?"}〜{("T%d" % hi) if hi else "?"} | '
                     f'{S_at(c,30):.2f} | {S_at(c,60):.2f} | {S_at(c,100):.2f} |')
    if len(groups) == 2:
        chi2, p = logrank(groups[0][1], groups[1][1])
        lines += ['', f'ログランク検定：χ² = {chi2:.3f}、p = {p:.3f}' if chi2 is not None else 'ログランク検定：事象が足りず計算できない',
                  '（p が 0.05 より大きければ「2群の生存曲線に差があるとは言えない」）']
    lines += ['', '## 波の中身（ターン帯ごとの平均）', '']
    for name, _, waves in groups:
        lines += [f'### {name}', '', '| ターン | 波の数 | 撃破率 D/A | 逃走率 E/A | 魔王HPの損失 | 負荷率 ρ̂ |', '|---|---|---|---|---|---|']
        for (lo, hi), n, d, e, hp, rho in wave_table(waves):
            lines.append(f'| T{lo}〜{hi if hi < 999 else ""} | {n} | {d:.2f} | {e:.2f} | {hp:.3f} | {rho:.2f} |')
        lines.append('')
    with open(out + '.md', 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')
    svg(curves, [g[0] for g in groups], out + '.svg')
    sys.stdout.buffer.write(('\n'.join(lines) + '\n').encode('utf-8'))   # ⚠ Windows の端末(cp932)で落ちないように


if __name__ == '__main__':
    main()

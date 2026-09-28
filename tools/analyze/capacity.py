"""系1：処理能力 C（1波で倒せる数）が迷宮の成長で伸びるかを、ターン帯ごとに比べる。

使い方: python tools/analyze/capacity.py docs/measure/eq4b docs/measure/p2s1_ref [--band 5]
出力: 群ごと・ターン帯ごとの平均（来た A・倒した D・逃げた E・撃破率・道のり L・守り K・同時交戦の最大・魔王HPの損失）
      と、turns.csv にある成長の列（階層・広さ・進化段・装備等級）。
"""
import csv, os, sys
from collections import defaultdict


def rows(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding='utf-8') as f:
        return list(csv.DictReader(f))


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def mean(xs):
    xs = [x for x in xs if x is not None]
    return sum(xs) / len(xs) if xs else None


def fmt(x, nd=1):
    return '-' if x is None else ('%.' + str(nd) + 'f') % x


def band_of(t, w):
    lo = ((t - 1) // w) * w + 1
    return lo, lo + w - 1


def summarize(d, w):
    waves = rows(os.path.join(d, 'waves.csv'))
    turns = rows(os.path.join(d, 'turns.csv'))
    wb = defaultdict(list)
    for r in waves:
        t = int(float(r['turn']))
        wb[band_of(t, w)].append(r)
    tb = defaultdict(list)
    for r in turns:
        t = int(float(r['turn']))
        tb[band_of(t, w)].append(r)
    out = []
    for b in sorted(set(wb) | set(tb)):
        ws, ts = wb.get(b, []), tb.get(b, [])
        A = mean([num(r['A']) for r in ws])
        D = mean([num(r['D']) for r in ws])
        E = mean([num(r['E']) for r in ws])
        kr = mean([num(r['D']) / num(r['A']) for r in ws if num(r['A'])])
        loss = mean([(num(r['lord_hp_start']) or 0) - (num(r['lord_hp_end']) or 0) for r in ws])
        out.append({
            'band': '%d-%d' % b, 'n': len(ws),
            'A': A, 'D': D, 'E': E, 'kr': kr,
            'L': mean([num(r['L']) for r in ws]), 'K': mean([num(r['K']) for r in ws]),
            'eng': mean([num(r.get('eng_max')) for r in ws]), 'loss': loss,
            'floors': mean([num(r['floors']) for r in ts]),
            'tiles': mean([num(r.get('tiles')) for r in ts]),
            'evo': mean([num(r.get('evo_depth')) for r in ts]),
            'gear': mean([num(r.get('gear_mean')) for r in ts]),
            'path': mean([num(r.get('path_len')) for r in ts]),
        })
    return out


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    w = 5
    if '--band' in sys.argv:
        w = int(sys.argv[sys.argv.index('--band') + 1])
    lines = []
    for d in args:
        lines.append('\n## ' + os.path.basename(d.rstrip('/\\')) + '\n')
        lines.append('| T | 波数 | 来た A | 倒した D | 逃げた E | 撃破率 | L | K | 同時交戦 | 魔王HP損失 | 階 | 広さ(マス) | 進化段 | 装備 | 道のり |')
        lines.append('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
        for s in summarize(d, w):
            lines.append('| %s | %d | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |' % (
                s['band'], s['n'], fmt(s['A']), fmt(s['D']), fmt(s['E']), fmt(s['kr'], 2),
                fmt(s['L']), fmt(s['K']), fmt(s['eng']), fmt(s['loss'], 3),
                fmt(s['floors']), fmt(s['tiles'], 0), fmt(s['evo'], 2), fmt(s['gear'], 2), fmt(s['path'], 0)))
    sys.stdout.buffer.write(('\n'.join(lines) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()

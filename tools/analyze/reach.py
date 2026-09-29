"""系1③：魔王へ届く確率を階ごとに分解する（advs.csv）。

使い方: python tools/analyze/reach.py docs/measure/p3s0_reach [--band 10]
出力:
  1. ターン帯 × 階：入った数・抜けた割合 s_f・そこで倒された／逃げた数
  2. ターン帯ごとの届く確率 r（魔王の階に入った割合）と、魔王を叩いた割合
  3. 逃げた理由の内訳（階ごと）
  4. 踏破目的だけに絞った s_f（降りられるのは踏破目的だけなので）
"""
import csv, os, sys
from collections import defaultdict


def band(t, w):
    lo = ((t - 1) // w) * w + 1
    return '%d-%d' % (lo, lo + w - 1)


def main():
    d = [a for a in sys.argv[1:] if not a.startswith('--')][0]
    w = int(sys.argv[sys.argv.index('--band') + 1]) if '--band' in sys.argv else 10
    rows = list(csv.DictReader(open(os.path.join(d, 'advs.csv'), encoding='utf-8')))
    out = []
    by_band = defaultdict(list)
    for r in rows:
        by_band[band(int(r['turn']), w)].append(r)

    def order(b):
        return int(b.split('-')[0])

    out.append('## 届く確率（魔王の階に入った割合）\n')
    out.append('| T | 冒険者 | 踏破目的 | 魔王の階に入った | 魔王を叩いた | 平均の階数 | 魔王の階 |')
    out.append('|---|---|---|---|---|---|---|')
    for b in sorted(by_band, key=order):
        rs = by_band[b]
        n = len(rs)
        conq = sum(1 for r in rs if r['conquer'] == '1')
        reached = sum(1 for r in rs if int(r['lord_floor']) >= 0 and int(r['deepest']) >= int(r['lord_floor']))
        hit = sum(1 for r in rs if r['hit_lord'] == '1')
        fl = sum(int(r['floors']) for r in rs) / n
        lf = sum(int(r['lord_floor']) + 1 for r in rs) / n
        out.append('| %s | %d | %.0f%% | %.1f%% | %.1f%% | %.1f | B%.1fF |' % (
            b, n, 100.0 * conq / n, 100.0 * reached / n, 100.0 * hit / n, fl, lf))

    for only_conq in (False, True):
        out.append('\n## 階ごとの抜ける確率 s_f' + ('（踏破目的だけ）' if only_conq else '（全員）') + '\n')
        out.append('| T | 階 | 入った | 抜けた s | 倒された | 捕らえた | 逃げた |')
        out.append('|---|---|---|---|---|---|---|')
        for b in sorted(by_band, key=order):
            rs = [r for r in by_band[b] if (r['conquer'] == '1' or not only_conq)]
            if not rs:
                continue
            maxf = max(int(r['deepest']) for r in rs)
            for f in range(0, maxf + 1):
                ent = [r for r in rs if int(r['deepest']) >= f]
                if not ent:
                    continue
                passed = sum(1 for r in ent if int(r['deepest']) > f)
                here = [r for r in ent if int(r['deepest']) == f]
                k = sum(1 for r in here if r['outcome'] == 'killed')
                c = sum(1 for r in here if r['outcome'] == 'captured')
                e = sum(1 for r in here if r['outcome'] == 'escaped')
                out.append('| %s | B%dF | %d | %.2f | %d | %d | %d |' % (b, f + 1, len(ent), passed / len(ent), k, c, e))

    out.append('\n## 逃げた理由（一番深く来た階ごと・全期間）\n')
    whys = defaultdict(lambda: defaultdict(int))
    keys = set()
    for r in rows:
        if r['outcome'] != 'escaped':
            continue
        whys[int(r['deepest'])][r['why']] += 1
        keys.add(r['why'])
    keys = sorted(keys)
    out.append('| 階 | ' + ' | '.join(keys) + ' |')
    out.append('|---|' + '---|' * len(keys))
    for f in sorted(whys):
        out.append('| B%dF | ' % (f + 1) + ' | '.join(str(whys[f].get(k, 0)) for k in keys) + ' |')

    out.append('\n## 階段で止まった者（stuck）の Lv と必要 Lv\n')
    st = [r for r in rows if r['why'] == 'stuck']
    if st:
        gap = sum(int(r['need_next']) - int(r['level']) for r in st) / len(st)
        out.append('stuck %d 人・必要Lvとの差の平均 %.1f' % (len(st), gap))
    sys.stdout.buffer.write(('\n'.join(out) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()

"""速さの等価性：同じ種の周を、速さ違いのフォルダどうしで1ターンずつ並べる（→ SimClock）。

使い方: python tools/analyze/equiv.py docs/measure/eqf_x1 docs/measure/eqf_x4 docs/measure/eqf_x16
出力: 周ごと・ターンごとの (来た A, 倒した D, 逃げた E, 魔王HP) を並べ、完全一致の割合と、
      ターン帯ごとの平均の差を出す。完全一致しないのは、画面側の演出が同じ乱数を使うため（下の注を参照）。
"""
import csv, os, sys


def load(d):
    out = {}
    for r in csv.DictReader(open(os.path.join(d, 'waves.csv'), encoding='utf-8')):
        out[(int(r['run']), int(r['turn']))] = (int(float(r['A'])), int(float(r['D'])), int(float(r['E'])), round(float(r['lord_hp_end']), 2))
    return out


def main():
    dirs = sys.argv[1:]
    data = [load(d) for d in dirs]
    keys = sorted(set.intersection(*[set(x) for x in data]))
    same = sum(1 for k in keys if all(x[k] == data[0][k] for x in data[1:]))
    lines = ['## 速さの等価性（%s）' % ' / '.join(os.path.basename(d) for d in dirs), '',
             '共通の (周, ターン)：%d　完全一致：%d（%.0f%%）' % (len(keys), same, 100.0 * same / max(1, len(keys))), '',
             '| T | ' + ' | '.join('%s A/D/E/HP' % os.path.basename(d) for d in dirs) + ' |',
             '|---|' + '---|' * len(dirs)]
    for lo in (1, 6, 11, 16):
        ks = [k for k in keys if lo <= k[1] < lo + 5]
        if not ks:
            continue
        cells = []
        for x in data:
            n = len(ks)
            cells.append('%.1f / %.1f / %.1f / %.2f' % tuple(sum(x[k][i] for k in ks) / n for i in range(4)))
        lines.append('| %d-%d | %s |' % (lo, lo + 4, ' | '.join(cells)))
    sys.stdout.buffer.write(('\n'.join(lines) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()

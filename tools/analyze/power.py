"""系3：強さの曲線。冒険者の強さ（1波の合計）と守りの強さ（配下の合計）を同じ物差し（HP×攻撃）で並べる。

使い方: python tools/analyze/power.py docs/measure/p3s7_base/ref [--band 10]
出力: ターン帯ごとの 来た人数・1人あたりの強さ・1波の強さ合計・守りの合計・配下の数・比（守り÷攻め）・倒した割合・魔王HPの損失
読み方: 比が下がっていく帯で守りが負け始める。基準プレイヤーで比を一定（目標の帯）に保つように冒険者の強さの伸びを決める。
"""
import csv, os, sys


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    w = int(sys.argv[sys.argv.index('--band') + 1]) if '--band' in sys.argv else 10
    out = ['| T | 波 | 来た | 1人の強さ | 攻めの合計 | 守りの合計 | 配下 | 守り÷攻め | 倒した割合 | 魔王HP損失 |',
           '|---|---|---|---|---|---|---|---|---|---|']
    for d in args:
        rows = [r for r in csv.DictReader(open(os.path.join(d, 'waves.csv'), encoding='utf-8')) if r.get('adv_power')]
        out.insert(0, '## ' + d)
        bands = {}
        for r in rows:
            t = int(r['turn'])
            lo = ((t - 1) // w) * w + 1
            bands.setdefault(lo, []).append(r)
        for lo in sorted(bands):
            b = bands[lo]
            n = len(b)
            A = sum(float(r['A']) for r in b) / n
            ap = sum(float(r['adv_power']) for r in b) / n
            dp = sum(float(r['def_power']) for r in b) / n
            dc = sum(float(r['def_count']) for r in b) / n
            kr = sum(float(r['D']) / max(1.0, float(r['A'])) for r in b) / n
            loss = sum(float(r['lord_hp_start']) - float(r['lord_hp_end']) for r in b) / n
            out.append('| %d-%d | %d | %.1f | %.0f | %.0f | %.0f | %.0f | %.2f | %.2f | %.3f |' % (
                lo, lo + w - 1, n, A, ap / max(1.0, A), ap, dp, dc, dp / max(1.0, ap), kr, loss))
    sys.stdout.buffer.write(('\n'.join(out) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()

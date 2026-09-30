"""群を交互に回した計測（arms.csv つき）を、群ごとの下位フォルダに分ける。

使い方: python tools/analyze/split_arms.py docs/measure/p2s1_abl
出力: docs/measure/p2s1_abl/<群名>/{runs,waves,turns,economy}.csv
      → そのまま survival.py / capacity.py / economy.py に渡せる。
"""
import csv, os, sys


def main():
    d = sys.argv[1]
    with open(os.path.join(d, 'arms.csv'), encoding='utf-8') as f:
        arm_of = {r['run']: r['arm'] for r in csv.DictReader(f)}
    arms = sorted(set(arm_of.values()))
    for name in ('runs.csv', 'waves.csv', 'turns.csv', 'economy.csv', 'advs.csv'):
        src = os.path.join(d, name)
        if not os.path.exists(src):
            continue
        with open(src, encoding='utf-8', newline='') as f:
            rd = csv.reader(f)
            header = next(rd)
            rows = list(rd)
        for a in arms:
            os.makedirs(os.path.join(d, a), exist_ok=True)
            with open(os.path.join(d, a, name), 'w', encoding='utf-8', newline='') as f:
                w = csv.writer(f)
                w.writerow(header)
                for r in rows:
                    if r and arm_of.get(r[0]) == a:
                        w.writerow(r)
    counts = {a: sum(1 for v in arm_of.values() if v == a) for a in arms}
    sys.stdout.buffer.write(('群ごとの周: ' + str(counts) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()

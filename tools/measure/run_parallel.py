"""計測用の実行ファイルを並列に走らせ、結果を1つのフォルダにまとめる（数理設計・計測の高速化 2026-10-01）。

使い方:
  python tools/measure/run_parallel.py --name p3s5_x --arms ref,old --runs-per-arm 6 --procs 8 [--max-turns 130] [--seed 1000]

- 群ごとに周を塊に分け、各塊を別プロセスで回す（1プロセス＝1つの群・数周）。
- 地図の種は `seed + 塊の頭の周番号 + 周` ＝ **群どうしで同じ地図**（共通乱数）。
- 途中経過は docs/measure/<name>/_parts/ に書かれ、全部終わったら docs/measure/<name>/ に
  runs/waves/turns/economy/advs.csv と arms.csv をまとめる（周番号は通し番号に振り直す）。
  → そのまま tools/analyze/split_arms.py → survival.py / capacity.py / reach.py に渡せる。
- 実行ファイルは Unity の Tools/計測/計測用の実行ファイルを書き出す で作る（Builds/Measure/Dangeon.exe）。
"""
import argparse, csv, glob, os, subprocess, sys, time

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
EXE = os.path.join(ROOT, 'Builds', 'Measure', 'Dangeon.exe')
BALANCE = os.path.join(ROOT, 'Assets', 'StreamingAssets', 'Balance', 'params.json')
FILES = ('runs.csv', 'waves.csv', 'turns.csv', 'economy.csv', 'advs.csv')


def say(s):
    sys.stdout.buffer.write((s + '\n').encode('utf-8'))
    sys.stdout.flush()


def plan(arms, runs_per_arm, procs):
    """(arm, seedOffset, runs) の塊の並び。群ごとに同じ切り方にする（同じ種で比べるため）。"""
    per_arm = max(1, procs // len(arms))
    size = -(-runs_per_arm // per_arm)   # 切り上げ
    chunks = []
    for a in arms:
        start = 0
        while start < runs_per_arm:
            n = min(size, runs_per_arm - start)
            chunks.append((a, start, n))
            start += n
    return chunks


def merge(outdir, parts):
    """塊ごとの CSV を通し番号でまとめる。parts = [(arm, partdir, runs)]"""
    arms_rows = []
    offset = 0
    writers = {}
    handles = {}
    try:
        for arm, pdir, _ in parts:
            runs_here = set()
            for name in FILES:
                src = os.path.join(pdir, name)
                if not os.path.exists(src):
                    continue
                with open(src, encoding='utf-8', newline='') as f:
                    rd = csv.reader(f)
                    header = next(rd, None)
                    if header is None:
                        continue
                    if name not in writers:
                        handles[name] = open(os.path.join(outdir, name), 'w', encoding='utf-8', newline='')
                        writers[name] = csv.writer(handles[name])
                        writers[name].writerow(header)
                    for row in rd:
                        if not row:
                            continue
                        r = int(row[0])
                        runs_here.add(r)
                        row[0] = str(offset + r)
                        writers[name].writerow(row)
            for r in sorted(runs_here):
                arms_rows.append((offset + r, arm))
            offset += max(runs_here) if runs_here else 0
    finally:
        for h in handles.values():
            h.close()
    with open(os.path.join(outdir, 'arms.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['run', 'arm'])
        for r, a in sorted(set(arms_rows)):
            w.writerow([r, a])
    return offset


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--name', required=True)
    ap.add_argument('--arms', default='ref')
    ap.add_argument('--runs-per-arm', type=int, default=6)
    ap.add_argument('--procs', type=int, default=8)
    ap.add_argument('--max-turns', type=int, default=130)
    ap.add_argument('--seed', type=int, default=1000)
    ap.add_argument('--extra', default='', help='実行ファイルへ足す引数（例: "-noFixedStep -speed 4"）')
    a = ap.parse_args()

    if not os.path.exists(EXE):
        say('⚠ 実行ファイルが無い: ' + EXE + '（Unity の Tools/計測/計測用の実行ファイルを書き出す）')
        return 1
    arms = [x.strip() for x in a.arms.split(',') if x.strip()]
    outdir = os.path.join(ROOT, 'docs', 'measure', a.name)
    partroot = os.path.join(outdir, '_parts')
    os.makedirs(partroot, exist_ok=True)

    chunks = plan(arms, a.runs_per_arm, a.procs)
    procs, parts = [], []
    t0 = time.time()
    for arm, start, n in chunks:
        tag = '%s_%02d' % (arm, start)
        pdir = os.path.join(partroot, tag)
        log = os.path.join(partroot, tag + '.md')
        cmd = [EXE, '-batchmode', '-nographics', '-measure',
               '-runs', str(n), '-arms', arm, '-seed', str(a.seed), '-seedOffset', str(start),
               '-maxTurns', str(a.max_turns), '-out', pdir, '-log', log, '-balance', BALANCE,
               '-logFile', os.path.join(partroot, tag + '.player.log')]
        if a.extra:
            cmd += a.extra.split()
        procs.append((tag, subprocess.Popen(cmd, cwd=ROOT)))
        parts.append((arm, pdir, n))
        say('▶ %s（%d周・種 %d+%d）' % (tag, n, a.seed, start))

    remaining = {t for t, _ in procs}
    while remaining:
        time.sleep(15)
        for tag, p in procs:
            if tag in remaining and p.poll() is not None:
                remaining.discard(tag)
                say('✔ %s 終了（%d分）' % (tag, (time.time() - t0) / 60))
    total = merge(outdir, parts)
    say('全%d周おわり（%.1f分）→ %s' % (total, (time.time() - t0) / 60, outdir))
    return 0


if __name__ == '__main__':
    sys.exit(main())

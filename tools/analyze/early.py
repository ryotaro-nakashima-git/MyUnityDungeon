# -*- coding: utf-8 -*-
"""
J5 序盤の手応え：ターンごと（既定 T1〜T15）に
  来た人数・倒した割合・届いた深さ（最深階/階数）・最下層まで届いた割合・魔王に触れた割合・
  魔王HPの減り・守り÷攻め（def_power/adv_power）を周の平均で出す。
使い方: python tools/analyze/early.py docs/measure/<name> [--to 15]
"""
import csv, sys, os, collections, argparse

ap = argparse.ArgumentParser()
ap.add_argument("dirs", nargs="+")
ap.add_argument("--to", type=int, default=15)
a = ap.parse_args()

def rows(p):
    with open(p, encoding="utf-8") as f:
        return list(csv.DictReader(f))

for d in a.dirs:
    advs = rows(os.path.join(d, "advs.csv"))
    waves = rows(os.path.join(d, "waves.csv"))
    turns = rows(os.path.join(d, "turns.csv"))
    runs = {r["run"] for r in waves}
    print("##", d, " runs=", len(runs))
    by = collections.defaultdict(list)
    for r in advs: by[int(r["turn"])].append(r)
    wv = collections.defaultdict(list)
    for r in waves: wv[int(r["turn"])].append(r)
    tv = collections.defaultdict(list)
    for r in turns: tv[int(r["turn"])].append(r)
    print("T  | 周 | 来 | 倒% | 深さ/階 | 最下層% | 魔王触% | 魔王HP減 | 守÷攻 | 階数 | 配置/枠 | DP")
    for t in range(1, a.to + 1):
        A = by.get(t, []); W = wv.get(t, []); Tn = tv.get(t, [])
        if not W: continue
        n = len(A) / max(1, len(W))
        k = sum(1 for r in A if r["outcome"] == "killed") / max(1, len(A))
        dep = [ (int(r["deepest"]) + 1) / max(1, int(r["floors"])) for r in A ]
        bottom = sum(1 for r in A if int(r["deepest"]) + 1 >= int(r["floors"])) / max(1, len(A))
        hit = sum(1 for r in A if r.get("hit_lord", "0") == "1") / max(1, len(A))
        hp = sum(float(r["lord_hp_start"]) - float(r["lord_hp_end"]) for r in W) / len(W)
        pw = [float(r["def_power"]) / max(1e-6, float(r["adv_power"])) for r in W if float(r["adv_power"]) > 0]
        fl = sum(int(r["floors"]) for r in Tn) / max(1, len(Tn))
        pl = sum(int(r["placed"]) for r in Tn) / max(1, len(Tn)); cap = sum(int(r["cap"]) for r in Tn) / max(1, len(Tn))
        dp = sum(int(r["dp"]) for r in Tn) / max(1, len(Tn))
        print(f"{t:2d} | {len(W):2d} | {n:4.1f} | {k*100:3.0f} | {sum(dep)/max(1,len(dep)):.2f} | {bottom*100:3.0f} | {hit*100:3.0f} | {hp:.2f} | {sum(pw)/max(1,len(pw)):5.2f} | {fl:.1f} | {pl:.0f}/{cap:.0f} | {dp:.0f}")
    print()

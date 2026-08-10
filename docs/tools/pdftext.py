# -*- coding: utf-8 -*-
"""ライブラリ無しでPDFの本文を取り出す（資料PDFを読むための道具）。

⚠ **なぜ要るか**：この環境には pypdf / pdfminer / PyMuPDF が入っていない。
  さらに Claude Code 内蔵のPDFリーダーが『password-protected』と誤判定するPDFがある
  （実際には `/Encrypt` を持っていなかった。`grep -c /Encrypt` で確かめられる）。
  そのときはこれを使う。

やること：
  1. `stream ... endstream` を全部 zlib 展開する
  2. `beginbfchar` / `beginbfrange` から ToUnicode CMap（グリフID→Unicode）を組む
  3. 本文ストリームの `<....>` 16進文字列を CMap で復号する

⚠ 改行の入れ方に注意。**グリフを1文字ずつ配置しているPDFでは `Td`/`TD` ごとに
  改行を入れると「1文字1行」になる**（実際にやらかした）。改行は `ET` だけに入れ、
  それでも1文字ずつになるなら、出力後に改行を畳んでから見出しで割り直す。

使い方: python docs/tools/pdftext.py <file.pdf>  → <file.pdf>.txt を書き出す
"""
import io, re, sys, zlib

PATH = sys.argv[1]
d = io.open(PATH, "rb").read()

streams = []
for m in re.finditer(rb"stream\r?\n", d):
    start = m.end()
    end = d.find(b"endstream", start)
    if end < 0:
        continue
    raw = d[start:end]
    try:
        streams.append(zlib.decompress(raw.rstrip(b"\r\n")))
    except Exception:
        try:
            streams.append(zlib.decompressobj().decompress(raw))
        except Exception:
            pass

# ---- ToUnicode CMap を集める（複数フォントぶんを1枚に混ぜる） ----
cmap = {}
for s in streams:
    if b"beginbfchar" not in s and b"beginbfrange" not in s:
        continue
    for blk in re.findall(rb"beginbfchar(.*?)endbfchar", s, re.S):
        for a, b in re.findall(rb"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>", blk):
            src = int(a, 16)
            t = b.decode("ascii")
            dst = "".join(chr(int(t[i:i + 4], 16)) for i in range(0, len(t), 4))
            cmap[src] = dst
    for blk in re.findall(rb"beginbfrange(.*?)endbfrange", s, re.S):
        for a, b, c in re.findall(rb"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>", blk):
            lo, hi, base = int(a, 16), int(b, 16), int(c, 16)
            for k in range(lo, min(hi, lo + 65535) + 1):
                cmap[k] = chr(base + (k - lo))

def decode_hex(h):
    out = []
    for i in range(0, len(h) - 1, 4):
        code = int(h[i:i + 4], 16)
        out.append(cmap.get(code, ""))
    return "".join(out)

# ---- 本文ストリームから文字列演算子を拾う ----
pieces = []
for s in streams:
    if b"Tj" not in s and b"TJ" not in s:
        continue
    # ⚠ このPDFはグリフを1文字ずつ配置しているので、Td/TD ごとに改行を入れると
    #   「1文字1行」になってしまう。改行は BT ブロックの区切り（ET）だけに入れる。
    txt = []
    for m in re.finditer(rb"<([0-9A-Fa-f\s]+)>|(ET)", s):
        if m.group(1):
            txt.append(decode_hex(re.sub(rb"\s", b"", m.group(1)).decode("ascii")))
        else:
            txt.append("\n")
    pieces.append("".join(txt))

body = "\n".join(pieces)
body = re.sub(r"\n{3,}", "\n\n", body)
sys.stdout.buffer.write(("CMap登録数=%d / 本文ストリーム=%d / 文字数=%d\n" % (len(cmap), len(pieces), len(body))).encode("utf-8"))
out = PATH + ".txt"
io.open(out, "w", encoding="utf-8").write(body)
sys.stdout.buffer.write(("→ %s\n" % out).encode("utf-8"))

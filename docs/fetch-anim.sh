#!/usr/bin/env bash
# 🎬 PixelLab のアニメフレームを取り込む。
#
# 使い方:
#   bash docs/fetch-anim.sh <種id> <キャラid> <状態>:<anim_uuid>:<コマ数> [...]
# 例:
#   bash docs/fetch-anim.sh zombie 5161bf15-.... idle:31c9....:4 walk:cf0f....:6
#
# ⚠ <anim_uuid> は group_id とは**別物**で、対応表からは組み立てられない。
#    get_character の出力にある animations/<ここ>/east/0.png から拾うこと。
# 出力: Assets/Resources/DungeonTale/Anim/<種id>/<状態>/<n>.png
set -e
ACC=271c6949-1a12-453e-82be-30854c63065a
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ID="$1"; CH="$2"; shift 2
DEST="$ROOT/Assets/Resources/DungeonTale/Anim/$ID"
mkdir -p "$DEST"
for spec in "$@"; do
  st="${spec%%:*}"; rest="${spec#*:}"; uid="${rest%%:*}"; cnt="${rest##*:}"
  mkdir -p "$DEST/$st"
  for i in $(seq 0 $((cnt-1))); do
    curl -s -o "$DEST/$st/$i.png" \
      "https://backblaze.pixellab.ai/file/pixellab-characters/$ACC/$CH/animations/$uid/east/$i.png"
  done
  n=$(ls "$DEST/$st"/*.png 2>/dev/null | wc -l)
  echo "$ID/$st: ${n}コマ"
done

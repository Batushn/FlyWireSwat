#!/usr/bin/env bash
# Turns the recorded MP4s into README GIFs (docs/media). Usage: Tools/make_gifs.sh [Build/Linux/Videos]
set -euo pipefail
cd "$(dirname "$0")/.."
SRC="${1:-Build/Linux/Videos}"
OUT=docs/media; mkdir -p "$OUT"
gif() { # in, out, start, duration, width, fps
  ffmpeg -y -loglevel error -ss "$3" -t "$4" -i "$1" -vf "fps=$6,scale=$5:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=128[p];[s1][p]paletteuse=dither=bayer:bayer_scale=4" "$2"
}
gif "$SRC/landscape/02_swatter.mp4"   "$OUT/showcase_swatter.gif" 3 7 720 15
gif "$SRC/landscape/11_bazooka.mp4"   "$OUT/showcase_bazooka.gif" 4 6 720 15
gif "$SRC/landscape/03_racket.mp4"    "$OUT/showcase_racket.gif"  3 7 720 15
gif "$SRC/landscape/19_uvzapper.mp4"  "$OUT/repellent_uv.gif"     0 6 720 15
gif "$SRC/landscape/12_waterbag.mp4"  "$OUT/repellent_waterbag.gif" 0 6 720 15
gif "$SRC/portrait/02_swatter.mp4"    "$OUT/portrait_swatter.gif" 3 6 360 15
ls -la "$OUT"

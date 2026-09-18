#!/usr/bin/env bash
# Downloads every third-party asset the project needs (all CC0 / OFL) and the FlyWire tables.
# Run once after cloning: ./Tools/fetch_assets.sh   (needs python3, curl, unzip)
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== Poly Haven models, textures, HDRI (CC0)"
python3 Tools/fetch_polyhaven.py

echo "== Kenney Blaster Kit (CC0)"
mkdir -p Assets/ThirdParty/Kenney/BlasterKit
if [ ! -d "Assets/ThirdParty/Kenney/BlasterKit/Models" ]; then
  curl -sL -A "Mozilla/5.0" -o /tmp/kenney_blaster.zip "https://kenney.nl/media/pages/assets/blaster-kit/261d80a716-1753959510/kenney_blaster-kit_2.1.zip"
  unzip -oq /tmp/kenney_blaster.zip -d Assets/ThirdParty/Kenney/BlasterKit
  rm -rf "Assets/ThirdParty/Kenney/BlasterKit/Models/OBJ format" "Assets/ThirdParty/Kenney/BlasterKit/Models/GLB format" Assets/ThirdParty/Kenney/BlasterKit/Previews Assets/ThirdParty/Kenney/BlasterKit/*.url Assets/ThirdParty/Kenney/BlasterKit/Overview.html "Assets/ThirdParty/Kenney/BlasterKit/Preview (Variation A).png"
fi

echo "== Inter font (OFL)"
mkdir -p Assets/ThirdParty/Fonts Assets/Resources/Fonts
if [ ! -f Assets/Resources/Fonts/Inter-Regular.ttf ]; then
  curl -sL -A "Mozilla/5.0" -o /tmp/inter.zip "https://github.com/rsms/inter/releases/download/v4.1/Inter-4.1.zip"
  unzip -oj /tmp/inter.zip "extras/ttf/Inter-Regular.ttf" "extras/ttf/Inter-Bold.ttf" "extras/ttf/Inter-SemiBold.ttf" "LICENSE.txt" -d Assets/ThirdParty/Fonts >/dev/null
  mv -f Assets/ThirdParty/Fonts/LICENSE.txt Assets/ThirdParty/Fonts/Inter-LICENSE-OFL.txt
  cp Assets/ThirdParty/Fonts/Inter-*.ttf Assets/Resources/Fonts/
fi

echo "== FlyWire FAFB v783 tables (CC-BY 4.0, ~60 MB)"
mkdir -p Data/raw
for f in connections classification neurons consolidated_cell_types; do
  [ -f Data/raw/$f.csv.gz ] || curl -sL -o Data/raw/$f.csv.gz "https://storage.googleapis.com/flywire-data/codex/data/fafb/783/$f.csv.gz"
done
python3 Tools/extract_escape_circuit.py

echo "== Procedural props (needs Blender on PATH; skipped if missing)"
if command -v blender >/dev/null 2>&1; then
  mkdir -p Assets/Resources/Models
  blender -b --python Tools/blender/make_props.py -- Assets/Resources/Models | grep -E "exported|FAILED" || true
fi

echo
echo "Done. Open the project in Unity 6, run  FlyWireSwat > Import Third-Party Assets, then open Assets/Scenes/FlyWireSwat.unity and press Play."

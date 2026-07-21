#!/usr/bin/env bash
# Package the shippable mod into dist/ShipHoloAI — ONLY what the game loads
# (About/, 1.6/, Textures/, Languages/), freshly built. Dev material (Source/,
# Concept/, Decompiled/, .claude/, CLAUDE.md, README) never enters the package.
# Upload dist/ShipHoloAI as the Workshop/release folder.
#
# Run from anywhere: paths resolve relative to the repo root.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DIST="$ROOT/dist/ShipHoloAI"
cd "$ROOT"

echo "== building assembly =="
FrameworkPathOverride=/usr/lib/mono/4.7.2-api \
    dotnet build Source/ShipHoloAI/ShipHoloAI.csproj -c Release -v quiet

echo "== staging $DIST =="
rm -rf "$ROOT/dist"
mkdir -p "$DIST"
for dir in About 1.6 Textures Languages; do
    cp -r "$ROOT/$dir" "$DIST/$dir"
done
# Debug symbols never ship.
rm -f "$DIST/1.6/Assemblies/"*.pdb

# Steam caps the Workshop preview at 1 MB: keep the repo's full-res art but
# shrink the shipped copy until it fits.
python3 - "$DIST/About/Preview.png" <<'EOF'
import io, os, sys
from PIL import Image

path = sys.argv[1]
if os.path.getsize(path) <= 1_000_000:
    print(f"preview: {os.path.getsize(path)//1024} KB (fits as-is)")
    sys.exit(0)
im = Image.open(path)
for scale in (1.0, 0.833, 0.75, 0.667, 0.5, 0.417):
    size = (int(im.width * scale), int(im.height * scale))
    buf = io.BytesIO()
    im.resize(size, Image.LANCZOS).save(buf, "PNG", optimize=True)
    if len(buf.getvalue()) <= 1_000_000:
        with open(path, "wb") as f:
            f.write(buf.getvalue())
        print(f"preview: rescaled to {size[0]}x{size[1]}, "
              f"{len(buf.getvalue())//1024} KB (Steam 1 MB cap)")
        break
else:
    sys.exit("preview: could not fit under Steam's 1 MB cap")
EOF

echo "== sanity =="
test -f "$DIST/1.6/Assemblies/ShipHoloAI.dll" || { echo "MISSING DLL"; exit 1; }
# Nothing but game-loaded content may ship.
stray=$(find "$DIST" \( -name "*.cs" -o -name "*.py" -o -name "*.csproj" \
    -o -name "*.pdb" -o -name ".claude" -o -name "Decompiled" -o -name "Concept" \) | head)
if [ -n "$stray" ]; then
    echo "STRAY DEV FILES IN PACKAGE:"; echo "$stray"; exit 1
fi

echo "== package contents =="
find "$DIST" -type d | sed "s|$DIST|ShipHoloAI|" | sort
echo "total: $(du -sh "$DIST" | cut -f1), $(find "$DIST" -type f | wc -l) files"
echo "OK — upload $DIST"

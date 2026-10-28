#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_svg="$root/assets/osu-oss/osu-oss.svg"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

magick -background none "$source_svg" -resize 1024x1024 "$work/osuoss-1024.png"

for size in 16 32 48 64 128 256 512; do
    magick "$work/osuoss-1024.png" -resize "${size}x${size}" "$work/osuoss-${size}.png"
done

magick "$work/osuoss-1024.png" -define icon:auto-resize=256,128,64,48,32,16 "$root/osu.Desktop/osuoss.ico"
cp "$work/osuoss-512.png" "$root/osu.Desktop/osuoss.png"
magick "$work/osuoss-1024.png" -define icon:auto-resize=256,128,64,48,32,16 "$root/tools/osu-deploy/install.ico"

for size in 64 128 256 512 1024; do
    mkdir -p "$root/tools/osu-deploy/templates/osuoss.AppDir/usr/share/icons/hicolor/${size}x${size}/apps"
    cp "$work/osuoss-${size}.png" "$root/tools/osu-deploy/templates/osuoss.AppDir/usr/share/icons/hicolor/${size}x${size}/apps/osuoss.png"
done
cp "$work/osuoss-1024.png" "$root/tools/osu-deploy/templates/osuoss.AppDir/osuoss.png"

WORK="$work" OUT="$root/tools/osu-deploy/templates/osuoss.app/Contents/Resources/AppIcon.icns" python3 - <<'PY'
import os, struct
from pathlib import Path

work = Path(os.environ["WORK"])
types = {
    b"icp4": "16",
    b"icp5": "32",
    b"ic07": "128",
    b"ic08": "256",
    b"ic09": "512",
    b"ic10": "1024",
    b"ic11": "32",
    b"ic12": "64",
    b"ic13": "256",
    b"ic14": "512",
}

data = b""
for type_code, size in types.items():
    png = (work / f"osuoss-{size}.png").read_bytes()
    data += type_code + struct.pack(">I", len(png) + 8) + png

Path(os.environ["OUT"]).write_bytes(b"icns" + struct.pack(">I", len(data) + 8) + data)
PY

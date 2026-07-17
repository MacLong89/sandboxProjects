#!/usr/bin/env python3
"""Fail if cutout sprites lack true alpha or have opaque corner mattes."""
from __future__ import annotations

import struct
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets" / "textures" / "art"

# Backgrounds may be fully opaque.
OPAQUE_OK = {
	"bg_harbor.png", "bg_kelp.png", "bg_bluewater.png", "bg_shelf.png", "bg_trench.png",
	"dock.png", "shop_interior.png", "ui_panel.png",
}


def read_png_rgba(path: Path):
    data = path.read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n"
    pos = 8
    w = h = None
    idat = b""
    while pos < len(data):
        length = struct.unpack(">I", data[pos:pos + 4])[0]
        tag = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if tag == b"IHDR":
            w, h, bit, ctype = struct.unpack(">IIBB", chunk[:10])
            if bit != 8 or ctype != 6:
                raise ValueError(f"{path.name}: need 8-bit RGBA, got bit={bit} type={ctype}")
        elif tag == b"IDAT":
            idat += chunk
        elif tag == b"IEND":
            break
    raw = zlib.decompress(idat)
    rows = []
    stride = w * 4 + 1
    for y in range(h):
        row = raw[y * stride:(y + 1) * stride]
        if row[0] != 0:
            raise ValueError(f"{path.name}: unsupported filter {row[0]}")
        rows.append(row[1:])
    rgba = b"".join(rows)
    return w, h, rgba


def validate(path: Path) -> list[str]:
    errors = []
    try:
        w, h, rgba = read_png_rgba(path)
    except Exception as e:
        return [f"{path.name}: {e}"]

    alphas = rgba[3::4]
    if path.name in OPAQUE_OK:
        return errors

    if all(a == 255 for a in alphas):
        errors.append(f"{path.name}: no transparent pixels (expected cutout alpha)")

    # Corner matte check: at least 2 of 4 corners should be transparent for cutouts.
    corners = [0, w - 1, (h - 1) * w, (h - 1) * w + (w - 1)]
    transparent_corners = sum(1 for i in corners if rgba[i * 4 + 3] < 16)
    if transparent_corners < 2:
        errors.append(f"{path.name}: corners look matted ({transparent_corners}/4 transparent)")

    return errors


def main() -> int:
    if not ART.exists():
        print("No art folder yet")
        return 1
    errors = []
    for png in sorted(ART.rglob("*.png")):
        errors.extend(validate(png))
    if errors:
        print("ALPHA VALIDATION FAILED:")
        for e in errors:
            print(" -", e)
        return 1
    print(f"Validated {len(list(ART.rglob('*.png')))} PNGs OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())

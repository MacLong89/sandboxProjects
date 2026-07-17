#!/usr/bin/env python3
"""Copy generated hero images into game textures and force true alpha cutouts."""
from __future__ import annotations

import struct
import zlib
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    Image = None

ROOT = Path(__file__).resolve().parents[2]
CURSOR_ASSETS = Path.home() / ".cursor" / "projects" / "c-Users-Macra-Projects-sandboxProjects-offshore-fishing" / "assets"
OUT = ROOT / "Assets" / "textures" / "art"


def write_png(path: Path, w: int, h: int, rgba: bytes) -> None:
    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = b"".join(b"\x00" + rgba[y * w * 4:(y + 1) * w * 4] for y in range(h))
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def nearest(src_rgba, sw, sh, dw, dh):
    out = bytearray(dw * dh * 4)
    for y in range(dh):
        sy = min(sh - 1, y * sh // dh)
        for x in range(dw):
            sx = min(sw - 1, x * sw // dw)
            si = (sy * sw + sx) * 4
            di = (y * dw + x) * 4
            out[di:di + 4] = src_rgba[si:si + 4]
    return bytes(out)


def process_cutout(src: Path, dest: Path, size: int):
    if Image is None:
        # fallback copy raw if already png-ish
        dest.write_bytes(src.read_bytes())
        return
    im = Image.open(src).convert("RGBA")
    # Make near-white / checker-like backgrounds transparent
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            # checkerboard greys and pure whiteish mats
            if a < 8:
                continue
            if abs(r - g) < 12 and abs(g - b) < 12 and r > 200:
                px[x, y] = (0, 0, 0, 0)
            elif abs(r - 192) < 20 and abs(g - 192) < 20 and abs(b - 192) < 20:
                px[x, y] = (0, 0, 0, 0)
            elif abs(r - 128) < 20 and abs(g - 128) < 20 and abs(b - 128) < 20 and a > 200:
                # only if neighbors look like checker - keep simple: ignore
                pass
    im = im.resize((size, size), Image.Resampling.NEAREST)
    rgba = im.tobytes()
    write_png(dest, size, size, rgba)


def process_bg(src: Path, dest_name: str, dw=320, dh=180):
    if Image is None:
        return
    im = Image.open(src).convert("RGBA").resize((dw, dh), Image.Resampling.NEAREST)
    write_png(OUT / dest_name, dw, dh, im.tobytes())


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    mapping_bg = {
        "hero_dock.png": ["bg_harbor.png", "dock.png"],
        "hero_ocean.png": ["bg_bluewater.png", "bg_kelp.png"],
        "hero_shop.png": ["shop_interior.png"],
    }
    for src_name, dests in mapping_bg.items():
        src = CURSOR_ASSETS / src_name
        if not src.exists():
            print(f"Missing {src}")
            continue
        for dest in dests:
            process_bg(src, dest)
            print(f"Imported {src_name} -> {dest}")

    hero_fish = CURSOR_ASSETS / "hero_yellowfin.png"
    if hero_fish.exists():
        process_cutout(hero_fish, OUT / "blue_yellowfin.png", 32)
        print("Imported hero yellowfin")

    hero_boat = CURSOR_ASSETS / "hero_boat.png"
    if hero_boat.exists() and Image is not None:
        im = Image.open(hero_boat).convert("RGBA")
        px = im.load()
        w, h = im.size
        for y in range(h):
            for x in range(w):
                r, g, b, a = px[x, y]
                if a < 8:
                    continue
                if abs(r - g) < 14 and abs(g - b) < 14 and r > 190:
                    px[x, y] = (0, 0, 0, 0)
        im = im.resize((64, 32), Image.Resampling.NEAREST)
        write_png(OUT / "boat_fisher.png", 64, 32, im.tobytes())
        write_png(OUT / "boat_skiff.png", 64, 32, im.tobytes())
        print("Imported hero boat")
    print("Done")


if __name__ == "__main__":
    main()

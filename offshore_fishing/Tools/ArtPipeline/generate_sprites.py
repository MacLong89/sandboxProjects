#!/usr/bin/env python3
"""Generate cozy pixel-art PNGs with true alpha for Offshore Fishing."""
from __future__ import annotations

import json
import math
import os
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "textures" / "art"
MANIFEST = ROOT / "Assets" / "Art" / "sprite_manifest.json"


def write_png(path: Path, w: int, h: int, rgba: bytes) -> None:
    """Write RGBA8888 PNG without external deps."""
    assert len(rgba) == w * h * 4

    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = b"".join(b"\x00" + rgba[y * w * 4:(y + 1) * w * 4] for y in range(h))
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def new_canvas(w: int, h: int, fill=(0, 0, 0, 0)):
    return [list(fill) for _ in range(w * h)], w, h


def setp(px, w, h, x, y, c):
    if 0 <= x < w and 0 <= y < h:
        px[y * w + x] = list(c)


def rect(px, w, h, x, y, rw, rh, c):
    for yy in range(y, y + rh):
        for xx in range(x, x + rw):
            setp(px, w, h, xx, yy, c)


def ellipse(px, w, h, cx, cy, rx, ry, c):
    for y in range(-ry, ry + 1):
        for x in range(-rx, rx + 1):
            if (x * x) * (ry * ry) + (y * y) * (rx * rx) <= rx * rx * ry * ry:
                setp(px, w, h, cx + x, cy + y, c)


def outline_rect(px, w, h, x, y, rw, rh, c):
    for xx in range(x, x + rw):
        setp(px, w, h, xx, y, c)
        setp(px, w, h, xx, y + rh - 1, c)
    for yy in range(y, y + rh):
        setp(px, w, h, x, yy, c)
        setp(px, w, h, x + rw - 1, yy, c)


def to_bytes(px):
    return bytes(v for p in px for v in p)


def make_bg(name, sky_top, sky_bot, water, sand, accent=None):
    w, h = 320, 180
    px, _, _ = new_canvas(w, h, (0, 0, 0, 255))
    for y in range(h):
        for x in range(w):
            if y < int(h * 0.42):
                t = y / (h * 0.42)
                c = tuple(int(a + (b - a) * t) for a, b in zip(sky_top, sky_bot)) + (255,)
            elif y < int(h * 0.88):
                t = (y - h * 0.42) / (h * 0.46)
                c = tuple(int(a * (1 - t * 0.45)) for a in water) + (255,)
                if (x + y * 3) % 41 == 0:
                    c = tuple(min(255, v + 18) for v in c[:3]) + (255,)
            else:
                c = sand + (255,)
                if (x * 11 + y * 5) % 27 == 0:
                    c = tuple(max(0, v - 20) for v in c[:3]) + (255,)
            setp(px, w, h, x, y, c)
    if accent:
        for i in range(8):
            ellipse(px, w, h, 40 + i * 35, int(h * 0.55 + (i % 3) * 8), 6, 3, accent + (180,))
    # islands
    rect(px, w, h, 240, 55, 40, 12, (90, 120, 90, 255))
    rect(px, w, h, 250, 45, 8, 14, (160, 160, 150, 255))
    write_png(OUT / f"{name}.png", w, h, to_bytes(px))


def make_boat(name, hull, cabin):
    w, h = 64, 32
    px, _, _ = new_canvas(w, h)
    rect(px, w, h, 4, 16, 56, 10, hull + (255,))
    outline_rect(px, w, h, 4, 16, 56, 10, (30, 24, 18, 255))
    rect(px, w, h, 28, 6, 18, 12, cabin + (255,))
    outline_rect(px, w, h, 28, 6, 18, 12, (20, 20, 30, 255))
    rect(px, w, h, 32, 1, 3, 6, (180, 180, 180, 255))
    rect(px, w, h, 10, 14, 5, 3, (200, 60, 60, 255))
    write_png(OUT / f"{name}.png", w, h, to_bytes(px))


def make_player():
    w, h = 16, 24
    px, _, _ = new_canvas(w, h)
    rect(px, w, h, 5, 1, 6, 5, (40, 90, 150, 255))
    rect(px, w, h, 5, 6, 6, 5, (230, 190, 150, 255))
    rect(px, w, h, 4, 11, 8, 8, (50, 70, 110, 255))
    rect(px, w, h, 4, 19, 3, 4, (40, 40, 50, 255))
    rect(px, w, h, 9, 19, 3, 4, (40, 40, 50, 255))
    write_png(OUT / "player.png", w, h, to_bytes(px))


def make_shopkeeper():
    w, h = 24, 32
    px, _, _ = new_canvas(w, h)
    rect(px, w, h, 7, 2, 10, 6, (50, 90, 140, 255))  # beanie
    rect(px, w, h, 7, 8, 10, 7, (230, 200, 170, 255))
    rect(px, w, h, 6, 15, 12, 10, (120, 80, 50, 255))  # apron
    rect(px, w, h, 8, 12, 8, 4, (240, 240, 240, 255))  # beard
    write_png(OUT / "shopkeeper.png", w, h, to_bytes(px))


def make_fish(name, body, fin, rare=False):
    w, h = 32, 16
    px, _, _ = new_canvas(w, h)
    ellipse(px, w, h, 14, 8, 11, 5, body + (255,))
    # tail
    for y in range(3, 13):
        for x in range(24, 31):
            if abs((y - 8) - (x - 30) * 0.7) < 3:
                setp(px, w, h, x, y, fin + (255,))
    setp(px, w, h, 10, 6, (255, 255, 255, 255))
    setp(px, w, h, 9, 6, (10, 10, 10, 255))
    if rare:
        ellipse(px, w, h, 14, 8, 12, 6, body[:3] + (40,))
    write_png(OUT / f"{name}.png", w, h, to_bytes(px))


def make_icon(name, color):
    w, h = 16, 16
    px, _, _ = new_canvas(w, h)
    rect(px, w, h, 2, 2, 12, 12, color + (255,))
    outline_rect(px, w, h, 2, 2, 12, 12, (20, 16, 12, 255))
    write_png(OUT / f"{name}.png", w, h, to_bytes(px))


def make_ui_panel():
    w, h = 64, 64
    px, _, _ = new_canvas(w, h)  # transparent canvas
    rect(px, w, h, 2, 2, 60, 60, (18, 24, 28, 230))
    outline_rect(px, w, h, 2, 2, 60, 60, (196, 146, 58, 255))
    # keep true transparent corners
    setp(px, w, h, 0, 0, (0, 0, 0, 0))
    setp(px, w, h, w - 1, 0, (0, 0, 0, 0))
    setp(px, w, h, 0, h - 1, (0, 0, 0, 0))
    setp(px, w, h, w - 1, h - 1, (0, 0, 0, 0))
    write_png(OUT / "ui_panel.png", w, h, to_bytes(px))


def make_dock():
    w, h = 320, 180
    px, _, _ = new_canvas(w, h, (0, 0, 0, 255))
    # sky/water first
    for y in range(h):
        for x in range(w):
            if y < 70:
                setp(px, w, h, x, y, (140, 190, 230, 255))
            elif y < 150:
                setp(px, w, h, x, y, (30, 110, 130, 255))
            else:
                setp(px, w, h, x, y, (40, 90, 100, 255))
    # shop building
    rect(px, w, h, 10, 40, 70, 50, (120, 80, 45, 255))
    rect(px, w, h, 20, 55, 20, 30, (60, 40, 25, 255))
    # pier
    rect(px, w, h, 80, 85, 180, 12, (90, 60, 35, 255))
    for i in range(8):
        rect(px, w, h, 90 + i * 22, 97, 4, 30, (70, 45, 25, 255))
    write_png(OUT / "dock.png", w, h, to_bytes(px))
    write_png(OUT / "shop_interior.png", w, h, to_bytes(px))


def make_anim_sheet(name, frames, frame_w, frame_h, painter):
    w, h = frame_w * frames, frame_h
    px, _, _ = new_canvas(w, h)
    for i in range(frames):
        painter(px, w, h, i * frame_w, 0, i)
    write_png(OUT / "anims" / f"{name}.png", w, h, to_bytes(px))


def paint_player_walk(px, w, h, ox, oy, frame):
    leg = 1 if frame % 2 == 0 else -1
    rect(px, w, h, ox + 5, oy + 1, 6, 5, (40, 90, 150, 255))
    rect(px, w, h, ox + 5, oy + 6, 6, 5, (230, 190, 150, 255))
    rect(px, w, h, ox + 4, oy + 11, 8, 8, (50, 70, 110, 255))
    rect(px, w, h, ox + 4, oy + 19, 3, 4, (40, 40, 50, 255))
    rect(px, w, h, ox + 9 + leg, oy + 19, 3, 4, (40, 40, 50, 255))


def paint_fish_swim(px, w, h, ox, oy, frame):
    body = (70, 150, 200)
    fin = (40, 100, 140)
    bob = (frame % 4) - 1
    ellipse(px, w, h, ox + 14, oy + 8 + bob, 11, 5, body + (255,))
    for y in range(3, 13):
        for x in range(24, 31):
            if abs((y - 8) - (x - 30) * 0.7) < 3:
                setp(px, w, h, ox + x, oy + y + bob, fin + (255,))


def paint_splash(px, w, h, ox, oy, frame):
    for i in range(frame + 1):
        ellipse(px, w, h, ox + 8 + i * 2, oy + 10 - i, 2 + i, 1 + i, (200, 230, 255, 200))


FISH = [
    ("harbor_minnow", (180, 190, 200), (140, 150, 160)),
    ("harbor_flounder", (190, 170, 120), (150, 130, 90)),
    ("harbor_mackerel", (80, 140, 160), (50, 100, 120)),
    ("harbor_perch", (90, 150, 80), (60, 110, 50)),
    ("harbor_ray", (120, 130, 150), (90, 100, 120)),
    ("harbor_seabass", (70, 90, 110), (40, 50, 70)),
    ("kelp_garibaldi", (240, 140, 40), (200, 100, 20)),
    ("kelp_sheephead", (200, 80, 90), (150, 50, 60)),
    ("kelp_rockfish", (180, 100, 60), (140, 70, 40)),
    ("kelp_lingcod", (100, 140, 90), (70, 100, 60)),
    ("kelp_halibut", (170, 160, 130), (120, 110, 90)),
    ("kelp_wolf", (90, 100, 120), (60, 70, 90)),
    ("blue_bonito", (90, 160, 190), (50, 110, 140)),
    ("blue_mahi", (80, 200, 120), (240, 200, 40)),
    ("blue_yellowfin", (50, 120, 180), (240, 200, 50), True),
    ("blue_wahoo", (60, 100, 140), (40, 70, 100)),
    ("blue_marlin", (40, 90, 150), (30, 60, 110), True),
    ("blue_sunfish", (180, 180, 120), (140, 140, 90)),
    ("shelf_cod", (150, 150, 160), (110, 110, 120)),
    ("shelf_grouper", (120, 140, 100), (80, 100, 70)),
    ("shelf_swordfish", (70, 90, 120), (40, 50, 80), True),
    ("shelf_oilfish", (80, 70, 60), (50, 40, 30)),
    ("shelf_crabking", (200, 80, 60), (150, 50, 40)),
    ("shelf_sleeper", (50, 55, 70), (30, 35, 45), True),
    ("trench_hatchetfish", (180, 200, 80), (120, 140, 40)),
    ("trench_viper", (80, 40, 50), (40, 20, 25)),
    ("trench_gulper", (60, 40, 70), (30, 20, 40)),
    ("trench_angler", (40, 50, 30), (200, 220, 80), True),
    ("trench_phantom", (70, 90, 110), (40, 60, 80), True),
    ("trench_leviathan", (30, 20, 50), (120, 60, 180), True),
]

ITEMS = [
    "rod_starter", "rod_fiberglass", "rod_carbon", "rod_titanium", "rod_abyss",
    "spool_basic", "spool_braided", "spool_deep", "spool_spectra", "spool_void",
    "hook_basic", "hook_better", "hook_barbed", "hook_circle", "hook_abyss",
    "bait_worms", "bait_minnows", "bait_squid", "bait_shrimp", "bait_jellyfish", "bait_premium",
]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "anims").mkdir(parents=True, exist_ok=True)

    make_bg("bg_harbor", (150, 190, 230), (220, 200, 150), (40, 120, 140), (170, 150, 90), (200, 230, 240))
    make_bg("bg_kelp", (120, 180, 220), (180, 210, 230), (20, 110, 90), (120, 100, 50), (40, 160, 90))
    make_bg("bg_bluewater", (90, 150, 230), (160, 200, 255), (10, 60, 140), (60, 70, 90), (80, 180, 220))
    make_bg("bg_shelf", (70, 90, 120), (110, 120, 140), (15, 35, 80), (70, 65, 55))
    make_bg("bg_trench", (10, 10, 25), (20, 20, 40), (5, 10, 30), (15, 10, 25), (80, 40, 120))

    make_boat("boat_skiff", (180, 120, 70), (50, 100, 160))
    make_boat("boat_fisher", (235, 235, 240), (40, 90, 150))
    make_boat("boat_explorer", (220, 180, 90), (50, 80, 120))
    make_boat("boat_oceanic", (210, 215, 225), (30, 60, 100))

    make_player()
    make_shopkeeper()
    make_dock()
    make_ui_panel()

    for name, body, fin, *rest in FISH:
        make_fish(name, body, fin, rare=bool(rest))

    for i, name in enumerate(ITEMS):
        hue = (40 + i * 17) % 200
        make_icon(name, (80 + hue // 2, 70 + (i * 13) % 100, 50 + (i * 9) % 80))

    make_anim_sheet("player_walk", 4, 16, 24, paint_player_walk)
    make_anim_sheet("fish_swim", 4, 32, 16, paint_fish_swim)
    make_anim_sheet("splash", 4, 16, 16, paint_splash)

    # bobber / particles
    px, w, h = new_canvas(8, 8)
    rect(px, w, h, 2, 1, 4, 3, (220, 50, 50, 255))
    rect(px, w, h, 2, 4, 4, 3, (240, 240, 240, 255))
    write_png(OUT / "bobber.png", w, h, to_bytes(px))

    entries = sorted(p.relative_to(OUT).as_posix() for p in OUT.rglob("*.png"))
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps({"sprites": entries, "transparent_required": True}, indent=2), encoding="utf-8")
    print(f"Wrote {len(entries)} sprites to {OUT}")


if __name__ == "__main__":
    main()

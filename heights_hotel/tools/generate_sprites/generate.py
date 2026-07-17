#!/usr/bin/env python3
"""Generate Heights Hotel pixel sprites with true RGBA transparency."""

from __future__ import annotations

import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "ui" / "sprites"
SOURCE = Path(__file__).resolve().parent / "source_art"
CELL_W, CELL_H = 96, 64
CHAR_W, CHAR_H = 16, 24

# Cozy dusk palette
PAL = {
    "outline": (40, 28, 36, 255),
    "wall": (92, 64, 72, 255),
    "wall_hi": (120, 86, 92, 255),
    "floor": (156, 112, 88, 255),
    "floor_hi": (180, 132, 100, 255),
    "wood": (120, 78, 52, 255),
    "wood_hi": (148, 98, 64, 255),
    "cream": (242, 220, 190, 255),
    "cream_dim": (220, 196, 168, 255),
    "gold": (220, 168, 72, 255),
    "gold_hi": (245, 200, 100, 255),
    "teal": (72, 140, 140, 255),
    "teal_hi": (100, 176, 168, 255),
    "rose": (196, 96, 110, 255),
    "rose_hi": (220, 130, 140, 255),
    "blue": (88, 120, 176, 255),
    "blue_hi": (120, 150, 200, 255),
    "green": (88, 140, 96, 255),
    "green_hi": (120, 170, 120, 255),
    "purple": (120, 96, 150, 255),
    "night1": (28, 32, 58, 255),
    "night2": (68, 48, 72, 255),
    "night3": (140, 88, 72, 255),
    "window": (255, 220, 140, 255),
    "dirt": (90, 70, 50, 180),
    "broken": (200, 60, 60, 200),
    "ghost": (255, 255, 255, 90),
    "white": (255, 255, 255, 255),
    "black": (20, 16, 18, 255),
    "skin": (232, 190, 160, 255),
    "hair1": (60, 40, 30, 255),
    "hair2": (180, 120, 60, 255),
    "hair3": (90, 70, 120, 255),
    "shirt_r": (200, 90, 90, 255),
    "shirt_b": (80, 120, 180, 255),
    "shirt_g": (80, 150, 110, 255),
    "shirt_y": (210, 180, 70, 255),
    "pants": (60, 60, 80, 255),
}


def new_rgba(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def px(draw: ImageDraw.ImageDraw, xy, color):
    draw.point(xy, fill=color)


def rect(draw: ImageDraw.ImageDraw, box, color, outline=None):
    draw.rectangle(box, fill=color, outline=outline)


# Full-bleed / solid cell art may be opaque; silhouettes must have A=0 outside.
OPAQUE_OK_PREFIXES = ("backdrop_", "sky_", "room_", "structure_floor", "structure_wall", "structure_roof", "structure_elevator")


def must_have_holes(stem: str) -> bool:
    return not any(stem.startswith(p) for p in OPAQUE_OK_PREFIXES)


def save_png(img: Image.Image, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    img.save(path, format="PNG")
    verify_transparency(path)


def verify_transparency(path: Path):
    img = Image.open(path)
    if img.mode != "RGBA":
        raise SystemExit(f"{path}: missing alpha channel (mode={img.mode})")
    if must_have_holes(path.stem) and img.getchannel("A").getextrema()[0] != 0:
        raise SystemExit(f"{path}: expected transparent pixels (A=0) outside silhouette")


def draw_room_shell(draw: ImageDraw.ImageDraw, accent, floor=None):
    floor = floor or PAL["floor"]
    rect(draw, (0, 0, CELL_W - 1, CELL_H - 1), PAL["wall"], PAL["outline"])
    rect(draw, (3, 3, CELL_W - 4, CELL_H - 11), PAL["cream_dim"])
    # subtle wallpaper pattern
    for x in range(7, CELL_W - 5, 12):
        draw.line((x, 8, x, CELL_H - 13), fill=(205, 180, 158, 255))
        draw.point((x + 2, 12), fill=(235, 208, 178, 255))
        draw.point((x - 2, 24), fill=(235, 208, 178, 255))
    rect(draw, (3, CELL_H - 11, CELL_W - 4, CELL_H - 3), floor)
    # floorboards, ceiling beam and crown molding
    for x in range(7, CELL_W - 4, 12):
        draw.line((x, CELL_H - 10, x, CELL_H - 4), fill=PAL["wood"])
    rect(draw, (3, 3, CELL_W - 4, 7), PAL["wood"])
    rect(draw, (3, 7, CELL_W - 4, 8), accent)
    rect(draw, (3, CELL_H - 14, CELL_W - 4, CELL_H - 12), accent)
    # two deep blue windows with warm frames
    for wx in (10, 54):
        rect(draw, (wx, 13, wx + 16, 31), PAL["outline"])
        rect(draw, (wx + 2, 15, wx + 14, 29), PAL["night2"])
        draw.line((wx + 8, 15, wx + 8, 29), fill=PAL["gold"])
        draw.line((wx + 2, 22, wx + 14, 22), fill=PAL["gold"])
        draw.point((wx + 4, 18), fill=PAL["window"])
    # pendant lamps
    for lx in (38, 58):
        draw.line((lx, 5, lx, 12), fill=PAL["outline"])
        rect(draw, (lx - 3, 11, lx + 3, 14), PAL["gold_hi"], PAL["outline"])


def draw_room_door(draw: ImageDraw.ImageDraw):
    rect(draw, (80, 19, 92, CELL_H - 12), PAL["outline"])
    rect(draw, (82, 21, 90, CELL_H - 12), PAL["wood"])
    draw.point((88, 36), fill=PAL["gold_hi"])


def gen_backdrop():
    source = SOURCE / "city_backdrop_source.png"
    if source.exists():
        raw = Image.open(source).convert("RGBA")
        target_ratio = 16 / 9
        crop_h = int(raw.width / target_ratio)
        if crop_h <= raw.height:
            top = (raw.height - crop_h) // 2
            raw = raw.crop((0, top, raw.width, top + crop_h))
        else:
            crop_w = int(raw.height * target_ratio)
            left = (raw.width - crop_w) // 2
            raw = raw.crop((left, 0, left + crop_w, raw.height))
        img = raw.resize((640, 360), Image.Resampling.NEAREST)
    else:
        img = new_rgba(640, 360)
        d = ImageDraw.Draw(img)
        for y in range(360):
            t = y / 359
            r = int(PAL["night1"][0] * (1 - t) + PAL["night3"][0] * t)
            g = int(PAL["night1"][1] * (1 - t) + PAL["night3"][1] * t)
            b = int(PAL["night1"][2] * (1 - t) + PAL["night3"][2] * t)
            d.line([(0, y), (639, y)], fill=(r, g, b, 255))
    path = OUT / "backdrop_sky.png"
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG")
    return {"backdrop_sky": {"path": "backdrop_sky.png", "frameW": 640, "frameH": 360, "frames": 1, "fps": 1, "loop": True, "pivot": [320, 360], "opaque": True}}


def gen_logo(catalog: dict):
    source = SOURCE / "logo_source.png"
    if not source.exists():
        return
    raw = Image.open(source).convert("RGBA")
    pixels = raw.load()
    # The generated source has a white matte. Remove only near-white pixels,
    # then crop to content and resize with hard nearest-neighbour edges.
    for y in range(raw.height):
        for x in range(raw.width):
            r, g, b, a = pixels[x, y]
            if min(r, g, b) > 205 and max(r, g, b) - min(r, g, b) < 14:
                pixels[x, y] = (0, 0, 0, 0)
    bbox = raw.getbbox()
    if bbox:
        raw = raw.crop(bbox)
    target_w = 236
    target_h = max(1, int(raw.height * target_w / raw.width))
    img = raw.resize((target_w, target_h), Image.Resampling.NEAREST)
    save_png(img, OUT / "ui_logo.png")
    catalog["ui_logo"] = entry("ui_logo.png", target_w, target_h, 1, 1, pivot=[target_w // 2, target_h // 2])


def gen_structure(catalog: dict):
    # lobby
    img = new_rgba(CELL_W, CELL_H)
    d = ImageDraw.Draw(img)
    draw_room_shell(d, PAL["gold"])
    rect(d, (10, 28, 40, CELL_H - 11), PAL["wood"], PAL["outline"])  # desk
    rect(d, (14, 20, 36, 28), PAL["gold_hi"], PAL["outline"])  # sign
    rect(d, (50, 18, 58, CELL_H - 11), PAL["teal"], PAL["outline"])  # plant
    rect(d, (70, 34, 86, CELL_H - 11), PAL["cream"], PAL["outline"])  # sofa
    draw_room_door(d)
    save_png(img, OUT / "room_lobby.png")
    catalog["room_lobby"] = entry("room_lobby.png", CELL_W, CELL_H, 1, 1)

    # floor plate / exterior wall / roof / elevator / preview
    for name, painter in [
        ("structure_floor", lambda d: (rect(d, (0, CELL_H - 8, CELL_W - 1, CELL_H - 1), PAL["wall"], PAL["outline"]), rect(d, (0, CELL_H - 6, CELL_W - 1, CELL_H - 3), PAL["wall_hi"]))),
        ("structure_wall", lambda d: rect(d, (0, 0, 6, CELL_H - 1), PAL["wall"], PAL["outline"])),
        ("structure_roof", lambda d: (rect(d, (0, 0, CELL_W - 1, 10), PAL["rose"], PAL["outline"]), rect(d, (4, 2, CELL_W - 5, 6), PAL["rose_hi"]))),
        ("structure_elevator", lambda d: (rect(d, (36, 4, 60, CELL_H - 4), PAL["wall_hi"], PAL["outline"]), rect(d, (40, 10, 56, CELL_H - 10), PAL["black"]), rect(d, (42, 12, 48, CELL_H - 12), PAL["gold"]))),
        ("structure_preview", lambda d: rect(d, (2, 2, CELL_W - 3, CELL_H - 3), PAL["ghost"], (255, 255, 255, 140))),
    ]:
        img = new_rgba(CELL_W, CELL_H)
        d = ImageDraw.Draw(img)
        painter(d)
        save_png(img, OUT / f"{name}.png")
        catalog[name] = entry(f"{name}.png", CELL_W, CELL_H, 1, 1)

    # Narrow elevator segment used as a repeated exterior shaft.
    img = new_rgba(24, CELL_H)
    d = ImageDraw.Draw(img)
    rect(d, (0, 0, 23, 63), PAL["outline"])
    rect(d, (3, 2, 20, 61), PAL["wall"])
    rect(d, (5, 7, 18, 55), PAL["black"])
    rect(d, (7, 9, 11, 53), PAL["gold"])
    rect(d, (12, 9, 16, 53), PAL["gold"])
    rect(d, (5, 3, 18, 6), PAL["wall_hi"])
    save_png(img, OUT / "structure_elevator_segment.png")
    catalog["structure_elevator_segment"] = entry("structure_elevator_segment.png", 24, CELL_H, 1, 1)

    # Rooftop silhouette: string lights, planters, parasol and a tiny pool.
    img = new_rgba(192, 44)
    d = ImageDraw.Draw(img)
    rect(d, (0, 37, 191, 43), PAL["outline"])
    rect(d, (3, 35, 188, 39), PAL["wall_hi"])
    for x in (12, 46, 146, 178):
        rect(d, (x, 26, x + 10, 36), PAL["wood"], PAL["outline"])
        d.ellipse((x - 2, 18, x + 12, 29), fill=PAL["green"], outline=PAL["outline"])
    d.line((18, 8, 96, 19, 174, 7), fill=PAL["outline"], width=1)
    for x, y in ((28, 10), (48, 13), (68, 16), (88, 18), (108, 17), (128, 14), (150, 10), (170, 7)):
        d.point((x, y), fill=PAL["gold_hi"])
        d.point((x, y + 1), fill=PAL["gold"])
    d.polygon([(55, 20), (72, 11), (89, 20)], fill=PAL["rose"], outline=PAL["outline"])
    rect(d, (71, 19, 73, 36), PAL["outline"])
    rect(d, (108, 27, 145, 36), PAL["teal"], PAL["outline"])
    rect(d, (112, 29, 141, 33), PAL["teal_hi"])
    save_png(img, OUT / "structure_rooftop_deck.png")
    catalog["structure_rooftop_deck"] = entry("structure_rooftop_deck.png", 192, 44, 1, 1, pivot=[96, 44])

    # Visible upgrade layers add richer furniture and warm sparkle per room level.
    for level in range(2, 6):
        img = new_rgba(CELL_W, CELL_H)
        d = ImageDraw.Draw(img)
        color = [PAL["teal_hi"], PAL["gold_hi"], PAL["rose_hi"], PAL["white"]][level - 2]
        # framed artwork, upgraded lamp, rug edge, and level-dependent sparkle
        rect(d, (43, 17, 53, 27), PAL["wood"], PAL["outline"])
        rect(d, (45, 19, 51, 25), color)
        rect(d, (32, 51, 66, 54), color, PAL["outline"])
        rect(d, (72, 34, 77, 52), PAL["wood_hi"], PAL["outline"])
        rect(d, (69, 30, 80, 35), PAL["gold_hi"], PAL["outline"])
        for i in range(level):
            x = 30 + i * 9
            y = 12 + (i % 2) * 4
            d.point((x, y), fill=PAL["white"])
            d.point((x - 1, y), fill=color)
            d.point((x + 1, y), fill=color)
            d.point((x, y - 1), fill=color)
            d.point((x, y + 1), fill=color)
        name = f"overlay_level_{level}"
        save_png(img, OUT / f"{name}.png")
        catalog[name] = entry(f"{name}.png", CELL_W, CELL_H, 1, 1)


def furniture_for(room: str, d: ImageDraw.ImageDraw):
    if room == "standard":
        rect(d, (10, 30, 42, 50), PAL["blue"], PAL["outline"])  # bed
        rect(d, (12, 28, 40, 34), PAL["cream"])  # pillow
        rect(d, (55, 34, 70, 54), PAL["wood"], PAL["outline"])  # nightstand
        rect(d, (58, 26, 66, 34), PAL["window"])  # lamp
    elif room == "deluxe":
        rect(d, (8, 28, 48, 52), PAL["purple"], PAL["outline"])
        rect(d, (10, 26, 46, 32), PAL["cream"])
        rect(d, (58, 20, 80, 54), PAL["wood"], PAL["outline"])
        rect(d, (62, 24, 76, 36), PAL["teal_hi"])
    elif room == "suite":
        rect(d, (6, 26, 50, 52), PAL["gold"], PAL["outline"])
        rect(d, (8, 24, 48, 30), PAL["white"])
        rect(d, (55, 18, 88, 40), PAL["teal"], PAL["outline"])
        rect(d, (60, 42, 85, 54), PAL["wood"], PAL["outline"])
    elif room == "cafe":
        rect(d, (8, 36, 50, 54), PAL["wood"], PAL["outline"])  # counter
        rect(d, (14, 28, 22, 36), PAL["rose_hi"])  # cups
        rect(d, (28, 28, 36, 36), PAL["cream"])
        rect(d, (60, 40, 72, 54), PAL["wood_hi"], PAL["outline"])  # stool
        rect(d, (78, 40, 90, 54), PAL["wood_hi"], PAL["outline"])
    elif room == "restaurant":
        rect(d, (12, 36, 36, 52), PAL["wood"], PAL["outline"])
        rect(d, (44, 36, 68, 52), PAL["wood"], PAL["outline"])
        rect(d, (18, 30, 30, 36), PAL["white"])
        rect(d, (50, 30, 62, 36), PAL["white"])
        rect(d, (78, 20, 90, 54), PAL["wall_hi"], PAL["outline"])  # kitchen pass
    elif room == "spa":
        rect(d, (16, 34, 50, 54), PAL["teal"], PAL["outline"])
        rect(d, (20, 38, 46, 50), PAL["teal_hi"])
        rect(d, (60, 20, 78, 54), PAL["cream"], PAL["outline"])
        rect(d, (64, 16, 74, 22), PAL["green_hi"])  # plant
    elif room == "gift":
        rect(d, (10, 24, 30, 54), PAL["rose"], PAL["outline"])
        rect(d, (34, 24, 54, 54), PAL["blue"], PAL["outline"])
        rect(d, (58, 24, 78, 54), PAL["green"], PAL["outline"])
        rect(d, (14, 28, 26, 36), PAL["gold_hi"])
        rect(d, (38, 28, 50, 36), PAL["white"])
    elif room == "laundry":
        rect(d, (12, 28, 36, 54), PAL["blue_hi"], PAL["outline"])
        rect(d, (18, 34, 30, 46), PAL["white"])
        rect(d, (44, 28, 68, 54), PAL["blue_hi"], PAL["outline"])
        rect(d, (50, 34, 62, 46), PAL["white"])
        rect(d, (74, 40, 88, 54), PAL["wood"], PAL["outline"])
    elif room == "workshop":
        rect(d, (10, 36, 50, 54), PAL["wood"], PAL["outline"])
        rect(d, (16, 28, 24, 36), PAL["gold"])  # tools
        rect(d, (28, 26, 36, 36), PAL["wall_hi"])
        rect(d, (60, 20, 85, 54), PAL["wall"], PAL["outline"])
    elif room == "staff":
        rect(d, (12, 34, 40, 54), PAL["green"], PAL["outline"])  # couch
        rect(d, (50, 28, 70, 54), PAL["wood"], PAL["outline"])  # locker
        rect(d, (76, 40, 90, 54), PAL["wood_hi"], PAL["outline"])  # table


def gen_rooms(catalog: dict):
    rooms = [
        ("standard", "room_standard", PAL["blue"]),
        ("deluxe", "room_deluxe", PAL["purple"]),
        ("suite", "room_suite", PAL["gold"]),
        ("cafe", "room_cafe", PAL["rose"]),
        ("restaurant", "room_restaurant", PAL["wood"]),
        ("spa", "room_spa", PAL["teal"]),
        ("gift", "room_giftshop", PAL["green"]),
        ("laundry", "room_laundry", PAL["blue_hi"]),
        ("workshop", "room_workshop", PAL["wall_hi"]),
        ("staff", "room_staff", PAL["green_hi"]),
    ]
    for key, name, accent in rooms:
        img = new_rgba(CELL_W, CELL_H)
        d = ImageDraw.Draw(img)
        draw_room_shell(d, accent)
        furniture_for(key, d)
        draw_room_door(d)
        save_png(img, OUT / f"{name}.png")
        catalog[name] = entry(f"{name}.png", CELL_W, CELL_H, 1, 1)


def gen_overlays(catalog: dict):
    specs = {
        "overlay_dirty": lambda d: [rect(d, (8 + i * 12, 40 + (i % 3) * 4, 14 + i * 12, 46 + (i % 3) * 4), PAL["dirt"]) for i in range(6)],
        "overlay_broken": lambda d: (d.line([(20, 16), (40, 40)], fill=PAL["broken"], width=2), d.line([(70, 12), (50, 36)], fill=PAL["broken"], width=2), rect(d, (44, 20, 52, 28), PAL["broken"])),
        "overlay_construction": lambda d: [rect(d, (10 + i * 14, 20, 20 + i * 14, 50), (200, 160, 80, 160), PAL["outline"]) for i in range(5)],
        "overlay_locked": lambda d: (rect(d, (36, 22, 60, 46), (0, 0, 0, 140), PAL["outline"]), rect(d, (42, 16, 54, 26), (0, 0, 0, 0), PAL["gold"])),
    }
    for name, painter in specs.items():
        img = new_rgba(CELL_W, CELL_H)
        d = ImageDraw.Draw(img)
        painter(d)
        save_png(img, OUT / f"{name}.png")
        catalog[name] = entry(f"{name}.png", CELL_W, CELL_H, 1, 1)


def draw_char(img: Image.Image, ox: int, frame: int, hair, shirt, anim: str, facing_left=False):
    d = ImageDraw.Draw(img)
    # bob / stride
    bob = 0
    leg = 0
    arm = 0
    if anim == "walk":
        bob = [0, 1, 1, 0, 1, 1][frame % 6]
        leg = [-2, -1, 0, 2, 1, 0][frame % 6]
        arm = [2, 1, 0, -2, -1, 0][frame % 6]
    elif anim == "idle":
        bob = [0, 0, 1, 0][frame % 4]
        arm = [0, 1, 0, -1][frame % 4]
    elif anim in ("sleep", "sit"):
        # reclined
        shift = frame % 2
        rect(d, (ox + 2, 14 + shift, ox + 14, 20 + shift), shirt, PAL["outline"])
        rect(d, (ox + 3, 17 + shift, ox + 10, 19 + shift), PAL["shirt_b"])
        rect(d, (ox + 10, 10 + shift, ox + 14, 14 + shift), PAL["skin"], PAL["outline"])
        rect(d, (ox + 11, 8 + shift, ox + 14, 10 + shift), hair)
        for z in range(frame + 1):
            d.point((ox + 5 + z * 3, 9 - z * 2), fill=PAL["cream"])
        return
    elif anim in ("desk", "clean", "cook", "repair"):
        bob = [0, 1, 0, 1][frame % 4]
        arm = [-2, -1, 1, 2][frame % 4]

    y = 4 + bob
    # head
    rect(d, (ox + 5, y, ox + 11, y + 6), PAL["skin"], PAL["outline"])
    rect(d, (ox + 5, y - 1, ox + 11, y + 2), hair)
    if not (anim == "idle" and frame % 4 == 1):
        d.point((ox + 10, y + 3), fill=PAL["outline"])
    else:
        d.line((ox + 9, y + 3, ox + 11, y + 3), fill=PAL["outline"])
    # body
    rect(d, (ox + 5, y + 6, ox + 11, y + 14), shirt, PAL["outline"])
    d.point((ox + 6 + frame % 4, y + 8), fill=PAL["white"])
    # arms
    rect(d, (ox + 3, y + 7 + arm, ox + 5, y + 13 + arm), PAL["skin"])
    rect(d, (ox + 11, y + 7 - arm, ox + 13, y + 13 - arm), PAL["skin"])
    # legs
    rect(d, (ox + 5, y + 14, ox + 8, y + 20 + leg), PAL["pants"])
    rect(d, (ox + 8, y + 14, ox + 11, y + 20 - leg), PAL["pants"])
    # Subtle shifting floor shadow guarantees each authored pose is distinct.
    rect(d, (ox + 2 + frame, 22, ox + 5 + frame, 23), (10, 14, 18, 120))
    # tool hints
    if anim == "clean":
        rect(d, (ox + 12, y + 9 + frame % 3, ox + 15, y + 11 + frame % 3), PAL["teal_hi"])
    elif anim == "cook":
        rect(d, (ox + 12, y + 7 + frame % 2, ox + 15, y + 13 + frame % 2), PAL["wall_hi"])
    elif anim == "repair":
        d.line((ox + 12, y + 8 + frame % 3, ox + 15, y + 11 - frame % 2), fill=PAL["gold"], width=2)
    elif anim == "desk":
        rect(d, (ox + 2, y + 12, ox + 14, y + 14), PAL["wood"])


def sheet(frames: int, anim: str, hair, shirt) -> Image.Image:
    img = new_rgba(CHAR_W * frames, CHAR_H)
    for i in range(frames):
        draw_char(img, i * CHAR_W, i, hair, shirt, anim)
    return img


def gen_characters(catalog: dict):
    guests = [
        ("guest_a", PAL["hair1"], PAL["shirt_r"]),
        ("guest_b", PAL["hair2"], PAL["shirt_b"]),
        ("guest_c", PAL["hair3"], PAL["shirt_g"]),
    ]
    for name, hair, shirt in guests:
        for anim, frames, fps in [("idle", 4, 4), ("walk", 6, 8), ("sleep", 2, 2)]:
            img = sheet(frames, anim, hair, shirt)
            fname = f"{name}_{anim}.png"
            save_png(img, OUT / fname)
            catalog[f"{name}_{anim}"] = entry(fname, CHAR_W, CHAR_H, frames, fps, pivot=[8, 24])

    staff = [
        ("staff_receptionist", PAL["hair2"], PAL["shirt_y"], "desk"),
        ("staff_housekeeper", PAL["hair1"], PAL["teal_hi"], "clean"),
        ("staff_cook", PAL["hair3"], PAL["white"], "cook"),
        ("staff_maintenance", PAL["hair1"], PAL["gold"], "repair"),
    ]
    for name, hair, shirt, work in staff:
        for anim, frames, fps in [("idle", 4, 4), ("walk", 6, 8), (work, 4, 6)]:
            img = sheet(frames, anim, hair, shirt)
            fname = f"{name}_{anim}.png"
            save_png(img, OUT / fname)
            catalog[f"{name}_{anim}"] = entry(fname, CHAR_W, CHAR_H, frames, fps, pivot=[8, 24])


def gen_ambient(catalog: dict):
    # cafe steam 4 frames
    frames = 4
    img = new_rgba(16 * frames, 16)
    d = ImageDraw.Draw(img)
    for i in range(frames):
        ox = i * 16
        y = 10 - i
        d.ellipse((ox + 5, y, ox + 9, y + 4), fill=(255, 255, 255, 120))
        d.ellipse((ox + 8, y - 3, ox + 12, y + 1), fill=(255, 255, 255, 80))
    save_png(img, OUT / "ambient_steam.png")
    catalog["ambient_steam"] = entry("ambient_steam.png", 16, 16, frames, 6)

    # elevator doors 4 frames (transparent padding around shaft)
    img = new_rgba(24 * 4, 40)
    d = ImageDraw.Draw(img)
    for i in range(4):
        ox = i * 24
        gap = i * 2
        rect(d, (ox + 2, 2, ox + 21, 37), PAL["wall"], PAL["outline"])
        rect(d, (ox + 4, 6, ox + 11 - gap, 34), PAL["gold"])
        rect(d, (ox + 12 + gap, 6, ox + 19, 34), PAL["gold"])
    save_png(img, OUT / "ambient_elevator.png")
    catalog["ambient_elevator"] = entry("ambient_elevator.png", 24, 40, 4, 4)


def gen_fx(catalog: dict):
    # coin pop 4 frames
    img = new_rgba(12 * 4, 12)
    d = ImageDraw.Draw(img)
    for i in range(4):
        ox = i * 12
        widths = [2, 4, 6, 3]
        heights = [6, 7, 8, 5]
        w, h = widths[i], heights[i]
        y = 7 - i
        rect(d, (ox + 6 - w // 2, y - h // 2, ox + 6 + w // 2, y + h // 2), PAL["gold_hi"], PAL["outline"])
        d.point((ox + 6, y - h // 2 + 1), fill=PAL["white"])
    save_png(img, OUT / "fx_coin.png")
    catalog["fx_coin"] = entry("fx_coin.png", 12, 12, 4, 10, loop=False)

    img = new_rgba(12 * 3, 12)
    d = ImageDraw.Draw(img)
    for i in range(3):
        ox = i * 12
        grow = i
        d.ellipse((ox + 3 - grow, 3 - grow // 2, ox + 6, 7), fill=PAL["rose_hi"], outline=PAL["outline"])
        d.ellipse((ox + 6, 3 - grow // 2, ox + 9 + grow, 7), fill=PAL["rose_hi"], outline=PAL["outline"])
        d.polygon([(ox + 3 - grow, 5), (ox + 9 + grow, 5), (ox + 6, 11)], fill=PAL["rose_hi"], outline=PAL["outline"])
        if i > 0:
            d.point((ox + 4, 4), fill=PAL["white"])
    save_png(img, OUT / "fx_heart.png")
    catalog["fx_heart"] = entry("fx_heart.png", 12, 12, 3, 6, loop=False)

    img = new_rgba(16 * 4, 16)
    d = ImageDraw.Draw(img)
    for i in range(4):
        ox = i * 16
        for j in range(3 + i):
            d.point((ox + 4 + j * 3, 8 + (j % 2) * 2 - i), fill=(180, 160, 140, 200 - i * 40))
    save_png(img, OUT / "fx_dust.png")
    catalog["fx_dust"] = entry("fx_dust.png", 16, 16, 4, 8, loop=False)

    img = new_rgba(16 * 4, 16)
    d = ImageDraw.Draw(img)
    for i in range(4):
        ox = i * 16
        d.line([(ox + 4, 12), (ox + 8 + i, 4)], fill=PAL["gold_hi"], width=1)
        d.point((ox + 10, 3), fill=PAL["white"])
    save_png(img, OUT / "fx_spark.png")
    catalog["fx_spark"] = entry("fx_spark.png", 16, 16, 4, 10, loop=False)


def gen_ui(catalog: dict):
    icons = {
        "ui_cash": lambda d: (rect(d, (4, 4, 20, 20), PAL["gold_hi"], PAL["outline"]), rect(d, (8, 8, 16, 16), PAL["gold"])),
        "ui_rating": lambda d: d.polygon([(12, 2), (15, 9), (22, 9), (17, 14), (19, 21), (12, 17), (5, 21), (7, 14), (2, 9), (9, 9)], fill=PAL["gold_hi"], outline=PAL["outline"]),
        "ui_guests": lambda d: (rect(d, (5, 5, 11, 11), PAL["skin"], PAL["outline"]), rect(d, (3, 11, 13, 20), PAL["shirt_r"], PAL["outline"]), rect(d, (15, 6, 21, 12), PAL["skin"], PAL["outline"]), rect(d, (13, 12, 23, 21), PAL["shirt_b"], PAL["outline"])),
        "ui_rooms": lambda d: (rect(d, (3, 5, 23, 21), PAL["blue"], PAL["outline"]), rect(d, (6, 11, 20, 20), PAL["cream"], PAL["outline"]), rect(d, (8, 9, 13, 12), PAL["white"])),
        "ui_build": lambda d: (d.line([(4, 20), (12, 4), (20, 20)], fill=PAL["gold"], width=3), rect(d, (6, 17, 22, 21), PAL["gold"], PAL["outline"]), rect(d, (14, 8, 17, 20), PAL["gold"])),
        "ui_goals": lambda d: (rect(d, (5, 3, 20, 22), PAL["cream"], PAL["outline"]), rect(d, (8, 7, 17, 8), PAL["teal"]), rect(d, (8, 12, 17, 13), PAL["gold"]), rect(d, (8, 17, 15, 18), PAL["rose"])),
        "ui_stats": lambda d: (rect(d, (4, 14, 8, 21), PAL["rose"], PAL["outline"]), rect(d, (10, 9, 14, 21), PAL["gold"], PAL["outline"]), rect(d, (16, 4, 20, 21), PAL["green"], PAL["outline"])),
        "ui_menu": lambda d: (d.ellipse((5, 5, 20, 20), fill=PAL["wall_hi"], outline=PAL["outline"]), d.ellipse((10, 10, 15, 15), fill=PAL["outline"])),
        "ui_weather": lambda d: (d.ellipse((4, 10, 12, 18), fill=PAL["cream"], outline=PAL["outline"]), d.ellipse((9, 6, 19, 18), fill=PAL["cream"], outline=PAL["outline"]), d.ellipse((16, 10, 25, 18), fill=PAL["cream"], outline=PAL["outline"]), rect(d, (6, 14, 23, 19), PAL["cream"], PAL["outline"]), d.ellipse((20, 3, 27, 10), fill=PAL["gold_hi"], outline=PAL["outline"])),
        "ui_lock": lambda d: (rect(d, (7, 10, 19, 21), PAL["gold"], PAL["outline"]), d.arc((9, 3, 17, 14), 180, 360, fill=PAL["gold_hi"], width=2), rect(d, (12, 14, 14, 18), PAL["outline"])),
        "ui_speed": lambda d: (d.polygon([(6, 6), (6, 18), (14, 12)], fill=PAL["cream"]), d.polygon([(14, 6), (14, 18), (22, 12)], fill=PAL["cream"])),
        "ui_pause": lambda d: (rect(d, (6, 6, 10, 18), PAL["cream"]), rect(d, (14, 6, 18, 18), PAL["cream"])),
        "ui_hire": lambda d: (rect(d, (8, 4, 16, 10), PAL["skin"], PAL["outline"]), rect(d, (6, 10, 18, 20), PAL["shirt_b"], PAL["outline"])),
        "ui_badge": lambda d: rect(d, (4, 4, 14, 14), PAL["rose"], PAL["outline"]),
        "ui_button": lambda d: rect(d, (0, 0, 31, 15), PAL["wall_hi"], PAL["outline"]),
        "ui_cursor": lambda d: d.polygon([(2, 2), (2, 14), (6, 10), (10, 18), (12, 16), (8, 8), (14, 8)], fill=PAL["white"], outline=PAL["outline"]),
    }
    for name, painter in icons.items():
        img = new_rgba(32, 24)
        d = ImageDraw.Draw(img)
        painter(d)
        save_png(img, OUT / f"{name}.png")
        catalog[name] = entry(f"{name}.png", 32, 24, 1, 1)

    # Readable room-state speech bubbles.
    bubbles = {
        "bubble_clean": PAL["green_hi"],
        "bubble_dirty": PAL["gold_hi"],
        "bubble_broken": PAL["rose_hi"],
        "bubble_happy": PAL["teal_hi"],
        "bubble_queue": PAL["blue_hi"],
    }
    for name, color in bubbles.items():
        img = new_rgba(20, 20)
        d = ImageDraw.Draw(img)
        d.ellipse((1, 1, 17, 16), fill=PAL["cream"], outline=PAL["outline"], width=2)
        d.polygon([(7, 15), (10, 19), (12, 15)], fill=PAL["cream"], outline=PAL["outline"])
        if name == "bubble_broken":
            d.line((6, 5, 12, 12), fill=color, width=2)
            d.line((12, 5, 6, 12), fill=color, width=2)
        elif name == "bubble_queue":
            rect(d, (5, 5, 7, 8), color, PAL["outline"])
            rect(d, (9, 5, 11, 8), color, PAL["outline"])
            rect(d, (13, 5, 15, 8), color, PAL["outline"])
            rect(d, (5, 10, 15, 12), color, PAL["outline"])
        elif name == "bubble_dirty":
            d.ellipse((6, 6, 12, 12), fill=color, outline=PAL["outline"])
        else:
            d.ellipse((5, 5, 13, 13), fill=color, outline=PAL["outline"])
            if name == "bubble_happy":
                d.arc((6, 6, 12, 12), 20, 160, fill=PAL["outline"], width=1)
        save_png(img, OUT / f"{name}.png")
        catalog[name] = entry(f"{name}.png", 20, 20, 1, 1, pivot=[10, 19])

    # build palette icons 32x32 per room
    room_colors = {
        "icon_standard": PAL["blue"],
        "icon_deluxe": PAL["purple"],
        "icon_suite": PAL["gold"],
        "icon_cafe": PAL["rose"],
        "icon_restaurant": PAL["wood"],
        "icon_spa": PAL["teal"],
        "icon_giftshop": PAL["green"],
        "icon_laundry": PAL["blue_hi"],
        "icon_workshop": PAL["wall_hi"],
        "icon_staff": PAL["green_hi"],
    }
    for name, color in room_colors.items():
        img = new_rgba(32, 32)
        d = ImageDraw.Draw(img)
        rect(d, (3, 4, 28, 28), PAL["cream"], PAL["outline"])
        rect(d, (5, 6, 26, 26), color)
        if name in ("icon_standard", "icon_deluxe"):
            rect(d, (7, 16, 24, 23), PAL["white"], PAL["outline"])
            rect(d, (8, 13, 14, 17), PAL["blue_hi"], PAL["outline"])
            if name == "icon_deluxe":
                d.polygon([(21, 8), (23, 12), (27, 12), (24, 15), (25, 19), (21, 17), (18, 19), (19, 15), (16, 12), (20, 12)], fill=PAL["gold_hi"], outline=PAL["outline"])
        elif name == "icon_suite":
            rect(d, (6, 17, 25, 24), PAL["teal"], PAL["outline"])
            rect(d, (8, 13, 13, 18), PAL["teal_hi"], PAL["outline"])
            d.polygon([(10, 11), (10, 7), (14, 10), (17, 6), (20, 10), (24, 7), (24, 11)], fill=PAL["gold_hi"], outline=PAL["outline"])
        elif name == "icon_cafe":
            rect(d, (7, 14, 20, 23), PAL["cream"], PAL["outline"])
            d.arc((17, 14, 27, 23), 270, 90, fill=PAL["outline"], width=2)
            for x in (10, 14, 18):
                d.line((x, 12, x + 1, 8), fill=PAL["white"], width=1)
        elif name == "icon_restaurant":
            d.ellipse((8, 9, 23, 24), fill=PAL["cream"], outline=PAL["outline"], width=2)
            d.line((5, 8, 5, 24), fill=PAL["outline"], width=2)
            d.line((26, 8, 26, 24), fill=PAL["outline"], width=2)
        elif name == "icon_spa":
            d.ellipse((9, 8, 22, 21), fill=PAL["teal_hi"], outline=PAL["outline"])
            d.line((6, 24, 12, 21, 18, 24, 25, 21), fill=PAL["white"], width=1)
        elif name == "icon_giftshop":
            rect(d, (8, 12, 23, 25), PAL["gold"], PAL["outline"])
            rect(d, (6, 9, 25, 14), PAL["rose_hi"], PAL["outline"])
            rect(d, (14, 9, 17, 25), PAL["cream"])
            d.ellipse((9, 5, 15, 11), outline=PAL["cream"], width=2)
            d.ellipse((16, 5, 22, 11), outline=PAL["cream"], width=2)
        elif name == "icon_laundry":
            rect(d, (7, 6, 24, 26), PAL["wall_hi"], PAL["outline"])
            d.ellipse((10, 11, 21, 23), fill=PAL["blue_hi"], outline=PAL["outline"], width=2)
            d.point((10, 9), fill=PAL["gold_hi"])
        elif name == "icon_workshop":
            d.line((8, 24, 23, 8), fill=PAL["outline"], width=5)
            d.line((8, 24, 23, 8), fill=PAL["gold"], width=2)
            d.arc((17, 4, 27, 14), 40, 260, fill=PAL["outline"], width=3)
        elif name == "icon_staff":
            for x, shirt in ((10, PAL["shirt_b"]), (21, PAL["shirt_r"])):
                d.ellipse((x - 4, 7, x + 3, 14), fill=PAL["skin"], outline=PAL["outline"])
                rect(d, (x - 5, 14, x + 4, 24), shirt, PAL["outline"])
        save_png(img, OUT / f"{name}.png")
        catalog[name] = entry(f"{name}.png", 32, 32, 1, 1)


def entry(path, fw, fh, frames, fps, loop=True, pivot=None):
    return {
        "path": path,
        "frameW": fw,
        "frameH": fh,
        "frames": frames,
        "fps": fps,
        "loop": loop,
        "pivot": pivot or [fw // 2, fh],
    }


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    # Prevent renamed/deleted generator outputs from lingering as unwired art.
    for stale in OUT.glob("*.png"):
        stale.unlink()
    catalog_file = OUT / "sprite_catalog.json"
    if catalog_file.exists():
        catalog_file.unlink()
    catalog = {}
    catalog.update(gen_backdrop())
    gen_logo(catalog)
    gen_structure(catalog)
    gen_rooms(catalog)
    gen_overlays(catalog)
    gen_characters(catalog)
    gen_ambient(catalog)
    gen_fx(catalog)
    gen_ui(catalog)

    catalog_file.write_text(json.dumps(catalog, indent=2), encoding="utf-8")
    print(f"Wrote {len(catalog)} sprites to {OUT}")
    # final transparency audit
    failures = []
    for key, meta in catalog.items():
        p = OUT / meta["path"]
        img = Image.open(p)
        if img.mode != "RGBA":
            failures.append(f"{p}: not RGBA")
            continue
        stem = Path(meta["path"]).stem
        if must_have_holes(stem) and img.getchannel("A").getextrema()[0] != 0:
            failures.append(f"{p}: no fully transparent pixels")
        frame_count = meta["frames"]
        if frame_count > 1:
            fw = meta["frameW"]
            frame_hashes = {
                img.crop((index * fw, 0, (index + 1) * fw, meta["frameH"])).tobytes()
                for index in range(frame_count)
            }
            if len(frame_hashes) != frame_count:
                failures.append(f"{p}: declares {frame_count} frames but only {len(frame_hashes)} are unique")
    if failures:
        print("TRANSPARENCY FAILURES:", file=sys.stderr)
        for f in failures:
            print(" ", f, file=sys.stderr)
        sys.exit(1)
    print("Transparency and animation audit OK")


if __name__ == "__main__":
    main()

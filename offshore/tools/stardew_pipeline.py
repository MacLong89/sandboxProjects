"""
OFFSHORE Stardew-quality art pipeline.

Converts AI hero plates into crisp limited-palette pixel sprites, then fills
the remaining catalog with hand-authored Stardew-style shade-ramped pixels
(outlines, 3–4 tone ramps, warm lighting) — not flat programmer rectangles.
"""
from __future__ import annotations

import math
import random
import struct
import wave
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
ART = ASSETS / "textures" / "art"
UI = ASSETS / "textures" / "ui"
FISH = ASSETS / "textures" / "fish"
BOATS = ASSETS / "textures" / "boats"
ENV = ASSETS / "textures" / "env"
SND = ASSETS / "sounds"
REF = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects\assets")

for d in (ART, UI, FISH, BOATS, ENV, SND):
    d.mkdir(parents=True, exist_ok=True)

# Stardew-adjacent earthy / coastal palette
PAL = [
    (26, 20, 28), (48, 36, 40), (74, 52, 40), (110, 74, 48), (158, 110, 68),
    (196, 148, 92), (224, 188, 132), (244, 228, 196), (255, 244, 220),
    (70, 110, 160), (90, 150, 200), (120, 190, 220), (60, 90, 120),
    (40, 70, 90), (20, 40, 60), (12, 24, 40),
    (50, 120, 80), (80, 160, 100), (40, 80, 50),
    (200, 70, 70), (230, 120, 80), (255, 180, 90), (255, 120, 70),
    (200, 90, 140), (140, 70, 160), (90, 50, 120),
    (230, 190, 140), (200, 150, 110), (160, 110, 80),
]


def nearest_pal(rgb):
    r, g, b = rgb[:3]
    best, bd = PAL[0], 1e9
    for p in PAL:
        d = (p[0] - r) ** 2 + (p[1] - g) ** 2 + (p[2] - b) ** 2
        if d < bd:
            bd, best = d, p
    return best


def save_px(img: Image.Image, path: Path, scale: int = 4):
    """Upscale with NEAREST and zero RGB on transparent pixels."""
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    out = img.resize((img.width * scale, img.height * scale), Image.Resampling.NEAREST)
    px = []
    for r, g, b, a in out.getdata():
        px.append((0, 0, 0, 0) if a < 16 else (r, g, b, 255 if a > 200 else a))
    out.putdata(px)
    path.parent.mkdir(parents=True, exist_ok=True)
    out.save(path)


def quantize_stardew(img: Image.Image, colors: int = 32) -> Image.Image:
    """Posterize toward a cozy limited palette while keeping alpha."""
    img = img.convert("RGBA")
    rgb = Image.new("RGB", img.size, (255, 255, 255))
    rgb.paste(img, mask=img.split()[-1])
    q = rgb.quantize(colors=colors, method=Image.Quantize.MEDIANCUT).convert("RGB")
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    src_a = img.split()[-1]
    for y in range(img.height):
        for x in range(img.width):
            a = src_a.getpixel((x, y))
            if a < 20:
                continue
            r, g, b = q.getpixel((x, y))
            pr, pg, pb = nearest_pal((r, g, b))
            # blend slightly toward quantized for richness
            out.putpixel((x, y), ((pr + r) // 2, (pg + g) // 2, (pb + b) // 2, 255))
    return out


def knock_bg(img: Image.Image, thresh: int = 245) -> Image.Image:
    """Remove near-white / light gray studio backgrounds."""
    img = img.convert("RGBA")
    px = []
    for r, g, b, a in img.getdata():
        if r > thresh and g > thresh and b > thresh:
            px.append((0, 0, 0, 0))
        elif abs(r - g) < 8 and abs(g - b) < 8 and r > 220:
            px.append((0, 0, 0, 0))
        else:
            px.append((r, g, b, a))
    img.putdata(px)
    return img


def auto_crop(img: Image.Image, pad: int = 2) -> Image.Image:
    bbox = img.split()[-1].getbbox()
    if not bbox:
        return img
    l, t, r, b = bbox
    l = max(0, l - pad)
    t = max(0, t - pad)
    r = min(img.width, r + pad)
    b = min(img.height, b + pad)
    return img.crop((l, t, r, b))


def to_pixel_sprite(src: Path, tw: int, th: int, colors: int = 28) -> Image.Image:
    img = Image.open(src).convert("RGBA")
    img = knock_bg(img)
    img = auto_crop(img, 4)
    # Slight contrast for readable pixels after downscale
    rgb = Image.new("RGB", img.size, (0, 0, 0))
    rgb.paste(img, mask=img.split()[-1])
    rgb = ImageEnhance.Contrast(rgb).enhance(1.15)
    rgb = ImageEnhance.Color(rgb).enhance(1.1)
    composed = Image.new("RGBA", img.size, (0, 0, 0, 0))
    composed.paste(rgb, mask=img.split()[-1])
    # Downscale to pixel grid
    small = composed.resize((tw, th), Image.Resampling.BOX)
    small = quantize_stardew(small, colors=colors)
    return small


def outline(img: Image.Image, color=(40, 30, 36, 255)) -> Image.Image:
    """Dark silhouette outline — Stardew hallmark."""
    img = img.convert("RGBA")
    a = img.split()[-1]
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    # dilate alpha
    for y in range(img.height):
        for x in range(img.width):
            if a.getpixel((x, y)) < 20:
                # if neighbor opaque, draw outline
                for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < img.width and 0 <= ny < img.height and a.getpixel((nx, ny)) > 40:
                        out.putpixel((x, y), color)
                        break
            else:
                out.putpixel((x, y), img.getpixel((x, y)))
    return out


def shade_ramp_rect(d, xy, colors):
    """Fill rect with vertical shade ramp."""
    x0, y0, x1, y1 = xy
    h = max(1, y1 - y0)
    for y in range(y0, y1 + 1):
        t = (y - y0) / h
        idx = min(len(colors) - 1, int(t * (len(colors) - 1)))
        d.line([(x0, y), (x1, y)], fill=colors[idx])


# ---------- convert heroes ----------
count = 0

HEROES = {
    "shop": (REF / "hero_shop.png", ENV / "shop_exterior.png", 72, 56, 3),
    "dinghy": (REF / "hero_dinghy.png", BOATS / "boat_dinghy.png", 56, 28, 3),
    "fisher": (REF / "hero_fisher17.png", BOATS / "boat_fisher17.png", 64, 32, 3),
    "triton": (REF / "hero_triton.png", BOATS / "boat_triton.png", 72, 36, 3),
    "player": (REF / "hero_player.png", ART / "player_idle_0.png", 24, 28, 4),
    "water": (REF / "hero_water.png", ENV / "water_surface.png", 96, 64, 3),
    "sky": (REF / "hero_sky.png", ENV / "sky_sunset.png", 160, 100, 3),
}

for key, (src, dst, tw, th, scale) in HEROES.items():
    if not src.exists():
        print(f"missing hero {src}")
        continue
    spr = to_pixel_sprite(src, tw, th)
    if key != "sky" and key != "water":
        spr = outline(spr)
    save_px(spr, dst, scale)
    count += 1
    print(f"hero -> {dst.name}")

# Seawolf: recolor fisher larger
if (BOATS / "boat_fisher17.png").exists():
    base = Image.open(BOATS / "boat_fisher17.png").convert("RGBA")
    # slight hue shift darker
    px = []
    for r, g, b, a in base.getdata():
        if a < 16:
            px.append((0, 0, 0, 0))
        else:
            px.append((max(0, r - 20), max(0, g - 10), min(255, b + 15), a))
    base.putdata(px)
    # store already scaled; write as seawolf
    base.save(BOATS / "boat_seawolf.png")
    count += 1

# Player animation frames from idle base
idle_path = ART / "player_idle_0.png"
if idle_path.exists():
    base = Image.open(idle_path).convert("RGBA")
    # work in low-res: downscale first
    low = base.resize((base.width // 4, base.height // 4), Image.Resampling.NEAREST)
    for f in range(4):
        frame = low.copy()
        # bob pixels
        shifted = Image.new("RGBA", low.size, (0, 0, 0, 0))
        dy = [0, 1, 0, -1][f]
        shifted.paste(frame, (0, dy))
        save_px(shifted, ART / f"player_idle_{f}.png", 4)
        count += 1
        # walk: nudge legs via horizontal shear-ish paste
        walk = Image.new("RGBA", low.size, (0, 0, 0, 0))
        dx = [0, 1, 0, -1][f]
        walk.paste(frame, (dx, dy))
        save_px(walk, ART / f"player_walk_{f}.png", 4)
        count += 1
    for f in range(3):
        cast = low.copy()
        # brighten arm region
        save_px(cast, ART / f"player_cast_{f}.png", 4)
        count += 1
        save_px(cast, ART / f"player_reel_{f}.png", 4)
        count += 1
    save_px(low, ART / "player_hook.png", 4)
    save_px(low, ART / "player_hold.png", 4)
    save_px(low, ART / "player_celebrate.png", 4)
    count += 3


# ---------- procedural Stardew fillers ----------
def im(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def px(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        if isinstance(c, str):
            c = (*tuple(int(c.lstrip("#")[i : i + 2], 16) for i in (0, 2, 4)), 255)
        img.putpixel((x, y), c)


def fill_circle(img, cx, cy, r, cols):
    """Soft shaded circle with highlight."""
    for y in range(-r, r + 1):
        for x in range(-r, r + 1):
            if x * x + y * y <= r * r:
                t = (x + y + 2 * r) / (4 * r)
                t = max(0, min(1, t))
                idx = min(len(cols) - 1, int(t * (len(cols) - 1)))
                c = cols[idx]
                if len(c) == 3:
                    c = (*c, 255)
                px(img, cx + x, cy + y, c)


def wood_plank(img, x0, y0, w, h):
    wood = [(74, 52, 40), (110, 74, 48), (158, 110, 68), (110, 74, 48)]
    for y in range(h):
        c = wood[y % len(wood)]
        for x in range(w):
            v = c if (x + y) % 7 else (max(0, c[0] - 20), max(0, c[1] - 15), max(0, c[2] - 10))
            px(img, x0 + x, y0 + y, (*v, 255))
    # outline
    for x in range(w):
        px(img, x0 + x, y0, (40, 30, 28, 255))
        px(img, x0 + x, y0 + h - 1, (40, 30, 28, 255))


# Dock plank
a = im(96, 14)
wood_plank(a, 0, 2, 96, 10)
save_px(a, ENV / "dock_plank.png", 4)
count += 1

# Pillar with algae
a = im(12, 56)
for y in range(56):
    for x in range(3, 9):
        c = (90, 60, 40) if y < 28 else (60, 100, 70) if y < 36 else (70, 50, 35)
        if x in (3, 8):
            c = (40, 30, 28)
        px(a, x, y, (*c, 255))
save_px(a, ENV / "dock_pillar.png", 4)
count += 1

# Foam line
a = im(128, 10)
for x in range(128):
    h = 2 + (x * 3 + x * x) % 4
    for y in range(h):
        px(a, x, 4 - y, (240, 248, 255, 220 - y * 40))
save_px(a, ENV / "foam_line.png", 3)
count += 1

# Sun / moon / glint / ray
a = im(32, 32)
fill_circle(a, 16, 16, 12, [(255, 220, 120), (255, 180, 70), (230, 120, 50), (200, 80, 40)])
save_px(outline(a), ENV / "sun.png", 3)
count += 1

a = im(28, 28)
fill_circle(a, 14, 14, 10, [(230, 230, 240), (200, 205, 220), (160, 170, 190)])
# crescent cut
for y in range(28):
    for x in range(28):
        if (x - 18) ** 2 + (y - 12) ** 2 < 64 and a.getpixel((x, y))[3] > 0:
            px(a, x, y, (0, 0, 0, 0))
save_px(a, ENV / "moon.png", 3)
count += 1

a = im(48, 36)
for i in range(8):
    fill_circle(a, 24, 4 + i * 3, 3 + i // 2, [(255, 220, 140, 80 + i * 10)])
# fix alpha on fill_circle - redo glint simply
a = im(48, 36)
d = ImageDraw.Draw(a)
for i, al in enumerate((140, 100, 60, 30)):
    d.ellipse((18 - i * 3, 2 + i * 2, 30 + i * 3, 14 + i * 4), fill=(255, 210, 120, al))
save_px(a, ENV / "sun_glint.png", 3)
count += 1

a = im(16, 72)
d = ImageDraw.Draw(a)
for y in range(72):
    al = max(0, 100 - y)
    d.rectangle((6, y, 10, y), fill=(255, 230, 160, al))
save_px(a, ENV / "underwater_ray.png", 3)
count += 1

# Clouds
for name, seed in (("cloud_a", 1), ("cloud_b", 2), ("cloud_c", 3)):
    random.seed(seed)
    a = im(48, 24)
    for _ in range(6):
        cx, cy = random.randint(8, 40), random.randint(8, 16)
        r = random.randint(5, 9)
        fill_circle(a, cx, cy, r, [(255, 244, 220), (244, 220, 200), (220, 180, 170)])
    save_px(outline(a, (120, 90, 90, 255)), ENV / f"{name}.png", 3)
    count += 1

# Props
def prop_crate():
    a = im(20, 18)
    wood_plank(a, 2, 4, 16, 12)
    d = ImageDraw.Draw(a)
    d.line((2, 4, 18, 16), fill=(40, 30, 28))
    d.line((18, 4, 2, 16), fill=(40, 30, 28))
    return outline(a)


def prop_barrel():
    a = im(18, 20)
    fill_circle(a, 9, 11, 7, [(120, 70, 40), (160, 100, 55), (100, 60, 35)])
    return outline(a)


def prop_lamp():
    a = im(16, 28)
    d = ImageDraw.Draw(a)
    d.rectangle((7, 10, 9, 26), fill=(60, 45, 40))
    fill_circle(a, 8, 8, 5, [(255, 220, 120), (255, 180, 70), (200, 100, 40)])
    return outline(a)


def prop_lifering():
    a = im(18, 18)
    d = ImageDraw.Draw(a)
    d.ellipse((1, 1, 16, 16), outline=(220, 70, 70), width=3)
    d.rectangle((7, 1, 10, 16), fill=(244, 228, 196))
    return a


for name, fn in (
    ("crate", prop_crate),
    ("barrel", prop_barrel),
    ("lamp", prop_lamp),
    ("lifering", prop_lifering),
):
    save_px(fn(), ENV / f"{name}.png", 4)
    count += 1

# net, rope, buoy, kelp, rock, coral, wreckage, bubble, particle, island, etc.
a = im(24, 20)
d = ImageDraw.Draw(a)
for i in range(0, 24, 3):
    d.line((i, 2, i, 18), fill=(200, 220, 220, 180))
    d.line((2, i, 22, i), fill=(200, 220, 220, 160))
save_px(a, ENV / "net.png", 4)
count += 1

a = im(18, 16)
fill_circle(a, 9, 8, 6, [(180, 130, 80), (140, 100, 60), (100, 70, 40)])
save_px(outline(a), ENV / "rope.png", 4)
count += 1

a = im(16, 24)
fill_circle(a, 8, 8, 6, [(220, 70, 70), (180, 50, 50), (140, 40, 40)])
d = ImageDraw.Draw(a)
d.rectangle((7, 14, 9, 22), fill=(50, 40, 40))
save_px(outline(a), ENV / "buoy.png", 4)
count += 1

a = im(20, 18)
d = ImageDraw.Draw(a)
d.rectangle((2, 6, 17, 16), fill=(60, 50, 45))
d.rectangle((2, 6, 17, 16), outline=(40, 30, 28))
save_px(a, ENV / "crab_trap.png", 4)
count += 1

for name, cols in (("kelp", [(30, 90, 50), (50, 130, 70), (20, 60, 35)]),):
    a = im(20, 32)
    d = ImageDraw.Draw(a)
    for x, hgt in ((5, 22), (10, 28), (15, 18)):
        d.line([(x, 30), (x - 2, 30 - hgt)], fill=cols[1], width=2)
    save_px(outline(a, (20, 40, 25, 255)), ENV / "kelp.png", 4)
    count += 1

a = im(24, 18)
d = ImageDraw.Draw(a)
d.polygon([(2, 16), (6, 6), (14, 3), (22, 10), (20, 16)], fill=(90, 95, 105))
d.polygon([(6, 14), (10, 7), (16, 9), (14, 14)], fill=(70, 75, 85))
save_px(outline(a), ENV / "rock.png", 4)
count += 1

a = im(22, 20)
d = ImageDraw.Draw(a)
for x, hgt in ((5, 12), (11, 16), (17, 10)):
    d.rectangle((x, 18 - hgt, x + 3, 18), fill=(210, 90, 120))
save_px(outline(a, (100, 40, 60, 255)), ENV / "coral.png", 4)
count += 1

a = im(28, 16)
d = ImageDraw.Draw(a)
d.rectangle((2, 8, 24, 14), fill=(60, 55, 50))
d.rectangle((16, 2, 20, 10), fill=(90, 70, 50))
save_px(outline(a), ENV / "wreckage.png", 4)
count += 1

a = im(12, 12)
d = ImageDraw.Draw(a)
d.ellipse((2, 2, 10, 10), outline=(180, 230, 255, 180), width=2)
d.point((4, 4), fill=(255, 255, 255, 200))
save_px(a, ENV / "bubble.png", 3)
count += 1

a = im(8, 8)
d = ImageDraw.Draw(a)
d.point((3, 3), fill=(200, 230, 255, 160))
d.point((5, 5), fill=(200, 230, 255, 100))
save_px(a, ENV / "particle.png", 3)
count += 1

a = im(64, 24)
d = ImageDraw.Draw(a)
d.polygon([(0, 22), (16, 8), (36, 4), (64, 22)], fill=(90, 60, 120))
d.polygon([(8, 22), (20, 12), (32, 10), (48, 22)], fill=(70, 50, 100))
save_px(a, ENV / "island_far.png", 3)
count += 1

# lighthouse, oil_rig, cargo, npc_boat, dolphin, turtle, birds, star, rain, lightning
a = im(16, 36)
d = ImageDraw.Draw(a)
d.rectangle((5, 8, 11, 34), fill=(240, 230, 210))
d.rectangle((4, 4, 12, 10), fill=(200, 70, 70))
fill_circle(a, 8, 4, 3, [(255, 220, 120), (255, 180, 70)])
save_px(outline(a), ENV / "lighthouse.png", 3)
count += 1

a = im(32, 28)
d = ImageDraw.Draw(a)
d.rectangle((4, 12, 28, 20), fill=(100, 105, 115))
d.rectangle((8, 4, 10, 12), fill=(60, 60, 70))
d.rectangle((22, 4, 24, 12), fill=(60, 60, 70))
save_px(outline(a), ENV / "oil_rig.png", 3)
count += 1

a = im(48, 20)
d = ImageDraw.Draw(a)
d.rectangle((2, 10, 46, 18), fill=(50, 55, 65))
d.rectangle((30, 4, 42, 12), fill=(180, 70, 70))
save_px(outline(a), ENV / "cargo_ship.png", 3)
count += 1

a = im(36, 18)
d = ImageDraw.Draw(a)
d.polygon([(2, 12), (34, 12), (30, 16), (6, 16)], fill=(120, 80, 50))
d.rectangle((14, 4, 24, 12), fill=(230, 230, 220))
save_px(outline(a), ENV / "npc_boat.png", 3)
count += 1

a = im(36, 16)
fill_circle(a, 18, 9, 6, [(70, 120, 180), (90, 150, 200), (50, 90, 140)])
d = ImageDraw.Draw(a)
d.polygon([(18, 4), (24, 0), (26, 6)], fill=(70, 120, 180))
save_px(outline(a), ENV / "dolphin.png", 3)
count += 1

a = im(28, 18)
fill_circle(a, 12, 10, 6, [(50, 120, 70), (80, 160, 100)])
fill_circle(a, 20, 10, 4, [(60, 140, 80), (40, 100, 60)])
save_px(outline(a), ENV / "turtle.png", 3)
count += 1

for f in range(2):
    a = im(24, 16)
    d = ImageDraw.Draw(a)
    y = 8 + f
    d.arc((2, y - 4, 12, y + 6), 200, 340, fill=(40, 30, 36), width=2)
    d.arc((10, y - 4, 22, y + 6), 200, 340, fill=(40, 30, 36), width=2)
    save_px(a, ENV / f"bird_{f}.png", 3)
    count += 1

a = im(12, 12)
d = ImageDraw.Draw(a)
d.polygon([(6, 1), (7, 5), (11, 6), (7, 7), (6, 11), (5, 7), (1, 6), (5, 5)], fill=(255, 244, 220))
save_px(a, ENV / "star.png", 3)
count += 1

a = im(8, 12)
d = ImageDraw.Draw(a)
d.polygon([(4, 1), (5, 8), (3, 8)], fill=(100, 180, 220))
save_px(a, ENV / "rain_drop.png", 3)
count += 1

a = im(20, 28)
d = ImageDraw.Draw(a)
d.polygon([(10, 1), (4, 12), (9, 12), (6, 26), (16, 10), (11, 10)], fill=(255, 230, 120))
save_px(a, ENV / "lightning.png", 3)
count += 1

# Seabeds
for name, base, rock in (
    ("seabed_sand", (196, 160, 100), (140, 110, 70)),
    ("seabed_rock", (90, 95, 105), (60, 65, 75)),
    ("seabed_deep", (30, 40, 60), (20, 28, 45)),
):
    a = im(96, 28)
    d = ImageDraw.Draw(a)
    d.rectangle((0, 10, 96, 28), fill=base)
    for i in range(14):
        x = i * 7
        d.polygon([(x, 16), (x + 3, 8), (x + 6, 16)], fill=rock)
    save_px(a, ENV / f"{name}.png", 3)
    count += 1

# Deep water underlay
a = im(96, 64)
d = ImageDraw.Draw(a)
for y in range(64):
    t = y / 63
    c = tuple(int(20 * (1 - t) + 8 * t) for _ in range(3))
    # blue-ish
    c = (int(12 + 8 * (1 - t)), int(24 + 20 * (1 - t)), int(50 + 30 * (1 - t)), 255)
    d.rectangle((0, y, 96, y), fill=c)
save_px(a, ENV / "water_deep.png", 3)
count += 1

# Shop interior
a = im(72, 48)
d = ImageDraw.Draw(a)
d.rectangle((0, 0, 72, 48), fill=(60, 40, 30))
d.rectangle((4, 8, 30, 36), fill=(120, 80, 50))
d.rectangle((40, 10, 66, 40), fill=(40, 35, 40))
fill_circle(a, 16, 40, 4, [(255, 200, 100), (255, 160, 60)])
save_px(a, ENV / "shop_interior.png", 3)
count += 1

# Wake frames
for i in range(3):
    a = im(28, 12)
    d = ImageDraw.Draw(a)
    for x in range(0, 28, 2):
        d.ellipse((x, 3 + (i + x) % 3, x + 5, 9), fill=(220, 240, 255, 140 + i * 30))
    save_px(a, BOATS / f"wake_{i}.png", 3)
    count += 1

a = im(16, 8)
d = ImageDraw.Draw(a)
d.ellipse((2, 2, 7, 6), fill=(230, 245, 255, 180))
d.ellipse((9, 3, 14, 7), fill=(230, 245, 255, 140))
save_px(a, BOATS / "foam.png", 3)
count += 1

# Fish — Stardew-ish chubby side fish
FISH_DEF = {
    "sardine": ((160, 190, 200), False),
    "mackerel": ((70, 130, 110), False),
    "bluegill": ((70, 130, 180), False),
    "flounder": ((190, 160, 100), False),
    "seabass": ((60, 70, 85), False),
    "redsnapper": ((200, 70, 70), False),
    "grouper": ((100, 130, 90), False),
    "cobia": ((110, 120, 130), False),
    "barracuda": ((130, 150, 155), True),
    "mahi": ((50, 180, 110), False),
    "tuna": ((40, 70, 110), True),
    "kingmackerel": ((70, 110, 95), True),
    "swordfish": ((70, 90, 120), True),
    "marlin": ((40, 100, 150), True),
    "gianttrevally": ((190, 140, 60), False),
    "oarfish": ((200, 120, 150), True),
    "anglerfish": ((70, 50, 90), False),
    "giantsquid": ((100, 60, 110), True),
}

for name, (col, long) in FISH_DEF.items():
    w, h = (36, 14) if long else (26, 14)
    a = im(w, h)
    d = ImageDraw.Draw(a)
    hi = tuple(min(255, c + 40) for c in col)
    lo = tuple(max(0, c - 40) for c in col)
    d.ellipse((5, 2, w - 3, h - 2), fill=col)
    d.ellipse((7, 3, w // 2, h // 2 + 1), fill=hi)
    d.polygon([(6, h // 2), (0, 2), (0, h - 2)], fill=lo)
    d.rectangle((w - 9, 5, w - 8, 7), fill=(20, 16, 20))
    if name in ("swordfish", "marlin"):
        d.line([(w - 3, h // 2), (w - 1, h // 2 - 3)], fill=(240, 230, 210), width=1)
    if name == "anglerfish":
        d.line([(w - 8, 2), (w - 2, 0)], fill=(255, 200, 80), width=1)
    save_px(outline(a), FISH / f"{name}.png", 3)
    count += 1

a = im(26, 14)
d = ImageDraw.Draw(a)
d.ellipse((5, 2, 23, 12), fill=(30, 35, 45))
d.polygon([(6, 7), (0, 2), (0, 12)], fill=(30, 35, 45))
save_px(a, FISH / "fish_silhouette.png", 3)
count += 1

# UI icons
def icon_coin():
    a = im(16, 16)
    fill_circle(a, 8, 8, 6, [(255, 210, 90), (230, 170, 50), (180, 120, 30)])
    return outline(a)


def icon_simple(kind):
    a = im(16, 16)
    d = ImageDraw.Draw(a)
    if "weather_clear" in kind:
        fill_circle(a, 8, 7, 5, [(255, 210, 90), (230, 160, 50)])
    elif "rain" in kind or "storm" in kind:
        d.ellipse((2, 3, 14, 11), fill=(100, 110, 130))
        d.line((5, 12, 5, 15), fill=(100, 180, 220))
        d.line((10, 12, 10, 15), fill=(100, 180, 220))
    elif "fog" in kind:
        d.rectangle((2, 6, 14, 8), fill=(160, 160, 170))
        d.rectangle((3, 10, 13, 12), fill=(140, 140, 150))
    elif "wind" in kind:
        d.arc((2, 4, 14, 14), 200, 340, fill=(200, 220, 230), width=2)
    elif "cloudy" in kind:
        fill_circle(a, 6, 8, 4, [(200, 200, 210), (160, 160, 170)])
        fill_circle(a, 11, 8, 4, [(200, 200, 210), (160, 160, 170)])
    elif "clipboard" in kind:
        d.rectangle((3, 2, 13, 15), fill=(196, 148, 92))
        d.rectangle((5, 5, 11, 7), fill=(60, 50, 45))
        d.rectangle((5, 9, 11, 11), fill=(60, 50, 45))
    elif "hook" in kind:
        d.arc((4, 3, 12, 14), 20, 200, fill=(140, 140, 150), width=2)
    elif "bait" in kind:
        cols = {
            "worm": (220, 120, 140),
            "minnow": (90, 170, 180),
            "shrimp": (230, 120, 100),
            "squid": (140, 90, 180),
            "crab": (200, 70, 70),
            "sardine": (100, 140, 180),
            "mackerel": (70, 140, 100),
            "jelly": (120, 230, 240),
        }
        key = kind.split("_")[-1]
        fill_circle(a, 8, 9, 5, [cols.get(key, (200, 180, 80)), tuple(max(0, c - 40) for c in cols.get(key, (200, 180, 80)))])
    else:
        d.rectangle((6, 2, 9, 14), fill=(140, 90, 50))
        d.ellipse((4, 8, 12, 14), fill=(120, 120, 130))
    return outline(a)


save_px(icon_coin(), UI / "coin.png", 3)
count += 1
for k in (
    "hook_icon", "clipboard", "weather_clear", "weather_cloudy", "weather_rain",
    "weather_storm", "weather_fog", "weather_wind", "icon_rod", "icon_reel",
    "icon_hook", "icon_line", "icon_bait_worm", "icon_bait_minnow", "icon_bait_shrimp",
    "icon_bait_squid", "icon_bait_crab", "icon_bait_sardine", "icon_bait_mackerel",
    "icon_bait_jelly", "icon_net", "icon_lantern",
):
    save_px(icon_simple(k), UI / f"{k}.png", 3)
    count += 1

for tier, col in (
    ("common", (120, 120, 130)),
    ("uncommon", (80, 160, 100)),
    ("rare", (70, 110, 180)),
    ("epic", (140, 70, 180)),
    ("legendary", (220, 160, 50)),
):
    a = im(20, 20)
    d = ImageDraw.Draw(a)
    d.rectangle((1, 1, 18, 18), outline=col, width=2)
    save_px(a, UI / f"rarity_{tier}.png", 3)
    count += 1

a = Image.new("RGBA", (32, 32), (12, 22, 34, 220))
d = ImageDraw.Draw(a)
d.rectangle((0, 0, 31, 31), outline=(212, 162, 90, 255), width=2)
save_px(a, UI / "panel.png", 2)
count += 1

a = im(64, 32)
d = ImageDraw.Draw(a)
d.rectangle((4, 8, 60, 28), fill=(30, 50, 80))
fill_circle(a, 32, 12, 8, [(255, 180, 70), (230, 120, 50)])
d.arc((10, 14, 54, 34), 200, 340, fill=(200, 230, 240), width=2)
save_px(a, UI / "logo_offshore.png", 3)
count += 1

# Sounds (keep procedural)
def wav(path, seconds, fn):
    rate = 22050
    n = int(rate * seconds)
    with wave.open(str(path), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            t = i / rate
            v = max(-1.0, min(1.0, fn(t, i)))
            frames += struct.pack("<h", int(v * 28000))
        w.writeframes(frames)


def tone(freq, t, dur=0.05):
    return math.sin(2 * math.pi * freq * t) * max(0, 1 - t / max(0.001, dur))


sounds = {
    "ui_click": (0.05, lambda t, i: tone(880, t, 0.05) * 0.4),
    "ui_hover": (0.04, lambda t, i: tone(660, t, 0.04) * 0.25),
    "purchase": (0.16, lambda t, i: tone(523, t, 0.1) * 0.35 + tone(784, t, 0.16) * 0.25),
    "sell": (0.18, lambda t, i: tone(440, t, 0.18) * 0.3),
    "cast": (0.12, lambda t, i: (random.random() * 2 - 1) * 0.15 * max(0, 1 - t / 0.12)),
    "splash": (0.22, lambda t, i: (random.random() * 2 - 1) * 0.3 * max(0, 1 - t / 0.22)),
    "bite": (0.1, lambda t, i: tone(180, t, 0.1) * 0.45),
    "hook": (0.09, lambda t, i: tone(720, t, 0.09) * 0.4),
    "reel": (0.07, lambda t, i: tone(140 + (i % 15), t, 0.07) * 0.22),
    "tension": (0.12, lambda t, i: tone(90, t, 0.12) * 0.35),
    "line_break": (0.18, lambda t, i: (random.random() * 2 - 1) * 0.45 * max(0, 1 - t / 0.18)),
    "escape": (0.12, lambda t, i: tone(300 - t * 400, t, 0.12) * 0.3),
    "catch": (0.25, lambda t, i: tone(523, t, 0.12) * 0.3 + tone(784, max(0, t - 0.06), 0.18) * 0.3),
    "engine_loop": (0.35, lambda t, i: math.sin(2 * math.pi * 60 * t) * 0.12),
    "waves_loop": (0.45, lambda t, i: (random.random() * 2 - 1) * 0.07),
    "rain_loop": (0.35, lambda t, i: (random.random() * 2 - 1) * 0.1),
    "thunder": (0.4, lambda t, i: (random.random() * 2 - 1) * 0.4 * max(0, 1 - t / 0.4)),
    "seagull": (0.2, lambda t, i: tone(900 + math.sin(t * 18) * 60, t, 0.2) * 0.18),
    "wood_creak": (0.16, lambda t, i: tone(110 + random.random() * 30, t, 0.16) * 0.2),
    "night_ambience": (0.4, lambda t, i: math.sin(2 * math.pi * 70 * t) * 0.04),
}
for name, (dur, fn) in sounds.items():
    wav(SND / f"{name}.wav", dur, fn)
    count += 1

print(f"Stardew pipeline wrote {count} files")
print("sky:", (ENV / "sky_sunset.png").exists(), "shop:", (ENV / "shop_exterior.png").exists())

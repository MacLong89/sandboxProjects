"""Builds plunge's real-art asset set from the archived source art.

Run from the workspace root (sandboxProjects):

    python plunge/tools/import_real_art.py

Sources (all under plunge/ArtSource, salvaged from the retired deep /
deep_dive projects plus agent-generated fills):
  - ArtSource/deep_textures/**     high-detail standalone sprites (RGBA)
  - ArtSource/generated/**         magenta-keyed atlases
  - ArtSource/generated_agent/**   atlases generated for missing sprites

Outputs into plunge/Assets/{sprites,ui,backgrounds}/ using the exact
file names PlungeGame / PlungeHud load. Animated actors get 4 "bob"
frames (name_0..name_3) synthesized from a single pose.
"""

from __future__ import annotations

import os
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parents[1]
PLUNGE = PROJECT / "Assets"
DEEP_TEX = PROJECT / "ArtSource" / "deep_textures"
DIVE_ART = PROJECT / "ArtSource" / "generated"
GEN = PROJECT / "ArtSource" / "generated_agent"

SPRITE_MAX = 512
ICON_MAX = 160
TERRAIN_MAX = 1024


def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def near_magenta_mask(img: Image.Image) -> np.ndarray:
    a = np.array(img, dtype=np.int16)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    return (r > 140) & (b > 140) & (g < 130) & (np.abs(r - b) < 90)


def apply_keep(img: Image.Image, keep: np.ndarray) -> Image.Image:
    # 1px erosion of the kept region (4-neighborhood) to kill AA fringe
    er = keep.copy()
    er[1:, :] &= keep[:-1, :]
    er[:-1, :] &= keep[1:, :]
    er[:, 1:] &= keep[:, :-1]
    er[:, :-1] &= keep[:, 1:]
    out = np.array(img, dtype=np.uint8)
    out[..., 3] = np.where(er, out[..., 3], 0)
    return Image.fromarray(out, "RGBA")


def key_magenta(img: Image.Image) -> Image.Image:
    """Remove every near-magenta pixel (backdrop and enclosed holes alike)."""
    return apply_keep(img, ~near_magenta_mask(img))


def key_magenta_border(img: Image.Image) -> Image.Image:
    """Remove only magenta connected to the border (keeps magenta-ish art)."""
    near = near_magenta_mask(img)
    h, w = near.shape
    bg = np.zeros((h, w), dtype=bool)
    dq = deque()
    for x in range(w):
        for y in (0, h - 1):
            if near[y, x] and not bg[y, x]:
                bg[y, x] = True
                dq.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if near[y, x] and not bg[y, x]:
                bg[y, x] = True
                dq.append((y, x))
    while dq:
        y, x = dq.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and near[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                dq.append((ny, nx))
    return apply_keep(img, ~bg)


def largest_component(img: Image.Image) -> Image.Image:
    """Keep only the largest opaque blob; drops slivers bleeding in from
    neighboring atlas cells."""
    alpha = np.array(img.getchannel("A")) > 8
    h, w = alpha.shape
    labels = np.zeros((h, w), dtype=np.int32)
    sizes = {}
    current = 0
    for sy in range(h):
        for sx in range(w):
            if not alpha[sy, sx] or labels[sy, sx]:
                continue
            current += 1
            dq = deque([(sy, sx)])
            labels[sy, sx] = current
            count = 0
            while dq:
                y, x = dq.popleft()
                count += 1
                for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1),
                               (y - 1, x - 1), (y - 1, x + 1), (y + 1, x - 1), (y + 1, x + 1)):
                    if 0 <= ny < h and 0 <= nx < w and alpha[ny, nx] and not labels[ny, nx]:
                        labels[ny, nx] = current
                        dq.append((ny, nx))
            sizes[current] = count
    if not sizes:
        return img
    best = max(sizes, key=sizes.get)
    out = np.array(img, dtype=np.uint8)
    out[..., 3] = np.where(labels == best, out[..., 3], 0)
    return Image.fromarray(out, "RGBA")


def trim(img: Image.Image, margin: int = 2) -> Image.Image:
    bbox = img.getchannel("A").getbbox()
    if bbox is None:
        return img
    left = max(0, bbox[0] - margin)
    top = max(0, bbox[1] - margin)
    right = min(img.width, bbox[2] + margin)
    bottom = min(img.height, bbox[3] + margin)
    return img.crop((left, top, right, bottom))


def cell(atlas: Image.Image, cols: int, rows: int, col: int, row: int, solo: bool = True) -> Image.Image:
    cw, ch = atlas.width / cols, atlas.height / rows
    box = (int(col * cw), int(row * ch), int((col + 1) * cw), int((row + 1) * ch))
    keyed = key_magenta(atlas.crop(box))
    if solo:
        keyed = largest_component(keyed)
    return trim(keyed)


def shrink(img: Image.Image, max_dim: int) -> Image.Image:
    if max(img.size) <= max_dim:
        return img
    s = max_dim / max(img.size)
    return img.resize((max(1, round(img.width * s)), max(1, round(img.height * s))), Image.LANCZOS)


def save(img: Image.Image, rel: str, max_dim: int = SPRITE_MAX) -> None:
    dest = PLUNGE / rel
    dest.parent.mkdir(parents=True, exist_ok=True)
    shrink(img, max_dim).save(dest)
    print(f"  {rel}")


def bob_frames(img: Image.Image, rel_base: str, max_dim: int = SPRITE_MAX) -> None:
    """Synthesize a gentle 4-frame swim/bob loop from one pose."""
    img = shrink(img, max_dim)
    pad = max(4, img.height // 16)
    canvas_size = (img.width + pad * 2, img.height + pad * 2)
    dy = max(1, img.height // 28)
    offsets = [0, -dy, 0, dy]
    angles = [0.0, 1.6, 0.0, -1.6]
    for i in range(4):
        frame = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
        rotated = img.rotate(angles[i], resample=Image.BICUBIC, expand=False)
        frame.paste(rotated, (pad, pad + offsets[i]), rotated)
        save(frame, f"{rel_base}_{i}.png", max_dim + pad * 2)


def hue_shift(img: Image.Image, degrees: float, sat_mul: float = 1.0) -> Image.Image:
    rgba = np.array(img)
    hsv = np.array(Image.fromarray(rgba[..., :3]).convert("HSV"), dtype=np.int16)
    hsv[..., 0] = (hsv[..., 0] + int(degrees / 360 * 255)) % 256
    hsv[..., 1] = np.clip(hsv[..., 1] * sat_mul, 0, 255)
    rgb = Image.fromarray(hsv.astype(np.uint8), "HSV").convert("RGB")
    out = np.dstack([np.array(rgb), rgba[..., 3]])
    return Image.fromarray(out, "RGBA")


def main() -> None:
    creatures = load(DIVE_ART / "deep_dive_creatures_props_atlas.png")  # 4x3
    hero = load(DIVE_ART / "deep_dive_hero_atlas.png")                  # 3x3
    ui_atlas = load(DIVE_ART / "deep_dive_ui_icon_atlas.png")           # 4x4
    missing = load(GEN / "plunge_missing_atlas.png")                    # 4x2
    ui_extra = load(GEN / "plunge_ui_extra_atlas.png")                  # 4x1

    print("sprites (animated):")
    bob_frames(load(DEEP_TEX / "diver" / "swim.png"), "sprites/diver")
    bob_frames(cell(creatures, 4, 3, 0, 0), "sprites/fish_common")            # yellow tang
    bob_frames(load(DEEP_TEX / "creatures" / "reef_fish.png"), "sprites/fish_blue")
    bob_frames(hue_shift(load(DEEP_TEX / "creatures" / "reef_fish.png"), 45, 0.75), "sprites/fish_rare")
    bob_frames(load(DEEP_TEX / "creatures" / "jellyfish.png"), "sprites/jelly")
    bob_frames(cell(missing, 4, 2, 0, 0), "sprites/shark")
    bob_frames(cell(hero, 3, 3, 2, 1), "sprites/sub")                         # lit submarine

    print("sprites (static):")
    save(cell(creatures, 4, 3, 2, 2), "sprites/chest.png")
    save(cell(creatures, 4, 3, 3, 2), "sprites/idol.png")
    save(cell(missing, 4, 2, 1, 0), "sprites/drone.png")
    save(cell(missing, 4, 2, 2, 0), "sprites/crystal.png")
    save(cell(missing, 4, 2, 3, 0), "sprites/crate.png")
    # Bubbles: border-key so the pink bubble bodies survive, then push hue to cyan
    cw, ch = missing.width / 4, missing.height / 2
    bubble_box = missing.crop((int(3 * cw), int(ch), missing.width, missing.height))
    bubble = trim(key_magenta_border(bubble_box))
    save(hue_shift(bubble, -110, 0.65), "sprites/bubble.png")
    save(load(DEEP_TEX / "world" / "coral_cluster.png"), "sprites/coral.png", TERRAIN_MAX)
    save(load(DEEP_TEX / "world" / "rocks.png"), "sprites/rock.png", TERRAIN_MAX)
    save(load(DEEP_TEX / "world" / "seabed_fill.png"), "sprites/seabed.png", TERRAIN_MAX)

    print("backgrounds:")
    save(load(DEEP_TEX / "world" / "ocean_backdrop.png"), "backgrounds/shallows.png", 1536)
    save(load(GEN / "plunge_bg_reef.png"), "backgrounds/reef.png", 1536)
    save(load(DIVE_ART / "deep_dive_cavern_reference.png"), "backgrounds/cavern.png", 1536)
    save(load(GEN / "plunge_bg_abyss.png"), "backgrounds/abyss.png", 1536)

    print("ui icons:")
    ui_map = {
        "helmet": (0, 0), "suit": (1, 0), "o2": (2, 0), "flippers": (3, 0),
        "harpoon": (0, 1), "lamp": (1, 1), "camera": (2, 1), "sonar": (3, 1),
        "bag": (0, 2), "prop": (0, 3), "shield": (1, 3), "gem": (2, 3),
    }
    for name, (c, r) in ui_map.items():
        save(cell(ui_atlas, 4, 4, c, r), f"ui/{name}.png", ICON_MAX)
    for name, (c, r) in {"lock": (0, 1), "knife": (1, 1), "settings": (2, 1)}.items():
        save(cell(missing, 4, 2, c, r), f"ui/{name}.png", ICON_MAX)
    for name, c in {"coin": 0, "heart": 1, "bolt": 2, "book": 3}.items():
        save(cell(ui_extra, 4, 1, c, 0), f"ui/{name}.png", ICON_MAX)

    print("done")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Install all ai_{stem}_{facing}_idle.png assets into animation facing packs."""

from __future__ import annotations

import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageOps

SIZE = 1024
ROOT = Path(__file__).resolve().parents[1]
ASSETS_GEN = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects-fauna2\assets")
MODELS = ROOT / "Assets" / "models"
ANIM = MODELS / "animations"
HUMAN = {"player", "guest", "guest_boy_1", "guest_boy_2", "guest_girl_1", "guest_sprite"}
PATTERN = re.compile(r"^ai_(?P<stem>.+)_(?P<facing>down|right|up)_idle\.png$", re.I)


def ensure_rgba(img: Image.Image) -> Image.Image:
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    if img.size != (SIZE, SIZE):
        img = img.resize((SIZE, SIZE), Image.Resampling.NEAREST)
    return img


def remove_matte(img: Image.Image) -> Image.Image:
    img = ensure_rgba(img)
    arr = np.asarray(img).copy()
    rgb = arr[..., :3].astype(np.int16)
    samples = [
        rgb[2, 2],
        rgb[2, SIZE - 3],
        rgb[SIZE - 3, 2],
        rgb[SIZE - 3, SIZE - 3],
        rgb[2, SIZE // 2],
        rgb[SIZE // 2, 2],
    ]
    bg = []
    for c in samples:
        c = tuple(int(v) for v in c)
        if all(abs(c[0] - b[0]) + abs(c[1] - b[1]) + abs(c[2] - b[2]) > 40 for b in bg):
            bg.append(c)
        if len(bg) >= 3:
            break

    mask = np.zeros(rgb.shape[:2], dtype=bool)
    for br, bgc, bb in bg:
        mask |= (
            (np.abs(rgb[..., 0] - br) <= 30)
            & (np.abs(rgb[..., 1] - bgc) <= 30)
            & (np.abs(rgb[..., 2] - bb) <= 30)
        )
    mask |= (rgb[..., 0] > 228) & (rgb[..., 1] > 228) & (rgb[..., 2] > 228)
    arr[mask, 3] = 0
    return Image.fromarray(arr, "RGBA")


def save(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    ensure_rgba(img).save(path, format="PNG")


def walk_frames(idle: Image.Image) -> list[Image.Image]:
    frames = []
    for amp, squash in ((0, 1.0), (-18, 0.93), (0, 1.0), (16, 0.95)):
        h = max(32, int(SIZE * squash))
        resized = idle.resize((SIZE, h), Image.Resampling.NEAREST)
        canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        canvas.paste(resized, (0, (SIZE - h) // 2 + amp), resized)
        frames.append(canvas)
    return frames


def write_pack(out_dir: Path, idle: Image.Image) -> None:
    idle = ensure_rgba(idle)
    save(idle, out_dir / "idle.png")
    for i, frame in enumerate(walk_frames(idle)):
        save(frame, out_dir / f"walk_{i}.png")


def dest_root(stem: str) -> Path:
    if stem in HUMAN or stem == "guest":
        folder = "guest" if stem in ("guest", "guest_sprite") else stem
        return ANIM / folder
    return ANIM / "animals" / stem


def mirror_right_to_left(character_root: Path) -> None:
    right = character_root / "right"
    left = character_root / "left"
    if not right.is_dir():
        return
    left.mkdir(parents=True, exist_ok=True)
    for src in right.glob("*.png"):
        save(ImageOps.mirror(ensure_rgba(Image.open(src))), left / src.name)


def main() -> int:
    touched: set[Path] = set()
    for src in sorted(ASSETS_GEN.glob("ai_*_*_idle.png")):
        m = PATTERN.match(src.name)
        if not m:
            continue
        stem = m.group("stem")
        facing = m.group("facing").lower()
        root = dest_root(stem)
        dest = root / facing
        idle = remove_matte(Image.open(src))
        write_pack(dest, idle)
        touched.add(root)
        print(f"installed {src.name} -> {dest}", flush=True)

        if facing == "down":
            static = (
                MODELS / "player_sprite.png"
                if stem == "player"
                else MODELS / f"{stem}.png"
                if stem in HUMAN
                else MODELS / "animals" / f"{stem}.png"
            )
            if stem == "guest":
                static = MODELS / "guest_sprite.png"
            save(idle, static)

    for root in sorted(touched):
        mirror_right_to_left(root)
        print(f"mirrored left for {root.name}", flush=True)

    print(f"Done. Updated {len(touched)} characters.", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())

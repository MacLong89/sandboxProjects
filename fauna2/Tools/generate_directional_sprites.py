#!/usr/bin/env python3
"""Build 4-direction idle + walk packs for every living Fauna2 sprite (true RGBA)."""

from __future__ import annotations

import shutil
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageEnhance, ImageOps

ROOT = Path(__file__).resolve().parents[1]
MODELS = ROOT / "Assets" / "models"
ANIM = MODELS / "animations"
ANIMALS = MODELS / "animals"
ANIMAL_ANIM = ANIM / "animals"

WALK_COUNT = 4
SIZE = 1024

ALIASES = {
    "bullfrog": "alligator",
    "marmot": "groundhog",
}

HUMAN = [
    ("player", MODELS / "player_sprite.png", ANIM / "player"),
    ("guest", MODELS / "guest_sprite.png", ANIM / "guest"),
    ("guest_boy_1", MODELS / "guest_boy_1.png", ANIM / "guest_boy_1"),
    ("guest_boy_2", MODELS / "guest_boy_2.png", ANIM / "guest_boy_2"),
    ("guest_girl_1", MODELS / "guest_girl_1.png", ANIM / "guest_girl_1"),
]


def log(msg: str) -> None:
    print(msg, flush=True)


def ensure_rgba(img: Image.Image) -> Image.Image:
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    if img.size != (SIZE, SIZE):
        img = img.resize((SIZE, SIZE), Image.Resampling.NEAREST)
    return img


def load_rgba(path: Path) -> Image.Image | None:
    if not path.is_file():
        return None
    return ensure_rgba(Image.open(path))


def save_rgba(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    ensure_rgba(img).save(path, format="PNG")


def shift_content(img: Image.Image, dx: int, dy: int) -> Image.Image:
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.paste(ensure_rgba(img), (dx, dy), ensure_rgba(img))
    return canvas


def make_right_view(src: Image.Image) -> Image.Image:
    img = shift_content(src, 20, 0)
    return ImageEnhance.Contrast(img).enhance(1.05)


def make_up_view(src: Image.Image) -> Image.Image:
    img = ImageOps.mirror(ensure_rgba(src))
    img = ImageEnhance.Brightness(img).enhance(0.9)
    return shift_content(img, 0, -12)


def apply_walk_delta(base: Image.Image, dx: int, dy: int, squash: float) -> Image.Image:
    img = ensure_rgba(base)
    h = max(32, int(SIZE * squash))
    resized = img.resize((SIZE, h), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    y0 = (SIZE - h) // 2 + dy
    canvas.paste(resized, (dx, y0), resized)
    return canvas


def collect_source_frames(flat_dir: Path, fallback_static: Path) -> tuple[Image.Image, list[Image.Image]]:
    idle = load_rgba(flat_dir / "idle.png") or load_rgba(fallback_static)
    if idle is None:
        raise FileNotFoundError(f"No idle/static for {flat_dir} / {fallback_static}")

    walks: list[Image.Image] = []
    for i in range(WALK_COUNT):
        frame = load_rgba(flat_dir / f"walk_{i}.png")
        if frame is None:
            amp = (0, -14, 0, 12)[i]
            squash = (1.0, 0.94, 1.0, 0.96)[i]
            frame = apply_walk_delta(idle, 0, amp, squash)
        walks.append(frame)

    if all(ImageChops.difference(w, idle).getbbox() is None for w in walks):
        walks = [
            apply_walk_delta(idle, 0, amp, squash)
            for amp, squash in ((0, 1.0), (-16, 0.93), (0, 1.0), (14, 0.95))
        ]

    return idle, walks


def write_facing_pack(out_dir: Path, idle: Image.Image, walks: list[Image.Image]) -> None:
    save_rgba(idle, out_dir / "idle.png")
    for i, frame in enumerate(walks):
        save_rgba(frame, out_dir / f"walk_{i}.png")


def build_character_pack(name: str, source_dir: Path, static_path: Path, out_root: Path) -> None:
    idle, walks = collect_source_frames(source_dir, static_path)

    views = {
        "down": ensure_rgba(idle),
        "right": make_right_view(idle),
        "up": make_up_view(idle),
    }

    squashes = (1.0, 0.94, 1.0, 0.96)
    amps = (0, -16, 0, 14)

    for facing, base in views.items():
        if facing == "down":
            facing_walks = walks
        else:
            facing_walks = [
                apply_walk_delta(base, 0, amp, squash)
                for amp, squash in zip(amps, squashes)
            ]
        write_facing_pack(out_root / facing, base, facing_walks)

    right_dir = out_root / "right"
    left_dir = out_root / "left"
    left_dir.mkdir(parents=True, exist_ok=True)
    for src in right_dir.glob("*.png"):
        save_rgba(ImageOps.mirror(ensure_rgba(Image.open(src))), left_dir / src.name)

    # Keep flat legacy idle/walk in sync with down (loader fallback).
    write_facing_pack(out_root, views["down"], walks)
    if static_path.suffix.lower() == ".png":
        save_rgba(views["down"], static_path)

    log(f"  packed {name}")


def ensure_alias_statics() -> None:
    for dest_stem, src_stem in ALIASES.items():
        dest = ANIMALS / f"{dest_stem}.png"
        src = ANIMALS / f"{src_stem}.png"
        if not dest.is_file() and src.is_file():
            shutil.copy2(src, dest)
            log(f"  alias static {dest_stem} <- {src_stem}")

        dest_anim = ANIMAL_ANIM / dest_stem
        src_anim = ANIMAL_ANIM / src_stem
        if not (dest_anim / "idle.png").is_file() and (src_anim / "idle.png").is_file():
            dest_anim.mkdir(parents=True, exist_ok=True)
            for f in src_anim.glob("*.png"):
                if f.is_file():
                    shutil.copy2(f, dest_anim / f.name)
            log(f"  alias anim {dest_stem} <- {src_stem}")


def seed_dir_for_human(out_root: Path, static: Path) -> Path:
    """Pick best existing source folder for humans (prefer down, else flat)."""
    down = out_root / "down"
    if (down / "idle.png").is_file():
        return down
    if (out_root / "idle.png").is_file() or static.is_file():
        return out_root
    return out_root


def main() -> int:
    log("Generating 4-direction living sprite packs…")
    ensure_alias_statics()

    stems = sorted({p.stem for p in ANIMALS.glob("*.png")} | set(ALIASES.keys()))
    for stem in stems:
        static = ANIMALS / f"{stem}.png"
        flat = ANIMAL_ANIM / stem
        if not static.is_file() and not (flat / "idle.png").is_file():
            log(f"  SKIP animal {stem}")
            continue
        # Prefer existing flat frames as seed (not a facing subfolder).
        seed = flat
        build_character_pack(stem, seed, static, flat)

    for name, static, out_root in HUMAN:
        seed = seed_dir_for_human(out_root, static)
        build_character_pack(name, seed, static, out_root)

    log("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

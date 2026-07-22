#!/usr/bin/env python3
"""Install AI walk-cycle sheets (2x2 or 1x4) into facing walk_0..3 frames."""

from __future__ import annotations

import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageOps

SIZE = 1024
WALK_COUNT = 4
ROOT = Path(__file__).resolve().parents[1]
ASSETS_GEN = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects-fauna2\assets")
ANIM = ROOT / "Assets" / "models" / "animations"
HUMAN = {"player", "guest", "guest_boy_1", "guest_boy_2", "guest_girl_1", "guest_sprite"}
PATTERN = re.compile(r"^ai_(?P<stem>.+)_(?P<facing>down|right|up)_walk\.png$", re.I)


def ensure_rgba(img: Image.Image) -> Image.Image:
	if img.mode != "RGBA":
		img = img.convert("RGBA")
	return img


def remove_matte(img: Image.Image) -> Image.Image:
	"""Punch near-corner background colors / near-white to alpha (vectorized)."""
	img = ensure_rgba(img)
	arr = np.asarray(img).copy()
	h, w = arr.shape[:2]
	rgb = arr[..., :3].astype(np.int16)
	samples = [
		rgb[2, 2],
		rgb[2, w - 3],
		rgb[h - 3, 2],
		rgb[h - 3, w - 3],
		rgb[2, w // 2],
		rgb[h // 2, 2],
	]
	bg = []
	for c in samples:
		c = tuple(int(v) for v in c)
		if all(abs(c[0] - b[0]) + abs(c[1] - b[1]) + abs(c[2] - b[2]) > 36 for b in bg):
			bg.append(c)
		if len(bg) >= 3:
			break

	mask = np.zeros((h, w), dtype=bool)
	for br, bgc, bb in bg:
		mask |= (
			(np.abs(rgb[..., 0] - br) <= 32)
			& (np.abs(rgb[..., 1] - bgc) <= 32)
			& (np.abs(rgb[..., 2] - bb) <= 32)
		)
	mask |= (rgb[..., 0] > 230) & (rgb[..., 1] > 230) & (rgb[..., 2] > 230)
	arr[mask, 3] = 0
	return Image.fromarray(arr, "RGBA")


def save(img: Image.Image, path: Path) -> None:
	path.parent.mkdir(parents=True, exist_ok=True)
	ensure_rgba(img).resize((SIZE, SIZE), Image.Resampling.NEAREST).save(path, format="PNG")


def slice_sheet(img: Image.Image) -> list[Image.Image] | None:
	img = ensure_rgba(img)
	w, h = img.size

	# Wide 1x4
	if w >= int(h * 2.4):
		cell = w // WALK_COUNT
		return [img.crop((i * cell, 0, (i + 1) * cell, h)) for i in range(WALK_COUNT)]

	# Tall 4x1
	if h >= int(w * 2.4):
		cell = h // WALK_COUNT
		return [img.crop((0, i * cell, w, (i + 1) * cell)) for i in range(WALK_COUNT)]

	# Default: 2x2 grid (most reliable AI layout)
	cell_w, cell_h = w // 2, h // 2
	frames = [
		img.crop((0, 0, cell_w, cell_h)),
		img.crop((cell_w, 0, w, cell_h)),
		img.crop((0, cell_h, cell_w, h)),
		img.crop((cell_w, cell_h, w, h)),
	]

	# Reject sheets where most cells are empty (single-pose dumps)
	opaque = 0
	for frame in frames:
		a = np.asarray(ensure_rgba(frame))[..., 3]
		if a.mean() > 4:
			opaque += 1
	if opaque < 3:
		return None
	return frames


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
	for name in ("walk_0.png", "walk_1.png", "walk_2.png", "walk_3.png"):
		src = right / name
		if src.is_file():
			save(ImageOps.mirror(ensure_rgba(Image.open(src))), left / name)


def main() -> int:
	touched: set[Path] = set()
	for src in sorted(ASSETS_GEN.glob("ai_*_*_walk.png")):
		m = PATTERN.match(src.name)
		if not m:
			continue
		stem = m.group("stem")
		facing = m.group("facing").lower()
		root = dest_root(stem)
		dest = root / facing
		sheet = remove_matte(Image.open(src))
		frames = slice_sheet(sheet)
		if frames is None:
			print(f"SKIP {src.name} — need a 2x2 or 1x4 sheet with walk poses", flush=True)
			continue
		for i, frame in enumerate(frames):
			save(frame, dest / f"walk_{i}.png")
		touched.add(root)
		print(f"installed walk {stem}/{facing}", flush=True)

	for root in sorted(touched):
		mirror_right_to_left(root)
		print(f"mirrored left walks for {root.name}", flush=True)

	print(f"Done. Updated walks for {len(touched)} characters.", flush=True)
	return 0


if __name__ == "__main__":
	sys.exit(main())

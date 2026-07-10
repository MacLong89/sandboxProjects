#!/usr/bin/env python3
"""
Generate Thorns terrain support textures.

By default ONLY writes *_rough.png (matte roughness) — does not touch albedo PNGs.
Pass --albedo to regenerate procedural grass/dirt/rock/snow/sand/water (overwrites).
"""
from __future__ import annotations

import argparse
import random
from pathlib import Path

from PIL import Image

OUT = Path(__file__).resolve().parents[1] / "Assets" / "materials" / "terrain_materials"
TERRAIN_MAT_OUT = Path(__file__).resolve().parents[1] / "Assets" / "terrain_materials"
ALBEDO_OUT_DIRS = (OUT, TERRAIN_MAT_OUT)
SIZE = 512
WATER_SIZE = 1024
SEED = 42069


def hex_rgb(h: str) -> tuple[int, int, int]:
	h = h.lstrip("#")
	return tuple(int(h[i : i + 2], 16) for i in (0, 2, 4))


def lerp(a: int, b: int, t: float) -> int:
	return int(a + (b - a) * t)


def mix(c0: tuple[int, int, int], c1: tuple[int, int, int], t: float) -> tuple[int, int, int]:
	return lerp(c0[0], c1[0], t), lerp(c0[1], c1[1], t), lerp(c0[2], c1[2], t)


def facet_noise(x: int, y: int, cell: int = 16) -> float:
	bx, by = x // cell, y // cell
	return ((bx * 17 + by * 31 + SEED) % 97) / 97.0


def write_faceted(name: str, sun: tuple[int, int, int], shade: tuple[int, int, int], cell: int = 16, jitter: float = 0.08):
	random.seed(SEED)
	img = Image.new("RGB", (SIZE, SIZE))
	pix = img.load()
	for y in range(SIZE):
		for x in range(SIZE):
			f = facet_noise(x, y, cell)
			lit = 0.58 + 0.42 * (1.0 if (x // cell + y // cell) % 3 == 0 else 0.82)
			base = mix(shade, sun, f)
			j = 1.0 + (random.random() - 0.5) * jitter
			pix[x, y] = tuple(min(255, int(c * lit * j)) for c in base)
	for folder in ALBEDO_OUT_DIRS:
		folder.mkdir(parents=True, exist_ok=True)
		img.save(folder / f"{name}.png")
		print(f"wrote {folder / f'{name}.png'}")


def write_matte_roughness(name: str, base: int = 238, cell: int = 20, spread: int = 10):
	"""Bright roughness = matte terrain (avoids glossy default_rough). Only writes *_rough.png."""
	random.seed(SEED + hash(name) % 10000)
	img = Image.new("L", (SIZE, SIZE))
	pix = img.load()
	for y in range(SIZE):
		for x in range(SIZE):
			f = facet_noise(x, y, cell)
			v = base + int((f - 0.5) * spread * 2)
			pix[x, y] = max(200, min(255, v))
	for folder in (OUT, TERRAIN_MAT_OUT):
		folder.mkdir(parents=True, exist_ok=True)
		img.save(folder / f"{name}_rough.png")
		print(f"wrote {folder / f'{name}_rough.png'}")


def write_water():
	random.seed(SEED + 3)
	surface = hex_rgb("#68B8F1")
	deep = hex_rgb("#247FB8")
	img = Image.new("RGB", (WATER_SIZE, WATER_SIZE))
	pix = img.load()
	for y in range(WATER_SIZE):
		for x in range(WATER_SIZE):
			wave = (math_sin(x * 0.04) + math_sin(y * 0.05)) * 0.5 + 0.5
			base = mix(deep, surface, wave * 0.65 + facet_noise(x, y, 32) * 0.35)
			pix[x, y] = base
	for folder in ALBEDO_OUT_DIRS:
		folder.mkdir(parents=True, exist_ok=True)
		img.save(folder / "water.png")
		print(f"wrote {folder / 'water.png'}")


def math_sin(v: float) -> float:
	import math

	return math.sin(v)


def generate_roughness():
	write_matte_roughness("grass", base=244, cell=18, spread=8)
	write_matte_roughness("dirt", base=250, cell=22, spread=6)
	write_matte_roughness("rock", base=232, cell=14, spread=12)
	write_matte_roughness("snow", base=218, cell=24, spread=10)


def _smooth01(t: float) -> float:
	t = max(0.0, min(1.0, t))
	return t * t * (3.0 - 2.0 * t)


def _hash_noise(x: float, y: float, seed: float) -> float:
	import math

	n = math.sin(x * 12.9898 + y * 78.233 + seed) * 43758.5453
	return n - math.floor(n)


def tint_grass_albedo_toward_plant(
	output_name: str = "grass_new_blend",
	strength: float = 0.48,
	plant_rgb: tuple[int, int, int] = (94, 198, 52),
) -> None:
	"""Shift grass albedo halfway between current olive ground and bright foliage green."""
	path = TERRAIN_MAT_OUT / f"{output_name}.png"
	if not path.exists():
		raise FileNotFoundError(f"Missing grass blend: {path}")

	img = Image.open(path).convert("RGB")
	pix = img.load()
	width, height = img.size

	rs: list[int] = []
	gs: list[int] = []
	bs: list[int] = []
	for y in range(0, height, 4):
		for x in range(0, width, 4):
			c = pix[x, y]
			rs.append(c[0])
			gs.append(c[1])
			bs.append(c[2])
	avg = (sum(rs) / len(rs), sum(gs) / len(gs), sum(bs) / len(bs))
	target = tuple(int(avg[i] + (plant_rgb[i] - avg[i]) * strength) for i in range(3))

	for y in range(height):
		for x in range(width):
			c = pix[x, y]
			offset = (c[0] - avg[0], c[1] - avg[1], c[2] - avg[2])
			shifted = (
				target[0] + offset[0] * 0.92,
				target[1] + offset[1] * 0.92,
				target[2] + offset[2] * 0.92,
			)
			pix[x, y] = tuple(max(0, min(255, int(v))) for v in shifted)

	img.save(path)
	print(f"tinted {path} toward {target} (strength={strength:.2f}, from avg={tuple(int(v) for v in avg)})")


def blend_grass_new_variants(output_name: str = "grass_new_blend") -> None:
	"""Tileable 4-way mix of grass_new*.png for thorns_grass.tmat."""
	sources = [
		"grass_new",
		"grass_new_lush",
		"grass_new_dirt",
		"grass_new_dry",
	]
	paths = [TERRAIN_MAT_OUT / f"{name}.png" for name in sources]
	for path in paths:
		if not path.exists():
			raise FileNotFoundError(f"Missing grass variant: {path}")

	images = [Image.open(path).convert("RGB") for path in paths]
	width = max(img.width for img in images)
	height = max(img.height for img in images)
	if width != height:
		size = max(width, height)
		width = height = size
	images = [img.resize((width, height), Image.Resampling.LANCZOS) for img in images]

	out = Image.new("RGB", (width, height))
	pix_out = out.load()
	pix = [img.load() for img in images]

	for y in range(height):
		for x in range(width):
			n_macro = _hash_noise(x / 96.0, y / 96.0, SEED + 3.7)
			n_fine = _hash_noise(x / 42.0, y / 42.0, SEED + 19.2)
			n_patch = _hash_noise(x / 18.0, y / 18.0, SEED + 47.5)

			w_lush = _smooth01((n_macro - 0.48) / 0.22) * _smooth01((n_fine - 0.28) / 0.28)
			w_dry = _smooth01((0.32 - n_macro) / 0.16) * _smooth01((0.55 - n_fine) / 0.22)
			w_dirt = _smooth01((n_fine - 0.58) / 0.18) * (1.0 - w_lush * 0.65)
			w_base = max(0.05, 1.0 - w_lush - w_dry - w_dirt)
			w_patch = _smooth01((n_patch - 0.5) / 0.35) * 0.22

			w0 = w_base * (1.0 - w_patch) + w_patch * 0.25
			w1 = w_lush * (1.0 - w_patch) + w_patch * 0.25
			w2 = w_dirt * (1.0 - w_patch) + w_patch * 0.25
			w3 = w_dry * (1.0 - w_patch) + w_patch * 0.25
			total = w0 + w1 + w2 + w3
			w0, w1, w2, w3 = w0 / total, w1 / total, w2 / total, w3 / total

			rgb = [0, 0, 0]
			for wi, img_i in zip((w0, w1, w2, w3), range(4)):
				c = pix[img_i][x, y]
				rgb[0] += c[0] * wi
				rgb[1] += c[1] * wi
				rgb[2] += c[2] * wi
			pix_out[x, y] = tuple(int(v) for v in rgb)

	TERRAIN_MAT_OUT.mkdir(parents=True, exist_ok=True)
	out_path = TERRAIN_MAT_OUT / f"{output_name}.png"
	out.save(out_path)
	print(f"wrote {out_path}")


def generate_albedo():
	write_faceted("grass", hex_rgb("#72C058"), hex_rgb("#3F6634"), cell=14)
	write_faceted("dirt", hex_rgb("#858A56"), hex_rgb("#565642"), cell=18)
	write_faceted("rock", hex_rgb("#748095"), hex_rgb("#3E4858"), cell=12)
	write_faceted("snow", hex_rgb("#F8FAFC"), hex_rgb("#D8E4EE"), cell=20, jitter=0.04)
	write_faceted("sand", hex_rgb("#C4B88A"), hex_rgb("#9A8F68"), cell=16)
	write_water()


def main():
	parser = argparse.ArgumentParser(description="Generate Thorns terrain textures.")
	parser.add_argument(
		"--albedo",
		action="store_true",
		help="Regenerate procedural albedo PNGs in materials/terrain_materials (OVERWRITES).",
	)
	parser.add_argument(
		"--blend-grass",
		action="store_true",
		help="Blend grass_new*.png variants into terrain_materials/grass_new_blend.png.",
	)
	parser.add_argument(
		"--tint-grass",
		action="store_true",
		help="Tint grass_new_blend.png greener (midpoint between current ground and foliage).",
	)
	parser.add_argument(
		"--tint-grass-strength",
		type=float,
		default=0.48,
		help="How far to shift grass albedo toward plant green (0-1).",
	)
	args = parser.parse_args()

	generate_roughness()
	if args.blend_grass:
		blend_grass_new_variants()
	if args.tint_grass:
		tint_grass_albedo_toward_plant(strength=args.tint_grass_strength)
	if args.albedo:
		generate_albedo()
	elif not args.blend_grass and not args.tint_grass:
		print("Skipped albedo (use --albedo to regenerate procedural grass/dirt/rock/snow).")


if __name__ == "__main__":
	main()

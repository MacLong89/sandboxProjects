#!/usr/bin/env python3
"""Create non-destructive stylized texture variants for the Thorns terrain look."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageEnhance, ImageOps

ROOT = Path(__file__).resolve().parents[1]
TERRAIN_DIRS = [
	ROOT / "Assets" / "terrain_materials",
	ROOT / "Assets" / "materials" / "terrain_materials",
]
SKYBOX = ROOT / "Assets" / "skybox.png"
FOLIAGE_DIR = ROOT / "Assets" / "models" / "foliage2"


def hex_rgb(value: str) -> tuple[int, int, int]:
	value = value.lstrip("#")
	return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4))


def lerp(a: float, b: float, t: float) -> float:
	return a + (b - a) * t


def mix(c0: tuple[int, int, int], c1: tuple[int, int, int], t: float) -> tuple[int, int, int]:
	return tuple(int(lerp(c0[i], c1[i], t)) for i in range(3))


def grade_to_palette(src: Image.Image, dark: str, mid: str, light: str, sat: float, contrast: float) -> Image.Image:
	src = src.convert("RGB")
	luma = ImageOps.grayscale(src)
	luma = ImageEnhance.Contrast(luma).enhance(contrast)

	dark_rgb = hex_rgb(dark)
	mid_rgb = hex_rgb(mid)
	light_rgb = hex_rgb(light)
	out = Image.new("RGB", src.size)

	src_px = src.load()
	luma_px = luma.load()
	out_px = out.load()

	for y in range(src.height):
		for x in range(src.width):
			t = luma_px[x, y] / 255.0
			if t < 0.52:
				color = mix(dark_rgb, mid_rgb, t / 0.52)
			else:
				color = mix(mid_rgb, light_rgb, (t - 0.52) / 0.48)

			original = src_px[x, y]
			out_px[x, y] = tuple(
				max(0, min(255, int(lerp(color[i], original[i], 1.0 - sat))))
				for i in range(3)
			)

	return out


def stylize_water(src: Image.Image) -> Image.Image:
	img = grade_to_palette(src, "#4B94A4", "#54B3D2", "#8BD4EA", sat=0.48, contrast=1.0)
	img = ImageEnhance.Color(img).enhance(0.82)
	img = ImageEnhance.Brightness(img).enhance(1.45)
	return img


def stylize_skybox(src: Image.Image) -> Image.Image:
	img = src.convert("RGB")
	img = ImageEnhance.Color(img).enhance(1.1)
	img = ImageEnhance.Contrast(img).enhance(1.03)
	img = ImageEnhance.Brightness(img).enhance(1.12)

	px = img.load()
	for y in range(img.height):
		top = 1.0 - y / max(1, img.height - 1)
		for x in range(img.width):
			r, g, b = px[x, y]
			blue_lift = 0.04 * top
			warm_cloud = max(0.0, (r + g + b) / 765.0 - 0.72) * 0.16
			px[x, y] = (
				max(0, min(255, int(r * (1.0 - blue_lift * 0.18) + 255 * warm_cloud))),
				max(0, min(255, int(g * (1.0 + blue_lift * 0.02) + 218 * warm_cloud))),
				max(0, min(255, int(b * (1.0 + blue_lift)))),
			)
	return img


def write_terrain_variant(name: str, image: Image.Image) -> None:
	for folder in TERRAIN_DIRS:
		target = folder / f"{name}_stylized.png"
		image.save(target)
		print(f"wrote {target}")


def write_foliage_variant(name: str, image: Image.Image) -> None:
	target = FOLIAGE_DIR / f"{name}_stylized.png"
	image.save(target)
	print(f"wrote {target}")


def main() -> None:
	source_dir = TERRAIN_DIRS[0]
	write_terrain_variant(
		"grass",
		grade_to_palette(
			Image.open(source_dir / "grass.png"),
			"#6F763E",
			"#9DA35C",
			"#C8CD82",
			sat=0.42,
			contrast=0.98,
		),
	)
	write_terrain_variant(
		"dirt",
		grade_to_palette(
			Image.open(source_dir / "dirt.png"),
			"#7A7254",
			"#A19668",
			"#C3B782",
			sat=0.35,
			contrast=0.98,
		),
	)
	write_terrain_variant(
		"rock",
		grade_to_palette(
			Image.open(source_dir / "rock.png"),
			"#747A7D",
			"#989D9F",
			"#C6C8C6",
			sat=0.35,
			contrast=0.98,
		),
	)
	write_terrain_variant(
		"snow",
		grade_to_palette(
			Image.open(source_dir / "snow.png"),
			"#CFE0EE",
			"#EEF5FA",
			"#FFFFFF",
			sat=0.55,
			contrast=1.02,
		),
	)
	write_terrain_variant(
		"sand",
		grade_to_palette(
			Image.open(source_dir / "sand.png"),
			"#AAA171",
			"#CEC083",
			"#E9DAA2",
			sat=0.35,
			contrast=0.98,
		),
	)
	write_terrain_variant( "water", stylize_water( Image.open(source_dir / "water.png") ) )

	write_foliage_variant(
		"pine_tree_basecolor",
		grade_to_palette(
			Image.open(FOLIAGE_DIR / "pine_tree_basecolor.png"),
			"#173C2C",
			"#285A3B",
			"#4B7B4C",
			sat=0.45,
			contrast=0.92,
		),
	)
	write_foliage_variant(
		"oak_tree_basecolor",
		grade_to_palette(
			Image.open(FOLIAGE_DIR / "oak_tree_basecolor.png"),
			"#315124",
			"#557239",
			"#83965A",
			sat=0.42,
			contrast=0.92,
		),
	)
	write_foliage_variant(
		"aspen_tree_basecolor",
		grade_to_palette(
			Image.open(FOLIAGE_DIR / "aspen_tree_basecolor.png"),
			"#556B39",
			"#7F9258",
			"#AEB878",
			sat=0.35,
			contrast=0.9,
		),
	)

	if SKYBOX.exists():
		target = ROOT / "Assets" / "skybox_stylized.png"
		stylize_skybox( Image.open(SKYBOX) ).save(target)
		print(f"wrote {target}")


if __name__ == "__main__":
	main()

#!/usr/bin/env python3
"""Generate Thorns UI chrome textures — parchment, wood 9-slice frames, tab rail, slots."""
from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "ui" / "menu" / "chrome"

# Concept palette
PARCHMENT = (227, 217, 198)
PARCHMENT_EDGE = (196, 181, 154)
PARCHMENT_STAIN = (168, 152, 128)
WOOD_DARK = (43, 35, 29)
WOOD_MID = (74, 58, 42)
WOOD_LIGHT = (98, 78, 56)
WOOD_GRAIN = (35, 28, 22)
SLOT_BG = (51, 51, 51)
SLOT_BORDER = (90, 90, 90)
MOSS = (74, 93, 35)
MOSS_DARK = (52, 68, 28)


def hex_rgb(value: str) -> tuple[int, int, int]:
	value = value.lstrip("#")
	return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4))


def lerp(a: float, b: float, t: float) -> float:
	return a + (b - a) * t


def mix(c0: tuple[int, int, int], c1: tuple[int, int, int], t: float) -> tuple[int, int, int]:
	return tuple(int(lerp(c0[i], c1[i], t)) for i in range(3))


def add_noise(img: Image.Image, amount: float = 12.0, seed: int = 42) -> Image.Image:
	rng = random.Random(seed)
	px = img.load()
	for y in range(img.height):
		for x in range(img.width):
			r, g, b = px[x, y][:3]
			n = rng.uniform(-amount, amount)
			px[x, y] = (
				max(0, min(255, int(r + n))),
				max(0, min(255, int(g + n))),
				max(0, min(255, int(b + n))),
				px[x, y][3] if len(px[x, y]) > 3 else 255,
			)
	return img


def parchment_tile(size: int = 512) -> Image.Image:
	img = Image.new("RGBA", (size, size), PARCHMENT + (255,))
	px = img.load()
	cx, cy = size / 2, size / 2
	max_r = math.hypot(cx, cy)
	for y in range(size):
		for x in range(size):
			d = math.hypot(x - cx, y - cy) / max_r
			t = min(1.0, d ** 1.35)
			base = mix(PARCHMENT, PARCHMENT_EDGE, t * 0.55)
			if t > 0.72:
				base = mix(base, PARCHMENT_STAIN, (t - 0.72) / 0.28)
			px[x, y] = base + (255,)
	add_noise(img, 10.0, 7)
	return img.filter(ImageFilter.GaussianBlur(radius=0.4))


def parchment_seamless(size: int = 256) -> Image.Image:
	"""Tile-safe parchment grain — periodic noise, no blur, no corner falloff."""
	img = Image.new("RGBA", (size, size), PARCHMENT + (255,))
	px = img.load()
	tau = math.tau
	for y in range(size):
		for x in range(size):
			u = x / size
			v = y / size
			# Periodic at tile edges — wraps cleanly when repeated.
			n = (
				math.sin(u * tau * 3.7) * math.cos(v * tau * 2.9) * 2.8
				+ math.sin((u + v) * tau * 5.1) * 2.0
				+ math.cos(u * tau * 8.3 + v * tau * 6.7) * 1.4
			)
			px[x, y] = (
				max(0, min(255, int(PARCHMENT[0] + n))),
				max(0, min(255, int(PARCHMENT[1] + n * 0.96))),
				max(0, min(255, int(PARCHMENT[2] + n * 0.9))),
				255,
			)
	return img


def parchment_flat(size: int = 512) -> Image.Image:
	"""Uniform parchment — delegates to seamless tile (no blur — blur darkens tile corners)."""
	return parchment_seamless(size)


def parchment_vignette(w: int, h: int) -> Image.Image:
	base = parchment_tile(max(w, h)).resize((w, h), Image.Resampling.LANCZOS)
	overlay = Image.new("RGBA", (w, h), (0, 0, 0, 0))
	draw = ImageDraw.Draw(overlay)
	margin = int(min(w, h) * 0.06)
	draw.rectangle([0, 0, w, h], fill=(60, 45, 30, 0))
	for i in range(margin):
		alpha = int(lerp(0, 55, i / margin))
		draw.rectangle([i, i, w - 1 - i, h - 1 - i], outline=(40, 30, 20, alpha))
	return Image.alpha_composite(base, overlay)


def wood_grain_rect(w: int, h: int, base: tuple[int, int, int], vary: float = 0.08) -> Image.Image:
	img = Image.new("RGBA", (w, h), base + (255,))
	px = img.load()
	for y in range(h):
		for x in range(w):
			wave = math.sin(x * 0.04 + y * 0.015) * 0.5 + math.sin(y * 0.08) * 0.3
			t = 0.5 + wave * vary
			c = mix(WOOD_DARK, WOOD_LIGHT, t)
			px[x, y] = c + (255,)
	add_noise(img, 8.0, 11)
	return img


def nine_slice_frame(size: int, slice_px: int, inner_alpha: int = 0) -> Image.Image:
	"""Border-only 9-slice: transparent center, wood border with bevel."""
	img = Image.new("RGBA", (size, size), (0, 0, 0, inner_alpha))
	draw = ImageDraw.Draw(img)
	s = slice_px

	# Fill border regions with wood grain patches
	for box in [
		(0, 0, size, s),
		(0, size - s, size, size),
		(0, 0, s, size),
		(size - s, 0, size, size),
	]:
		patch = wood_grain_rect(box[2] - box[0], box[3] - box[1], WOOD_MID)
		img.paste(patch, (box[0], box[1]))

	# Corner darkening
	for corner in [(0, 0), (size - s, 0), (0, size - s), (size - s, size - s)]:
		corner_img = wood_grain_rect(s, s, WOOD_DARK, 0.12)
		img.paste(corner_img, corner)

	# Inner highlight line
	hl = mix(WOOD_LIGHT, PARCHMENT, 0.15)
	draw.line([(s, s), (size - s, s)], fill=hl + (180,), width=2)
	draw.line([(s, s), (s, size - s)], fill=hl + (140,), width=2)

	# Outer shadow
	draw.rectangle([0, 0, size - 1, size - 1], outline=WOOD_GRAIN + (255,), width=2)
	return img


def tab_rail(w: int = 1024, h: int = 72) -> Image.Image:
	"""Single horizontal wood plank — smooth grain, no pinstripe banding when stretched."""
	img = Image.new("RGBA", (w, h), WOOD_DARK + (255,))
	px = img.load()
	for y in range(h):
		vgrad = y / max(h - 1, 1)
		for x in range(w):
			# Vertical plank grain only (no horizontal sine bands).
			t = 0.5 + math.sin(x * 0.018) * 0.07 + math.sin(x * 0.041 + 1.7) * 0.04
			base = mix(WOOD_DARK, WOOD_MID, t)
			base = mix(base, WOOD_LIGHT, (1.0 - vgrad) * 0.14)
			px[x, y] = base + (255,)
	add_noise(img, 5.0, 23)
	img = img.filter(ImageFilter.GaussianBlur(radius=0.55))
	draw = ImageDraw.Draw(img)
	draw.rectangle([0, h - 4, w, h], fill=WOOD_GRAIN + (255,))
	draw.line([0, h - 5, w, h - 5], fill=mix(WOOD_LIGHT, (212, 175, 90), 0.2) + (70,), width=1)
	draw.line([0, 1, w, 1], fill=WOOD_LIGHT + (70,), width=1)
	return img


def column_divider(w: int = 20, h: int = 512) -> Image.Image:
	"""Vertical wood beam between menu columns."""
	img = Image.new("RGBA", (w, h), WOOD_DARK + (255,))
	px = img.load()
	for x in range(w):
		hgrad = abs(x - (w - 1) / 2) / max((w - 1) / 2, 1)
		for y in range(h):
			t = 0.5 + math.sin(y * 0.022 + x * 0.004) * 0.08
			base = mix(WOOD_DARK, WOOD_MID, t)
			base = mix(base, WOOD_GRAIN, hgrad * 0.35)
			px[x, y] = base + (255,)
	add_noise(img, 4.0, 31)
	draw = ImageDraw.Draw(img)
	draw.line([1, 0, 1, h], fill=mix(WOOD_LIGHT, (212, 175, 90), 0.15) + (100,), width=1)
	draw.line([w - 2, 0, w - 2, h], fill=WOOD_GRAIN + (220,), width=2)
	return img


def section_rule(w: int = 256, h: int = 10) -> Image.Image:
	"""Ornamental horizontal rule — diamond center, for section headers."""
	img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
	draw = ImageDraw.Draw(img)
	mid = h // 2
	gold = (212, 175, 90)
	draw.line([0, mid, w, mid], fill=WOOD_DARK + (190,), width=1)
	draw.line([0, mid + 1, w, mid + 1], fill=WOOD_GRAIN + (80,), width=1)
	cx = w // 2
	draw.polygon([(cx, 1), (cx + 5, mid), (cx, h - 2), (cx - 5, mid)], fill=gold + (255,))
	draw.polygon([(cx, 3), (cx + 3, mid), (cx, h - 4), (cx - 3, mid)], fill=mix(WOOD_MID, gold, 0.35) + (255,))
	return img


def tab_plank(w: int = 120, h: int = 72, selected: bool = False) -> Image.Image:
	base = WOOD_MID if not selected else WOOD_LIGHT
	img = wood_grain_rect(w, h, base, 0.14 if selected else 0.08)
	draw = ImageDraw.Draw(img)
	border = WOOD_LIGHT if selected else WOOD_DARK
	draw.rectangle([1, 1, w - 2, h - 2], outline=border + (255,), width=2)
	if selected:
		draw.rectangle([4, 4, w - 5, h - 5], outline=(212, 175, 90, 90), width=1)
	return img


def slot_tile(size: int = 64) -> Image.Image:
	img = Image.new("RGBA", (size, size), SLOT_BG + (230,))
	draw = ImageDraw.Draw(img)
	draw.rectangle([0, 0, size - 1, size - 1], outline=SLOT_BORDER + (255,), width=2)
	draw.rectangle([2, 2, size - 3, size - 3], outline=(70, 70, 70, 255), width=1)
	# subtle top highlight
	for x in range(3, size - 3):
		img.putpixel((x, 3), mix(SLOT_BG, (80, 80, 80), 0.3) + (255,))
	return img


def vine_corner(size: int = 128) -> Image.Image:
	img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
	draw = ImageDraw.Draw(img)
	# moss blob top-left
	for r in range(40, 8, -4):
		alpha = int(lerp(200, 80, (40 - r) / 32))
		draw.ellipse([-r // 3, -r // 3, r, r], fill=MOSS + (alpha,))
	draw.ellipse([4, 4, 52, 48], fill=MOSS_DARK + (160,))
	# vine strokes
	draw.line([(8, 60), (30, 40), (55, 28), (90, 18)], fill=MOSS_DARK + (200,), width=3)
	draw.line([(12, 70), (35, 55), (70, 42)], fill=MOSS + (180,), width=2)
	return img.filter(ImageFilter.GaussianBlur(radius=0.3))


def main() -> None:
	OUT.mkdir(parents=True, exist_ok=True)
	assets = {
		"parchment_clean.png": parchment_seamless(256),
		# menu_backdrop.png — hand-authored full-screen parchment; do not regenerate.
		"menu_backdrop_vignette.png": parchment_vignette(1024, 768),
		"frame_panel_9.png": nine_slice_frame(256, 128),
		"frame_section_9.png": nine_slice_frame(192, 96),
		"frame_card_9.png": nine_slice_frame(144, 72),
		"frame_slot_9.png": nine_slice_frame(96, 48),
		"frame_outer_9slice.png": nine_slice_frame(256, 128),
		"tab_rail.png": tab_rail(),
		"column_divider.png": column_divider(),
		"section_rule.png": section_rule(),
		"tab_plank_normal.png": tab_plank(selected=False),
		"tab_plank_selected.png": tab_plank(selected=True),
		"tab_plank_hover.png": tab_plank(selected=False),
		"tab_plank_active.png": tab_plank(selected=True),
		"slot_dark.png": slot_tile(),
		"vine_corner_tl.png": vine_corner(),
		"parchment_panel.png": parchment_seamless(256),
	}

	for name, img in assets.items():
		path = OUT / name
		img.save(path, "PNG")
		print(f"wrote {path.relative_to(ROOT)}")

	print(f"Done — {len(assets)} textures in {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
	main()

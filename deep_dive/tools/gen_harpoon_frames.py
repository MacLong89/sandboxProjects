"""Generate harpoon throw frames and spear sprite for DEEP."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[1] / "Assets" / "textures"
IDLE = OUT / "diver" / "idle.png"

SPEAR = (200, 205, 210, 255)
SPEAR_TIP = (230, 140, 45, 255)


def make_frame(base: Image.Image, length_frac: float, nudge_frac: float = 0) -> Image.Image:
	im = base.copy()
	draw = ImageDraw.Draw(im)
	w, h = base.size
	hand_x = int(w * 0.55) + int(w * nudge_frac)
	hand_y = int(h * 0.38)
	length = max(3, int(w * length_frac))
	line_w = max(1, int(w * 0.004))
	tip = max(4, int(w * 0.012))
	x1 = hand_x + length
	draw.line([(hand_x, hand_y), (x1, hand_y)], fill=SPEAR, width=line_w)
	draw.polygon([(x1, hand_y), (x1 + tip, hand_y - tip // 2), (x1 + tip, hand_y + tip // 2)], fill=SPEAR_TIP)
	return im


def draw_spear_sprite() -> Image.Image:
	im = Image.new("RGBA", (20, 5), (0, 0, 0, 0))
	draw = ImageDraw.Draw(im)
	draw.line([(1, 2), (14, 2)], fill=SPEAR, width=1)
	draw.polygon([(14, 2), (18, 1), (18, 3)], fill=SPEAR_TIP)
	return im


def main() -> int:
	diver_dir = OUT / "diver"
	effects_dir = OUT / "effects"
	diver_dir.mkdir(parents=True, exist_ok=True)
	effects_dir.mkdir(parents=True, exist_ok=True)

	if not IDLE.exists():
		print(f"MISSING {IDLE}")
		return 1

	base = Image.open(IDLE).convert("RGBA")
	extends = [(0.035, 0), (0.055, 0.01), (0.075, 0.02)]
	for i, (length_frac, nudge_frac) in enumerate(extends, start=1):
		path = diver_dir / f"harpoon_{i}.png"
		make_frame(base, length_frac, nudge_frac).save(path, "PNG")
		print(f"wrote {path}")

	spear_path = effects_dir / "harpoon_spear.png"
	draw_spear_sprite().save(spear_path, "PNG")
	print(f"wrote {spear_path}")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())

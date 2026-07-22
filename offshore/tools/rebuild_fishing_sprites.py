"""Rebuild cast/fish/hook sprites: match idle hat→boot scale, strip bobber/line."""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets" / "textures" / "art"
SHEET = ROOT / "Assets" / "Art" / "heroes" / "hero_player_cast_sheet.png"
TARGET_W, TARGET_H = 112, 160


def load_rgba(path: Path) -> np.ndarray:
	return np.array(Image.open(path).convert("RGBA"))


def cap_boot_span(arr: np.ndarray) -> tuple[int, int, int]:
	"""Return (cap_top_y, boot_bottom_y, height) using blue cap + brown boots."""
	r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
	opaque = a > 20
	# Blue cap / shirt-ish (prefer brighter blue for cap)
	cap = opaque & (b > 130) & (b > r + 30) & (b > g + 10) & (r < 120)
	# Brown boots
	boot = opaque & (r > 50) & (r > g) & (g > b) & (r < 200) & (b < 80) & (g < 120)

	cap_ys = np.where(cap.any(axis=1))[0]
	boot_ys = np.where(boot.any(axis=1))[0]
	if len(cap_ys) == 0 or len(boot_ys) == 0:
		# Fallback: dense core
		dens = ndimage.uniform_filter(opaque.astype(float), size=9)
		core = dens > 0.5
		ys = np.where(core.any(axis=1))[0]
		return int(ys.min()), int(ys.max()), int(ys.max() - ys.min() + 1)

	# Cap top = highest blue in upper half; boots = lowest brown in lower half
	h = arr.shape[0]
	cap_top = int(cap_ys[cap_ys < h * 0.55].min()) if np.any(cap_ys < h * 0.55) else int(cap_ys.min())
	boot_bot = int(boot_ys[boot_ys > h * 0.45].max()) if np.any(boot_ys > h * 0.45) else int(boot_ys.max())
	return cap_top, boot_bot, boot_bot - cap_top + 1


def torso_center_x(arr: np.ndarray) -> int:
	r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
	opaque = a > 20
	shirt = opaque & (b > 120) & (b > r + 20) & (r < 130)
	ys, xs = np.where(shirt)
	if len(xs) == 0:
		ys, xs = np.where(opaque)
	return int(xs.mean())


def strip_bobber_line(img: np.ndarray) -> np.ndarray:
	"""Remove red/white bobber and thin grey line; keep brown rod + body."""
	out = img.copy()
	h, w = out.shape[:2]
	r, g, b, a = out[:, :, 0], out[:, :, 1], out[:, :, 2], out[:, :, 3]
	opaque = a > 20

	# Wooden rod / reel metal — never delete these.
	is_wood = opaque & (r > 70) & (r > g + 5) & (g > b) & (r < 200) & (b < 100) & (g < 140)
	is_metal = opaque & (np.abs(r.astype(int) - g) < 25) & (np.abs(g.astype(int) - b) < 25) & (r > 140) & (r < 220)
	keep = is_wood | (is_metal & (ndimage.uniform_filter(opaque.astype(float), size=5) > 0.25))

	dens = ndimage.uniform_filter(opaque.astype(float), size=5)
	thick = dens > 0.42
	thick = ndimage.binary_dilation(thick, iterations=2) | keep

	is_red = opaque & (r > 130) & (g < 120) & (b < 120) & (r > g + 25)
	is_grey = (
		opaque
		& (np.abs(r.astype(int) - g) < 30)
		& (np.abs(g.astype(int) - b) < 30)
		& (r > 80)
		& (r < 190)  # exclude brighter metal
	)
	is_whiteish = opaque & (r > 200) & (g > 200) & (b > 200)
	thin = dens < 0.28

	kill = (is_red | (is_whiteish & thin) | (is_grey & thin)) & (~thick) & (~keep)

	lab, n = ndimage.label(opaque & ~kill)
	if n:
		sizes = ndimage.sum(opaque & ~kill, lab, range(1, n + 1))
		main = 1 + int(np.argmax(sizes))
		for i, sz in enumerate(sizes, start=1):
			if i != main and sz < 150:
				# keep wood blobs even if small (rod tip)
				blob = lab == i
				if not np.any(is_wood & blob):
					kill |= blob

	out[kill, 3] = 0

	# Bobber red halves anywhere
	r, g, b, a = out[:, :, 0], out[:, :, 1], out[:, :, 2], out[:, :, 3]
	is_red = (a > 20) & (r > 130) & (g < 120) & (b < 120) & (r > g + 25)
	out[is_red, 3] = 0
	out[out[:, :, 3] == 0, :3] = 0

	# Thin grey filaments only (not wood, not metal reel)
	opaque = out[:, :, 3] > 20
	cx = torso_center_x(out)
	cap_y, boot_y, _ = cap_boot_span(out)
	cy = (cap_y + boot_y) // 2
	yy, xx = np.ogrid[:h, :w]
	dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
	dens = ndimage.uniform_filter(opaque.astype(float), size=5)
	r, g, b, a = out[:, :, 0], out[:, :, 1], out[:, :, 2], out[:, :, 3]
	is_wood = (a > 20) & (r > 70) & (r > g + 5) & (g > b) & (r < 200) & (b < 100)
	is_grey = (
		(a > 20)
		& (np.abs(r.astype(int) - g) < 30)
		& (np.abs(g.astype(int) - b) < 30)
		& (r > 80)
		& (r < 190)
	)
	filament = is_grey & (dens < 0.22) & (dist > 52) & (~is_wood)
	out[filament, 3] = 0
	# Tip nub: bright white/grey at rod tip (last few pixels of wood+white)
	is_tip = (a > 20) & (r > 180) & (g > 180) & (b > 180) & (dens < 0.35) & (~is_wood)
	out[is_tip, 3] = 0
	out[out[:, :, 3] == 0, :3] = 0
	return out


def place_matched(src: np.ndarray, idle_span: int, idle_boot_y: int, bias_left: bool = False) -> np.ndarray:
	cap_y, boot_y, span = cap_boot_span(src)
	scale = idle_span / max(1, span)
	nw = max(1, int(round(src.shape[1] * scale)))
	nh = max(1, int(round(src.shape[0] * scale)))
	scaled = np.array(Image.fromarray(src).resize((nw, nh), Image.Resampling.NEAREST))

	cap_y, boot_y, span = cap_boot_span(scaled)
	cx = torso_center_x(scaled)
	dy = idle_boot_y - boot_y
	dx = TARGET_W // 2 - cx
	if bias_left:
		# Keep more of a forward-pointing rod on-canvas.
		xs = np.where(scaled[:, :, 3] > 0)[1]
		content_right = int(xs.max())
		# Shift left until right edge of content is near canvas right (or body still visible).
		dx = min(dx, TARGET_W - 2 - content_right)
		dx = max(dx, 2 - int(xs.min()))

	canvas = np.zeros((TARGET_H, TARGET_W, 4), dtype=np.uint8)
	ys, xs = np.where(scaled[:, :, 3] > 0)
	yy = ys + dy
	xx = xs + dx
	ok = (yy >= 0) & (yy < TARGET_H) & (xx >= 0) & (xx < TARGET_W)
	canvas[yy[ok], xx[ok]] = scaled[ys[ok], xs[ok]]
	return canvas


def keep_main_sprite(fr: np.ndarray) -> np.ndarray:
	"""Drop detached bobber blobs; keep largest connected sprite."""
	alpha = fr[:, :, 3] > 10
	lab, n = ndimage.label(alpha)
	if n <= 1:
		return fr
	sizes = ndimage.sum(alpha, lab, range(1, n + 1))
	main = 1 + int(np.argmax(sizes))
	out = fr.copy()
	out[lab != main, 3] = 0
	out[out[:, :, 3] == 0, :3] = 0
	ys, xs = np.where(out[:, :, 3] > 10)
	return out[ys.min() : ys.max() + 1, xs.min() : xs.max() + 1]


def extract_frames(sheet: np.ndarray) -> list[tuple[str, np.ndarray]]:
	rgb = sheet[:, :, :3].astype(int)
	bg = (rgb[:, :, 0] > 235) & (rgb[:, :, 1] > 235) & (rgb[:, :, 2] > 235)
	sheet = sheet.copy()
	sheet[bg, 3] = 0
	sheet[sheet[:, :, 3] == 0, :3] = 0

	# Tight splits so mid/throw don't share floating bobbers.
	splits = [(68, 400), (560, 980), (1040, 1536)]
	names = ["hold", "mid", "throw"]
	frames = []
	for (x0, x1), name in zip(splits, names):
		crop = sheet[:, x0:x1]
		ys, xs = np.where(crop[:, :, 3] > 10)
		fr = crop[ys.min() : ys.max() + 1, xs.min() : xs.max() + 1].copy()
		fr = keep_main_sprite(fr)
		frames.append((name, fr))
	return frames


def main() -> None:
	idle = load_rgba(ART / "player_idle_0.png")
	icap, iboot, ispan = cap_boot_span(idle)
	print(f"idle cap={icap} boot={iboot} span={ispan}")

	frames = extract_frames(load_rgba(SHEET))
	built: dict[str, np.ndarray] = {}
	for name, fr in frames:
		cleaned = strip_bobber_line(fr)
		ys, xs = np.where(cleaned[:, :, 3] > 10)
		cleaned = cleaned[ys.min() : ys.max() + 1, xs.min() : xs.max() + 1]
		cleaned = strip_bobber_line(cleaned)

		canvas = place_matched(cleaned, ispan, iboot, bias_left=(name == "throw"))
		canvas = strip_bobber_line(canvas)
		built[name] = canvas

		cap, boot, span = cap_boot_span(canvas)
		red = (
			(canvas[:, :, 0] > 130)
			& (canvas[:, :, 1] < 120)
			& (canvas[:, :, 2] < 120)
			& (canvas[:, :, 3] > 20)
			& (canvas[:, :, 0] > canvas[:, :, 1] + 25)
		)
		print(f"{name}: cap={cap} boot={boot} span={span} red={int(red.sum())}")

	Image.fromarray(built["hold"]).save(ART / "player_cast_hold.png")
	Image.fromarray(built["mid"]).save(ART / "player_cast_mid.png")
	Image.fromarray(built["throw"]).save(ART / "player_cast_throw.png")
	Image.fromarray(built["throw"]).save(ART / "player_fish.png")
	Image.fromarray(built["mid"]).save(ART / "player_hook.png")
	print("ok")


if __name__ == "__main__":
	main()

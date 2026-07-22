"""
Build fishing poses from the real walk/idle player PNGs + a painted rod.

This guarantees the same pixel style as walking, and writes NEW filenames so
s&box texture cache cannot keep serving the old cast/fish/hook paths.
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets" / "textures" / "art"
W, H = 112, 160

# Rod palette
WOOD = (120, 72, 40, 255)
WOOD_DK = (78, 46, 24, 255)
WOOD_LT = (158, 104, 58, 255)
REEL = (170, 176, 184, 255)
REEL_DK = (110, 116, 124, 255)
OUTLINE = (36, 28, 32, 255)


def load(name: str) -> np.ndarray:
	return np.array(Image.open(ART / name).convert("RGBA"))


def save(arr: np.ndarray, name: str) -> None:
	Image.fromarray(arr).save(ART / name)
	print(f"  -> {name}")


def blit(dst: np.ndarray, src: np.ndarray, dx: int = 0, dy: int = 0) -> None:
	ys, xs = np.where(src[:, :, 3] > 20)
	yy, xx = ys + dy, xs + dx
	ok = (yy >= 0) & (yy < H) & (xx >= 0) & (xx < W)
	dst[yy[ok], xx[ok]] = src[ys[ok], xs[ok]]


def clear_rect(arr: np.ndarray, x0: int, y0: int, x1: int, y1: int) -> None:
	arr[max(0, y0) : min(H, y1), max(0, x0) : min(W, x1), :] = 0


def put(arr: np.ndarray, x: int, y: int, rgba) -> None:
	if 0 <= x < W and 0 <= y < H:
		arr[y, x] = rgba


def draw_line(arr: np.ndarray, x0: int, y0: int, x1: int, y1: int, rgba, width: int = 2) -> None:
	steps = max(abs(x1 - x0), abs(y1 - y0), 1)
	for i in range(steps + 1):
		t = i / steps
		x = int(round(x0 + (x1 - x0) * t))
		y = int(round(y0 + (y1 - y0) * t))
		for oy in range(-(width // 2), width // 2 + 1):
			for ox in range(-(width // 2), width // 2 + 1):
				put(arr, x + ox, y + oy, rgba)


def draw_reel(arr: np.ndarray, cx: int, cy: int) -> None:
	for y in range(cy - 4, cy + 5):
		for x in range(cx - 4, cx + 5):
			if (x - cx) ** 2 + (y - cy) ** 2 <= 16:
				put(arr, x, y, REEL_DK if (x - cx) ** 2 + (y - cy) ** 2 > 9 else REEL)


def draw_rod(arr: np.ndarray, hx: int, hy: int, tipx: int, tipy: int, reel_at: float = 0.18) -> None:
	# outline then wood
	draw_line(arr, hx, hy, tipx, tipy, OUTLINE, width=4)
	draw_line(arr, hx, hy, tipx, tipy, WOOD_DK, width=3)
	draw_line(arr, hx, hy, tipx, tipy, WOOD, width=2)
	# tip highlight
	mx = int(hx + (tipx - hx) * 0.72)
	my = int(hy + (tipy - hy) * 0.72)
	draw_line(arr, mx, my, tipx, tipy, WOOD_LT, width=1)
	rx = int(hx + (tipx - hx) * reel_at)
	ry = int(hy + (tipy - hy) * reel_at)
	draw_reel(arr, rx, ry)


def lean(src: np.ndarray, shear: float, dy: int = 0) -> np.ndarray:
	"""Simple horizontal shear for cast lean. shear>0 leans top to the right."""
	out = np.zeros_like(src)
	for y in range(H):
		shift = int(round((y - H * 0.55) * shear)) + 0
		row = src[y]
		xs = np.where(row[:, 3] > 20)[0]
		for x in xs:
			nx = x + shift
			ny = y + dy
			if 0 <= nx < W and 0 <= ny < H:
				out[ny, nx] = row[x]
	return out


def erase_forearms(arr: np.ndarray) -> None:
	"""Clear typical idle forearm/hand region so a held rod reads cleanly."""
	# Right-facing idle: arms hang roughly x 30-78, y 70-115 — clear only lower arm band.
	r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
	skin = (a > 20) & (r > 160) & (g > 110) & (b > 80) & (r > b) & (r > g - 10)
	# Hands / lower arms in mid torso band
	band = np.zeros((H, W), dtype=bool)
	band[72:118, 28:82] = True
	kill = skin & band
	arr[kill] = 0


def pose_hold(base: np.ndarray) -> np.ndarray:
	body = lean(base, shear=-0.04, dy=0)
	erase_forearms(body)
	# hands near shoulder / head — rod back
	draw_rod(body, hx=48, hy=58, tipx=8, tipy=18, reel_at=0.22)
	# simple hands
	for x, y in ((46, 60), (52, 62), (44, 64), (50, 66)):
		put(body, x, y, (220, 170, 130, 255))
		put(body, x + 1, y, (220, 170, 130, 255))
	return body


def pose_mid(base: np.ndarray) -> np.ndarray:
	body = lean(base, shear=0.02, dy=-1)
	erase_forearms(body)
	draw_rod(body, hx=58, hy=48, tipx=96, tipy=10, reel_at=0.2)
	for x, y in ((56, 50), (62, 52), (54, 54), (60, 56)):
		put(body, x, y, (220, 170, 130, 255))
		put(body, x + 1, y, (220, 170, 130, 255))
	return body


def pose_throw(base: np.ndarray) -> np.ndarray:
	body = lean(base, shear=0.06, dy=1)
	erase_forearms(body)
	draw_rod(body, hx=70, hy=78, tipx=108, tipy=70, reel_at=0.18)
	for x, y in ((68, 80), (74, 82), (66, 84), (72, 86)):
		put(body, x, y, (220, 170, 130, 255))
		put(body, x + 1, y, (220, 170, 130, 255))
	return body


def pose_fish(base: np.ndarray) -> np.ndarray:
	body = base.copy()
	erase_forearms(body)
	draw_rod(body, hx=62, hy=88, tipx=104, tipy=118, reel_at=0.2)
	for x, y in ((60, 90), (66, 92), (58, 94), (64, 96)):
		put(body, x, y, (220, 170, 130, 255))
		put(body, x + 1, y, (220, 170, 130, 255))
	return body


def pose_hook(base: np.ndarray) -> np.ndarray:
	body = lean(base, shear=-0.05, dy=2)
	erase_forearms(body)
	# bent rod under tension
	draw_rod(body, hx=54, hy=70, tipx=102, tipy=42, reel_at=0.22)
	# bend mid segment upward
	draw_line(body, 70, 58, 88, 40, WOOD, width=2)
	for x, y in ((52, 72), (58, 74), (50, 76), (56, 78)):
		put(body, x, y, (220, 170, 130, 255))
		put(body, x + 1, y, (220, 170, 130, 255))
	return body


def pose_reel(base: np.ndarray, phase: int) -> np.ndarray:
	body = lean(base, shear=-0.03, dy=phase % 2)
	erase_forearms(body)
	hy = 74 + (1 if phase == 1 else -1 if phase == 2 else 0)
	draw_rod(body, hx=56, hy=hy, tipx=100, tipy=48 + phase, reel_at=0.24)
	for x, y in ((54, hy + 2), (60, hy + 4), (52, hy + 6), (58, hy + 8)):
		put(body, x, y, (220, 170, 130, 255))
		put(body, x + 1, y, (220, 170, 130, 255))
	return body


def main() -> None:
	idle = load("player_idle_0.png")
	walk = load("player_walk_0.png")
	# Prefer idle for planted feet; use walk for a slightly staggered cast stance.
	planted = idle
	cast_base = walk

	jobs = {
		"player_fish_charge.png": pose_hold(cast_base),
		"player_fish_swing.png": pose_mid(cast_base),
		"player_fish_release.png": pose_throw(cast_base),
		"player_fish_wait.png": pose_fish(planted),
		"player_fish_fight.png": pose_hook(planted),
		"player_fish_reel_0.png": pose_reel(planted, 0),
		"player_fish_reel_1.png": pose_reel(planted, 1),
		"player_fish_reel_2.png": pose_reel(planted, 2),
		# Catch result: idle + fish held at waist (simple rod-less keep pose = idle with arms forward blob)
		"player_fish_keep.png": pose_fish(planted),
	}

	print("Painting fishing poses from walk/idle…")
	for name, arr in jobs.items():
		save(arr, name)

	# Also overwrite legacy names so any stray references still update on disk.
	legacy = {
		"player_cast_hold.png": "player_fish_charge.png",
		"player_cast_mid.png": "player_fish_swing.png",
		"player_cast_throw.png": "player_fish_release.png",
		"player_fish.png": "player_fish_wait.png",
		"player_hook.png": "player_fish_fight.png",
		"player_reel_0.png": "player_fish_reel_0.png",
		"player_reel_1.png": "player_fish_reel_1.png",
		"player_reel_2.png": "player_fish_reel_2.png",
		"player_hold.png": "player_fish_keep.png",
	}
	for dst, src in legacy.items():
		Image.open(ART / src).save(ART / dst)
		print(f"  mirrored {dst}")

	print("ok")


if __name__ == "__main__":
	main()

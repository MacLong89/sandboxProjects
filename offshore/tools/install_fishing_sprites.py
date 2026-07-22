"""Install AI fishing hero plates into game sprites with FULL rod visible (no crop)."""
from __future__ import annotations

from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
HEROES = ROOT / "Assets" / "Art" / "heroes"
CURSOR = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects-offshore\assets")
ART = ROOT / "Assets" / "textures" / "art"

# Wide/tall canvas so full rods + tip extensions fit without shrinking the body too far.
TARGET_W, TARGET_H = 256, 200
PAD = 10
TIP_EXTEND = 32
MIN_BODY_FRAC = 0.68  # keep cap→boot span near idle so the player doesn't look tiny

JOBS = [
	# hero → runtime name (v3 busts texture cache)
	("hero_player_cast_hold.png", "player_rod_charge.png"),
	("hero_player_cast_mid.png", "player_rod_swing.png"),
	("hero_player_cast_throw.png", "player_rod_release.png"),
	("hero_player_fish.png", "player_rod_wait.png"),
	("hero_player_hook.png", "player_rod_fight.png"),
	("hero_player_reel.png", "player_rod_reel_0.png"),
]


def find_hero(name: str) -> Path | None:
	for base in (HEROES, CURSOR):
		p = base / name
		if p.exists():
			return p
	return None


def is_bg(r, g, b, a, seed_rgb, tol=38):
	if a < 8:
		return True
	if r > 235 and g > 235 and b > 235:
		return True
	if abs(r - g) < 10 and abs(g - b) < 10 and r > 210:
		return True
	if b > r + 15 and b > g + 10 and b > 140 and r + g + b > 420:
		return True
	if g > r + 10 and g > 120 and b > 140 and r + g + b > 400:
		return True
	if r > 180 and g > 160 and b > 120 and abs(r - g) < 40 and r + g + b > 480:
		return True
	dr, dg, db = abs(r - seed_rgb[0]), abs(g - seed_rgb[1]), abs(b - seed_rgb[2])
	if dr + dg + db < tol * 3 and max(dr, dg, db) < tol + 10:
		return True
	return False


def knock_bg(img: Image.Image) -> Image.Image:
	img = img.convert("RGBA")
	w, h = img.size
	px = img.load()
	seeds = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1), (w // 2, 0), (0, h // 2), (w - 1, h // 2)]
	visited = [[False] * h for _ in range(w)]
	q = deque()
	for sx, sy in seeds:
		r, g, b, a = px[sx, sy]
		seed = (r, g, b)
		if not is_bg(r, g, b, a, seed, tol=55):
			if r + g + b < 500:
				continue
		q.append((sx, sy, seed))
		visited[sx][sy] = True
	while q:
		x, y, seed = q.popleft()
		r, g, b, a = px[x, y]
		if is_bg(r, g, b, a, seed, tol=48):
			px[x, y] = (0, 0, 0, 0)
			for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
				if 0 <= nx < w and 0 <= ny < h and not visited[nx][ny]:
					visited[nx][ny] = True
					q.append((nx, ny, seed))
	for y in range(h):
		for x in range(w):
			r, g, b, a = px[x, y]
			if a and is_bg(r, g, b, a, (255, 255, 255), tol=28):
				px[x, y] = (0, 0, 0, 0)
	return img


def auto_crop(img: Image.Image, pad: int = 8) -> Image.Image:
	bbox = img.split()[-1].getbbox()
	if not bbox:
		return img
	l, t, r, b = bbox
	return img.crop((max(0, l - pad), max(0, t - pad), min(img.width, r + pad), min(img.height, b + pad)))


def strip_bobber_line(arr: np.ndarray) -> np.ndarray:
	out = arr.copy()
	r, g, b, a = out[:, :, 0], out[:, :, 1], out[:, :, 2], out[:, :, 3]
	opaque = a > 20
	is_wood = opaque & (r > 70) & (r > g + 5) & (g > b) & (r < 210) & (b < 110) & (g < 150)
	is_red = opaque & (r > 140) & (g < 110) & (b < 110) & (r > g + 30)
	dens = ndimage.uniform_filter(opaque.astype(float), size=5)
	is_grey_thin = (
		opaque
		& (np.abs(r.astype(int) - g) < 28)
		& (np.abs(g.astype(int) - b) < 28)
		& (r > 80)
		& (r < 190)
		& (dens < 0.22)
	)
	out[is_red, 3] = 0
	out[is_grey_thin & ~is_wood, 3] = 0
	out[out[:, :, 3] == 0, :3] = 0
	return out


def keep_largest(arr: np.ndarray) -> np.ndarray:
	alpha = arr[:, :, 3] > 10
	lab, n = ndimage.label(alpha)
	if n <= 1:
		return arr
	sizes = ndimage.sum(alpha, lab, range(1, n + 1))
	main = 1 + int(np.argmax(sizes))
	out = arr.copy()
	out[lab != main, 3] = 0
	out[out[:, :, 3] == 0, :3] = 0
	return out


def cap_boot_span(arr: np.ndarray) -> tuple[int, int, int]:
	r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
	opaque = a > 20
	cap = opaque & (b > 120) & (b > r + 25) & (b > g + 8) & (r < 140)
	boot = opaque & (r > 45) & (r > g) & (g > b) & (r < 210) & (b < 90) & (g < 130)
	cap_ys = np.where(cap.any(axis=1))[0]
	boot_ys = np.where(boot.any(axis=1))[0]
	if len(cap_ys) == 0 or len(boot_ys) == 0:
		ys = np.where(opaque.any(axis=1))[0]
		return int(ys.min()), int(ys.max()), int(ys.max() - ys.min() + 1)
	h = arr.shape[0]
	cap_top = int(cap_ys[cap_ys < h * 0.55].min()) if np.any(cap_ys < h * 0.55) else int(cap_ys.min())
	boot_bot = int(boot_ys[boot_ys > h * 0.4].max()) if np.any(boot_ys > h * 0.4) else int(boot_ys.max())
	return cap_top, boot_bot, boot_bot - cap_top + 1


def prep_hero(src: Path) -> np.ndarray:
	img = knock_bg(Image.open(src).convert("RGBA"))
	img = auto_crop(img, 12)
	arr = strip_bobber_line(np.array(img))
	arr = keep_largest(arr)
	ys, xs = np.where(arr[:, :, 3] > 10)
	arr = arr[ys.min() : ys.max() + 1, xs.min() : xs.max() + 1]
	arr = strip_bobber_line(arr)
	return arr


def torso_center(arr: np.ndarray) -> tuple[int, int]:
	r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
	opaque = a > 20
	shirt = opaque & (b > 110) & (b > r + 15) & (r < 150)
	ys, xs = np.where(shirt)
	if len(xs) == 0:
		ys, xs = np.where(opaque)
	return int(xs.mean()), int(ys.mean())


def wood_mask(arr: np.ndarray) -> np.ndarray:
	r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
	opaque = a > 20
	return opaque & (r > 70) & (r > g + 5) & (g > b) & (r < 210) & (b < 110) & (g < 150)


def extend_rod_tip(arr: np.ndarray, extend: int = TIP_EXTEND) -> np.ndarray:
	"""Paint a tapered tip past the farthest wood pixel so rods don't end in a flat crop."""
	out = arr.copy()
	h, w = out.shape[:2]
	wood = wood_mask(out)
	ys, xs = np.where(wood)
	if len(xs) < 8:
		return out

	cx, cy = torso_center(out)
	# Ignore boot/belt browns in the lower body when finding the rod tip.
	upper = ys < int(h * 0.72)
	if np.count_nonzero(upper) >= 8:
		xs, ys = xs[upper], ys[upper]
	dist = (xs - cx) ** 2 + (ys - cy) ** 2
	order = np.argsort(dist)[-12:]
	tip_x = int(np.mean(xs[order]))
	tip_y = int(np.mean(ys[order]))
	vx, vy = tip_x - cx, tip_y - cy
	length = max(1.0, (vx * vx + vy * vy) ** 0.5)
	ux, uy = vx / length, vy / length

	# How far can we go before hitting the padded canvas edge?
	max_i = extend
	for i in range(1, extend + 8):
		px = tip_x + ux * i
		py = tip_y + uy * i
		if px < PAD or py < PAD or px > w - 1 - PAD or py > h - 1 - PAD:
			max_i = max(4, i - 1)
			break
	else:
		max_i = extend

	wood_colors = [
		(120, 72, 40, 255),
		(158, 104, 58, 255),
		(78, 46, 24, 255),
	]
	for i in range(1, max_i + 1):
		half = max(0, 2 - i // max(6, max_i // 3))
		px = int(round(tip_x + ux * i))
		py = int(round(tip_y + uy * i))
		col = wood_colors[0] if i < max_i - 3 else wood_colors[1]
		for oy in range(-half, half + 1):
			for ox in range(-half, half + 1):
				x, y = px + ox, py + oy
				if PAD <= x < w - PAD and PAD <= y < h - PAD and out[y, x, 3] < 20:
					out[y, x] = col
		if half >= 1 and i % 2 == 0:
			for ox, oy in ((-half - 1, 0), (half + 1, 0), (0, -half - 1), (0, half + 1)):
				x, y = px + ox, py + oy
				if PAD <= x < w - PAD and PAD <= y < h - PAD and out[y, x, 3] < 20:
					out[y, x] = wood_colors[2]
	return out


def place_fit_all(src: np.ndarray, idle_span: int) -> np.ndarray:
	"""
	Scale the FULL sprite (body + rod) to fit inside the canvas with reserved tip margin.
	Never crop — shrink until everything fits, bottom-align boots, then extend rod tip.
	"""
	sh, sw = src.shape[:2]
	_, _, src_body = cap_boot_span(src)
	max_w = TARGET_W - PAD * 2 - TIP_EXTEND - 20
	max_h = TARGET_H - PAD * 2 - TIP_EXTEND // 2 - 12
	fit_scale = min(max_w / max(1, sw), max_h / max(1, sh)) * 0.9
	body_scale = (idle_span * MIN_BODY_FRAC) / max(1, src_body)
	scale = min(fit_scale, body_scale)
	nw = max(1, int(round(sw * scale)))
	nh = max(1, int(round(sh * scale)))
	scaled = np.array(Image.fromarray(src).resize((nw, nh), Image.Resampling.BOX))
	scaled[:, :, 3] = np.where(scaled[:, :, 3] < 80, 0, 255).astype(np.uint8)
	scaled[scaled[:, :, 3] == 0, :3] = 0

	xs = np.where(scaled[:, :, 3] > 0)[1]
	ys = np.where(scaled[:, :, 3] > 0)[0]
	content_left, content_right = int(xs.min()), int(xs.max())
	content_top, content_bot = int(ys.min()), int(ys.max())

	_, boot_y, _ = cap_boot_span(scaled)
	target_boot = TARGET_H - PAD - 2
	dy = target_boot - boot_y
	dx = (TARGET_W - (content_right - content_left + 1)) // 2 - content_left

	tcx, tcy = torso_center(scaled)
	wood = wood_mask(scaled)
	wys, wxs = np.where(wood)
	tip_room = TIP_EXTEND + PAD + 12
	if len(wxs):
		upper = wys < int(scaled.shape[0] * 0.72)
		if np.count_nonzero(upper) >= 8:
			wxs, wys = wxs[upper], wys[upper]
		d = (wxs - tcx) ** 2 + (wys - tcy) ** 2
		fx = int(wxs[np.argmax(d)])
		if fx >= tcx:
			dx = PAD + 8 - content_left
		else:
			dx = TARGET_W - PAD - 8 - content_right

	dx = min(dx, PAD - content_left)
	dx = max(dx, PAD - content_left)
	dx = min(dx, TARGET_W - tip_room - (content_right - content_left) - content_left)
	dx = max(dx, PAD - content_left)
	dx = min(dx, TARGET_W - PAD - 1 - content_right)
	dy = min(dy, PAD - content_top)
	dy = max(dy, TARGET_H - PAD - 1 - content_bot)

	canvas = np.zeros((TARGET_H, TARGET_W, 4), dtype=np.uint8)
	yy = ys + dy
	xx = xs + dx
	ok = (yy >= 0) & (yy < TARGET_H) & (xx >= 0) & (xx < TARGET_W)
	if not np.all(ok):
		shrunk = np.array(
			Image.fromarray(src).resize((max(1, int(sw * 0.9)), max(1, int(sh * 0.9))), Image.Resampling.BOX)
		)
		return place_fit_all(shrunk, idle_span)
	canvas[yy, xx] = scaled[ys, xs]
	canvas = extend_rod_tip(canvas)
	return tight_crop(canvas, pad=10)


def tight_crop(arr: np.ndarray, pad: int = 6) -> np.ndarray:
	"""Trim empty padding so FitHeight uses the real character+rod, not a huge blank canvas."""
	ys, xs = np.where(arr[:, :, 3] > 20)
	if len(xs) == 0:
		return arr
	x0 = max(0, int(xs.min()) - pad)
	y0 = max(0, int(ys.min()) - pad)
	x1 = min(arr.shape[1], int(xs.max()) + pad + 1)
	y1 = min(arr.shape[0], int(ys.max()) + pad + 1)
	return arr[y0:y1, x0:x1].copy()


def tip_report(arr: np.ndarray) -> dict:
	op = arr[:, :, 3] > 20
	wood = wood_mask(arr)
	h, w = arr.shape[:2]
	ys, xs = np.where(wood)
	if len(xs) == 0:
		return {"ok": False, "reason": "no wood"}
	cx, cy = torso_center(arr)
	upper = ys < int(h * 0.72)
	if np.count_nonzero(upper) >= 8:
		xs, ys = xs[upper], ys[upper]
	i = int(np.argmax((xs - cx) ** 2 + (ys - cy) ** 2))
	tx, ty = int(xs[i]), int(ys[i])
	margin = min(tx, ty, w - 1 - tx, h - 1 - ty)
	# Only treat as edge-hit if opaque pixels sit on the border band.
	edge_hit = bool(op[:, :2].any() or op[:, -2:].any() or op[:2, :].any())
	return {
		"ok": margin >= 6 and not edge_hit,
		"tip": (tx, ty),
		"margin": margin,
		"edge_hit": edge_hit,
		"opaque": int(op.sum()),
	}


def main() -> None:
	idle = np.array(Image.open(ART / "player_idle_0.png").convert("RGBA"))
	_, iboot, ispan = cap_boot_span(idle)
	print(f"idle boot={iboot} span={ispan} canvas={TARGET_W}x{TARGET_H}")

	failed = []
	for hero_name, out_name in JOBS:
		src = find_hero(hero_name)
		if src is None:
			print(f"MISSING {hero_name}")
			failed.append(out_name)
			continue
		arr = prep_hero(src)
		canvas = place_fit_all(arr, ispan)
		Image.fromarray(canvas).save(ART / out_name)
		cap, boot, span = cap_boot_span(canvas)
		rep = tip_report(canvas)
		print(f"{out_name}: cap={cap} boot={boot} span={span} tip={rep}")
		if not rep.get("ok"):
			failed.append(out_name)

	# Reel bob variants
	reel0 = ART / "player_rod_reel_0.png"
	if reel0.exists():
		base = np.array(Image.open(reel0).convert("RGBA"))
		for i, (dx, dy) in enumerate(((0, 0), (1, -2), (-1, 2))):
			frame = np.zeros_like(base)
			ys, xs = np.where(base[:, :, 3] > 0)
			yy, xx = ys + dy, xs + dx
			ok = (yy >= 0) & (yy < TARGET_H) & (xx >= 0) & (xx < TARGET_W)
			frame[yy[ok], xx[ok]] = base[ys[ok], xs[ok]]
			# Re-extend tip after bob so variants stay complete.
			frame = extend_rod_tip(frame, extend=TIP_EXTEND // 2)
			Image.fromarray(frame).save(ART / f"player_rod_reel_{i}.png")
		print("player_rod_reel_0..2")

	wait = ART / "player_rod_wait.png"
	if wait.exists():
		Image.open(wait).save(ART / "player_rod_keep.png")
		print("player_rod_keep.png")

	if failed:
		print("FAILED tip checks:", ", ".join(failed))
		raise SystemExit(1)
	print("ok — all rods have tip margin")


if __name__ == "__main__":
	main()

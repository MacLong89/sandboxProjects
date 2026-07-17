"""
Convert AI-generated sprites with fake checkerboard/solid backgrounds into true RGBA alpha PNGs.
"""
from __future__ import annotations

import sys
from collections import deque
from pathlib import Path

from PIL import Image


def is_background(r: int, g: int, b: int, a: int) -> bool:
	"""Treat near-white, light-gray, checker tiles, and magenta key as background."""
	if a < 8:
		return True
	# Magenta / near-magenta chroma key (high R+B, low G)
	if r > 200 and b > 200 and g < 80:
		return True
	mx, mn = max(r, g, b), min(r, g, b)
	sat = mx - mn
	# Low-saturation light pixels = UI checkerboard / paper fill
	if mx >= 175 and sat <= 28:
		return True
	# Mid light-gray checker cell (often ~190-230)
	avg = (r + g + b) / 3
	if avg >= 185 and sat <= 22:
		return True
	# Soft shadow checker cell (~140-180 gray)
	if 140 <= avg <= 200 and sat <= 18 and mn >= 130:
		return True
	return False


def flood_clear_background(im: Image.Image) -> Image.Image:
	im = im.convert("RGBA")
	w, h = im.size
	px = im.load()
	visited = [[False] * w for _ in range(h)]
	q: deque[tuple[int, int]] = deque()

	def try_seed(x: int, y: int) -> None:
		if visited[y][x]:
			return
		r, g, b, a = px[x, y]
		if is_background(r, g, b, a):
			q.append((x, y))
			visited[y][x] = True

	# Seed from every edge pixel that looks like background
	for x in range(w):
		try_seed(x, 0)
		try_seed(x, h - 1)
	for y in range(h):
		try_seed(0, y)
		try_seed(w - 1, y)

	# Also seed a dense grid of obvious bg pixels connected via later neighbor walk —
	# keeps interior checker holes if they touch edges through flood.
	while q:
		x, y = q.popleft()
		px[x, y] = (0, 0, 0, 0)
		for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
			if nx < 0 or ny < 0 or nx >= w or ny >= h or visited[ny][nx]:
				continue
			r, g, b, a = px[nx, ny]
			if is_background(r, g, b, a):
				visited[ny][nx] = True
				q.append((nx, ny))

	# Second pass: clear any remaining orphan checker pixels (not connected to edges)
	# that are clearly bg and surrounded mostly by transparent/bg.
	for y in range(h):
		for x in range(w):
			r, g, b, a = px[x, y]
			if a == 0:
				continue
			if not is_background(r, g, b, a):
				continue
			# Only strip leftover bg if local neighborhood is mostly already clear/bg
			bg_n = 0
			tot = 0
			for ny in range(max(0, y - 2), min(h, y + 3)):
				for nx in range(max(0, x - 2), min(w, x + 3)):
					tot += 1
					rr, gg, bb, aa = px[nx, ny]
					if aa < 8 or is_background(rr, gg, bb, aa):
						bg_n += 1
			if tot > 0 and bg_n / tot >= 0.55:
				px[x, y] = (0, 0, 0, 0)

	return im


def trim_transparent(im: Image.Image, pad: int = 2) -> Image.Image:
	bbox = im.getbbox()
	if not bbox:
		return im
	l, t, r, b = bbox
	l = max(0, l - pad)
	t = max(0, t - pad)
	r = min(im.width, r + pad)
	b = min(im.height, b + pad)
	return im.crop((l, t, r, b))


def process(src: Path, dst: Path) -> None:
	im = Image.open(src)
	out = flood_clear_background(im)
	out = trim_transparent(out)
	dst.parent.mkdir(parents=True, exist_ok=True)
	out.save(dst, "PNG")
	# Verify alpha usage
	alphas = out.getchannel("A")
	amin, amax = alphas.getextrema()
	data = list(alphas.get_flattened_data())
	opaque = sum(1 for p in data if p > 200)
	clear = sum(1 for p in data if p < 10)
	print(f"{dst.name}: size={out.size} alpha_range=({amin},{amax}) opaque~{opaque} clear~{clear}")


def main() -> int:
	src_dir = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects-deep\assets")
	out_root = Path(r"C:\Users\Macra\Projects\sandboxProjects\deep\Assets\textures")
	mapping = {
		"idle.png": out_root / "diver" / "idle.png",
		"swim.png": out_root / "diver" / "swim.png",
		"swim_up.png": out_root / "diver" / "swim_up.png",
		"swim_down.png": out_root / "diver" / "swim_down.png",
		"boat.png": out_root / "world" / "boat.png",
		"seaweed.png": out_root / "world" / "seaweed.png",
		"ruins.png": out_root / "world" / "ruins.png",
		"rocks.png": out_root / "world" / "rocks.png",
	}
	missing = [n for n in mapping if not (src_dir / n).exists()]
	if missing:
		print("MISSING sources:", ", ".join(missing))
		return 1
	for name, dst in mapping.items():
		process(src_dir / name, dst)

	# Concept ref copy (not runtime)
	ref_src = src_dir / "c__Users_Macra_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_image-d21113f9-2006-4336-bd7a-1042b92effb6.png"
	ref_dst = Path(r"C:\Users\Macra\Projects\sandboxProjects\deep\Assets\refs\concept_deep.png")
	if ref_src.exists():
		ref_dst.parent.mkdir(parents=True, exist_ok=True)
		Image.open(ref_src).save(ref_dst, "PNG")
		print(f"ref copied -> {ref_dst}")
	return 0


if __name__ == "__main__":
	sys.exit(main())

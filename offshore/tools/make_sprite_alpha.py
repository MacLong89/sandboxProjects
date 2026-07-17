#!/usr/bin/env python3
"""Convert generated sprites into true RGBA PNGs.

1) Flood-key edge backdrop color.
2) Globally chroma-key hotspot magenta (#FF00FF) and near-magenta leftovers
   trapped in enclosed gaps (flood alone misses these and they show as pink in-game).
"""

from __future__ import annotations

import argparse
import sys
from collections import deque
from pathlib import Path

from PIL import Image

MAGENTA = (255, 0, 255)


def dist_rgb(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
	dr = a[0] - b[0]
	dg = a[1] - b[1]
	db = a[2] - b[2]
	return (dr * dr + dg * dg + db * db) ** 0.5


def nearly_bg(pixel: tuple[int, int, int, int], bg: tuple[int, int, int], thresh: float) -> bool:
	return dist_rgb(pixel[:3], bg) <= thresh


def is_magentaish(rgb: tuple[int, int, int], thresh: float) -> bool:
	# Hot magenta + purple-pink leftovers common in gen fills.
	if dist_rgb(rgb, MAGENTA) <= thresh:
		return True
	r, g, b = rgb
	# High R+B, very low G (classic keyed leftover)
	if r >= 200 and b >= 180 and g <= 80 and abs(r - b) <= 90:
		return True
	if r >= 220 and b >= 120 and g <= 40:
		return True
	return False


def flood_alpha(im: Image.Image, thresh: float, edge_feather: int, magenta_thresh: float) -> Image.Image:
	rgba = im.convert("RGBA")
	w, h = rgba.size
	px = rgba.load()

	samples = [
		px[0, 0][:3],
		px[w - 1, 0][:3],
		px[0, h - 1][:3],
		px[w - 1, h - 1][:3],
		px[w // 2, 0][:3],
		px[0, h // 2][:3],
		px[w - 1, h // 2][:3],
		px[w // 2, h - 1][:3],
	]
	bg = tuple(sum(c[i] for c in samples) // len(samples) for i in range(3))

	visited = [[False] * w for _ in range(h)]
	transparent = [[False] * w for _ in range(h)]
	q: deque[tuple[int, int]] = deque()

	seeds = [
		(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1),
		(w // 2, 0), (0, h // 2), (w - 1, h // 2), (w // 2, h - 1),
		(1, 1), (w - 2, 1), (1, h - 2), (w - 2, h - 2),
	]
	for x, y in seeds:
		if 0 <= x < w and 0 <= y < h and nearly_bg(px[x, y], bg, thresh):
			q.append((x, y))
			visited[y][x] = True
			transparent[y][x] = True

	while q:
		x, y = q.popleft()
		for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
			if nx < 0 or ny < 0 or nx >= w or ny >= h or visited[ny][nx]:
				continue
			visited[ny][nx] = True
			if nearly_bg(px[nx, ny], bg, thresh):
				transparent[ny][nx] = True
				q.append((nx, ny))

	# Global chroma pass — kill enclosed magenta pockets the flood never reaches.
	for y in range(h):
		for x in range(w):
			if transparent[y][x]:
				continue
			if is_magentaish(px[x, y][:3], magenta_thresh):
				transparent[y][x] = True

	out = Image.new("RGBA", (w, h))
	out_px = out.load()
	for y in range(h):
		for x in range(w):
			r, g, b, a = px[x, y]
			if transparent[y][x]:
				out_px[x, y] = (r, g, b, 0)
			else:
				out_px[x, y] = (r, g, b, 255)

	if edge_feather > 0:
		soft = out.copy()
		s = soft.load()
		for y in range(h):
			for x in range(w):
				if transparent[y][x]:
					continue
				neigh = 0
				clear = 0
				for dy in range(-edge_feather, edge_feather + 1):
					for dx in range(-edge_feather, edge_feather + 1):
						nx, ny = x + dx, y + dy
						if nx < 0 or ny < 0 or nx >= w or ny >= h:
							continue
						neigh += 1
						if transparent[ny][nx]:
							clear += 1
				if clear > 0:
					r, g, b, _ = s[x, y]
					alpha = max(0, 255 - int(255 * (clear / max(1, neigh)) * 1.6))
					s[x, y] = (r, g, b, alpha)
		out = soft

	return out


def report(path: Path, im: Image.Image) -> None:
	rgba = im.convert("RGBA")
	hist = rgba.getchannel("A").histogram()
	# Count leftover near-magenta opaque pixels
	px = rgba.load()
	w, h = rgba.size
	mag = 0
	for y in range(0, h, 2):
		for x in range(0, w, 2):
			r, g, b, a = px[x, y]
			if a > 200 and is_magentaish((r, g, b), 90):
				mag += 1
	print(
		f"{path.name}: zero={hist[0]} full={hist[255]} leftover_magenta~{mag*4}"
	)


def process_one(src: Path, dst: Path, thresh: float, feather: int, magenta_thresh: float) -> None:
	im = Image.open(src)
	out = flood_alpha(im, thresh=thresh, edge_feather=feather, magenta_thresh=magenta_thresh)
	dst.parent.mkdir(parents=True, exist_ok=True)
	out.save(dst, "PNG")
	report(dst, out)


def main() -> int:
	parser = argparse.ArgumentParser()
	parser.add_argument("inputs", nargs="+", type=Path)
	parser.add_argument("--out-dir", type=Path, required=True)
	parser.add_argument("--thresh", type=float, default=42.0)
	parser.add_argument("--feather", type=int, default=1)
	parser.add_argument("--magenta-thresh", type=float, default=95.0)
	args = parser.parse_args()

	for src in args.inputs:
		if not src.exists():
			print(f"MISSING {src}", file=sys.stderr)
			continue
		process_one(src, args.out_dir / src.name, args.thresh, args.feather, args.magenta_thresh)
	return 0


if __name__ == "__main__":
	raise SystemExit(main())

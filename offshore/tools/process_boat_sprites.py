"""Convert generated boat sprites to true RGBA with edge-flood transparency."""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image

SRC = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects-offshore\assets")
DST_PROPS = Path(r"C:\Users\Macra\Projects\sandboxProjects\offshore\Assets\textures\props")
DST_BOATS = DST_PROPS / "boats"

JOBS = [
	("gen_rowboat_empty.png", DST_PROPS / "rowboat.png"),
	("gen_bay_boat_empty.png", DST_PROPS / "boat.png"),
	("gen_bay_boat_empty.png", DST_BOATS / "bay_boat.png"),
	("gen_sport_fisher_empty.png", DST_BOATS / "sport_fisher.png"),
	("gen_trawler_empty.png", DST_BOATS / "trawler.png"),
	("gen_rowboat_boarded.png", DST_BOATS / "rowboat_boarded.png"),
	("gen_bay_boat_boarded.png", DST_BOATS / "bay_boat_boarded.png"),
	("gen_sport_fisher_boarded.png", DST_BOATS / "sport_fisher_boarded.png"),
	("gen_trawler_boarded.png", DST_BOATS / "trawler_boarded.png"),
]


def is_bg(r: int, g: int, b: int, a: int) -> bool:
	if a < 8:
		return True
	# Solid black canvas from the generator
	if r <= 24 and g <= 24 and b <= 24:
		return True
	# Occasional white canvas
	if r >= 248 and g >= 248 and b >= 248:
		return True
	return False


def flood_clear(img: Image.Image) -> Image.Image:
	img = img.convert("RGBA")
	pix = list(img.getdata())
	w, h = img.size

	def at(x: int, y: int) -> int:
		return y * w + x

	visited = bytearray(w * h)
	q: deque[int] = deque()

	def seed(x: int, y: int) -> None:
		i = at(x, y)
		r, g, b, a = pix[i]
		if is_bg(r, g, b, a):
			q.append(i)

	for x in range(w):
		seed(x, 0)
		seed(x, h - 1)
	for y in range(h):
		seed(0, y)
		seed(w - 1, y)

	cleared = 0
	while q:
		i = q.popleft()
		if visited[i]:
			continue
		r, g, b, a = pix[i]
		if not is_bg(r, g, b, a):
			continue
		visited[i] = 1
		pix[i] = (0, 0, 0, 0)
		cleared += 1
		x, y = i % w, i // w
		for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
			if 0 <= nx < w and 0 <= ny < h and not visited[at(nx, ny)]:
				q.append(at(nx, ny))

	# One-pass soft fringe: near-black touching cleared alpha only (don't eat navy hull).
	fringe = 0
	for y in range(h):
		for x in range(w):
			i = at(x, y)
			r, g, b, a = pix[i]
			if a == 0:
				continue
			if not (r <= 32 and g <= 32 and b <= 32):
				continue
			touch = False
			for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
				if 0 <= nx < w and 0 <= ny < h and pix[at(nx, ny)][3] == 0:
					touch = True
					break
			if touch:
				pix[i] = (0, 0, 0, 0)
				fringe += 1

	img.putdata(pix)
	print(f"  cleared={cleared} fringe={fringe}")
	return img


def crop_to_alpha(img: Image.Image, pad: int = 6) -> Image.Image:
	bbox = img.split()[-1].getbbox()
	if not bbox:
		return img
	x0, y0, x1, y1 = bbox
	return img.crop(
		(
			max(0, x0 - pad),
			max(0, y0 - pad),
			min(img.width, x1 + pad),
			min(img.height, y1 + pad),
		)
	)


def verify(img: Image.Image) -> str:
	w, h = img.size
	pix = img.load()
	corners = [pix[0, 0], pix[w - 1, 0], pix[0, h - 1], pix[w - 1, h - 1]]
	opaque_corners = sum(1 for c in corners if c[3] > 10)
	transparent = img.split()[-1].histogram()[0]
	return f"{w}x{h} transparent={100.0 * transparent / (w * h):.1f}% opaque_corners={opaque_corners}"


def process(src: Path, dst: Path) -> None:
	img = flood_clear(Image.open(src))
	img = crop_to_alpha(img)
	dst.parent.mkdir(parents=True, exist_ok=True)
	img.save(dst, "PNG")
	print(f"OK {dst.name}: {verify(img)}")


def main() -> None:
	DST_BOATS.mkdir(parents=True, exist_ok=True)
	for name, dst in JOBS:
		src = SRC / name
		if not src.exists():
			print(f"MISSING {src}")
			continue
		print(name)
		process(src, dst)


if __name__ == "__main__":
	main()

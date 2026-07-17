from pathlib import Path
from PIL import Image
from collections import deque

SRC = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects-deep\assets")
DST = Path(r"C:\Users\Macra\Projects\sandboxProjects\deep\Assets\textures\ui")

NAMES = ["icon_oxygen.png", "icon_pressure.png", "icon_health.png", "icon_boost.png"]


def is_bg(r, g, b, a):
	if a < 8:
		return True
	mx, mn = max(r, g, b), min(r, g, b)
	sat = mx - mn
	avg = (r + g + b) / 3
	if mx >= 175 and sat <= 28:
		return True
	if avg >= 185 and sat <= 22:
		return True
	if 140 <= avg <= 200 and sat <= 18 and mn >= 130:
		return True
	return False


def clear_bg(im: Image.Image) -> Image.Image:
	im = im.convert("RGBA")
	w, h = im.size
	px = im.load()
	visited = [[False] * w for _ in range(h)]
	q = deque()

	def seed(x, y):
		if visited[y][x]:
			return
		r, g, b, a = px[x, y]
		if is_bg(r, g, b, a):
			visited[y][x] = True
			q.append((x, y))

	for x in range(w):
		seed(x, 0)
		seed(x, h - 1)
	for y in range(h):
		seed(0, y)
		seed(w - 1, y)

	while q:
		x, y = q.popleft()
		px[x, y] = (0, 0, 0, 0)
		for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
			if 0 <= nx < w and 0 <= ny < h and not visited[ny][nx]:
				r, g, b, a = px[nx, ny]
				if is_bg(r, g, b, a):
					visited[ny][nx] = True
					q.append((nx, ny))

	bbox = im.getbbox()
	if bbox:
		im = im.crop(bbox)
	# Normalize to square-ish HUD size
	im.thumbnail((128, 128), Image.Resampling.LANCZOS)
	return im


def main():
	DST.mkdir(parents=True, exist_ok=True)
	for name in NAMES:
		src = SRC / name
		if not src.exists():
			print("MISSING", name)
			continue
		out = clear_bg(Image.open(src))
		dst = DST / name
		out.save(dst, "PNG")
		print(name, out.size, "corner", out.getpixel((0, 0)))


if __name__ == "__main__":
	main()

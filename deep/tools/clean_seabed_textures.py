"""Clean DEEP seabed textures: strip magenta fringe, tighten alpha."""
from PIL import Image
import os

WORLD = os.path.normpath(
	os.path.join(os.path.dirname(__file__), "..", "Assets", "textures", "world")
)


def is_magenta(r, g, b, a):
	if a < 8:
		return False
	# Hot pink / key fringe leftover from generation
	return r >= 160 and b >= 160 and g <= 100


def clean(path, soft_black_to_alpha=False, black_thresh=28):
	im = Image.open(path).convert("RGBA")
	px = im.load()
	w, h = im.size
	removed_mag = 0
	removed_black = 0
	for y in range(h):
		for x in range(w):
			r, g, b, a = px[x, y]
			if is_magenta(r, g, b, a):
				px[x, y] = (0, 0, 0, 0)
				removed_mag += 1
				continue
			if soft_black_to_alpha and a > 0 and r <= black_thresh and g <= black_thresh and b <= black_thresh:
				# Only clear near-edge near-black that was meant as matte, keep dark rock interiors
				# by requiring neighbor mostly transparent
				edgeish = x < 4 or y < 4 or x >= w - 4 or y >= h - 4
				if edgeish:
					px[x, y] = (0, 0, 0, 0)
					removed_black += 1
	im.save(path, "PNG")
	print(f"{os.path.basename(path)}: magenta={removed_mag} edgeblack={removed_black}")


def main():
	clean(os.path.join(WORLD, "seabed_fill.png"))
	clean(os.path.join(WORLD, "seabed_ridge.png"))
	clean(os.path.join(WORLD, "seabed_chunk.png"))
	print("done")


if __name__ == "__main__":
	main()

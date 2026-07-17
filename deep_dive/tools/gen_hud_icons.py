"""Regenerate cleaner hotbar icons with true RGBA transparency."""
from PIL import Image, ImageDraw
import math
import os

OUT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "Assets", "ui", "icons"))
SIZE = 128


def new_img():
	return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def save(im, name):
	path = os.path.join(OUT, name)
	# Ensure fully transparent corners
	im = im.convert("RGBA")
	im.save(path, "PNG")
	print("wrote", name)


def circle(d, c, r, fill, outline=None, w=2):
	x, y = c
	d.ellipse([x - r, y - r, x + r, y + r], fill=fill, outline=outline, width=w)


def main():
	os.makedirs(OUT, exist_ok=True)

	# Fins — clearer silhouette
	im = new_img(); d = ImageDraw.Draw(im)
	for dx, m in ((-24, 1), (24, -1)):
		blade = [(64 + dx, 22), (64 + dx + m * 20, 34), (64 + dx + m * 24, 92),
				 (64 + dx + m * 6, 110), (64 + dx - m * 8, 98), (64 + dx - m * 12, 40)]
		d.polygon(blade, fill=(36, 170, 195, 255))
		d.polygon([(64 + dx - m * 4, 48), (64 + dx + m * 10, 52), (64 + dx + m * 12, 86), (64 + dx - m * 2, 82)],
				  fill=(18, 110, 135, 255))
		d.ellipse([64 + dx - 14, 16, 64 + dx + 14, 42], fill=(235, 245, 250, 255))
	save(im, "tool_fins.png")

	# Harpoon
	im = new_img(); d = ImageDraw.Draw(im)
	d.polygon([(24, 82), (98, 34), (104, 44), (30, 92)], fill=(190, 200, 210, 255))
	d.polygon([(98, 34), (116, 24), (110, 50)], fill=(230, 235, 240, 255))
	d.rounded_rectangle([18, 74, 44, 100], radius=8, fill=(110, 80, 45, 255))
	save(im, "tool_harpoon.png")

	# Scanner
	im = new_img(); d = ImageDraw.Draw(im)
	circle(d, (50, 50), 30, (70, 170, 220, 70), (100, 210, 245, 255), 7)
	d.line([(74, 74), (108, 108)], fill=(210, 220, 230, 255), width=11)
	circle(d, (50, 50), 12, (160, 230, 255, 140))
	save(im, "tool_scanner.png")

	# Camera
	im = new_img(); d = ImageDraw.Draw(im)
	d.rounded_rectangle([20, 42, 108, 98], radius=14, fill=(48, 58, 72, 255))
	d.rounded_rectangle([38, 30, 72, 46], radius=5, fill=(72, 82, 96, 255))
	circle(d, (64, 70), 22, (28, 130, 175, 255), (190, 225, 245, 255), 4)
	circle(d, (64, 70), 9, (190, 235, 255, 220))
	d.ellipse([90, 50, 102, 62], fill=(255, 200, 55, 255))
	save(im, "tool_camera.png")

	# Oxygen
	im = new_img(); d = ImageDraw.Draw(im)
	d.rounded_rectangle([46, 30, 82, 110], radius=16, fill=(45, 145, 215, 255))
	d.rectangle([54, 18, 74, 34], fill=(170, 180, 190, 255))
	d.ellipse([56, 8, 72, 22], fill=(130, 140, 150, 255))
	d.rounded_rectangle([54, 44, 74, 92], radius=8, fill=(140, 215, 255, 200))
	save(im, "tool_oxygen.png")

	# Drone — clearer yellow quadcopter
	im = new_img(); d = ImageDraw.Draw(im)
	for cx, cy in ((30, 34), (98, 34), (30, 94), (98, 94)):
		circle(d, (cx, cy), 14, (210, 215, 225, 230), (150, 155, 165, 255), 2)
		d.line([(cx, cy), (64, 64)], fill=(90, 95, 105, 255), width=4)
	d.rounded_rectangle([42, 48, 86, 86], radius=10, fill=(245, 195, 40, 255))
	circle(d, (54, 66), 7, (35, 45, 55, 255))
	circle(d, (74, 66), 7, (35, 45, 55, 255))
	save(im, "tool_drone.png")

	# Lure
	im = new_img(); d = ImageDraw.Draw(im)
	d.ellipse([32, 22, 96, 80], fill=(230, 75, 95, 255))
	d.ellipse([44, 32, 70, 54], fill=(255, 170, 180, 170))
	for x in (46, 58, 70, 82):
		d.line([(x, 74), (x - 3, 112)], fill=(200, 55, 75, 255), width=5)
		d.ellipse([x - 9, 106, x + 1, 118], fill=(230, 85, 105, 230))
	save(im, "tool_lure.png")

	# Sub
	im = new_img(); d = ImageDraw.Draw(im)
	d.ellipse([16, 42, 112, 98], fill=(245, 190, 40, 255))
	d.ellipse([34, 52, 70, 88], fill=(35, 115, 165, 255))
	d.ellipse([42, 58, 58, 74], fill=(165, 225, 255, 210))
	d.polygon([(100, 58), (118, 48), (118, 92), (100, 82)], fill=(210, 155, 30, 255))
	d.rectangle([48, 30, 64, 46], fill=(200, 155, 30, 255))
	save(im, "tool_sub.png")

	print("hotbar icons refreshed")


if __name__ == "__main__":
	main()
